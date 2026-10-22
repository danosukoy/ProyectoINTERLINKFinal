using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerDataManager : MonoBehaviour
{
    private static PlayerDataManager _instance;

    /// <summary>
    /// Instancia con recuperación perezosa: si el estático se pierde (p. ej.
    /// recompilación en mitad del Play Mode), vuelve a localizar el objeto.
    /// </summary>
    public static PlayerDataManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<PlayerDataManager>(FindObjectsInactive.Include);
            return _instance;
        }
        private set => _instance = value;
    }

    public WeaponDefinition wPrimary;
    public WeaponDefinition wSecondary;
    public WeaponDefinition wExtra;

    [Header("Loadout fijo")]
    // Cuchillo: slot fijo del que nunca se desequipa (estilo CS/Valorant).
    public WeaponDefinition cuchillo;

    [Header("Inventario seleccionado (pool)")]
    [Tooltip("Armas editadas en InventoryMenu. El lobby solo ofrece estas. Vacío = todas.")]
    public List<WeaponDefinition> poolArmas = new List<WeaponDefinition>();

    [Header("Habilidades equipadas (3 slots)")]
    public SkillInfo[] skillsEquipadas = new SkillInfo[3];

    public SkillInfo[] skills;

    public int skillPoints = 5;

    [Header("Clase (habilidades fijas al entrar a la partida)")]
    [Tooltip("Kit completo: pasiva + seleccion + especial. Se sincroniza en el spawn.")]
    public ClaseDefinition claseElegida;

    [Header("Registros (para persistencia del loadout)")]
    [SerializeField] private WeaponRegistry weaponRegistry;
    [SerializeField] private SkillRegistry skillRegistry;

    // Claves de PlayerPrefs
    private const string PREF_WPRIMARY  = "loadout_wPrimary";
    private const string PREF_WSECONDARY = "loadout_wSecondary";
    private const string PREF_WEXTRA    = "loadout_wExtra";
    private const string PREF_CUCHILLO  = "loadout_cuchillo";
    private const string PREF_POOL      = "loadout_pool";
    private const string PREF_SKILL     = "loadout_skill_";

    void Awake()
    {
        // Ojo: se usa el campo _instance directamente (no la propiedad, que
        // haría una búsqueda perezosa y el objeto se encontraría a sí mismo).
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // carga al iniciar el juego
        CargarLoadout();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }


    void UpdateSkills(){
      for(int i = 0; i < skills.Length; i++){
      {
        if(PlayerPrefs.GetInt($"hab_{skills[i].skillName}") == 1){
          // skillList.Add(skills[i]);
        }
      }
    }

   }

    // ---------- Habilidades (slots del menú de armería) ----------

    /// <summary>
    /// Alterna una habilidad: si ya está equipada la quita; si no, la equipa
    /// en el primer slot libre (o pisa el slot 0 si los tres están llenos).
    /// Devuelve true si la habilidad quedó equipada.
    /// </summary>
    public bool AlternarSkill(SkillInfo s){
        if (s == null) return false;

        for (int i = 0; i < skillsEquipadas.Length; i++){
            if (skillsEquipadas[i] == s){
                skillsEquipadas[i] = null;
                GuardarLoadout();
                return false;
            }
        }

        int hueco = -1;
        for (int i = 0; i < skillsEquipadas.Length; i++){
            if (skillsEquipadas[i] == null){ hueco = i; break; }
        }

        skillsEquipadas[hueco >= 0 ? hueco : 0] = s;
        GuardarLoadout();
        return true;
    }

    public bool TieneSkill(SkillInfo s){
        return SlotDeSkill(s) >= 0;
    }

    public int SlotDeSkill(SkillInfo s){
        if (s == null) return -1;
        for (int i = 0; i < skillsEquipadas.Length; i++){
            if (skillsEquipadas[i] == s) return i;
        }
        return -1;
    }

    // ---------- Inventario seleccionado (pool) ----------
    // Esquema: inventario total (registro) -> seleccionado (pool, editado en
    // InventoryMenu) -> partida (el lobby solo ofrece el pool).

    /// <summary>True si el arma está disponible para el lobby. Pool vacío = todas.</summary>
    public bool EnPool(WeaponDefinition def){
        if (def == null) return false;
        if (poolArmas == null || poolArmas.Count == 0) return true;
        return poolArmas.Contains(def);
    }

    /// <summary>Se dispara al cambiar el pool (los grids del inventario se re-bindean).</summary>
    public static event System.Action OnPoolCambiado;

    /// <summary>Tope del pool por categoría (Primary/Secondary/Extra, melee en Extra).</summary>
    public const int MaxPoolPorCategoria = 5;

    /// <summary>
    /// Alterna un arma en el pool. Al sacarla, la desequipa del loadout si
    /// estaba equipada. Devuelve true si quedó dentro del pool.
    /// </summary>
    public bool AlternarPool(WeaponDefinition def){
        if (def == null) return false;
        if (poolArmas == null) poolArmas = new List<WeaponDefinition>();

        if (poolArmas.Contains(def)){
            poolArmas.Remove(def);
            if (wPrimary == def)   wPrimary = null;
            if (wSecondary == def) wSecondary = null;
            if (wExtra == def)     wExtra = null;
            if (cuchillo == def)   cuchillo = null;
            GuardarLoadout();
            OnPoolCambiado?.Invoke();
            return false;
        }

        // Tope central por categoría: vale para todas las UI (cards y botones viejos).
        WeaponCategory catNueva = def.IsMelee ? WeaponCategory.Extra : def.category;
        if (ContarEnPool(catNueva) >= MaxPoolPorCategoria)
        {
            Debug.Log($"[PlayerDataManager] Pool de {catNueva} lleno ({MaxPoolPorCategoria}). {def.itemName} rechazada.");
            return false;
        }

        poolArmas.Add(def);
        GuardarLoadout();
        OnPoolCambiado?.Invoke();
        return true;
    }

    /// <summary>Cuántas armas del pool son de esa categoría (melee cuenta en Extra).</summary>
    public int ContarEnPool(WeaponCategory cat)
    {
        if (poolArmas == null) return 0;
        int n = 0;
        for (int i = 0; i < poolArmas.Count; i++)
        {
            WeaponDefinition d = poolArmas[i];
            if (d == null) continue;
            if ((d.IsMelee ? WeaponCategory.Extra : d.category) == cat) n++;
        }
        return n;
    }

    // ---------- Persistencia del loadout (PlayerPrefs) ----------

    /// <summary>
    /// Guarda el loadout actual (armas, cuchillo, pool y habilidades) en PlayerPrefs.
    /// Se llama al cambiar la selección.
    /// </summary>
    public void GuardarLoadout(){
        PlayerPrefs.SetString(PREF_WPRIMARY,   wPrimary   != null ? wPrimary.itemName   : "");
        PlayerPrefs.SetString(PREF_WSECONDARY, wSecondary != null ? wSecondary.itemName : "");
        PlayerPrefs.SetString(PREF_WEXTRA,     wExtra     != null ? wExtra.itemName     : "");
        PlayerPrefs.SetString(PREF_CUCHILLO,   cuchillo   != null ? cuchillo.itemName   : "");
        PlayerPrefs.SetString(PREF_POOL, SerializarPool());

        for (int i = 0; i < skillsEquipadas.Length; i++){
            PlayerPrefs.SetString(PREF_SKILL + i, skillsEquipadas[i] != null ? skillsEquipadas[i].skillName : "");
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Asigna los registros si aún no están (útil cuando el GameManager se
    /// crea por bootstrap en una escena que no es StartMenu). Si acaba de
    /// recibirlos, recarga el loadout persistido.
    /// </summary>
    public void AsignarRegistros(WeaponRegistry wr, SkillRegistry sr){
        bool faltaba = weaponRegistry == null;
        if (weaponRegistry == null) weaponRegistry = wr;
        if (skillRegistry == null) skillRegistry = sr;
        if (faltaba && weaponRegistry != null) CargarLoadout();
    }

    /// <summary>
    /// Restaura el loadout guardado resolviendo los nombres contra los
    /// registros. Si un registro no está asignado o el nombre no existe,
    /// el slot queda null. Se llama en Awake.
    /// </summary>
    public void CargarLoadout(){
        wPrimary   = BuscarArma(PlayerPrefs.GetString(PREF_WPRIMARY, ""));
        wSecondary = BuscarArma(PlayerPrefs.GetString(PREF_WSECONDARY, ""));
        wExtra     = BuscarArma(PlayerPrefs.GetString(PREF_WEXTRA, ""));
        cuchillo   = BuscarArma(PlayerPrefs.GetString(PREF_CUCHILLO, ""));
        DeserializarPool(PlayerPrefs.GetString(PREF_POOL, ""));

        for (int i = 0; i < skillsEquipadas.Length; i++){
            skillsEquipadas[i] = BuscarSkill(PlayerPrefs.GetString(PREF_SKILL + i, ""));
        }

        AplicarLoadoutPorDefecto();
    }

    /// <summary>
    /// Garantiza que el loadout sea jugable: si un slot está vacío o guarda una
    /// arma INCOMPLETA (sin modelo, daño o munición, p. ej. Z576 en desarrollo),
    /// lo rellena con la primera arma COMPLETA del registro de esa categoría.
    /// Si el jugador guardó un arma completada, se respeta. No se guarda en
    /// PlayerPrefs: en cuanto el jugador configure su armería, esto desaparece.
    /// </summary>
    private void AplicarLoadoutPorDefecto(){
        if (weaponRegistry == null || weaponRegistry.armas == null) return;

        NormalizarPool();

        // El loadout debe vivir dentro del pool (si el pool tiene contenido)
        if (poolArmas != null && poolArmas.Count > 0){
            if (!poolArmas.Contains(wPrimary))   wPrimary = null;
            if (!poolArmas.Contains(wSecondary)) wSecondary = null;
            if (!poolArmas.Contains(wExtra))     wExtra = null;
            if (!poolArmas.Contains(cuchillo))   cuchillo = null;
        }

        // Los slots con armas incompletas se tratan como vacíos
        if (!EsArmaUsable(wPrimary))   wPrimary   = null;
        if (!EsArmaUsable(wSecondary)) wSecondary = null;
        if (!EsArmaUsable(wExtra))     wExtra     = null;
        if (!EsCuchilloUsable(cuchillo)) cuchillo = null;

        if (wPrimary != null && wSecondary != null && wExtra != null && cuchillo != null) return;

        for (int i = 0; i < weaponRegistry.armas.Count; i++){
            WeaponDefinition def = weaponRegistry.armas[i];
            if (poolArmas != null && poolArmas.Count > 0 && !poolArmas.Contains(def)) continue;
            if (def != null && def.IsMelee && EsCuchilloUsable(def))
            {
                if (cuchillo == null) cuchillo = def;
                continue;
            }
            if (!EsArmaUsable(def)) continue;

            switch (def.category){
                case WeaponCategory.Primary:   if (wPrimary == null)   wPrimary = def;   break;
                case WeaponCategory.Secondary: if (wSecondary == null) wSecondary = def; break;
                case WeaponCategory.Extra:     if (wExtra == null)     wExtra = def;     break;
            }
        }

        if (wPrimary != null || wSecondary != null || wExtra != null){
            Debug.Log($"[PlayerDataManager] Loadout por defecto asignado: {wPrimary?.itemName ?? "—"} / {wSecondary?.itemName ?? "—"} / {wExtra?.itemName ?? "—"}");
        }
    }

    /// <summary>Un arma es usable si tiene modelo, daño y munición (cargador).</summary>
    private static bool EsArmaUsable(WeaponDefinition def){
        return def != null && def.model != null && def.damage > 0f && def.magSize > 0;
    }

    /// <summary>Un cuchillo es usable si es melee con modelo y daño (sin munición).</summary>
    private static bool EsCuchilloUsable(WeaponDefinition def){
        return def != null && def.IsMelee && def.model != null && def.damage > 0f;
    }

    private string SerializarPool(){
        if (poolArmas == null || poolArmas.Count == 0) return "";
        var nombres = new List<string>();
        for (int i = 0; i < poolArmas.Count; i++)
            if (poolArmas[i] != null && !string.IsNullOrEmpty(poolArmas[i].itemName))
                nombres.Add(poolArmas[i].itemName);
        return string.Join("|", nombres);
    }

    private void DeserializarPool(string guardado){
        if (poolArmas == null) poolArmas = new List<WeaponDefinition>();
        else poolArmas.Clear();
        if (string.IsNullOrEmpty(guardado)) return;
        string[] nombres = guardado.Split('|');
        for (int i = 0; i < nombres.Length; i++){
            WeaponDefinition def = BuscarArma(nombres[i]);
            if (def != null && !poolArmas.Contains(def)) poolArmas.Add(def);
        }
    }

    /// <summary>
    /// Limpia el pool de entradas inválidas. Si queda vacío, se rellena con
    /// todas las armas usables del registro (comportamiento clásico).
    /// </summary>
    private void NormalizarPool(){
        if (poolArmas == null) poolArmas = new List<WeaponDefinition>();
        for (int i = poolArmas.Count - 1; i >= 0; i--){
            WeaponDefinition def = poolArmas[i];
            bool ok = def != null && (EsArmaUsable(def) || EsCuchilloUsable(def));
            if (!ok) poolArmas.RemoveAt(i);
        }
        if (poolArmas.Count == 0 && weaponRegistry != null && weaponRegistry.armas != null){
            for (int i = 0; i < weaponRegistry.armas.Count; i++){
                WeaponDefinition def = weaponRegistry.armas[i];
                if (def != null && (EsArmaUsable(def) || EsCuchilloUsable(def)))
                    poolArmas.Add(def);
            }
        }
    }

    private WeaponDefinition BuscarArma(string nombre){
        if (string.IsNullOrEmpty(nombre) || weaponRegistry == null) return null;
        for (int i = 0; i < weaponRegistry.armas.Count; i++){
            if (weaponRegistry.armas[i] != null && weaponRegistry.armas[i].itemName == nombre)
                return weaponRegistry.armas[i];
        }
        return null;
    }

    private SkillInfo BuscarSkill(string nombre){
        if (string.IsNullOrEmpty(nombre) || skillRegistry == null) return null;
        for (int i = 0; i < skillRegistry.habilidades.Count; i++){
            if (skillRegistry.habilidades[i] != null && skillRegistry.habilidades[i].skillName == nombre)
                return skillRegistry.habilidades[i];
        }
        return null;
    }
}
