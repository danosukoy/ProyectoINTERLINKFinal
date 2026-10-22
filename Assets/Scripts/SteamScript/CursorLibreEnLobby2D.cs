using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantiene el cursor libre (visible y sin bloqueo) en la lobby 2D.
///
/// La lobby 2D es una vista cenital con cámara fija: el jugador se mueve con
/// WASD y no apunta con el ratón, así que no hay motivo para capturarlo. Pero
/// varias UIs reutilizadas de la partida 3D (armería, votación, pausa) vuelven
/// a bloquear el cursor al cerrarse, y en la lobby eso deja el puntero atrapado
/// en el centro sin poder pulsar nada.
///
/// En vez de tocar cada UI (que sí necesita el bloqueo en partida), este guardia
/// se auto-instala solo cuando la escena es la lobby 2D y deshace el bloqueo.
/// </summary>
[DisallowMultipleComponent]
public class CursorLibreEnLobby2D : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstalar()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        Comprobar(SceneManager.GetActiveScene());
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => Comprobar(escena);

    private static void Comprobar(Scene escena)
    {
        var existente = FindFirstObjectByType<CursorLibreEnLobby2D>(FindObjectsInactive.Include);

        // Solo en la lobby 2D: mismo criterio que usa el resto del lobby 2D
        // (escena LobbyRoom + spawner de avatares 2D presente).
        bool esLobby2D = escena.name == "LobbyRoom"
            && FindFirstObjectByType<LobbyAvatarSpawner2D>(FindObjectsInactive.Include) != null;

        if (!esLobby2D)
        {
            if (existente != null) Destroy(existente.gameObject);
            return;
        }

        if (existente != null) return;
        new GameObject("CursorLibreLobby2D").AddComponent<CursorLibreEnLobby2D>();
    }

    private void LateUpdate()
    {
        // Cada frame, por si alguna UI lo bloqueó después. Es una asignación
        // barata y solo escribe cuando de verdad hay que corregir algo.
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }
}