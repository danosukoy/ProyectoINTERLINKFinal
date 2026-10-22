using NUnit.Framework;

/// <summary>
/// Reglas de victoria FFA verificadas sin escena ni red.
/// Si estos pasan, GameModeManager (capa fina de red) delega bien.
/// </summary>
public class FFAScoreboardTests
{
    private FFAScoreboard tabla;

    [SetUp]
    public void Setup()
    {
        tabla = new FFAScoreboard();
    }

    [Test]
    public void Kill_SumaAlAsesino_Y_MuerteALaVictima()
    {
        tabla.RegistrarKill(1ul, 2ul);

        Assert.AreEqual(1, tabla.GetKills(1ul));
        Assert.AreEqual(0, tabla.GetDeaths(1ul));
        Assert.AreEqual(0, tabla.GetKills(2ul));
        Assert.AreEqual(1, tabla.GetDeaths(2ul));
    }

    [Test]
    public void Suicidio_NoSumaKill_PeroSiMuerte()
    {
        tabla.RegistrarKill(1ul, 1ul);

        Assert.AreEqual(0, tabla.GetKills(1ul));
        Assert.AreEqual(1, tabla.GetDeaths(1ul));
    }

    [Test]
    public void AsesinoInvalido_NoSumaKill()
    {
        tabla.RegistrarKill(FFAScoreboard.SinAsesino, 2ul);

        Assert.AreEqual(0, tabla.GetKills(2ul));
        Assert.AreEqual(1, tabla.GetDeaths(2ul));
        Assert.AreEqual(0, tabla.Jugadores - 1); // solo la víctima registrada
    }

    [Test]
    public void Tabla_OrdenadaPorKillsDesc()
    {
        tabla.RegistrarKill(1ul, 9ul);
        tabla.RegistrarKill(2ul, 9ul);
        tabla.RegistrarKill(2ul, 9ul);
        tabla.RegistrarKill(3ul, 9ul);
        tabla.RegistrarKill(3ul, 9ul);
        tabla.RegistrarKill(3ul, 9ul);

        var t = tabla.Tabla();

        Assert.AreEqual(3ul, t[0].clientId);
        Assert.AreEqual(3, t[0].killCount);
        Assert.AreEqual(2ul, t[1].clientId);
        Assert.AreEqual(1ul, t[2].clientId);
    }

    [Test]
    public void Empate_GanaElRegistradoAntes()
    {
        tabla.RegistrarJugador(7ul);
        tabla.RegistrarJugador(3ul);
        tabla.RegistrarKill(7ul, 9ul);
        tabla.RegistrarKill(3ul, 9ul);

        Assert.AreEqual(7ul, tabla.LiderPorTiempo());

        var t = tabla.Tabla();
        Assert.AreEqual(7ul, t[0].clientId);
        Assert.AreEqual(3ul, t[1].clientId);
    }

    [Test]
    public void GanadorPorScore_NullAntesDelLimite_IdAlAlcanzarlo()
    {
        const int limite = 3;
        tabla.RegistrarKill(1ul, 2ul);
        tabla.RegistrarKill(1ul, 2ul);
        Assert.IsNull(tabla.GanadorPorScore(limite));

        tabla.RegistrarKill(1ul, 2ul);
        Assert.AreEqual(1ul, tabla.GanadorPorScore(limite));
    }

    [Test]
    public void GanadorPorScore_LimiteCeroONegativo_EsNull()
    {
        tabla.RegistrarKill(1ul, 2ul);
        Assert.IsNull(tabla.GanadorPorScore(0));
        Assert.IsNull(tabla.GanadorPorScore(-5));
    }

    [Test]
    public void Desconocido_DevuelveCero()
    {
        Assert.AreEqual(0, tabla.GetKills(123ul));
        Assert.AreEqual(0, tabla.GetDeaths(123ul));
    }

    [Test]
    public void Limpiar_ReseteaTodo()
    {
        tabla.RegistrarKill(1ul, 2ul);
        tabla.Limpiar();

        Assert.AreEqual(0, tabla.Jugadores);
        Assert.AreEqual(0, tabla.GetKills(1ul));
        Assert.AreEqual(0, tabla.Tabla().Count);
        Assert.AreEqual(FFAScoreboard.SinAsesino, tabla.LiderPorTiempo());
    }
}
