using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caja 3 para FFA: envuelve al modelo puro FFAScoreboard (Game.Data)
/// y lo expone como componente buscable por GameModeManager.
/// Sin red, sin estado de partida: solo decide kills y victoria.
/// </summary>
public class FFARules : MonoBehaviour, IGameModeRules
{
    private readonly FFAScoreboard tabla = new FFAScoreboard();

    public string RulesId => "FFA";
    public int Jugadores => tabla.Jugadores;

    public void RegistrarJugador(ulong clientId) => tabla.RegistrarJugador(clientId);
    public void RegistrarKill(ulong killerClientId, ulong victimaClientId) => tabla.RegistrarKill(killerClientId, victimaClientId);
    public int GetKills(ulong clientId) => tabla.GetKills(clientId);
    public int GetDeaths(ulong clientId) => tabla.GetDeaths(clientId);
    public List<(ulong clientId, int killCount, int deathCount)> Tabla() => tabla.Tabla();
    public ulong? GanadorPorScore(int scoreToWin) => tabla.GanadorPorScore(scoreToWin);
    public ulong LiderPorTiempo() => tabla.LiderPorTiempo();
    public void Limpiar() => tabla.Limpiar();
}
