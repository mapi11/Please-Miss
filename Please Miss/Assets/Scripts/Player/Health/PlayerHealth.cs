using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum HitZoneDamageMode : byte
{
    /// <summary>Final damage = bullet damage * Damage Value.</summary>
    BulletDamageMultiplier,

    /// <summary>Final damage = Damage Value, ignoring the bullet's base damage.</summary>
    FixedDamage
}

public enum BodyZoneType : byte
{
    [InspectorName("Left Hand")] LeftHand,
    [InspectorName("Right Hand")] RightHand,
    [InspectorName("Head")] Head,
    [InspectorName("Body")] Body,
    [InspectorName("Left Eye")] LeftEye,
    [InspectorName("Right Eye")] RightEye
}

[Serializable]
public sealed class PlayerHitZone
{
    [Tooltip("Часть тела, в которую попадает пуля")]
    [SerializeField] private BodyZoneType zoneType = BodyZoneType.Body;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private HitZoneDamageMode damageMode = HitZoneDamageMode.BulletDamageMultiplier;
    [Min(0f)] [SerializeField] private float damageValue = 1f;
    [Tooltip("Дополнительные очки за убийство в эту часть тела")]
    [Min(0)] [SerializeField] private int killPoints = 0;

    public string ZoneName => GetZoneDisplayName(zoneType);
    public BodyZoneType ZoneType => zoneType;
    public Collider HitCollider => hitCollider;
    public HitZoneDamageMode DamageMode => damageMode;
    public float DamageValue => Mathf.Max(0f, damageValue);
    public int KillPoints => Mathf.Max(0, killPoints);

    public static string GetZoneDisplayName(BodyZoneType type)
    {
        switch (type)
        {
            case BodyZoneType.LeftHand: return "Left Hand";
            case BodyZoneType.RightHand: return "Right Hand";
            case BodyZoneType.Head: return "Head";
            case BodyZoneType.Body: return "Body";
            case BodyZoneType.LeftEye: return "Left Eye";
            case BodyZoneType.RightEye: return "Right Eye";
            default: return "Body";
        }
    }

    public float CalculateDamage(float bulletDamage)
    {
        return damageMode == HitZoneDamageMode.FixedDamage
            ? DamageValue
            : Mathf.Max(0f, bulletDamage) * DamageValue;
    }
}

/// <summary>
/// Server-authoritative player health with configurable collider hit zones.
/// Add this component to the root NetworkObject of the player.
/// </summary>
public sealed class PlayerHealth : NetworkBehaviour, IDamageable
{
    [Header("Health")]
    [Min(1f)] [SerializeField] private float maximumHealth = 100f;
    [SerializeField] private bool startAtFullHealth = true;

    [Header("Role rules")]
    [SerializeField] private PlayerRoleState roleState;
    [Tooltip("When enabled, projectile damage is applied only to Runner players.")]
    [SerializeField] private bool projectilesDamageOnlyRunners = true;
    [Tooltip("Useful before the role-selection system is ready.")]
    [SerializeField] private bool allowProjectileDamageWhenRoleIsNoneForTesting = true;
    [SerializeField] private bool preventSelfDamage = true;

    [Header("Hit zones")]
    [Tooltip("The exact collider struck by the projectile is looked up in this list.")]
    [SerializeField] private List<PlayerHitZone> hitZones = new List<PlayerHitZone>();
    [Tooltip("Trigger-коллайдер \"близкий пролёт\". Если пуля пересекла его, но не попала в игрока, игрок получает бонусные очки")]
    [SerializeField] private Collider nearMissCollider;

    [Header("Audio")]
    [SerializeField] private PlayerSfx playerSfx;

    [Header("Inspector test buttons")]
    [Min(0.1f)] [SerializeField] private float debugHealthStep = 10f;

    private readonly NetworkVariable<float> networkHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> networkDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> networkDeathTorque = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float offlineHealth;
    private bool offlineDead;
    private float offlineDeathTorque;

    public event Action<float, float> OnHealthChanged;
    public event Action<bool> OnDeathStateChanged;
    public event Action<DamageInfo, float, string> OnDamageAppliedOnServer;

    public float MaximumHealth => maximumHealth;
    public float CurrentHealth => IsSpawned ? networkHealth.Value : offlineHealth;
    public float NormalizedHealth => maximumHealth <= 0f ? 0f : CurrentHealth / maximumHealth;
    public bool IsDead => IsSpawned ? networkDead.Value : offlineDead;
    public float LastDeathTorque => IsSpawned ? networkDeathTorque.Value : offlineDeathTorque;
    public float DebugHealthStep => debugHealthStep;
    public IReadOnlyList<PlayerHitZone> HitZones => hitZones;
    public Collider NearMissCollider => nearMissCollider;

    private void Awake()
    {
        if (roleState == null)
            roleState = GetComponent<PlayerRoleState>();

        if (playerSfx == null)
            playerSfx = GetComponent<PlayerSfx>();

        OnDeathStateChanged += HandleDeathStateChanged;

        offlineHealth = startAtFullHealth ? maximumHealth : Mathf.Clamp(offlineHealth, 0f, maximumHealth);
        offlineDead = offlineHealth <= 0f;
    }

    private void HandleDeathStateChanged(bool dead)
    {
        if (dead)
            playerSfx?.PlayDeath();
    }

    private void OnValidate()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
        debugHealthStep = Mathf.Max(0.1f, debugHealthStep);

        if (!Application.isPlaying)
        {
            offlineHealth = startAtFullHealth
                ? maximumHealth
                : Mathf.Clamp(offlineHealth, 0f, maximumHealth);
            offlineDead = offlineHealth <= 0f;
        }

        WarnAboutInvalidHitZones();
    }

    public override void OnNetworkSpawn()
    {
        networkHealth.OnValueChanged += HandleNetworkHealthChanged;
        networkDead.OnValueChanged += HandleNetworkDeadChanged;

        if (IsServer)
        {
            float initialHealth = startAtFullHealth
                ? maximumHealth
                : Mathf.Clamp(offlineHealth, 0f, maximumHealth);

            SetHealthOnServer(initialHealth);
        }

        OnHealthChanged?.Invoke(CurrentHealth, maximumHealth);
        OnDeathStateChanged?.Invoke(IsDead);
    }

    public override void OnNetworkDespawn()
    {
        networkHealth.OnValueChanged -= HandleNetworkHealthChanged;
        networkDead.OnValueChanged -= HandleNetworkDeadChanged;
    }

    public void TakeDamage(in DamageInfo damageInfo)
    {
        if (!IsServer || networkDead.Value || damageInfo.BaseDamage <= 0f)
            return;

        if (preventSelfDamage && damageInfo.AttackerClientId == OwnerClientId)
            return;

        if (damageInfo.SourceType == DamageSourceType.Projectile && !CanReceiveProjectileDamage())
            return;

        float finalDamage = CalculateFinalDamage(
            damageInfo.BaseDamage,
            damageInfo.HitCollider,
            out string zoneName
        );

        if (finalDamage <= 0f)
            return;

        networkDeathTorque.Value = damageInfo.DeathTorque;
        SetHealthOnServer(networkHealth.Value - finalDamage);
        OnDamageAppliedOnServer?.Invoke(damageInfo, finalDamage, zoneName);
    }

    public void ServerAddHealth(float amount)
    {
        if (!IsServer || amount <= 0f || networkDead.Value)
            return;

        SetHealthOnServer(networkHealth.Value + amount);
    }

    public void ServerRestoreFullHealth()
    {
        if (!IsServer)
            return;

        SetHealthOnServer(maximumHealth);
    }

    public void ServerSetHealth(float value)
    {
        if (!IsServer)
            return;

        SetHealthOnServer(value);
    }

    public float CalculateFinalDamage(float bulletDamage, Collider hitCollider, out string zoneName)
    {
        PlayerHitZone zone = FindHitZone(hitCollider);
        if (zone != null)
        {
            zoneName = zone.ZoneName;
            return zone.CalculateDamage(bulletDamage);
        }

        zoneName = "None";
        if (hitCollider != null)
        {
            Debug.LogWarning(
                $"Hit on collider '{hitCollider.name}' (on '{hitCollider.transform.parent?.name}') " +
                $"is not listed in Hit Zones on '{name}'. No damage applied.",
                this
            );
        }
        return 0f;
    }

    public PlayerHitZone FindHitZone(Collider hitCollider)
    {
        if (hitCollider == null || hitZones == null)
            return null;

        for (int i = 0; i < hitZones.Count; i++)
        {
            PlayerHitZone zone = hitZones[i];
            if (zone != null && zone.HitCollider == hitCollider)
                return zone;
        }

        return null;
    }

    public void DebugRemoveHealth()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RequestDebugHealthChange(-debugHealthStep);
#endif
    }

    public void DebugAddHealth()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RequestDebugHealthChange(debugHealthStep);
#endif
    }

    public void DebugKill()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RequestDebugSetHealth(0f);
#endif
    }

    public void DebugRestoreFullHealth()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RequestDebugSetHealth(maximumHealth);
#endif
    }

    private bool CanReceiveProjectileDamage()
    {
        if (!projectilesDamageOnlyRunners)
            return true;

        if (roleState == null)
            return allowProjectileDamageWhenRoleIsNoneForTesting;

        return roleState.CurrentRole == PlayerRole.Runner ||
               (allowProjectileDamageWhenRoleIsNoneForTesting && roleState.CurrentRole == PlayerRole.None);
    }

    private void SetHealthOnServer(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maximumHealth);
        networkHealth.Value = clamped;
        networkDead.Value = clamped <= 0f;
    }

    private void SetOfflineHealth(float value, float deathTorque = 0f)
    {
        float previousHealth = offlineHealth;
        bool previousDead = offlineDead;

        offlineHealth = Mathf.Clamp(value, 0f, maximumHealth);
        offlineDead = offlineHealth <= 0f;
        if (offlineDead)
            offlineDeathTorque = deathTorque;

        if (!Mathf.Approximately(previousHealth, offlineHealth))
            OnHealthChanged?.Invoke(offlineHealth, maximumHealth);

        if (previousDead != offlineDead)
            OnDeathStateChanged?.Invoke(offlineDead);
    }

    private void RequestDebugHealthChange(float delta)
    {
        if (!Application.isPlaying)
            return;

        if (!IsSpawned)
        {
            SetOfflineHealth(offlineHealth + delta);
            return;
        }

        if (IsServer)
        {
            SetHealthOnServer(networkHealth.Value + delta);
            return;
        }

        DebugChangeHealthRpc(delta);
    }

    private void RequestDebugSetHealth(float value)
    {
        if (!Application.isPlaying)
            return;

        if (!IsSpawned)
        {
            SetOfflineHealth(value);
            return;
        }

        if (IsServer)
        {
            SetHealthOnServer(value);
            return;
        }

        DebugSetHealthRpc(value);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DebugChangeHealthRpc(float delta)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SetHealthOnServer(networkHealth.Value + delta);
#endif
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DebugSetHealthRpc(float value)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SetHealthOnServer(value);
#endif
    }

    private void HandleNetworkHealthChanged(float previousValue, float newValue)
    {
        if (newValue < previousValue && IsOwner)
            playerSfx?.PlayDamage();

        OnHealthChanged?.Invoke(newValue, maximumHealth);
    }

    private void HandleNetworkDeadChanged(bool previousValue, bool newValue)
    {
        OnDeathStateChanged?.Invoke(newValue);
    }

    private void WarnAboutInvalidHitZones()
    {
        if (hitZones == null)
            return;

        HashSet<Collider> usedColliders = new HashSet<Collider>();
        for (int i = 0; i < hitZones.Count; i++)
        {
            PlayerHitZone zone = hitZones[i];
            if (zone == null || zone.HitCollider == null)
                continue;

            if (!zone.HitCollider.transform.IsChildOf(transform) && zone.HitCollider.transform != transform)
            {
                Debug.LogWarning(
                    $"Hit zone '{zone.ZoneName}' references a collider outside player '{name}'.",
                    this
                );
            }

            if (!usedColliders.Add(zone.HitCollider))
            {
                Debug.LogWarning(
                    $"Collider '{zone.HitCollider.name}' is listed more than once in PlayerHealth on '{name}'.",
                    this
                );
            }
        }
    }
}
