using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class BulletPickup : Interactable
{
    [Header("Visual")]
    [SerializeField] private Renderer[] headRenderers;

    [Header("Auto Release")]
    [Min(0f)] [SerializeField] private float releaseDelay = 0.3f;

    private readonly NetworkVariable<FixedString64Bytes> bulletId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Color> headColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private MaterialPropertyBlock propertyBlock;

    public void InitializeServer(BulletDefinition bullet)
    {
        if (bullet == null)
            return;

        bulletId.Value = new FixedString64Bytes(bullet.BulletId);
        headColor.Value = bullet.HeadColor;
        ApplyVisual();
    }

    public override void OnNetworkSpawn()
    {
        bulletId.OnValueChanged += OnBulletDataChanged;
        headColor.OnValueChanged += OnBulletDataChanged;
        ApplyVisual();
    }

    public override void OnNetworkDespawn()
    {
        bulletId.OnValueChanged -= OnBulletDataChanged;
        headColor.OnValueChanged -= OnBulletDataChanged;
    }

    private void OnBulletDataChanged<T>(T previousValue, T newValue)
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (headRenderers == null || headRenderers.Length == 0)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Color color = headColor.Value;
        foreach (Renderer renderer in headRenderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player) || player == null)
            return false;

        SniperWeaponController weapon = player.GetComponent<SniperWeaponController>();
        PlayerRoleState role = player.GetComponent<PlayerRoleState>();

        return weapon != null && weapon.HasRifleEquipped && role != null && role.IsSniper;
    }

    public override void OnHandBegin(PlayerController player)
    {
        if (player == null)
            return;

        NetworkObject playerNetworkObject = player.GetComponent<NetworkObject>();
        if (playerNetworkObject != null)
            PickupServerRpc(playerNetworkObject);

        if (player != null)
            player.StartCoroutine(ReleaseAfterDelay(player));
    }

    private IEnumerator ReleaseAfterDelay(PlayerController player)
    {
        yield return new WaitForSeconds(releaseDelay);
        if (player != null)
            player.ReleaseCurrentInteractable();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickupServerRpc(NetworkObjectReference playerReference)
    {
        if (!playerReference.TryGet(out NetworkObject playerNetworkObject))
            return;

        PlayerController player = playerNetworkObject != null
            ? playerNetworkObject.GetComponent<PlayerController>()
            : null;

        if (player == null)
            return;

        SniperWeaponController weapon = player.GetComponent<SniperWeaponController>();
        PlayerRoleState role = player.GetComponent<PlayerRoleState>();
        if (weapon == null || role == null || !role.IsSniper || !weapon.HasRifleEquipped)
            return;

        if (weapon.ServerRefillFromAmmoBox(bulletId.Value))
            NetworkObject.Despawn(true);
    }
}
