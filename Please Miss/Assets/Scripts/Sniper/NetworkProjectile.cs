using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkProjectile : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private Renderer[] bulletHeadRenderers;
    [SerializeField] private WeaponContentDatabase contentDatabase;

    [Header("Collision")]
    [SerializeField] private Collider hitCollider;
    [SerializeField] private LayerMask hitMask = ~0;
    [Min(0.1f)] [SerializeField] private float maximumLifetime = 15f;
    [SerializeField] private int bulletLayer = -1;

    private float cachedRadius;

    private readonly NetworkVariable<FixedString64Bytes> bulletId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Color> bulletColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly HashSet<ulong> nearMissedPlayers = new HashSet<ulong>();
    private MaterialPropertyBlock propertyBlock;

    private float currentSpeed;
    private float accelerationPerSecond;
    private float damage;
    private float deathTorque;
    private float lifeRemaining;
    private ulong attackerClientId;
    private bool initialized;


    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        CacheColliderRadius();
    }

    private void CacheColliderRadius()
    {
        if (hitCollider == null)
        {
            cachedRadius = 0f;
            return;
        }

        if (hitCollider is SphereCollider sphere)
        {
            Vector3 scale = hitCollider.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            cachedRadius = sphere.radius * maxScale;
        }
        else if (hitCollider is CapsuleCollider capsule)
        {
            Vector3 scale = hitCollider.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            cachedRadius = capsule.radius * maxScale;
        }
        else
        {
            cachedRadius = hitCollider.bounds.extents.magnitude;
        }
    }

    public override void OnNetworkSpawn()
    {
        hitMask &= ~GameLayers.InvisibleWallMask;

        if (bulletLayer >= 0)
            gameObject.layer = bulletLayer;

        if (hitCollider != null && IsServer)
            IgnoreCharacterControllerCollisions();

        bulletId.OnValueChanged += OnBulletIdChanged;
        bulletColor.OnValueChanged += OnBulletColorChanged;
        ApplyVisual();
    }

    private void IgnoreCharacterControllerCollisions()
    {
        var controllers = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        foreach (var cc in controllers)
        {
            if (cc != null && cc.gameObject.scene.isLoaded)
                Physics.IgnoreCollision(hitCollider, cc, true);
        }
    }

    public override void OnNetworkDespawn()
    {
        bulletId.OnValueChanged -= OnBulletIdChanged;
        bulletColor.OnValueChanged -= OnBulletColorChanged;
    }

    public void InitializeServer(
        BulletDefinition definition,
        float rifleMuzzleVelocity,
        ulong shooterClientId,
        float torque)
    {
        if (!IsServer || definition == null)
            return;

        bulletId.Value = new FixedString64Bytes(definition.BulletId);
        bulletColor.Value = definition.HeadColor;
        currentSpeed = rifleMuzzleVelocity * definition.SpeedMultiplier;
        accelerationPerSecond = definition.AccelerationPerSecond;
        damage = definition.Damage;
        deathTorque = torque;
        attackerClientId = shooterClientId;
        lifeRemaining = maximumLifetime;
        initialized = true;

        ApplyVisual();
    }

    private void Update()
    {
        if (!IsServer || !initialized)
            return;

        float deltaTime = Time.deltaTime;
        lifeRemaining -= deltaTime;

        if (lifeRemaining <= 0f)
        {
            DespawnProjectile();
            return;
        }

        currentSpeed += accelerationPerSecond * deltaTime;
        float distance = currentSpeed * deltaTime;
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        if (TryFindNearestValidHit(origin, direction, distance, out RaycastHit hit))
        {
            transform.position = hit.point;

            IDamageable damageable = FindDamageable(hit.collider);
            if (damageable != null)
            {
                DamageInfo damageInfo = new DamageInfo(
                    damage,
                    attackerClientId,
                    hit.collider,
                    hit.point,
                    hit.normal,
                    DamageSourceType.Projectile,
                    bulletId.Value.ToString(),
                    deathTorque
                );

                damageable.TakeDamage(in damageInfo);

                PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (hitHealth != null)
                    nearMissedPlayers.Remove(hitHealth.OwnerClientId);
            }

            DespawnProjectile();
            return;
        }

        Vector3 newPos = origin + direction * distance;
        transform.position = newPos;
    }

    private bool TryFindNearestValidHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit nearestHit)
    {
        int count;
        if (cachedRadius > 0f)
        {
            count = Physics.SphereCastNonAlloc(
                origin,
                cachedRadius,
                direction,
                hitBuffer,
                distance,
                hitMask,
                QueryTriggerInteraction.Collide
            );
        }
        else
        {
            count = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                distance,
                hitMask,
                QueryTriggerInteraction.Collide
            );
        }

        float nearestDistance = float.MaxValue;
        nearestHit = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = hitBuffer[i];
            if (candidate.collider == null)
                continue;

            if (candidate.collider.GetComponent<CharacterController>() != null)
                continue;

            if (candidate.collider.transform.IsChildOf(transform))
                continue;

            NetworkObject hitNetworkObject = candidate.collider.GetComponentInParent<NetworkObject>();
            if (hitNetworkObject != null && hitNetworkObject.OwnerClientId == attackerClientId)
                continue;

            PlayerHealth health = candidate.collider.GetComponentInParent<PlayerHealth>();
            if (health != null && health.NearMissCollider == candidate.collider)
            {
                RecordNearMiss(health);
                continue;
            }

            if (candidate.distance >= nearestDistance)
                continue;

            nearestDistance = candidate.distance;
            nearestHit = candidate;
            found = true;
        }

        return found;
    }

    private void RecordNearMiss(PlayerHealth health)
    {
        if (health == null || !health.IsSpawned || health.IsDead)
            return;

        nearMissedPlayers.Add(health.OwnerClientId);
    }

    private void AwardNearMissBonuses()
    {
        if (nearMissedPlayers.Count == 0)
            return;

        int reward = GameManager.Instance != null ? GameManager.Instance.NearMissReward : 0;
        if (reward <= 0)
        {
            nearMissedPlayers.Clear();
            return;
        }

        foreach (ulong clientId in nearMissedPlayers)
        {
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            NearMissBonusClientRpc(reward, rpcParams);
        }

        nearMissedPlayers.Clear();
    }

    [ClientRpc]
    private void NearMissBonusClientRpc(int reward, ClientRpcParams rpcParams = default)
    {
        if (reward <= 0) return;

        LocalPlayerSettings.AddPoints(reward);

        if (GameManager.Instance != null)
            GameManager.Instance.NotifyNearMissReward(reward);
    }

    private void OnBulletIdChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        ApplyVisual();
    }

    private void OnBulletColorChanged(Color oldValue, Color newValue)
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Color color = bulletColor.Value;
        BulletDefinition definition = contentDatabase != null
            ? contentDatabase.GetBullet(bulletId.Value.ToString())
            : null;

        if (definition != null)
            color = definition.HeadColor;

        if (bulletHeadRenderers == null)
            return;

        foreach (Renderer targetRenderer in bulletHeadRenderers)
        {
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private static IDamageable FindDamageable(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageable damageable)
                return damageable;
        }

        return null;
    }

    private void DespawnProjectile()
    {
        AwardNearMissBonuses();

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}
