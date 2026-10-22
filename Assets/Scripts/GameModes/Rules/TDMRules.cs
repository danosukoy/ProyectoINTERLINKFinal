using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caja 3 para TDM 6v6 sin fuego amigo: envuelve a TDMScoreboard (Game.Data).
/// </summary>
public class TDMRules : MonoBehaviour, ITeamModeRules
{
    private readonly TDMScoreboard tabla = new TDMScoreboard();

    public string RulesId => "TDM";
    public int Jugadores => tabla.Jugadores;

    public void RegistrarJugador(ulong clientId) => tabla.RegistrarJugador(clientId);
    public void RegistrarKill(ulong killerClientId, ulong victimaClientId) => tabla.RegistrarKill(killerClientId, victimaClientId);
    public int GetKills(ulong clientId) => tabla.GetKills(clientId);
    public int GetDeaths(ulong clientId) => tabla.GetDeaths(clientId);
    public List<(ulong clientId, int killCount, int deathCount)> Tabla() => tabla.Tabla();
    public void Limpiar() => tabla.Limpiar();

    // Compatibilidad con contrato base: en TDM no hay ganador individual.
    public ulong? GanadorPorScore(int scoreToWin) => null;
    public ulong LiderPorTiempo() => ulong.MaxValue;

    public int GetEquipo(ulong clientId) => tabla.GetEquipo(clientId);
    public bool AsignarEquipo(ulong clientId, int equipo) => tabla.AsignarEquipo(clientId, equipo);
    public int EquipoConHueco() => tabla.EquipoConHueco();
    public int ContarEquipo(int equipo) => tabla.ContarEquipo(equipo);
    public int GetPuntosEquipo(int equipo) => tabla.GetPuntosEquipo(equipo);
    public int? GanadorPorEquipos(int scoreToWin) => tabla.GanadorPorEquipos(scoreToWin);
    public int LiderPorEquipos() => tabla.LiderPorEquipos();
}
