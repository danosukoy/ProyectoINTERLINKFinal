using System.Collections.Generic;

/// <summary>
/// Modelo puro de puntuación FFA (sin Unity, sin red): kills, muertes,
/// tabla ordenada y reglas de victoria. Testeable con EditMode tests.
/// GameModeManager lo usa en el servidor como modelo autoritativo y
/// replica el resultado a los clientes vía NetworkList.
/// </summary>
public class FFAScoreboard
{
    /// <summary>Id sin asesino válido (coherente con Health).</summary>
    public const ulong SinAsesino = ulong.MaxValue;

    private readonly Dictionary<ulong, int> kills = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, int> deaths = new Dictionary<ulong, int>();
    private readonly List<ulong> ordenRegistro = new List<ulong>();

    public int Jugadores => kills.Count;

    public void RegistrarJugador(ulong clientId)
    {
        if (!kills.ContainsKey(clientId))
        {
            kills[clientId] = 0;
            deaths[clientId] = 0;
            ordenRegistro.Add(clientId);
        }
    }

    /// <summary>
    /// Registra una muerte: +1 muerte a la víctima y, si el asesino es válido
    /// y distinto (sin suicidios), +1 kill al asesino.
    /// </summary>
    public void RegistrarKill(ulong killerClientId, ulong victimaClientId)
    {
        RegistrarJugador(victimaClientId);
        deaths[victimaClientId]++;

        if (killerClientId != SinAsesino && killerClientId != victimaClientId)
        {
            RegistrarJugador(killerClientId);
            kills[killerClientId]++;
        }
    }

    public int GetKills(ulong clientId)
    {
        return kills.TryGetValue(clientId, out int k) ? k : 0;
    }

    public int GetDeaths(ulong clientId)
    {
        return deaths.TryGetValue(clientId, out int d) ? d : 0;
    }

    /// <summary>Tabla ordenada por kills desc; empate = registrado antes.</summary>
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

    /// <summary>
    /// Ganador por score: el de más kills entre los que alcanzan scoreToWin.
    /// Null si nadie llega (o scoreToWin &lt;= 0).
    /// </summary>
    public ulong? GanadorPorScore(int scoreToWin)
    {
        if (scoreToWin <= 0) return null;
        ulong? ganador = null;
        int mejor = -1;
        foreach (ulong id in ordenRegistro)
        {
            int k = kills[id];
            if (k >= scoreToWin && k > mejor)
            {
                mejor = k;
                ganador = id;
            }
        }
        return ganador;
    }

    /// <summary>Líder por tiempo agotado (más kills; empate = registrado antes).</summary>
    public ulong LiderPorTiempo()
    {
        ulong lider = SinAsesino;
        int mejor = -1;
        foreach (ulong id in ordenRegistro)
        {
            if (kills[id] > mejor)
            {
                mejor = kills[id];
                lider = id;
            }
        }
        return lider;
    }

    public void Limpiar()
    {
        kills.Clear();
        deaths.Clear();
        ordenRegistro.Clear();
    }
}
