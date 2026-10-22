using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Control de bloqueo de input por UI. La UI lo marca al abrir un overlay y lo
/// limpia al cerrarlo; el jugador (2D y 3D) consulta HayModalAbierta para no
/// moverse ni disparar. Evita que Gameplay referencie tipos UI.
///
/// Red de seguridad: el flag se limpia en cada carga de escena, porque es
/// estático y un cierre incompleto lo dejaría pegado, congelando al jugador
/// incluso en escenas donde ya no hay ningún panel abierto.
/// </summary>
public static class BloqueoInterfaz
{
    private static bool hayOverlayAbierto = false;

    public static bool HayModalAbierta => hayOverlayAbierto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EngancharEscenas()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        hayOverlayAbierto = false;
    }

    /// <summary>
    /// Con "Domain Reload" desactivado los estáticos sobreviven entre sesiones
    /// de Play; se limpian al arrancar para no heredar un bloqueo.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarAlArrancar()
    {
        hayOverlayAbierto = false;
        DisparoBloqueadoPorUI = false;
    }

    public static void AbrirModal() => hayOverlayAbierto = true;

    public static void CerrarModal() => hayOverlayAbierto = false;

    /// <summary>
    /// La UI lo activa (p. ej. scoreboard con TAB): bloquea disparar,
    /// recargar y cambiar de arma, pero no moverse. Se auto-limpia al
    /// ocultar el panel que lo activó.
    /// </summary>
    public static bool DisparoBloqueadoPorUI { get; set; }
}