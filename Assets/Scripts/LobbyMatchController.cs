using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Controlador de la cuenta atrás de la sala (LobbyRoom) antes de entrar a la
/// partida. El host la inicia y, vía RPC (SendTo.ClientsAndHost), TODOS los
/// jugadores (clientes y host) ven el mismo contador de 5 segundos en pantalla.
/// Al terminar, SOLO el host (servidor) carga la escena de juego con el
/// NetworkSceneManager para que el cambio llegue sincronizado a todos.
/// Debe estar sobre un NetworkObject presente en la escena (p. ej. un objeto
/// "LobbyMatch") para que el RPC funcione en todos los clientes.
/// </summary>
public class LobbyMatchController : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int segundosCuentaAtras = 5;
    [SerializeField] private string mensajeCuentaAtras = "LA PARTIDA COMIENZA EN";

    /// <summary>Inicia la cuenta atrás en todos los clientes + host.</summary>
    public void IniciarCuentaAtras()
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("[LobbyMatchController] No está spawnado en red; no se puede sincronizar la cuenta atrás.");
            return;
        }

        IniciarCuentaAtrasServerRpc(segundosCuentaAtras);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void IniciarCuentaAtrasServerRpc(int segundos)
    {
        CuentaAtrasClientRpc(segundos);
    }

    /// <summary>
    /// Todos (clientes y host) muestran el contador. Al llegar a 0, el host
    /// (IsServer) carga la escena de partida; el resto solo limpia el overlay.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void CuentaAtrasClientRpc(int segundos)
    {
        MatchCountdown.Iniciar(segundos, mensajeCuentaAtras, () =>
        {
            if (IsServer && IsHost)
            {
                EjecutarCargaPartidaEnHost();
            }
        });
    }

    private void EjecutarCargaPartidaEnHost()
    {
        // Si hay votación activa, usa el mapa ganador; si no, SampleScene
        string escena = "SampleScene";
        if (MapVoteManager.Instance != null && MapVoteManager.Instance.mapaGanador.Value >= 0)
        {
            int idx = MapVoteManager.Instance.mapaGanador.Value;
            if (idx >= 0 && idx < MapVoteManager.Instance.mapas.Length)
                escena = MapVoteManager.Instance.mapas[idx].sceneName;
        }
        SteamLobbyManager lobby = SteamLobbyManager.Instance;
        if (lobby == null || !lobby.IsHost || !lobby.CurrentLobby.HasValue)
        {
            Debug.LogWarning("[LobbyMatchController] Ya no hay sala o dejamos de ser host; no se inicia la partida.");
            return;
        }

        lobby.StartGame(escena);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Auto-asegura votación BO2 si no existe en escena (sin edición manual).
        // La pantalla 3D y el panel se auto-instalan desde el lado UI.
        AsegurarMapVoteManager();
    }

    private void Start()
    {
        // SOLO / sin red: NO se crea MapVoteManager. Un NetworkBehaviour sin
        // spawnear no tiene sus NetworkVariable/NetworkList vinculadas, así que
        // cada escritura emitía un warning por frame. Además su votación
        // offline cargaba la partida sola a los ~33 s, saltándose el botón
        // INICIAR PARTIDA (el flujo documentado de MenuManager.PlaySolo).
        // El timer de votación solo tiene sentido con jugadores: lo crea
        // AsegurarMapVoteManager cuando hay servidor.
    }

    private void AsegurarMapVoteManager()
    {
        var existente = MapVoteManager.Instance;
        // Si hay uno offline (sin NetworkObject spawneado), se sustituye por red
        if (existente != null && !existente.IsSpawned)
        {
            Destroy(existente.gameObject);
        }
        if (MapVoteManager.Instance == null && IsServer)
        {
            var go = new GameObject("MapVoteManager");
            go.transform.SetParent(transform, false);
            var netObj = go.AddComponent<Unity.Netcode.NetworkObject>();
            go.AddComponent<MapVoteManager>();
            netObj.Spawn();
        }
        // Expulsión por votación: objeto raíz persistente (vale en lobby y partida).
        if (VoteKickManager.Instance == null && IsServer)
        {
            var goKick = new GameObject("VoteKickManager");
            DontDestroyOnLoad(goKick);
            var netKick = goKick.AddComponent<Unity.Netcode.NetworkObject>();
            goKick.AddComponent<VoteKickManager>();
            netKick.Spawn();
        }
    }
}
