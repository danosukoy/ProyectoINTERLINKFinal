using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Vista tonta de un arma (una celda del inventario).
/// Vive sobre el panel placeholder puesto en escena (SelectedWeapons /
/// TotalWeapons): construye su interior una vez y solo re-bindea datos.
/// Sin Update, sin Find, sin tocar PlayerDataManager.
/// </summary>
public class WeaponCard : MonoBehaviour, IPointerEnterHandler
{
    private static readonly Color FondoNormal = new Color(0.14f, 0.18f, 0.25f, 0.95f);
    private static readonly Color FondoSeleccion = new Color(0.25f, 0.55f, 0.95f, 0.95f);
    private static readonly Color FondoPendiente = new Color(1f, 0.6f, 0.15f, 0.95f);

    private Image fondo;
    private Button boton;
    private TextMeshProUGUI nombre;
    private TextMeshProUGUI stats;

    private WeaponDefinition actual;
    private System.Action<WeaponDefinition> alElegir;
    private System.Action<WeaponDefinition> alPasarMouse;

    private void Awake()
    {
        AsegurarVista();
    }

    /// <summary>
    /// Construye/cachea la vista una vez. Se revalida en Bind por si el
    /// Awake no corrió (orden de arranque, añadido en runtime).
    /// </summary>
    private void AsegurarVista()
    {
        if (fondo == null)
        {
            fondo = GetComponent<Image>();
            if (fondo == null) fondo = gameObject.AddComponent<Image>();
        }
        if (fondo != null) fondo.color = FondoNormal;

        if (boton == null)
        {
            boton = GetComponent<Button>();
            if (boton == null) boton = gameObject.AddComponent<Button>();
        }
        if (boton != null) boton.targetGraphic = fondo;

        if (nombre == null)
            nombre = transform.Find("Nombre")?.GetComponent<TextMeshProUGUI>()
                ?? CrearTexto("Nombre", 16, true, new Color(1f, 1f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -6f));
        if (stats == null)
            stats = transform.Find("Stats")?.GetComponent<TextMeshProUGUI>()
                ?? CrearTexto("Stats", 12, false, new Color(0.75f, 0.8f, 0.9f),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 6f));
    }

    private TextMeshProUGUI CrearTexto(string nombreGo, int tamano, bool negrita, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos)
    {
        var go = new GameObject(nombreGo, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(-12f, 24f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = tamano;
        if (negrita) tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>
    /// Vuelca datos a vista. Solo re cablea el clic si cambia el arma o el callback.
    /// pendiente = marcada con el último clic (naranja), pendiente de intercambio.
    /// </summary>
    public void Bind(WeaponDefinition def, bool seleccionada, System.Action<WeaponDefinition> onPick, System.Action<WeaponDefinition> onHover = null, bool pendiente = false)
    {
        if (def == null) { gameObject.SetActive(false); return; }
        AsegurarVista();
        if (fondo == null || boton == null || nombre == null || stats == null)
        {
            Debug.LogWarning($"[WeaponCard] {name} sin vista; bind omitido.");
            return;
        }
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        PonTexto(nombre, def.itemName);
        PonTexto(stats, LineaStats(def));
        Color querido = pendiente ? FondoPendiente : seleccionada ? FondoSeleccion : FondoNormal;
        if (fondo.color != querido) fondo.color = querido;

        if (actual != def || alElegir != onPick || alPasarMouse != onHover)
        {
            actual = def;
            alElegir = onPick;
            alPasarMouse = onHover;
            boton.onClick.RemoveAllListeners();
            WeaponDefinition capturada = def;
            System.Action<WeaponDefinition> callback = onPick;
            boton.onClick.AddListener(() => callback?.Invoke(capturada));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (actual != null) alPasarMouse?.Invoke(actual);
    }

    public void Vaciar()
    {
        actual = null;
        gameObject.SetActive(false);
    }

    private static void PonTexto(TextMeshProUGUI tmp, string valor)
    {
        if (tmp != null && tmp.text != valor) tmp.text = valor;
    }

    private static string LineaStats(WeaponDefinition def)
    {
        string modo = def.IsMelee ? "MELEE"
            : def.IsAutomatic ? "AUTO"
            : def.IsSemiAutomatic ? "SEMI"
            : def.weaponType.ToString().ToUpper();
        if (def.IsMelee) return $"{modo}  DAÑO {def.damage:0}";
        return $"{modo}  DAÑO {def.damage:0}  CARG {def.magSize}";
    }
}
