using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Avatar de lobby en 2D (vista cenital). Sustituye al LobbyWalkablePlayer 3D:
/// movimiento en el plano XY con Rigidbody2D sin gravedad y sincronización de
/// transform con autoridad del dueño (ClientNetworkTransform + NetworkRigidbody2D).
///
/// Optimizaciones frente al avatar 3D (Player.prefab: 28 GameObjects / 110 componentes):
/// - No lleva cámara ni AudioListener: la lobby usa una cámara ortográfica fija.
/// - No lleva canvas de HUD: el prefab es 1 GameObject con 7 componentes.
/// - Sin luces ni materiales 3D: un solo SpriteRenderer por avatar.
/// </summary>
[DisallowMultipleComponent]
public class LobbyWalkablePlayer2D : NetworkBehaviour
{
    /// <summary>
    /// Avatar del jugador local de esta máquina. La UI lo consulta en vez de
    /// buscarlo por tag o recorrer la escena.
    /// </summary>
    public static LobbyWalkablePlayer2D Local { get; private set; }

    // Registro por cliente (mismo patrón que PlayerController.Todos/PorCliente).
    // Permite al servidor retirar los avatares 2D al entrar a la partida sin
    // recorrer la escena ni depender de la escena en la que esté el objeto.
    private static readonly Dictionary<ulong, LobbyWalkablePlayer2D> porCliente =
        new Dictionary<ulong, LobbyWalkablePlayer2D>();

    public static LobbyWalkablePlayer2D PorCliente(ulong clientId)
    {
        porCliente.TryGetValue(clientId, out var avatar);
        return avatar;
    }

    public static IReadOnlyCollection<LobbyWalkablePlayer2D> Todos => porCliente.Values;

    [Header("Movimiento")]
    [SerializeField] private float velocidad = 6f;
    [SerializeField] private float multiplicadorSprint = 1.45f;

    [Header("Apariencia")]
    [SerializeField] private SpriteRenderer cuerpo;
    [SerializeField] private bool colorearPorCliente = true;

    [Header("Etiqueta (opcional: la rellena el sistema de nametags)")]
    [SerializeField] private TextMeshPro etiquetaNombre;

    private Rigidbody2D rb;
    private Vector2 entrada;
    private bool sprint;

    public SpriteRenderer Cuerpo => cuerpo;
    public TextMeshPro Etiqueta => etiquetaNombre;

    /// <summary>
    /// Igual que PlayerController.IsLocalPlayer: sin Netcode activo el avatar
    /// se controla igualmente (pruebas offline abriendo la escena directamente).
    /// </summary>
    public bool EsLocal => !IsSpawned || IsOwner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        // Lobby cenital: sin gravedad y sin rotación por física.
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (cuerpo == null) cuerpo = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Modo offline: la escena se abrió sin Netcode activo.
        if (!IsSpawned)
        {
            // NetworkRigidbody2D arranca el cuerpo Kinematic en Awake y solo lo
            // pasa a Dynamic en OnNetworkSpawn (según autoridad). Sin red ese
            // spawn nunca ocurre, así que se fuerza aquí para poder probar.
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            RegistrarLocal();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (cuerpo == null) cuerpo = GetComponentInChildren<SpriteRenderer>(true);

        if (IsSpawned) porCliente[OwnerClientId] = this;

        AplicarColor();

        if (EsLocal)
        {
            // El dueño mueve su cuerpo con física dinámica; el resto queda
            // Kinematic y lo interpola NetworkTransform (lo decide NetworkRigidbody2D).
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            RegistrarLocal();
        }
    }

    public override void OnNetworkDespawn()
    {
        Desregistrar();
    }

    public override void OnDestroy()
    {
        Desregistrar();
        base.OnDestroy();
    }

    /// <summary>
    /// Borra la entrada del registro sin depender de OwnerClientId (puede venir
    /// ya reseteado cuando llega OnNetworkDespawn).
    /// </summary>
    private void Desregistrar()
    {
        ulong clave = ulong.MaxValue;
        foreach (var kv in porCliente)
        {
            if (kv.Value == this) { clave = kv.Key; break; }
        }
        if (clave != ulong.MaxValue) porCliente.Remove(clave);

        if (Local == this) Local = null;
    }

    private void RegistrarLocal()
    {
        Local = this;
        ActualizarEtiqueta();
    }

    private void Update()
    {
        if (!EsLocal) return;

        if (MovimientoBloqueado())
        {
            entrada = Vector2.zero;
            sprint = false;
            return;
        }

        entrada = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (entrada.sqrMagnitude > 1f) entrada.Normalize();
        sprint = Input.GetKey(KeyCode.LeftShift);
    }

    private void FixedUpdate()
    {
        if (!EsLocal || rb == null) return;

        float v = sprint ? velocidad * multiplicadorSprint : velocidad;
        rb.linearVelocity = entrada * v;
    }

    /// <summary>
    /// Con modal abierta (armería, votación, inventario) el avatar no se mueve.
    /// Las hotkeys las gestiona LobbyArmeriaController.
    /// </summary>
    private static bool MovimientoBloqueado()
    {
        if (LobbyArmeriaController.Instance != null && LobbyArmeriaController.Instance.AbiertaAhora) return true;
        return BloqueoInterfaz.HayModalAbierta;
    }

    private void AplicarColor()
    {
        if (!colorearPorCliente || cuerpo == null) return;
        cuerpo.color = ColorDeCliente(OwnerClientId);
    }

    /// <summary>Color estable y bien repartido por clientId (proporción áurea).</summary>
    public static Color ColorDeCliente(ulong clientId)
    {
        float h = (clientId * 0.6180339887f) % 1f;
        return Color.HSVToRGB(h, 0.62f, 0.95f);
    }

    private void ActualizarEtiqueta()
    {
        if (etiquetaNombre == null) return;

        ulong id = IsSpawned ? OwnerClientId : 0UL;

        string nombre;
        if (SteamLobbyManager.Instance != null)
            nombre = SteamLobbyManager.Instance.GetPlayerName(id);
        else if (Steamworks.SteamClient.IsValid)
            nombre = Steamworks.SteamClient.Name;
        else
            nombre = $"Jugador #{id}";

        bool esHost = NetworkManager.Singleton != null && id == NetworkManager.ServerClientId;
        etiquetaNombre.text = esHost ? $"{nombre} <color=#00FF88>[HOST]</color>" : nombre;
    }
}
