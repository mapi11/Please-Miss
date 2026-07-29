using Unity.Netcode;
using UnityEngine;

public class WinScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject sniperEndPanel;
    [SerializeField] private GameObject runnerEndPanel;

    private void Update()
    {
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

    private static bool IsLocalPlayerSniper()
    {
        if (NetworkManager.Singleton == null) return false;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return false;

        var role = localClient.PlayerObject.GetComponent<NetworkPlayerRole>();
        return role != null && role.IsSniper;
    }
}
