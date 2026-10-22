using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pantalla de carga (sorting 2000, por encima de todo): fondo que bloquea
/// input, mensaje, barra determinada o animada, y tip rotativo. Se auto-crea
/// al arrancar y escucha AvisosCarga. Auto-ocultado de seguridad 1s después
/// de cada cambio de escena por si un evento se pierde.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    private static readonly string[] Tips = new string[]
    {
        "Apunta a la cabeza: daño x2.",
        "Edita tu pool en INVENTARIO antes de entrar al lobby.",
        "Recién aparecido parpadeas: eres inmune 2 segundos.",
        "Mantén TAB para ver la puntuación.",
        "La escopeta reparte su daño por perdigón.",
    };

    private GameObject root;
    private TextMeshProUGUI mensajeTexto;
    private TextMeshProUGUI tipTexto;
    private Slider barra;
    private float progreso = -1f;
    private float pulso = 0f;
    private bool visible = false;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("LoadingScreen");
        go.AddComponent<LoadingScreen>();
    }

    /// <summary>Tope por si un Terminar se pierde: nunca más de 20s visible.</summary>
    private const float MAX_VISIBLE = 20f;
    private float mostradoEn = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        EnsureExists();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConstruirUI();
        root.SetActive(false);
    }

    private void OnEnable()
    {
        AvisosCarga.AlEmpezar -= Mostrar;
        AvisosCarga.AlEmpezar += Mostrar;
        AvisosCarga.AlProgresar -= AlProgresar;
        AvisosCarga.AlProgresar += AlProgresar;
        AvisosCarga.AlTerminar -= Ocultar;
        AvisosCarga.AlTerminar += Ocultar;
    }

    private void OnDisable()
    {
        AvisosCarga.AlEmpezar -= Mostrar;
        AvisosCarga.AlProgresar -= AlProgresar;
        AvisosCarga.AlTerminar -= Ocultar;
    }

    private void Update()
    {
        if (!visible || barra == null) return;
        // Seguridad: ningún Terminar perdido la deja colgada
        if (Time.unscaledTime - mostradoEn > MAX_VISIBLE) { Ocultar(); return; }
        if (progreso < 0f)
        {
            // Indeterminado: vaivén
            pulso += Time.unscaledDeltaTime * 1.5f;
            barra.value = 0.5f + 0.4f * Mathf.Sin(pulso);
        }
        else barra.value = progreso;
    }

    public void Mostrar(string mensaje)
    {
        if (root == null) ConstruirUI();
        mostradoEn = Time.unscaledTime;
        if (mensajeTexto != null) mensajeTexto.text = mensaje;
        if (tipTexto != null) tipTexto.text = "TIP: " + Tips[Random.Range(0, Tips.Length)];
        progreso = -1f;
        root.SetActive(true);
        visible = true;
    }

    public void AlProgresar(float p)
    {
        progreso = Mathf.Clamp01(p);
    }

    public void Ocultar()
    {
        CancelInvoke(nameof(Ocultar));
        if (root != null) root.SetActive(false);
        visible = false;
    }

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root = canvasGo;

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
        bgImg.raycastTarget = true; // bloquea input durante la carga

        var centroGo = new GameObject("Centro", typeof(RectTransform), typeof(VerticalLayoutGroup));
        centroGo.transform.SetParent(canvasGo.transform, false);
        var cRt = centroGo.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(640, 220); cRt.anchoredPosition = Vector2.zero;
        var vlg = centroGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 18f; vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

        mensajeTexto = CrearTexto(centroGo.transform, "CARGANDO...", 34, true, new Color(0.4f, 0.85f, 1f), 64);

        var barraGo = new GameObject("Barra", typeof(RectTransform), typeof(Slider));
        barraGo.transform.SetParent(centroGo.transform, false);
        var bRt = barraGo.GetComponent<RectTransform>(); bRt.sizeDelta = new Vector2(0, 26);
        barra = barraGo.GetComponent<Slider>();
        barra.minValue = 0f; barra.maxValue = 1f; barra.value = 0f;
        var barraLe = barraGo.AddComponent<LayoutElement>(); barraLe.preferredHeight = 26f;

        var fondoGo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondoGo.transform.SetParent(barraGo.transform, false);
        var fRt = fondoGo.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
        fondoGo.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.30f, 1f);
        var rellGo = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
        rellGo.transform.SetParent(barraGo.transform, false);
        var rRt = rellGo.GetComponent<RectTransform>();
        rRt.anchorMin = Vector2.zero; rRt.anchorMax = Vector2.one;
        rRt.offsetMin = Vector2.zero; rRt.offsetMax = Vector2.zero;
        rellGo.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
        barra.fillRect = rRt;
        barra.targetGraphic = rellGo.GetComponent<Image>();

        tipTexto = CrearTexto(centroGo.transform, "", 17, false, new Color(0.75f, 0.78f, 0.82f), 30);
    }

    private static TextMeshProUGUI CrearTexto(Transform padre, string texto, int tamano, bool negrita, Color color, float alto)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, alto);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = tamano;
        if (negrita) tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = color; tmp.text = texto;
        tmp.raycastTarget = false;
        return tmp;
    }
}
