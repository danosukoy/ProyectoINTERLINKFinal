using UnityEngine;

/// <summary>
/// Pasiva del Doctor: +25 puntos de vida máxima.
/// La aplica el servidor antes de fijar la vida máxima en red.
/// </summary>
[CreateAssetMenu(fileName = "Pasiva_MayorVida", menuName = "Item/Skills/Pasiva Mayor Vida")]
public class PasivaMayorVida : SkillDefinition
{
    public float vidaExtra = 25f;

    public override void AplicarAlSpawn(StatBlock stats)
    {
        if (stats == null) return;
        stats.SumarVidaMaxima(vidaExtra);
    }
}
