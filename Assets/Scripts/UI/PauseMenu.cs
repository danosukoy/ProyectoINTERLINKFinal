using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Pausa con jugador instanciado (ESC): panel con OPCIONES + SALIR (cae a la
/// lista de salas). Vale en lobby y en partida. Online no congela el mundo
/// (imposible); offline sí (timeScale 0). Mientras está abierto bloquea
/// movimiento/disparo vía BloqueoInterfaz. Otros modales consumen el ESC antes.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    private GameObject root;
    private bool abierto = false;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("PauseMenu");
        go.AddComponent<PauseMenu>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear() => EnsureExists();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConstruirUI();
        root.SetActive(false);
    }

    private static bool HayJugadorLocal()
    {
        foreach (var pc in PlayerController.Todos)
            if (pc != null && pc.IsLocalPlayer) return true;
        return false;
    }

    private static bool OtroModalManda()
    {
        if (OptionsMenu.Instance != null && OptionsMenu.Instance.Abierto) return true;
        if (BloqueoInterfaz.HayModalAbierta && !InstanceAbierto()) return true;
        if (LobbyArmeriaController.Instance != null && LobbyArmeriaController.Instance.Abierto) return true;
        return false;
    }

    private static bool InstanceAbierto() => Instance != null && Instance.abierto;

    /// <summary>True mientras el panel de pausa está a la vista.</summary>
    public bool Abierto => abierto;

    private void Update()
    {
        // Red de seguridad: sólo la pausa congela el tiempo, así que un
        // timeScale a 0 con la pausa cerrada es un cierre que se saltó
        // Cerrar() (o un cambio de escena a mitad de pausa). Sin esto el
        // jugador se queda sin poder moverse para siempre, porque FixedUpdate
        // deja de ejecutarse.
        if (!abierto && Time.timeScale == 0f) Time.timeScale = 1f;

        // Sin jugador local no hay pausa (y si lo había, se cierra)
        if (!HayJugadorLocal())
        {
            if (abierto) Cerrar();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        // Opciones y otros modales consumen el ESC antes
        if (OtroModalManda()) return;

        if (abierto) Cerrar();
        else Abrir();
    }

    private TextMeshProUGUI textoSalir;

    public void Abrir()
    {
        if (root == null) ConstruirUI();
        root.SetActive(true);
        if (!abierto) BloqueoInterfaz.AbrirModal();
        abierto = true;
        // Etiqueta según contexto: en lobby se sale del lobby, en partida de la partida
        bool enLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyRoom";
        if (textoSalir != null) textoSalir.text = enLobby ? "SALIR DEL LOBBY" : "SALIR DE LA PARTIDA";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EsOffline()) Time.timeScale = 0f;
    }

    public void Cerrar()
    {
        if (root != null) root.SetActive(false);
        bool estaba = abierto;
        abierto = false;
        if (!estaba) return;
        BloqueoInterfaz.CerrarModal();
        Time.timeScale = 1f;
        if (!BloqueoInterfaz.HayModalAbierta)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OnOpciones()
    {
        OptionsMenu.Abrir();
    }

    public void OnSalirDePartida()
    {
        Cerrar();
        // La lista de salas al volver (no el menú principal seco). SteamLobbyListUI
        // lo consume en Start() para mostrar directo la lista.
        SteamLobbyManager.VolverALaLista = true;
        MatchCountdown.Cancelar();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var lobby = SteamLobbyManager.Instance;
        bool enRed = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (enRed && lobby != null)
        {
            lobby.LeaveLobby();
            // LeaveLobby dispara OnLobbyLeft, pero en partida/lobby no hay
            // SteamLobbyListUI vivo que lo escuche (solo existe en StartMenu),
            // así que cargamos StartMenu aquí directamente.
        }
        CargaLocal.Cargar("StartMenu", "Volviendo al menú...");
    }

    private static bool EsOffline()
    {
        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    }

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root = canvasGo;
        root.SetActive(false);

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f); panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(480, 330); panelRt.anchoredPosition = Vector2.zero;
        panelGo.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 28, 28); vlg.spacing = 16f;
        vlg.childControlHeight = false; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

        CrearTitulo(panelGo.transform);
        CrearBoton(panelGo.transform, "OPCIONES", OnOpciones);
        textoSalir = CrearBoton(panelGo.transform, "SALIR DE LA PARTIDA", OnSalirDePartida);
        CrearHint(panelGo.transform);
    }

    private static void CrearTitulo(Transform padre)
    {
        var go = new GameObject("Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 56);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 32; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.4f, 0.85f, 1f);
        tmp.text = "PAUSA";
        tmp.raycastTarget = false;
    }

    private static void CrearHint(Transform padre)
    {
        var go = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 26);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(1f, 1f, 1f, 0.6f);
        tmp.text = "ESC para continuar";
        tmp.raycastTarget = false;
    }

    private static TextMeshProUGUI CrearBoton(Transform padre, string texto, UnityEngine.Events.UnityAction alClick)
    {
        var go = new GameObject("Boton_" + texto, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 56);
        go.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(alClick);
        var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 20; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.text = texto;
        tmp.raycastTarget = false;
        return tmp;
    }
}
