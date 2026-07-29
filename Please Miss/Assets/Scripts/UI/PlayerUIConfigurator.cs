using UnityEngine;

public class PlayerUIConfigurator : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject runnerUI;
    [SerializeField] private GameObject sniperUI;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<PlayerHealth>();
            return;
        }

        if (targetCanvas != null)
            targetCanvas.enabled = playerHealth.IsSpawned && playerHealth.IsOwner;
    }

    public void Configure(PlayerRole role)
    {
        if (runnerUI != null) runnerUI.SetActive(role == PlayerRole.Runner);
        if (sniperUI != null) sniperUI.SetActive(role == PlayerRole.Sniper);
    }
}
