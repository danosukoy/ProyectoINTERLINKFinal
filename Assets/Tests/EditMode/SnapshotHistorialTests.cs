using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Interpolación y poda del historial usado por lag compensation.
/// </summary>
public class SnapshotHistorialTests
{
    private SnapshotHistorial hist;

    [SetUp]
    public void Setup()
    {
        hist = new SnapshotHistorial();
    }

    [Test]
    public void Vacio_DevuelveFalse()
    {
        Assert.IsFalse(hist.TryInterpolar(1f, out _, out _));
    }

    [Test]
    public void UnSoloSnapshot_DevuelveEse()
    {
        hist.Registrar(1f, new Vector3(5f, 0f, 0f), Quaternion.identity);

        Assert.IsTrue(hist.TryInterpolar(0f, out Vector3 p0, out _));
        Assert.IsTrue(hist.TryInterpolar(99f, out Vector3 p1, out _));
        Assert.AreEqual(5f, p0.x);
        Assert.AreEqual(5f, p1.x);
    }

    [Test]
    public void PuntoMedio_InterpolaLineal()
    {
        hist.Registrar(0f, Vector3.zero, Quaternion.identity);
        hist.Registrar(1f, new Vector3(10f, 0f, 0f), Quaternion.identity);

        Assert.IsTrue(hist.TryInterpolar(0.5f, out Vector3 p, out _));
        Assert.AreEqual(5f, p.x, 0.001f);
    }

    [Test]
    public void FueraDeRango_HaceClamp()
    {
        hist.Registrar(1f, new Vector3(1f, 0f, 0f), Quaternion.identity);
        hist.Registrar(2f, new Vector3(2f, 0f, 0f), Quaternion.identity);

        hist.TryInterpolar(0f, out Vector3 antes, out _);
        hist.TryInterpolar(99f, out Vector3 despues, out _);
        Assert.AreEqual(1f, antes.x);
        Assert.AreEqual(2f, despues.x);
    }

    [Test]
    public void RegistroDesordenado_SeIgnora()
    {
        hist.Registrar(2f, new Vector3(2f, 0f, 0f), Quaternion.identity);
        hist.Registrar(1f, new Vector3(1f, 0f, 0f), Quaternion.identity);

        Assert.AreEqual(1, hist.Count);
        hist.TryInterpolar(5f, out Vector3 p, out _);
        Assert.AreEqual(2f, p.x);
    }

    [Test]
    public void Poda_EliminaViejosFueraDeVentana()
    {
        hist.ventana = 1f;
        hist.Registrar(0f, Vector3.zero, Quaternion.identity);
        hist.Registrar(0.5f, Vector3.one, Quaternion.identity);
        hist.Registrar(2f, new Vector3(2f, 2f, 2f), Quaternion.identity);

        Assert.AreEqual(1, hist.Count);
        hist.TryInterpolar(0f, out Vector3 p, out _);
        Assert.AreEqual(2f, p.x);
    }

    [Test]
    public void Limpiar_Vacia()
    {
        hist.Registrar(0f, Vector3.zero, Quaternion.identity);
        hist.Limpiar();
        Assert.AreEqual(0, hist.Count);
        Assert.IsFalse(hist.TryInterpolar(0f, out _, out _));
    }
}
