using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawner de avatares de lobby 2D. Solo actúa en el servidor: crea un avatar
/// por cliente con SpawnWithOwnership (el dueño tiene autoridad de movimiento)
/// y lo destruye al desconectarse.
///
/// Sustituye a LobbyAvatarSpawner (3D). Aquel además "adoptaba" los
/// PlayerController de SampleScene porque lobby y partida compartían prefab;
/// con prefabs separados (LobbyAvatar2D / Player 3D) esa adopción ya no aplica:
/// el servidor despawneará el avatar de partida y spawneará este.
/// </summary>
[DisallowMultipleComponent]
public class LobbyAvatarSpawner2D : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab del avatar 2D. Debe estar registrado en DefaultNetworkPrefabs.")]
    [SerializeField] private GameObject avatarPrefab;

    [Header("Puntos de aparición (plano XY)")]
    [Tooltip("Si está vacío se reparten en círculo alrededor de fallbackCenter.")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector2 fallbackCenter = Vector2.zero;
    [SerializeField] private float fallbackRadius = 2.5f;

    [Header("Límite")]
    [SerializeField] private int maxAvatares = 32;

    private readonly Dictionary<ulong, GameObject> avatares = new Dictionary<ulong, GameObject>();

    /// <summary>Avatares vivos en el servidor (para métricas/debug).</summary>
    public int Total => avatares.Count;

    private void Start()
    {
        // Fallback offline: escena abierta sin Netcode activo (pruebas en editor).
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            SpawnOffline();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback += AlConectarse;
        nm.OnClientDisconnectCallback += AlDesconectarse;

        foreach (ulong clientId in nm.ConnectedClientsIds)
            SpawnearPara(clientId);
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= AlConectarse;
            nm.OnClientDisconnectCallback -= AlDesconectarse;
        }

        avatares.Clear();
    }

    private void AlConectarse(ulong clientId)
    {
        if (IsServer) SpawnearPara(clientId);
    }

    private void AlDesconectarse(ulong clientId)
    {
        if (!IsServer) return;

        if (avatares.TryGetValue(clientId, out GameObject avatar) && avatar != null)
        {
            NetworkObject netObj = avatar.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn();
            else Destroy(avatar);
        }

        avatares.Remove(clientId);
    }

    private void SpawnearPara(ulong clientId)
    {
        if (avatares.TryGetValue(clientId, out GameObject existente) && existente != null) return;
        if (avatares.Count >= maxAvatares) return;

        // El avatar de partida (3D) migra con la escena al volver al lobby:
        // se retira antes de crear el 2D para no tener dos cuerpos por jugador.
        RetirarAvatarDePartida(clientId);

        if (avatarPrefab == null)
        {
            Debug.LogError("[LobbyAvatarSpawner2D] avatarPrefab sin asignar: no se puede spawnear.");
            return;
        }

        Vector2 posicion = PosicionDeSlot(avatares.Count);
        GameObject go = Instantiate(avatarPrefab, posicion, Quaternion.identity);
        go.name = $"LobbyAvatar2D_{clientId}";

        NetworkObject netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[LobbyAvatarSpawner2D] El prefab no tiene NetworkObject.");
            Destroy(go);
            return;
        }

        netObj.SpawnWithOwnership(clientId);
        avatares[clientId] = go;
    }

    /// <summary>Elimina el Player 3D de este cliente si venía de SampleScene.</summary>
    private void RetirarAvatarDePartida(ulong clientId)
    {
        PlayerController pc = PlayerController.PorCliente(clientId);
        if (pc == null) return;

        NetworkObject netObj = pc.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        else Destroy(pc.gameObject);

        Debug.Log($"[LobbyAvatarSpawner2D] Avatar 3D retirado al cliente {clientId}.");
    }

    private Vector2 PosicionDeSlot(int indice)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[indice % spawnPoints.Length].position;

        int total = Mathf.Max(1, maxAvatares);
        float angulo = indice * (Mathf.PI * 2f) / total;
        return fallbackCenter + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * fallbackRadius;
    }

    private void SpawnOffline()
    {
        if (avatarPrefab == null) return;

        GameObject go = Instantiate(avatarPrefab, PosicionDeSlot(0), Quaternion.identity);
        go.name = "LobbyAvatar2D_Offline";
        avatares[0] = go;
    }
}
