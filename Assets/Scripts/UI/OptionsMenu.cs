using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de OPCIÓN del StartMenu: sensibilidad del ratón + volumen maestro.
/// Persistidos en PlayerPrefs y aplicados sin pasar por escena (runtime UI).
/// </summary>
public class OptionsMenu : MonoBehaviour
{
    public static OptionsMenu Instance { get; private set; }

    public const string PREF_SENS = "opcion_sensibilidad";
    public const string PREF_VOL = "opcion_volumen";
    public const float SENS_DEF = 7f;

    private GameObject root;
    private Slider sensSlider;
    private TextMeshProUGUI sensValor;
    private Slider volSlider;
    private TextMeshProUGUI volValor;
    private bool abierto = false;
    public bool Abierto => abierto;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("OptionsMenu");
        go.AddComponent<OptionsMenu>();
    }

    public static void Abrir()
    {
        EnsureExists();
        Instance.AbrirInterno();
    }

    public static float SensibilidadGuardada() => PlayerPrefs.GetFloat(PREF_SENS, SENS_DEF);
    public static float VolumenGuardado() => PlayerPrefs.GetFloat(PREF_VOL, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AplicarVolumenArranque()
    {
        AudioListener.volume = VolumenGuardado();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConstruirUI();
        CerrarInterno();
    }

    private void Update()
    {
        if (!abierto) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Cerrar();
    }

    public void AbrirInterno()
    {
        if (root == null) ConstruirUI();
        root.SetActive(true);
        abierto = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refrescar();
    }

    public void Cerrar()
    {
        if (root != null) root.SetActive(false);
        abierto = false;
    }

    private void CerrarInterno()
    {
        if (root != null) root.SetActive(false);
        abierto = false;
    }

    private void Refrescar()
    {
        if (sensSlider != null) sensSlider.value = SensibilidadGuardada();
        if (volSlider != null) volSlider.value = VolumenGuardado();
        ActualizarTextos();
    }

    private void ActualizarTextos()
    {
        if (sensValor != null) sensValor.text = $"{SensibilidadGuardada():0.0}";
        if (volValor != null) volValor.text = $"{VolumenGuardado() * 100f:0}%";
    }

    private void OnSensCambiada(float v)
    {
        PlayerPrefs.SetFloat(PREF_SENS, v);
        PlayerPrefs.Save();
        // Aplica en vivo a los jugadores ya spawneados
        foreach (var loco in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            if (loco != null) loco.mouseSensitivity = v;
        ActualizarTextos();
    }

    private void OnVolCambiado(float v)
    {
        PlayerPrefs.SetFloat(PREF_VOL, v);
        PlayerPrefs.Save();
        AudioListener.volume = v;
        ActualizarTextos();
    }

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 940;
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
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.7f);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f); panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560, 380); panelRt.anchoredPosition = Vector2.zero;
        panelGo.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 28, 28); vlg.spacing = 18f;
        vlg.childControlHeight = false; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

        CrearTitulo(panelGo.transform, "OPCIÓN");
        sensSlider = CrearFilaSlider(panelGo.transform, "SENSIBILIDAD", 1f, 15f, out sensValor);
        sensSlider.onValueChanged.AddListener(OnSensCambiada);
        volSlider = CrearFilaSlider(panelGo.transform, "VOLUMEN", 0f, 1f, out volValor);
        volSlider.onValueChanged.AddListener(OnVolCambiado);

        var cerrarGo = new GameObject("Volver", typeof(RectTransform), typeof(Image), typeof(Button));
        cerrarGo.transform.SetParent(panelGo.transform, false);
        var cRt = cerrarGo.GetComponent<RectTransform>(); cRt.sizeDelta = new Vector2(0, 48);
        cerrarGo.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
        var cBtn = cerrarGo.GetComponent<Button>();
        var cTxt = CrearTexto(cerrarGo.transform, "VOLVER", 18);
        cBtn.onClick.AddListener(Cerrar);
    }

    private static void CrearTitulo(Transform padre, string texto)
    {
        var go = new GameObject("Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 52);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 30; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.4f, 0.85f, 1f);
        tmp.text = texto;
    }

    private static TextMeshProUGUI CrearTexto(Transform padre, string texto, int tamano)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = tamano; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.text = texto;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Slider CrearFilaSlider(Transform padre, string etiqueta, float min, float max, out TextMeshProUGUI valor)
    {
        var filaGo = new GameObject("Fila_" + etiqueta, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        filaGo.transform.SetParent(padre, false);
        var fRt = filaGo.GetComponent<RectTransform>(); fRt.sizeDelta = new Vector2(0, 44);
        var hlg = filaGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

        var labGo = new GameObject("Etiqueta", typeof(RectTransform), typeof(TextMeshProUGUI));
        labGo.transform.SetParent(filaGo.transform, false);
        var lab = labGo.GetComponent<TextMeshProUGUI>();
        lab.font = TMP_Settings.defaultFontAsset; lab.fontSize = 18; lab.fontStyle = FontStyles.Bold;
        lab.alignment = TextAlignmentOptions.Left; lab.color = Color.white; lab.text = etiqueta;
        var labLe = labGo.AddComponent<LayoutElement>(); labLe.preferredWidth = 220f;

        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(filaGo.transform, false);
        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = false;
        var sliderLe = sliderGo.AddComponent<LayoutElement>(); sliderLe.flexibleWidth = 1f;

        // Fondo + handle mínimos para que se vea sin sprites
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.4f); bgRt.anchorMax = new Vector2(1, 0.6f);
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.32f, 1f);
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(sliderGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0.4f); fillRt.anchorMax = new Vector2(1, 0.6f);
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        fillGo.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(sliderGo.transform, false);
        var handleRt = handleGo.GetComponent<RectTransform>(); handleRt.sizeDelta = new Vector2(20, 28);
        handleGo.GetComponent<Image>().color = Color.white;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleGo.GetComponent<Image>();

        var valGo = new GameObject("Valor", typeof(RectTransform), typeof(TextMeshProUGUI));
        valGo.transform.SetParent(filaGo.transform, false);
        valor = valGo.GetComponent<TextMeshProUGUI>();
        valor.font = TMP_Settings.defaultFontAsset; valor.fontSize = 18; valor.fontStyle = FontStyles.Bold;
        valor.alignment = TextAlignmentOptions.Right; valor.color = new Color(1f, 0.85f, 0.4f);
        valor.text = "";
        var valLe = valGo.AddComponent<LayoutElement>(); valLe.preferredWidth = 80f;

        return slider;
    }
}
