using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Fachada del jugador: orquesta Locomoción + Combate + Vida y expone la API
/// que usan UI, modos de juego y cámara. Sin lógica propia de movimiento
/// ni de armas (viven en PlayerLocomotion / PlayerCombat).
/// </summary>
[DisallowMultipleComponent]
public class PlayerController : NetworkBehaviour
{
    public PlayerLocomotion Locomocion { get; private set; }
    public PlayerCombat Combate { get; private set; }
    public PlayerSkills Habilidades { get; private set; }

    // La vida vive en el componente Health (autoridad de servidor)
    public Health Vida { get; private set; }

    private PlayerSmoothMovement smoothMovement;
    private PlayerUI playerUI;
    internal IRecargaUI Ui => playerUI;

    public new bool IsLocalPlayer => !IsSpawned || IsOwner;

    // ---------- Registro estático (sustituye FindObjectsByType) ----------
    private static readonly HashSet<PlayerController> instancias = new HashSet<PlayerController>();
    private static readonly Dictionary<ulong, PlayerController> porCliente = new Dictionary<ulong, PlayerController>();

    public static IReadOnlyCollection<PlayerController> Todos => instancias;

    public static PlayerController PorCliente(ulong clientId)
    {
        porCliente.TryGetValue(clientId, out var pc);
        return pc;
    }

    private void Registrar()
    {
        instancias.Add(this);
        if (IsSpawned) porCliente[OwnerClientId] = this;
    }

    private void Desregistrar()
    {
        instancias.Remove(this);
        if (IsSpawned && porCliente.TryGetValue(OwnerClientId, out var pc) && pc == this)
            porCliente.Remove(OwnerClientId);
    }

    // ---------- Fachada (delegan a los sub-sistemas) ----------

    public Transform cameraHolder => Locomocion != null ? Locomocion.cameraHolder : null;

    public bool grounded
    {
        get => Locomocion != null && Locomocion.grounded;
        set { if (Locomocion != null) Locomocion.grounded = value; }
    }

    public bool isRunning
    {
        get => Locomocion != null && Locomocion.IsRunning;
        set { if (Locomocion != null) Locomocion.IsRunning = value; }
    }

    public bool canShoot
    {
        get => Combate == null || Combate.canShoot;
        set { if (Combate != null) Combate.canShoot = value; }
    }

    public WeaponRuntime currentRuntime => Combate != null ? Combate.currentRuntime : null;
    public WeaponDefinition currentWeapon => Combate != null ? Combate.currentWeapon : null;
    public List<WeaponDefinition> Weapons => Combate != null ? Combate.Weapons : null;

    public void ChangeWeapon(int index) { if (Combate != null) Combate.ChangeWeapon(index); }
    public void StartReload() { if (Combate != null) Combate.StartReload(); }
    public void StopReload() { if (Combate != null) Combate.StopReload(); }
    public void SetGroundedState(bool grounded) { if (Locomocion != null) Locomocion.SetGroundedState(grounded); }
    public void Respawn(Vector3 posicion) { if (Combate != null) Combate.Respawn(posicion); }

    // ---------- Ciclo de vida ----------

    private void Awake()
    {
        Locomocion = GetComponent<PlayerLocomotion>();
        Combate = GetComponent<PlayerCombat>();
        Habilidades = GetComponent<PlayerSkills>();
        Vida = GetComponent<Health>();
        if (Vida != null)
        {
            Vida.OnMuerte += ManejarMuerte;
        }
        GetSmoothMovement();
    }

    public override void OnDestroy()
    {
        if (Vida != null)
        {
            Vida.OnMuerte -= ManejarMuerte;
        }
        Desregistrar();
        base.OnDestroy();
    }

    private void ManejarMuerte()
    {
        canShoot = false;
        Debug.Log($"[PlayerController] {gameObject.name} ha muerto. (Respawn lo gestiona GameModeManager)");
    }

    public PlayerSmoothMovement GetSmoothMovement()
    {
        if (smoothMovement != null) return smoothMovement;

        smoothMovement = GetComponentInChildren<PlayerSmoothMovement>(true);
        if (smoothMovement == null)
            smoothMovement = GetComponentInParent<PlayerSmoothMovement>();
        if (smoothMovement == null)
            smoothMovement = GetComponent<PlayerSmoothMovement>();

        return smoothMovement;
    }

    private void Start()
    {
        // Soporte para pruebas offline / singleplayer sin Netcode activo
        if (!IsSpawned)
        {
            Registrar();
            SetupLocalPlayer();
        }
    }

    public override void OnNetworkSpawn()
    {
        Registrar();
        if (IsOwner)
        {
            SetupLocalPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
    }

    public override void OnNetworkDespawn()
    {
        Desregistrar();
    }

    // ---------- Input (delegado) ----------

    /// <summary>
    /// True si un panel modal (armería, votación) exige quietud total:
    /// ni moverse ni disparar.
    /// </summary>
    public bool InterfazModalAbierta()
    {
        if (LobbyArmeriaController.Instance != null && LobbyArmeriaController.Instance.AbiertaAhora)
            return true;
        return BloqueoInterfaz.HayModalAbierta;
    }

    private bool EnCuentaAtras()
    {
        if (MatchCountdown.Activo) return true;
        var gm = GameModeManager.Instance;
        return gm != null && gm.EnCuentaAtras;
    }

    /// <summary>Para sistemas con input propio (p. ej. scroll de PlayerSmoothMovement).</summary>
    public bool EntradaDeJuegoBloqueada() => InterfazModalAbierta() || EnCuentaAtras();

    private void Update()
    {
        if (Locomocion == null || Combate == null) return;
        if (!IsLocalPlayer) return;

        // Modal abierta: quietud total + freno (no deslizar con
        // la última velocidad).
        // (Las hotkeys B/I/TAB/ESC las gestiona LobbyArmeriaController)
        if (InterfazModalAbierta())
        {
            Locomocion.Detener();
            return;
        }

        // Cuenta atrás (lobby o partida): se puede mirar, pero no
        // moverse ni disparar.
        if (EnCuentaAtras())
        {
            Locomocion.Detener();
            Locomocion.Look();
            return;
        }

        Locomocion.TickLocal();
        Combate.TickLocal();
        if (Habilidades != null) Habilidades.TickLocal();
    }

    private void FixedUpdate()
    {
        if (Locomocion == null) return;
        if (!IsLocalPlayer) return;
        // Sin este guard el Rigidbody seguía moviéndose con modales abiertos
        if (EntradaDeJuegoBloqueada()) return;

        Locomocion.TickFixed();
    }

    // ---------- Cámara local / remota ----------

    private void SetupLocalPlayer()
    {
        Camera playerCamera = Locomocion != null ? Locomocion.ObtenerCamara() : Camera.main;
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";

            var audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Desactiva la cámara de vista general de la sala (LobbyRoom) si existe,
        // ahora que la cámara del jugador local toma el control.
        GameObject overviewCam = GameObject.Find("Overview Main Camera");
        if (overviewCam != null)
        {
            overviewCam.SetActive(false);
        }

        // Bind PlayerUI si existe en la escena
        playerUI = FindFirstObjectByType<PlayerUI>();
        if (playerUI != null)
        {
            playerUI.player = this;
        }
    }

    private void SetupRemotePlayer()
    {
        // El movimiento de los jugadores remotos llega por NetworkTransform;
        // su Rigidbody debe ser kinámico para que la física local no pelee
        // con las actualizaciones de red.
        if (Locomocion != null)
        {
            Locomocion.SetCuerpoCinematico(true);
        }

        Transform holder = cameraHolder;
        if (holder != null)
        {
            Camera remoteCam = holder.GetComponentInChildren<Camera>(true);
            if (remoteCam != null)
            {
                remoteCam.gameObject.SetActive(false);
                remoteCam.enabled = false;

                var audioListener = remoteCam.GetComponent<AudioListener>();
                if (audioListener != null) audioListener.enabled = false;
            }
        }
    }
}
