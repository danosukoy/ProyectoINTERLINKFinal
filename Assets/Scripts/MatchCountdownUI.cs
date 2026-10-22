using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Contador regresivo genérico en pantalla (p. ej. "LA PARTIDA COMIENZA EN 5").
/// Se construye en runtime (sin cableado en escena) y se usa desde LobbyRoom
/// (host iniciando partida) y desde la partida en solitario (JUGAR SOLO).
/// </summary>
public static class MatchCountdown
{
    /// <summary>Indica si ya hay un contador activo (evita dobles arranques).</summary>
    public static bool Activo { get; set; }

    /// <summary>
    /// Destruye el overlay si existe (p. ej. desconexión a mitad de cuenta).
    /// El flag Activo se resetea en el OnDestroy de la rutina.
    /// </summary>
    public static void Cancelar()
    {
        var overlay = GameObject.Find("MatchCountdownOverlay");
        if (overlay != null) Object.Destroy(overlay);
        Activo = false;
    }

    /// <summary>
    /// Muestra un contador de 'segundos' a 1 con el mensaje indicado; al llegar
    /// a 0 ejecuta 'alTerminar' y destruye el overlay.
    /// </summary>
    public static void Iniciar(int segundos, string mensaje, System.Action alTerminar)
    {
        if (Activo)
        {
            Debug.LogWarning("[MatchCountdown] Ya hay un contador activo, se ignora la nueva petición.");
            return;
        }

        if (segundos < 1)
        {
            alTerminar?.Invoke();
            return;
        }

        Activo = true;

        var holder = new GameObject("MatchCountdownOverlay");
        var rutina = holder.AddComponent<MatchCountdownRutina>();
        rutina.Ejecutar(segundos, mensaje, alTerminar);
    }
}

/// <summary>Rutina interna del contador (MonoBehaviour para corrutinas).</summary>
public class MatchCountdownRutina : MonoBehaviour
{
    private int segundos;
    private string mensaje;
    private System.Action alTerminar;
    private TextMeshProUGUI texto;

    public void Ejecutar(int segundos, string mensaje, System.Action alTerminar)
    {
        this.segundos = segundos;
        this.mensaje = mensaje;
        this.alTerminar = alTerminar;
        ConstruirUI();
        StartCoroutine(Contar());
    }

    private void OnDestroy()
    {
        MatchCountdown.Activo = false;
    }

    // ------------------------------------------------------------------
    // UI construida en runtime
    // ------------------------------------------------------------------

    private void ConstruirUI()
    {
        // Canvas propio con orden alto para pintar por encima de todo.
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        // Fondo oscuro translúcido (no bloquea clics: raycastTarget = false)
        var bg = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        bgImg.raycastTarget = false;

        // Texto central: mensaje + número grande
        var textGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        texto = textGo.GetComponent<TextMeshProUGUI>();
        texto.font = ObtenerFuente();
        if (texto.font != null) texto.fontSize = 54;
        texto.fontStyle = FontStyles.Bold;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
    }

    private TMP_FontAsset ObtenerFuente()
    {
        // Fuente por defecto de TMP o la de TMP Essentials (LiberationSans SDF).
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) return font;

        Debug.LogWarning("[MatchCountdown] No se encontró fuente TMP; el contador se mostrará sin texto visible.");
        return null;
    }

    // ------------------------------------------------------------------

    private IEnumerator Contar()
    {
        for (int i = segundos; i >= 1; i--)
        {
            if (texto != null)
                texto.text = $"{mensaje}\n\n<size=120>{i}</size>";
            yield return new WaitForSecondsRealtime(1f);
        }

        var callback = alTerminar;
        Destroy(gameObject);
        callback?.Invoke();
    }
}