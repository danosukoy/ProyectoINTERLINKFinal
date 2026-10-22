using System.Collections.Generic;

/// <summary>
/// Caja 1: modelo puro de TDM 6v6 sin fuego amigo (sin Unity, sin red).
/// Guarda equipo por jugador, kills/muertes individuales y puntos por equipo.
/// Un kill solo suma punto si asesino y víctima son de equipos distintos y válidos.
/// Testeable con EditMode tests.
/// </summary>
public class TDMScoreboard
{
    public const ulong SinAsesino = ulong.MaxValue;

    private readonly Dictionary<ulong, int> equipos = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, int> kills = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, int> deaths = new Dictionary<ulong, int>();
    private readonly List<ulong> ordenRegistro = new List<ulong>();
    private readonly Dictionary<int, int> puntosEquipo = new Dictionary<int, int>()
    {
        { Equipo.Rojo, 0 },
        { Equipo.Azul, 0 },
    };

    public int Jugadores => kills.Count;

    public int ContarEquipo(int equipo)
    {
        int n = 0;
        foreach (var kv in equipos)
            if (kv.Value == equipo) n++;
        return n;
    }

    /// <summary>Asigna equipo si hay hueco (máx 6). Devuelve false si está lleno.</summary>
    public bool AsignarEquipo(ulong clientId, int equipo)
    {
        if (!Equipo.EsValido(equipo)) return false;
        if (equipos.TryGetValue(clientId, out int actual) && actual == equipo)
        {
            RegistrarJugador(clientId);
            return true;
        }
        if (ContarEquipo(equipo) >= Equipo.MaxPorEquipo) return false;
        equipos[clientId] = equipo;
        RegistrarJugador(clientId);
        return true;
    }

    /// <summary>Auto-balanceo: devuelve Rojo o Azul con menos gente. -1 si lleno.</summary>
    public int EquipoConHueco()
    {
        int rojo = ContarEquipo(Equipo.Rojo);
        int azul = ContarEquipo(Equipo.Azul);
        if (rojo <= azul && rojo < Equipo.MaxPorEquipo) return Equipo.Rojo;
        if (azul < Equipo.MaxPorEquipo) return Equipo.Azul;
        if (rojo < Equipo.MaxPorEquipo) return Equipo.Rojo;
        return -1;
    }

    public void RegistrarJugador(ulong clientId)
    {
        if (!kills.ContainsKey(clientId))
        {
            kills[clientId] = 0;
            deaths[clientId] = 0;
            if (!equipos.ContainsKey(clientId)) equipos[clientId] = Equipo.Ninguno;
            ordenRegistro.Add(clientId);
        }
    }

    public int GetEquipo(ulong clientId)
    {
        return equipos.TryGetValue(clientId, out int e) ? e : Equipo.Ninguno;
    }

    /// <summary>
    /// Registra una muerte. Siempre +1 muerte a la víctima.
    /// Solo suma kill individual + punto de equipo si hay asesino válido,
    /// distinto de la víctima y de equipo distinto (sin fuego amigo).
    /// </summary>
    public void RegistrarKill(ulong killerClientId, ulong victimaClientId)
    {
        RegistrarJugador(victimaClientId);
        deaths[victimaClientId]++;

        if (killerClientId == SinAsesino || killerClientId == victimaClientId) return;
        RegistrarJugador(killerClientId);

        int equipoK = GetEquipo(killerClientId);
        int equipoV = GetEquipo(victimaClientId);
        if (!Equipo.EsValido(equipoK) || !Equipo.EsValido(equipoV)) return;
        if (equipoK == equipoV) return; // sin fuego amigo: no suma

        kills[killerClientId]++;
        puntosEquipo[equipoK]++;
    }

    public int GetKills(ulong clientId) => kills.TryGetValue(clientId, out int k) ? k : 0;
    public int GetDeaths(ulong clientId) => deaths.TryGetValue(clientId, out int d) ? d : 0;
    public int GetPuntosEquipo(int equipo) => puntosEquipo.TryGetValue(equipo, out int p) ? p : 0;

    public List<(ulong clientId, int killCount, int deathCount)> Tabla()
    {
        var tabla = new List<(ulong clientId, int killCount, int deathCount)>();
        foreach (ulong id in ordenRegistro)
            tabla.Add((id, kills[id], deaths[id]));
        tabla.Sort((a, b) =>
        {
            int cmp = b.killCount.CompareTo(a.killCount);
            if (cmp != 0) return cmp;
            return ordenRegistro.IndexOf(a.clientId).CompareTo(ordenRegistro.IndexOf(b.clientId));
        });
        return tabla;
    }

    /// <summary>Ganador por puntos de equipo. Null si nadie llega a scoreToWin.</summary>
    public int? GanadorPorEquipos(int scoreToWin)
    {
        if (scoreToWin <= 0) return null;
        int rojo = GetPuntosEquipo(Equipo.Rojo);
        int azul = GetPuntosEquipo(Equipo.Azul);
        bool rGana = rojo >= scoreToWin;
        bool aGana = azul >= scoreToWin;
        if (rGana && !aGana) return Equipo.Rojo;
        if (aGana && !rGana) return Equipo.Azul;
        if (rGana && aGana) return rojo == azul ? Equipo.Rojo : (rojo > azul ? Equipo.Rojo : Equipo.Azul);
        return null;
    }

    /// <summary>Líder por tiempo: equipo con más puntos. Empate = Rojo.</summary>
    public int LiderPorEquipos()
    {
        return GetPuntosEquipo(Equipo.Azul) > GetPuntosEquipo(Equipo.Rojo) ? Equipo.Azul : Equipo.Rojo;
    }

    public void Limpiar()
    {
        equipos.Clear();
        kills.Clear();
        deaths.Clear();
        ordenRegistro.Clear();
        puntosEquipo[Equipo.Rojo] = 0;
        puntosEquipo[Equipo.Azul] = 0;
    }
}
