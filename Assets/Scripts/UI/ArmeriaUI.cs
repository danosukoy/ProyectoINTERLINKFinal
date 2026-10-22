using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Genera dinámicamente los botones del menú de armas a partir del
/// WeaponRegistry. Añadir un arma al juego = listarla en el registro;
/// no hay que tocar ninguna escena.
/// Dos modos: loadout (lobby: equipa slots) y pool (InventoryMenu: edita el
/// inventario seleccionado que el lobby podrá ofrecer).
/// </summary>
public class ArmeriaUI : MonoBehaviour
{
    [Header("Modo")]
    [Tooltip("En InventoryMenu edita el pool; en lobby asigna el loadout.")]
    public bool modoPool = false;
    [Header("Referencias")]
    [SerializeField] private WeaponRegistry registro;
    [SerializeField] private WeaponButton botonPrefab;

    [Header("Habilidades (opcional)")]
    [SerializeField] private SkillRegistry registroSkills;
    [SerializeField] private SkillButton botonSkillPrefab;
    [SerializeField] private Transform contenedorSkills;

    [Header("Contenedores por categoría")]
    [SerializeField] private Transform contenedorPrimary;
    [SerializeField] private Transform contenedorSecondary;
    [SerializeField] private Transform contenedorExtra;
    [Tooltip("Contenedor de cuchillos (melee). Lo crea ArmeriaTabs si no está asignado.")]
    [SerializeField] private Transform contenedorCuchillos;

    [Header("Categoría visible (InventoryMenu)")]
    [Tooltip("Si activo, solo se muestra una categoría; los botones de la escena llaman a MostrarCategoria.")]
    public bool pestanasPorCategoria = true;

    private WeaponCategory categoriaVisible = WeaponCategory.Primary;

    public WeaponCategory CategoriaVisible => categoriaVisible;

    /// <summary>Se dispara al cambiar de pestaña (los grids del inventario se re-bindean).</summary>
    public event System.Action OnCategoriaCambiada;

    [Header("Referencias compartidas (las inyecta ArmeriaTabs)")]
    [SerializeField] private TextMeshProUGUI panelNombre;
    [SerializeField] private TextMeshProUGUI panelInfo;
    [SerializeField] private Transform vistaPrevia;

    public Transform ContenedorPrimary => contenedorPrimary;
    public Transform ContenedorSecondary => contenedorSecondary;
    public Transform ContenedorSkills => contenedorSkills;
    public WeaponRegistry Registro => registro;

    public void AsignarContenedorCuchillos(Transform t)
    {
        if (t != null) contenedorCuchillos = t;
    }

    public void AsignarReferenciasCompartidas(TextMeshProUGUI nombre, TextMeshProUGUI info, Transform preview)
    {
        if (nombre != null) panelNombre = nombre;
        if (info != null) panelInfo = info;
        if (preview != null) vistaPrevia = preview;
    }

    private void Start()
    {
        if (registro == null)
        {
            Debug.LogWarning("[ArmeriaUI] Falta el WeaponRegistry.");
            return;
        }

        // Sin edición manual: en InventoryMenu se edita el pool seleccionado.
        if (!modoPool && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "InventoryMenu")
            modoPool = true;

        GenerarBotones();

        // Estado inicial: solo la categoría visible (los botones son los de la escena).
        if (modoPool && pestanasPorCategoria)
            MostrarCategoria(WeaponCategory.Primary);
    }

    /// <summary>
    /// Muestra solo la columna de la categoría pedida. Melee vive en Extra
    /// cuando no hay contenedor de cuchillos asignado.
    /// </summary>
    public void MostrarCategoria(WeaponCategory categoria)
    {
        categoriaVisible = categoria;
        if (contenedorPrimary != null) contenedorPrimary.gameObject.SetActive(categoria == WeaponCategory.Primary);
        if (contenedorSecondary != null) contenedorSecondary.gameObject.SetActive(categoria == WeaponCategory.Secondary);
        if (contenedorExtra != null) contenedorExtra.gameObject.SetActive(categoria == WeaponCategory.Extra);
        if (contenedorCuchillos != null) contenedorCuchillos.gameObject.SetActive(categoria == WeaponCategory.Extra);
        OnCategoriaCambiada?.Invoke();
    }

    public void GenerarBotones()
    {
        LimpiarContenedor(contenedorPrimary);
        LimpiarContenedor(contenedorSecondary);
        LimpiarContenedor(contenedorExtra);
        LimpiarContenedor(contenedorCuchillos);
        LimpiarContenedor(contenedorSkills);

        var pdm = PlayerDataManager.Instance;

        for (int i = 0; i < registro.armas.Count; i++)
        {
            WeaponDefinition arma = registro.armas[i];
            if (arma == null) continue;

            // En lobby solo se ofrece el inventario seleccionado (pool).
            // En modo pool se muestra el inventario total para editarlo.
            if (!modoPool && pdm != null && !pdm.EnPool(arma)) continue;

            // Melee primero: va a la columna de cuchillos
            Transform destino;
            if (arma.IsMelee)
                destino = contenedorCuchillos != null ? contenedorCuchillos : contenedorExtra;
            else destino = arma.category switch
            {
                WeaponCategory.Primary => contenedorPrimary,
                WeaponCategory.Secondary => contenedorSecondary,
                WeaponCategory.Extra => contenedorExtra,
                _ => contenedorPrimary
            };

            if (destino == null || botonPrefab == null) continue;

            WeaponButton boton = Instantiate(botonPrefab, destino);
            boton.gameObject.name = $"Boton_{arma.itemName}";
            boton.weapon = arma;
            boton.modoPool = modoPool;
            boton.InicializarReferencias(panelNombre, panelInfo, vistaPrevia);
        }

        AsignarGruposDeseleccion();
        GenerarBotonesSkills();
    }

    private void AsignarGruposDeseleccion()
    {
        AsignarGrupoAContenedor(contenedorPrimary);
        AsignarGrupoAContenedor(contenedorSecondary);
        AsignarGrupoAContenedor(contenedorExtra);
        AsignarGrupoAContenedor(contenedorCuchillos);
    }

    private static void AsignarGrupoAContenedor(Transform contenedor)
    {
        if (contenedor == null) return;
        WeaponButton[] grupo = contenedor.GetComponentsInChildren<WeaponButton>(true);
        for (int i = 0; i < grupo.Length; i++)
            if (grupo[i] != null) grupo[i].AsignarGrupo(grupo);
    }

    private void GenerarBotonesSkills()
    {
        if (registroSkills == null || botonSkillPrefab == null || contenedorSkills == null)
            return;

        for (int i = 0; i < registroSkills.habilidades.Count; i++)
        {
            SkillInfo habilidad = registroSkills.habilidades[i];
            if (habilidad == null) continue;

            SkillButton boton = Instantiate(botonSkillPrefab, contenedorSkills);
            boton.gameObject.name = $"Boton_{habilidad.skillName}";
            boton.skill = habilidad;
            boton.InicializarReferencias(panelNombre, panelInfo);

            Button uiButton = boton.GetComponent<Button>();
            if (uiButton != null)
                uiButton.onClick.AddListener(boton.AlClickear);
        }
    }

    private void LimpiarContenedor(Transform contenedor)
    {
        if (contenedor == null) return;

        for (int i = contenedor.childCount - 1; i >= 0; i--)
        {
            Destroy(contenedor.GetChild(i).gameObject);
        }
    }
}
