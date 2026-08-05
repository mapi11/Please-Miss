using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private TMP_InputField profileInput;

    [Header("Player")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Image colorPreview;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Dropdown colorDropdown;

    [Header("Local")]
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField portInput;
    [SerializeField] private Button startLocalHostButton;
    [SerializeField] private Button startLocalClientButton;

    [Header("Online")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button startOnlineHostButton;
    [SerializeField] private Button joinOnlineButton;
    [SerializeField] private Button pasteCodeButton;
    [SerializeField] private Button copyCodeButton;

    [Header("Test Profiles")]
    [SerializeField] private Button testProfile1Button;
    [SerializeField] private Button testProfile2Button;
    [SerializeField] private Button testProfile3Button;
    [SerializeField] private Button testProfile4Button;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text footerText;

    [Header("Points")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private Button addPointsButton;
    [SerializeField] private Button removePointsButton;

    [Header("Inventory")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private RectTransform inventoryContent;
    [SerializeField] private GameObject inventoryPanelPrefab;

    [Header("Shop")]
    [SerializeField] private Button shopButton;
    [SerializeField] private RectTransform shopContent;
    [SerializeField] private GameObject shopPanelPrefab;

    private Color32 selectedColor;
    private NetworkConnectionManager connectionManager;
    private GameObject spawnedInventoryPanel;
    private GameObject spawnedShopPanel;

    private void Awake()
    {
        connectionManager = NetworkConnectionManager.Instance;

        if (connectionManager == null)
            connectionManager = FindFirstObjectByType<NetworkConnectionManager>();
    }

    private void Start()
    {
        if (connectionManager == null)
        {
            SetStatus("NetworkConnectionManager not found");
            return;
        }

        connectionManager.StatusChanged += OnStatusChanged;
        LocalPlayerSettings.PointsChanged += OnPointsChanged;

        InitializeFields();
        EnsureTestButtons();
        BindButtons();
        EnsureShopCatalogRegistered();
        InventoryMenuUI.WarmUp(inventoryPanelPrefab);
        RefreshColor();
        RefreshFooter();
        RefreshPoints();
        SetStatus(connectionManager.Status);
    }

    private void EnsureShopCatalogRegistered()
    {
        if (shopPanelPrefab == null || shopContent == null)
            return;

        if (spawnedShopPanel == null)
        {
            spawnedShopPanel = Instantiate(shopPanelPrefab, shopContent);
            spawnedShopPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (connectionManager != null)
            connectionManager.StatusChanged -= OnStatusChanged;

        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void InitializeFields()
    {
        string profileId = GetProfileId();

        LocalPlayerSettings.Load(profileId);
        LocalPlayerSettings.EnsureSession();
        selectedColor = LocalPlayerSettings.PlayerColor;

        int savedIndex = PlayerPrefs.GetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", -1);

        if (savedIndex >= 0 && savedIndex < GameSessionData.ColorValues.Length &&
            ColorsMatch(GameSessionData.ColorValues[savedIndex], selectedColor))
        {
            GameSessionData.SelectedColorIndex = savedIndex;
        }
        else
        {
            GameSessionData.SelectedColorIndex = FindColorIndex(selectedColor);
        }

        if (playerNameInput != null)
            playerNameInput.text = LocalPlayerSettings.PlayerName;

        if (addressInput != null)
            addressInput.text = "127.0.0.1";

        if (portInput != null)
            portInput.text = "7777";

        InitColorDropdown();
    }

    private void InitColorDropdown()
    {
        if (colorDropdown == null)
            return;

        colorDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < GameSessionData.ColorNames.Length; i++)
            options.Add(new TMP_Dropdown.OptionData(GameSessionData.ColorNames[i]));

        colorDropdown.AddOptions(options);
        colorDropdown.SetValueWithoutNotify(GameSessionData.SelectedColorIndex);
        colorDropdown.onValueChanged.RemoveAllListeners();
        colorDropdown.onValueChanged.AddListener(OnColorChanged);
    }

    private void OnColorChanged(int index)
    {
        GameSessionData.SelectedColorIndex = index;
        selectedColor = GameSessionData.ColorValues[index];
        LocalPlayerSettings.Load(GetProfileId());
        LocalPlayerSettings.SetPlayerColor(selectedColor);
        PlayerPrefs.SetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", index);
        PlayerPrefs.Save();
        RefreshColor();
    }

    private void BindButtons()
    {
        if (startLocalHostButton != null)
            startLocalHostButton.onClick.AddListener(OnStartLocalHostClicked);

        if (startLocalClientButton != null)
            startLocalClientButton.onClick.AddListener(OnStartLocalClientClicked);

        if (startOnlineHostButton != null)
            startOnlineHostButton.onClick.AddListener(OnStartOnlineHostClicked);

        if (joinOnlineButton != null)
            joinOnlineButton.onClick.AddListener(OnJoinOnlineClicked);

        if (pasteCodeButton != null)
            pasteCodeButton.onClick.AddListener(OnPasteCodeClicked);

        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);

        if (testProfile1Button != null)
            testProfile1Button.onClick.AddListener(() => ApplyTestProfile(1));

        if (testProfile2Button != null)
            testProfile2Button.onClick.AddListener(() => ApplyTestProfile(2));

        if (testProfile3Button != null)
            testProfile3Button.onClick.AddListener(() => ApplyTestProfile(3));

        if (testProfile4Button != null)
            testProfile4Button.onClick.AddListener(() => ApplyTestProfile(4));

        if (addPointsButton != null)
            addPointsButton.onClick.AddListener(OnAddPointsClicked);

        if (removePointsButton != null)
            removePointsButton.onClick.AddListener(OnRemovePointsClicked);

        if (inventoryButton != null)
            inventoryButton.onClick.AddListener(OnInventoryButtonClicked);

        if (shopButton != null)
            shopButton.onClick.AddListener(OnShopButtonClicked);
    }

    private void OnInventoryButtonClicked()
    {
        if (inventoryPanelPrefab == null || inventoryContent == null)
            return;

        if (spawnedInventoryPanel != null)
            Destroy(spawnedInventoryPanel);

        spawnedInventoryPanel = Instantiate(inventoryPanelPrefab, inventoryContent);
    }

    private void OnShopButtonClicked()
    {
        if (shopPanelPrefab == null || shopContent == null)
            return;

        if (spawnedShopPanel == null)
        {
            spawnedShopPanel = Instantiate(shopPanelPrefab, shopContent);
        }
        else
        {
            bool show = !spawnedShopPanel.activeSelf;
            spawnedShopPanel.SetActive(show);
        }
    }

    private void OnAddPointsClicked()
    {
        LocalPlayerSettings.SetPoints(LocalPlayerSettings.PlayerPoints + 100);
        RefreshPoints();
    }

    private void OnRemovePointsClicked()
    {
        LocalPlayerSettings.SetPoints(LocalPlayerSettings.PlayerPoints - 100);
        RefreshPoints();
    }

    private void EnsureTestButtons()
    {
        EnsurePointButton(ref addPointsButton, "+100", 1);
        EnsurePointButton(ref removePointsButton, "-100", 2);
    }

    private void EnsurePointButton(ref Button button, string label, int column)
    {
        if (button != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        GameObject go = new GameObject($"TestPoints_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f - (column - 1) * 120f, -70f);
        rect.sizeDelta = new Vector2(110f, 40f);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
    }

    private void ApplyTestProfile(int index)
    {
        if (index < 1 || index > GameSessionData.ColorValues.Length)
            return;

        int colorIndex = index - 1;

        if (playerNameInput != null)
            playerNameInput.text = $"Player_{index}";

        GameSessionData.SelectedColorIndex = colorIndex;
        selectedColor = GameSessionData.ColorValues[colorIndex];
        LocalPlayerSettings.Load(GetProfileId());
        LocalPlayerSettings.SetPlayerColor(selectedColor);
        PlayerPrefs.SetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", colorIndex);
        PlayerPrefs.Save();

        if (colorDropdown != null)
            colorDropdown.SetValueWithoutNotify(colorIndex);

        RefreshColor();
    }

    private void OnStartLocalHostClicked()
    {
        ApplyLocalConnectionFields();
        connectionManager.StartLocalHost(GetProfileId(), GetPlayerName(), selectedColor);
    }

    private void OnStartLocalClientClicked()
    {
        ApplyLocalConnectionFields();
        connectionManager.StartLocalClient(GetProfileId(), GetPlayerName(), selectedColor);
    }

    private async void OnStartOnlineHostClicked()
    {
        await connectionManager.StartOnlineHostAsync(GetProfileId(), GetPlayerName(), selectedColor);
    }

    private async void OnJoinOnlineClicked()
    {
        string joinCode = joinCodeInput != null ? joinCodeInput.text : "";
        await connectionManager.JoinOnlineAsync(GetProfileId(), GetPlayerName(), joinCode, selectedColor);
    }

    private void OnPasteCodeClicked()
    {
        if (joinCodeInput == null)
            return;

        joinCodeInput.text = GUIUtility.systemCopyBuffer.Trim().ToUpperInvariant();
    }

    private void OnCopyCodeClicked()
    {
        if (connectionManager == null)
            return;

        string code = connectionManager.CurrentJoinCode;

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("No join code available");
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        SetStatus($"Code copied: {code}");
    }

    private void ApplyLocalConnectionFields()
    {
        string address = "127.0.0.1";
        ushort port = 7777;

        if (addressInput != null && !string.IsNullOrWhiteSpace(addressInput.text))
            address = addressInput.text.Trim();

        if (portInput != null && ushort.TryParse(portInput.text, out ushort parsedPort))
            port = parsedPort;

        connectionManager.SetLocalConnectionData(address, port);
    }

    private string GetProfileId()
    {
        if (profileInput == null || string.IsNullOrWhiteSpace(profileInput.text))
            return "Default";

        return profileInput.text.Trim();
    }

    private string GetPlayerName()
    {
        if (playerNameInput == null || string.IsNullOrWhiteSpace(playerNameInput.text))
            return "Player";

        return playerNameInput.text.Trim();
    }

    private void RefreshColor()
    {
        if (colorPreview != null)
            colorPreview.color = selectedColor;

        if (colorText != null)
            colorText.text = $"R:{selectedColor.r} G:{selectedColor.g} B:{selectedColor.b}";
    }

    private void RefreshFooter()
    {
        if (footerText != null)
            footerText.text = $"{GameSessionData.GameVersion}";
    }

    private void RefreshPoints()
    {
        if (pointsText == null)
            pointsText = CreatePointsText();

        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
    }

    private TMP_Text CreatePointsText()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        GameObject go = new GameObject("PointsText", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(300f, 40f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Right;
        text.fontSize = 28f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private static TMP_FontAsset FindAnyFontAsset()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.font != null)
                return text.font;
        }

        return null;
    }

    private void OnStatusChanged(string newStatus)
    {
        SetStatus(newStatus);
    }

    private void OnPointsChanged(int newPoints)
    {
        RefreshPoints();
    }

    private void SetStatus(string value)
    {
        if (statusText == null) return;
        statusText.text = value ?? "";
    }

    private static bool ColorsMatch(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private static int FindColorIndex(Color32 color)
    {
        for (int i = 0; i < GameSessionData.ColorValues.Length; i++)
        {
            if (ColorsMatch(GameSessionData.ColorValues[i], color))
                return i;
        }
        return 0;
    }
}
