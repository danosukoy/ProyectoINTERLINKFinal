using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    public event Action<Lobby> OnLobbyCreated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<List<Lobby>> OnLobbiesFound;
    public event Action<string> OnLobbyError;
    public event Action<Lobby, Friend> OnLobbyMemberJoinedEvent;
    public event Action<Lobby, Friend> OnLobbyMemberLeftEvent;

    private Lobby? currentLobby;
    private bool isHost;

    /// <summary>
    /// Motivo de una desconexión forzada (p. ej. expulsión por votación).
    /// Lo fija el servidor vía RPC antes de desconectar; se consume al volver
    /// al menú para mostrarlo en vez del genérico.
    /// </summary>
    public static string MotivoDesconexion;

    /// <summary>
    /// Al volver al menú tras una expulsión se abre directo la lista de salas.
    /// </summary>
    public static bool VolverALaLista;

    public Lobby? CurrentLobby => currentLobby;
    public bool IsHost => isHost;

    private const string HostAddressKey = "HostAddress";
    private const string LobbyNameKey = "LobbyName";

    /// <summary>
    /// Marca que identifica las salas creadas por ESTE proyecto.
    ///
    /// El appid 480 (Spacewar) es el app de pruebas público de Valve: TODO
    /// desarrollador que usa Steamworks sin appid propio publica sus salas en el
    /// mismo espacio. Sin este filtro, la búsqueda devuelve salas ajenas (de ahí
    /// nombres tipo "Sala 109775241439217466" y errores al unirse, porque no
    /// hablan nuestro protocolo ni nuestro Netcode).
    /// </summary>
    public const string ProyectoKey = "ProyectoInterlink";
    public const string ProyectoValor = "1";

    // Mapeo clientId de Netcode -> SteamID (rellenado en ConnectionApproval)
    private readonly Dictionary<ulong, ulong> clientSteamIds = new Dictionary<ulong, ulong>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetupEventListeners();
    }

    private void SetupEventListeners()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedHandler;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEnteredHandler;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoinedHandler;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeaveHandler;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreatedHandler;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequestedHandler;
    }

    // ------------------------------------------------------------------
    // Bootstrap del transporte Steam P2P + Connection Approval
    // ------------------------------------------------------------------

    private SteamP2PTransport EnsureSteamTransport()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[SteamLobbyManager] NetworkManager.Singleton es nulo. ¿Existe un NetworkManager en la escena?");
            return null;
        }

        SteamP2PTransport transport = nm.GetComponent<SteamP2PTransport>();
        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<SteamP2PTransport>();
        }

        if (nm.NetworkConfig.NetworkTransport != transport)
        {
            nm.NetworkConfig.NetworkTransport = transport;
        }

        // Approval: el cliente envía su SteamID como ConnectionData; el servidor lo registra.
        nm.NetworkConfig.ConnectionApproval = true;
        if (nm.ConnectionApprovalCallback == null)
        {
            nm.ConnectionApprovalCallback = HandleConnectionApproval;
        }

        // Pantalla de carga en cambios de escena en red (vale para host y
        // clientes: cada peer recibe sus propios eventos de escena).
        if (!suscritoEscenasNetcode && nm.SceneManager != null)
        {
            suscritoEscenasNetcode = true;
            nm.SceneManager.OnSceneEvent += AlEventoEscenaNetcode;
        }

        return transport;
    }

    private bool suscritoEscenasNetcode = false;

    private void AlEventoEscenaNetcode(SceneEvent ev)
    {
        switch (ev.SceneEventType)
        {
            case SceneEventType.Load:
                AvisosCarga.Empezar("Cargando partida...");
                break;
            case SceneEventType.LoadComplete:
            case SceneEventType.SynchronizeComplete:
            case SceneEventType.UnloadComplete:
                // En SampleScene manda el GameModeManager (la oculta al arrancar
                // el countdown); aquí la dejaríamos caer antes de posicionar.
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "SampleScene")
                    break;
                AvisosCarga.Terminar();
                break;
        }
    }

    private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong steamId = 0;
        if (request.Payload != null && request.Payload.Length > 0)
        {
            string payload = Encoding.UTF8.GetString(request.Payload);
            ulong.TryParse(payload, out steamId);
        }

        // Sin payload solo puede ser el host local (su propia aprobación interna).
        if (steamId == 0 && SteamClient.IsValid)
        {
            steamId = SteamClient.SteamId;
        }

        clientSteamIds[request.ClientNetworkId] = steamId;

        response.Approved = true;
        // El spawneo de avatares lo hace LobbyAvatarSpawner2D (o GameModeManager
        // al entrar a la partida), no el auto-spawn de Netcode.
        response.CreatePlayerObject = false;

        Debug.Log($"[SteamLobbyManager] Approval: clientId {request.ClientNetworkId} -> SteamID {steamId}");
    }

    public ulong GetSteamIdForClient(ulong clientId)
    {
        return clientSteamIds.TryGetValue(clientId, out ulong steamId) ? steamId : 0;
    }

    public string GetPlayerName(ulong clientId)
    {
        ulong steamId = GetSteamIdForClient(clientId);
        if (steamId == 0) return $"Jugador #{clientId}";

        string name = new Friend(steamId).Name;
        return string.IsNullOrEmpty(name) ? $"Jugador #{clientId}" : name;
    }

    // ------------------------------------------------------------------
    // Flujo de lobbies
    // ------------------------------------------------------------------

    public async void CreateLobby(string lobbyName = "Partida Steam", int maxMembers = 16)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            Debug.LogError("[SteamLobbyManager - Facepunch] SteamAPI no está inicializado.");
            OnLobbyError?.Invoke("SteamAPI no inicializado");
            return;
        }

        try
        {
            Debug.Log($"[SteamLobbyManager - Facepunch] Creando sala Steam: {lobbyName}...");
            AvisosCarga.Empezar("Creando sala...");
            var createResult = await SteamMatchmaking.CreateLobbyAsync(maxMembers);

            if (!createResult.HasValue)
            {
                Debug.LogError("[SteamLobbyManager - Facepunch] Falló la creación de la sala en Steam.");
                OnLobbyError?.Invoke("Error al crear sala en Steam");
                return;
            }

            Lobby lobby = createResult.Value;

            // El orden importa: la sala nace Invisible (CreateLobbyAsync usa
            // LobbyType.Invisible). Primero se marca y se prepara, y solo al
            // final SetPublic() la hace descubrible. Así nunca existe una
            // ventana en la que sea pública sin la marca del proyecto.
            lobby.SetData(ProyectoKey, ProyectoValor);
            lobby.SetJoinable(true);
            lobby.SetData(LobbyNameKey, string.IsNullOrWhiteSpace(lobbyName) ? $"Partida de {SteamClient.Name}" : lobbyName);
            // Los clientes usan este SteamID para conectar por Steam P2P relay.
            lobby.SetData(HostAddressKey, SteamClient.SteamId.ToString());
            lobby.SetPublic();

            currentLobby = lobby;
            isHost = true;

            Debug.Log($"[SteamLobbyManager - Facepunch] Sala creada exitosamente en Steam. ID: {lobby.Id}");
            OnLobbyCreated?.Invoke(lobby);

            // NetworkManager & Scene Transition
            SteamP2PTransport transport = EnsureSteamTransport();
            if (NetworkManager.Singleton != null && transport != null)
            {
                if (!NetworkManager.Singleton.IsListening)
                {
                    // Si el host no arranca (relay caído), no se entra a una
                    // sala muerta: se avisa y se abandona la sala de Steam.
                    if (!NetworkManager.Singleton.StartHost())
                    {
                        Debug.LogError("[SteamLobbyManager - Facepunch] StartHost falló: sin red no hay sala.");
                        OnLobbyError?.Invoke("No se pudo iniciar el host (red Steam no disponible)");
                        try { lobby.Leave(); } catch { /* ya cerrada */ }
                        currentLobby = null;
                        isHost = false;
                        AvisosCarga.Terminar();
                        return;
                    }
                }

                if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("LobbyRoom", LoadSceneMode.Single);
                }
                else
                {
                    SceneManager.LoadScene("LobbyRoom");
                }
            }
            else
            {
                Debug.LogWarning("[SteamLobbyManager] NetworkManager o transporte no disponibles. Cargando escena LobbyRoom directamente.");
                SceneManager.LoadScene("LobbyRoom");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager - Facepunch] Excepción al crear sala: {e.Message}");
            OnLobbyError?.Invoke(e.Message);
        }
    }

    public async void FindLobbies()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            // Antes esto era un return mudo: la UI se quedaba en "Buscando..."
            // hasta el timeout de 12s sin explicar nada.
            Debug.LogWarning("[SteamLobbyManager - Facepunch] FindLobbies sin Steam inicializado.");
            OnLobbyError?.Invoke("Steam no está inicializado. Abre el cliente de Steam.");
            return;
        }

        try
        {
            Debug.Log("[SteamLobbyManager - Facepunch] Buscando salas públicas en Steam...");
            // Solo salas de este proyecto: el filtro por key/value lo aplica
            // Steam en el servidor, así que descarta las de otros juegos que
            // comparten el appid 480 antes de recibirlas.
            var lobbies = await SteamMatchmaking.LobbyList
                .WithKeyValue(ProyectoKey, ProyectoValor)
                .WithSlotsAvailable(1)
                .RequestAsync();

            List<Lobby> lobbyList = new List<Lobby>();
            if (lobbies != null)
            {
                foreach (var l in lobbies)
                {
                    // Nunca listar la propia sala.
                    if (currentLobby.HasValue && l.Id == currentLobby.Value.Id) continue;
                    lobbyList.Add(l);
                }
            }

            Debug.Log($"[SteamLobbyManager - Facepunch] Encontradas {lobbyList.Count} salas públicas en Steam.");
            OnLobbiesFound?.Invoke(lobbyList);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager - Facepunch] Error al buscar salas: {e.Message}");
            OnLobbyError?.Invoke($"Error al buscar salas: {e.Message}");
        }
    }

    public async void JoinLobby(Lobby lobby)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized) return;

        try
        {
            Debug.Log($"[SteamLobbyManager - Facepunch] Uniéndose a la sala: {lobby.Id}...");
            RoomEnter result = await lobby.Join();

            if (result == RoomEnter.Success)
            {
                currentLobby = lobby;
                isHost = false;
                Debug.Log($"[SteamLobbyManager - Facepunch] Unido exitosamente a la sala {lobby.Id}");

                OnLobbyJoined?.Invoke(lobby);

                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                {
                    SteamP2PTransport transport = EnsureSteamTransport();
                    // El Owner lo pone el backend de Steam al crear la sala: no tiene
                    // race como el metadata. El metadata queda de respaldo.
                    ulong hostSteamId = 0;
                    try { hostSteamId = lobby.Owner.Id.Value; } catch { hostSteamId = 0; }
                    string hostAddress = hostSteamId != 0 ? hostSteamId.ToString() : lobby.GetData(HostAddressKey);
                    if (hostSteamId == 0) ulong.TryParse(hostAddress, out hostSteamId);

                    if (transport != null && hostSteamId != 0)
                    {
                        transport.HostSteamId = hostSteamId;
                        // Mandamos nuestro SteamID para que el host lo registre en el approval.
                        NetworkManager.Singleton.NetworkConfig.ConnectionData =
                            Encoding.UTF8.GetBytes(SteamClient.SteamId.ToString());

                        if (!NetworkManager.Singleton.StartClient())
                        {
                            OnLobbyError?.Invoke("No se pudo iniciar el cliente de red");
                        }
                        else
                        {
                            RegisterClientDisconnectHandler();
                        }
                        // IMPORTANTE: no cargamos LobbyRoom aquí. El scene management de
                        // Netcode sincroniza la escena activa del host al conectar.
                    }
                    else
                    {
                        Debug.LogError($"[SteamLobbyManager] No se pudo leer el SteamID del host del lobby ('{hostAddress}').");
                        OnLobbyError?.Invoke("El lobby no tiene un host válido");
                    }
                }
            }
            else
            {
                Debug.LogError($"[SteamLobbyManager - Facepunch] Error al unirse a la sala: {result}");
                OnLobbyError?.Invoke($"Error al unirse: {result}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager - Facepunch] Excepción al unirse: {e.Message}");
            OnLobbyError?.Invoke(e.Message);
        }
    }

    public void LeaveLobby()
    {
        if (currentLobby.HasValue)
        {
            currentLobby.Value.Leave();
            currentLobby = null;
            isHost = false;
            clientSteamIds.Clear();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            Debug.Log("[SteamLobbyManager - Facepunch] Has abandonado la sala Steam.");
            OnLobbyLeft?.Invoke();
        }
    }

    // ------------------------------------------------------------------
    // Pérdida de conexión con el host (solo clientes)
    // ------------------------------------------------------------------

    private void RegisterClientDisconnectHandler()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // En el host este callback salta cuando cualquier cliente se va; solo nos
        // interesa el caso de un cliente puro que pierde al host.
        if (isHost) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

        // Si ya no hay lobby, la salida fue voluntaria (LeaveLobby) y ese flujo
        // ya gestiona el regreso al menú; evitamos eventos duplicados.
        if (!currentLobby.HasValue) return;

        Debug.Log("[SteamLobbyManager] Conexión con el host perdida o servidor cerrado.");

        currentLobby.Value.Leave();
        currentLobby = null;
        isHost = false;
        clientSteamIds.Clear();

        string motivo = !string.IsNullOrEmpty(MotivoDesconexion) ? MotivoDesconexion : "Conexión con el host perdida";
        MotivoDesconexion = null;
        OnLobbyError?.Invoke(motivo);

        if (OnLobbyLeft != null)
        {
            OnLobbyLeft.Invoke();
        }
        else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "StartMenu")
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("StartMenu");
        }
    }

    private void OnLobbyCreatedHandler(Result result, Lobby lobby)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Evento OnLobbyCreated: {result}");
    }

    private void OnLobbyEnteredHandler(Lobby lobby)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Evento OnLobbyEntered: {lobby.Id}");
    }

    public void StartGame(string sceneName = "SampleScene")
    {
        if (!isHost) return;
        Debug.Log($"[SteamLobbyManager - Facepunch] Host iniciando la partida en escena {sceneName}...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnLobbyMemberJoinedHandler(Lobby lobby, Friend friend)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Jugador unido a la sala: {friend.Name}");
        OnLobbyMemberJoinedEvent?.Invoke(lobby, friend);
    }

    private void OnLobbyMemberLeaveHandler(Lobby lobby, Friend friend)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Jugador salió de la sala: {friend.Name}");
        OnLobbyMemberLeftEvent?.Invoke(lobby, friend);
    }

    private void OnLobbyGameCreatedHandler(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Partida iniciada en puerto {port}");
    }

    private void OnGameLobbyJoinRequestedHandler(Lobby lobby, SteamId steamId)
    {
        Debug.Log($"[SteamLobbyManager - Facepunch] Invitación recibida de {friendName(steamId)}");
        JoinLobby(lobby);
    }

    private string friendName(SteamId id)
    {
        return new Friend(id).Name;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreatedHandler;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEnteredHandler;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoinedHandler;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeaveHandler;
        SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreatedHandler;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequestedHandler;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
}
