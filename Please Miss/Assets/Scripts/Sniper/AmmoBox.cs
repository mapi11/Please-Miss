using Unity.Netcode;
using UnityEngine;

public sealed class AmmoBox : Interactable
{
    [Header("Ammo")]
    [SerializeField] private BulletDefinition bulletDefinition;
    [Min(0.1f)] [SerializeField] private float maximumUseDistance = 2.5f;
    [Min(0f)] [SerializeField] private float serverCooldown = 0.25f;

    [Header("Auto Release")]
    [Min(0f)] [SerializeField] private float releaseDelay = 0.3f;

    private double nextServerUseTime;

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player) || player == null || bulletDefinition == null)
            return false;

        SniperWeaponController weapon = player.GetComponent<SniperWeaponController>();
        PlayerRoleState role = player.GetComponent<PlayerRoleState>();

        return weapon != null && weapon.HasRifleEquipped && role != null && role.IsSniper;
    }

    public override void OnHandBegin(PlayerController player)
    {
        if (player == null || bulletDefinition == null)
            return;

        NetworkObject playerNetworkObject = player.GetComponent<NetworkObject>();
        if (IsSpawned && playerNetworkObject != null)
        {
            RefillRpc(playerNetworkObject);
            StartCoroutine(ReleaseAfterDelay(player));
            return;
        }

        SniperWeaponController weapon = player.GetComponent<SniperWeaponController>();
        if (weapon != null && weapon.IsServer)
            weapon.ServerRefill(bulletDefinition);

        StartCoroutine(ReleaseAfterDelay(player));
    }

    private System.Collections.IEnumerator ReleaseAfterDelay(PlayerController player)
    {
        yield return new WaitForSeconds(releaseDelay);
        if (player != null)
            player.ReleaseCurrentInteractable();
    }

    [Rpc(SendTo.Server)]
    private void RefillRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        if (NetworkManager.ServerTime.Time < nextServerUseTime)
            return;

        if (!playerReference.TryGet(out NetworkObject playerObject) || playerObject == null)
            return;

        if (playerObject.OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        if (Vector3.Distance(playerObject.transform.position, transform.position) > maximumUseDistance)
            return;

        SniperWeaponController weapon = playerObject.GetComponent<SniperWeaponController>();
        PlayerRoleState role = playerObject.GetComponent<PlayerRoleState>();

        if (weapon == null || role == null || !role.IsSniper)
            return;

        if (weapon.ServerRefill(bulletDefinition))
            nextServerUseTime = NetworkManager.ServerTime.Time + serverCooldown;
    }
}
