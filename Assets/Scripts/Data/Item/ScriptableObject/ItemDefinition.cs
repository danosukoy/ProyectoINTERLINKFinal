using UnityEngine;

/// <summary>
/// Definición base para todos los items del juego.
/// Padre de WeaponDefinition y futuros ItemDefinition derivados (consumibles, perks, etc.).
/// Contiene los datos comunes serializables en ScriptableObject.
/// </summary>
public class ItemDefinition : ScriptableObject
{
    [Header("Item")]
    public string itemName;
    [TextArea] public string itemInfo;
}
