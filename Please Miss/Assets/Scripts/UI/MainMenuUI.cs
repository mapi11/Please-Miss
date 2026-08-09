using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Image colorPreview;
    [Tooltip("Кнопка выбора предыдущего цвета")]
    [SerializeField] private Button prevColorButton;
    [Tooltip("Кнопка выбора следующего цвета")]
    [SerializeField] private Button nextColorButton;
    [Tooltip("Родитель кружков цветов (всегда отображается 6 кружков-окно со сдвигом)")]
    [SerializeField] private RectTransform colorCirclesRoot;
    [Tooltip("Префаб кружка цвета: Image (заливается цветом) + дочерний объект \"SelectedContainer\" (галочка выбранного цвета, выключен в префабе)")]
    [SerializeField] private GameObject colorCirclePrefab;

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

    [Header("Exit")]
    [Tooltip("Кнопка выхода из игры (закрывает приложение)")]
    [SerializeField] private Button exitGameButton;

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
    private readonly List<GameObject> colorCircles = new List<GameObject>();
    private int lastSelectedIndex = -1;

    private const int VisibleColorCount = 6;

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
        LocalPlayerSettings.Load("Default");
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
    }

    private void OnPrevColorClicked()
    {
        SetColorIndex(WrapColorIndex(GameSessionData.SelectedColorIndex - 1));
    }

    private void OnNextColorClicked()
    {
        SetColorIndex(WrapColorIndex(GameSessionData.SelectedColorIndex + 1));
    }

    private int WrapColorIndex(int index)
    {
        int count = GameSessionData.ColorValues.Length;

        if (count <= 0)
            return 0;

        return ((index % count) + count) % count;
    }

    private void SetColorIndex(int index)
    {
        GameSessionData.SelectedColorIndex = index;
        selectedColor = GameSessionData.ColorValues[index];
        LocalPlayerSettings.Load("Default");
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

        if (exitGameButton != null)
            exitGameButton.onClick.AddListener(OnExitGameClicked);

        if (prevColorButton != null)
            prevColorButton.onClick.AddListener(OnPrevColorClicked);

        if (nextColorButton != null)
            nextColorButton.onClick.AddListener(OnNextColorClicked);

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

    private void ApplyTestProfile(int index)
    {
        if (index < 1 || index > GameSessionData.ColorValues.Length)
            return;

        int colorIndex = index - 1;

        if (playerNameInput != null)
            playerNameInput.text = $"Player_{index}";

        GameSessionData.SelectedColorIndex = colorIndex;
        selectedColor = GameSessionData.ColorValues[colorIndex];
        LocalPlayerSettings.Load("Default");
        LocalPlayerSettings.SetPlayerColor(selectedColor);
        PlayerPrefs.SetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", colorIndex);
        PlayerPrefs.Save();

        RefreshColor();
    }

    private void OnStartLocalHostClicked()
    {
        ApplyLocalConnectionFields();
        connectionManager.StartLocalHost("Default", GetPlayerName(), selectedColor);
    }

    private void OnStartLocalClientClicked()
    {
        ApplyLocalConnectionFields();
        connectionManager.StartLocalClient("Default", GetPlayerName(), selectedColor);
    }

    private async void OnStartOnlineHostClicked()
    {
        await connectionManager.StartOnlineHostAsync("Default", GetPlayerName(), selectedColor);
    }

    private async void OnJoinOnlineClicked()
    {
        string joinCode = joinCodeInput != null ? joinCodeInput.text : "";
        await connectionManager.JoinOnlineAsync("Default", GetPlayerName(), joinCode, selectedColor);
    }

    private void OnPasteCodeClicked()
    {
        if (joinCodeInput == null)
            return;

        joinCodeInput.text = GUIUtility.systemCopyBuffer.Trim().ToUpperInvariant();
    }

    private void OnExitGameClicked()
    {
        Application.Quit();
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

        RefreshColorCircles();
    }

    /// <summary>
    /// Лента из 6 кружков-окна: показывает 6 цветов из списка, сдвигаясь так,
    /// чтобы выбранный цвет всегда был в кадре. На кружке выбранного цвета включается SelectedContainer с галочкой.
    /// </summary>
    private void RefreshColorCircles()
    {
        EnsureColorCircles();

        int total = GameSessionData.ColorValues.Length;
        int current = Mathf.Clamp(GameSessionData.SelectedColorIndex, 0, Mathf.Max(0, total - 1));
        int windowStart = Mathf.Clamp(current - 2, 0, Mathf.Max(0, total - VisibleColorCount));
        int currentCircleIndex = -1;

        for (int i = 0; i < colorCircles.Count; i++)
        {
            GameObject circle = colorCircles[i];
            int actualIndex = windowStart + i;

            if (actualIndex >= total)
            {
                circle.SetActive(false);
                continue;
            }

            circle.SetActive(true);

            Image img = circle.GetComponent<Image>();
            if (img != null)
                img.color = GameSessionData.ColorValues[actualIndex];

            SetCheckActive(circle, actualIndex == current);

            if (actualIndex == current)
                currentCircleIndex = i;
        }

        if (currentCircleIndex >= 0 && lastSelectedIndex != -1 && lastSelectedIndex != current)
            PlaySelectionAnimation(currentCircleIndex, current > lastSelectedIndex ? 1 : -1);

        lastSelectedIndex = current;
    }

    private void PlaySelectionAnimation(int selectedCircleIndex, int direction)
    {
        PlaySlotSlide(direction);
        PlaySpin360(selectedCircleIndex);
    }

    /// <summary>Слот-машина: вся лента проскакивает в сторону нового цвета и возвращается.</summary>
    private void PlaySlotSlide(int direction)
    {
        if (colorCirclesRoot == null || direction == 0)
            return;

        float spacing = 40f;

        if (colorCircles.Count >= 2 && colorCircles[0].activeSelf && colorCircles[1].activeSelf)
        {
            float a = colorCircles[0].transform.position.x;
            float b = colorCircles[1].transform.position.x;
            spacing = Mathf.Max(1f, Mathf.Abs(a - b));
        }

        RectTransform root = colorCirclesRoot;
        Vector2 basePos = root.anchoredPosition;

        root.DOKill();
        root.anchoredPosition = basePos + new Vector2(direction * spacing, 0f);
        root.DOAnchorPos(basePos, 0.35f).SetEase(Ease.OutCubic);
    }

    /// <summary>Вращение: выбранный кружок делает полный оборот на 360°.</summary>
    private void PlaySpin360(int selectedCircleIndex)
    {
        RectTransform selected = colorCircles[selectedCircleIndex].transform as RectTransform;

        selected.DOKill();
        selected.localRotation = Quaternion.identity;
        selected.DORotate(new Vector3(0f, 0f, 360f), 0.5f, RotateMode.FastBeyond360)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                if (selected != null)
                    selected.localRotation = Quaternion.identity;
            });
    }

    private void EnsureColorCircles()
    {
        if (colorCirclePrefab == null || colorCirclesRoot == null)
            return;

        for (int i = colorCircles.Count; i < VisibleColorCount; i++)
        {
            GameObject circle = Instantiate(colorCirclePrefab, colorCirclesRoot);
            circle.name = $"ColorCircle_{i}";
            colorCircles.Add(circle);
        }
    }

    private static void SetCheckActive(GameObject circle, bool active)
    {
        Transform selected = circle.transform.Find("SelectedContainer");

        if (selected != null)
            selected.gameObject.SetActive(active);
    }

    private void RefreshFooter()
    {
        if (footerText == null)
            return;

        string version = GameSessionData.GameVersion;
        int versionStart = version.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });

        footerText.text = versionStart >= 0 ? version.Substring(versionStart) : version;
    }

    private void RefreshPoints()
    {
        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
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
