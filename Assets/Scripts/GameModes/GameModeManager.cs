using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Manager de modos de juego dentro de la partida (SampleScene).
/// Empieza con FFA: cuenta atrás 5s "LA PARTIDA COMIENZA EN", luego EnCurso,
/// scoring por kills (NetworkList), respawn con delay, fin por score/tiempo.
/// Funciona online (autoridad servidor) y offline (solo).
/// Debe estar en SampleScene sobre un NetworkObject (p.ej. "GameModeManager").
/// </summary>
public class GameModeManager : NetworkBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public enum EstadoPartida { Esperando = 0, CuentaAtras = 1, EnCurso = 2, Finalizada = 3 }

    [Header("Modo (uno solo: el asset decide FFA/TDM/futuros vía rulesId)")]
    [FormerlySerializedAs("modoFFA")]
    [Tooltip("Definición activa (FFA.asset, TDM.asset, ...). Nuevo modo = nuevo asset, sin campos nuevos aquí.")]
    public GameModeDefinition modo;

    [Header("Spawns")]
    [Tooltip("Si vacío, genera anillo de 8 puntos alrededor del origen")]
    public Transform[] spawnPoints;

    [Header("Avatar tardío (late join)")]
    [Tooltip("Prefab del jugador para spawnear a quien entra con la partida empezada.")]
    [SerializeField] private GameObject playerPrefab;

    // Sync
    public NetworkVariable<int> estado = new NetworkVariable<int>(
        (int)EstadoPartida.Esperando,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoPartida = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> ganadorClientId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Scoring: listas paralelas playerIds <-> kills <-> deaths (capa de red).
    // La decisión (quién suma, quién gana) vive en la Caja 3 (IGameModeRules).
    private NetworkList<ulong> playerIds = new NetworkList<ulong>();
    private NetworkList<int> kills = new NetworkList<int>();
    private NetworkList<int> deaths = new NetworkList<int>();
    // TDM: equipo por jugador (0/1/2) en el mismo índice + puntos por equipo [0,rojo,azul].
    private NetworkList<int> equiposNet = new NetworkList<int>();
    private NetworkList<int> puntosEquiposNet = new NetworkList<int>();

    /// <summary>Definición activa (un solo campo: el asset manda vía rulesId).</summary>
    public GameModeDefinition ModoActual() => modo;
    /// <summary>True si la partida es TDM por equipos (modo.rulesId == TDM).</summary>
    public bool EsTDM() => modo != null && modo.rulesId == "TDM" && Reglas is ITeamModeRules;

    [Header("Reglas (Caja 3)")]
    [Tooltip("Si vacío, se auto-crea según ModoActual().rulesId (FFA/TDM).")]
    [SerializeField] private MonoBehaviour reglasComponent;

    /// <summary>Caja 3 activa. El JEFE solo llama a esto, no sabe si es FFA o TDM.</summary>
    public IGameModeRules Reglas { get; private set; }

    public event System.Action OnScoresActualizados;
    public event System.Action<ulong, ulong, int> OnKillFeed;

    // Offline espejo local (cuando !IsSpawned las NetworkList no sincronizan pero sí funcionan local)
    private bool countdownLanzado = false;
    private float matchTimerLocal = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ResolverReglas();
    }

    /// <summary>
    /// Caja 2 busca su Caja 3: usa la asignada en Inspector o crea según
    /// ModoActual().rulesId (FFA/TDM). Si el modo es nulo, asume FFA.
    /// </summary>
    private void ResolverReglas()
    {
        if (Reglas != null) return;
        if (reglasComponent is IGameModeRules directa)
        {
            Reglas = directa;
            return;
        }
        // Si la regla en escena coincide con el modo activo, se respeta.
        string idPrevisto = ModoActual() != null && !string.IsNullOrEmpty(ModoActual().rulesId) ? ModoActual().rulesId : "FFA";
        var enEscena = GetComponent<IGameModeRules>();
        if (enEscena != null && enEscena.RulesId == idPrevisto)
        {
            Reglas = enEscena;
            reglasComponent = enEscena as MonoBehaviour;
            return;
        }
        string id = idPrevisto;
        if (string.IsNullOrEmpty(id)) id = "FFA";
        if (id == "TDM")
        {
            var tdm = GetComponent<TDMRules>();
            if (tdm == null) tdm = gameObject.AddComponent<TDMRules>();
            Reglas = tdm;
            reglasComponent = tdm;
        }
        else if (id == "FFA")
        {
            var ffa = GetComponent<FFARules>();
            if (ffa == null) ffa = gameObject.AddComponent<FFARules>();
            Reglas = ffa;
            reglasComponent = ffa;
        }
        else
        {
            Debug.LogWarning($"[GameModeManager] rulesId '{id}' sin implementación; usando FFA.");
            var ffa = GetComponent<FFARules>();
            if (ffa == null) ffa = gameObject.AddComponent<FFARules>();
            Reglas = ffa;
            reglasComponent = ffa;
        }
    }

    // ---------- API de equipos (TDM) ----------

    /// <summary>Equipo de un cliente: 0=ninguno, 1=Rojo, 2=Azul.</summary>
    public int GetEquipoDe(ulong clientId)
    {
        int i = IndiceJugador(clientId);
        if (i >= 0 && i < equiposNet.Count) return equiposNet[i];
        if (Reglas is ITeamModeRules tr) return tr.GetEquipo(clientId);
        return Equipo.Ninguno;
    }

    public int GetPuntosEquipo(int equipo)
    {
        if (equiposNet.Count == 0 && Reglas is ITeamModeRules tr0) return tr0.GetPuntosEquipo(equipo);
        if (equipo == Equipo.Rojo && puntosEquiposNet.Count > 1) return puntosEquiposNet[1];
        if (equipo == Equipo.Azul && puntosEquiposNet.Count > 2) return puntosEquiposNet[2];
        if (Reglas is ITeamModeRules tr) return tr.GetPuntosEquipo(equipo);
        return 0;
    }

    /// <summary>True si ambos tienen equipo válido y es el mismo (para fuego amigo).</summary>
    public bool SonMismoEquipo(ulong a, ulong b)
    {
        if (a == ulong.MaxValue || b == ulong.MaxValue) return false;
        if (a == b) return false;
        int ea = GetEquipoDe(a);
        int eb = GetEquipoDe(b);
        return Equipo.EsValido(ea) && ea == eb;
    }

    private void Start()
    {
        if (Reglas == null) ResolverReglas();
        if (ModoActual() == null)
            Debug.LogWarning("[GameModeManager] Sin GameModeDefinition asignado; usando valores por defecto (5s).");

        EnsureSpawnPoints();

        if (!IsSpawned)
        {
            // Offline / solo: arranca directo
            Debug.Log("[GameModeManager] Modo offline: iniciando FFA local.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "SampleScene")
                AvisosCarga.Empezar("Entrando a la partida...");
            StartCoroutine(RutinaOffline());
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        EnsureSpawnPoints();
        // El HUD (UI) se auto-crea solo; Gameplay no lo referencia.

        // Entrando a SampleScene: tapa con loading hasta que arranque el
        // countdown (cubre la ventana de migración). Late joiners (ya EnCurso)
        // no necesitan espera: se oculta de inmediato.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "SampleScene")
        {
            if (estado.Value == (int)EstadoPartida.Esperando)
                AvisosCarga.Empezar("Entrando a la partida...");
            else
                AvisosCarga.Terminar();
        }

        playerIds.OnListChanged += _ => OnScoresActualizados?.Invoke();
        kills.OnListChanged += _ => OnScoresActualizados?.Invoke();
        deaths.OnListChanged += _ => OnScoresActualizados?.Invoke();
        equiposNet.OnListChanged += _ => OnScoresActualizados?.Invoke();
        puntosEquiposNet.OnListChanged += _ => OnScoresActualizados?.Invoke();

        if (IsServer)
        {
            ResolverPrefabAvatar();
            InicializarPuntosEquipos();
            SustituirAvataresDeLobby();
            RegistrarJugadoresExistentes();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClienteConectado;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClienteDesconectado;

            // Suscribe muertes de jugadores ya presentes + futuros
            StartCoroutine(EsperarYSuscribirMuertes());
            StartCoroutine(RutinaServidor());
        }
        else
        {
            // Cliente: escucha fin para mostrar victoria
            ganadorClientId.OnValueChanged += (_, nuevo) =>
            {
                if (nuevo != ulong.MaxValue && estado.Value == (int)EstadoPartida.Finalizada)
                    MostrarVictoria(nuevo);
            };
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClienteConectado;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClienteDesconectado;
        }
        base.OnNetworkDespawn();
    }

    // ---------------- Offline ----------------

    private IEnumerator RutinaOffline()
    {
        estado.Value = (int)EstadoPartida.CuentaAtras;
        var modo = ModoActual();
        int segundos = modo != null ? modo.countdownSeconds : 5;
        string mensaje = modo != null ? modo.mensajeCuentaAtras : "LA PARTIDA COMIENZA EN";

        BloquearDisparoTodos(false);
        bool terminado = false;
        AvisosCarga.Terminar();
        MatchCountdown.Iniciar(segundos, mensaje, () => terminado = true);
        yield return new WaitUntil(() => terminado);

        estado.Value = (int)EstadoPartida.EnCurso;
        BloquearDisparoTodos(true);
        matchTimerLocal = modo != null && modo.matchDuration > 0 ? modo.matchDuration : 600f;

        // Suscribe muerte local para respawn offline
        foreach (var pc in PlayerController.Todos)
        {
            if (pc != null && pc.Vida != null)
            {
                var capturado = pc;
                pc.Vida.OnMuerte += () => StartCoroutine(RespawnOffline(capturado));
            }
        }
        Debug.Log("[GameModeManager] FFA offline en curso.");
    }

    private IEnumerator RespawnOffline(PlayerController pc)
    {
        float delay = ModoActual() != null ? ModoActual().respawnDelay : 3f;
        yield return new WaitForSeconds(delay);
        if (pc == null) yield break;
        pc.Respawn(GetSpawnAleatorio());
        if (pc.Vida != null) pc.Vida.ActivarProteccionSpawn(ProteccionActual());
    }

    private float ProteccionActual() => ModoActual() != null ? ModoActual().spawnProtection : 2f;

    // ---------------- Servidor ----------------

    private IEnumerator RutinaServidor()
    {
        // Input congelado desde el frame 1 (CuentaAtras): nadie se mueve ni
        // dispara en posiciones de migración mientras se colocan.
        estado.Value = (int)EstadoPartida.CuentaAtras;
        BloquearDisparoTodos(false);

        // Espera a que los avatares migren de LobbyRoom a SampleScene
        yield return new WaitForSeconds(1.5f);
        RegistrarJugadoresExistentes();
        yield return new WaitForSeconds(0.5f);

        // Al anillo de spawn (dueño aplica su propio teleport) y EN EL MISMO
        // instante arranca el overlay: un solo movimiento visible.
        ReposicionarTodosAlInicio();
        yield return null;

        var modoSrv = ModoActual();
        int segundos = modoSrv != null ? modoSrv.countdownSeconds : 5;
        string mensaje = modoSrv != null ? modoSrv.mensajeCuentaAtras : "LA PARTIDA COMIENZA EN";

        MostrarCuentaAtrasClientRpc(segundos, mensaje);

        // Espera a que el overlay termine en servidor (mismo tiempo real)
        yield return new WaitForSecondsRealtime(segundos + 0.3f);

        estado.Value = (int)EstadoPartida.EnCurso;
        tiempoPartida.Value = modoSrv != null && modoSrv.matchDuration > 0 ? modoSrv.matchDuration : 600f;
        DesbloquearDisparoClientRpc();
        Debug.Log($"[GameModeManager] {(EsTDM() ? "TDM" : "FFA")} en curso (online).");
    }

    // ---------- Lag compensation (solo servidor) ----------
    public LagCompensator Compensador { get; private set; }
    private float pingTimer = 0f;

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        // Ping/pong RTT en clientes (1 Hz): el servidor lo usa para el rewind.
        if (IsSpawned && !IsServer)
        {
            pingTimer += Time.deltaTime;
            if (pingTimer >= 1f)
            {
                pingTimer = 0f;
                PingServerRpc(nm.LocalTime.Time);
            }
            return;
        }

        if (!IsServer || estado.Value != (int)EstadoPartida.EnCurso) return;

        // Snapshots de todos los avatares para el rewind (tiempo de servidor).
        float ahora = (float)nm.LocalTime.Time;
        foreach (var pc in PlayerController.Todos)
        {
            if (pc == null) continue;
            if (Compensador == null) Compensador = new LagCompensator();
            Compensador.Registrar(pc.OwnerClientId, pc.transform.position, pc.transform.rotation, ahora);
        }

        if (tiempoPartida.Value > 0)
        {
            tiempoPartida.Value -= Time.deltaTime;
            if (tiempoPartida.Value <= 0f)
            {
                tiempoPartida.Value = 0f;
                FinalizarPorTiempo();
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void PingServerRpc(double tiempoCliente, RpcParams rpc = default)
    {
        // Eco directo al que preguntó (el resto no lo necesita).
        ulong quien = rpc.Receive.SenderClientId;
        var destino = RpcTarget.Single(quien, RpcTargetUse.Temp);
        PongClientRpc(tiempoCliente, new RpcParams { Send = new RpcSendParams { Target = destino } });
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PongClientRpc(double tiempoCliente, RpcParams rpc = default)
    {
        float rtt = (float)(NetworkManager.Singleton.LocalTime.Time - tiempoCliente);
        ReportarRttServerRpc(Mathf.Max(0f, rtt));
    }

    [Rpc(SendTo.Server)]
    private void ReportarRttServerRpc(float rtt, RpcParams rpc = default)
    {
        if (Compensador == null) Compensador = new LagCompensator();
        Compensador.ReportarRtt(rpc.Receive.SenderClientId, rtt);
    }

    // ---------------- Cuenta atrás ----------------

    [Rpc(SendTo.ClientsAndHost)]
    private void MostrarCuentaAtrasClientRpc(int segundos, string mensaje)
    {
        // Releva al loading: a partir de aquí manda el overlay 5s
        AvisosCarga.Terminar();
        MatchCountdown.Iniciar(segundos, mensaje, () => { });
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DesbloquearDisparoClientRpc()
    {
        foreach (var pc in PlayerController.Todos)
        {
            // Solo el dueño desbloquea su flag local; el servidor ya lo tiene
            if (pc.IsLocalPlayer) pc.canShoot = true;
        }
    }

    private void BloquearDisparoTodos(bool permitir)
    {
        foreach (var pc in PlayerController.Todos)
            pc.canShoot = permitir;
    }

    private void ReposicionarTodosAlInicio()
    {
        int i = 0;
        foreach (var pc in PlayerController.Todos)
        {
            pc.Respawn(GetSpawnPorIndice(i++));
            pc.canShoot = false;
            if (pc.Vida != null) pc.Vida.ActivarProteccionSpawn(ProteccionActual());
        }
    }

    // ---------------- Jugadores / scoring FFA ----------------

    private void OnClienteConectado(ulong clientId)
    {
        // Nuevo jugador a mitad de partida: regístralo y colócalo
        StartCoroutine(EsperarYRegistrar(clientId));
    }

    private void ResolverPrefabAvatar()
    {
        if (playerPrefab != null) return;
#if UNITY_EDITOR
        playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
#endif
    }

    private IEnumerator EsperarYRegistrar(ulong clientId)
    {
        // Late join: el cliente cae directo a SampleScene (sincronía de Netcode)
        // sin avatar porque el spawner vive en LobbyRoom. Se lo crea aquí;
        // su propio OnNetworkSpawn publica su loadout y munición.
        SpawnearAvatarTardio(clientId);
        yield return new WaitForSeconds(1f);
        RegistrarJugadoresExistentes();
        SuscribirMuertes();
    }

    /// <summary>
    /// Lobby y partida ya no comparten prefab: el avatar 2D del lobby migra con
    /// la escena (DestroyWithScene = false), así que hay que retirarlo y crear
    /// un Player 3D por cliente. Se llama antes de RegistrarJugadoresExistentes,
    /// que es quien lee PlayerController.Todos.
    /// </summary>
    private void SustituirAvataresDeLobby()
    {
        if (!IsServer) return;

        var deLobby = new List<LobbyWalkablePlayer2D>(LobbyWalkablePlayer2D.Todos);
        int retirados = 0;
        foreach (var avatar in deLobby)
        {
            if (avatar == null) continue;
            NetworkObject netObj = avatar.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            else Destroy(avatar.gameObject);
            retirados++;
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        int solicitados = 0;
        foreach (ulong clientId in nm.ConnectedClientsIds)
        {
            SpawnearAvatarTardio(clientId);
            solicitados++;
        }

        Debug.Log($"[GameModeManager] Swap lobby→partida: {solicitados} avatares 3D creados" + (retirados > 0 ? $", {retirados} avatares 2D retirados por nosotros" : " (los 2D ya los retiró el cambio de escena)") + ".");
    }

    private void SpawnearAvatarTardio(ulong clientId)
    {
        if (!IsServer || playerPrefab == null) return;
        if (PlayerController.PorCliente(clientId) != null) return; // ya tiene avatar

        if (EsTDM())
        {
            RegistrarEnListasSiFalta(clientId);
            AsignarEquipoBalanceado(clientId);
        }
        Vector3 pos = GetSpawnPara(clientId);
        GameObject go = Instantiate(playerPrefab, pos, Quaternion.identity);
        go.name = $"Player_Instance_{clientId}";
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null) netObj.SpawnWithOwnership(clientId);
        var vida = go.GetComponent<Health>();
        if (vida != null) vida.ActivarProteccionSpawn(ProteccionActual());
        Debug.Log($"[GameModeManager] Avatar tardío spawneado para cliente {clientId}.");
    }

    private void OnClienteDesconectado(ulong clientId)
    {
        // Mantiene su score histórico (visible en marcador final); solo retira
        // el avatar para no dejar cadáveres bloqueando spawns.
        if (!IsServer) return;
        var pc = PlayerController.PorCliente(clientId);
        if (pc == null) return;
        var netObj = pc.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(pc.gameObject);
        Debug.Log($"[GameModeManager] Avatar del cliente {clientId} retirado por desconexión (score conservado).");
    }

    private void InicializarPuntosEquipos()
    {
        if (!IsServer) return;
        // Bucle acotado: si un Add fallara en silencio, el while colgaría el hilo principal.
        for (int i = puntosEquiposNet.Count; i < 3; i++) puntosEquiposNet.Add(0);
    }

    private void RegistrarJugadoresExistentes()
    {
        if (Reglas == null) ResolverReglas();
        if (IsServer) InicializarPuntosEquipos();
        var pcs = PlayerController.Todos;
        foreach (var pc in pcs)
        {
            ulong id = pc.OwnerClientId;
            if (!ContieneJugador(id))
            {
                playerIds.Add(id);
                kills.Add(0);
                deaths.Add(0);
                equiposNet.Add(Equipo.Ninguno);
                Reglas.RegistrarJugador(id);
                Debug.Log($"[GameModeManager] Registrado jugador {id} ({pc.gameObject.name}).");
            }
            if (EsTDM()) AsignarEquipoBalanceado(id);
        }
        if (EsTDM()) SincronizarPuntosEquipos();
    }

    /// <summary>Asigna Rojo/Azul con menos gente (máx 6). Solo servidor.</summary>
    private void AsignarEquipoBalanceado(ulong clientId)
    {
        if (!IsServer) return;
        if (!(Reglas is ITeamModeRules tr)) return;
        int actual = tr.GetEquipo(clientId);
        if (Equipo.EsValido(actual)) { SincronizarEquipoEnRed(clientId, actual); return; }
        int equipo = tr.EquipoConHueco();
        if (equipo < 0)
        {
            Debug.LogWarning($"[GameModeManager][TDM] Equipos llenos (6v6), {clientId} queda sin equipo.");
            return;
        }
        tr.AsignarEquipo(clientId, equipo);
        SincronizarEquipoEnRed(clientId, equipo);
        Debug.Log($"[GameModeManager][TDM] Cliente {clientId} -> {Equipo.Nombre(equipo)}.");
    }

    private void SincronizarEquipoEnRed(ulong clientId, int equipo)
    {
        int i = IndiceJugador(clientId);
        if (i >= 0 && i < equiposNet.Count) equiposNet[i] = equipo;
    }

    private void SincronizarPuntosEquipos()
    {
        if (!IsServer) return;
        if (!(Reglas is ITeamModeRules tr)) return;
        InicializarPuntosEquipos();
        puntosEquiposNet[1] = tr.GetPuntosEquipo(Equipo.Rojo);
        puntosEquiposNet[2] = tr.GetPuntosEquipo(Equipo.Azul);
    }

    private bool ContieneJugador(ulong id)
    {
        for (int i = 0; i < playerIds.Count; i++)
            if (playerIds[i] == id) return true;
        return false;
    }

    private int IndiceJugador(ulong id)
    {
        for (int i = 0; i < playerIds.Count; i++)
            if (playerIds[i] == id) return i;
        return -1;
    }

    private IEnumerator EsperarYSuscribirMuertes()
    {
        yield return new WaitForSeconds(2f);
        SuscribirMuertes();
        // Reintenta por si spawnean tarde
        yield return new WaitForSeconds(3f);
        SuscribirMuertes();
    }

    private void SuscribirMuertes()
    {
        foreach (var pc in PlayerController.Todos)
        {
            if (pc.Vida == null) continue;
            pc.Vida.OnMuerteConInfo -= OnMuerteJugador;
            pc.Vida.OnMuerteConInfo += OnMuerteJugador;
        }
    }

    private void OnMuerteJugador(GameObject victima, ulong killerClientId)
    {
        if (!IsServer) return;
        if (estado.Value != (int)EstadoPartida.EnCurso) return;

        // Encuentra víctima por GameObject
        PlayerController pcVictima = null;
        if (victima != null) pcVictima = victima.GetComponentInParent<PlayerController>();
        if (pcVictima == null)
        {
            // Fallback: busca por último daño (recorre todos y encuentra el muerto reciente)
            foreach (var pc in PlayerController.Todos)
                if (pc.Vida != null && pc.Vida.Muerto) { pcVictima = pc; break; }
        }
        if (pcVictima == null) return;

        ulong victimaId = pcVictima.OwnerClientId;
        RegistrarEnListasSiFalta(victimaId);

        // Modelo autoritativo: decide kills y victoria (misma regla que los tests)
        if (Reglas == null) ResolverReglas();
        if (EsTDM() && Reglas is ITeamModeRules trTdm)
        {
            // Sin fuego amigo: el daño ya se bloqueó en Health, aquí solo se puntúa.
            trTdm.RegistrarKill(killerClientId, victimaId);
            int idxV = IndiceJugador(victimaId);
            if (idxV >= 0) deaths[idxV] = trTdm.GetDeaths(victimaId);
            if (killerClientId != ulong.MaxValue && killerClientId != victimaId)
            {
                RegistrarEnListasSiFalta(killerClientId);
                int idxK = IndiceJugador(killerClientId);
                if (idxK >= 0) kills[idxK] = trTdm.GetKills(killerClientId);
                int killsKiller = trTdm.GetKills(killerClientId);
                SincronizarPuntosEquipos();
                Debug.Log($"[GameModeManager][TDM] Kill: {killerClientId} -> {victimaId} ({Equipo.Nombre(trTdm.GetEquipo(killerClientId))} {trTdm.GetPuntosEquipo(trTdm.GetEquipo(killerClientId))})");
                AnunciarKillClientRpc(killerClientId, victimaId, killsKiller);
                int scoreToWin = ModoActual() != null ? ModoActual().scoreToWin : 50;
                var ganadorEq = trTdm.GanadorPorEquipos(scoreToWin);
                if (ganadorEq.HasValue)
                {
                    FinalizarConEquipo(ganadorEq.Value);
                    return;
                }
            }
        }
        else
        {
            Reglas.RegistrarKill(killerClientId, victimaId);

            // Réplica a las NetworkList para los clientes
            int idxV = IndiceJugador(victimaId);
            if (idxV >= 0) deaths[idxV] = Reglas.GetDeaths(victimaId);
            if (killerClientId != ulong.MaxValue && killerClientId != victimaId)
            {
                RegistrarEnListasSiFalta(killerClientId);
                int idxK = IndiceJugador(killerClientId);
                if (idxK >= 0) kills[idxK] = Reglas.GetKills(killerClientId);

                int killsKiller = Reglas.GetKills(killerClientId);
                Debug.Log($"[GameModeManager][FFA] Kill: {killerClientId} -> {victimaId} ({killsKiller} kills)");

                AnunciarKillClientRpc(killerClientId, victimaId, killsKiller);

                int scoreToWin = ModoActual() != null ? ModoActual().scoreToWin : 15;
                var ganador = Reglas.GanadorPorScore(scoreToWin);
                if (ganador.HasValue)
                {
                    FinalizarConGanador(ganador.Value);
                    return;
                }
            }
        }

        // Respawn con delay del modo
        float delay = ModoActual() != null ? ModoActual().respawnDelay : 3f;
        StartCoroutine(RespawnConDelay(pcVictima, delay));
    }

    private IEnumerator RespawnConDelay(PlayerController pc, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pc == null || estado.Value != (int)EstadoPartida.EnCurso) yield break;
        pc.Respawn(GetSpawnPara(pc.OwnerClientId));
        if (pc.Vida != null) pc.Vida.ActivarProteccionSpawn(ProteccionActual());
    }

    // ---------------- Fin ----------------

    private void FinalizarPorTiempo()
    {
        if (Reglas == null) ResolverReglas();
        if (EsTDM() && Reglas is ITeamModeRules tr)
        {
            FinalizarConEquipo(tr.LiderPorEquipos());
            return;
        }
        // Gana el de más kills; empate = registrado antes (regla testeada)
        FinalizarConGanador(Reglas.LiderPorTiempo());
    }

    private void RegistrarEnListasSiFalta(ulong id)
    {
        if (IndiceJugador(id) < 0)
        {
            playerIds.Add(id);
            kills.Add(0);
            deaths.Add(0);
            equiposNet.Add(Equipo.Ninguno);
            if (EsTDM()) AsignarEquipoBalanceado(id);
        }
    }

    private void FinalizarConGanador(ulong ganador)
    {
        if (estado.Value == (int)EstadoPartida.Finalizada) return;
        estado.Value = (int)EstadoPartida.Finalizada;
        ganadorClientId.Value = ganador;
        BloquearDisparoTodos(false);
        MostrarVictoriaClientRpc(ganador);
        Debug.Log($"[GameModeManager][FFA] Partida finalizada. Ganador: {ganador}");

        // Tras el overlay de victoria (4s), todos vuelven al lobby para revotar.
        if (IsServer) Invoke(nameof(RetornarAlLobby), 4.5f);
    }

    /// <summary>TDM: gana un equipo (1=Rojo, 2=Azul).</summary>
    public int equipoGanador = Equipo.Ninguno;

    private void FinalizarConEquipo(int equipo)
    {
        if (estado.Value == (int)EstadoPartida.Finalizada) return;
        estado.Value = (int)EstadoPartida.Finalizada;
        equipoGanador = equipo;
        ganadorClientId.Value = ulong.MaxValue;
        BloquearDisparoTodos(false);
        MostrarVictoriaEquipoClientRpc(equipo);
        Debug.Log($"[GameModeManager][TDM] Partida finalizada. Ganador: {Equipo.Nombre(equipo)} ({GetPuntosEquipo(Equipo.Rojo)}-{GetPuntosEquipo(Equipo.Azul)})");
        if (IsServer) Invoke(nameof(RetornarAlLobby), 4.5f);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void MostrarVictoriaEquipoClientRpc(int equipo)
    {
        equipoGanador = equipo;
        string victoria = ModoActual() != null ? ModoActual().mensajeVictoria : "¡VICTORIA!";
        MatchCountdown.Iniciar(4, $"{victoria}\nGanador: EQUIPO {Equipo.Nombre(equipo)}\n{GetPuntosEquipo(Equipo.Rojo)} - {GetPuntosEquipo(Equipo.Azul)}", () => { });
    }

    private void RetornarAlLobby()
    {
        if (!IsServer) return;
        if (estado.Value != (int)EstadoPartida.Finalizada) return;
        Debug.Log("[GameModeManager] Volviendo a LobbyRoom...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyRoom", LoadSceneMode.Single);
        else
            SceneManager.LoadScene("LobbyRoom");
    }

    /// <summary>
    /// DEBUG (editor): fuerza la victoria para probar el retorno al lobby
    /// sin jugar 15 kills. Click derecho en el componente → Forzar victoria.
    /// </summary>
    [ContextMenu("DEBUG: Forzar victoria y volver al lobby")]
    private void ForzarVictoriaDebug()
    {
        if (IsSpawned && !IsServer) return;
        estado.Value = (int)EstadoPartida.EnCurso;
        if (EsTDM())
        {
            FinalizarConEquipo(Equipo.Rojo);
            return;
        }
        ulong ganador = 0;
        if (IsSpawned && NetworkManager.Singleton != null)
        {
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) { ganador = id; break; }
        }
        FinalizarConGanador(ganador);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void MostrarVictoriaClientRpc(ulong ganador)
    {
        MostrarVictoria(ganador);
    }

    private void MostrarVictoria(ulong ganador)
    {
        string nombre = ResolverNombre(ganador);
        string victoria = ModoActual() != null ? ModoActual().mensajeVictoria : "¡VICTORIA!";
        MatchCountdown.Iniciar(4, $"{victoria}\nGanador: {nombre}", () => { });
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnunciarKillClientRpc(ulong killer, ulong victima, int killsKiller)
    {
        string nk = ResolverNombre(killer);
        string nv = ResolverNombre(victima);
        Debug.Log($"[FFA] {nk} eliminó a {nv} ({killsKiller} kills)");
        OnKillFeed?.Invoke(killer, victima, killsKiller);
    }

    public string GetPlayerDisplayName(ulong clientId) => ResolverNombre(clientId);

    public List<(ulong clientId, int killCount, int deathCount)> GetTablaFFA()
    {
        var tabla = new List<(ulong clientId, int killCount, int deathCount)>();
        // Las NetworkList sincronizan por separado: en clientes pueden llegar
        // desfasadas (playerIds antes que kills). Itera hasta el mínimo común.
        int n = Mathf.Min(playerIds.Count, Mathf.Min(kills.Count, deaths.Count));
        for (int i = 0; i < n; i++)
            tabla.Add((playerIds[i], kills[i], deaths[i]));
        tabla.Sort((a, b) => b.killCount.CompareTo(a.killCount));
        return tabla;
    }

    private string ResolverNombre(ulong clientId)
    {
        if (SteamLobbyManager.Instance != null)
        {
            string n = SteamLobbyManager.Instance.GetPlayerName(clientId);
            if (!string.IsNullOrEmpty(n)) return n;
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == clientId)
            return "TÚ";
        return $"Jugador #{clientId}";
    }

    // ---------------- Spawns ----------------

    private List<Transform> spawnsRojo = new List<Transform>();
    private List<Transform> spawnsAzul = new List<Transform>();

    private void EnsureSpawnPoints()
    {
        if (EsTDM()) { EnsureSpawnPointsEquipos(); return; }
        if (spawnPoints != null && spawnPoints.Length > 0) return;
        // Genera anillo de 8 puntos r=6 alrededor del origen, y=1.2
        var list = new List<Transform>();
        var holder = new GameObject("FFA_SpawnPoints");
        holder.transform.SetParent(transform, false);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            var go = new GameObject($"Spawn_{i}");
            go.transform.SetParent(holder.transform, false);
            go.transform.position = new Vector3(Mathf.Cos(a) * 6f, 1.2f, Mathf.Sin(a) * 6f);
            go.transform.rotation = Quaternion.LookRotation(-go.transform.position.normalized, Vector3.up);
            list.Add(go.transform);
        }
        spawnPoints = list.ToArray();
    }

    /// <summary>TDM: 6 puntos por equipo, Rojo en x=-8, Azul en x=+8.</summary>
    private void EnsureSpawnPointsEquipos()
    {
        if (spawnsRojo.Count == 6 && spawnsAzul.Count == 6) return;
        spawnsRojo.Clear();
        spawnsAzul.Clear();
        var holder = new GameObject("TDM_SpawnPoints");
        holder.transform.SetParent(transform, false);
        for (int i = 0; i < 6; i++)
        {
            float z = (i - 2.5f) * 2.5f;
            var goR = new GameObject($"SpawnRojo_{i}");
            goR.transform.SetParent(holder.transform, false);
            goR.transform.position = new Vector3(-8f, 1.2f, z);
            goR.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            spawnsRojo.Add(goR.transform);
            var goA = new GameObject($"SpawnAzul_{i}");
            goA.transform.SetParent(holder.transform, false);
            goA.transform.position = new Vector3(8f, 1.2f, z);
            goA.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            spawnsAzul.Add(goA.transform);
        }
    }

    private Vector3 GetSpawnAleatorio()
    {
        if (EsTDM())
        {
            // Sin equipo claro: alterna para no acumular.
            return Random.value < 0.5f ? GetSpawnEquipo(Equipo.Rojo) : GetSpawnEquipo(Equipo.Azul);
        }
        if (spawnPoints == null || spawnPoints.Length == 0) return new Vector3(0, 1.2f, 0);
        return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    }

    /// <summary>Spawn aleatorio de un equipo concreto.</summary>
    public Vector3 GetSpawnEquipo(int equipo)
    {
        if (equipo == Equipo.Rojo && spawnsRojo.Count > 0)
            return spawnsRojo[Random.Range(0, spawnsRojo.Count)].position;
        if (equipo == Equipo.Azul && spawnsAzul.Count > 0)
            return spawnsAzul[Random.Range(0, spawnsAzul.Count)].position;
        return GetSpawnAleatorio();
    }

    /// <summary>Spawn según el equipo del cliente (TDM) o aleatorio (FFA).</summary>
    public Vector3 GetSpawnPara(ulong clientId)
    {
        if (EsTDM()) return GetSpawnEquipo(GetEquipoDe(clientId));
        return GetSpawnAleatorio();
    }

    private Vector3 GetSpawnPorIndice(int i)
    {
        if (EsTDM())
        {
            // Inicio: intercala equipos para repartir el anillo.
            int equipo = (i % 2 == 0) ? Equipo.Rojo : Equipo.Azul;
            var lista = equipo == Equipo.Rojo ? spawnsRojo : spawnsAzul;
            if (lista.Count > 0) return lista[(i / 2) % lista.Count].position;
        }
        if (spawnPoints == null || spawnPoints.Length == 0) return new Vector3(i * 2f, 1.2f, 0);
        return spawnPoints[i % spawnPoints.Length].position;
    }

    // ---------------- API pública (Scoreboard futuro) ----------------

    public int GetKills(ulong clientId)
    {
        int i = IndiceJugador(clientId);
        return (i >= 0 && i < kills.Count) ? kills[i] : 0;
    }

    public int GetDeaths(ulong clientId)
    {
        int i = IndiceJugador(clientId);
        return (i >= 0 && i < deaths.Count) ? deaths[i] : 0;
    }

    public bool EnCurso => estado.Value == (int)EstadoPartida.EnCurso;
    public bool EnCuentaAtras => estado.Value == (int)EstadoPartida.CuentaAtras;
}
