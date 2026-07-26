using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }
    public static bool IsInLobby { get; set; } = true;

    [Header("Settings")]
    [SerializeField] private float countdownDuration = 5f;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private LobbyUI lobbyUI;

    private Coroutine countdownCoroutine;
    private bool isLeaving;

    public bool IsCountdownActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        IsInLobby = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            IsInLobby = false;
    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnLocalDisconnected;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (lobbyUI != null)
            lobbyUI.RebuildCards();

        HideInventoryUI();
    }

    private void HideInventoryUI()
    {
        var invUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
        foreach (var inv in invUIs)
            inv.gameObject.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnLocalDisconnected;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientChanged(ulong clientId)
    {
        if (lobbyUI != null)
            lobbyUI.RebuildCards();

        NotifyPlayersChangedClientRpc();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (lobbyUI != null)
            lobbyUI.RemoveCard(clientId);

        NotifyPlayersChangedClientRpc();

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
            IsCountdownActive = false;
            NetworkConnectionManager.ConnectionLocked = false;
            HideCountdownClientRpc();
        }
    }

    [ClientRpc]
    private void NotifyPlayersChangedClientRpc()
    {
        if (!IsServer && lobbyUI != null)
            lobbyUI.RebuildCards();
    }

    private void OnLocalDisconnected(ulong clientId)
    {
        if (isLeaving) return;
        if (IsServer) return;

        isLeaving = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnPlayerStateChanged()
    {
        if (!IsServer) return;

        if (IsCountdownActive)
            CancelCountdown();

        CheckAllReady();
        UpdateWarning();
    }

    private void UpdateWarning()
    {
        if (lobbyUI == null) return;

        bool allReady = true;
        int sniperCount = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var roleComp = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (roleComp == null) continue;
            if (!roleComp.IsReady) allReady = false;
            if (roleComp.CurrentRole == PlayerRole.Sniper) sniperCount++;
        }

        int warning = 0;

        if (sniperCount > 1)
            warning = 2;
        else if (allReady && sniperCount == 0 && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            warning = 1;

        lobbyUI.SetWarning(warning);
        UpdateWarningClientRpc(warning);
    }

    [ClientRpc]
    private void UpdateWarningClientRpc(int warning)
    {
        if (!IsServer && lobbyUI != null)
            lobbyUI.SetWarning(warning);
    }

    private void CancelCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        IsCountdownActive = false;
        NetworkConnectionManager.ConnectionLocked = false;

        HideCountdownClientRpc();
    }

    private void CheckAllReady()
    {
        if (IsCountdownActive) return;
        if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return;

        int sniperCount = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) return;

            var roleComp = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (roleComp == null) return;

            if (!roleComp.IsReady) return;
            if (roleComp.CurrentRole == PlayerRole.Sniper) sniperCount++;
            if (roleComp.CurrentRole == PlayerRole.None) return;
        }

        if (sniperCount != 1) return;

        if (countdownCoroutine == null)
            countdownCoroutine = StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        IsCountdownActive = true;
        NetworkConnectionManager.ConnectionLocked = true;

        float remaining = countdownDuration;

        while (remaining > 0)
        {
            int seconds = (int)Mathf.Ceil(remaining);

            if (lobbyUI != null)
                lobbyUI.ShowCountdown(seconds);

            ShowCountdownClientRpc(seconds);

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (IsCountdownActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

        IsCountdownActive = false;
    }

    [ClientRpc]
    private void ShowCountdownClientRpc(int seconds)
    {
        if (lobbyUI != null)
            lobbyUI.ShowCountdown(seconds);
    }

    [ClientRpc]
    private void HideCountdownClientRpc()
    {
        if (lobbyUI != null)
            lobbyUI.HideCountdown();
    }
}
