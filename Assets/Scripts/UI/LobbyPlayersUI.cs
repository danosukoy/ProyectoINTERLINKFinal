using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using Steamworks.Data;
using Image = UnityEngine.UI.Image;
using Color = UnityEngine.Color;

/// <summary>
/// Panel de administración de jugadores de la sala (LobbyRoom).
/// - Lista los miembros conectados con su nombre.
/// - Marca al host ([HOST]) y a ti ([TÚ]).
/// - Muestra el contador de jugadores (X/Y).
/// Se refresca solo: al arrancar y con los eventos de Steam de entrada/salida
/// de miembros. La UI se construye en runtime (sin cableado en escena).
/// </summary>
public class LobbyPlayersUI : MonoBehaviour
{
    [Header("Apariencia")]
    [SerializeField] private float anchoPanel = 360f;
    [SerializeField] private float altoPanel = 520f;
    [SerializeField] private float margenDerecha = 24f;
    [SerializeField] private float margenArriba = 110f;

    // UI construida en runtime
    private RectTransform panel;
    private TextMeshProUGUI textoTitulo;
    private RectTransform lista;
    private readonly List<GameObject> entradas = new List<GameObject>();

    // Handlers guardados para poder desuscribirse en OnDestroy
    private System.Action<Lobby, Friend> handlerEntrada;
    private System.Action<Lobby, Friend> handlerSalida;

    private void Start()
    {
        ConstruirUI();

        if (SteamLobbyManager.Instance != null)
        {
            handlerEntrada = (l, f) => Refrescar();
            handlerSalida = (l, f) => Refrescar();
            SteamLobbyManager.Instance.OnLobbyMemberJoinedEvent += handlerEntrada;
            SteamLobbyManager.Instance.OnLobbyMemberLeftEvent += handlerSalida;
        }

        Refrescar();
    }

    private void OnDestroy()
    {
        if (SteamLobbyManager.Instance != null)
        {
            if (handlerEntrada != null) SteamLobbyManager.Instance.OnLobbyMemberJoinedEvent -= handlerEntrada;
            if (handlerSalida != null) SteamLobbyManager.Instance.OnLobbyMemberLeftEvent -= handlerSalida;
        }
    }

    // ------------------------------------------------------------------
    // Construcción de la UI
    // ------------------------------------------------------------------

    private void ConstruirUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[LobbyPlayersUI] No hay Canvas padre; el panel no se muestra.");
            return;
        }

        // Panel contenedor, anclado arriba a la derecha
        var panelGo = new GameObject("JugadoresPanel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvas.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-margenDerecha, -margenArriba);
        panel.sizeDelta = new Vector2(anchoPanel, altoPanel);

        var img = panelGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.10f, 0.14f, 0.85f);
        img.raycastTarget = false;

        // Título: "JUGADORES (X/Y)"
        var tituloGo = new GameObject("Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        tituloGo.transform.SetParent(panel, false);
        var tituloRect = tituloGo.GetComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0f, 1f);
        tituloRect.anchorMax = new Vector2(1f, 1f);
        tituloRect.pivot = new Vector2(0.5f, 1f);
        tituloRect.anchoredPosition = new Vector2(0f, -8f);
        tituloRect.sizeDelta = new Vector2(-16f, 44f);

        textoTitulo = tituloGo.GetComponent<TextMeshProUGUI>();
        textoTitulo.font = Fuente();
        if (textoTitulo.font != null) textoTitulo.fontSize = 22;
        textoTitulo.fontStyle = FontStyles.Bold;
        textoTitulo.alignment = TextAlignmentOptions.Center;
        textoTitulo.color = new Color(0.35f, 0.8f, 1f);
        textoTitulo.raycastTarget = false;

        // Lista vertical de jugadores
        var listaGo = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listaGo.transform.SetParent(panel, false);
        lista = listaGo.GetComponent<RectTransform>();
        lista.anchorMin = Vector2.zero;
        lista.anchorMax = Vector2.one;
        lista.offsetMin = new Vector2(8f, 8f);
        lista.offsetMax = new Vector2(-8f, -56f);

        var vlg = listaGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
    }

    // ------------------------------------------------------------------
    // Refresco desde el lobby
    // ------------------------------------------------------------------

    private void Refrescar()
    {
        LimpiarEntradas();

        Lobby? lobby = SteamLobbyManager.Instance != null
            ? SteamLobbyManager.Instance.CurrentLobby
            : (Lobby?)null;

        if (lobby.HasValue)
        {
            int max = lobby.Value.MaxMembers;
            int contador = 0;

            foreach (Friend miembro in lobby.Value.Members)
            {
                bool soyYo = miembro.Id == SteamClient.SteamId;
                bool esHost = miembro.Id == lobby.Value.Owner.Id;
                CrearEntrada(NombreDe(miembro), esHost, soyYo);
                contador++;
            }

            ActualizarTitulo(contador, max);
        }
        else
        {
            // Sin sala activa (p. ej. LobbyRoom abierta directamente en el editor)
            string nombre = SteamClient.IsValid ? SteamClient.Name : "Jugador 1";
            CrearEntrada(nombre, true, true);
            ActualizarTitulo(1, 4);
        }
    }

    private string NombreDe(Friend miembro)
    {
        string nombre = miembro.Name;
        return string.IsNullOrWhiteSpace(nombre) ? $"Jugador #{miembro.Id}" : nombre;
    }

    private void ActualizarTitulo(int actual, int maximo)
    {
        if (textoTitulo != null)
            textoTitulo.text = maximo > 0 ? $"JUGADORES ({actual}/{maximo})" : $"JUGADORES ({actual})";
    }

    private void CrearEntrada(string nombre, bool esHost, bool soyYo)
    {
        if (lista == null) return;

        var entryGo = new GameObject($"Entrada_{entradas.Count}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        entryGo.transform.SetParent(lista, false);
        var entryRect = entryGo.GetComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(0f, 44f);

        var entryImg = entryGo.GetComponent<Image>();
        entryImg.color = new Color(0.16f, 0.22f, 0.30f, 0.9f);
        entryImg.raycastTarget = false;

        var hlg = entryGo.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.spacing = 10f;
        hlg.childControlHeight = true;
        hlg.childControlWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;

        // Punto de color: verde si eres tú, azul si es el host, gris resto
        var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(entryGo.transform, false);
        var dotRect = dotGo.GetComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(14f, 14f);
        var dot = dotGo.GetComponent<Image>();
        dot.color = soyYo ? new Color(0.2f, 0.9f, 0.4f)
                  : esHost ? new Color(0.3f, 0.7f, 1f)
                  : new Color(0.55f, 0.58f, 0.62f);
        dot.raycastTarget = false;

        // Nombre + etiquetas
        var nameGo = new GameObject("Nombre", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameGo.transform.SetParent(entryGo.transform, false);
        var nameLayout = nameGo.GetComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;

        var tmp = nameGo.GetComponent<TextMeshProUGUI>();
        tmp.font = Fuente();
        if (tmp.font != null) tmp.fontSize = 17;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.text = nombre;

        if (esHost) tmp.text += "  <color=#00FF88><b>[HOST]</b></color>";
        if (soyYo) tmp.text += "  <color=#FFD75E><b>[TÚ]</b></color>";

        entradas.Add(entryGo);
    }

    private void LimpiarEntradas()
    {
        for (int i = 0; i < entradas.Count; i++)
        {
            if (entradas[i] != null) Destroy(entradas[i]);
        }
        entradas.Clear();
    }

    private TMP_FontAsset Fuente()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) return font;

        return null;
    }
}