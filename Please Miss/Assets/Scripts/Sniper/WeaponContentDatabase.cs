using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponContentDatabase", menuName = "Game/Weapons/Content Database")]
public sealed class WeaponContentDatabase : ScriptableObject
{
    [SerializeField] private BulletDefinition[] bullets;
    [SerializeField] private SniperRifleDefinition[] rifles;

    private Dictionary<string, BulletDefinition> bulletById;
    private Dictionary<string, SniperRifleDefinition> rifleById;

    public BulletDefinition GetBullet(string id)
    {
        EnsureCache();
        return !string.IsNullOrEmpty(id) && bulletById.TryGetValue(id, out BulletDefinition value)
            ? value
            : null;
    }

    public SniperRifleDefinition GetRifle(string id)
    {
        EnsureCache();
        return !string.IsNullOrEmpty(id) && rifleById.TryGetValue(id, out SniperRifleDefinition value)
            ? value
            : null;
    }

    private void OnEnable()
    {
        BuildCache();
    }

    private void OnValidate()
    {
        BuildCache();
    }

    private void EnsureCache()
    {
        if (bulletById == null || rifleById == null)
            BuildCache();
    }

    private void BuildCache()
    {
        bulletById = new Dictionary<string, BulletDefinition>(StringComparer.Ordinal);
        rifleById = new Dictionary<string, SniperRifleDefinition>(StringComparer.Ordinal);

        if (bullets != null)
        {
            foreach (BulletDefinition bullet in bullets)
            {
                if (bullet == null || string.IsNullOrWhiteSpace(bullet.BulletId))
                    continue;

                bulletById[bullet.BulletId] = bullet;
            }
        }

        if (rifles != null)
        {
            foreach (SniperRifleDefinition rifle in rifles)
            {
                if (rifle == null || string.IsNullOrWhiteSpace(rifle.RifleId))
                    continue;

                rifleById[rifle.RifleId] = rifle;
            }
        }
    }
}
