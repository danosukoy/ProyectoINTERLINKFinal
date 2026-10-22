using UnityEngine;

/// <summary>
/// Contexto que recibe una habilidad activa al ejecutarse. Se rellena en el
/// servidor. Solo usa tipos de UnityEngine para que Game.Data no dependa de
/// Game.Gameplay (la dirección de dependencias es Data -> Gameplay -> UI).
/// </summary>
public struct ContextoHabilidad
{
    public GameObject jugador;
    public Transform camara;
    public Vector3 origen;
    public Vector3 direccion;
    public ulong clienteId;
}

/// <summary>
/// Datos + comportamiento de UNA habilidad (patrón Strategy sobre
/// ScriptableObject: una clase por habilidad, sin switch por tipo).
/// Es inmutable en runtime: el estado (cooldown, cargas, duración) vive en
/// SkillRuntime, y la ejecución la decide siempre el servidor.
/// </summary>
public abstract class SkillDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string nombre = "Nueva habilidad";
    [TextArea(2, 4)] public string descripcion;
    public Sprite icono;

    [Header("Clasificación")]
    public ClasePersonaje clase = ClasePersonaje.Soldado;
    public TipoHabilidad tipo = TipoHabilidad.Seleccion;

    [Header("Reglas (solo activas)")]
    [Tooltip("Segundos de espera tras usarla. Se multiplica por StatBlock.multiplicadorCooldown")]
    public float cooldown = 10f;
    [Tooltip("Usos disponibles antes de recargar (1 = cooldown simple)")]
    public int cargas = 1;
    [Tooltip("Segundos que dura el efecto (0 = instantánea)")]
    public float duracion = 0f;
    [Tooltip("Si el jugador muere, se cancela el efecto")]
    public bool cancelaAlMorir = true;

    /// <summary>True si la habilidad se activa con input (selección / especial).</summary>
    public bool EsActiva => tipo != TipoHabilidad.Pasiva;

    /// <summary>
    /// PASIVAS: se llaman una sola vez al spawnear el jugador (servidor),
    /// antes de publicar la vida máxima. Deben ser idempotentes.
    /// </summary>
    public virtual void AplicarAlSpawn(StatBlock stats) { }

    /// <summary>
    /// ACTIVAS: el servidor ya validó (vivo, cargas, cooldown, permiso).
    /// Devuelve true si el efecto se aplicó (y por tanto toca gastar cooldown).
    /// NO debe escribir en StatBlock: para eso está AplicarBuff (así el
    /// recálculo desde la base no acumula deriva).
    /// </summary>
    public virtual bool EjecutarServidor(ContextoHabilidad ctx) { return false; }

    /// <summary>
    /// ACTIVAS con duración: modifica los stats mientras el efecto está vivo.
    /// PlayerSkills recalcula el StatBlock entero (base + buffs activos) cada
    /// vez que cambia algo, así que este método debe ser idempotente y NO
    /// guardar estado en el ScriptableObject.
    /// </summary>
    public virtual void AplicarBuff(StatBlock stats, float duracionRestante) { }

    /// <summary>
    /// ACTIVAS con duración: el efecto acaba de expirar (servidor). Aquí se
    /// limpia lo que NO sea stats (parar VFX, devolver objetos a la pool).
    /// </summary>
    public virtual void AlExpirar(ContextoHabilidad ctx) { }

    /// <summary>Texto para el HUD / armería (se puede sobrescribir).</summary>
    public virtual string TextoHud()
    {
        return nombre + " (" + cooldown.ToString("0") + "s)";
    }
}
