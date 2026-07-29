using UnityEngine;

public class PlayerUIConfigurator : MonoBehaviour
{
    [SerializeField] private GameObject runnerUI;
    [SerializeField] private GameObject sniperUI;
    [SerializeField] private GameObject[] ownerOnlyElements;

    private PlayerHealth playerHealth;

    private void Awake()
    {
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

        bool shouldShow = playerHealth.IsSpawned && playerHealth.IsOwner;

        for (int i = 0; i < ownerOnlyElements.Length; i++)
        {
            if (ownerOnlyElements[i] != null && ownerOnlyElements[i].activeSelf != shouldShow)
                ownerOnlyElements[i].SetActive(shouldShow);
        }
    }

    public void Configure(PlayerRole role)
    {
        if (runnerUI != null) runnerUI.SetActive(role == PlayerRole.Runner);
        if (sniperUI != null) sniperUI.SetActive(role == PlayerRole.Sniper);
    }
}
