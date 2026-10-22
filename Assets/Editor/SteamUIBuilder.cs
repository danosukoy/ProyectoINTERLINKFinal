using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class SteamUIBuilder
{
    [MenuItem("Tools/Build Steam Main Menu UI")]
    public static void BuildUI()
    {
        // 1. Ensure EventSystem exists in Scene for Mouse Click Registration
        var eventSystemGo = GameObject.Find("EventSystem");
        if (eventSystemGo == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // 2. Ensure Canvas exists
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = canvasGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
        }

        // Clean up old canvas contents for a fresh, perfectly aligned layout
        for (int i = canvasGo.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvasGo.transform.GetChild(i).gameObject;
            if (child.name.StartsWith("Steam_"))
            {
                Object.DestroyImmediate(child);
            }
        }

        // 3. Background Panel
        GameObject bgObj = CreateUIElement("Steam_Background", canvasGo.transform);
        SetFullAnchor(bgObj);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);

        // -------------------------------------------------------------
        // PANEL 1: MAIN MENU PANEL (Steam_MainMenuPanel)
        // -------------------------------------------------------------
        GameObject mainPanel = CreateUIElement("Steam_MainMenuPanel", bgObj.transform);
        SetFullAnchor(mainPanel);

        // Title Header Container
        GameObject headerContainer = CreateUIElement("HeaderContainer", mainPanel.transform);
        SetStretchAnchor(headerContainer, new Vector2(0.1f, 0.70f), new Vector2(0.9f, 0.95f));

        GameObject titleObj = CreateUIElement("Title", headerContainer.transform);
        SetStretchAnchor(titleObj, new Vector2(0, 0.45f), new Vector2(1, 1f));
        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "PROYECTO INTERLINK";
        titleTmp.fontSize = 56;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.92f, 0.96f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Center;

        GameObject subTitleObj = CreateUIElement("SubTitle", headerContainer.transform);
        SetStretchAnchor(subTitleObj, new Vector2(0, 0f), new Vector2(1, 0.45f));
        TextMeshProUGUI subTitleTmp = subTitleObj.AddComponent<TextMeshProUGUI>();
        subTitleTmp.text = "MENÚ PRINCIPAL MULTIJUGADOR (STEAM + NETCODE)";
        subTitleTmp.fontSize = 20;
        subTitleTmp.color = new Color(0.35f, 0.75f, 1f);
        subTitleTmp.alignment = TextAlignmentOptions.Center;

        // Session Indicator Status Box (Main Menu)
        GameObject sessionBox = CreateUIElement("Steam_SessionBox", mainPanel.transform);
        SetStretchAnchor(sessionBox, new Vector2(0.20f, 0.56f), new Vector2(0.80f, 0.66f));
        Image sessionBg = sessionBox.AddComponent<Image>();
        sessionBg.color = new Color(0.09f, 0.14f, 0.22f, 0.95f);

        Outline sessionOutline = sessionBox.AddComponent<Outline>();
        sessionOutline.effectColor = new Color(0.15f, 0.55f, 0.85f, 0.6f);
        sessionOutline.effectDistance = new Vector2(2, -2);

        GameObject sessionTextObj = CreateUIElement("SessionText", sessionBox.transform);
        SetFullAnchor(sessionTextObj);
        TextMeshProUGUI sessionTmp = sessionTextObj.AddComponent<TextMeshProUGUI>();
        sessionTmp.text = "<color=#00FF88>● SESIÓN INICIADA:</color> Verificando conexión a Steam...";
        sessionTmp.fontSize = 18;
        sessionTmp.color = Color.white;
        sessionTmp.alignment = TextAlignmentOptions.Center;

        // Main Menu Buttons Container
        GameObject menuBtnsObj = CreateUIElement("MenuButtonsContainer", mainPanel.transform);
        SetStretchAnchor(menuBtnsObj, new Vector2(0.32f, 0.12f), new Vector2(0.68f, 0.48f));
        VerticalLayoutGroup menuLayout = menuBtnsObj.AddComponent<VerticalLayoutGroup>();
        menuLayout.spacing = 20;
        menuLayout.childControlWidth = true;
        menuLayout.childControlHeight = false;

        Button enterLobbiesBtn = CreateButton("EnterLobbiesButton", menuBtnsObj.transform, "MULTIJUGADOR / SALAS", new Color(0.12f, 0.58f, 0.95f), 60);
        Button quitGameBtn = CreateButton("QuitButton", menuBtnsObj.transform, "SALIR DEL JUEGO", new Color(0.28f, 0.30f, 0.38f), 60);

        // -------------------------------------------------------------
        // PANEL 2: LOBBY LIST PANEL (Steam_LobbyListPanel)
        // -------------------------------------------------------------
        GameObject lobbyPanelObj = CreateUIElement("Steam_LobbyListPanel", bgObj.transform);
        SetFullAnchor(lobbyPanelObj);
        lobbyPanelObj.SetActive(false);

        // Top Navigation Bar
        GameObject topBarObj = CreateUIElement("TopBar", lobbyPanelObj.transform);
        SetStretchAnchor(topBarObj, new Vector2(0.04f, 0.90f), new Vector2(0.96f, 0.98f));

        Button backToMenuBtn = CreateButton("BackToMenuButton", topBarObj.transform, "< VOLVER AL MENÚ", new Color(0.20f, 0.25f, 0.35f), 50);
        SetStretchAnchor(backToMenuBtn.gameObject, new Vector2(0f, 0f), new Vector2(0.22f, 1f));

        GameObject lobbyStatusTextObj = CreateUIElement("LobbyStatusText", topBarObj.transform);
        SetStretchAnchor(lobbyStatusTextObj, new Vector2(0.25f, 0f), new Vector2(1f, 1f));
        TextMeshProUGUI statusTmp = lobbyStatusTextObj.AddComponent<TextMeshProUGUI>();
        statusTmp.text = "Conectando con Steam...";
        statusTmp.fontSize = 18;
        statusTmp.color = new Color(0.8f, 0.9f, 1f);
        statusTmp.alignment = TextAlignmentOptions.Right;

        // Content Area Container
        GameObject contentAreaObj = CreateUIElement("ContentArea", lobbyPanelObj.transform);
        SetStretchAnchor(contentAreaObj, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.87f));

        // Left Sidebar
        GameObject sidebarObj = CreateUIElement("Steam_Sidebar", contentAreaObj.transform);
        SetStretchAnchor(sidebarObj, new Vector2(0f, 0f), new Vector2(0.26f, 1f));
        Image sidebarImg = sidebarObj.AddComponent<Image>();
        sidebarImg.color = new Color(0.09f, 0.13f, 0.20f, 0.95f);

        Outline sidebarOutline = sidebarObj.AddComponent<Outline>();
        sidebarOutline.effectColor = new Color(0.15f, 0.40f, 0.65f, 0.4f);
        sidebarOutline.effectDistance = new Vector2(2, -2);

        VerticalLayoutGroup sidebarLayout = sidebarObj.AddComponent<VerticalLayoutGroup>();
        sidebarLayout.padding = new RectOffset(20, 20, 25, 25);
        sidebarLayout.spacing = 15;
        sidebarLayout.childControlWidth = true;
        sidebarLayout.childControlHeight = false;

        Button createBtn = CreateButton("Steam_CreateLobbyButton", sidebarObj.transform, "+ CREAR SALA", new Color(0.12f, 0.55f, 0.90f), 55);
        Button refreshBtn = CreateButton("Steam_RefreshLobbyButton", sidebarObj.transform, "REFRESCAR SALAS", new Color(0.15f, 0.35f, 0.60f), 55);
        Button leaveBtn = CreateButton("Steam_LeaveLobbyButton", sidebarObj.transform, "ABANDONAR SALA", new Color(0.85f, 0.30f, 0.25f), 55);

        // Right Lobby List Container Panel
        GameObject listCardObj = CreateUIElement("Steam_LobbyCardPanel", contentAreaObj.transform);
        SetStretchAnchor(listCardObj, new Vector2(0.29f, 0f), new Vector2(1f, 1f));
        Image listCardImg = listCardObj.AddComponent<Image>();
        listCardImg.color = new Color(0.08f, 0.11f, 0.17f, 0.95f);

        Outline listCardOutline = listCardObj.AddComponent<Outline>();
        listCardOutline.effectColor = new Color(0.15f, 0.40f, 0.65f, 0.4f);
        listCardOutline.effectDistance = new Vector2(2, -2);

        GameObject lobbyHeaderObj = CreateUIElement("LobbyHeader", listCardObj.transform);
        SetStretchAnchor(lobbyHeaderObj, new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f));
        TextMeshProUGUI lobbyHeaderTmp = lobbyHeaderObj.AddComponent<TextMeshProUGUI>();
        lobbyHeaderTmp.text = "SALAS PÚBLICAS DISPONIBLES EN STEAM";
        lobbyHeaderTmp.fontSize = 20;
        lobbyHeaderTmp.fontStyle = FontStyles.Bold;
        lobbyHeaderTmp.color = new Color(0.85f, 0.92f, 1f);
        lobbyHeaderTmp.alignment = TextAlignmentOptions.Left;

        // Scroll View Container
        GameObject scrollViewObj = CreateUIElement("Steam_ScrollView", listCardObj.transform);
        SetStretchAnchor(scrollViewObj, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.88f));
        Image scrollImg = scrollViewObj.AddComponent<Image>();
        scrollImg.color = new Color(0.04f, 0.06f, 0.10f, 0.65f);

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewportObj = CreateUIElement("Viewport", scrollViewObj.transform);
        SetFullAnchor(viewportObj);
        viewportObj.AddComponent<Image>();
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewportObj.GetComponent<RectTransform>();

        GameObject contentObj = CreateUIElement("Content", viewportObj.transform);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(12, 12, 12, 12);
        contentLayout.spacing = 10;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        // -------------------------------------------------------------
        // MODAL: CREATE LOBBY POPUP (Steam_CreateModal with Stepper [-] [+])
        // -------------------------------------------------------------
        GameObject modalObj = CreateUIElement("Steam_CreateModal", bgObj.transform);
        SetFullAnchor(modalObj);
        Image modalBg = modalObj.AddComponent<Image>();
        modalBg.color = new Color(0, 0, 0, 0.82f);
        modalObj.SetActive(false);

        GameObject dialogObj = CreateUIElement("Dialog", modalObj.transform);
        SetStretchAnchor(dialogObj, new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f));
        Image dialogImg = dialogObj.AddComponent<Image>();
        dialogImg.color = new Color(0.12f, 0.16f, 0.24f, 1f);

        Outline dialogOutline = dialogObj.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.15f, 0.55f, 0.85f, 0.6f);
        dialogOutline.effectDistance = new Vector2(3, -3);

        GameObject modalTitle = CreateUIElement("ModalTitle", dialogObj.transform);
        SetStretchAnchor(modalTitle, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));
        TextMeshProUGUI modalTitleTmp = modalTitle.AddComponent<TextMeshProUGUI>();
        modalTitleTmp.text = "CREAR NUEVA SALA EN STEAM";
        modalTitleTmp.fontSize = 24;
        modalTitleTmp.fontStyle = FontStyles.Bold;
        modalTitleTmp.color = Color.white;
        modalTitleTmp.alignment = TextAlignmentOptions.Center;

        // Lobby Name Input Field
        GameObject nameInputObj = CreateInputField("LobbyNameInput", dialogObj.transform, "Nombre de la partida...", new Vector2(0, 50));
        SetStretchAnchor(nameInputObj, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.76f));

        // Max Players Stepper Container ([-] 16 Jugadores [+])
        GameObject stepperContainer = CreateUIElement("MaxPlayersStepperContainer", dialogObj.transform);
        SetStretchAnchor(stepperContainer, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.50f));
        Image stepperBg = stepperContainer.AddComponent<Image>();
        stepperBg.color = new Color(0.06f, 0.08f, 0.12f, 1f);

        HorizontalLayoutGroup stepperLayout = stepperContainer.AddComponent<HorizontalLayoutGroup>();
        stepperLayout.padding = new RectOffset(8, 8, 5, 5);
        stepperLayout.spacing = 15;
        stepperLayout.childControlWidth = true;
        stepperLayout.childControlHeight = true;

        Button decBtn = CreateButton("DecreaseMaxPlayersButton", stepperContainer.transform, "-", new Color(0.20f, 0.28f, 0.40f), 45);
        LayoutElement decElem = decBtn.GetComponent<LayoutElement>();
        decElem.preferredWidth = 65;

        GameObject valObj = CreateUIElement("MaxPlayersValueText", stepperContainer.transform);
        LayoutElement valElem = valObj.AddComponent<LayoutElement>();
        valElem.flexibleWidth = 1f;
        TextMeshProUGUI valTmp = valObj.AddComponent<TextMeshProUGUI>();
        valTmp.text = "16 Jugadores";
        valTmp.fontSize = 20;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.color = new Color(0.85f, 0.95f, 1f);
        valTmp.alignment = TextAlignmentOptions.Center;

        Button incBtn = CreateButton("IncreaseMaxPlayersButton", stepperContainer.transform, "+", new Color(0.20f, 0.28f, 0.40f), 45);
        LayoutElement incElem = incBtn.GetComponent<LayoutElement>();
        incElem.preferredWidth = 65;

        // Modal Action Buttons (Confirm & Cancel)
        Button confirmModalBtn = CreateButton("ConfirmButton", dialogObj.transform, "CONFIRMAR Y CREAR", new Color(0.12f, 0.65f, 0.35f), 50);
        SetStretchAnchor(confirmModalBtn.gameObject, new Vector2(0.06f, 0.06f), new Vector2(0.48f, 0.22f));

        Button cancelModalBtn = CreateButton("CancelButton", dialogObj.transform, "CANCELAR", new Color(0.75f, 0.25f, 0.25f), 50);
        SetStretchAnchor(cancelModalBtn.gameObject, new Vector2(0.52f, 0.06f), new Vector2(0.94f, 0.22f));

        // -------------------------------------------------------------
        // BIND REFERENCES TO SteamLobbyListUI
        // -------------------------------------------------------------
        SteamLobbyListUI listUI = canvasGo.GetComponent<SteamLobbyListUI>();
        if (listUI == null) listUI = canvasGo.AddComponent<SteamLobbyListUI>();

        SetPrivateField(listUI, "mainMenuPanel", mainPanel);
        SetPrivateField(listUI, "lobbyListPanel", lobbyPanelObj);

        SetPrivateField(listUI, "sessionStatusText", sessionTmp);
        SetPrivateField(listUI, "sessionStatusIndicator", sessionBg);
        SetPrivateField(listUI, "enterLobbiesButton", enterLobbiesBtn);
        SetPrivateField(listUI, "quitGameButton", quitGameBtn);
        SetPrivateField(listUI, "backToMenuButton", backToMenuBtn);

        SetPrivateField(listUI, "lobbyListContainer", contentObj.transform);
        SetPrivateField(listUI, "refreshButton", refreshBtn);
        SetPrivateField(listUI, "createButton", createBtn);
        SetPrivateField(listUI, "leaveLobbyButton", leaveBtn);
        SetPrivateField(listUI, "statusText", statusTmp);
        SetPrivateField(listUI, "createLobbyPanel", modalObj);
        SetPrivateField(listUI, "lobbyNameInput", nameInputObj.GetComponent<TMP_InputField>());

        SetPrivateField(listUI, "maxPlayersValueText", valTmp);
        SetPrivateField(listUI, "decreaseMaxPlayersButton", decBtn);
        SetPrivateField(listUI, "increaseMaxPlayersButton", incBtn);
        SetPrivateField(listUI, "confirmCreateButton", confirmModalBtn);
        SetPrivateField(listUI, "cancelCreateButton", cancelModalBtn);

        // Ensure SteamManagers persistent GameObject exists with NetworkManager attached
        var mgrObj = GameObject.Find("SteamManagers");
        if (mgrObj == null) mgrObj = new GameObject("SteamManagers");
        if (mgrObj.GetComponent<SteamManager>() == null) mgrObj.AddComponent<SteamManager>();
        if (mgrObj.GetComponent<SteamLobbyManager>() == null) mgrObj.AddComponent<SteamLobbyManager>();

        if (mgrObj.GetComponent<NetworkManager>() == null)
        {
            var utp = mgrObj.GetComponent<UnityTransport>();
            if (utp == null) utp = mgrObj.AddComponent<UnityTransport>();

            var nm = mgrObj.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;

            GameObject playerPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            if (playerPrefabAsset != null)
            {
                nm.NetworkConfig.PlayerPrefab = playerPrefabAsset;
            }
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SteamUIBuilder] ¡NetworkManager y SteamLobbyManager integrados en SteamManagers persistente!");
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetFullAnchor(GameObject go)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetStretchAnchor(GameObject go, Vector2 min, Vector2 max)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Button CreateButton(string name, Transform parent, string text, Color color, float height)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        LayoutElement elem = btnObj.AddComponent<LayoutElement>();
        elem.preferredHeight = height;
        elem.minHeight = height - 10;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        Button btn = btnObj.AddComponent<Button>();

        GameObject textObj = CreateUIElement("Text", btnObj.transform);
        SetFullAnchor(textObj);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private static GameObject CreateInputField(string name, Transform parent, string placeholderText, Vector2 pos)
    {
        GameObject inputObj = CreateUIElement(name, parent);
        Image img = inputObj.AddComponent<Image>();
        img.color = new Color(0.06f, 0.08f, 0.12f, 1f);

        TMP_InputField input = inputObj.AddComponent<TMP_InputField>();

        GameObject textObj = CreateUIElement("Text", inputObj.transform);
        SetFullAnchor(textObj);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.offsetMin = new Vector2(15, 5);
        textRect.offsetMax = new Vector2(-15, -5);
        TextMeshProUGUI textTmp = textObj.AddComponent<TextMeshProUGUI>();
        textTmp.fontSize = 18;
        textTmp.color = Color.white;

        GameObject placeholderObj = CreateUIElement("Placeholder", inputObj.transform);
        SetFullAnchor(placeholderObj);
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.offsetMin = new Vector2(15, 5);
        placeholderRect.offsetMax = new Vector2(-15, -5);
        TextMeshProUGUI placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderTmp.text = placeholderText;
        placeholderTmp.fontSize = 18;
        placeholderTmp.color = new Color(0.5f, 0.6f, 0.7f, 0.6f);

        input.textComponent = textTmp;
        input.placeholder = placeholderTmp;

        return inputObj;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (field != null) field.SetValue(target, value);
    }
}
