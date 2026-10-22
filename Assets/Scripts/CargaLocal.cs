using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga local (fuera de sesión Netcode) con pantalla: emite progreso real
/// del AsyncOperation. Las cargas en red las cubren los eventos de escena
/// de Netcode (SteamLobbyManager). No usar con Netcode escuchando.
/// </summary>
public static class CargaLocal
{
    private class Runner : MonoBehaviour { }

    private static Runner runner;

    public static void Cargar(string escena, string mensaje = null)
    {
        if (runner == null)
        {
            var go = new GameObject("CargaLocalRunner");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        runner.StartCoroutine(Rutina(escena, mensaje ?? $"Cargando {escena}..."));
    }

    private static IEnumerator Rutina(string escena, string mensaje)
    {
        AvisosCarga.Empezar(mensaje);
        var op = SceneManager.LoadSceneAsync(escena, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError($"[CargaLocal] No se pudo cargar '{escena}'.");
            AvisosCarga.Terminar();
            yield break;
        }
        while (!op.isDone)
        {
            AvisosCarga.Progreso(Mathf.Clamp01(op.progress / 0.9f));
            yield return null;
        }
        AvisosCarga.Terminar();
    }
}
