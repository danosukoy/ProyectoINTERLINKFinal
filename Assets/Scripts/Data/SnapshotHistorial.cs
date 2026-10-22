using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Historial de snapshots (tiempo, posición, rotación) de UN jugador para
/// lag compensation. Interpolación lineal/esférica, sin red ni escena:
/// testeable con EditMode tests.
/// </summary>
public class SnapshotHistorial
{
    public struct Snapshot
    {
        public float tiempo;
        public Vector3 posicion;
        public Quaternion rotacion;
    }

    /// <summary>Antigüedad máxima conservada (s). Debe superar al rewind máximo.</summary>
    public float ventana = 1.5f;

    private readonly List<Snapshot> snaps = new List<Snapshot>(64);

    public int Count => snaps.Count;

    public void Registrar(float tiempo, Vector3 posicion, Quaternion rotacion)
    {
        // Tiempos monótonos: ignora registros viejos/desordenados
        if (snaps.Count > 0 && tiempo <= snaps[snaps.Count - 1].tiempo) return;

        snaps.Add(new Snapshot { tiempo = tiempo, posicion = posicion, rotacion = rotacion });

        // Poda por ventana
        float limite = tiempo - ventana;
        int quitar = 0;
        while (quitar < snaps.Count && snaps[quitar].tiempo < limite) quitar++;
        if (quitar > 0) snaps.RemoveRange(0, quitar);
    }

    /// <summary>
    /// Pose interpolada en el instante pedido. Fuera de rango hace clamp al
    /// extremo más cercano. False si no hay datos.
    /// </summary>
    public bool TryInterpolar(float tiempo, out Vector3 posicion, out Quaternion rotacion)
    {
        posicion = Vector3.zero;
        rotacion = Quaternion.identity;
        if (snaps.Count == 0) return false;
        if (snaps.Count == 1 || tiempo <= snaps[0].tiempo)
        {
            posicion = snaps[0].posicion;
            rotacion = snaps[0].rotacion;
            return true;
        }

        Snapshot ultimo = snaps[snaps.Count - 1];
        if (tiempo >= ultimo.tiempo)
        {
            posicion = ultimo.posicion;
            rotacion = ultimo.rotacion;
            return true;
        }

        for (int i = 1; i < snaps.Count; i++)
        {
            if (snaps[i].tiempo >= tiempo)
            {
                Snapshot a = snaps[i - 1];
                Snapshot b = snaps[i];
                float tramo = b.tiempo - a.tiempo;
                float k = tramo > 0f ? (tiempo - a.tiempo) / tramo : 0f;
                posicion = Vector3.Lerp(a.posicion, b.posicion, k);
                rotacion = Quaternion.Slerp(a.rotacion, b.rotacion, k);
                return true;
            }
        }

        return false;
    }

    public void Limpiar()
    {
        snaps.Clear();
    }
}
