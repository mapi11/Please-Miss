using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Compatibility adapter for gameplay scripts such as SniperWeaponController and PlayerHealth.
/// It does not store a second network role. It reads the role selected in NetworkPlayerRole.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerRole))]
public sealed class PlayerRoleState : NetworkBehaviour
{
    [Header("Role source")]
    [SerializeField] private NetworkPlayerRole networkPlayerRole;

    [Header("Used only before networking starts")]
    [SerializeField] private PlayerRole offlineRole = PlayerRole.None;

    [Header("Temporary host testing")]
    [SerializeField] private bool makeHostPlayerSniperForTesting;

    public PlayerRole CurrentRole
    {
        get
        {
            if (networkPlayerRole != null && networkPlayerRole.IsSpawned)
                return networkPlayerRole.CurrentRole;

            return offlineRole;
        }
    }

    public bool IsSniper => CurrentRole == PlayerRole.Sniper;
    public bool IsRunner => CurrentRole == PlayerRole.Runner;
    public NetworkPlayerRole NetworkRole => networkPlayerRole;

    private void Awake()
    {
        ResolveRoleSource();
    }

    public override void OnNetworkSpawn()
    {
        ResolveRoleSource();

        if (IsServer &&
            makeHostPlayerSniperForTesting &&
            OwnerClientId == NetworkManager.ServerClientId &&
            networkPlayerRole != null)
        {
            networkPlayerRole.ServerSetRole(PlayerRole.Sniper);
        }
    }

    public void ServerSetRole(PlayerRole role)
    {
        ResolveRoleSource();

        if (!IsServer)
        {
            Debug.LogWarning("ServerSetRole can only be called on the server.", this);
            return;
        }

        if (networkPlayerRole == null)
        {
            Debug.LogError("NetworkPlayerRole was not found on the Player Prefab.", this);
            return;
        }

        networkPlayerRole.ServerSetRole(role);
    }

    private void ResolveRoleSource()
    {
        if (networkPlayerRole == null)
            networkPlayerRole = GetComponent<NetworkPlayerRole>();
    }
}
