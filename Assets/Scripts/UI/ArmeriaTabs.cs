using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pestañas de la armería del LobbyRoom: ARMAS (primaria + secundaria + cuchillo)
/// y HABILIDADES. Se auto-instala en runtime: clona la columna de primarias
/// para crear la columna de cuchillos, construye la barra de pestañas e
/// inyecta las referencias compartidas en ArmeriaUI (sin GameObject.Find).
/// Todo se deriva de las referencias ya asignadas en escena.
/// </summary>
public class ArmeriaTabs : MonoBehaviour
{
    private GameObject colPrimarias;
    private GameObject colSecundarias;
    private GameObject colHabilidades;
    private GameObject colCuchillos;

    private Button tabArmasBtn;
    private Button tabSkillsBtn;
    private Image tabArmasImg;
    private Image tabSkillsImg;

    private static readonly Color ColorActivo = new Color(0.25f, 0.55f, 0.95f, 1f);
    private static readonly Color ColorInactivo = new Color(0.16f, 0.22f, 0.30f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        AlCargarEscena(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private static void AlCargarEscena(UnityEngine.SceneManagement.Scene escena, UnityEngine.SceneManagement.LoadSceneMode modo)
    {
        if (escena.name != "LobbyRoom") return;
        if (FindFirstObjectByType<ArmeriaTabs>(FindObjectsInactive.Include) != null) return;
        var armeria = FindFirstObjectByType<LobbyArmeriaController>(FindObjectsInactive.Include);
        if (armeria == null) return;
        armeria.gameObject.AddComponent<ArmeriaTabs>();
    }

    private void Start()
    {
        var armeria = FindFirstObjectByType<ArmeriaUI>(FindObjectsInactive.Include);
        if (armeria == null || armeria.ContenedorPrimary == null || armeria.ContenedorSkills == null)
        {
            Debug.LogWarning("[ArmeriaTabs] ArmeriaUI sin contenedores asignados.");
            return;
        }

        colPrimarias = armeria.ContenedorPrimary.parent.gameObject;
        colSecundarias = armeria.ContenedorSecondary != null
            ? armeria.ContenedorSecondary.parent.gameObject : null;
        colHabilidades = armeria.ContenedorSkills.parent.gameObject;

        CrearColumnaCuchillos(armeria);
        InyectarReferenciasCompartidas(armeria);
        CrearTabBar();
        MostrarArmas();
    }

    // ---------- Columna de cuchillos ----------

    private void CrearColumnaCuchillos(ArmeriaUI armeria)
    {
        Transform contenedor = colPrimarias.transform.parent.Find("ColCuchillos/Cuchillos");
        if (contenedor == null)
        {
            // Clona ColPrimarias para heredar estilo/layout
            colCuchillos = Instantiate(colPrimarias, colPrimarias.transform.parent);
            colCuchillos.name = "ColCuchillos";

            // Título -> CUCHILLO
            var titulo = colCuchillos.transform.Find("Texto")?.GetComponent<TextMeshProUGUI>();
            if (titulo != null) titulo.text = "CUCHILLO";

            // Contenedor Primarys -> Cuchillos, vaciando los botones clonados
            var clon = colCuchillos.transform.Find("Primarys");
            if (clon != null)
            {
                clon.name = "Cuchillos";
                for (int i = clon.childCount - 1; i >= 0; i--)
                    Destroy(clon.GetChild(i).gameObject);
                contenedor = clon;
            }
        }
        else
        {
            colCuchillos = contenedor.parent.gameObject;
        }

        if (contenedor == null)
        {
            Debug.LogWarning("[ArmeriaTabs] ColCuchillos sin contenedor 'Cuchillos'.");
            return;
        }

        // Entrega el contenedor a ArmeriaUI y regenera (por si ya generó sin él)
        armeria.AsignarContenedorCuchillos(contenedor);
        armeria.GenerarBotones();
    }

    private void InyectarReferenciasCompartidas(ArmeriaUI armeria)
    {
        // PanelInfo vive bajo PanelArmeria (búsqueda acotada al panel, no global)
        Transform panel = colPrimarias.transform.parent.parent;
        TextMeshProUGUI nombre = panel.Find("PanelInfo/TextName")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI info = panel.Find("PanelInfo/TextInfo")?.GetComponent<TextMeshProUGUI>();

        // Vista previa 3D: primer hijo del rig (referencia asignada en escena)
        Transform preview = null;
        var lobbyArmeria = FindFirstObjectByType<LobbyArmeriaController>(FindObjectsInactive.Include);
        if (lobbyArmeria != null) preview = lobbyArmeria.VistaPreviaArmas;

        armeria.AsignarReferenciasCompartidas(nombre, info, preview);
        armeria.GenerarBotones();
    }

    // ---------- Barra de pestañas ----------

    private void CrearTabBar()
    {
        Transform panel = colPrimarias.transform.parent.parent;
        if (panel.Find("TabBar") != null) return;

        var barGo = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        barGo.transform.SetParent(panel, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 1f);
        barRt.anchorMax = new Vector2(0.5f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -64f);
        barRt.sizeDelta = new Vector2(440f, 46f);

        var hlg = barGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;

        tabArmasBtn = CrearBotonPestana(barGo.transform, "ARMAS", MostrarArmas);
        tabSkillsBtn = CrearBotonPestana(barGo.transform, "HABILIDADES", MostrarHabilidades);
        tabArmasImg = tabArmasBtn.GetComponent<Image>();
        tabSkillsImg = tabSkillsBtn.GetComponent<Image>();
    }

    private Button CrearBotonPestana(Transform padre, string texto, UnityEngine.Events.UnityAction alClick)
    {
        var go = new GameObject($"Tab_{texto}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.color = ColorInactivo;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(alClick);

        var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var rt = txtGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 18; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.text = texto;
        tmp.raycastTarget = false;
        return btn;
    }

    // ---------- Cambio de pestaña ----------

    public void MostrarArmas()
    {
        if (colPrimarias != null) colPrimarias.SetActive(true);
        if (colSecundarias != null) colSecundarias.SetActive(true);
        if (colCuchillos != null) colCuchillos.SetActive(true);
        if (colHabilidades != null) colHabilidades.SetActive(false);
        PintarPestanas(true);
    }

    public void MostrarHabilidades()
    {
        if (colPrimarias != null) colPrimarias.SetActive(false);
        if (colSecundarias != null) colSecundarias.SetActive(false);
        if (colCuchillos != null) colCuchillos.SetActive(false);
        if (colHabilidades != null) colHabilidades.SetActive(true);
        PintarPestanas(false);
    }

    private void PintarPestanas(bool armas)
    {
        if (tabArmasImg != null) tabArmasImg.color = armas ? ColorActivo : ColorInactivo;
        if (tabSkillsImg != null) tabSkillsImg.color = armas ? ColorInactivo : ColorActivo;
    }
}
