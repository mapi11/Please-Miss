using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SniperEndPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject sniperEndPanel;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private GameObject diedPlayerPanelPrefab;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text totalEarnedText;
    [SerializeField] private TMP_Text timeLeftText;
    [SerializeField] private RectTransform rewardsContainer;
    [SerializeField] private GameObject rewardPanelPrefab;

    private bool panelHidden;

    private void Awake()
    {
        ResolvePanel();
        if (sniperEndPanel == null)
        {
            enabled = false;
            return;
        }

        ResolveElements();

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitGame);
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitGame);

        if (GameManager.Instance != null)
            GameManager.Instance.OnSniperKillRecorded -= OnSniperKillRecorded;

        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSniperKillRecorded += OnSniperKillRecorded;

        LocalPlayerSettings.PointsChanged += OnPointsChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSniperKillRecorded -= OnSniperKillRecorded;

        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void Update()
    {
        if (panelHidden) return;
        if (GameManager.Instance == null || NetworkManager.Singleton == null) return;

        bool stateEnded = GameManager.Instance.State.Value == GameManager.GameState.Ended;
        bool show = IsLocalPlayerSniper() && stateEnded;

        if (sniperEndPanel.activeSelf != show)
            sniperEndPanel.SetActive(show);

        // Хост не видит Exit, чтобы случайно не выйти и не выкинуть всех из игры
        if (exitButton != null && exitButton.gameObject.activeSelf != !IsLocalHost())
            exitButton.gameObject.SetActive(!IsLocalHost());

        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshPointsText();
            RefreshTimeLeftText();
        }
    }

    private static bool IsLocalHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }

    private void OnPointsChanged(int newPoints)
    {
        RefreshPointsText();
    }

    private void RefreshPointsText()
    {
        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";

        if (totalEarnedText != null && GameManager.Instance != null)
            totalEarnedText.text = $"Total Earned: +{Mathf.Max(0, GameManager.Instance.TotalEarnedThisGame)}";
    }

    private void RefreshTimeLeftText()
    {
        if (timeLeftText != null && GameManager.Instance != null)
            timeLeftText.text = "Time left: " + FormatMinutesSeconds(GameManager.Instance.ElapsedMatchTime);
    }

    private void OnSniperKillRecorded(string playerName, Color32 color, float survivedTime, string hitZone, int mainPoints, int bonusPoints)
    {
        SpawnDiedPlayerCard(playerName, color, survivedTime, hitZone);
        SpawnRewardCard(mainPoints, bonusPoints, hitZone);
    }

    private void SpawnDiedPlayerCard(string playerName, Color32 color, float survivedTime, string hitZone)
    {
        RectTransform container = ResolveContainer();

        GameObject cardObject;
        if (diedPlayerPanelPrefab != null)
        {
            cardObject = Instantiate(diedPlayerPanelPrefab, container, false);
        }
        else
        {
            cardObject = CreateCardFallback(container);
        }

        var panel = cardObject.GetComponent<DiedPlayerPanel>();
        if (panel != null)
            panel.Setup(playerName, color, survivedTime, hitZone);
    }

    private void SpawnRewardCard(int mainPoints, int bonusPoints, string hitZone)
    {
        RectTransform container = ResolveRewardsContainer();

        GameObject cardObject;
        if (rewardPanelPrefab != null)
        {
            cardObject = Instantiate(rewardPanelPrefab, container, false);
        }
        else
        {
            cardObject = CreateRewardCardFallback(container);
        }

        var panel = cardObject.GetComponent<RewardPanel>();
        if (panel != null)
            panel.Setup("Kill", mainPoints, bonusPoints);
    }

    private GameObject CreateRewardCardFallback(Transform parent)
    {
        GameObject go = new GameObject("RewardPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 40f);
        rect.anchoredPosition = Vector2.zero;

        Image background = go.AddComponent<Image>();
        background.color = new Color(0.1f, 0.55f, 0.2f, 0.85f);

        CreateFallbackText(go.transform, "ActionText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -5f), new Vector2(200f, 24f), TextAlignmentOptions.Left);
        CreateFallbackText(go.transform, "MainPointsText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(150f, 24f), TextAlignmentOptions.Center);
        CreateFallbackText(go.transform, "BonusPointsText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -5f), new Vector2(200f, 24f), TextAlignmentOptions.Right);

        go.AddComponent<RewardPanel>();
        return go;
    }

    private GameObject CreateCardFallback(Transform parent)
    {
        GameObject go = new GameObject("DiedPlayerPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 60f);
        rect.anchoredPosition = Vector2.zero;

        Image background = go.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        CreateFallbackText(go.transform, "NameText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -5f), new Vector2(200f, 24f), TextAlignmentOptions.Left);
        CreateFallbackText(go.transform, "TimeText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(180f, 24f), TextAlignmentOptions.Center);
        CreateFallbackText(go.transform, "ZoneText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -5f), new Vector2(200f, 24f), TextAlignmentOptions.Right);

        go.AddComponent<DiedPlayerPanel>();
        return go;
    }

    private TMP_Text CreateFallbackText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = 20f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private void OnExitGame()
    {
        panelHidden = true;
        if (sniperEndPanel != null)
            sniperEndPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.ShutdownNetwork();
        else if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainMenu");
    }

    private static bool IsLocalPlayerSniper()
    {
        if (NetworkManager.Singleton == null) return false;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return false;

        var role = localClient.PlayerObject.GetComponent<NetworkPlayerRole>();
        return role != null && role.IsSniper;
    }

    private RectTransform ResolveContainer()
    {
        if (cardsContainer != null)
            return cardsContainer;

        if (sniperEndPanel != null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "CardsContainer");
            if (t is RectTransform rect)
            {
                cardsContainer = rect;
                return rect;
            }
        }

        cardsContainer = CreateContainer(sniperEndPanel != null ? sniperEndPanel.transform : transform, "CardsContainer");
        return cardsContainer;
    }

    private RectTransform ResolveRewardsContainer()
    {
        if (rewardsContainer != null)
            return rewardsContainer;

        if (sniperEndPanel != null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "RewardsContainer");
            if (t is RectTransform rect)
            {
                rewardsContainer = rect;
                return rect;
            }
        }

        rewardsContainer = CreateContainer(sniperEndPanel != null ? sniperEndPanel.transform : transform, "RewardsContainer");
        return rewardsContainer;
    }

    private RectTransform CreateContainer(Transform parent, string containerName)
    {
        GameObject go = new GameObject(containerName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(40f, 90f);
        rect.offsetMax = new Vector2(-40f, -60f);
        return rect;
    }

    private void ResolvePanel()
    {
        if (sniperEndPanel != null) return;

        if (gameObject.name == "sniperEndPanel")
        {
            sniperEndPanel = gameObject;
            return;
        }

        Transform child = transform.Find("sniperEndPanel");
        if (child != null)
            sniperEndPanel = child.gameObject;
    }

    private void ResolveElements()
    {
        if (sniperEndPanel == null) return;

        if (exitButton == null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "ExitButton");
            if (t != null)
                exitButton = t.GetComponent<Button>();
        }

        if (exitButton == null)
            exitButton = CreateExitButton();

        if (pointsText == null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "PointsText");
            if (t != null)
                pointsText = t.GetComponent<TMP_Text>();
        }

        if (pointsText == null)
            pointsText = CreatePointsText();

        if (totalEarnedText == null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "TotalEarnedText");
            if (t != null)
                totalEarnedText = t.GetComponent<TMP_Text>();
        }

        if (totalEarnedText == null)
            totalEarnedText = CreateTotalEarnedText();

        if (timeLeftText == null)
        {
            Transform t = FindChildRecursive(sniperEndPanel.transform, "TimeLeftText");
            if (t != null)
                timeLeftText = t.GetComponent<TMP_Text>();
        }

        if (timeLeftText == null)
            timeLeftText = CreateTimeLeftText();

        if (cardsContainer == null)
            ResolveContainer();
    }

    private TMP_Text CreatePointsText()
    {
        GameObject go = new GameObject("PointsText", typeof(RectTransform));
        go.transform.SetParent(sniperEndPanel.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -10f);
        rect.sizeDelta = new Vector2(400f, 30f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private TMP_Text CreateTotalEarnedText()
    {
        GameObject go = new GameObject("TotalEarnedText", typeof(RectTransform));
        go.transform.SetParent(sniperEndPanel.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -45f);
        rect.sizeDelta = new Vector2(400f, 30f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private TMP_Text CreateTimeLeftText()
    {
        GameObject go = new GameObject("TimeLeftText", typeof(RectTransform));
        go.transform.SetParent(sniperEndPanel.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -80f);
        rect.sizeDelta = new Vector2(400f, 30f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private static string FormatMinutesSeconds(float seconds)
    {
        int totalSec = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mins = totalSec / 60;
        int secs = totalSec % 60;
        return mins > 0 ? $"{mins} min {secs} sec" : $"{secs} sec";
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private Button CreateExitButton()
    {
        Button source = null;
        foreach (var btn in sniperEndPanel.GetComponentsInChildren<Button>(true))
        {
            source = btn;
            break;
        }

        if (source != null)
        {
            Button clone = Instantiate(source, source.transform.parent);
            clone.gameObject.name = "ExitButton";

            RectTransform rect = clone.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition += Vector2.down * 90f;

            TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = "Exit game";

            return clone;
        }

        GameObject go = new GameObject("ExitButton", typeof(RectTransform));
        go.transform.SetParent(sniperEndPanel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(300f, 50f);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        TextMeshProUGUI exitLabel = labelGo.AddComponent<TextMeshProUGUI>();
        exitLabel.text = "Exit game";
        exitLabel.alignment = TextAlignmentOptions.Center;
        exitLabel.fontSize = 24f;
        exitLabel.color = Color.white;
        exitLabel.font = FindAnyFontAsset();

        return button;
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
}
