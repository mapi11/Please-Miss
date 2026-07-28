using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RunnerHudUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject panelPrefab;

    private readonly Dictionary<ulong, RunnerTeamPanel> panels = new Dictionary<ulong, RunnerTeamPanel>();

    private void Start()
    {
        contentParent.gameObject.SetActive(true);
        Rebuild();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        Clear();
    }

    private void Rebuild()
    {
        Clear();
        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            TryCreatePanel(client.ClientId, client.PlayerObject);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            TryCreatePanel(clientId, client.PlayerObject);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (panels.TryGetValue(clientId, out var panel))
        {
            Destroy(panel.gameObject);
            panels.Remove(clientId);
        }
    }

    private void TryCreatePanel(ulong clientId, NetworkObject playerObj)
    {
        if (playerObj == null) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        var role = playerObj.GetComponent<NetworkPlayerRole>();
        if (role != null && !role.IsRunner) return;

        var go = Instantiate(panelPrefab, contentParent);
        var panel = go.GetComponent<RunnerTeamPanel>();
        panel.Setup(clientId, playerObj);
        panels[clientId] = panel;
    }

    private void Clear()
    {
        foreach (var panel in panels.Values)
            Destroy(panel.gameObject);
        panels.Clear();
    }
}
