using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de todas las armas del juego.
/// Lo usan el menú de armería (genera los botones dinámicamente)
/// y, en el futuro, la sincronización de loadout en red.
/// Añadir un arma nueva = crear su WeaponDefinition y listarla aquí.
/// </summary>
[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Item/WeaponRegistry")]
public class WeaponRegistry : ScriptableObject
{
    public List<WeaponDefinition> armas = new List<WeaponDefinition>();
}
