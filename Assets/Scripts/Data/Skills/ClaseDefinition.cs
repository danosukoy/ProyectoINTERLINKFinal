using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kit completo de una clase: pasiva + selección + especial.
/// Las habilidades son fijas al entrar a la partida, así que el kit es la
/// única fuente de verdad (no se eligen slots por separado).
/// El kit admite huecos: SkillDe(tipo) puede devolver null.
/// </summary>
[CreateAssetMenu(fileName = "Clase", menuName = "Item/ClaseDefinition")]
public class ClaseDefinition : ScriptableObject
{
    public ClasePersonaje clase = ClasePersonaje.Soldado;
    public Sprite icono;
    [TextArea(2, 4)] public string descripcion;

    [Tooltip("Habilidades del kit. Cada tipo debe aparecer como máximo una vez.")]
    public List<SkillDefinition> kit = new List<SkillDefinition>();

    /// <summary>Habilidad del tipo pedido, o null si el kit no la tiene.</summary>
    public SkillDefinition SkillDe(TipoHabilidad tipo)
    {
        if (kit == null) return null;
        for (int i = 0; i < kit.Count; i++)
        {
            if (kit[i] != null && kit[i].tipo == tipo) return kit[i];
        }
        return null;
    }

    /// <summary>Aplica todas las pasivas del kit a un StatBlock recién reiniciado.</summary>
    public void AplicarPasivas(StatBlock stats)
    {
        if (stats == null) return;
        stats.Reiniciar();
        if (kit == null) return;
        for (int i = 0; i < kit.Count; i++)
        {
            if (kit[i] == null || kit[i].tipo != TipoHabilidad.Pasiva) continue;
            kit[i].AplicarAlSpawn(stats);
        }
    }
}
