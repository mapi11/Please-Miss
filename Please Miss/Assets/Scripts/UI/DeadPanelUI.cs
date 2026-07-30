using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DeadPanelUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject deadPanel;
    [SerializeField] private Button spectateButton;
    [SerializeField] private Button nextButton;

    private SpectatorManager spectatorManager;
    private bool panelHidden;

    private void Awake()
    {
        if (spectateButton != null)
            spectateButton.onClick.AddListener(OnSpectate);
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNext);

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
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNext);
    }

    private void Update()
    {
        if (panelHidden) return;

        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<PlayerHealth>();

            if (playerHealth == null && NetworkManager.Singleton != null)
            {
                var localClient = NetworkManager.Singleton.LocalClient;
                if (localClient?.PlayerObject != null)
                    playerHealth = localClient.PlayerObject.GetComponent<PlayerHealth>();
            }

            return;
        }

        bool dead = playerHealth.IsSpawned && playerHealth.IsOwner && playerHealth.IsDead;

        if (deadPanel != null && deadPanel.activeSelf != dead)
            deadPanel.SetActive(dead);

        if (dead)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnSpectate()
    {
        Debug.Log("[DeadPanelUI] OnSpectate clicked");
        panelHidden = true;
        if (deadPanel != null)
            deadPanel.SetActive(false);

        CacheManager();

        if (spectatorManager != null)
        {
            Debug.Log("[DeadPanelUI] Found SpectatorManager, calling EnterSpectatorMode");
            spectatorManager.EnterSpectatorMode();
        }
        else
        {
            Debug.LogError("[DeadPanelUI] SpectatorManager is NULL!");
        }
    }

    private void OnNext()
    {
        if (deadPanel != null)
            deadPanel.SetActive(false);

        GameManager.LocalRunnerFinished = true;
    }
}
