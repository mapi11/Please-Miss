public sealed class SniperRiflePickableItem : PickableItem
{
    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player) || player == null)
            return false;

        PlayerRoleState roleState = player.GetComponent<PlayerRoleState>();
        return roleState != null && roleState.IsSniper;
    }

    public override void OnHandBegin(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        base.OnHandBegin(player);
    }
}
