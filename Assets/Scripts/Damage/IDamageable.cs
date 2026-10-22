using UnityEngine;

/// <summary>
/// Interfaz de daño: cualquier cosa que pueda recibir daño la implementa
/// (jugadores, y en el futuro enemigos u objetos destruibles).
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Aplica daño. 'punto' y 'direccion' describen el impacto
    /// (para futuros FX, empuje, etc.). 'instigador' puede ser null.
    /// </summary>
    void RecibirDanio(float cantidad, Vector3 punto, Vector3 direccion, GameObject instigador);
}
