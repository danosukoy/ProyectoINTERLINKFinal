using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lag compensation del servidor: guarda snapshots de cada avatar y permite
/// rebobinar sus hitboxes al instante que vio el tirador (ahora - RTT).
/// Uso: using (comp.RebobinarExcepto(shooterId, segundos)) { raycast... }
/// Al salir del using todo vuelve a su sitio (con Physics.SyncTransforms).
/// Solo vive en el servidor; en clientes/offline no hace nada.
/// </summary>
public class LagCompensator
{
    /// <summary>Rewind máximo aplicado aunque el RTT medido sea mayor.</summary>
    public float rewindMaximo = 0.5f;

    private readonly Dictionary<ulong, SnapshotHistorial> historiales = new Dictionary<ulong, SnapshotHistorial>();
    private readonly Dictionary<ulong, float> rtts = new Dictionary<ulong, float>();

    public void Registrar(ulong clientId, Vector3 posicion, Quaternion rotacion, float tiempoServidor)
    {
        if (!historiales.TryGetValue(clientId, out var h))
        {
            h = new SnapshotHistorial();
            historiales[clientId] = h;
        }
        h.Registrar(tiempoServidor, posicion, rotacion);
    }

    public void ReportarRtt(ulong clientId, float rtt)
    {
        if (rtt < 0f) return;
        if (!rtts.TryGetValue(clientId, out float previo))
            rtts[clientId] = Mathf.Min(rtt, rewindMaximo);
        else
            rtts[clientId] = Mathf.Min(Mathf.Lerp(previo, rtt, 0.25f), rewindMaximo);
    }

    public float GetRtt(ulong clientId)
    {
        return rtts.TryGetValue(clientId, out float r) ? r : 0f;
    }

    public void Olvidar(ulong clientId)
    {
        historiales.Remove(clientId);
        rtts.Remove(clientId);
    }

    public void Limpiar()
    {
        historiales.Clear();
        rtts.Clear();
    }

    /// <summary>
    /// Mueve las raíces de todos los avatares (menos el tirador) a su pose
    /// interpolada en (ahoraServidor - segundos). El scope las restaura.
    /// Sin datos de un jugador, ese se queda donde está.
    /// </summary>
    public RewindScope RebobinarExcepto(ulong tiradorId, float segundos, float ahoraServidor)
    {
        float objetivo = ahoraServidor - Mathf.Clamp(segundos, 0f, rewindMaximo);
        var respaldo = new List<(Transform t, Vector3 p, Quaternion r)>();

        foreach (var pc in PlayerController.Todos)
        {
            if (pc == null || pc.OwnerClientId == tiradorId) continue;
            if (!historiales.TryGetValue(pc.OwnerClientId, out var h)) continue;
            if (!h.TryInterpolar(objetivo, out Vector3 pos, out Quaternion rot)) continue;

            Transform t = pc.transform;
            respaldo.Add((t, t.position, t.rotation));
            t.SetPositionAndRotation(pos, rot);
        }

        Physics.SyncTransforms();
        return new RewindScope(respaldo);
    }

    public readonly struct RewindScope : IDisposable
    {
        private readonly List<(Transform t, Vector3 p, Quaternion r)> respaldo;

        public RewindScope(List<(Transform t, Vector3 p, Quaternion r)> respaldo)
        {
            this.respaldo = respaldo;
        }

        public void Dispose()
        {
            if (respaldo == null) return;
            for (int i = 0; i < respaldo.Count; i++)
            {
                var (t, p, r) = respaldo[i];
                if (t != null) t.SetPositionAndRotation(p, r);
            }
            Physics.SyncTransforms();
        }
    }
}
