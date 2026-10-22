using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// BO2-style votación de mapas en LobbyRoom. Pantalla 3D + panel de voto.
/// 30s para votar 1 de 3 mapas (mismo SampleScene x3 si solo hay 1). Solo cuchillo en lobby.
/// Host decide ganador y carga escena vía NetworkSceneManager.
/// </summary>
public class MapVoteManager : NetworkBehaviour
{
    public static MapVoteManager Instance { get; private set; }

    [Serializable]
    public struct MapOption
    {
        public string displayName;
        public string sceneName;
        public Sprite thumbnail;
    }

    [Header("Mapas (3 opciones BO2)")]
    public MapOption[] mapas = new MapOption[3]
    {
        new MapOption{displayName="Mapa 1 - SampleScene", sceneName="SampleScene"},
        new MapOption{displayName="Mapa 2 - SampleScene", sceneName="SampleScene"},
        new MapOption{displayName="Mapa 3 - SampleScene", sceneName="SampleScene"},
    };

    [Header("Timer")]
    public float duracionVotacion = 30f;

    // Sync
    public NetworkVariable<bool> votacionActiva = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> mapaGanador = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkList<int> votosPorMapa;
    private NetworkList<int> votoDeCliente; // indexado por orden de voto, mapeo clientId->mapIndex vía diccionario servidor
    private Dictionary<ulong, int> votoPorCliente = new Dictionary<ulong, int>();

    public event Action OnVotacionIniciada;
    public event Action<int> OnVotacionFinalizada; // mapa ganador index
    public event Action OnVotosActualizados;

    /// <summary>
    /// La UI (MapVoteUI, en otro assembly) abre el panel al invocarse.
    /// Así Gameplay nunca referencia tipos de UI.
    /// </summary>
    public static event Action AlSolicitarAbrirPanel;
    public static void SolicitarAbrirPanelVotacion() => AlSolicitarAbrirPanel?.Invoke();

    private float serverTimer = 0f;
    private bool votacionFinalizando = false;

    private bool suscritoEscena = false;

    private void Awake()
    {
        votosPorMapa = new NetworkList<int>();
        votoDeCliente = new NetworkList<int>();
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
        votacionActiva.OnValueChanged += (_, v) => { if (v) OnVotacionIniciada?.Invoke(); };
        mapaGanador.OnValueChanged += (_, v) => { if (v >= 0 && votacionActiva.Value == false) OnVotacionFinalizada?.Invoke(v); };
        votosPorMapa.OnListChanged += _ => OnVotosActualizados?.Invoke();
        tiempoRestante.OnValueChanged += (_, __) => OnVotosActualizados?.Invoke();
        SuscribirEscena();

        if (IsServer)
        {
            // Inicializa contadores
            votosPorMapa.Clear();
            for (int i = 0; i < mapas.Length; i++) votosPorMapa.Add(0);

            // Auto-inicia votación casi inmediata (0.5s) para que el panel ya tenga contador 30s
            Invoke(nameof(IniciarVotacion), 0.5f);
        }
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

    /// <summary>
    /// Al volver de una partida al lobby se resetea y re-arranca la votación
    /// (cubra manager fresco o persistido). Al salir del lobby se apaga.
    /// Solo el servidor escribe el estado (sin red no existe el manager).
    /// </summary>
    private void AlCambiarEscena(Scene _, Scene nueva)
    {
        if (this == null || gameObject == null) return;
        if (IsSpawned && !IsServer) return;

        // Sin red no hay estado de red que resetear: escribir una
        // NetworkVariable/NetworkList de un NetworkBehaviour no spawneado
        // emite un warning en cada escritura.
        if (!IsSpawned) return;

        if (nueva.name == "LobbyRoom")
        {
            CancelInvoke();
            votacionFinalizando = false;
            votacionActiva.Value = false;
            mapaGanador.Value = -1;
            votoPorCliente.Clear();
            votosPorMapa.Clear();
            for (int i = 0; i < mapas.Length; i++) votosPorMapa.Add(0);
            Invoke(nameof(IniciarVotacion), 2f);
        }
        else
        {
            CancelInvoke();
            votacionActiva.Value = false;
        }
    }

    public void IniciarVotacion()
    {
        if (!IsServer) return;
        if (votacionActiva.Value) return;
        // El lobby ya está usable: fuera pantallas de carga pendientes
        AvisosCarga.Terminar();
        votacionActiva.Value = true;
        mapaGanador.Value = -1;
        votacionFinalizando = false;
        votoPorCliente.Clear();
        votosPorMapa.Clear();
        for (int i = 0; i < mapas.Length; i++) votosPorMapa.Add(0);
        serverTimer = duracionVotacion;
        tiempoRestante.Value = serverTimer;
        Debug.Log($"[MapVoteManager] Votación iniciada: {duracionVotacion}s, {mapas.Length} opciones");
    }

    private void Update()
    {
        if (!IsServer || votacionFinalizando) return;
        if (!votacionActiva.Value) return;

        serverTimer -= Time.deltaTime;
        tiempoRestante.Value = Mathf.Max(0f, serverTimer);

        // Si todos votaron antes de tiempo, acorta a 3s finales
        if (votoPorCliente.Count >= GetExpectedVoters() && serverTimer > 3f)
        {
            serverTimer = 3f;
        }

        if (serverTimer <= 0f)
        {
            FinalizarVotacion();
        }
    }

    private int GetExpectedVoters()
    {
        if (NetworkManager.Singleton == null) return 1;
        return NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    private void FinalizarVotacion()
    {
        votacionFinalizando = true;
        votacionActiva.Value = false;

        // Determina ganador (empate = menor índice)
        int ganador = 0;
        int maxVotos = -1;
        for (int i = 0; i < votosPorMapa.Count; i++)
        {
            if (votosPorMapa[i] > maxVotos)
            {
                maxVotos = votosPorMapa[i];
                ganador = i;
            }
        }
        // Si nadie votó, gana 0
        if (maxVotos <= 0) ganador = 0;
        mapaGanador.Value = ganador;

        string escenaGanadora = mapas[ganador].sceneName;
        Debug.Log($"[MapVoteManager] Ganador: {mapas[ganador].displayName} ({escenaGanadora}) con {maxVotos} votos");

        // Notifica y carga tras 3s (tiempo para ver resultado)
        Invoke(nameof(CargarMapaGanador), 3f);
    }

    private void CargarMapaGanador()
    {
        if (IsSpawned && !IsServer) return;
        int idx = mapaGanador.Value;
        if (idx < 0 || idx >= mapas.Length) idx = 0;
        string escena = string.IsNullOrEmpty(mapas[idx].sceneName) ? "SampleScene" : mapas[idx].sceneName;
        Debug.Log($"[MapVoteManager] Cargando mapa ganador: {escena}");

        // Usa el flujo existente de LobbyMatchController/SteamLobbyManager
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsHost)
        {
            SteamLobbyManager.Instance.StartGame(escena);
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(escena, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(escena);
        }
    }

    // ---------- Voto cliente → servidor ----------

    public bool YaVote(ulong clientId) => votoPorCliente.ContainsKey(clientId);
    public int MiVoto(ulong clientId) => votoPorCliente.TryGetValue(clientId, out int v) ? v : -1;

    public void Votar(int mapIndex)
    {
        if (!votacionActiva.Value) { Debug.LogWarning("[MapVoteManager] Votación no activa"); return; }
        if (mapIndex < 0 || mapIndex >= mapas.Length) return;
        if (!IsSpawned)
        {
            // Offline/solo: aplica local directo (usa client 0)
            ulong localId = 0;
            if (votoPorCliente.TryGetValue(localId, out int prevOff))
            {
                if (prevOff == mapIndex) return;
                votosPorMapa[prevOff]--;
            }
            votoPorCliente[localId] = mapIndex;
            votosPorMapa[mapIndex]++;
            OnVotosActualizados?.Invoke();
            Debug.Log($"[MapVoteManager] Voto offline: mapa {mapIndex} -> {string.Join(",", GetVotosArray())}");
            return;
        }
        VotarServerRpc(mapIndex);
    }

    [Rpc(SendTo.Server)]
    private void VotarServerRpc(int mapIndex, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!votacionActiva.Value) return;
        if (mapIndex < 0 || mapIndex >= mapas.Length) return;

        // Un voto por cliente (puede cambiar)
        if (votoPorCliente.TryGetValue(sender, out int prev))
        {
            if (prev == mapIndex) return; // mismo voto
            votosPorMapa[prev]--;
            votoPorCliente[sender] = mapIndex;
        }
        else
        {
            votoPorCliente[sender] = mapIndex;
        }
        votosPorMapa[mapIndex]++;

        Debug.Log($"[MapVoteManager] Cliente {sender} votó mapa {mapIndex} ({mapas[mapIndex].displayName}) -> votos: {string.Join(",", GetVotosArray())}");
    }

    public int[] GetVotosArray()
    {
        int[] arr = new int[votosPorMapa.Count];
        for (int i = 0; i < votosPorMapa.Count; i++) arr[i] = votosPorMapa[i];
        return arr;
    }

    public int GetTotalVotos()
    {
        int t = 0;
        for (int i = 0; i < votosPorMapa.Count; i++) t += votosPorMapa[i];
        return t;
    }
}
