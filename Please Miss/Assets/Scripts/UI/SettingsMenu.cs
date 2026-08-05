using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private Transform settingsSpawnPoint;
    [Tooltip("Optional. If not assigned, a Button named 'SettingsButton' is searched in children")]
    [SerializeField] private Button openButton;

    private GameObject settingsInstance;
    private bool closingSettings;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        BindOpenButton();
    }

    private void BindOpenButton()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
            return;
        }

        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == "SettingsButton")
            {
                button.onClick.AddListener(Open);
                break;
            }
        }
    }

    public void Open()
    {
        if (settingsInstance != null || closingSettings)
            return;

        IsOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Transform parent = settingsSpawnPoint != null && settingsSpawnPoint.gameObject.activeInHierarchy
            ? settingsSpawnPoint
            : transform;

        settingsInstance = Instantiate(settingsPanelPrefab, parent);
    }

    public void Close()
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
        IsOpen = false;
        closingSettings = false;
        settingsInstance = null;
    }

    public void OnPanelClosedExternally()
    {
        IsOpen = false;
        closingSettings = false;
        settingsInstance = null;
    }
}
