using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auditoría de DontDestroyOnLoad: qué sobrevive entre escenas.
/// Uso: PersistentesDebug.LogResumen() desde consola, botón debug o test.
/// La escena "DontDestroyOnLoad" es accesible por nombre en runtime.
/// </summary>
public static class PersistentesDebug
{
    public static string Resumen()
    {
        var ddol = SceneManager.GetSceneByName("DontDestroyOnLoad");
        var sb = new StringBuilder();
        sb.AppendLine($"[DDOL] escena válida={ddol.IsValid()} raíces={ddol.rootCount}");
        if (ddol.IsValid())
        {
            foreach (var go in ddol.GetRootGameObjects())
            {
                if (go == null) continue;
                var comps = go.GetComponents<Component>();
                var nombres = new StringBuilder();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (i > 0) nombres.Append(',');
                    nombres.Append(comps[i] == null ? "null" : comps[i].GetType().Name);
                }
                sb.AppendLine($"[DDOL] - {go.name} [{nombres}] hijos={go.transform.childCount}");
            }
        }
        return sb.ToString();
    }

    public static void LogResumen()
    {
        Debug.Log(Resumen());
    }
}
