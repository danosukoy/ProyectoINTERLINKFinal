using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using Steamworks;
using Steamworks.Data;
using UnityEngine.SceneManagement;
using Image = UnityEngine.UI.Image;
using Color = UnityEngine.Color;

public class SteamLobbyListUI : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyListPanel;

    [Header("Session Indicator")]
    [SerializeField] private TMP_Text sessionStatusText;
    [SerializeField] private Image sessionStatusIndicator;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button enterLobbiesButton;
    [SerializeField] private Button inventarioButton;
    [SerializeField] private Button opcionButton;
    [SerializeField] private Button quitGameButton;

    [Header("Lobby List UI References")]
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button createButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Create Lobby Modal")]
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Text maxPlayersValueText;
    [SerializeField] private Button decreaseMaxPlayersButton;
    [SerializeField] private Button increaseMaxPlayersButton;
    [SerializeField] private Button confirmCreateButton;
    [SerializeField] private Button cancelCreateButton;

    [Header("In-Lobby Room UI References (in 3D LobbyRoom)")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button leaveInLobbyButton;

    private List<GameObject> pooledLobbyItems = new List<GameObject>();
    private List<Lobby> currentLobbies = new List<Lobby>();
    private bool isInventoryOpen = false;
    private int currentMaxPlayers = 16;

    // Sistemas propios de StartMenu (se apagan mientras el inventario está abierto)
    private Camera menuCam;
    private EventSystem menuEventSystem;
    private AudioListener menuListener;

    private void OnEnable()
    {
        // El cableado vive aquí (no solo en Start): si el Canvas arranca
        // apagado o se apaga/enciende, los botones siempre quedan vivos.
        // Ambas funciones son idempotentes (quitan antes de poner).
        SetupUIReferences();
        SetupEventListeners();
    }

    private void Start()
    {
        menuCam = Camera.main;
        menuEventSystem = FindFirstObjectByType<EventSystem>();
        if (menuCam != null) menuListener = menuCam.GetComponent<AudioListener>();

        if (SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            SetupInLobbyRoomUI();
        }
        else
        {
            // StartMenu arranca en la SALA 2D (PlayerArea): solo el entorno,
            // sin el overlay de menús (Steam_Background). Los paneles se abren
            // desde las estaciones: StationLobby y StationInventario.
            MostrarSala2D();

            if (SteamLobbyManager.VolverALaLista)
            {
                SteamLobbyManager.VolverALaLista = false;
                Debug.Log("[SteamLobbyListUI] Venías de una sala (salida/expulsión): usa la estación de salas para buscar otra.");
            }
        }

        // Reenganche garantizado: Start corre después de todos los Awake, así
        // que aqui SteamLobbyManager.Instance ya existe aunque OnEnable llegara
        // antes (ver SetupEventListeners).
        SetupEventListeners();

        // Destino listo: por si algún Terminar se perdió por el camino
        AvisosCarga.Terminar();

        // Mensaje de una desconexión anterior (p. ej. caída del host)
        if (!string.IsNullOrEmpty(MensajePendiente))
        {
            UpdateStatus($"Error: {MensajePendiente}");
            MensajePendiente = null;
        }
    }

    private void SetupUIReferences()
    {
        if (lobbyListContainer == null)
        {
            var contentObj = GameObject.Find("Content");
            if (contentObj != null) lobbyListContainer = contentObj.transform;
        }

        if (enterLobbiesButton != null)
        {
            enterLobbiesButton.onClick.RemoveAllListeners();
            enterLobbiesButton.onClick.AddListener(OnEnterLobbiesClicked);
        }

        if (inventarioButton != null && HayCanvasInventario())
        {
            inventarioButton.onClick.RemoveAllListeners();
            inventarioButton.onClick.AddListener(OnOpenInventoryClicked);
        }

        if (opcionButton != null)
        {
            opcionButton.onClick.RemoveAllListeners();
            opcionButton.onClick.AddListener(OnOpcionClicked);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
            quitGameButton.onClick.AddListener(OnQuitGameClicked);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }

        if (refreshButton != null) { refreshButton.onClick.RemoveAllListeners(); refreshButton.onClick.AddListener(OnRefreshClicked); }
        if (createButton != null) { createButton.onClick.RemoveAllListeners(); createButton.onClick.AddListener(OnCreateClicked); }
        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.RemoveAllListeners();
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        }

        if (decreaseMaxPlayersButton != null)
        {
            decreaseMaxPlayersButton.onClick.RemoveAllListeners();
            decreaseMaxPlayersButton.onClick.AddListener(OnDecreaseMaxPlayersClicked);
        }

        if (increaseMaxPlayersButton != null)
        {
            increaseMaxPlayersButton.onClick.RemoveAllListeners();
            increaseMaxPlayersButton.onClick.AddListener(OnIncreaseMaxPlayersClicked);
        }

        if (confirmCreateButton != null) { confirmCreateButton.onClick.RemoveAllListeners(); confirmCreateButton.onClick.AddListener(OnConfirmCreateClicked); }
        if (cancelCreateButton != null) { cancelCreateButton.onClick.RemoveAllListeners(); cancelCreateButton.onClick.AddListener(OnCancelCreateClicked); }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }

        // Ojo: en el LobbyRoom este mismo botón rojo lleva el listener
        // persistente LobbyArmeriaController.Alternar. Si aquí se añadiera
        // también OnOpenInventoryClicked, un clic haría las dos cosas: abrir la
        // armería y marcar el inventario de StartMenu (que no existe allí),
        // dejando el movimiento bloqueado permanentemente.
        if (inventoryButton != null && HayCanvasInventario())
        {
            inventoryButton.onClick.RemoveAllListeners();
            inventoryButton.onClick.AddListener(OnOpenInventoryClicked);
            // Blindaje: el botón de inventario nunca debe abandonar la sala.
            inventoryButton.onClick.RemoveListener(OnLeaveLobbyClicked);
        }

        if (leaveInLobbyButton != null)
        {
            leaveInLobbyButton.onClick.RemoveAllListeners();
            leaveInLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
            // Blindaje simétrico: el botón de salir nunca abre el inventario.
            leaveInLobbyButton.onClick.RemoveListener(OnOpenInventoryClicked);
        }

        if (createLobbyPanel != null) createLobbyPanel.SetActive(false);
        if (lobbyItemPrefab == null) lobbyItemPrefab = CreateDefaultItemPrefab();

        UpdateMaxPlayersText();
    }

    private void SetupEventListeners()
    {
        // Idempotente y re-llamable. Motivo: OnEnable puede ejecutarse ANTES de
        // que SteamLobbyManager haya corrido su Awake, así que Instance es null
        // y la suscripción se perdía. Después FindLobbies encontraba las salas e
        // invocaba OnLobbiesFound sin nadie escuchando: la UI se quedaba en
        // "Buscando..." hasta el timeout de 12s. Por eso también se llama desde
        // Start() (ya con todos los Awake hechos) y desde RefreshLobbies().
        var manager = SteamLobbyManager.Instance != null
            ? SteamLobbyManager.Instance
            : FindFirstObjectByType<SteamLobbyManager>();

        if (manager != null)
        {
            // Quitar antes de poner: si ya estaba suscrito, no se duplica.
            manager.OnLobbiesFound -= OnLobbiesFoundHandler;
            manager.OnLobbyJoined -= OnLobbyJoinedHandler;
            manager.OnLobbyLeft -= OnLobbyLeftHandler;
            manager.OnLobbyCreated -= OnLobbyCreatedHandler;
            manager.OnLobbyError -= OnLobbyErrorHandler;

            manager.OnLobbiesFound += OnLobbiesFoundHandler;
            manager.OnLobbyJoined += OnLobbyJoinedHandler;
            manager.OnLobbyLeft += OnLobbyLeftHandler;
            manager.OnLobbyCreated += OnLobbyCreatedHandler;
            manager.OnLobbyError += OnLobbyErrorHandler;
        }

        UpdateSessionIndicator();
    }

    public void UpdateSessionIndicator()
    {
        bool isConnected = SteamManager.Instance != null && SteamManager.Instance.IsInitialized;

        if (sessionStatusText != null)
        {
            if (isConnected)
            {
                sessionStatusText.text = $"<color=#00FF88>● SESIÓN INICIADA:</color> {SteamClient.Name} <size=14><color=#80C0FF>(SteamID: {SteamClient.SteamId})</color></size>";
            }
            else
            {
                sessionStatusText.text = "<color=#FF4444>● STEAM NO DETECTADO:</color> Abre el cliente de Steam para habilitar el multijugador";
            }
        }

        if (sessionStatusIndicator != null)
        {
            sessionStatusIndicator.color = isConnected ? new Color(0.0f, 0.9f, 0.4f, 1f) : new Color(0.9f, 0.2f, 0.2f, 1f);
        }
    }

    public void OnDecreaseMaxPlayersClicked()
    {
        currentMaxPlayers = Mathf.Clamp(currentMaxPlayers - 1, 2, 32);
        UpdateMaxPlayersText();
    }

    public void OnIncreaseMaxPlayersClicked()
    {
        currentMaxPlayers = Mathf.Clamp(currentMaxPlayers + 1, 2, 32);
        UpdateMaxPlayersText();
    }

    private void UpdateMaxPlayersText()
    {
        if (maxPlayersValueText != null)
        {
            maxPlayersValueText.text = $"{currentMaxPlayers} Jugadores";
        }
    }

    public void OnEnterLobbiesClicked()
    {
        ShowLobbyListState();
        RefreshLobbies();
    }

    /// <summary>Abre el panel de opciones (sensibilidad + volumen).</summary>
    public void OnOpcionClicked()
    {
        OptionsMenu.Abrir();
    }

    public void OnBackToMenuClicked()
    {
        ShowMainMenuState();
    }

    private GameObject fondoMenu;

    private void AsegurarFondo()
    {
        if (fondoMenu != null) return;
        var todos = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            var t = todos[i];
            if (t != null && t.name == "Steam_Background") { fondoMenu = t.gameObject; break; }
        }
    }

    /// <summary>
    /// Estado inicial de StartMenu: la SALA 2D a la vista y el overlay de menús
    /// (Steam_Background, que además es el padre de los tres paneles) apagado.
    /// Es el estado al arrancar la escena y al que se vuelve con ESC.
    /// </summary>
    public void MostrarSala2D()
    {
        // Si hay inventario abierto, cerrarlo primero: su cierre restaura el
        // menú, así que el apagado del overlay va después.
        if (isInventoryOpen) CloseInventory();

        AsegurarFondo();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyListPanel != null) lobbyListPanel.SetActive(false);
        if (createLobbyPanel != null) createLobbyPanel.SetActive(false);
        if (fondoMenu != null) fondoMenu.SetActive(false);
        BloqueoInterfaz.CerrarModal();
    }

    /// <summary>True si el overlay de menús está a la vista.</summary>
    private bool OverlayAbierto()
    {
        AsegurarFondo();
        return fondoMenu != null && fondoMenu.activeSelf;
    }

    public void ShowMainMenuState()
    {
        CloseInventory();
        AsegurarFondo();
        if (createLobbyPanel != null) createLobbyPanel.SetActive(false);
        if (fondoMenu != null && !fondoMenu.activeSelf) fondoMenu.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyListPanel != null) lobbyListPanel.SetActive(false);
        BloqueoInterfaz.AbrirModal();
        UpdateSessionIndicator();
    }

    public void ShowLobbyListState()
    {
        CloseInventory();
        AsegurarFondo();
        if (fondoMenu != null && !fondoMenu.activeSelf) fondoMenu.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyListPanel != null) lobbyListPanel.SetActive(true);
        if (createButton != null) createButton.gameObject.SetActive(true);
        if (refreshButton != null) refreshButton.gameObject.SetActive(true);
        BloqueoInterfaz.AbrirModal();
        UpdateSessionIndicator();
    }

    private void SetupInLobbyRoomUI()
    {
        // INICIAR PARTIDA se muestra al host (multijugador con sala) o cuando
        // NO hay sala activa: es el modo SOLO (o LobbyRoom abierta en el editor).
        bool haySala = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.CurrentLobby.HasValue;
        bool esHost = SteamLobbyManager.Instance?.IsHost ?? false;

        if (startGameButton != null) startGameButton.gameObject.SetActive(esHost || !haySala);
        if (inventoryButton != null) inventoryButton.gameObject.SetActive(true);
    }

    public void OnRefreshClicked()
    {
        RefreshLobbies();
    }

    private bool buscandoSalas = false;
    private Coroutine timeoutBusqueda;

    public void RefreshLobbies()
    {
        // Red de seguridad: si el manager se creó después que nosotros, aquí se
        // reenganchan los eventos antes de pedir la lista.
        SetupEventListeners();

        UpdateStatus("Buscando salas públicas en Steam...");
        if (SteamLobbyManager.Instance != null)
        {
            buscandoSalas = true;
            if (timeoutBusqueda != null) StopCoroutine(timeoutBusqueda);
            timeoutBusqueda = StartCoroutine(TimeoutBusqueda(12f));
            SteamLobbyManager.Instance.FindLobbies();
        }
        else
        {
            UpdateStatus("Sin conexión con Steam.");
        }
    }

    private System.Collections.IEnumerator TimeoutBusqueda(float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        timeoutBusqueda = null;
        if (buscandoSalas)
        {
            buscandoSalas = false;
            UpdateStatus("Steam no respondió. Revisa tu conexión y pulsa actualizar.");
            Debug.LogWarning("[SteamLobbyListUI] Timeout buscando salas (12s sin respuesta).");
        }
    }

    public void OnCreateClicked()
    {
        if (createLobbyPanel == null) { OnConfirmCreateClicked(); return; }

        createLobbyPanel.SetActive(true);
        BloqueoInterfaz.AbrirModal();
        EnfocarNombreSala();
    }

    /// <summary>
    /// Deja el cursor en el campo de nombre nada más abrir el modal.
    ///
    /// Sin esto, escribir dependía de que el clic acertara en el campo: si el
    /// clic cae en el borde o el EventSystem cambia el seleccionado, el input
    /// queda "seleccionado pero sin cursor" (isFocused == false) y el teclado
    /// no escribe nada. Aquí se fuerza el foco por código.
    ///
    /// No se toca el texto ni la selección: TMP ya selecciona todo al enfocar
    /// (onFocusSelectAll) y, si se deja vacío, OnConfirmCreateClicked pone un
    /// nombre por defecto.
    /// </summary>
    private void EnfocarNombreSala()
    {
        if (lobbyNameInput == null) return;

        // Nombre por defecto visible: así el usuario ve que el campo funciona
        // aunque no haya escrito aún.
        if (string.IsNullOrWhiteSpace(lobbyNameInput.text))
        {
            bool haySteam = SteamManager.Instance != null && SteamManager.Instance.IsInitialized;
            lobbyNameInput.text = haySteam && !string.IsNullOrEmpty(SteamClient.Name)
                ? $"Partida de {SteamClient.Name}"
                : "Mi partida";
        }

        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(lobbyNameInput.gameObject);

        // El foco real se aplica en el LateUpdate de TMP.
        lobbyNameInput.ActivateInputField();
    }

    public void OnConfirmCreateClicked()
    {
        // El submit de TMP también salta al DESELECCIONAR el campo, así que solo
        // se acepta si el modal está realmente abierto. Sin esta guarda, abrir
        // el modal y perder el foco creaba la sala sola con el nombre por defecto.
        if (createLobbyPanel != null && !createLobbyPanel.activeSelf) return;

        string name = lobbyNameInput != null && !string.IsNullOrWhiteSpace(lobbyNameInput.text) ? lobbyNameInput.text : $"Partida de {SteamClient.Name}";
        int max = Mathf.Clamp(currentMaxPlayers, 2, 32);

        UpdateStatus("Creando sala 3D en Steam...");
        if (createLobbyPanel != null) createLobbyPanel.SetActive(false);
        BloqueoInterfaz.CerrarModal();
        ReactivarJugador2D();

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.CreateLobby(name, max);
        }
    }

    public void OnCancelCreateClicked()
    {
        if (createLobbyPanel != null) createLobbyPanel.SetActive(false);
        BloqueoInterfaz.CerrarModal();
        ReactivarJugador2D();
    }

    /// <summary>Reactiva el PlayerController2D si se había desactivado por un modal.</summary>
    private void ReactivarJugador2D()
    {
        var pc = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (pc != null && !pc.enabled) pc.enabled = true;
    }

    public void OnLeaveLobbyClicked()
    {
        CloseInventory();
        UpdateStatus("Abandonando sala...");
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }
        SceneManager.LoadScene("StartMenu");
    }

    public void OnStartGameClicked()
    {
        CloseInventory();
        if (cuentaAtrasActiva) return;

        bool haySala = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.CurrentLobby.HasValue;
        bool esHost = SteamLobbyManager.Instance?.IsHost ?? false;

        // Con sala activa, solo el host puede iniciar la partida.
        if (haySala && !esHost) return;

        cuentaAtrasActiva = true;
        if (startGameButton != null) startGameButton.interactable = false;

        UpdateStatus("La partida comienza en 5 segundos...");

        if (haySala && esHost)
        {
            // MULTIJUGADOR: cuenta atrás sincronizada; todos los clientes (y el
            // host) ven el mismo contador y al terminar el host carga la escena.
            var ctrl = FindFirstObjectByType<LobbyMatchController>();
            if (ctrl != null && ctrl.IsSpawned)
            {
                ctrl.IniciarCuentaAtras();
            }
            else
            {
                MatchCountdown.Iniciar(5, "LA PARTIDA COMIENZA EN", () =>
                {
                    cuentaAtrasActiva = false;
                    if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsHost)
                        SteamLobbyManager.Instance.StartGame("SampleScene");
                });
            }
        }
        else
        {
            // SOLO / offline (sin sala): cuenta atrás local de 5s y carga
            // directa de SampleScene sin red (nada de Steam).
            MatchCountdown.Iniciar(5, "PARTIDA EN SOLITARIO", () =>
            {
                cuentaAtrasActiva = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene("SampleScene");
            });
        }
    }

    /// <summary>Evita arranques duplicados durante la cuenta atrás.</summary>
    private bool cuentaAtrasActiva = false;

    private void Update()
    {
        // ESC cierra el inventario y devuelve el menú
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isInventoryOpen) CloseInventory();
            // Primero el modal de crear sala: ESC debe cancelar solo eso.
            else if (createLobbyPanel != null && createLobbyPanel.activeSelf) OnCancelCreateClicked();
            // Desde cualquier panel (lista o menú principal) se vuelve a la sala 2D.
            else if (OverlayAbierto()) MostrarSala2D();
        }

        // Mientras el modal de crear sala está abierto, el campo de nombre debe
        // seguir enfocado. Cualquier Submit/Cancel del mando (o del propio
        // ratón al tocar un botón) se lo quita; si se pierde el foco el usuario
        // deja de poder escribir, así que se recupera aquí.
        // SetSelectedGameObject no hace nada si ya está seleccionado, y TMP
        // ignora ActivateInputField si ya está activo: es seguro cada frame.
        if (createLobbyPanel != null && createLobbyPanel.activeSelf
            && lobbyNameInput != null && !lobbyNameInput.isFocused)
        {
            EnfocarNombreSala();
        }

        // Auto-reparación: sin inventario abierto el menú debe tener input.
        // Si el EventSystem quedó apagado (p.ej. apertura fallida), el input
        // de la sala (nombre) muere; se reenciende solo.
        // Además, en Unity 6 a veces el EventSystem está enabled pero
        // EventSystem.current == null porque OnEnable no registró el static.
        // Forzamos la re-registro invocando OnEnable si hace falta.
        if (!isInventoryOpen)
        {
            if (menuEventSystem == null)
                menuEventSystem = FindFirstObjectByType<EventSystem>();
            if (menuEventSystem != null)
            {
                if (!menuEventSystem.enabled)
                    menuEventSystem.enabled = true;
                else if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    // Re-ejecutar OnEnable para que registre EventSystem.current
                    var onEnable = typeof(EventSystem).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (onEnable != null) onEnable.Invoke(menuEventSystem, null);
                }
            }
        }

        SincronizarBloqueoInterfaz();
    }

    /// <summary>
    /// Recalcula el bloqueo de movimiento a partir de lo que se ve en pantalla,
    /// en vez de confiar en las llamadas AbrirModal/CerrarModal repartidas por
    /// la UI. Así un cierre que se salte el CerrarModal (p. ej. el inventario,
    /// que apaga este Canvas y por tanto este Update) no deja al jugador
    /// congelado al volver a la sala 2D.
    /// </summary>
    private void SincronizarBloqueoInterfaz()
    {
        BuscarCanvasInventario();

        bool panelDeMenu =
               (mainMenuPanel != null && mainMenuPanel.activeSelf)
            || (lobbyListPanel != null && lobbyListPanel.activeSelf)
            || (createLobbyPanel != null && createLobbyPanel.activeSelf);

        bool inventario =
               isInventoryOpen
            || (invRaiz != null && invRaiz.activeInHierarchy);

        bool opciones = OptionsMenu.Instance != null && OptionsMenu.Instance.Abierto;

        if (panelDeMenu || inventario || opciones) BloqueoInterfaz.AbrirModal();
        else BloqueoInterfaz.CerrarModal();
    }

    public void OnOpenInventoryClicked()
    {
        // El inventario de StartMenu (Canvas (1)/CPrincipal) no existe en el
        // LobbyRoom. Ahí el botón INVENTARIO abre la armería (su propio
        // listener persistente en LobbyArmeriaController). Si se dejaba pasar,
        // MostrarInventario() marcaba el modal y apagaba este Canvas, dejando
        // al jugador sin movimiento y con el ESC muerto.
        if (!HayCanvasInventario())
        {
            Debug.Log("[SteamLobbyListUI] Sin inventario en esta escena: se ignora el clic.");
            return;
        }

        Debug.Log($"[SteamLobbyListUI] Click INVENTARIO (abierto={isInventoryOpen}).");
        if (!isInventoryOpen)
        {
            var host = FindFirstObjectByType<InventarioCanvasHost>(FindObjectsInactive.Include);
            if (host != null) host.Abrir();
            else MostrarInventario();
        }
        else
        {
            CloseInventory();
        }
    }

    /// <summary>True si en la escena activa existe el canvas del inventario.</summary>
    private bool HayCanvasInventario()
    {
        BuscarCanvasInventario();
        return invRaiz != null || invPrincipal != null;
    }

    public void CloseInventory()
    {
        if (!isInventoryOpen) return;
        var host = FindFirstObjectByType<InventarioCanvasHost>(FindObjectsInactive.Include);
        if (host != null) host.Cerrar();
        else OcultarInventario();
    }

    private GameObject invRaiz;
    private GameObject invPrincipal;
    private GameObject invSkills;
    private GameObject invPlayer;
    private GameObject invWeapons;

    private void BuscarCanvasInventario()
    {
        if (invRaiz == null) invRaiz = BuscarObjeto("Canvas (1)");
        if (invPrincipal == null) invPrincipal = BuscarObjeto("CPrincipal");
        if (invSkills == null) invSkills = BuscarObjeto("CSkills");
        if (invPlayer == null) invPlayer = BuscarObjeto("CPlayer");
        if (invWeapons == null) invWeapons = BuscarObjeto("CWeapons");
    }

    private static GameObject BuscarObjeto(string nombre)
    {
        var todos = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            var t = todos[i];
            if (t != null && t.name == nombre) return t.gameObject;
        }
        return GameObject.Find(nombre);
    }

    /// <summary>
    /// Muestra el inventario (ya vive en esta escena): oculta el menú y
    /// enseña CPrincipal. Sin cámaras ni escenas: solo canvas.
    /// Funciona con o sin host.
    /// </summary>
    public void MostrarInventario()
    {
        // Sin canvas de inventario no hay nada que mostrar: no se marca el
        // modal ni se apaga la UI (eso dejaba al jugador inmóvil para siempre).
        if (!HayCanvasInventario())
        {
            Debug.LogWarning("[SteamLobbyListUI] MostrarInventario sin canvas de inventario en la escena: ignorado.");
            return;
        }

        isInventoryOpen = true;
        BloqueoInterfaz.AbrirModal();
        UpdateStatus("Abriendo Inventario...");
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyListPanel != null) lobbyListPanel.SetActive(false);
        gameObject.SetActive(false);

        BuscarCanvasInventario();
        if (invRaiz != null && !invRaiz.activeSelf) invRaiz.SetActive(true);
        if (invSkills != null) invSkills.SetActive(false);
        if (invPlayer != null) invPlayer.SetActive(false);
        if (invWeapons != null) invWeapons.SetActive(false);
        if (invPrincipal != null) invPrincipal.SetActive(true);
        else Debug.LogWarning("[SteamLobbyListUI] CPrincipal no encontrado en escena.");
    }

    /// <summary>Vuelve al menú principal. Funciona con o sin host.</summary>
    public void OcultarInventario()
    {
        if (invRaiz != null) invRaiz.SetActive(false);
        RestaurarMenu();
    }

    /// <summary>Muestra una sección del inventario y apaga las demás.</summary>
    public void MostrarSeccionInventario(GameObject seccion)
    {
        BuscarCanvasInventario();
        if (invRaiz != null && !invRaiz.activeSelf) invRaiz.SetActive(true);
        if (invPrincipal != null) invPrincipal.SetActive(seccion == invPrincipal);
        if (invSkills != null) invSkills.SetActive(seccion == invSkills);
        if (invPlayer != null) invPlayer.SetActive(seccion == invPlayer);
        if (invWeapons != null) invWeapons.SetActive(seccion == invWeapons);
    }

    private void RestaurarMenu()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        isInventoryOpen = false;
        // Al cerrar el inventario se vuelve a la sala 2D, no al menú superpuesto.
        MostrarSala2D();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnLobbiesFoundHandler(List<Lobby> lobbies)
    {
        buscandoSalas = false;
        if (timeoutBusqueda != null) { StopCoroutine(timeoutBusqueda); timeoutBusqueda = null; }
        currentLobbies = lobbies;
        UpdateStatus(lobbies.Count > 0
            ? $"Encontradas {lobbies.Count} salas públicas en Steam"
            : "No hay salas públicas. Crea la tuya.");
        UpdateLobbyList();
    }

    private void OnLobbyJoinedHandler(Lobby lobby)
    {
        Debug.Log("[SteamLobbyListUI] Entrando directamente al ambiente 3D LobbyRoom!");
    }

    private void OnLobbyCreatedHandler(Lobby lobby)
    {
        Debug.Log("[SteamLobbyListUI] Sala creada. Entrando directamente al ambiente 3D LobbyRoom!");
    }

    /// <summary>Mensaje de error que sobrevive al cambio a StartMenu.</summary>
    public static string MensajePendiente;

    private void OnLobbyLeftHandler()
    {
        // Limpieza para volver al menú clicable: cursor libre y sin overlays
        // colgados (p. ej. cuenta atrás a medias por caída del host).
        MatchCountdown.Cancelar();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CargaLocal.Cargar("StartMenu", "Volviendo al menú...");
    }

    private void OnLobbyErrorHandler(string error)
    {
        // Corta la búsqueda en curso: si no, el timeout de 12s pisaría el error
        // real con "Steam no respondió".
        buscandoSalas = false;
        if (timeoutBusqueda != null) { StopCoroutine(timeoutBusqueda); timeoutBusqueda = null; }

        MensajePendiente = error;
        UpdateStatus($"Error: {error}");
    }

    private void UpdateLobbyList()
    {
        if (lobbyListContainer == null) return;

        foreach (Transform child in lobbyListContainer)
        {
            child.gameObject.SetActive(false);
            pooledLobbyItems.Add(child.gameObject);
        }

        foreach (var lobby in currentLobbies)
        {
            GameObject item;
            if (pooledLobbyItems.Count > 0)
            {
                item = pooledLobbyItems[0];
                pooledLobbyItems.RemoveAt(0);
            }
            else
            {
                item = Instantiate(lobbyItemPrefab, lobbyListContainer);
            }

            // Siempre activar: el prefab por defecto se crea inactivo
            // (CreateDefaultItemPrefab termina en SetActive(false)) y la rama de
            // instancia no lo encendía, así que la primera búsqueda mostraba la
            // lista vacía aunque sí hubiera salas.
            item.SetActive(true);

            var itemData = item.GetComponent<LobbyItemData>();
            if (itemData != null)
            {
                string lobbyName = lobby.GetData("LobbyName");
                if (string.IsNullOrEmpty(lobbyName)) lobbyName = $"Sala {lobby.Id}";

                int currentCount = lobby.MemberCount;
                int maxCount = lobby.MaxMembers;

                itemData.Setup(lobbyName, (uint)currentCount, (uint)maxCount, lobby.Id.Value.ToString());
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void OnQuitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private GameObject CreateDefaultItemPrefab()
    {
        GameObject go = new GameObject("RuntimeSteamLobbyItem", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(LobbyItemData));
        
        LayoutElement mainElem = go.GetComponent<LayoutElement>();
        mainElem.preferredHeight = 50;
        mainElem.minHeight = 45;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.20f, 0.30f, 0.85f);

        HorizontalLayoutGroup group = go.GetComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(15, 15, 8, 8);
        group.spacing = 15;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;

        GameObject nameObj = new GameObject("LobbyNameText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(go.transform, false);
        LayoutElement nameElem = nameObj.GetComponent<LayoutElement>();
        nameElem.flexibleWidth = 1f;

        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 17;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Left;

        GameObject countObj = new GameObject("PlayerCountText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        countObj.transform.SetParent(go.transform, false);
        LayoutElement countElem = countObj.GetComponent<LayoutElement>();
        countElem.preferredWidth = 120f;

        TextMeshProUGUI countTmp = countObj.GetComponent<TextMeshProUGUI>();
        countTmp.fontSize = 15;
        countTmp.color = new Color(0.6f, 0.85f, 1f);
        countTmp.alignment = TextAlignmentOptions.Center;

        GameObject btnObj = new GameObject("JoinButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(go.transform, false);
        LayoutElement btnElem = btnObj.GetComponent<LayoutElement>();
        btnElem.preferredWidth = 130f;
        btnElem.preferredHeight = 35f;

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0.12f, 0.58f, 0.95f);

        GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform bTextRect = btnTextObj.GetComponent<RectTransform>();
        bTextRect.anchorMin = Vector2.zero;
        bTextRect.anchorMax = Vector2.one;
        bTextRect.offsetMin = Vector2.zero;
        bTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "UNIRSE";
        btnTmp.fontSize = 15;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        LobbyItemData itemData = go.GetComponent<LobbyItemData>();
        var fields = typeof(LobbyItemData).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var f in fields)
        {
            if (f.Name == "lobbyIdText") f.SetValue(itemData, nameTmp);
            if (f.Name == "playerCountText") f.SetValue(itemData, countTmp);
            if (f.Name == "joinButton") f.SetValue(itemData, btnObj.GetComponent<Button>());
        }

        go.SetActive(false);
        return go;
    }

    private void OnDestroy()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbiesFound -= OnLobbiesFoundHandler;
            SteamLobbyManager.Instance.OnLobbyJoined -= OnLobbyJoinedHandler;
            SteamLobbyManager.Instance.OnLobbyLeft -= OnLobbyLeftHandler;
            SteamLobbyManager.Instance.OnLobbyCreated -= OnLobbyCreatedHandler;
            SteamLobbyManager.Instance.OnLobbyError -= OnLobbyErrorHandler;
        }
    }
}
