using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkInventorySync : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    public Inventory Inventory => inventory;
    public GameObject CurrentHeldVisual => currentHeldVisual;
    public int NetworkActiveSlot => networkActiveSlot.Value;
    public string NetworkActiveItemName => networkActiveItemName.Value.ToString();
    public event Action<GameObject> OnHeldVisualChanged;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform leftHoldPivot;
    [SerializeField] private Transform rightHoldPivot;
    [SerializeField] private Transform dropOrigin;

    [Header("Item Prefabs")]
    [SerializeField] private ItemEntry[] items;
    [SerializeField] private float heldItemScale = 0.8f;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingItemMappings = true;

    [System.Serializable]
    private class ItemEntry
    {
        public string Name;
        public GameObject WorldDropPrefab;
        public GameObject HeldVisualPrefab;
    }

    [Header("Throw")]
    [SerializeField] private Transform leftEjectPoint;
    [SerializeField] private Transform rightEjectPoint;
    [SerializeField] private float minThrowForce = 3f;
    [SerializeField] private float maxThrowForce = 15f;
    [SerializeField] private float throwBlockCheckRadius = 0.15f;
    [SerializeField] private LayerMask throwBlockMask = ~0;

    private readonly NetworkVariable<int> networkActiveSlot = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> networkActiveItemName = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes(""),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<byte> networkActiveHand = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameObject currentHeldVisual;

    private void OnValidate()
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].WorldDropPrefab == null)
                continue;

            var pickable = items[i].WorldDropPrefab.GetComponent<PickableItem>();
            if (pickable == null)
                continue;

            if (string.IsNullOrEmpty(items[i].Name) && !string.IsNullOrEmpty(pickable.ItemName))
                items[i].Name = pickable.ItemName;

            if (items[i].HeldVisualPrefab == null)
                items[i].HeldVisualPrefab = pickable.HeldVisualPrefab;
        }
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (inventory != null)
            inventory.OnActiveSlotChanged += OnLocalActiveSlotChanged;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnActiveSlotChanged -= OnLocalActiveSlotChanged;
    }

    public override void OnNetworkSpawn()
    {
        networkActiveSlot.OnValueChanged += OnActiveSlotChanged;
        networkActiveItemName.OnValueChanged += OnActiveItemNameChanged;
        networkActiveHand.OnValueChanged += OnActiveHandChanged;

        UpdateHeldVisual(
            networkActiveSlot.Value,
            networkActiveItemName.Value.ToString(),
            networkActiveHand.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        networkActiveSlot.OnValueChanged -= OnActiveSlotChanged;
        networkActiveItemName.OnValueChanged -= OnActiveItemNameChanged;
        networkActiveHand.OnValueChanged -= OnActiveHandChanged;
    }

    private void OnLocalActiveSlotChanged(int slot)
    {
        if (inventory == null)
            return;

        if (IsSpawned && networkActiveSlot.Value == slot)
            return;

        byte handIndex = playerController != null
            ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
            : (byte)0;

        string itemName = slot >= 0 ? inventory.GetItemAtSlot(slot) : null;

        UpdateHeldVisual(slot, itemName, handIndex);

        if (!IsSpawned)
            return;

        UpdateActiveSlotServerRpc(slot, new FixedString64Bytes(itemName ?? ""), handIndex);
    }

    [ServerRpc]
    private void UpdateActiveSlotServerRpc(int slot, FixedString64Bytes itemName, byte handIndex)
    {
        networkActiveSlot.Value = slot;
        networkActiveItemName.Value = itemName;
        networkActiveHand.Value = handIndex;
    }

    private void OnActiveSlotChanged(int oldValue, int newValue)
    {
        UpdateHeldVisual(newValue, networkActiveItemName.Value.ToString(), networkActiveHand.Value);
    }

    private void OnActiveItemNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        int slot = networkActiveSlot.Value;
        string itemName = newValue.ToString();

        UpdateHeldVisual(slot, itemName, networkActiveHand.Value);
    }

    private void OnActiveHandChanged(byte oldValue, byte newValue)
    {
        UpdateHeldVisual(networkActiveSlot.Value, networkActiveItemName.Value.ToString(), newValue);
    }

    private void UpdateHeldVisual(int slot, string itemName, byte handIndex)
    {
        DestroyCurrentHeldVisual();

        if (slot < 0 || string.IsNullOrEmpty(itemName))
            return;

        Transform pivot = handIndex == 0 ? rightHoldPivot : leftHoldPivot;

        if (pivot == null)
            return;

        GameObject prefab = inventory != null
            ? inventory.GetSlotHeldPrefab(slot)
            : null;

        if (prefab == null)
        {
            int prefabIndex = GetPrefabIndex(itemName);

            if (prefabIndex >= 0 && prefabIndex < items.Length)
            {
                prefab = items[prefabIndex].HeldVisualPrefab != null
                    ? items[prefabIndex].HeldVisualPrefab
                    : items[prefabIndex].WorldDropPrefab;
            }
        }

        if (prefab == null)
        {
            if (logMissingItemMappings)
            {
                Debug.LogWarning(
                    $"NetworkInventorySync could not find a Held Visual Prefab for item '{itemName}'. " +
                    "Add this item to NetworkInventorySync -> Items and assign Name, World Drop Prefab and Held Visual Prefab.",
                    this
                );
            }
            return;
        }

        currentHeldVisual = Instantiate(prefab, pivot);
        currentHeldVisual.transform.localPosition = Vector3.zero;
        currentHeldVisual.transform.localRotation = Quaternion.identity;
        currentHeldVisual.transform.localScale = Vector3.one * heldItemScale;

        var renderers = currentHeldVisual.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
            r.enabled = true;

        var rigidbodies = currentHeldVisual.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
            Destroy(rb);

        var colliders = currentHeldVisual.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
            Destroy(c);

        OnHeldVisualChanged?.Invoke(currentHeldVisual);
    }

    private void DestroyCurrentHeldVisual()
    {
        if (currentHeldVisual == null)
            return;

        Destroy(currentHeldVisual);
        currentHeldVisual = null;
        OnHeldVisualChanged?.Invoke(null);
    }

    public void LaunchActiveItem(float charge, Vector3 direction)
    {
        if (inventory == null)
            return;

        if (IsSpawned && !IsOwner)
            return;

        int slot = inventory.ActiveSlot;
        if (slot < 0)
            return;

        string itemName = inventory.GetItemAtSlot(slot);
        if (string.IsNullOrEmpty(itemName))
            return;

        Vector3 position = GetLaunchPosition();
        if (IsPositionBlocked(position))
            return;

        Vector3 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : transform.forward;
        Vector3 velocity = safeDirection * Mathf.Lerp(minThrowForce, maxThrowForce, charge);

        // This is a prefab asset reference stored by Inventory. Never call Destroy on it.
        GameObject heldVisualPrefab = inventory.GetSlotHeldPrefab(slot);

        DestroyCurrentHeldVisual();
        inventory.RemoveItem(slot);

        if (IsSpawned)
        {
            byte handIndex = playerController != null
                ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
                : (byte)0;

            LaunchServerRpc(slot, new FixedString64Bytes(itemName), handIndex, velocity);
            DestroyRuntimeHeldTemplateIfNeeded(heldVisualPrefab);
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(safeDirection, Vector3.up);
        int prefabIndex = GetPrefabIndex(itemName);
        GameObject dropObject;

        if (prefabIndex >= 0 && prefabIndex < items.Length && items[prefabIndex].WorldDropPrefab != null)
        {
            dropObject = Instantiate(items[prefabIndex].WorldDropPrefab, position, rotation);
        }
        else
        {
            dropObject = BuildDropItem(position, rotation, heldVisualPrefab, itemName);
        }

        Rigidbody body = dropObject.GetComponent<Rigidbody>();
        if (body != null)
            body.linearVelocity = velocity;

        DestroyRuntimeHeldTemplateIfNeeded(heldVisualPrefab);
    }

    private static void DestroyRuntimeHeldTemplateIfNeeded(GameObject heldVisualTemplate)
    {
        if (heldVisualTemplate == null)
            return;

        // Prefab assets have no valid Scene and must never be destroyed with Object.Destroy.
        // Generated runtime templates from PickableItem.BuildVisualFromSelf do have a valid Scene.
        if (heldVisualTemplate.scene.IsValid())
            UnityEngine.Object.Destroy(heldVisualTemplate);
    }

    public bool CanLaunchActiveItem()
    {
        if (inventory == null)
            return false;

        int slot = inventory.ActiveSlot;

        if (slot < 0)
            return false;

        string itemName = inventory.GetItemAtSlot(slot);

        if (string.IsNullOrEmpty(itemName))
            return false;

        return true;
    }

    [ServerRpc]
    private void LaunchServerRpc(int slot, FixedString64Bytes itemName, byte handIndex, Vector3 velocity)
    {
        if (inventory != null)
            inventory.RemoveItem(slot);

        Vector3 position = ComputeThrowPosition(handIndex);
        Quaternion rotation = Quaternion.LookRotation(velocity);

        string name = itemName.ToString();
        int idx = GetPrefabIndex(name);

        if (idx < 0 || idx >= items.Length || items[idx].WorldDropPrefab == null)
        {
            Debug.LogError(
                $"Cannot throw network item '{name}': no World Drop Prefab is configured in NetworkInventorySync -> Items.",
                this
            );
            return;
        }

        GameObject obj = Instantiate(items[idx].WorldDropPrefab, position, rotation);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.linearVelocity = velocity;

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError(
                $"World Drop Prefab for '{name}' must contain a NetworkObject component.",
                obj
            );
            Destroy(obj);
            return;
        }

        if (!netObj.IsSpawned)
            netObj.Spawn(true);
    }

    public bool TryGetConfiguredPrefabs(
        string itemName,
        out GameObject worldDropPrefab,
        out GameObject heldVisualPrefab)
    {
        worldDropPrefab = null;
        heldVisualPrefab = null;

        int index = GetPrefabIndex(itemName);
        if (index < 0 || index >= items.Length)
            return false;

        worldDropPrefab = items[index].WorldDropPrefab;
        heldVisualPrefab = items[index].HeldVisualPrefab;
        return worldDropPrefab != null || heldVisualPrefab != null;
    }

    private int GetPrefabIndex(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || items == null)
            return -1;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].Name == itemName)
                return i;
        }

        return -1;
    }

    private Vector3 GetLaunchPosition()
    {
        bool useLeft = playerController != null &&
                       playerController.SelectedInteractionHand == PlayerController.InteractionHand.Left;

        if (useLeft && leftEjectPoint != null)
            return leftEjectPoint.position;

        if (!useLeft && rightEjectPoint != null)
            return rightEjectPoint.position;

        if (dropOrigin != null)
            return dropOrigin.position;

        if (useLeft && leftHoldPivot != null)
            return leftHoldPivot.position;

        if (rightHoldPivot != null)
            return rightHoldPivot.position;

        return transform.position + transform.forward * 0.5f;
    }

    private Vector3 ComputeThrowPosition(byte handIndex)
    {
        bool useLeft = handIndex == 1;

        if (useLeft && leftEjectPoint != null)
            return leftEjectPoint.position;

        if (!useLeft && rightEjectPoint != null)
            return rightEjectPoint.position;

        if (dropOrigin != null)
            return dropOrigin.position;

        if (useLeft && leftHoldPivot != null)
            return leftHoldPivot.position;

        if (rightHoldPivot != null)
            return rightHoldPivot.position;

        return transform.position + transform.forward * 0.5f;
    }

    private bool IsPositionBlocked(Vector3 position)
    {
        Vector3 bodyCenter = transform.position + Vector3.up * 0.5f;
        float dist = Vector3.Distance(bodyCenter, position);

        if (dist < 0.01f)
            return false;

        Collider[] hits = Physics.OverlapCapsule(
            bodyCenter,
            position,
            throwBlockCheckRadius,
            throwBlockMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (!hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private static GameObject BuildDropItem(Vector3 position, Quaternion rotation, GameObject heldVisual, string itemName)
    {
        GameObject obj = new GameObject($"Dropped_{itemName}");
        obj.transform.SetPositionAndRotation(position, rotation);

        if (heldVisual != null)
        {
            GameObject vis = Instantiate(heldVisual, obj.transform);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;

            var renderers = vis.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
                r.enabled = true;
        }

        obj.AddComponent<BoxCollider>();

        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = true;

        PickableItem pickable = obj.AddComponent<PickableItem>();
        pickable.SetupItem(itemName, null);

        return obj;
    }
}
