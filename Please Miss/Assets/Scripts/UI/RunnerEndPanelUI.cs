using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunnerEndPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject runnerEndPanel;
    [SerializeField] private TMP_Text survivedTimeText;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button spectateButton;
    [SerializeField] private RectTransform rewardsContainer;
    [SerializeField] private GameObject rewardPanelPrefab;

    [Header("Survived Time Colors")]
    [Tooltip("Цвет текста времени, когда игрок выжил")]
    [SerializeField] private Color survivedColor = new Color(0.25f, 0.55f, 1f, 1f);
    [Tooltip("Цвет текста времени, когда игрок умер")]
    [SerializeField] private Color diedColor = new Color(1f, 0.25f, 0.25f, 1f);

    private SpectatorManager spectatorManager;
    private bool panelHidden;
    private float survivedTime = -1f;
    private bool isSpectatorMode;
    private bool buttonsCleaned;
    private static SpectatorController cachedSpectator;

    private void Awake()
    {
        isSpectatorMode = GetComponentInParent<SpectatorController>(true) != null;

        ResolvePanel();
        if (runnerEndPanel == null)
        {
            enabled = false;
            return;
        }

        ResolveElements();

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitGame);
        if (spectateButton != null)
            spectateButton.onClick.AddListener(OnSpectate);
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitGame);
        if (spectateButton != null)
            spectateButton.onClick.RemoveListener(OnSpectate);

        if (GameManager.Instance != null)
            GameManager.Instance.OnRunnerRewardRecorded -= OnRunnerRewardRecorded;
        if (GameManager.Instance != null)
            GameManager.Instance.OnNearMissRecorded -= OnNearMissRecorded;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnRunnerRewardRecorded += OnRunnerRewardRecorded;
        if (GameManager.Instance != null)
            GameManager.Instance.OnNearMissRecorded += OnNearMissRecorded;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnRunnerRewardRecorded -= OnRunnerRewardRecorded;
        if (GameManager.Instance != null)
            GameManager.Instance.OnNearMissRecorded -= OnNearMissRecorded;
    }

    private void Update()
    {
        if (panelHidden) return;
        if (GameManager.Instance == null || NetworkManager.Singleton == null) return;

        bool stateEnded = GameManager.Instance.State.Value == GameManager.GameState.Ended;

        if (IsSpectating())
        {
            if (runnerEndPanel.activeSelf != stateEnded)
                runnerEndPanel.SetActive(stateEnded);

            if (spectateButton != null && spectateButton.gameObject.activeSelf)
                spectateButton.gameObject.SetActive(false);

            if (stateEnded)
            {
                CleanupExtraButtons();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        bool localFinished = GameManager.LocalRunnerFinished;
        bool dead = IsLocalPlayerDead();

        bool show;
        if (isSpectatorMode)
        {
            show = stateEnded;
        }
        else if (IsLocalPlayerSniper())
        {
            show = false;
        }
        else
        {
            show = dead || localFinished || stateEnded;
        }

        if (runnerEndPanel.activeSelf != show)
            runnerEndPanel.SetActive(show);

        if (show)
        {
            CleanupExtraButtons();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (survivedTimeText != null)
        {
            if (!isSpectatorMode && show && (dead || localFinished || stateEnded))
            {
                if (survivedTime < 0f)
                {
                    survivedTime = Mathf.Max(0f,
                        GameManager.Instance.GameDuration - GameManager.Instance.GameTimeRemaining.Value);
                }

                string resultLabel = dead ? "You died" : "You survived";
                survivedTimeText.text = resultLabel + ": " + FormatTime(survivedTime);
                survivedTimeText.color = dead ? diedColor : survivedColor;
            }
            else if (!show)
            {
                survivedTimeText.text = "";
            }
        }

        if (spectateButton != null)
        {
            bool canSpectate = !stateEnded && GameManager.Instance.CountAliveRunners() > 0;
            if (spectateButton.gameObject.activeSelf != canSpectate)
                spectateButton.gameObject.SetActive(canSpectate);
        }
    }

    private void CleanupExtraButtons()
    {
        if (buttonsCleaned) return;
        if (runnerEndPanel == null || exitButton == null) return;

        buttonsCleaned = true;

        foreach (var btn in runnerEndPanel.GetComponentsInChildren<Button>(true))
        {
            if (btn != exitButton && btn != spectateButton)
                Destroy(btn.gameObject);
        }
    }

    private void OnRunnerRewardRecorded(int reward, string reason)
    {
        SpawnRewardCard(reward, reason);
    }

    private void OnNearMissRecorded(int reward, string reason)
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
            panel.Setup(0, "", reward, reason);
    }

    private void SpawnRewardCard(int reward, string reason)
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
            panel.Setup(reward, reason, 0, "");
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
        background.color = new Color(0.1f, 0.45f, 0.75f, 0.85f);

        CreateFallbackText(go.transform, "MainPointsText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -5f), new Vector2(300f, 24f), TextAlignmentOptions.Left);
        CreateFallbackText(go.transform, "BonusPointsText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -5f), new Vector2(200f, 24f), TextAlignmentOptions.Right);

        go.AddComponent<RewardPanel>();
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

    private void OnSpectate()
    {
        panelHidden = true;
        if (runnerEndPanel != null)
            runnerEndPanel.SetActive(false);

        CacheManager();

        if (spectatorManager != null)
            spectatorManager.EnterSpectatorMode();
        else
            Debug.LogError("[RunnerEndPanelUI] SpectatorManager is NULL!");
    }

    private void OnExitGame()
    {
        panelHidden = true;
        if (runnerEndPanel != null)
            runnerEndPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.ShutdownNetwork();
        else if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainMenu");
    }

    private void CacheManager()
    {
        if (spectatorManager != null) return;
        if (NetworkManager.Singleton == null) return;

        var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (local != null)
            spectatorManager = local.GetComponent<SpectatorManager>();
    }

    private static bool IsSpectating()
    {
        if (cachedSpectator != null && cachedSpectator.IsSpectating)
            return true;

        foreach (var spectator in FindObjectsOfType<SpectatorController>())
        {
            if (spectator.IsOwner && spectator.IsSpectating)
            {
                cachedSpectator = spectator;
                return true;
            }
        }

        cachedSpectator = null;
        return false;
    }

    private static bool IsLocalPlayerRunner()
    {
        if (NetworkManager.Singleton == null) return false;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return false;

        var role = localClient.PlayerObject.GetComponent<NetworkPlayerRole>();
        return role != null && role.IsRunner;
    }

    private static bool IsLocalPlayerSniper()
    {
        if (NetworkManager.Singleton == null) return false;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return false;

        var role = localClient.PlayerObject.GetComponent<NetworkPlayerRole>();
        return role != null && role.IsSniper;
    }

    private static bool IsLocalPlayerDead()
    {
        if (NetworkManager.Singleton == null) return false;

        var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (local == null) return false;

        var health = local.GetComponent<PlayerHealth>();
        return health != null && health.IsSpawned && health.IsOwner && health.IsDead;
    }

    private void ResolvePanel()
    {
        if (runnerEndPanel != null) return;

        if (gameObject.name == "runnerEndPanel")
        {
            runnerEndPanel = gameObject;
            return;
        }

        Transform child = transform.Find("runnerEndPanel");
        if (child != null)
            runnerEndPanel = child.gameObject;
    }

    private void ResolveElements()
    {
        if (runnerEndPanel == null) return;

        if (survivedTimeText == null)
        {
            Transform t = FindChildRecursive(runnerEndPanel.transform, "SurvivedTimeText");
            if (t != null)
                survivedTimeText = t.GetComponent<TMP_Text>();
        }

        if (exitButton == null)
        {
            Transform t = FindChildRecursive(runnerEndPanel.transform, "ExitButton");
            if (t != null)
                exitButton = t.GetComponent<Button>();
        }

        if (spectateButton == null)
        {
            Transform t = FindChildRecursive(runnerEndPanel.transform, "SpectateButton");
            if (t != null)
                spectateButton = t.GetComponent<Button>();
        }

        if (survivedTimeText == null)
            survivedTimeText = CreateSurvivedTimeText();

        if (exitButton == null)
            exitButton = CreateExitButton();

        if (rewardsContainer == null)
        {
            Transform t = FindChildRecursive(runnerEndPanel.transform, "RewardsContainer");
            if (t is RectTransform rect)
                rewardsContainer = rect;
        }

        if (rewardsContainer == null)
            rewardsContainer = CreateRewardsContainer();
    }

    private RectTransform CreateRewardsContainer()
    {
        GameObject go = new GameObject("RewardsContainer", typeof(RectTransform));
        go.transform.SetParent(runnerEndPanel.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(40f, 90f);
        rect.offsetMax = new Vector2(-40f, -60f);
        return rect;
    }

    private RectTransform ResolveRewardsContainer()
    {
        if (rewardsContainer != null)
            return rewardsContainer;

        if (runnerEndPanel != null)
        {
            Transform t = FindChildRecursive(runnerEndPanel.transform, "RewardsContainer");
            if (t is RectTransform rect)
            {
                rewardsContainer = rect;
                return rect;
            }
        }

        rewardsContainer = CreateRewardsContainer();
        return rewardsContainer;
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

    private TMP_Text CreateSurvivedTimeText()
    {
        GameObject go = new GameObject("SurvivedTimeText", typeof(RectTransform));
        go.transform.SetParent(runnerEndPanel.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.55f);
        rect.anchorMax = new Vector2(0.5f, 0.55f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 60f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36f;
        text.color = Color.white;
        text.font = FindAnyFontAsset();
        return text;
    }

    private Button CreateExitButton()
    {
        Button source = null;
        foreach (var btn in runnerEndPanel.GetComponentsInChildren<Button>(true))
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
        go.transform.SetParent(runnerEndPanel.transform, false);

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

    private static string FormatTime(float seconds)
    {
        int totalSec = Mathf.CeilToInt(seconds);
        int mins = totalSec / 60;
        int secs = totalSec % 60;
        return $"{mins}:{secs:D2}";
    }
}
