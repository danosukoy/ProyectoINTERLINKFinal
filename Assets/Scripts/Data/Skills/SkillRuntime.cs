using UnityEngine;

/// <summary>
/// Estado de UNA habilidad en UN jugador. Clase normal (POCO): no es
/// MonoBehaviour ni ScriptableObject. No tiene Update ni GameObject; lo
/// tickea PlayerSkills.
///
/// Verdad autoritativa = servidor. El dueño mantiene una copia espejo para
/// pintar el HUD, que se actualiza con el RPC de confirmación.
///
/// Modelo de cargas: cada uso resta 1 carga sin cooldown. Al llegar a 0
/// arranca el cooldown completo, y al terminar se recuperan todas.
/// </summary>
public class SkillRuntime
{
    public SkillDefinition Definicion { get; private set; }
    public int IndiceSlot { get; private set; }
    public int IndiceTecla { get; private set; }

    public float CooldownRestante { get; private set; }
    public float DuracionRestante { get; private set; }
    public int CargasRestantes { get; private set; }

    public bool EnCooldown => CooldownRestante > 0f;
    public bool BuffActivo => DuracionRestante > 0f;
    public bool Vacio => Definicion == null;

    public SkillRuntime(SkillDefinition definicion, int indiceSlot, int indiceTecla)
    {
        Definicion = definicion;
        IndiceSlot = indiceSlot;
        IndiceTecla = definicion != null && definicion.EsActiva ? indiceTecla : -1;
        Reiniciar();
    }

    /// <summary>¿Se puede activar ahora? (predicción local y validación servidor).</summary>
    public bool PuedeUsar()
    {
        if (Definicion == null || !Definicion.EsActiva) return false;
        return CargasRestantes > 0 && CooldownRestante <= 0f;
    }

    /// <summary>
    /// Consume un uso. multiplicadorCooldown viene de StatBlock (Aceleración = 0.8).
    /// Devuelve el cooldown resultante para replicarlo al dueño.
    /// </summary>
    public float Gastar(float multiplicadorCooldown)
    {
        if (Definicion == null) return 0f;

        CargasRestantes = Mathf.Max(0, CargasRestantes - 1);
        if (CargasRestantes > 0) return CooldownRestante;   // quedan cargas: sigue lista

        float factor = Mathf.Max(0.1f, multiplicadorCooldown);
        CooldownRestante = Mathf.Max(0f, Definicion.cooldown * factor);
        return CooldownRestante;
    }

    /// <summary>
    /// Avanza el estado (servidor y copia del dueño). Devuelve true si un
    /// buff acaba de expirar, para que PlayerSkills recalcule el StatBlock.
    /// </summary>
    public bool Tick(float dt)
    {
        if (Definicion == null || dt <= 0f) return false;
        bool expiroBuff = false;

        if (CooldownRestante > 0f)
        {
            CooldownRestante = Mathf.Max(0f, CooldownRestante - dt);
            if (CooldownRestante == 0f) CargasRestantes = Mathf.Max(1, Definicion.cargas);
        }

        if (DuracionRestante > 0f)
        {
            DuracionRestante = Mathf.Max(0f, DuracionRestante - dt);
            if (DuracionRestante == 0f) expiroBuff = true;
        }

        return expiroBuff;
    }

    /// <summary>Arranca el efecto con duración (buffs, zonas...).</summary>
    public void IniciarDuracion()
    {
        DuracionRestante = Definicion != null ? Definicion.duracion : 0f;
    }

    /// <summary>Cancela el efecto activo (muerte, fin de partida, cambio de clase).</summary>
    public void CancelarDuracion()
    {
        DuracionRestante = 0f;
    }

    /// <summary>Estado inicial: sin cooldown y con todas las cargas.</summary>
    public void Reiniciar()
    {
        CooldownRestante = 0f;
        DuracionRestante = 0f;
        CargasRestantes = Definicion != null ? Mathf.Max(1, Definicion.cargas) : 0;
    }

    /// <summary>Espejo en el dueño: el servidor confirma el uso por RPC.</summary>
    public void SincronizarDesdeServidor(float cooldown, int cargas, float duracion = -1f)
    {
        CooldownRestante = Mathf.Max(0f, cooldown);
        CargasRestantes = Mathf.Max(0, cargas);
        if (duracion >= 0f) DuracionRestante = duracion;
    }

    /// <summary>Texto de depuración (NetcodeTestFlow / consola).</summary>
    public override string ToString()
    {
        if (Definicion == null) return "[slot vacio]";
        return Definicion.nombre +
               " | cd " + CooldownRestante.ToString("0.0") + "s" +
               " | cargas " + CargasRestantes + "/" + Definicion.cargas +
               (BuffActivo ? " | buff " + DuracionRestante.ToString("0.0") + "s" : "");
    }
}
