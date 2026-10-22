/// <summary>
/// Tipo de habilidad dentro del kit de una clase.
/// Pasiva  -> se aplica sola al spawnear (no tiene input ni cooldown).
/// Seleccion -> activa a voluntad, cooldown medio (tecla E).
/// Especial  -> activa a voluntad, cooldown largo / cargas (tecla Q).
/// </summary>
public enum TipoHabilidad
{
    Pasiva,
    Seleccion,
    Especial
}
