/// <summary>
/// Contrato mínimo que PlayerCombat necesita para el feedback de recarga.
/// Lo implementa PlayerUI: así combate no depende de la UI concreta.
/// </summary>
public interface IRecargaUI
{
    void TriggerLoadRoutine(float duration);
    void StopLoadRoutine();
}
