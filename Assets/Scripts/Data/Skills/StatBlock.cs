using UnityEngine;

/// <summary>
/// Modificadores acumulados de un jugador (stats base + pasivas + buffs temporales).
/// NO es un ScriptableObject: es estado de runtime, uno por jugador, y solo lo
/// escribe el servidor. Las pasivas escriben aquí al spawnear; los buffs
/// temporales (Ataque mejorado CaC, Escudo, Armadura) suman y luego Caducan.
/// </summary>
[System.Serializable]
public class StatBlock
{
    [Header("Multiplicadores (1 = neutro)")]
    [Tooltip("Multiplicador de velocidad de movimiento. Mayor Agilidad = 1.15")]
    public float multiplicadorVelocidad = 1f;

    [Tooltip("Vida máxima extra en puntos absolutos. Mayor vida = +25")]
    public float vidaMaximaExtra = 0f;

    [Tooltip("Multiplicador de daño cuerpo a cuerpo (cuchillo). Ataque mejorado CaC = 1.5")]
    public float multiplicadorDanioCuerpoACuerpo = 1f;

    [Tooltip("Multiplicador de cooldown de habilidades. Aceleración = 0.8 (más rápido)")]
    public float multiplicadorCooldown = 1f;

    [Header("Defensa")]
    [Range(0f, 1f)]
    [Tooltip("Fracción de daño recibido que se ignora. Armadura = 0.5 (-50%)")]
    public float reduccionDanio = 0f;

    /// <summary>Vuelve a valores neutros (usar antes de recalcular las pasivas).</summary>
    public void Reiniciar()
    {
        multiplicadorVelocidad = 1f;
        vidaMaximaExtra = 0f;
        multiplicadorDanioCuerpoACuerpo = 1f;
        multiplicadorCooldown = 1f;
        reduccionDanio = 0f;
    }

    /// <summary>fraccion positiva = más rápido (0.15 = +15%).</summary>
    public void SumarVelocidad(float fraccion)
    {
        multiplicadorVelocidad *= 1f + fraccion;
    }

    /// <summary>Puntos absolutos de vida extra (puede ser negativo).</summary>
    public void SumarVidaMaxima(float cantidad)
    {
        vidaMaximaExtra += cantidad;
    }

    /// <summary>fraccion positiva = más daño (0.5 = +50%).</summary>
    public void SumarDanioCuerpoACuerpo(float fraccion)
    {
        multiplicadorDanioCuerpoACuerpo *= 1f + fraccion;
    }

    /// <summary>
    /// fraccion NEGATIVA = habilidades más rápidas (Aceleración = -0.2).
    /// El resultado nunca baja de 0.1 para no anular los cooldowns.
    /// </summary>
    public void SumarCooldown(float fraccion)
    {
        multiplicadorCooldown = Mathf.Max(0.1f, multiplicadorCooldown * (1f + fraccion));
    }

    /// <summary>Suma reducción de daño acumulada (se recorta entre 0 y 1).</summary>
    public void SumarReduccionDanio(float fraccion)
    {
        reduccionDanio = Mathf.Clamp01(reduccionDanio + fraccion);
    }

    /// <summary>Copia los valores de otro bloque (para respaldar un estado base).</summary>
    public void CopiarDe(StatBlock otro)
    {
        if (otro == null) return;
        multiplicadorVelocidad = otro.multiplicadorVelocidad;
        vidaMaximaExtra = otro.vidaMaximaExtra;
        multiplicadorDanioCuerpoACuerpo = otro.multiplicadorDanioCuerpoACuerpo;
        multiplicadorCooldown = otro.multiplicadorCooldown;
        reduccionDanio = otro.reduccionDanio;
    }

    public override string ToString()
    {
        return "vel x" + multiplicadorVelocidad.ToString("0.00") +
               " | vida +" + vidaMaximaExtra.ToString("0") +
               " | cac x" + multiplicadorDanioCuerpoACuerpo.ToString("0.00") +
               " | cd x" + multiplicadorCooldown.ToString("0.00") +
               " | dr " + reduccionDanio.ToString("0.00");
    }
}
