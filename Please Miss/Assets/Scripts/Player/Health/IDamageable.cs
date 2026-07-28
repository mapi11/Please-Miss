public interface IDamageable
{
    /// <summary>
    /// Must be called on the server/authority that owns gameplay state.
    /// </summary>
    void TakeDamage(in DamageInfo damageInfo);
}
