using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    void Start() { }
    void Update() { }

    public void ChangeSceneInt(int n){
        CargaLocal.Cargar(SceneUtility.GetScenePathByBuildIndex(n), "Cargando...");
    }

    public void LoadLobbyRoom()
    {
        CargaLocal.Cargar("LobbyRoom", "Entrando al lobby...");
    }

    public void ReturnToMainMenu()
    {
        CargaLocal.Cargar("StartMenu", "Volviendo al menú...");
    }

    /// <summary>
    /// Partida de UN SOLO JUGADOR: lleva al jugador a la LobbyRoom (sin Steam
    /// ni lobby). Allí puede configurar el loadout en la armería y pulsar
    /// "INICIAR PARTIDA", que muestra el contador de 5 segundos y carga
    /// SampleScene en modo offline (sin red).
    /// </summary>
    public void PlaySolo()
    {
        // Si quedara un lobby Steam residual (p. ej. volviendo de una partida
        // online), lo cerramos para que la partida solo sea 100% offline.
        // LeaveLobby es no-op si no hay lobby activo.
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }

        // El cursor queda libre en la sala (menú 3D con UI).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[MenuManager] JUGAR SOLO: entrando a la LobbyRoom (sin sala).");
        CargaLocal.Cargar("LobbyRoom", "Entrando al lobby...");
    }
}
