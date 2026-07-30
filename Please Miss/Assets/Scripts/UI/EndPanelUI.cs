using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class EndPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject sniperEndPanel;
    [SerializeField] private GameObject runnerEndPanel;
    [SerializeField] private Button spectateButton;

    private SpectatorManager spectatorManager;
    private bool panelHidden;

    private void Awake()
    {
        if (spectateButton != null)
            spectateButton.onClick.AddListener(OnSpectate);

        CacheManager();
    }

    private void CacheManager()
    {
        if (spectatorManager != null) return;
        if (NetworkManager.Singleton == null) return;

        var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (local != null)
            spectatorManager = local.GetComponent<SpectatorManager>();
    }

    private void OnDestroy()
    {
        if (spectateButton != null)
            spectateButton.onClick.RemoveListener(OnSpectate);
    }

    private void Update()
    {
        if (panelHidden) return;
        if (GameManager.Instance == null || NetworkManager.Singleton == null) return;

        bool show = false;

        if (GameManager.Instance.State.Value == GameManager.GameState.Ended)
        {
            bool isSniper = IsLocalPlayerSniper();
            if (sniperEndPanel != null)
                sniperEndPanel.SetActive(isSniper);
            if (runnerEndPanel != null)
                runnerEndPanel.SetActive(!isSniper);
            show = true;
        }
        else if (GameManager.LocalRunnerFinished)
        {
            if (sniperEndPanel != null)
                sniperEndPanel.SetActive(false);
            if (runnerEndPanel != null)
                runnerEndPanel.SetActive(true);
            show = true;
        }
        else
        {
            if (sniperEndPanel != null) sniperEndPanel.SetActive(false);
            if (runnerEndPanel != null) runnerEndPanel.SetActive(false);
        }

        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnSpectate()
    {
        Debug.Log("[EndPanelUI] OnSpectate clicked");
        panelHidden = true;
        if (sniperEndPanel != null) sniperEndPanel.SetActive(false);
        if (runnerEndPanel != null) runnerEndPanel.SetActive(false);

        CacheManager();

        if (spectatorManager != null)
        {
            Debug.Log("[EndPanelUI] Found SpectatorManager, calling EnterSpectatorMode");
            spectatorManager.EnterSpectatorMode();
        }
        else
        {
            Debug.LogError("[EndPanelUI] SpectatorManager is NULL!");
        }
    }

    private static bool IsLocalPlayerSniper()
    {
        if (NetworkManager.Singleton == null) return false;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return false;

        var role = localClient.PlayerObject.GetComponent<NetworkPlayerRole>();
        return role != null && role.IsSniper;
    }
}
