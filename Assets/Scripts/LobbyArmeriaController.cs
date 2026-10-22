using UnityEngine;
using TMPro;

/// <summary>
/// Controla el menú de armería del LobbyRoom: un overlay (canvas) que se
/// abre/cierra sin cambiar de escena, estilo CS/Valorant. Gestiona el
/// cursor, la vista previa 3D del arma y el refresco de los slots de
/// habilidades.
/// </summary>
public class LobbyArmeriaController : MonoBehaviour
{
    private static LobbyArmeriaController _instance;

    /// <summary>
    /// Instancia con recuperación perezosa: si el estático se pierde (p. ej.
    /// recompilación en mitad del Play Mode), vuelve a localizar el objeto.
    /// </summary>
    public static LobbyArmeriaController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<LobbyArmeriaController>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [Header("Referencias de escena")]
    [Tooltip("Raíz del overlay de armería (canvas con los paneles).")]
    [SerializeField] private GameObject panelArmeria;

    [Tooltip("Rig de vista previa 3D; contiene a WGOTransform donde se instancian los modelos.")]
    [SerializeField] private Transform previewRig;

    [Header("Loadout por defecto")]
    [Tooltip("Cuchillo que se asigna a PlayerDataManager si no tiene uno (slot fijo).")]
    [SerializeField] private WeaponDefinition cuchilloPorDefecto;

    [Header("Registros (para persistencia en modo bootstrap)")]
    [SerializeField] private WeaponRegistry weaponRegistry;
    [SerializeField] private SkillRegistry skillRegistry;

    [Header("Slots de habilidades (UI)")]
    [SerializeField] private TextMeshProUGUI[] textosSlots = new TextMeshProUGUI[3];

    public bool Abierto { get; private set; }

    /// <summary>
    /// True solo si el overlay sigue realmente a la vista. El avatar 2D bloquea
    /// el movimiento con esto y no con Abierto: si un cierre ocultó el panel por
    /// otra vía, Abierto se quedaría pegado en true y el jugador no podría moverse
    /// nunca más.
    /// </summary>
    public bool AbiertaAhora => Abierto && panelArmeria != null && panelArmeria.activeInHierarchy;

    /// <summary>Primer hijo del rig = WGOTransform (vista previa del arma).</summary>
    public Transform VistaPreviaArmas =>
        (previewRig != null && previewRig.childCount > 0) ? previewRig.GetChild(0) : null;

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        AsegurarGameManager();

        // Pestañas ARMAS/HABILIDADES: ArmeriaTabs (UI) se auto-instala solo.

        if (panelArmeria != null)
            panelArmeria.SetActive(false);
        if (previewRig != null)
            previewRig.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Auto-reparación: si el panel se ocultó por otra vía (ESC de otro
        // sistema, cambio de escena del overlay), el flag no debe quedarse
        // encendido bloqueando el movimiento del avatar.
        if (Abierto && panelArmeria != null && !panelArmeria.activeSelf)
        {
            Abierto = false;
            DesmontarPreview();
        }

        // Hotkeys independientes del avatar: B/I/TAB alternan, ESC cierra
        if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            Alternar();
        }
        else if (Abierto && Input.GetKeyDown(KeyCode.Escape))
        {
            Cerrar();
        }

        if (!Abierto) return;

        RefrescarSlotsUI();
    }

    // ---------- API pública ----------

    public void Alternar()
    {
        if (Abierto) Cerrar();
        else Abrir();
    }

    public void Abrir()
    {
        if (panelArmeria == null)
        {
            Debug.LogWarning("[LobbyArmeriaController] Falta la referencia a panelArmeria.");
            return;
        }

        AsegurarGameManager();

        Abierto = true;
        panelArmeria.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MontarPreview();
        RefrescarSlotsUI();
    }

    public void Cerrar()
    {
        Abierto = false;

        if (panelArmeria != null)
            panelArmeria.SetActive(false);

        DesmontarPreview();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Interno ----------

    /// <summary>
    /// Garantiza que exista un PlayerDataManager (por si la escena LobbyRoom
    /// se abre directamente en el editor sin pasar por StartMenu) y que tenga
    /// el cuchillo fijo asignado.
    /// </summary>
    private void AsegurarGameManager()
    {
        if (PlayerDataManager.Instance == null)
        {
            var existente = FindFirstObjectByType<PlayerDataManager>(FindObjectsInactive.Include);
            if (existente == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<PlayerDataManager>();
                Debug.Log("[LobbyArmeriaController] GameManager creado en LobbyRoom (modo editor).");
            }
        }

        if (PlayerDataManager.Instance != null
            && PlayerDataManager.Instance.cuchillo == null
            && cuchilloPorDefecto != null)
        {
            PlayerDataManager.Instance.cuchillo = cuchilloPorDefecto;
        }

        // Si el GameManager se creó por bootstrap (sin registros), se asignan
        // y se recarga el loadout persistido para que la selección sobreviva.
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.AsignarRegistros(weaponRegistry, skillRegistry);
        }
    }

    /// <summary>
    /// Cuelga el rig de vista previa de la cámara del jugador local para que
    /// el modelo el arma aparezca flotando a la derecha de la pantalla.
    /// </summary>
    private void MontarPreview()
    {
        if (previewRig == null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[LobbyArmeriaController] No hay cámara principal para la vista previa.");
            return;
        }

        previewRig.SetParent(cam.transform, false);
        previewRig.gameObject.SetActive(true);
    }

    private void DesmontarPreview()
    {
        if (previewRig == null) return;

        // Limpia el modelo instanciado (WGOTransform es el primer hijo)
        Transform wgot = previewRig.childCount > 0 ? previewRig.GetChild(0) : null;
        if (wgot != null)
        {
            for (int i = wgot.childCount - 1; i >= 0; i--)
                Destroy(wgot.GetChild(i).gameObject);
        }

        previewRig.SetParent(null, true);
        previewRig.gameObject.SetActive(false);
    }

    private void RefrescarSlotsUI()
    {
        if (PlayerDataManager.Instance == null || textosSlots == null) return;

        SkillInfo[] equipadas = PlayerDataManager.Instance.skillsEquipadas;

        for (int i = 0; i < textosSlots.Length; i++)
        {
            if (textosSlots[i] == null) continue;

            SkillInfo s = (equipadas != null && i < equipadas.Length) ? equipadas[i] : null;
            textosSlots[i].text = s != null ? $"{i + 1}. {s.skillName}" : $"{i + 1}. —";
        }
    }
}
