using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] private GameObject pausePanelPrefab;
    [SerializeField] private Transform pauseSpawnPoint;
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private Transform settingsSpawnPoint;

    private GameObject pauseInstance;
    private GameObject settingsInstance;
    private PlayerController playerController;
    private bool returningToMenu;
    private bool closingPause;
    private bool closingSettings;

    public bool IsOpen { get; private set; }
    public bool SettingsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        playerController = GetLocalPlayerController();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

            Instance = null;
        }
    }

    private void Update()
    {
        if (returningToMenu)
            return;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsConnectedClient &&
            !NetworkManager.Singleton.IsServer)
        {
            ReturnToMainMenu();
            return;
        }

        if (LobbyManager.IsInLobby && LobbyManager.Instance != null)
            return;

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (settingsInstance != null || closingSettings)
            return;

        if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen)
            return;

        if (pauseInstance != null || closingPause)
        {
            if (!closingPause)
                ClosePause();
        }
        else
        {
            OpenPause();
        }
    }

    private void OnDisconnected(ulong clientId)
    {
        if (returningToMenu)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            return;

        ReturnToMainMenu();
    }

    public void OpenPause()
    {
        if (pauseInstance != null || closingPause)
            return;

        IsOpen = true;

        if (playerController == null)
            playerController = GetLocalPlayerController();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Transform parent = pauseSpawnPoint != null && pauseSpawnPoint.gameObject.activeInHierarchy
            ? pauseSpawnPoint
            : transform;

        pauseInstance = Instantiate(pausePanelPrefab, parent);
    }

    public void ClosePause()
    {
        if (pauseInstance == null || closingPause)
            return;

        closingPause = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var panel = pauseInstance.GetComponent<PausePanelUI>();

        if (panel != null)
        {
            panel.AnimateOut(OnPauseClosed);
        }
        else
        {
            OnPauseClosed();
        }
    }

    private void OnPauseClosed()
    {
        IsOpen = false;
        closingPause = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        pauseInstance = null;
    }

    public void OpenSettings()
    {
        if (settingsInstance != null || closingSettings)
            return;

        SettingsOpen = true;

        Transform parent = settingsSpawnPoint != null ? settingsSpawnPoint : transform;
        settingsInstance = Instantiate(settingsPanelPrefab, parent);
    }

    public void CloseSettings()
    {
        if (settingsInstance == null || closingSettings)
            return;

        closingSettings = true;

        var panel = settingsInstance.GetComponent<SettingsPanelUI>();

        if (panel != null)
        {
            panel.AnimateOut(OnSettingsClosed);
        }
        else
        {
            OnSettingsClosed();
        }
    }

    private void OnSettingsClosed()
    {
        SettingsOpen = false;
        closingSettings = false;
        settingsInstance = null;
    }

    public void OnSettingsPanelClosedExternally()
    {
        closingSettings = false;
        settingsInstance = null;
        SettingsOpen = false;
    }

    public void ReturnToMainMenu()
    {
        if (returningToMenu)
            return;

        returningToMenu = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.ShutdownNetwork();
        else if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        if (pauseInstance != null)
        {
            Destroy(pauseInstance);
            pauseInstance = null;
        }

        if (settingsInstance != null)
        {
            Destroy(settingsInstance);
            settingsInstance = null;
        }

        closingPause = false;
        closingSettings = false;
        IsOpen = false;
        SettingsOpen = false;

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private static PlayerController GetLocalPlayerController()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;

            if (localClient != null && localClient.PlayerObject != null)
            {
                var pc = localClient.PlayerObject.GetComponentInChildren<PlayerController>();

                if (pc != null)
                    return pc;
            }
        }

        var all = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].IsOwner)
                return all[i];
        }

        return null;
    }
}
