/// <summary>
/// Caja 1: equipos de TDM. 0 = sin equipo, 1 = Rojo, 2 = Azul.
/// Sin Unity, sin red: solo números para no romper Game.Data.
/// </summary>
public static class Equipo
{
    public const int Ninguno = 0;
    public const int Rojo = 1;
    public const int Azul = 2;
    public const int MaxPorEquipo = 6;

    public static string Nombre(int equipo)
    {
        return equipo == Rojo ? "ROJO" : equipo == Azul ? "AZUL" : "—";
    }

    public static bool EsValido(int equipo)
    {
        return equipo == Rojo || equipo == Azul;
    }
}
