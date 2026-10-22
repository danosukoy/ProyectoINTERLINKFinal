using System;

/// <summary>
/// Bus de avisos de carga de escena. Gameplay emite, la UI (LoadingScreen,
/// en otro assembly) escucha. Así ninguna carga se queda sin pantalla.
/// </summary>
public static class AvisosCarga
{
    /// <summary>Empieza una carga (mensaje para mostrar). Progreso: -1 = indeterminado.</summary>
    public static event Action<string> AlEmpezar;
    public static event Action<float> AlProgresar;
    public static event Action AlTerminar;

    public static void Empezar(string mensaje) => AlEmpezar?.Invoke(mensaje);
    public static void Progreso(float p) => AlProgresar?.Invoke(p);
    public static void Terminar() => AlTerminar?.Invoke();
}
