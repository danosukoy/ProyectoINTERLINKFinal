using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Controlador de la partida de UN SOLO JUGADOR (SampleScene en modo offline).
/// - Pulsa ESC dos veces en menos de 2 segundos para volver al menú principal
///   (una sola pulsación queda en silencio para no interferir con otras UI).
/// Solo actúa en modo offline: en partida multijugador el host gestiona la
/// transición de escena con el NetworkSceneManager, así que aquí no hace nada.
/// </summary>
public class SoloMatchController : MonoBehaviour
{
    /// <summary>Ventana (en segundos) en la que cuenta la doble pulsación de ESC.</summary>
    [SerializeField] private float ventanaDobleEsc = 2f;

    private float ultimoEscTime = -10f;

    private void Start()
    {
        // Offline sin red: SampleScene ya no trae player en escena (se quitó
        // el fantasma server-owned que duplicaba el control en host). Si no
        // hay jugador, se instancia uno local para probar en solo.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;
        if (FindFirstObjectByType<PlayerController>() != null)
            return;
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (prefab != null)
        {
            Instantiate(prefab, new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            Debug.Log("[SoloMatchController] Player local instanciado (modo solo offline).");
        }
#endif
    }

    private void Update()
    {
        // En multijugador (host o cliente) la salida la controla el flujo de red.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        // Con modal abierta (pausa) el ESC lo consume ella
        if (BloqueoInterfaz.HayModalAbierta)
        {
            ultimoEscTime = -10f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.time - ultimoEscTime <= ventanaDobleEsc)
            {
                SalirAlMenu();
            }
            else
            {
                ultimoEscTime = Time.time;
                Debug.Log("[SoloMatchController] Pulsa ESC de nuevo para volver al menú principal.");
            }
        }
    }

    /// <summary>Vuelve al StartMenu desbloqueando el cursor.</summary>
    public void SalirAlMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[SoloMatchController] Saliendo de la partida solo hacia StartMenu...");
        CargaLocal.Cargar("StartMenu", "Volviendo al menú...");
    }
}