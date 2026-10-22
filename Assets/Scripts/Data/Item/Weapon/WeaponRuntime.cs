using UnityEngine;

/// <summary>
/// Estados posibles de un arma equipada. Las transiciones se validan
/// dentro de WeaponRuntime para que sean imposibles los estados inconsistentes.
/// </summary>
public enum EstadoArma
{
    Lista,
    Recargando,
    Cambiando
}

/// <summary>
/// Estado runtime de UNA instancia de arma (munición, cooldowns, estado).
/// Se construye a partir de un WeaponDefinition y NUNCA modifica el asset.
/// Clase C# pura: testeable sin escena.
/// </summary>
[System.Serializable]
public class WeaponRuntime
{
    public WeaponDefinition def;

    public int currentAmmo;
    public int reserveAmmo;
    public float nextFireTime;
    public EstadoArma estado = EstadoArma.Lista;

    public bool isReloading => estado == EstadoArma.Recargando;

    public WeaponRuntime(WeaponDefinition definition)
    {
        def = definition;
        Reiniciar();
    }

    /// <summary>Restaura munición y estado a los valores del asset.</summary>
    public void Reiniciar()
    {
        bool esMeleeTipo = def != null && def.IsMelee;
        currentAmmo = (!esMeleeTipo && def != null && def.magSize > 0) ? def.magSize : 0;
        reserveAmmo = (!esMeleeTipo && def != null && def.magSize > 0) ? def.reserveAmmo : 0;
        nextFireTime = 0f;
        estado = EstadoArma.Lista;
    }

    /// <summary>True si el arma es melee (no consume munición). Canónico: WeaponType.Melee.</summary>
    public bool esMelee => def == null || def.IsMelee;

    public bool PuedeDisparar()
    {
        return def != null
            && estado == EstadoArma.Lista
            && (esMelee || currentAmmo > 0)
            && Time.time >= nextFireTime;
    }

    /// <summary>
    /// Consume munición y fija el cooldown si se puede disparar.
    /// Devuelve el vector de dispersión (spread) aleatorio a sumar a la dirección.
    /// </summary>
    public bool IntentarDisparar(out Vector3 spread)
    {
        spread = Vector3.zero;
        if (!PuedeDisparar()) return false;

        nextFireTime = Time.time + def.FireDelay;
        if (!esMelee) currentAmmo--;

        spread = new Vector3(
            Random.Range(-def.Spread.x, def.Spread.x),
            Random.Range(-def.Spread.y, def.Spread.y),
            Random.Range(-def.Spread.z, def.Spread.z));

        return true;
    }

    public bool PuedeRecargar()
    {
        return def != null
            && estado == EstadoArma.Lista
            && currentAmmo < def.magSize
            && reserveAmmo > 0;
    }

    public void IniciarRecarga()
    {
        if (def != null && estado == EstadoArma.Lista)
            estado = EstadoArma.Recargando;
    }

    /// <summary>Transfiere munición de la reserva al cargador y vuelve a Lista.</summary>
    public void CompletarRecarga()
    {
        if (def == null) return;

        int needed = def.magSize - currentAmmo;
        int available = Mathf.Min(needed, reserveAmmo);

        currentAmmo += available;
        reserveAmmo -= available;
        estado = EstadoArma.Lista;
    }

    public void CancelarRecarga()
    {
        if (estado == EstadoArma.Recargando)
            estado = EstadoArma.Lista;
    }

    /// <summary>Corrección autoritativa del servidor (sincronización de red).</summary>
    public void ForzarMunicion(int cargador, int reserva)
    {
        currentAmmo = cargador;
        reserveAmmo = reserva;
    }
}
