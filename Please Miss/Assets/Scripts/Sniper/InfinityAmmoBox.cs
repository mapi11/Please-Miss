using Unity.Netcode;
using UnityEngine;

public sealed class InfinityAmmoBox : Interactable
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

        SniperWeaponController weapon = player.GetComponent<SniperWeaponController>();
        if (weapon != null)
            weapon.RefillFromAmmoBoxServerRpc(new Unity.Collections.FixedString64Bytes(bulletDefinition.BulletId));

        StartCoroutine(ReleaseAfterDelay(player));
    }

    private System.Collections.IEnumerator ReleaseAfterDelay(PlayerController player)
    {
        yield return new WaitForSeconds(releaseDelay);
        if (player != null)
            player.ReleaseCurrentInteractable();
    }
}
