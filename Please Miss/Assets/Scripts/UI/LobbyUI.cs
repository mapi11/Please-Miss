using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Cards")]
    [SerializeField] private Transform playersContent;
    [SerializeField] private GameObject playerCardPrefab;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;

    [Header("Warning")]
    [SerializeField] private GameObject noSniperWarning;
    [SerializeField] private GameObject multiSniperWarning;

    [Header("HUD")]
    [SerializeField] private LobbyHudUI hudUI;

    [Header("Leave")]
    [SerializeField] private Button leaveButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Fast Start")]
    [SerializeField] private Button fastStartButton;

    private readonly Dictionary<ulong, LobbyPlayerCard> activeCards = new();

    private void Start()
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(LeaveLobby);

        if (fastStartButton != null)
            fastStartButton.onClick.AddListener(FastStartTest);
    }

    public void RebuildCards()
    {
        if (NetworkManager.Singleton == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        HashSet<ulong> activeIds = new HashSet<ulong>();

        foreach (var client in clients)
        {
            if (client.PlayerObject == null) continue;
            activeIds.Add(client.ClientId);

            if (activeCards.TryGetValue(client.ClientId, out var existing) && existing != null)
            {
                existing.Refresh();
                continue;
            }

            GameObject cardObj = Instantiate(playerCardPrefab, playersContent);
            var card = cardObj.GetComponent<LobbyPlayerCard>();
            if (card != null)
            {
                card.Setup(client.ClientId, client.PlayerObject, client.ClientId == localId);
                activeCards[client.ClientId] = card;
            }
        }

        List<ulong> toRemove = new List<ulong>();
        foreach (var kvp in activeCards)
        {
            if (!activeIds.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (var id in toRemove)
            RemoveCard(id);
    }

    public void RemoveCard(ulong clientId)
    {
        if (activeCards.TryGetValue(clientId, out var card) && card != null)
            Destroy(card.gameObject);

        activeCards.Remove(clientId);
    }

    public void ShowCountdown(int seconds)
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(true);

        if (countdownText != null)
            countdownText.text = seconds.ToString();
    }

    public void HideCountdown()
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(false);
    }

    public void SetWarning(int type)
    {
        if (noSniperWarning != null)
            noSniperWarning.SetActive(type == 1);

        if (multiSniperWarning != null)
            multiSniperWarning.SetActive(type == 2);
    }

    public void FastStartTest()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.FastStartTest();
    }

    public void LeaveLobby()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
