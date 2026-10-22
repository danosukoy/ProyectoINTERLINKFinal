using UnityEngine;

/// <summary>
/// Aviso flotante "F - VER SALAS" / "E - VOTAR MAPA" (hint runtime de pantalla).
///
/// Antes vivía al final de MapVoteStation (la estación 3D del lobby antiguo).
/// Al retirar el lobby 3D se movió aquí, que es su sitio: lo usan
/// InteractStation2D y MapVoteStation2D.
/// </summary>
public static class MapVotePromptUI
{
    private static GameObject go;
    private static TMPro.TextMeshProUGUI texto;

    public static void Mostrar(string msg)
    {
        if (go == null) Construir();
        if (texto != null) texto.text = msg;
        if (go != null) go.SetActive(true);
    }

    public static void Ocultar() { if (go != null) go.SetActive(false); }

    private static void Construir()
    {
        var canvasGo = new GameObject("MapVotePrompt", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGo.transform.SetParent(null);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        canvasGo.transform.SetAsLastSibling();

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -120);
        rt.sizeDelta = new Vector2(260, 44);
        var img = bg.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.65f);
        img.raycastTarget = false;

        var tGo = new GameObject("Texto", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        tGo.transform.SetParent(bg.transform, false);
        var tr = tGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        texto = tGo.GetComponent<TMPro.TextMeshProUGUI>();
        texto.font = TMPro.TMP_Settings.defaultFontAsset;
        texto.fontSize = 18;
        texto.fontStyle = TMPro.FontStyles.Bold;
        texto.alignment = TMPro.TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.raycastTarget = false;
        texto.text = "E - VOTAR MAPA";
        go = canvasGo;
    }
}