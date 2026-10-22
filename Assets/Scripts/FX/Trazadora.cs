using UnityEngine;

/// <summary>
/// Línea de trazadora entre el origen del disparo y el punto de impacto.
/// Se desvanece durante su vida útil y vuelve sola al pool de FX.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class Trazadora : MonoBehaviour
{
    private LineRenderer linea;
    private GameObject prefabOriginal;
    private float duracion = 0.1f;
    private float tiempo;
    private Color colorInicio;
    private Color colorFin;

    public void Inicializar(GameObject prefab, Vector3 origen, Vector3 destino, float dur = 0.1f)
    {
        if (linea == null) linea = GetComponent<LineRenderer>();

        prefabOriginal = prefab;
        duracion = Mathf.Max(0.02f, dur);
        tiempo = 0f;

        linea.positionCount = 2;
        linea.SetPosition(0, origen);
        linea.SetPosition(1, destino);

        colorInicio = linea.startColor;
        colorFin = linea.endColor;
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(tiempo / duracion);

        linea.startColor = new Color(colorInicio.r, colorInicio.g, colorInicio.b, colorInicio.a * k);
        linea.endColor = new Color(colorFin.r, colorFin.g, colorFin.b, colorFin.a * k);

        if (tiempo >= duracion)
        {
            FXPool.Devolver(prefabOriginal, gameObject);
        }
    }
}
