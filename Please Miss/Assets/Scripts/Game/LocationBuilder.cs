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
    private readonly List<Transform> built = new();

    private void Start()
    {
        if (buildOnStart)
            Build();
    }

    private void Update()
    {
        if (!networkSetupPending)
            return;

        if (NetworkManager.Singleton == null)
            return;

        networkSetupPending = false;
        SetupNestedNetworkObjects();
    }

    [ContextMenu("Build")]
    public void Build()
    {
        Clear();

        if (types == null || types.Length == 0 || sequence == null || sequence.Length == 0)
        {
            Debug.LogError("LocationBuilder: types or sequence is empty", this);
            return;
        }

        int seed = randomSeed;

        if (seed == 0)
        {
            seed = UnityEngine.Random.Range(1, int.MaxValue);
            randomSeed = seed;
        }

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
        for (int i = 0; i < built.Count; i++)
        {
            if (built[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(built[i].gameObject);
            else
                DestroyImmediate(built[i].gameObject);
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

    private void SpawnAttachments(Transform segmentRoot)
    {
        var locationPrefab = segmentRoot.GetComponent<LocationPrefab>();
        if (locationPrefab == null || locationPrefab.Attachments == null)
            return;

        foreach (var attachment in locationPrefab.Attachments)
        {
            if (attachment == null || attachment.prefab == null || attachment.slot == null)
                continue;

            GameObject instance = Instantiate(attachment.prefab, attachment.slot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Destroy(instance);
                continue;
            }

            networkObject.Spawn();
        }
    }

    private void SetupNestedNetworkObjects()
    {
        if (NetworkManager.Singleton == null)
            return;

        bool isServer = NetworkManager.Singleton.IsServer;

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

                    networkObject.Spawn();
                }

                SpawnAttachments(built[i]);
            }
            else
            {
                foreach (var networkObject in networkObjects)
                    networkObject.gameObject.SetActive(false);
            }
        }
    }
}
