/// <summary>
/// Caja 3: extensión para modos por equipos (TDM).
/// Hereda el contrato base para que el JEFE siga tratando todo como IGameModeRules.
/// </summary>
public interface ITeamModeRules : IGameModeRules
{
    int GetEquipo(ulong clientId);
    bool AsignarEquipo(ulong clientId, int equipo);
    int EquipoConHueco();
    int ContarEquipo(int equipo);
    int GetPuntosEquipo(int equipo);
    int? GanadorPorEquipos(int scoreToWin);
    int LiderPorEquipos();
}
