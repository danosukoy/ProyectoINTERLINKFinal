using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel BO2-style para votar mapa. Lo abre MapVoteStation (E en pantalla 3D).
/// 30s, 3 opciones (mismo SampleScene x3 si solo hay 1). Todos votan, ganador carga escena.
/// Se construye en runtime sobre ScreenSpaceOverlay.
/// </summary>
public class MapVoteUI : MonoBehaviour
{
    public static MapVoteUI Instance { get; private set; }

    private GameObject root;
    private TextMeshProUGUI titulo;
    private TextMeshProUGUI timerTexto;
    private TextMeshProUGUI estadoTexto;
    private Button[] botones = new Button[3];
    private TextMeshProUGUI[] conteos = new TextMeshProUGUI[3];
    private TextMeshProUGUI[] nombres = new TextMeshProUGUI[3];
    private Image[] fondos = new Image[3];
    private bool abierto = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConstruirUI();
        Cerrar();
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("MapVoteUI");
        go.AddComponent<MapVoteUI>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear() => EnsureExists();

    private void OnEnable()
    {
        MapVoteManager.AlSolicitarAbrirPanel -= Abrir;
        MapVoteManager.AlSolicitarAbrirPanel += Abrir;
        TrySubscribe();
    }

    private void OnDisable()
    {
        MapVoteManager.AlSolicitarAbrirPanel -= Abrir;
        if (managerSuscrita != null)
        {
            managerSuscrita.OnVotosActualizados -= Refrescar;
            managerSuscrita.OnVotacionFinalizada -= OnFinalizada;
            managerSuscrita = null;
        }
    }

    private MapVoteManager managerSuscrita;

    private void TrySubscribe()
    {
        var inst = MapVoteManager.Instance;
        if (inst != null && inst == managerSuscrita) return;
        if (inst != null)
        {
            managerSuscrita = inst;
            inst.OnVotosActualizados += Refrescar;
            inst.OnVotacionFinalizada += OnFinalizada;
            Refrescar();
        }
        else
        {
            Invoke(nameof(TrySubscribe), 1f);
        }
    }

    private void Update()
    {
        // El manager se recrea al volver de cada partida: re-suscribe.
        if (managerSuscrita != MapVoteManager.Instance)
            TrySubscribe();
        if (!abierto) return;

        // Timer 30s siempre visible
        if (timerTexto != null)
        {
            if (MapVoteManager.Instance != null)
            {
                float t = MapVoteManager.Instance.tiempoRestante.Value;
                bool activa = MapVoteManager.Instance.votacionActiva.Value;
                if (activa)
                {
                    timerTexto.text = $"TIEMPO: {Mathf.CeilToInt(t)}s / 30s";
                    timerTexto.color = t < 10f ? new Color(1f, 0.4f, 0.4f) : Color.white;
                }
                else if (MapVoteManager.Instance.mapaGanador.Value >= 0)
                {
                    int ganador = MapVoteManager.Instance.mapaGanador.Value;
                    timerTexto.text = $"GANADOR: {MapVoteManager.Instance.mapas[ganador].displayName}";
                    timerTexto.color = new Color(0.4f, 1f, 0.6f);
                }
                else
                {
                    // Antes de iniciar (primeros 0.5s)
                    timerTexto.text = "TIEMPO: 30s / 30s";
                    timerTexto.color = Color.white;
                }
            }
            else
            {
                timerTexto.text = "TIEMPO: 30s / 30s";
            }
        }

        // ESC cierra y re-lockea
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cerrar();
        }
    }

    public void Abrir()
    {
        if (root == null) ConstruirUI();
        root.SetActive(true);
        if (!abierto) BloqueoInterfaz.AbrirModal();
        abierto = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refrescar();
    }

    public void Cerrar()
    {
        if (root != null) root.SetActive(false);
        bool estabaAbierto = abierto;
        abierto = false;
        // Solo toca el cursor si el panel llegó a abrirse (el Awake también
        // llama a Cerrar y en el menú el cursor debe quedar libre).
        if (!estabaAbierto) return;
        BloqueoInterfaz.CerrarModal();
        // Re-lock solo si no hay armería abierta
        if (LobbyArmeriaController.Instance == null || !LobbyArmeriaController.Instance.Abierto)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        MapVotePromptUI.Ocultar();
    }

    private void Refrescar()
    {
        if (MapVoteManager.Instance == null) return;
        var votos = MapVoteManager.Instance.GetVotosArray();
        int total = MapVoteManager.Instance.GetTotalVotos();
        bool activa = MapVoteManager.Instance.votacionActiva.Value;

        // Obtener mi voto actual
        ulong myId = 0;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            myId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        else if (Steamworks.SteamClient.IsValid)
            myId = Steamworks.SteamClient.SteamId;

        int miVoto = -1;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            miVoto = MapVoteManager.Instance.MiVoto(myId);

        for (int i = 0; i < 3; i++)
        {
            if (MapVoteManager.Instance != null && i >= MapVoteManager.Instance.mapas.Length) continue;
            // Actualiza nombre por si manager recién apareció
            if (nombres[i] != null && MapVoteManager.Instance != null && i < MapVoteManager.Instance.mapas.Length)
                nombres[i].text = MapVoteManager.Instance.mapas[i].displayName;
            int v = i < votos.Length ? votos[i] : 0;
            float pct = total > 0 ? (v * 100f / total) : 0f;
            if (conteos[i] != null) conteos[i].text = $"{v} votos ({pct:0}%)";
            if (fondos[i] != null)
            {
                bool seleccionado = miVoto == i;
                bool esGanador = !activa && MapVoteManager.Instance != null && MapVoteManager.Instance.mapaGanador.Value == i;
                if (esGanador)
                    fondos[i].color = new Color(0.2f, 0.8f, 0.4f, 0.95f);
                else if (seleccionado)
                    fondos[i].color = new Color(0.25f, 0.55f, 0.95f, 0.95f);
                else
                    fondos[i].color = new Color(0.16f, 0.22f, 0.30f, 0.9f);
            }
            // Botones siempre interactuables mientras votación activa o iniciando; deshabilita solo al finalizar con ganador
            if (botones[i] != null)
            {
                bool deshabilitar = !activa && MapVoteManager.Instance != null && MapVoteManager.Instance.mapaGanador.Value >= 0;
                botones[i].interactable = !deshabilitar;
            }
        }

        if (estadoTexto != null)
        {
            if (!activa && MapVoteManager.Instance.mapaGanador.Value >= 0)
                estadoTexto.text = "¡Mapa ganador seleccionado! Cargando...";
            else if (miVoto >= 0)
                estadoTexto.text = $"Votaste por: {MapVoteManager.Instance.mapas[miVoto].displayName} (puedes cambiar)";
            else
                estadoTexto.text = "Elige un mapa para votar";
        }
    }

    private void OnFinalizada(int ganador)
    {
        Refrescar();
        // Auto-cierra en 4s
        Invoke(nameof(Cerrar), 4f);
    }

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("MapVoteUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        root = canvasGo;
        root.SetActive(false);

        // Fondo oscuro
        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0,0,0,0.65f);
        bgImg.raycastTarget = true;
        bgImg.raycastTarget = true;

        // Panel central 700x520
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f); panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720, 560); panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24,24,24,24); vlg.spacing = 12f; vlg.childControlHeight = false; vlg.childControlWidth = true; vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

        // Título
        var tituloGo = new GameObject("Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        tituloGo.transform.SetParent(panelGo.transform, false);
        var tRt = tituloGo.GetComponent<RectTransform>(); tRt.sizeDelta = new Vector2(0, 48);
        titulo = tituloGo.GetComponent<TextMeshProUGUI>();
        titulo.font = TMP_Settings.defaultFontAsset; titulo.fontSize = 28; titulo.fontStyle = FontStyles.Bold;
        titulo.alignment = TextAlignmentOptions.Center; titulo.color = new Color(0.4f, 0.85f, 1f);
        titulo.text = "VOTACIÓN DE MAPA";

        // Timer
        var timerGo = new GameObject("Timer", typeof(RectTransform), typeof(TextMeshProUGUI));
        timerGo.transform.SetParent(panelGo.transform, false);
        var tmRt = timerGo.GetComponent<RectTransform>(); tmRt.sizeDelta = new Vector2(0, 36);
        timerTexto = timerGo.GetComponent<TextMeshProUGUI>();
        timerTexto.font = TMP_Settings.defaultFontAsset; timerTexto.fontSize = 22; timerTexto.fontStyle = FontStyles.Bold;
        timerTexto.alignment = TextAlignmentOptions.Center; timerTexto.color = Color.white;
        timerTexto.text = "TIEMPO: 30s";

        // Botones mapas
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var btnGo = new GameObject($"Mapa{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
            btnGo.transform.SetParent(panelGo.transform, false);
            var brt = btnGo.GetComponent<RectTransform>(); brt.sizeDelta = new Vector2(0, 84);
            var bImg = btnGo.GetComponent<Image>(); bImg.color = new Color(0.16f, 0.22f, 0.30f, 0.9f);
            fondos[i] = bImg;
            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors; colors.highlightedColor = new Color(0.3f, 0.6f, 1f); colors.pressedColor = new Color(0.2f, 0.5f, 0.9f); btn.colors = colors;
            botones[i] = btn;
            var hlg = btnGo.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16,16,12,12); hlg.spacing = 16f; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

            var nameGo = new GameObject("Nombre", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(btnGo.transform, false);
            var nTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nTmp.font = TMP_Settings.defaultFontAsset; nTmp.fontSize = 20; nTmp.fontStyle = FontStyles.Bold;
            nTmp.alignment = TextAlignmentOptions.Left; nTmp.color = Color.white;
            string nombre = (MapVoteManager.Instance != null && idx < MapVoteManager.Instance.mapas.Length) ? MapVoteManager.Instance.mapas[idx].displayName : $"Mapa {idx+1} - SampleScene";
            nTmp.text = nombre;
            nombres[i] = nTmp;
            var le = nameGo.AddComponent<LayoutElement>(); le.flexibleWidth = 1f;

            var countGo = new GameObject("Conteo", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGo.transform.SetParent(btnGo.transform, false);
            var cTmp = countGo.GetComponent<TextMeshProUGUI>();
            cTmp.font = TMP_Settings.defaultFontAsset; cTmp.fontSize = 16; cTmp.alignment = TextAlignmentOptions.Right; cTmp.color = new Color(0.9f, 0.9f, 0.9f);
            cTmp.text = "0 votos";
            conteos[i] = cTmp;
            var ce = countGo.AddComponent<LayoutElement>(); ce.preferredWidth = 140f;

            btn.onClick.AddListener(() => OnClickMapa(idx));
        }

        // Estado
        var estadoGo = new GameObject("Estado", typeof(RectTransform), typeof(TextMeshProUGUI));
        estadoGo.transform.SetParent(panelGo.transform, false);
        var eRt = estadoGo.GetComponent<RectTransform>(); eRt.sizeDelta = new Vector2(0, 32);
        estadoTexto = estadoGo.GetComponent<TextMeshProUGUI>();
        estadoTexto.font = TMP_Settings.defaultFontAsset; estadoTexto.fontSize = 16;
        estadoTexto.alignment = TextAlignmentOptions.Center; estadoTexto.color = new Color(0.85f, 0.85f, 0.85f);
        estadoTexto.text = "Elige un mapa para votar";

        // Botón cerrar
        var cerrarGo = new GameObject("Cerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        cerrarGo.transform.SetParent(panelGo.transform, false);
        var cRt = cerrarGo.GetComponent<RectTransform>(); cRt.sizeDelta = new Vector2(0, 44);
        var cImg = cerrarGo.GetComponent<Image>(); cImg.color = new Color(0.25f, 0.25f, 0.28f, 0.9f);
        var cBtn = cerrarGo.GetComponent<Button>();
        var cTxtGo = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTxtGo.transform.SetParent(cerrarGo.transform, false);
        var ctRt = cTxtGo.GetComponent<RectTransform>(); ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one; ctRt.offsetMin = Vector2.zero; ctRt.offsetMax = Vector2.zero;
        var cTmp2 = cTxtGo.GetComponent<TextMeshProUGUI>();
        cTmp2.font = TMP_Settings.defaultFontAsset; cTmp2.fontSize = 18; cTmp2.fontStyle = FontStyles.Bold;
        cTmp2.alignment = TextAlignmentOptions.Center; cTmp2.color = Color.white; cTmp2.text = "CERRAR (ESC)";
        cBtn.onClick.AddListener(Cerrar);

        // Click en fondo cierra
        var bgBtn = bg.GetComponent<Image>();
        bgBtn.raycastTarget = true;
        var bgButton = bg.AddComponent<Button>();
        bgButton.transition = Selectable.Transition.None;
        bgButton.onClick.AddListener(Cerrar);
    }

    private void OnClickMapa(int idx)
    {
        MapVoteManager.Instance?.Votar(idx);
        // Al votar se sale del panel (los conteos siguen visibles en la pantalla 3D)
        Cerrar();
    }
}
