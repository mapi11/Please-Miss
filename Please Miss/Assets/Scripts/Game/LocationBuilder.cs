using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LocationBuilder : MonoBehaviour
{
    [Serializable]
    public class LocationType
    {
        public string typeName;
        public LocationPrefab[] variants;
    }

    [Serializable]
    public class LocationSlot
    {
        public string typeName;
    }

    [Header("Types")]
    [SerializeField] private LocationType[] types;

    [Header("Sequence")]
    [SerializeField] private LocationSlot[] sequence;

    [Header("Options")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private int randomSeed;

    private bool networkSetupPending;
    private bool offlineMode;
    private bool builtOnce;
    private float offlineWait;
    private readonly List<Transform> built = new();
    private readonly List<GameObject> localAttachments = new();

    private void Update()
    {
        if (NetworkManager.Singleton != null && offlineMode)
        {
            builtOnce = false;
            RestoreFromOfflinePreview();
            Clear();
            return;
        }

        if (networkSetupPending)
        {
            ProcessNetworkSetup();
            return;
        }

        if (!buildOnStart || builtOnce)
            return;

        int seed = ResolveSeed();

        if (seed != 0)
        {
            builtOnce = true;
            Build(seed);
            networkSetupPending = true;
        }
    }

    private void ProcessNetworkSetup()
    {
        if (NetworkManager.Singleton != null)
        {
            networkSetupPending = false;
            SetupNestedNetworkObjects();
            return;
        }

        offlineWait += Time.deltaTime;
        if (offlineWait >= 2f)
        {
            networkSetupPending = false;
            offlineMode = true;
            SetupNestedNetworkObjects();
        }
    }

    private int ResolveSeed()
    {
        if (NetworkManager.Singleton == null)
        {
            if (randomSeed != 0)
                return randomSeed;

            randomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            return randomSeed;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            if (GameManager.Instance == null)
                return 0;

            if (GameManager.Instance.LocationSeed.Value == 0)
            {
                GameManager.Instance.LocationSeed.Value =
                    (uint)(randomSeed != 0 ? randomSeed : UnityEngine.Random.Range(1, int.MaxValue));
            }

            return (int)GameManager.Instance.LocationSeed.Value;
        }

        if (GameManager.Instance != null && GameManager.Instance.LocationSeed.Value != 0)
            return (int)GameManager.Instance.LocationSeed.Value;

        return 0;
    }

    private void RestoreFromOfflinePreview()
    {
        offlineMode = false;

        for (int i = 0; i < localAttachments.Count; i++)
        {
            if (localAttachments[i] != null)
                Destroy(localAttachments[i]);
        }

        localAttachments.Clear();
    }

    [ContextMenu("Build")]
    public void Build()
    {
        if (Application.isPlaying)
        {
            int seed = ResolveSeed();
            if (seed == 0)
            {
                Debug.LogWarning("LocationBuilder: waiting for synced location seed", this);
                return;
            }

            builtOnce = true;
            Build(seed);
            networkSetupPending = true;
            return;
        }

        Build(randomSeed != 0 ? randomSeed : UnityEngine.Random.Range(1, int.MaxValue));
        networkSetupPending = true;
    }

    public void Build(int seed)
    {
        Clear();

        if (types == null || types.Length == 0 || sequence == null || sequence.Length == 0)
        {
            Debug.LogError("LocationBuilder: types or sequence is empty", this);
            return;
        }

        if (seed == 0)
            seed = UnityEngine.Random.Range(1, int.MaxValue);

        Debug.Log($"LocationBuilder: building with seed {seed}", this);
        var random = new System.Random(seed);
        Transform reference = transform;

        for (int i = 0; i < sequence.Length; i++)
        {
            var slot = sequence[i];

            if (slot == null || string.IsNullOrEmpty(slot.typeName))
            {
                Debug.LogError($"LocationBuilder: slot {i} has no typeName", this);
                continue;
            }

            var type = FindType(slot.typeName);

            if (type == null)
            {
                Debug.LogError($"LocationBuilder: type '{slot.typeName}' not found", this);
                continue;
            }

            if (type.variants == null || type.variants.Length == 0)
            {
                Debug.LogError($"LocationBuilder: type '{slot.typeName}' has no variants", this);
                continue;
            }

            var variant = type.variants[random.Next(type.variants.Length)];

            if (variant == null)
            {
                Debug.LogError($"LocationBuilder: variant of '{slot.typeName}' is null", this);
                continue;
            }

            var instance = Instantiate(variant);
            instance.transform.SetParent(transform, false);

            SnapTo(instance, reference);
            built.Add(instance.transform);

            reference = instance.EndPoint != null ? instance.EndPoint : instance.transform;
        }

        networkSetupPending = true;
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        for (int i = 0; i < built.Count; i++)
        {
            if (built[i] == null)
                continue;

            if (Application.isPlaying)
            {
                if (isServer)
                {
                    var networkObjects = built[i].GetComponentsInChildren<NetworkObject>(true);
                    foreach (var networkObject in networkObjects)
                    {
                        if (networkObject != null && networkObject.IsSpawned)
                            networkObject.Despawn();
                    }
                }

                Destroy(built[i].gameObject);
            }
            else
            {
                DestroyImmediate(built[i].gameObject);
            }
        }

        built.Clear();
    }

    private LocationType FindType(string name)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] != null && types[i].typeName == name)
                return types[i];
        }

        return null;
    }

    private void SnapTo(LocationPrefab segment, Transform reference)
    {
        Transform root = segment.transform;
        Transform start = segment.StartPoint != null ? segment.StartPoint : root;

        Vector3 localStart = root.InverseTransformPoint(start.position);
        Quaternion align = Quaternion.FromToRotation(start.forward, reference.forward);

        root.rotation = align * root.rotation;
        root.position = reference.position - root.rotation * localStart;
    }

    private GameObject InstantiateAttachment(LocationPrefab.Attachment attachment)
    {
        if (attachment == null || attachment.slot == null)
            return null;

        if (attachment.prefabs == null || attachment.prefabs.Length == 0)
            return null;

        if (UnityEngine.Random.value * 100f > attachment.spawnChance)
            return null;

        GameObject prefab = attachment.prefabs[UnityEngine.Random.Range(0, attachment.prefabs.Length)];
        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, attachment.slot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Debug.Log($"LocationBuilder: spawned '{instance.name}' at '{attachment.slot.name}'", instance);
        return instance;
    }

    private void SpawnAttachments(Transform segmentRoot)
    {
        var locationPrefab = segmentRoot.GetComponent<LocationPrefab>();
        if (locationPrefab == null || locationPrefab.Attachments == null)
            return;

        foreach (var attachment in locationPrefab.Attachments)
        {
            GameObject instance = InstantiateAttachment(attachment);
            if (instance == null)
                continue;

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Destroy(instance);
                continue;
            }

            networkObject.Spawn(true);
        }
    }

    private void SpawnAttachmentsLocal(Transform segmentRoot)
    {
        var locationPrefab = segmentRoot.GetComponent<LocationPrefab>();
        if (locationPrefab == null || locationPrefab.Attachments == null)
            return;

        foreach (var attachment in locationPrefab.Attachments)
        {
            GameObject instance = InstantiateAttachment(attachment);
            if (instance != null)
                localAttachments.Add(instance);
        }
    }

    private void SetupNestedNetworkObjects()
    {
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        for (int i = 0; i < built.Count; i++)
        {
            if (built[i] == null)
                continue;

            var networkObjects = built[i].GetComponentsInChildren<NetworkObject>(true);

            if (isServer)
            {
                foreach (var networkObject in networkObjects)
                {
                    if (networkObject == null || networkObject.IsSpawned)
                        continue;

                    networkObject.Spawn(true);
                }

                SpawnAttachments(built[i]);
            }
            else if (NetworkManager.Singleton != null)
            {
                foreach (var networkObject in networkObjects)
                    networkObject.gameObject.SetActive(false);
            }
            else
            {
                SpawnAttachmentsLocal(built[i]);
            }
        }
    }
}
