using UnityEngine;

public enum DamageSourceType : byte
{
    Other,
    Projectile,
    Debug
}

/// <summary>
/// Server-side description of one damage event.
/// Collider references are local Unity references and are not sent over the network.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float BaseDamage;
    public readonly ulong AttackerClientId;
    public readonly Collider HitCollider;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
    public readonly DamageSourceType SourceType;
    public readonly string SourceId;
    public readonly float DeathTorque;

    public DamageInfo(
        float baseDamage,
        ulong attackerClientId,
        Collider hitCollider,
        Vector3 hitPoint,
        Vector3 hitNormal,
        DamageSourceType sourceType,
        string sourceId = null,
        float deathTorque = 0f)
    {
        BaseDamage = Mathf.Max(0f, baseDamage);
        AttackerClientId = attackerClientId;
        HitCollider = hitCollider;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        SourceType = sourceType;
        SourceId = sourceId ?? string.Empty;
        DeathTorque = deathTorque;
    }
}
