using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Transporte para Netcode for GameObjects sobre Steam Networking Sockets (P2P relay).
///
/// - El HOST crea un "relay socket" de Steam. Los clientes se conectan a él usando el
///   SteamID del host (publicado en los datos del lobby), todo relayado por la red de
///   Valve: sin abrir puertos, sin IPs, funciona a través de NAT (estilo Crab Game / PEAK).
/// - Modo servidor: cada conexión Steam entrante se mapea a un transport clientId único.
/// - Modo cliente: una única conexión hacia el host (ServerClientId).
/// </summary>
[DisallowMultipleComponent]
public class SteamP2PTransport : NetworkTransport, ISocketManager, IConnectionManager
{
    private const int VirtualPort = 27015;
    private const float ClientConnectTimeoutSeconds = 25f;

    [Tooltip("SteamID del host al que conectarse (solo modo cliente). Lo rellena SteamLobbyManager desde los datos del lobby.")]
    public ulong HostSteamId;

    private SocketManager socketManager;          // solo servidor
    private ConnectionManager connectionManager;  // solo cliente

    private bool isServer;
    private bool isClient;
    private bool clientConnected;
    private float clientConnectStartTime;
    private bool clientTimedOut;

    private ulong nextTransportId = 1;
    private readonly Dictionary<uint, ulong> connectionToTransportId = new Dictionary<uint, ulong>();
    private readonly Dictionary<ulong, Connection> transportIdToConnection = new Dictionary<ulong, Connection>();

    private struct QueuedEvent
    {
        public NetworkEvent Type;
        public ulong ClientId;
        public byte[] Data;
        public float ReceiveTime;
    }
    private readonly Queue<QueuedEvent> eventQueue = new Queue<QueuedEvent>();

    public override ulong ServerClientId => 0;

    public override void Initialize(NetworkManager networkManager = null)
    {
        // Sin inicialización adicional necesaria.
    }

    // ------------------------------------------------------------------
    // Arranque
    // ------------------------------------------------------------------

    /// <summary>
    /// Reintenta una creación de socket/conexión relay. El SDR tarda segundos
    /// en estar listo tras arrancar; el primer intento suele devolver socket
    /// inválido y los siguientes ya funcionan. No se fía del callback de
    /// estado (SteamNetworkingUtils.Status) porque en esta versión de
    /// Facepunch nunca sale de Unknown: se prueba directamente.
    /// </summary>
    private static T Reintentar<T>(Func<T> crear, string que) where T : class
    {
        Exception ultimoError = null;
        for (int i = 0; i < 4; i++)
        {
            try
            {
                T resultado = crear();
                if (resultado != null) return resultado;
                ultimoError = new Exception("resultado nulo");
            }
            catch (Exception e) { ultimoError = e; }

            try { SteamNetworkingUtils.InitRelayNetworkAccess(); } catch { /* reintento */ }
            if (SteamClient.IsValid) SteamClient.RunCallbacks();
            Debug.LogWarning($"[SteamP2PTransport] {que} no listo (intento {i + 1}/4), reintentando...");
            System.Threading.Thread.Sleep(1000);
        }
        Debug.LogError($"[SteamP2PTransport] {que} falló tras 4 intentos: {ultimoError?.Message}");
        return null;
    }

    public override bool StartServer()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            Debug.LogError("[SteamP2PTransport] No se puede crear el socket relay: Steam no está inicializado.");
            return false;
        }

        ShutdownInternal();

        socketManager = Reintentar(() => SteamNetworkingSockets.CreateRelaySocket(VirtualPort, this), "socket relay servidor");
        if (socketManager == null)
        {
            ShutdownInternal();
            return false;
        }

        isServer = true;
        Debug.Log($"[SteamP2PTransport] Socket relay P2P creado (virtual port {VirtualPort}). SteamID del host: {SteamClient.SteamId}");
        return true;
    }

    public override bool StartClient()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized)
        {
            Debug.LogError("[SteamP2PTransport] No se puede conectar: Steam no está inicializado.");
            return false;
        }

        if (HostSteamId == 0)
        {
            Debug.LogError("[SteamP2PTransport] HostSteamId no configurado. No se puede conectar al host.");
            return false;
        }

        ShutdownInternal();

        connectionManager = Reintentar(() =>
        {
            var mgr = SteamNetworkingSockets.ConnectRelay(HostSteamId, VirtualPort, this);
            if (mgr == null || mgr.Connection.Id == 0)
                throw new Exception("Invalid Connection");
            return mgr;
        }, "conexión P2P relay");
        if (connectionManager == null)
        {
            ShutdownInternal();
            return false;
        }

        isClient = true;
        clientConnected = false;
        clientTimedOut = false;
        clientConnectStartTime = Time.realtimeSinceStartup;
        Debug.Log($"[SteamP2PTransport] Iniciando conexión P2P relay hacia el host {HostSteamId}...");
        return true;
    }

    // ------------------------------------------------------------------
    // Bombeo de mensajes (main thread)
    // ------------------------------------------------------------------

    private void Update()
    {
        if (isServer && socketManager != null)
        {
            socketManager.Receive();
        }

        if (isClient && connectionManager != null)
        {
            connectionManager.Receive();

            // Timeout de conexión: Steam P2P no tiene timeout propio de conexión.
            if (!clientConnected && !clientTimedOut &&
                Time.realtimeSinceStartup - clientConnectStartTime > ClientConnectTimeoutSeconds)
            {
                clientTimedOut = true;
                Debug.LogError("[SteamP2PTransport] Timeout conectando al host por Steam P2P.");
                SetDisconnectEvent(DisconnectEvents.MaxConnectionAttempts, "Timeout conectando al host por Steam P2P");
                EnqueueEvent(NetworkEvent.Disconnect, ServerClientId, null);
                connectionManager.Close();
            }
        }
    }

    private void EnqueueEvent(NetworkEvent type, ulong clientId, byte[] data)
    {
        eventQueue.Enqueue(new QueuedEvent
        {
            Type = type,
            ClientId = clientId,
            Data = data,
            ReceiveTime = Time.realtimeSinceStartup
        });
    }

    // ------------------------------------------------------------------
    // Netcode: envío y polling
    // ------------------------------------------------------------------

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        if (payload.Array == null || payload.Count == 0) return;

        SendType sendType = (networkDelivery == NetworkDelivery.Unreliable || networkDelivery == NetworkDelivery.UnreliableSequenced)
            ? SendType.Unreliable
            : SendType.Reliable;

        if (isServer)
        {
            if (clientId == ServerClientId) return; // el host no se envía a sí mismo por la red
            if (transportIdToConnection.TryGetValue(clientId, out Connection connection))
            {
                connection.SendMessage(payload.Array, payload.Offset, payload.Count, sendType);
            }
        }
        else if (isClient)
        {
            // Topología cliente-servidor: el cliente solo habla con el host.
            if (clientId != ServerClientId) return;
            if (connectionManager != null && connectionManager.Connection.Id > 0)
            {
                connectionManager.Connection.SendMessage(payload.Array, payload.Offset, payload.Count, sendType);
            }
        }
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        if (eventQueue.Count > 0)
        {
            QueuedEvent queued = eventQueue.Dequeue();
            clientId = queued.ClientId;
            receiveTime = queued.ReceiveTime;
            payload = queued.Data != null ? new ArraySegment<byte>(queued.Data) : default;
            return queued.Type;
        }

        clientId = 0;
        payload = default;
        receiveTime = 0f;
        return NetworkEvent.Nothing;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (isServer && transportIdToConnection.TryGetValue(clientId, out Connection connection))
        {
            connection.Close();
        }
    }

    public override void DisconnectLocalClient()
    {
        if (isClient && connectionManager != null)
        {
            connectionManager.Close();
        }
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        // Steam no expone RTT de forma sencilla por esta API; 0 = desconocido.
        return 0;
    }

    public override void Shutdown()
    {
        ShutdownInternal();
    }

    private void ShutdownInternal()
    {
        if (socketManager != null)
        {
            try { socketManager.Close(); } catch { /* ya cerrado */ }
            socketManager = null;
        }

        if (connectionManager != null)
        {
            try { connectionManager.Close(); } catch { /* ya cerrado */ }
            connectionManager = null;
        }

        connectionToTransportId.Clear();
        transportIdToConnection.Clear();
        eventQueue.Clear();
        nextTransportId = 1;
        isServer = false;
        isClient = false;
        clientConnected = false;
        clientTimedOut = false;
    }

    // ------------------------------------------------------------------
    // ISocketManager (servidor: conexiones entrantes)
    // ------------------------------------------------------------------

    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        // Se acepta la conexión a nivel de transporte; la autenticación real la hace
        // Netcode vía ConnectionApproval (el cliente manda su SteamID como payload).
        connection.Accept();
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        ulong transportId = nextTransportId++;
        connectionToTransportId[connection.Id] = transportId;
        transportIdToConnection[transportId] = connection;

        SteamId remoteSteamId = info.Identity.SteamId;
        Debug.Log($"[SteamP2PTransport] Cliente Steam conectado: {remoteSteamId} -> transportId {transportId}");
        EnqueueEvent(NetworkEvent.Connect, transportId, null);
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        if (connectionToTransportId.TryGetValue(connection.Id, out ulong transportId))
        {
            connectionToTransportId.Remove(connection.Id);
            transportIdToConnection.Remove(transportId);
            Debug.Log($"[SteamP2PTransport] Cliente Steam desconectado: transportId {transportId} ({info.State})");
            EnqueueEvent(NetworkEvent.Disconnect, transportId, null);
        }
        connection.Close();
    }

    public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        if (size <= 0) return;
        if (!connectionToTransportId.TryGetValue(connection.Id, out ulong transportId)) return;

        byte[] copy = new byte[size];
        Marshal.Copy(data, copy, 0, size);
        EnqueueEvent(NetworkEvent.Data, transportId, copy);
    }

    // ------------------------------------------------------------------
    // IConnectionManager (cliente: conexión hacia el host)
    // ------------------------------------------------------------------

    public void OnConnecting(ConnectionInfo info)
    {
        // Esperando a que el relay de Steam establezca la ruta.
    }

    public void OnConnected(ConnectionInfo info)
    {
        clientConnected = true;
        Debug.Log("[SteamP2PTransport] Conectado al host por Steam P2P.");
        EnqueueEvent(NetworkEvent.Connect, ServerClientId, null);
    }

    public void OnDisconnected(ConnectionInfo info)
    {
        if (clientTimedOut) return; // ya se notificó el timeout
        Debug.Log($"[SteamP2PTransport] Desconectado del host ({info.State}).");
        SetDisconnectEvent(DisconnectEvents.ClosedByRemote, $"Desconectado del host ({info.State})");
        EnqueueEvent(NetworkEvent.Disconnect, ServerClientId, null);
    }

    public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        if (size <= 0) return;

        byte[] copy = new byte[size];
        Marshal.Copy(data, copy, 0, size);
        EnqueueEvent(NetworkEvent.Data, ServerClientId, copy);
    }
}
