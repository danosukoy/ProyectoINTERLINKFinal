using UnityEngine;

/// <summary>
/// Definición data-driven de un modo de juego (FFA, TDM futuro, etc.).
/// Padre canónico para configurar modos sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "GameModeDef", menuName = "GameMode/Definition")]
public class GameModeDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string modeId = "FFA";
    public string modeName = "Todos contra todos";
    [Tooltip("Id de reglas que ejecuta este modo (FFA, TDM futuro). Lo usa GameModeManager para elegir la Caja 3.")]
    public string rulesId = "FFA";

    [Header("Partida")]
    [Tooltip("Segundos de cuenta atrás al iniciar ('LA PARTIDA COMIENZA EN 5')")]
    public int countdownSeconds = 5;
    [Tooltip("Duración de la partida en segundos (0 = sin límite)")]
    public float matchDuration = 600f;
    [Tooltip("Kills para ganar (0 = sin límite, gana por tiempo)")]
    public int scoreToWin = 15;
    [Tooltip("Delay de respawn en segundos")]
    public float respawnDelay = 3f;
    [Tooltip("Invulnerabilidad anti spawn-kill al aparecer (0 = desactivada)")]
    public float spawnProtection = 2f;
    [Tooltip("Máximo de jugadores")]
    public int maxPlayers = 16;

    [Header("Mensajes")]
    public string mensajeCuentaAtras = "LA PARTIDA COMIENZA EN";
    public string mensajeVictoria = "¡VICTORIA!";
}
