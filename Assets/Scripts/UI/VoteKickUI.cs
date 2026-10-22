using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Panel de votación de expulsión: muestra acusado, SÍ/NO en vivo y timer.
/// F1 = sí, F2 = no (y botones). El acusado lo ve sin botones. Resultado
/// 3s y se cierra. Se auto-crea al arrancar.
/// </summary>
public class VoteKickUI : MonoBehaviour
{
    public static VoteKickUI Instance { get; private set; }

    private GameObject root;
    private TextMeshProUGUI titulo;
    private TextMeshProUGUI conteo;
    private TextMeshProUGUI timerTexto;
    private GameObject filaBotones;
    private bool visible = false;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("VoteKickUI");
        go.AddComponent<VoteKickUI>();
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

    private void Update()
    {
        var mgr = VoteKickManager.Instance;
        bool debeMostrar = mgr != null && (mgr.votacionActiva.Value || mgr.resultado.Value != 0);
        if (debeMostrar && !visible) Mostrar();
        else if (!debeMostrar && visible) Ocultar();

        if (!visible || mgr == null) return;

        // Timer
        if (mgr.votacionActiva.Value)
        {
            float t = mgr.tiempoRestante.Value;
            if (timerTexto != null)
            {
                timerTexto.text = $"TIEMPO: {Mathf.CeilToInt(t)}s";
                timerTexto.color = t < 10f ? new Color(1f, 0.4f, 0.4f) : Color.white;
            }
            if (conteo != null) conteo.text = $"SÍ: {mgr.votosSi.Value}   /   NO: {mgr.votosNo.Value}";
            string nombreAcusado = mgr.acusadoNombre.Value.ToString();
            if (titulo != null && !string.IsNullOrEmpty(nombreAcusado))
                titulo.text = $"¿EXPULSAR A {nombreAcusado}?";
        }
        else if (mgr.resultado.Value == 1)
        {
            if (titulo != null) titulo.text = "JUGADOR EXPULSADO";
            if (conteo != null) conteo.text = "";
            if (timerTexto != null) timerTexto.text = "";
        }
        else if (mgr.resultado.Value == 2)
        {
            if (titulo != null) titulo.text = "VOTACIÓN RECHAZADA";
            if (conteo != null) conteo.text = "";
            if (timerTexto != null) timerTexto.text = "";
        }

        // L4D: todos votan, acusado incluido (se defiende con NO)
        bool puedoVotar = mgr.votacionActiva.Value && NetworkManager.Singleton != null;
        if (filaBotones != null && filaBotones.activeSelf != puedoVotar)
            filaBotones.SetActive(puedoVotar);

        if (puedoVotar)
        {
            if (Input.GetKeyDown(KeyCode.F1)) mgr.Votar(true);
            if (Input.GetKeyDown(KeyCode.F2)) mgr.Votar(false);
        }
    }

    private void Mostrar()
    {
        if (root == null) ConstruirUI();
        root.SetActive(true);
        visible = true;
    }

    private void Ocultar()
    {
        if (root != null) root.SetActive(false);
        visible = false;
    }

    public void VotarSi() => VoteKickManager.Instance?.Votar(true);
    public void VotarNo() => VoteKickManager.Instance?.Votar(false);

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 960;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root = canvasGo;
        root.SetActive(false);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f); panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0, -90);
        panelRt.sizeDelta = new Vector2(560, 250);
        panelGo.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.08f, 0.96f);
        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 16, 16); vlg.spacing = 10f;
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

        titulo = CrearTexto(panelGo.transform, "¿EXPULSAR?", 24, 44);
        titulo.color = new Color(1f, 0.5f, 0.4f);
        conteo = CrearTexto(panelGo.transform, "", 20, 36);
        timerTexto = CrearTexto(panelGo.transform, "", 18, 32);

        filaBotones = new GameObject("Botones", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        filaBotones.transform.SetParent(panelGo.transform, false);
        var fRt = filaBotones.GetComponent<RectTransform>(); fRt.sizeDelta = new Vector2(0, 52);
        var fHlg = filaBotones.GetComponent<HorizontalLayoutGroup>();
        fHlg.spacing = 12f; fHlg.childControlHeight = true; fHlg.childForceExpandHeight = true;
        fHlg.childControlWidth = true; fHlg.childForceExpandWidth = true;

        CrearBoton(filaBotones.transform, "SÍ (F1)", new Color(0.2f, 0.6f, 0.3f, 1f), VotarSi);
        CrearBoton(filaBotones.transform, "NO (F2)", new Color(0.7f, 0.25f, 0.25f, 1f), VotarNo);
    }

    private static TextMeshProUGUI CrearTexto(Transform padre, string texto, int tamano, float alto)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, alto);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = tamano; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.text = texto;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void CrearBoton(Transform padre, string texto, Color color, UnityEngine.Events.UnityAction alClick)
    {
        var go = new GameObject("Boton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(alClick);
        var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var rt = txtGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 20; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.text = texto;
        tmp.raycastTarget = false;
    }
}
