using UnityEngine;

/// <summary>
/// Categoría del arma. Determina en qué slot del loadout se equipa.
/// </summary>
public enum WeaponCategory
{
    Primary,
    Secondary,
    Extra
}

/// <summary>
/// Tipo de disparo del arma. Define comportamiento de input y lógica (melee, ráfaga, perdigones).
/// </summary>
public enum WeaponType
{
    Automatic,      // Mantener pulsado dispara (fusiles)
    SemiAutomatic,  // Un disparo por click (pistolas, DMR)
    Melee,          // Sin munición, corto alcance (cuchillo)
    Shotgun         // Múltiples perdigones por disparo
}

/// <summary>
/// Datos PUROS de un arma (ScriptableObject inmutable en runtime).
/// Todo el estado de juego (munición, recarga, cooldowns) vive en WeaponRuntime.
/// Hereda de ItemDefinition (padre canónico de todos los items).
/// </summary>
[CreateAssetMenu(fileName = "WeaponDef", menuName = "Item/WeaponDefinition")]
public class WeaponDefinition : ItemDefinition
{
    [Header("Data")]
    public WeaponCategory category;
    public WeaponType weaponType = WeaponType.Automatic;

    [SerializeField] public GameObject model;
    public GameObject modelPrefab
    {
        get => model;
        set => model = value;
    }

    // Posición/rotación base del viewmodel (las aplica PlayerSmoothMovement al WeaponHolder)
    public Vector3 weaponPosition;
    public Quaternion weaponRotation = Quaternion.identity;

    // Velocidad de la animación de cambio de arma
    public float speed = 10f;

    public LayerMask hitMask = ~0;
    public Vector3 Spread = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Fire")]
    public float damage = 25f;
    public float maxDistance = 100f;

    // Semántica: <= 1 = segundos entre disparos | > 1 = disparos por segundo
    public float fireRate = 0.15f;

    // Legacy: se mantiene serializado para migración de assets existentes.
    // El valor canónico es weaponType. isAutomatic se sincroniza en OnValidate.
    [HideInInspector][SerializeField] private bool isAutomatic = true;

    [Header("Shotgun")]
    [Tooltip("Solo para WeaponType.Shotgun: perdigones por disparo")]
    public int pelletCount = 8;
    [Tooltip("Solo para Shotgun: daño se reparte por perdigón si pelletCount > 1")]
    public bool damagePerPellet = true;

    [Header("Recoil")]
    public RecoilProfile recoilProfile;

    [Header("Sway & Bob")]
    public SwayBobProfile swayBobProfile;

    [Header("Ammo & Reload")]
    public int magSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2.0f;

    /// <summary>Retardo entre disparos en segundos (normaliza fireRate).</summary>
    public float FireDelay => (fireRate > 1.0f) ? (1.0f / fireRate) : fireRate;

    // Helpers canónicos derivados de weaponType (evitan ramificar por bool/magSize)
    public bool IsAutomatic => weaponType == WeaponType.Automatic;
    public bool IsSemiAutomatic => weaponType == WeaponType.SemiAutomatic;
    public bool IsMelee => weaponType == WeaponType.Melee;
    public bool IsShotgun => weaponType == WeaponType.Shotgun;

    // Compatibilidad: isAutomatic legacy → weaponType
    public bool LegacyIsAutomatic => isAutomatic;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Migración automática: si el asset aún usa isAutomatic/magSize para inferir tipo
        // y weaponType está en default, lo sincroniza sin pisar cambios manuales.
        // Solo corre en editor.
        if (weaponType == WeaponType.Automatic && !isAutomatic && magSize <= 0)
        {
            // Caso cuchillo legacy: magSize 0 + isAutomatic false → Melee
            weaponType = WeaponType.Melee;
        }
        else if (weaponType == WeaponType.Automatic && !isAutomatic && magSize > 0)
        {
            weaponType = WeaponType.SemiAutomatic;
        }

        // Sincroniza bool legacy con el tipo canónico
        isAutomatic = (weaponType == WeaponType.Automatic);

        // Shotgun necesita pelletCount válido
        if (weaponType == WeaponType.Shotgun && pelletCount <= 1) pelletCount = 8;
        // Melee no usa munición
        if (weaponType == WeaponType.Melee)
        {
            magSize = 0;
            reserveAmmo = 0;
        }
    }
#endif
}
