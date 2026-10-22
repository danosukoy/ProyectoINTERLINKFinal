using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// HUD de puntuación FFA: barra superior (modo + timer + objetivo + tus kills)
/// + scoreboard con TAB + killfeed lateral. Runtime, sin cableado en escena.
/// </summary>
public class GameModeHUD : MonoBehaviour
{
    public static GameModeHUD Instance { get; private set; }

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("GameModeHUD");
        go.AddComponent<GameModeHUD>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear() => EnsureExists();

    private GameObject root;
    private TextMeshProUGUI topBarTexto;
    private GameObject scoreboardPanel;
    private Transform scoreboardLista;
    private readonly List<FilaVista> poolFilas = new List<FilaVista>();
    private Transform killfeedLista;
    private readonly List<GameObject> feedPool = new List<GameObject>();
    private readonly List<TextMeshProUGUI> feedTextos = new List<TextMeshProUGUI>();
    private readonly List<Coroutine> feedRutinas = new List<Coroutine>();
    private readonly List<int> feedVersion = new List<int>();
    private int feedIndice;

    // Cachés anti-alloc: topbar a 4Hz, nombres de Steam y líder.
    private float proximoTopbar;
    private string ultimoTopbarTexto;
    private string liderCache = "—";
    private readonly Dictionary<ulong, string> nombresCache = new Dictionary<ulong, string>();
    private bool scoreboardSucio = true;

    private sealed class FilaVista
    {
        public GameObject root;
        public Image fondo;
        public TextMeshProUGUI pos;
        public TextMeshProUGUI nombre;
        public TextMeshProUGUI kills;
        public TextMeshProUGUI muertes;
        public Image kickImg;
        public Button kickBtn;
        public ulong clientId = ulong.MaxValue;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConstruirUI();
    }

    private void OnEnable() => TrySubscribe();
    private void OnDestroy()
    {
        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.OnScoresActualizados -= AlCambiarScores;
            GameModeManager.Instance.OnKillFeed -= OnKill;
        }
    }

    private void TrySubscribe()
    {
        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.OnScoresActualizados -= AlCambiarScores;
            GameModeManager.Instance.OnKillFeed -= OnKill;
            GameModeManager.Instance.OnScoresActualizados += AlCambiarScores;
            GameModeManager.Instance.OnKillFeed += OnKill;
            nombresCache.Clear();
            AlCambiarScores();
        }
        else Invoke(nameof(TrySubscribe), 1f);
    }

    /// <summary>Scores cambiaron: actualiza cachés y solo reconstruye si el panel se ve.</summary>
    private void AlCambiarScores()
    {
        ActualizarCaches();
        if (scoreboardPanel != null && scoreboardPanel.activeSelf)
        {
            RefrescarScoreboard();
            scoreboardSucio = false;
        }
        else scoreboardSucio = true;
        ProbarActualizarTopbar(true);
    }

    private string NombreCache(ulong clientId)
    {
        if (nombresCache.TryGetValue(clientId, out string n)) return n;
        string nombre = GameModeManager.Instance != null
            ? GameModeManager.Instance.GetPlayerDisplayName(clientId)
            : $"Jugador #{clientId}";
        if (nombresCache.Count > 64) nombresCache.Clear();
        nombresCache[clientId] = nombre;
        return nombre;
    }

    private void ActualizarCaches()
    {
        var gm = GameModeManager.Instance;
        if (gm == null) return;
        if (gm.EsTDM()) { liderCache = null; return; }
        var tabla = gm.GetTablaFFA();
        liderCache = tabla.Count > 0 ? $"{NombreCache(tabla[0].clientId)} {tabla[0].killCount}" : "—";
    }

    private void ProbarActualizarTopbar(bool forzar)
    {
        if (!forzar && Time.unscaledTime < proximoTopbar) return;
        proximoTopbar = Time.unscaledTime + 0.25f;
        ActualizarTopBar();
    }

    private void Update()
    {
        ProbarActualizarTopbar(false);
        // TAB mantenido = scoreboard + cursor libre para clicar (p. ej. expulsar).
        // Mientras se ve, el combate queda bloqueado (el click es para la UI).
        bool tab = Input.GetKey(KeyCode.Tab);
        if (scoreboardPanel != null && scoreboardPanel.activeSelf != tab)
        {
            scoreboardPanel.SetActive(tab);
            BloqueoInterfaz.DisparoBloqueadoPorUI = tab;
            if (tab)
            {
                if (scoreboardSucio) { RefrescarScoreboard(); scoreboardSucio = false; }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!BloqueoInterfaz.HayModalAbierta
                && (LobbyArmeriaController.Instance == null || !LobbyArmeriaController.Instance.Abierto))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        // Offline sin manager: oculta
        if (GameModeManager.Instance == null && root != null && root.activeSelf)
            root.SetActive(false);
        else if (GameModeManager.Instance != null && root != null && !root.activeSelf)
            root.SetActive(true);
    }

    private void ActualizarTopBar()
    {
        if (topBarTexto == null || GameModeManager.Instance == null) return;
        var gm = GameModeManager.Instance;
        var modoDef = gm.ModoActual();
        string modo = modoDef != null ? modoDef.modeName.ToUpper() : "FFA";
        if (gm.EsTDM())
        {
            ActualizarTopBarTDM(gm, modo);
            return;
        }
        string tiempo;
        if (gm.estado.Value == (int)GameModeManager.EstadoPartida.CuentaAtras)
            tiempo = "INICIANDO...";
        else if (gm.estado.Value == (int)GameModeManager.EstadoPartida.Finalizada)
            tiempo = "FIN";
        else
        {
            float t = gm.tiempoPartida.Value;
            if (t <= 0f && !gm.IsSpawned) t = 0f;
            tiempo = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
        }
        var modoAct = gm.ModoActual();
        int objetivo = modoAct != null ? modoAct.scoreToWin : 15;
        ulong yo = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
        int misKills = yo != ulong.MaxValue ? gm.GetKills(yo) : 0;
        // Líder desde caché (se recalcula solo al cambiar scores, no cada frame).
        string texto = $"{modo}  |  {tiempo}  |  OBJ {objetivo}  |  LÍDER: {liderCache}  |  TÚ: {misKills}";
        if (texto != ultimoTopbarTexto) { topBarTexto.text = texto; ultimoTopbarTexto = texto; }
    }

    private void ActualizarTopBarTDM(GameModeManager gm, string modo)
    {
        string tiempo;
        if (gm.estado.Value == (int)GameModeManager.EstadoPartida.CuentaAtras)
            tiempo = "INICIANDO...";
        else if (gm.estado.Value == (int)GameModeManager.EstadoPartida.Finalizada)
            tiempo = $"FIN - GANA {Equipo.Nombre(gm.equipoGanador)}";
        else
        {
            float t = gm.tiempoPartida.Value;
            if (t <= 0f && !gm.IsSpawned) t = 0f;
            tiempo = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
        }
        var modoAct = gm.ModoActual();
        int objetivo = modoAct != null ? modoAct.scoreToWin : 50;
        int rojo = gm.GetPuntosEquipo(Equipo.Rojo);
        int azul = gm.GetPuntosEquipo(Equipo.Azul);
        string texto = $"{modo}  |  {tiempo}  |  OBJ {objetivo}  |  ROJO {rojo} - {azul} AZUL";
        if (texto != ultimoTopbarTexto) { topBarTexto.text = texto; ultimoTopbarTexto = texto; }
    }

    private void RefrescarScoreboard()
    {
        if (GameModeManager.Instance == null || scoreboardLista == null) return;

        var gm = GameModeManager.Instance;
        var tabla = gm.GetTablaFFA();
        // TDM: agrupa por equipo (Rojo primero) para leer 6v6 de un vistazo.
        if (gm.EsTDM())
            tabla.Sort((a, b) => gm.GetEquipoDe(b.clientId).CompareTo(gm.GetEquipoDe(a.clientId)));
        ulong yo = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;

        while (poolFilas.Count < tabla.Count)
            poolFilas.Add(CrearFilaVista(poolFilas.Count));

        for (int i = 0; i < poolFilas.Count; i++)
        {
            var vista = poolFilas[i];
            if (vista == null || vista.root == null) continue;
            if (i >= tabla.Count) { if (vista.root.activeSelf) vista.root.SetActive(false); continue; }
            var row = tabla[i];
            string nombre = NombreCache(row.clientId);
            if (gm.EsTDM())
            {
                int eq = gm.GetEquipoDe(row.clientId);
                if (Equipo.EsValido(eq)) nombre = $"[{Equipo.Nombre(eq)}] {nombre}";
            }
            bool soyYo = row.clientId == yo;
            bool esGanador = gm.EsTDM()
                ? gm.GetEquipoDe(row.clientId) == gm.equipoGanador && gm.equipoGanador != Equipo.Ninguno
                : row.clientId == gm.ganadorClientId.Value;
            ActualizarFilaVista(vista, i + 1, row.clientId, nombre, row.killCount, row.deathCount, soyYo, esGanador);
        }
    }

    private FilaVista CrearFilaVista(int indice)
    {
        var go = new GameObject($"Fila_{indice}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(scoreboardLista, false);
        var vista = new FilaVista { root = go, fondo = go.GetComponent<Image>() };
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 6, 6); hlg.spacing = 8f;

        vista.pos = CrearCelda(go, "", 50f, TextAlignmentOptions.Center, new Color(0.7f, 0.8f, 1f));
        vista.nombre = CrearCelda(go, "", 0f, TextAlignmentOptions.Left, Color.white);
        vista.nombre.GetComponent<LayoutElement>().flexibleWidth = 1f;
        vista.kills = CrearCelda(go, "", 70f, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));
        vista.muertes = CrearCelda(go, "", 70f, TextAlignmentOptions.Center, new Color(0.75f, 0.75f, 0.75f));
        // Proponer expulsión (TAB): en todas las filas, incluida la propia
        // (el servidor rechaza la auto-expulsión; el botón queda visible).
        var kickGo = new GameObject("Kick", typeof(RectTransform), typeof(Image), typeof(Button));
        kickGo.transform.SetParent(go.transform, false);
        var kRt = kickGo.GetComponent<RectTransform>(); kRt.sizeDelta = new Vector2(40, 0);
        vista.kickImg = kickGo.GetComponent<Image>();
        vista.kickBtn = kickGo.GetComponent<Button>();
        var kTxt = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        kTxt.transform.SetParent(kickGo.transform, false);
        var kxRt = kTxt.GetComponent<RectTransform>();
        kxRt.anchorMin = Vector2.zero; kxRt.anchorMax = Vector2.one;
        kxRt.offsetMin = Vector2.zero; kxRt.offsetMax = Vector2.zero;
        var kxTmp = kTxt.GetComponent<TextMeshProUGUI>();
        kxTmp.font = TMP_Settings.defaultFontAsset; kxTmp.fontSize = 18; kxTmp.fontStyle = FontStyles.Bold;
        kxTmp.alignment = TextAlignmentOptions.Center; kxTmp.color = Color.white; kxTmp.text = "X";
        kxTmp.raycastTarget = false;
        return vista;
    }

    private static void PonTexto(TextMeshProUGUI tmp, string valor)
    {
        if (tmp != null && tmp.text != valor) tmp.text = valor;
    }

    private void ActualizarFilaVista(FilaVista vista, int pos, ulong clientId, string nombre, int k, int d, bool soyYo, bool esGanador)
    {
        if (!vista.root.activeSelf) vista.root.SetActive(true);
        vista.root.name = $"Fila_{pos}";
        vista.fondo.color = esGanador ? new Color(0.2f, 0.6f, 0.3f, 0.9f)
            : soyYo ? new Color(0.25f, 0.5f, 0.9f, 0.9f)
            : new Color(0.14f, 0.18f, 0.25f, 0.9f);
        PonTexto(vista.pos, $"#{pos}");
        PonTexto(vista.nombre, nombre + (soyYo ? "  [TÚ]" : ""));
        PonTexto(vista.kills, $"{k}");
        PonTexto(vista.muertes, $"{d}");
        vista.kickImg.color = soyYo ? new Color(0.45f, 0.45f, 0.48f, 1f) : new Color(0.7f, 0.25f, 0.25f, 1f);
        if (vista.clientId != clientId)
        {
            vista.clientId = clientId;
            vista.kickBtn.onClick.RemoveAllListeners();
            ulong objetivo = clientId;
            vista.kickBtn.onClick.AddListener(() => VoteKickManager.Instance?.IniciarVotacion(objetivo));
        }
    }

    private TextMeshProUGUI CrearCelda(GameObject padre, string txt, float ancho, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject("Celda", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre.transform, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 16; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align; tmp.color = color; tmp.text = txt;
        var le = go.AddComponent<LayoutElement>();
        if (ancho > 0) le.preferredWidth = ancho;
        return tmp;
    }

    private void OnKill(ulong killer, ulong victima, int killsKiller)
    {
        if (GameModeManager.Instance == null) return;
        AddFeed($"{NombreCache(killer)} <color=#FF6B6B>✕</color> {NombreCache(victima)}");
    }

    private void AddFeed(string msg)
    {
        if (killfeedLista == null) return;
        while (feedPool.Count < 4)
        {
            var go = new GameObject("Feed", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(killfeedLista, false);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var tGo = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(go.transform, false);
            var rt = tGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 2); rt.offsetMax = new Vector2(-8, -2);
            var tmp = tGo.GetComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Left; tmp.color = Color.white;
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 26f;
            go.SetActive(false);
            feedPool.Add(go);
            feedTextos.Add(tmp);
            feedRutinas.Add(null);
            feedVersion.Add(0);
        }
        int slot = feedIndice++ % 4;
        var item = feedPool[slot];
        feedTextos[slot].text = msg;
        item.transform.SetAsFirstSibling();
        item.SetActive(true);
        feedVersion[slot]++;
        if (feedRutinas[slot] != null) StopCoroutine(feedRutinas[slot]);
        feedRutinas[slot] = StartCoroutine(OcultarFeed(slot, feedVersion[slot], 5f));
    }

    private IEnumerator OcultarFeed(int slot, int version, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (slot < 0 || slot >= feedPool.Count) yield break;
        if (feedVersion[slot] != version) yield break;
        if (feedPool[slot] != null) feedPool[slot].SetActive(false);
        feedRutinas[slot] = null;
    }

    private void ConstruirUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root = canvasGo;

        // Top bar
        var topGo = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        topGo.transform.SetParent(canvasGo.transform, false);
        var topRt = topGo.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0.5f, 1f); topRt.anchorMax = new Vector2(0.5f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = new Vector2(0, -10);
        topRt.sizeDelta = new Vector2(760, 38);
        var topImg = topGo.GetComponent<Image>();
        topImg.color = new Color(0.05f, 0.07f, 0.11f, 0.85f);
        topImg.raycastTarget = false;

        var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(topGo.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        topBarTexto = txtGo.GetComponent<TextMeshProUGUI>();
        topBarTexto.font = TMP_Settings.defaultFontAsset; topBarTexto.fontSize = 18; topBarTexto.fontStyle = FontStyles.Bold;
        topBarTexto.alignment = TextAlignmentOptions.Center; topBarTexto.color = Color.white;
        topBarTexto.raycastTarget = false;
        topBarTexto.text = "FFA | --:--";

        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(canvasGo.transform, false);
        var hRt = hintGo.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 1f); hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0, -52); hRt.sizeDelta = new Vector2(400, 22);
        var hint = hintGo.GetComponent<TextMeshProUGUI>();
        hint.font = TMP_Settings.defaultFontAsset; hint.fontSize = 13;
        hint.alignment = TextAlignmentOptions.Center; hint.color = new Color(1, 1, 1, 0.6f);
        hint.raycastTarget = false;
        hint.text = "Mantén TAB para ver puntuaciones";

        // Killfeed (derecha)
        var feedGo = new GameObject("Killfeed", typeof(RectTransform), typeof(VerticalLayoutGroup));
        feedGo.transform.SetParent(canvasGo.transform, false);
        var fRt = feedGo.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(1f, 1f); fRt.anchorMax = new Vector2(1f, 1f);
        fRt.pivot = new Vector2(1f, 1f);
        fRt.anchoredPosition = new Vector2(-16, -100);
        fRt.sizeDelta = new Vector2(300, 140);
        var fvlg = feedGo.GetComponent<VerticalLayoutGroup>();
        fvlg.spacing = 4f; fvlg.childControlHeight = false; fvlg.childForceExpandHeight = false;
        killfeedLista = feedGo.transform;

        // Scoreboard (TAB, oculto por defecto)
        var sbGo = new GameObject("Scoreboard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        sbGo.transform.SetParent(canvasGo.transform, false);
        var sbRt = sbGo.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.5f, 0.5f); sbRt.anchorMax = new Vector2(0.5f, 0.5f);
        sbRt.sizeDelta = new Vector2(560, 420); sbRt.anchoredPosition = Vector2.zero;
        var sbImg = sbGo.GetComponent<Image>();
        sbImg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        var sbVlg = sbGo.GetComponent<VerticalLayoutGroup>();
        sbVlg.padding = new RectOffset(16, 16, 16, 16); sbVlg.spacing = 8f;
        scoreboardPanel = sbGo;

        var titleGo = new GameObject("Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(sbGo.transform, false);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.font = TMP_Settings.defaultFontAsset; titleTmp.fontSize = 24; titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center; titleTmp.color = new Color(0.4f, 0.85f, 1f);
        titleTmp.text = "PUNTUACIÓN — FFA";

        var headGo = new GameObject("Cabecera", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        headGo.transform.SetParent(sbGo.transform, false);
        var hhlg = headGo.GetComponent<HorizontalLayoutGroup>();
        hhlg.spacing = 8f;
        CrearCabecera(headGo, "POS", 50f);
        var hc = CrearCabecera(headGo, "JUGADOR", 0f);
        hc.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CrearCabecera(headGo, "KILLS", 70f);
        CrearCabecera(headGo, "MUERTES", 70f);

        var listGo = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGo.transform.SetParent(sbGo.transform, false);
        var lvlg = listGo.GetComponent<VerticalLayoutGroup>();
        lvlg.spacing = 4f;
        var leList = listGo.AddComponent<LayoutElement>(); leList.flexibleHeight = 1f;
        scoreboardLista = listGo.transform;

        sbGo.SetActive(false);
    }

    private TextMeshProUGUI CrearCabecera(GameObject padre, string txt, float ancho)
    {
        var go = new GameObject("C", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre.transform, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = 13; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.6f, 0.7f, 0.85f);
        tmp.text = txt;
        var le = go.AddComponent<LayoutElement>();
        if (ancho > 0) le.preferredWidth = ancho;
        return tmp;
    }
}
