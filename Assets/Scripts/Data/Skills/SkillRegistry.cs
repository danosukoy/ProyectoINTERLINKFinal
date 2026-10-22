using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de todas las habilidades del juego.
/// Lo usa el menú de armería (genera los botones dinámicamente).
/// Añadir una habilidad nueva = crear su SkillInfo y listarla aquí.
/// </summary>
[CreateAssetMenu(fileName = "SkillRegistry", menuName = "Item/SkillRegistry")]
public class SkillRegistry : ScriptableObject
{
    public List<SkillInfo> habilidades = new List<SkillInfo>();
}
