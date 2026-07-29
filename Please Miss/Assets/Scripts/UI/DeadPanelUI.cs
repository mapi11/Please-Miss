using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DeadPanelUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject deadPanel;
    [SerializeField] private Button endGameButton;

    private void Awake()
    {
        if (endGameButton != null)
            endGameButton.onClick.AddListener(OnEndGameButton);
    }

    private void OnDestroy()
    {
        if (endGameButton != null)
            endGameButton.onClick.RemoveListener(OnEndGameButton);
    }

    private void Update()
    {
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

    public void OnEndGameButton()
    {
        if (deadPanel != null)
            deadPanel.SetActive(false);

        GameManager.LocalRunnerFinished = true;
    }
}
