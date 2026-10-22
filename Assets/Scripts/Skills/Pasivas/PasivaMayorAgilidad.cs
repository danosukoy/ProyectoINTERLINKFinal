using UnityEngine;

/// <summary>
/// Pasiva del Soldado: +15% de velocidad de movimiento.
/// </summary>
[CreateAssetMenu(fileName = "Pasiva_MayorAgilidad", menuName = "Item/Skills/Pasiva Mayor Agilidad")]
public class PasivaMayorAgilidad : SkillDefinition
{
    [Range(0f, 1f)] public float fraccionVelocidad = 0.15f;

    public override void AplicarAlSpawn(StatBlock stats)
    {
        if (stats == null) return;
        stats.SumarVelocidad(fraccionVelocidad);
    }
}
