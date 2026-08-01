using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public sealed class AmmoSpawnSlot
{
    public Transform spawnPoint;
    public BulletDefinition bullet;
}

public sealed class LimitedAmmoBox : NetworkBehaviour
{
    [Header("Spawn Slots")]
    [SerializeField] private GameObject bulletPickupPrefab;
    [SerializeField] private AmmoSpawnSlot[] slots;

    [Header("One Type Of Bullets")]
    [SerializeField] private bool oneTypeOnly;
    [SerializeField] private BulletDefinition singleBulletType;

    [Header("Spawn Timing")]
    [Min(0f)] [SerializeField] private float fallbackDelay = 5f;

    private bool pickupsSpawned;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            StartCoroutine(SpawnFallback());
        }
        else
        {
            SpawnInitialPickups();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (pickupsSpawned)
            return;

        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted)
            return;

        if (sceneEvent.SceneName != gameObject.scene.name)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        SpawnInitialPickups();
    }

    private IEnumerator SpawnFallback()
    {
        yield return new WaitForSeconds(fallbackDelay);

        if (!pickupsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

            SpawnInitialPickups();
        }
    }

    private void SpawnInitialPickups()
    {
        if (pickupsSpawned)
            return;

        pickupsSpawned = true;

        if (bulletPickupPrefab == null)
            return;

        if (slots == null || slots.Length == 0)
        {
            if (oneTypeOnly && singleBulletType != null)
                SpawnPickup(transform, singleBulletType);
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            AmmoSpawnSlot slot = slots[i];
            if (slot == null || slot.spawnPoint == null)
                continue;

            BulletDefinition bullet = oneTypeOnly ? singleBulletType : slot.bullet;
            if (bullet == null)
                continue;

            SpawnPickup(slot.spawnPoint, bullet);
        }
    }

    private void SpawnPickup(Transform parent, BulletDefinition bullet)
    {
        GameObject instance = Instantiate(bulletPickupPrefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        BulletPickup pickup = instance.GetComponent<BulletPickup>();
        if (pickup == null)
        {
            Destroy(instance);
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Destroy(instance);
            return;
        }

        pickup.InitializeServer(bullet);
        networkObject.Spawn(true);
    }
}
