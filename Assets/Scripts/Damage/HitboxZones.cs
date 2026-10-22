using UnityEngine;

/// <summary>
/// Marca un collider como zona de daño con multiplicador
/// (p. ej. cabeza x2, torso x1). Se coloca en colliders trigger
/// hijos del jugador, en la layer "Hitbox".
/// </summary>
public class HitboxZones : MonoBehaviour
{
    [Tooltip("Multiplicador de daño para impactos en esta zona (cabeza = 2, torso = 1)")]
    public float multiplicador = 1f;
}
