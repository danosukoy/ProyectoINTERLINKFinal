using UnityEngine;

/// <summary>
/// Punto único de entrada al inventario (estación F, botón INVENTARIO, BACK).
/// Delega en SteamLobbyListUI, que es quien posee los canvas.
/// Existe por compatibilidad; la lógica vive en el menú.
/// </summary>
public class InventarioCanvasHost : MonoBehaviour
{
    public bool Abierto { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "StartMenu") return;
        if (FindFirstObjectByType<InventarioCanvasHost>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("InventarioCanvasHost");
        go.AddComponent<InventarioCanvasHost>();
    }

    public void Abrir()
    {
        if (Abierto) return;
        Abierto = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var ui = FindFirstObjectByType<SteamLobbyListUI>(FindObjectsInactive.Include);
        if (ui != null) ui.MostrarInventario();
    }

    public void Cerrar()
    {
        if (!Abierto) return;
        Abierto = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var ui = FindFirstObjectByType<SteamLobbyListUI>(FindObjectsInactive.Include);
        if (ui != null) ui.OcultarInventario();
    }

    /// <summary>Muestra una sección del inventario y apaga las demás.</summary>
    public void MostrarSeccion(GameObject seccion)
    {
        var ui = FindFirstObjectByType<SteamLobbyListUI>(FindObjectsInactive.Include);
        if (ui != null) ui.MostrarSeccionInventario(seccion);
    }
}
