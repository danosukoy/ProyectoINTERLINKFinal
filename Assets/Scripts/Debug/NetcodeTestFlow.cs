#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Arnés de pruebas multijugador LOCAL, sin Steam (solo editor / development builds).
///
/// Por qué existe: Steam solo permite una sesión del mismo juego por máquina y en
/// un Mac solo puede haber un cliente de Steam logueado, así que no se pueden
/// levantar 2 jugadores por el flujo Steam en una sola máquina. Esto usa el
/// UnityTransport que YA trae el NetworkManager de StartMenu (127.0.0.1:7777)
/// para validar la parte que no depende de Steam: la transición de escena y el
/// swap de avatares 2D ⇄ 3D.
///
/// Importante: el botón INICIAR PARTIDA sigue exigiendo lobby de Steam
/// (LobbyMatchController comprueba lobby.IsHost), así que aquí se usa F3.
///
/// Teclas:
///   F1 = Host: StartHost + carga LobbyRoom
///   F2 = Cliente: StartClient contra 127.0.0.1:7777
///   F3 = Host: cargar SampleScene (partida)
///   F4 = Host: volver a LobbyRoom
///   F5 = volcar estado completo al log
/// </summary>
public class NetcodeTestFlow : MonoBehaviour
{
    private const string EscenaLobby = "LobbyRoom";
    private const string EscenaPartida = "SampleScene";

    private float proximoAviso;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Instalar()
    {
        if (Object.FindFirstObjectByType<NetcodeTestFlow>() != null) return;

        var go = new GameObject("~NetcodeTestFlow");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<NetcodeTestFlow>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) IniciarHost();
        if (Input.GetKeyDown(KeyCode.F2)) IniciarCliente();
        if (Input.GetKeyDown(KeyCode.F3)) CargarEscena(EscenaPartida);
        if (Input.GetKeyDown(KeyCode.F4)) CargarEscena(EscenaLobby);
        if (Input.GetKeyDown(KeyCode.F5)) VolcarEstado();

        // La votación forma parte del flujo real: avisa cada 10s para que el host
        // no se lleve una sorpresa cuando la partida cargue sola.
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && Time.time >= proximoAviso)
        {
            proximoAviso = Time.time + 10f;
            if (MapVoteManager.Instance != null)
            {
                float restante = MapVoteManager.Instance.tiempoRestante.Value;
                if (restante > 0f)
                    Debug.Log($"[TestFlow] Votación en curso: quedan {restante:F0}s (el host cargará la partida solo, o pulsa F3).");
            }
        }
    }

    private void IniciarHost()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) { Debug.LogError("[TestFlow] No hay NetworkManager en la escena."); return; }

        if (nm.IsListening)
        {
            Debug.LogWarning($"[TestFlow] Ya hay sesión activa ({(nm.IsServer ? "host" : "cliente")}).");
            return;
        }

        ConfigurarAprobacion(nm);

        if (!nm.StartHost())
        {
            Debug.LogError("[TestFlow] StartHost falló (¿UnityTransport presente y puerto 7777 libre?).");
            return;
        }

        Debug.Log("[TestFlow] HOST iniciado (UnityTransport 127.0.0.1:7777). Cargando LobbyRoom...");
        CargarEscena(EscenaLobby);
    }

    /// <summary>
    /// Reproduce lo que hace SteamLobbyManager.EnsureSteamTransport en el flujo
    /// real: aprueba conexiones con CreatePlayerObject = false. Sin esto el arnés
    /// autospawnearía el Player 3D en el lobby (Netcode lo hace solo cuando
    /// ConnectionApproval está desactivado), ensuciando la prueba y metiendo un
    /// segundo AudioListener en la escena.
    /// </summary>
    private static void ConfigurarAprobacion(NetworkManager nm)
    {
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = (request, response) =>
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
        };
    }

    private void IniciarCliente()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) { Debug.LogError("[TestFlow] No hay NetworkManager en la escena."); return; }

        if (nm.IsListening)
        {
            Debug.LogWarning($"[TestFlow] Ya hay sesión activa ({(nm.IsServer ? "host" : "cliente")}).");
            return;
        }

        if (!nm.StartClient())
        {
            Debug.LogError("[TestFlow] StartClient falló. ¿Está el host levantado en 127.0.0.1:7777?");
            return;
        }

        Debug.Log("[TestFlow] CLIENTE conectando a 127.0.0.1:7777... (la escena la sincroniza el host)");
    }

    private void CargarEscena(string escena)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            Debug.LogWarning("[TestFlow] Solo el host cambia de escena; el cliente la recibe por red.");
            return;
        }

        if (nm.SceneManager == null)
        {
            Debug.LogError("[TestFlow] NetworkManager.SceneManager es nulo.");
            return;
        }

        Debug.Log($"[TestFlow] Host carga '{escena}'.");
        nm.SceneManager.LoadScene(escena, LoadSceneMode.Single);
    }

    private void VolcarEstado()
    {
        NetworkManager nm = NetworkManager.Singleton;

        Debug.Log("===== ESTADO TEST =====");
        if (nm == null)
        {
            Debug.Log("  NetworkManager: NULL");
        }
        else
        {
            string rol = nm.IsServer ? "HOST" : (nm.IsClient ? "CLIENTE" : "inactivo");
            Debug.Log($"  rol={rol} listening={nm.IsListening} clientes={string.Join(",", nm.ConnectedClientsIds)}");
        }

        Debug.Log($"  escena={SceneManager.GetActiveScene().name}");

        foreach (var a in LobbyWalkablePlayer2D.Todos)
            Debug.Log($"  avatar 2D: cliente={a.OwnerClientId} pos={a.transform.position}");

        foreach (var p in PlayerController.Todos)
            Debug.Log($"  avatar 3D: cliente={p.OwnerClientId} pos={p.transform.position}");

        Debug.Log(MapVoteManager.Instance != null
            ? $"  votación: restante={MapVoteManager.Instance.tiempoRestante.Value:F1}s"
            : "  votación: sin MapVoteManager");

        Debug.Log("=======================");
    }

    private void OnGUI()
    {
        NetworkManager nm = NetworkManager.Singleton;

        string rol = nm == null ? "sin NetworkManager"
                   : nm.IsServer ? "HOST"
                   : nm.IsClient ? "CLIENTE"
                   : "inactivo";
        int clientes = (nm != null && nm.IsListening) ? nm.ConnectedClientsIds.Count : 0;

        GUILayout.BeginArea(new Rect(8f, Screen.height - 100f, 430f, 92f), GUI.skin.box);
        GUILayout.Label($"[TEST] {rol}   clientes={clientes}");
        GUILayout.Label($"escena: {SceneManager.GetActiveScene().name}");
        GUILayout.Label($"avatares 2D: {LobbyWalkablePlayer2D.Todos.Count}    avatares 3D: {PlayerController.Todos.Count}");
        GUILayout.Space(6f);
        GUILayout.Label("F1 host      F2 cliente");
        GUILayout.Label("F3 SampleScene      F4 LobbyRoom");
        GUILayout.Label("F5 volcar estado al log");
        GUILayout.EndArea();
    }
}
#endif
