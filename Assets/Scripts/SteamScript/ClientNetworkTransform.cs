using Unity.Netcode.Components;

/// <summary>
/// NetworkTransform con autoridad en el CLIENTE dueño del objeto.
/// El cliente propietario envía sus actualizaciones de transform al resto;
/// el servidor solo las retransmite. Ideal para movimiento de jugadores
/// controlado por física local (estilo Crab Game / PEAK).
/// </summary>
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
