using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Componente de vida con autoridad de servidor.
/// La vida viaja en una NetworkVariable que solo escribe el servidor.
/// Funciona también sin red (modo offline del editor).
/// </summary>
public class Health : NetworkBehaviour, IDamageable
{
    [SerializeField] private float vidaMaxima = 100f;

    public NetworkVariable<float> vidaActual = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Vida máxima efectiva (base + pasivas de clase). La escribe SOLO el
    /// servidor; los clientes la reciben por red. 0 = sin fijar (usa vidaMaxima).
    /// </summary>
    public NetworkVariable<float> vidaMaximaRed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Respaldo local para el modo offline del editor (sin Netcode activo)
    private float vidaMaximaLocal = 0f;

    /// <summary>
    /// Protección anti spawn-kill: mientras está activa se ignora todo daño.
    /// La escribe el servidor (GameModeManager al spawnear/respawnear).
    /// </summary>
    public NetworkVariable<bool> protegido = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Se invoca en el servidor al aplicar daño (cantidad aplicada).</summary>
    public event Action<float> OnDanioAplicado;

    /// <summary>Se invoca en el servidor cuando la vida llega a 0.</summary>
    public event Action OnMuerte;

    /// <summary>Se invoca en el servidor al morir con info del asesino (para GameMode FFA).</summary>
    public event Action<GameObject, ulong> OnMuerteConInfo;

    public GameObject UltimoAtacante { get; private set; }
    public ulong UltimoAtacanteClientId { get; private set; }

    /// <summary>Se invoca en todos los clientes cuando la vida cambia (sincronización).</summary>
    public event Action<float, float> OnVidaCambiada;

    public float Vida => vidaActual != null ? vidaActual.Value : vidaMaxima;
    public float VidaMaxima => VidaMaximaEfectiva();

    /// <summary>Vida máxima base del prefab, sin modificadores (la usan las pasivas).</summary>
    public float VidaMaximaBase => vidaMaxima;

    private float VidaMaximaEfectiva()
    {
        float enRed = vidaMaximaRed != null ? vidaMaximaRed.Value : 0f;
        if (enRed > 0f) return enRed;
        if (vidaMaximaLocal > 0f) return vidaMaximaLocal;
        return vidaMaxima;
    }

    /// <summary>
    /// Fija la vida máxima efectiva. Solo servidor (u offline). La usan las
    /// pasivas de clase al spawnear y los buffs que cambian la vida máxima.
    /// </summary>
    public void EstablecerVidaMaxima(float valor, bool curarHastaMaximo = false)
    {
        if (IsSpawned && !IsServer) return;

        float nueva = Mathf.Max(1f, valor);
        vidaMaximaLocal = nueva;
        if (IsSpawned) vidaMaximaRed.Value = nueva;

        vidaActual.Value = curarHastaMaximo ? nueva : Mathf.Min(vidaActual.Value, nueva);
    }
    public bool Muerto => Vida <= 0f;

    private MeshRenderer cuerpoRenderer;
    private Coroutine proteccionRutina;

    private void Awake()
    {
        vidaActual.OnValueChanged += ManejarCambioVida;
        cuerpoRenderer = transform.Find("Capsule")?.GetComponent<MeshRenderer>();
    }

    private void Update()
    {
        // Parpadeo visible de protección en todos los peers
        if (cuerpoRenderer == null) return;
        if (protegido.Value)
            cuerpoRenderer.enabled = (Time.time * 8f) % 1f < 0.6f;
        else if (!cuerpoRenderer.enabled)
            cuerpoRenderer.enabled = true;
    }

    public override void OnNetworkSpawn()
    {
        // El servidor fija la vida inicial autoritativa
        if (IsServer)
        {
            vidaActual.Value = VidaMaxima;
        }
    }

    public override void OnDestroy()
    {
        vidaActual.OnValueChanged -= ManejarCambioVida;
        base.OnDestroy();
    }

    private void ManejarCambioVida(float anterior, float nueva)
    {
        OnVidaCambiada?.Invoke(anterior, nueva);
    }

    public void RecibirDanio(float cantidad, Vector3 punto, Vector3 direccion, GameObject instigador)
    {
        if (cantidad <= 0f || Muerto) return;

        ulong killerClientId = ResolverClientId(instigador);
        if (EsFuegoAmigo(killerClientId)) return;

        if (IsServer)
        {
            UltimoAtacante = instigador;
            UltimoAtacanteClientId = killerClientId;
            AplicarDanioServidor(cantidad);
        }
        else if (IsSpawned)
        {
            SolicitarDanioServerRpc(cantidad, killerClientId);
        }
        else
        {
            // Modo offline (sin Netcode activo): aplicación directa
            UltimoAtacante = instigador;
            UltimoAtacanteClientId = killerClientId;
            AplicarDanioServidor(cantidad);
        }
    }

    private static ulong ResolverClientId(GameObject go)
    {
        if (go == null) return ulong.MaxValue;
        // Solo se lee OwnerClientId si hay red activa; en offline se devuelve
        // SinAsesino sin tocar Netcode (sus getters no son seguros sin spawn).
        var nb = go.GetComponentInParent<NetworkBehaviour>();
        if (nb != null && nb.IsSpawned && nb.OwnerClientId != ulong.MaxValue) return nb.OwnerClientId;
        var no = go.GetComponentInParent<NetworkObject>();
        if (no != null && no.IsSpawned && no.OwnerClientId != ulong.MaxValue) return no.OwnerClientId;
        return ulong.MaxValue;
    }

    /// <summary>Activa invulnerabilidad temporal (spawn/respawn). Solo servidor u offline.</summary>
    public void ActivarProteccionSpawn(float segundos)
    {
        if (IsSpawned && !IsServer) return;
        if (segundos <= 0f) return;
        if (proteccionRutina != null) StopCoroutine(proteccionRutina);
        protegido.Value = true;
        proteccionRutina = StartCoroutine(DesactivarProteccionTras(segundos));
    }

    private IEnumerator DesactivarProteccionTras(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        protegido.Value = false;
        proteccionRutina = null;
    }

    private void AplicarDanioServidor(float cantidad)
    {
        if (Muerto) return;
        if (protegido.Value) return;

        vidaActual.Value = Mathf.Max(0f, vidaActual.Value - cantidad);
        OnDanioAplicado?.Invoke(cantidad);

        if (vidaActual.Value <= 0f)
        {
            OnMuerte?.Invoke();
            OnMuerteConInfo?.Invoke(UltimoAtacante, UltimoAtacanteClientId);
        }
    }

    /// <summary>Ignora daño entre compañeros del mismo equipo (vale para TDM y futuros modos por equipos).</summary>
    private bool EsFuegoAmigo(ulong killerClientId)
    {
        var gm = GameModeManager.Instance;
        if (gm == null) return false;
        ulong victimaId = ResolverClientId(gameObject);
        if (victimaId == ulong.MaxValue) return false;
        return gm.SonMismoEquipo(killerClientId, victimaId);
    }

    [Rpc(SendTo.Server)]
    private void SolicitarDanioServerRpc(float cantidad, ulong killerClientId)
    {
        if (EsFuegoAmigo(killerClientId)) return;
        UltimoAtacanteClientId = killerClientId;
        // Resuelve GameObject del asesino en servidor por clientId
        UltimoAtacante = null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(killerClientId, out var cli))
        {
            if (cli.PlayerObject != null) UltimoAtacante = cli.PlayerObject.gameObject;
            else UltimoAtacante = PlayerController.PorCliente(killerClientId)?.gameObject;
        }
        AplicarDanioServidor(cantidad);
    }

    /// <summary>Restaura la vida (solo servidor; p. ej. respawn o curación).</summary>
    public void Curar(float cantidad)
    {
        if (!IsServer && IsSpawned) return;
        vidaActual.Value = Mathf.Min(VidaMaxima, vidaActual.Value + cantidad);
    }

    public void RestaurarVida()
    {
        if (!IsServer && IsSpawned) return;
        vidaActual.Value = VidaMaxima;
    }
}
