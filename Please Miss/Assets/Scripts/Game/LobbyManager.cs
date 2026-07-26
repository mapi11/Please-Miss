using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float countdownDuration = 5f;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private LobbyUI lobbyUI;

    private Coroutine countdownCoroutine;

    public bool IsCountdownActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        if (NetworkManager.Singleton == null) return;

        if (clientId == NetworkManager.Singleton.LocalClientId && !IsServer)
            SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnPlayerStateChanged()
    {
        if (!IsServer) return;

        if (IsCountdownActive)
            CancelCountdown();

        CheckAllReady();
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

        bool hasSniper = false;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) return;

            var roleComp = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (roleComp == null) return;

            if (!roleComp.IsReady) return;
            if (roleComp.CurrentRole == PlayerRole.Sniper) hasSniper = true;
            if (roleComp.CurrentRole == PlayerRole.None) return;
        }

        if (!hasSniper) return;

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
            if (lobbyUI != null)
                lobbyUI.ShowCountdown((int)Mathf.Ceil(remaining));

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

        IsCountdownActive = false;
    }

    [ClientRpc]
    private void HideCountdownClientRpc()
    {
        if (!IsServer && lobbyUI != null)
            lobbyUI.HideCountdown();
    }
}
