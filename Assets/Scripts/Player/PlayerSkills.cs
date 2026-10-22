using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Módulo de habilidades del jugador. La fachada PlayerController lo tickea en
/// TickLocal(); no sabe nada de movimiento ni de armas.
///
/// Reparto por autoridad:
/// - SERVIDOR: aplica el kit (pasivas + vida máxima), valida, ejecuta y avanza
///   los cooldowns. Aquí vive la verdad.
/// - DUEÑO: lee el input (E = selección, Q = especial), predice el cooldown
///   para no spamear el RPC y pinta el HUD con lo que confirma el servidor.
///
/// El kit es fijo al entrar a la partida: se resuelve una sola vez en el spawn.
/// El cliente NUNCA dice qué habilidad usa ni cuánto daño hace: sólo pide un slot.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class PlayerSkills : NetworkBehaviour
{
    public const int SLOT_PASIVA = 0;
    public const int SLOT_SELECCION = 1;
    public const int SLOT_ESPECIAL = 2;

    [Header("Clases (indice 0=Soldado, 1=Doctor, 2=Mago)")]
    [SerializeField] private ClaseDefinition[] clasesDisponibles;
    [Tooltip("Kit de respaldo cuando no hay PlayerDataManager (offline / pruebas)")]
    [SerializeField] private ClaseDefinition clasePorDefecto;

    [Header("Referencias")]
    [SerializeField] private PlayerCombat combate;
    [SerializeField] private Health salud;
    [Tooltip("Vacío = usa Camera.main")]
    [SerializeField] private Transform camara;

    [Header("Estado (solo lectura)")]
    [Tooltip("Indice de clase en clasesDisponibles. -1 = sin resolver. Lo escribe el servidor")]
    public NetworkVariable<int> claseRed = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly SkillRuntime[] slots = new SkillRuntime[3];
    private StatBlock statsBase = new StatBlock();   // solo pasivas
    private StatBlock stats = new StatBlock();       // pasivas + buffs activos

    /// <summary>Stats efectivos de este jugador. Sólo el servidor los escribe.</summary>
    public StatBlock Stats => stats;
    public ClaseDefinition Clase { get; private set; }

    /// <summary>El dueño pinta el HUD: (slot, cooldown, cargas).</summary>
    public event Action<int, float, int> OnUsoConfirmado;

    /// <summary>El kit se (re)construyó (índice de clase).</summary>
    public event Action<int> OnClaseAplicada;

    // 0 = seleccion (E) | 1 = especial (Q)
    private static readonly KeyCode[] Teclas = { KeyCode.E, KeyCode.Q };

    /// <summary>True en red si somos el dueño; en offline siempre (modo editor).</summary>
    private bool EsMio => !IsSpawned || IsOwner;

    // ---------- Ciclo de vida ----------

    private void Awake()
    {
        if (salud == null) salud = GetComponent<Health>();
        if (combate == null) combate = GetComponent<PlayerCombat>();
    }

    private void Start()
    {
        // En red lo hace OnNetworkSpawn; aquí sólo el modo offline del editor.
        if (IsSpawned) return;
        AplicarKit(ResolverClase());
    }

    public override void OnNetworkSpawn()
    {
        claseRed.OnValueChanged += AlCambiarClaseRed;

        if (IsServer)
        {
            AplicarKit(ResolverClase());
        }
        else if (claseRed.Value >= 0)
        {
            // Cliente: sólo la copia visual del kit (HUD). El servidor ya aplicó stats.
            ConstruirSlots(ClaseDeIndice(claseRed.Value));
        }
    }

    public override void OnNetworkDespawn()
    {
        claseRed.OnValueChanged -= AlCambiarClaseRed;
    }

    /// <summary>
    /// Avanza cooldowns y duraciones. Corre en el servidor (verdad) y en la
    /// copia del dueño (sólo para el HUD). Los clientes remotos no lo hacen.
    /// </summary>
    private void Update()
    {
        if (IsSpawned && !IsServer && !IsOwner) return;
        if (Time.timeScale == 0f) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < slots.Length; i++)
        {
            var rt = slots[i];
            if (rt == null || rt.Vacio) continue;

            bool expiro = rt.Tick(dt);
            if (!expiro) continue;

            // Sólo el servidor cierra el efecto; el dueño lo refleja por RPC/tick.
            if (!IsServer) continue;
            rt.Definicion.AlExpirar(ConstruirContexto());
            RecalcularStats();
        }
    }

    // ---------- Input (dueño) ----------

    /// <summary>Lo llama PlayerController.TickLocal(). Sólo lee input, no aplica nada.</summary>
    public void TickLocal()
    {
        if (!EsMio || Clase == null) return;
        if (Time.timeScale == 0f) return;
        if (BloqueoInterfaz.HayModalAbierta) return;
        if (salud != null && salud.Muerto) return;
        if (Input.GetKeyDown(KeyCode.Escape)) return;

        for (int i = SLOT_SELECCION; i <= SLOT_ESPECIAL; i++)
        {
            var rt = slots[i];
            if (rt == null || rt.Vacio || rt.IndiceTecla < 0 || rt.IndiceTecla >= Teclas.Length) continue;
            if (!Input.GetKeyDown(Teclas[rt.IndiceTecla])) continue;
            if (!rt.PuedeUsar()) continue;   // predicción local: evita spam de RPC

            if (IsSpawned) UsarSlotServerRpc(i);
            else UsarSlotOffline(i);
        }
    }

    // ---------- Servidor ----------

    [Rpc(SendTo.Server)]
    private void UsarSlotServerRpc(int slot, RpcParams rpcParams = default)
    {
        // Anti-suplantación: sólo el dueño del objeto puede usar su slot
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        EjecutarSlotServidor(slot, out float cooldown, out int cargas, out float duracion);

        if (cooldown < 0f) return;   // no se ejecutó: no se confirma nada
        ConfirmarUsoClientRpc(slot, cooldown, cargas, duracion);
    }

    /// <summary>
    /// Validación + ejecución autoritativa. Devuelve cooldown &lt; 0 si no se
    /// pudo usar (muerto, protegido, cargas, cooldown o efecto que falló).
    /// </summary>
    private void EjecutarSlotServidor(int slot, out float cooldown, out int cargas, out float duracion)
    {
        cooldown = -1f;
        cargas = 0;
        duracion = 0f;

        if (slot < 0 || slot >= slots.Length) return;
        if (salud != null && (salud.Muerto || salud.protegido.Value)) return;

        var rt = slots[slot];
        if (rt == null || rt.Vacio || !rt.PuedeUsar()) return;

        var ctx = ConstruirContexto();
        if (!rt.Definicion.EjecutarServidor(ctx)) return;   // el efecto no se aplicó

        if (rt.Definicion.duracion > 0f)
        {
            rt.IniciarDuracion();
            RecalcularStats();
        }

        cooldown = rt.Gastar(stats.multiplicadorCooldown);
        cargas = rt.CargasRestantes;
        duracion = rt.DuracionRestante;
    }

    /// <summary>Modo offline del editor: mismo camino pero sin RPC ni red.</summary>
    private void UsarSlotOffline(int slot)
    {
        EjecutarSlotServidor(slot, out float cooldown, out int cargas, out float duracion);
        if (cooldown < 0f) return;
        slots[slot].SincronizarDesdeServidor(cooldown, cargas, duracion);
        OnUsoConfirmado?.Invoke(slot, cooldown, cargas);
    }

    [Rpc(SendTo.Owner)]
    private void ConfirmarUsoClientRpc(int slot, float cooldown, int cargas, float duracion)
    {
        if (slot < 0 || slot >= slots.Length) return;
        var rt = slots[slot];
        if (rt == null) return;

        rt.SincronizarDesdeServidor(cooldown, cargas, duracion);
        OnUsoConfirmado?.Invoke(slot, cooldown, cargas);
    }

    // ---------- Kit y stats ----------

    private ClaseDefinition ResolverClase()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm != null && pdm.claseElegida != null) return pdm.claseElegida;
        if (clasePorDefecto != null) return clasePorDefecto;
        return ClaseDeIndice(0);
    }

    private ClaseDefinition ClaseDeIndice(int indice)
    {
        if (clasesDisponibles == null || indice < 0 || indice >= clasesDisponibles.Length) return null;
        return clasesDisponibles[indice];
    }

    private int IndiceDeClase(ClaseDefinition clase)
    {
        if (clase == null || clasesDisponibles == null) return -1;
        for (int i = 0; i < clasesDisponibles.Length; i++)
            if (clasesDisponibles[i] == clase) return i;
        return -1;
    }

    /// <summary>Servidor (u offline): construye slots, aplica pasivas y vida máxima.</summary>
    private void AplicarKit(ClaseDefinition clase)
    {
        if (clase == null)
        {
            Debug.LogWarning("[Habilidades] Sin clase asignada: revisa clasesDisponibles en el Player.prefab.");
            return;
        }

        ConstruirSlots(clase);

        statsBase = new StatBlock();
        clase.AplicarPasivas(statsBase);
        stats.CopiarDe(statsBase);

        if (salud != null)
            salud.EstablecerVidaMaxima(salud.VidaMaximaBase + stats.vidaMaximaExtra, true);

        if (IsSpawned && IsServer)
        {
            int indice = IndiceDeClase(clase);
            if (indice >= 0) claseRed.Value = indice;
        }
    }

    private void ConstruirSlots(ClaseDefinition clase)
    {
        Clase = clase;
        if (clase == null)
        {
            slots[SLOT_PASIVA] = null;
            slots[SLOT_SELECCION] = null;
            slots[SLOT_ESPECIAL] = null;
            return;
        }

        slots[SLOT_PASIVA] = new SkillRuntime(clase.SkillDe(TipoHabilidad.Pasiva), SLOT_PASIVA, -1);
        slots[SLOT_SELECCION] = new SkillRuntime(clase.SkillDe(TipoHabilidad.Seleccion), SLOT_SELECCION, 0);
        slots[SLOT_ESPECIAL] = new SkillRuntime(clase.SkillDe(TipoHabilidad.Especial), SLOT_ESPECIAL, 1);

        OnClaseAplicada?.Invoke(IndiceDeClase(clase));
    }

    /// <summary>
    /// Recalcula stats desde la base (pasivas) + buffs activos. Se recalcula
    /// entero a propósito: restar deltas acumula deriva con el tiempo.
    /// </summary>
    private void RecalcularStats()
    {
        stats.CopiarDe(statsBase);

        for (int i = 0; i < slots.Length; i++)
        {
            var rt = slots[i];
            if (rt == null || rt.Vacio || !rt.BuffActivo) continue;
            rt.Definicion.AplicarBuff(stats, rt.DuracionRestante);
        }

        if (salud != null)
            salud.EstablecerVidaMaxima(salud.VidaMaximaBase + stats.vidaMaximaExtra, false);
    }

    private void AlCambiarClaseRed(int anterior, int nuevo)
    {
        if (IsServer) return;   // el servidor ya tiene su kit aplicado
        ConstruirSlots(ClaseDeIndice(nuevo));
    }

    /// <summary>Contexto para ejecutar habilidades (servidor).</summary>
    public ContextoHabilidad ConstruirContexto()
    {
        var ctx = new ContextoHabilidad();
        ctx.jugador = gameObject;
        ctx.clienteId = IsSpawned ? OwnerClientId : ulong.MaxValue;

        Transform vista = camara != null ? camara : (Camera.main != null ? Camera.main.transform : null);
        if (vista != null)
        {
            ctx.camara = vista;
            ctx.origen = vista.position;
            ctx.direccion = vista.forward;
        }
        else
        {
            ctx.camara = null;
            ctx.origen = transform.position;
            ctx.direccion = transform.forward;
        }

        return ctx;
    }

    /// <summary>Resumen para depuración (NetcodeTestFlow F5).</summary>
    public string Resumen()
    {
        string clase = Clase != null ? Clase.clase.ToString() : "sin clase";
        string texto = "[Habilidades] " + clase + " | " + stats.ToString() +
                       " | vida " + (salud != null ? salud.Vida.ToString("0") + "/" + salud.VidaMaxima.ToString("0") : "-");
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].Vacio) texto += "\n  slot " + i + ": " + slots[i];
        return texto;
    }
}
