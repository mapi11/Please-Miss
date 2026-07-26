using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerRole : NetworkBehaviour
{
    private readonly NetworkVariable<byte> networkRole = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> networkIsReady = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public PlayerRole CurrentRole => (PlayerRole)networkRole.Value;
    public bool IsReady => networkIsReady.Value;

    public event System.Action<PlayerRole, PlayerRole> OnRoleChanged;
    public event System.Action<bool, bool> OnReadyChanged;

    public override void OnNetworkSpawn()
    {
        networkRole.OnValueChanged += OnRoleValueChanged;
        networkIsReady.OnValueChanged += OnReadyValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkRole.OnValueChanged -= OnRoleValueChanged;
        networkIsReady.OnValueChanged -= OnReadyValueChanged;
    }

    public void RequestSetRole(PlayerRole role)
    {
        if (!IsSpawned || role == CurrentRole) return;

        if (IsServer)
            SetRoleServer(role);
        else
            SetRoleServerRpc(role);
    }

    public void RequestToggleReady()
    {
        if (!IsSpawned) return;

        if (IsServer)
            networkIsReady.Value = !networkIsReady.Value;
        else
            ToggleReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetRoleServerRpc(PlayerRole role, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        SetRoleServer(role);
    }

    private void SetRoleServer(PlayerRole role)
    {
        networkRole.Value = (byte)role;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnPlayerStateChanged();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        networkIsReady.Value = !networkIsReady.Value;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnPlayerStateChanged();
    }

    private void OnRoleValueChanged(byte oldValue, byte newValue)
    {
        OnRoleChanged?.Invoke((PlayerRole)oldValue, (PlayerRole)newValue);
    }

    private void OnReadyValueChanged(bool oldValue, bool newValue)
    {
        OnReadyChanged?.Invoke(oldValue, newValue);
    }
}
