using System.Collections.Generic;

/// <summary>
/// Caja 3: contrato puro de reglas de un modo (sin Unity, sin red).
/// El JEFE (GameModeManager) solo llama a esto. No sabe si es FFA o TDM.
/// Vive en Game.Data para no romper la referencia Data -> Gameplay.
/// </summary>
public interface IGameModeRules
{
    string RulesId { get; }
    int Jugadores { get; }
    void RegistrarJugador(ulong clientId);
    void RegistrarKill(ulong killerClientId, ulong victimaClientId);
    int GetKills(ulong clientId);
    int GetDeaths(ulong clientId);
    List<(ulong clientId, int killCount, int deathCount)> Tabla();
    ulong? GanadorPorScore(int scoreToWin);
    ulong LiderPorTiempo();
    void Limpiar();
}
