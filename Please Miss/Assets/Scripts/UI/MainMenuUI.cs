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

    private Color32 selectedColor;
    private NetworkConnectionManager connectionManager;

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

        InitializeFields();
        BindButtons();
        RefreshColor();
        RefreshFooter();
        SetStatus(connectionManager.Status);
    }

    private void OnDestroy()
    {
        if (connectionManager != null)
            connectionManager.StatusChanged -= OnStatusChanged;
    }

    private void InitializeFields()
    {
        string profileId = GetProfileId();

        LocalPlayerSettings.Load(profileId);
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

    private void OnStatusChanged(string newStatus)
    {
        SetStatus(newStatus);
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
