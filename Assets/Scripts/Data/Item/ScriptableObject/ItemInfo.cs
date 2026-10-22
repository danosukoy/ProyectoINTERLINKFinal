using UnityEngine;

/// <summary>
/// Alias legacy para compatibilidad con assets existentes.
/// ItemDefinition es ahora el padre canónico; ItemInfo se mantiene como wrapper.
/// </summary>
[System.Obsolete("Usa ItemDefinition como padre. ItemInfo se mantiene por compatibilidad.")]
public class ItemInfo : ItemDefinition { }
