using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The single network source of truth for the player's selected lobby role and ready state.
/// Add exactly one instance to the network Player Prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerRole : NetworkBehaviour
{
    private readonly NetworkVariable<PlayerRole> networkRole = new NetworkVariable<PlayerRole>(
        PlayerRole.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> networkIsReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField] private PlayerRole debugRole;
    [SerializeField] private bool debugReady;

    public PlayerRole CurrentRole => networkRole.Value;
    public bool IsReady => networkIsReady.Value;
    public bool IsSniper => CurrentRole == PlayerRole.Sniper;
    public bool IsRunner => CurrentRole == PlayerRole.Runner;

    public event Action<PlayerRole, PlayerRole> OnRoleChanged;
    public event Action<bool, bool> OnReadyChanged;

    public override void OnNetworkSpawn()
    {
        debugRole = networkRole.Value;
        debugReady = networkIsReady.Value;
        networkRole.OnValueChanged += HandleRoleChanged;
        networkIsReady.OnValueChanged += HandleReadyChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkRole.OnValueChanged -= HandleRoleChanged;
        networkIsReady.OnValueChanged -= HandleReadyChanged;
    }

    public void RequestSetRole(PlayerRole role)
    {
        if (!IsSpawned || (!IsOwner && !IsServer))
            return;

        if (!IsValidSelectableRole(role) || role == CurrentRole)
            return;

        if (IsServer)
        {
            TrySetRoleOnServer(role, OwnerClientId);
        }
        else
        {
            SetRoleServerRpc(role);
        }
    }

    public void RequestToggleReady()
    {
        if (!IsSpawned || (!IsOwner && !IsServer))
            return;

        if (IsServer)
        {
            ToggleReadyOnServer();
        }
        else
        {
            ToggleReadyServerRpc();
        }
    }

    public bool ServerSetRole(PlayerRole role)
    {
        if (!IsServer)
        {
            Debug.LogWarning("ServerSetRole can only be called on the server.", this);
            return false;
        }

        if (!IsValidRole(role))
            return false;

        return TrySetRoleOnServer(role, OwnerClientId);
    }

    public void ServerSetReady(bool isReady)
    {
        if (!IsServer)
        {
            Debug.LogWarning("ServerSetReady can only be called on the server.", this);
            return;
        }

        if (isReady && CurrentRole == PlayerRole.None)
            return;

        networkIsReady.Value = isReady;
        NotifyLobbyStateChanged();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetRoleServerRpc(PlayerRole role, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!IsValidSelectableRole(role))
            return;

        TrySetRoleOnServer(role, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        ToggleReadyOnServer();
    }

    private bool TrySetRoleOnServer(PlayerRole role, ulong requestingClientId)
    {
        if (!IsServer || role == CurrentRole)
            return role == CurrentRole;

        networkRole.Value = role;

        // A changed role must be confirmed again with Ready.
        if (networkIsReady.Value)
            networkIsReady.Value = false;

        NotifyLobbyStateChanged();
        return true;
    }

    private void ToggleReadyOnServer()
    {
        if (!IsServer || CurrentRole == PlayerRole.None)
            return;

        networkIsReady.Value = !networkIsReady.Value;
        NotifyLobbyStateChanged();
    }

    private void NotifyLobbyStateChanged()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnPlayerStateChanged();
    }

    private void HandleRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        debugRole = newRole;
        OnRoleChanged?.Invoke(oldRole, newRole);
    }

    private void HandleReadyChanged(bool oldReady, bool newReady)
    {
        debugReady = newReady;
        OnReadyChanged?.Invoke(oldReady, newReady);
    }

    private static bool IsValidSelectableRole(PlayerRole role)
    {
        return role == PlayerRole.Runner || role == PlayerRole.Sniper;
    }

    private static bool IsValidRole(PlayerRole role)
    {
        return role == PlayerRole.None || IsValidSelectableRole(role);
    }
}
