using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Expulsión estilo Left 4 Dead: el botón ✕ propone y eso ya cuenta como SÍ
/// del proponente; todos (incluido el acusado) votan F1=SÍ / F2=NO durante
/// 20s. Hace falta SÍ > 50% de los conectados (no votar = NO implícito).
/// La auto-expulsión (tu propia ✕) es directa, sin votación.
/// Al aprobar: aviso al afectado y DisconnectClient 1s después.
/// No se puede votar contra el host ni en offline. Anti-abuso: 60s entre
/// propuestas del mismo jugador, 120s de protección tras un rechazo.
/// Vive en DontDestroyOnLoad: vale en lobby y en partida.
/// </summary>
public class VoteKickManager : NetworkBehaviour
{
    public static VoteKickManager Instance { get; private set; }

    public const float DURACION = 20f;
    public const float COOLDOWN_INICIADOR = 60f;
    public const float PROTECCION_RECHAZO = 120f;

    public NetworkVariable<bool> votacionActiva = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> acusadoId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // FixedString: NetworkVariable<string> no tiene serialización generada en Netcode.
    public NetworkVariable<FixedString64Bytes> acusadoNombre = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes(""), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> votosSi = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> votosNo = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>0 = en curso/sin resultado, 1 = expulsado, 2 = rechazada, 3 = cancelada.</summary>
    public NetworkVariable<int> resultado = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Solo servidor (no viajan por red)
    private readonly Dictionary<ulong, bool> votos = new Dictionary<ulong, bool>();
    private readonly Dictionary<ulong, float> ultimoInicio = new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, float> protegidoHasta = new Dictionary<ulong, float>();
    private float serverTimer = 0f;
    private bool suscritoEscena = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnDestroy()
    {
        DesuscribirEscena();
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SuscribirEscena();
    }

    public override void OnNetworkDespawn()
    {
        DesuscribirEscena();
        base.OnNetworkDespawn();
    }

    private void SuscribirEscena()
    {
        if (suscritoEscena) return;
        suscritoEscena = true;
        SceneManager.activeSceneChanged += AlCambiarEscena;
    }

    private void DesuscribirEscena()
    {
        if (!suscritoEscena) return;
        suscritoEscena = false;
        SceneManager.activeSceneChanged -= AlCambiarEscena;
    }

    private void AlCambiarEscena(Scene _, Scene nueva)
    {
        // Cambio de contexto (partida<->lobby): cancela sin expulsar
        if (!IsServer) return;
        if (votacionActiva.Value) CancelarVotacion();
    }

    private void Update()
    {
        if (!IsServer || !votacionActiva.Value) return;

        // El acusado se fue solo: se acabó
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(acusadoId.Value))
        {
            CancelarVotacion();
            return;
        }

        serverTimer -= Time.deltaTime;
        tiempoRestante.Value = Mathf.Max(0f, serverTimer);
        if (serverTimer <= 0f) FinalizarVotacion();
    }

    // ---------- Iniciar ----------

    /// <summary>Llamado por la UI local (botón ✕ del scoreboard).</summary>
    public void IniciarVotacion(ulong targetClientId)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("[VoteKick] Sin red no hay expulsiones.");
            return;
        }
        SolicitarKickServerRpc(targetClientId);
    }

    [Rpc(SendTo.Server)]
    private void SolicitarKickServerRpc(ulong targetClientId, RpcParams rpc = default)
    {
        ulong solicitante = rpc.Receive.SenderClientId;
        if (votacionActiva.Value) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId)) return;
        // Auto-expulsión permitida (solo se perjudica uno mismo).
        if (targetClientId == NetworkManager.ServerClientId) return; // el host no se expulsa

        float ahora = Time.realtimeSinceStartup;
        if (ultimoInicio.TryGetValue(solicitante, out float ultimo) && ahora - ultimo < COOLDOWN_INICIADOR)
        {
            Debug.Log($"[VoteKick] Cliente {solicitante} en cooldown de propuestas.");
            return;
        }
        if (protegidoHasta.TryGetValue(targetClientId, out float prot) && ahora < prot)
        {
            Debug.Log($"[VoteKick] Cliente {targetClientId} protegido tras rechazo previo.");
            return;
        }

        // Auto-expulsión: directa, sin votación (solo se perjudica uno mismo)
        if (targetClientId == solicitante)
        {
            IniciarExpulsionDirecta(targetClientId);
            return;
        }

        ultimoInicio[solicitante] = ahora;
        votos.Clear();
        votosSi.Value = 0;
        votosNo.Value = 0;
        resultado.Value = 0;
        acusadoId.Value = targetClientId;
        acusadoNombre.Value = ResolverNombre(targetClientId);
        serverTimer = DURACION;
        tiempoRestante.Value = serverTimer;
        votacionActiva.Value = true;

        // L4D: proponer ya cuenta como SÍ del proponente
        votos[solicitante] = true;
        votosSi.Value = 1;

        Debug.Log($"[VoteKick] Votación contra {targetClientId} ({acusadoNombre.Value}) iniciada por {solicitante} (1 SÍ auto).");
    }

    /// <summary>Expulsión propia inmediata: aviso + kick diferido, sin votos.</summary>
    private void IniciarExpulsionDirecta(ulong clientId)
    {
        acusadoId.Value = clientId;
        acusadoNombre.Value = ResolverNombre(clientId);
        votosSi.Value = 1;
        votosNo.Value = 0;
        resultado.Value = 1;
        Debug.Log($"[VoteKick] Auto-expulsión de {clientId} ({acusadoNombre.Value}).");
        AvisarExpulsionClientRpc(new RpcParams
        {
            Send = new RpcSendParams { Target = RpcTarget.Single(clientId, RpcTargetUse.Temp) }
        });
        StartCoroutine(ExpulsarTrasAviso(clientId));
    }

    // ---------- Votar ----------

    /// <summary>Llamado por la UI local (F1 = true, F2 = false).</summary>
    public void Votar(bool aFavor)
    {
        if (!IsSpawned || !votacionActiva.Value) return;
        VotoServerRpc(aFavor);
    }

    [Rpc(SendTo.Server)]
    private void VotoServerRpc(bool aFavor, RpcParams rpc = default)
    {
        ulong votante = rpc.Receive.SenderClientId;
        if (!votacionActiva.Value) return;
        // L4D: el acusado también vota (normalmente NO en defensa propia)
        if (votos.ContainsKey(votante)) return; // un voto por jugador
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(votante)) return;

        votos[votante] = aFavor;
        if (aFavor) votosSi.Value++;
        else votosNo.Value++;

        // Decisión anticipada cuando ya es matemáticamente inevitable
        int elegibles = Elegibles();
        if (votosSi.Value > elegibles / 2) { FinalizarVotacion(); return; }
        if (votosNo.Value > elegibles / 2) { FinalizarVotacion(); return; }
        if (votos.Count >= elegibles && serverTimer > 3f) serverTimer = 3f;
    }

    private int Elegibles()
    {
        if (NetworkManager.Singleton == null) return 1;
        // Todos los conectados votan, acusado incluido
        return Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count);
    }

    // ---------- Finalizar ----------

    private void FinalizarVotacion()
    {
        if (!votacionActiva.Value) return;
        votacionActiva.Value = false;

        bool expulsar = votosSi.Value > Elegibles() / 2;
        ulong acusado = acusadoId.Value;

        if (expulsar)
        {
            resultado.Value = 1;
            Debug.Log($"[VoteKick] Expulsado {acusado} ({acusadoNombre.Value}): {votosSi.Value} sí / {votosNo.Value} no.");
            AvisarExpulsionClientRpc(new RpcParams
            {
                Send = new RpcSendParams { Target = RpcTarget.Single(acusado, RpcTargetUse.Temp) }
            });
            StartCoroutine(ExpulsarTrasAviso(acusado));
        }
        else
        {
            resultado.Value = 2;
            protegidoHasta[acusado] = Time.realtimeSinceStartup + PROTECCION_RECHAZO;
            Debug.Log($"[VoteKick] Rechazada contra {acusado}: {votosSi.Value} sí / {votosNo.Value} no.");
            StartCoroutine(LimpiarResultadoTras(4f));
        }
    }

    private void CancelarVotacion()
    {
        votacionActiva.Value = false;
        resultado.Value = 3;
        StartCoroutine(LimpiarResultadoTras(3f));
    }

    private System.Collections.IEnumerator LimpiarResultadoTras(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        if (!votacionActiva.Value) resultado.Value = 0;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AvisarExpulsionClientRpc(RpcParams rpc = default)
    {
        SteamLobbyManager.MotivoDesconexion = "Fuiste expulsado por votación de los jugadores.";
        SteamLobbyManager.VolverALaLista = true;
    }

    private System.Collections.IEnumerator ExpulsarTrasAviso(ulong acusado)
    {
        yield return new WaitForSeconds(1f);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
            && NetworkManager.Singleton.ConnectedClients.ContainsKey(acusado))
        {
            NetworkManager.Singleton.DisconnectClient(acusado);
        }
        yield return new WaitForSeconds(3f);
        if (!votacionActiva.Value) resultado.Value = 0;
    }

    private string ResolverNombre(ulong clientId)
    {
        if (SteamLobbyManager.Instance != null)
        {
            string n = SteamLobbyManager.Instance.GetPlayerName(clientId);
            if (!string.IsNullOrEmpty(n)) return n;
        }
        return $"Jugador #{clientId}";
    }
}
