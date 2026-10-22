using UnityEngine;

/// <summary>
/// Pasiva del Mago: -20% de cooldown en todas sus habilidades activas.
/// </summary>
[CreateAssetMenu(fileName = "Pasiva_Aceleracion", menuName = "Item/Skills/Pasiva Aceleracion")]
public class PasivaAceleracion : SkillDefinition
{
    [Range(-0.9f, 0f)] public float fraccionCooldown = -0.2f;

    public override void AplicarAlSpawn(StatBlock stats)
    {
        if (stats == null) return;
        stats.SumarCooldown(fraccionCooldown);
    }
}
