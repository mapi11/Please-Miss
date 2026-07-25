using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerRole : NetworkBehaviour
{
    private readonly NetworkVariable<byte> networkRole = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public PlayerRole CurrentRole => (PlayerRole)networkRole.Value;

    public event System.Action<PlayerRole, PlayerRole> OnRoleChanged;

    public override void OnNetworkSpawn()
    {
        networkRole.OnValueChanged += OnRoleValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkRole.OnValueChanged -= OnRoleValueChanged;
    }

    public void RequestSetRole(PlayerRole role)
    {
        if (!IsSpawned || role == CurrentRole) return;

        if (IsServer)
            SetRoleServer(role);
        else
            SetRoleServerRpc(role);
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
    }

    private void OnRoleValueChanged(byte oldValue, byte newValue)
    {
        OnRoleChanged?.Invoke((PlayerRole)oldValue, (PlayerRole)newValue);
    }
}
