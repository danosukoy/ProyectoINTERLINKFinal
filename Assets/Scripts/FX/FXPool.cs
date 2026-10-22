using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool estático de efectos (muzzle flash, impactos, trazadoras).
/// En multijugador instanciar/destruir por disparo mata el rendimiento;
/// el pool reutiliza GameObjects desactivados.
/// </summary>
public static class FXPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    /// <summary>
    /// Activa una instancia del prefab en pos/rot (la crea si el pool está vacío).
    /// 'duracion' programa su devolución automática al pool.
    /// </summary>
    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float duracion)
    {
        if (prefab == null) return null;

        GameObject instancia = null;

        if (pools.TryGetValue(prefab, out Queue<GameObject> cola) && cola.Count > 0)
        {
            instancia = cola.Dequeue();
        }

        if (instancia == null)
        {
            instancia = Object.Instantiate(prefab);
        }

        instancia.transform.position = pos;
        instancia.transform.rotation = rot;
        instancia.SetActive(true);

        // Reinicia sistemas de partículas si los tiene
        var sistemas = instancia.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < sistemas.Length; i++)
        {
            sistemas[i].Clear();
            sistemas[i].Play();
        }

        // duracion <= 0: el propio efecto gestiona su vida (p. ej. Trazadora)
        if (duracion > 0f)
        {
            var vida = instancia.GetComponent<VidaEfecto>();
            if (vida == null) vida = instancia.AddComponent<VidaEfecto>();
            vida.Programar(prefab, duracion);
        }

        return instancia;
    }

    /// <summary>Devuelve la instancia al pool (se desactiva, no se destruye).</summary>
    public static void Devolver(GameObject prefabOriginal, GameObject instancia)
    {
        if (instancia == null) return;

        instancia.SetActive(false);

        if (!pools.TryGetValue(prefabOriginal, out Queue<GameObject> cola))
        {
            cola = new Queue<GameObject>();
            pools[prefabOriginal] = cola;
        }
        cola.Enqueue(instancia);
    }
}

/// <summary>
/// Devuelve un efecto al pool pasado su tiempo de vida.
/// </summary>
public class VidaEfecto : MonoBehaviour
{
    private GameObject prefabOriginal;
    private float tiempoRestante;

    public void Programar(GameObject prefab, float duracion)
    {
        prefabOriginal = prefab;
        tiempoRestante = duracion;
    }

    private void Update()
    {
        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante <= 0f)
        {
            FXPool.Devolver(prefabOriginal, gameObject);
        }
    }
}
