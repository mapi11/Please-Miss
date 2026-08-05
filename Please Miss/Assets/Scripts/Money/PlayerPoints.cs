using Unity.Netcode;
using UnityEngine;

public class PlayerPoints : NetworkBehaviour
{
    private readonly NetworkVariable<int> networkPoints = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool initializedOnServer;

    public int Balance => networkPoints.Value;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            ReportStartingPointsServerRpc(LocalPlayerSettings.PlayerPoints);
    }

    [ServerRpc]
    private void ReportStartingPointsServerRpc(int startingPoints)
    {
        if (initializedOnServer)
            return;

        initializedOnServer = true;
        networkPoints.Value = Mathf.Max(0, startingPoints);
    }

    public void SetServerStartingPoints(int value)
    {
        if (!IsServer)
            return;

        initializedOnServer = true;
        networkPoints.Value = Mathf.Max(0, value);
    }

    public void AddServerPoints(int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        networkPoints.Value = Mathf.Max(0, networkPoints.Value + amount);
    }

    public bool TrySpendServerPoints(int amount)
    {
        if (!IsServer || amount <= 0)
            return false;

        if (networkPoints.Value < amount)
            return false;

        networkPoints.Value -= amount;
        return true;
    }
}