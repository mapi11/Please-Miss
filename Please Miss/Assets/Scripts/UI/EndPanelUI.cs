using Unity.Netcode;
using UnityEngine;

public class EndPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject sniperEndPanel;

    private void Awake()
    {
        if (GetComponent<RunnerEndPanelUI>() == null)
            gameObject.AddComponent<RunnerEndPanelUI>();
    }

    private void Update()
    {
        if (GameManager.Instance == null || NetworkManager.Singleton == null) return;

        bool show = GameManager.Instance.State.Value == GameManager.GameState.Ended
                    && IsLocalPlayerSniper();

        if (sniperEndPanel != null && sniperEndPanel.activeSelf != show)
            sniperEndPanel.SetActive(show);

        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
