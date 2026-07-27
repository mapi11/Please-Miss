using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkInventorySync : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    public Inventory Inventory => inventory;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform leftHoldPivot;
    [SerializeField] private Transform rightHoldPivot;
    [SerializeField] private Transform dropOrigin;

    [Header("Item Prefabs")]
    [SerializeField] private ItemEntry[] items;
    [SerializeField] private float heldItemScale = 0.8f;

    [System.Serializable]
    private class ItemEntry
    {
        public string Name;
        public GameObject WorldDropPrefab;
    }

    [Header("Throw")]
    [SerializeField] private Transform leftEjectPoint;
    [SerializeField] private Transform rightEjectPoint;
    [SerializeField] private float minThrowForce = 3f;
    [SerializeField] private float maxThrowForce = 15f;
    [SerializeField] private float throwBlockCheckRadius = 0.15f;
    [SerializeField] private LayerMask throwBlockMask = ~0;

    private readonly NetworkVariable<int> networkActiveSlot = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> networkActiveItemName = new(
        new FixedString64Bytes(""),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<byte> networkActiveHand = new(
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
            if (items[i] != null && items[i].WorldDropPrefab != null && string.IsNullOrEmpty(items[i].Name))
            {
                var pickable = items[i].WorldDropPrefab.GetComponent<PickableItem>();
                if (pickable != null && !string.IsNullOrEmpty(pickable.ItemName))
                    items[i].Name = pickable.ItemName;
            }
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
        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

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
                prefab = items[prefabIndex].WorldDropPrefab;
        }

        if (prefab == null)
            return;

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
    }

    public void LaunchActiveItem(float charge, Vector3 direction)
    {
        if (inventory == null)
            return;

        int slot = inventory.ActiveSlot;

        if (slot < 0)
            return;

        string itemName = inventory.GetItemAtSlot(slot);

        if (string.IsNullOrEmpty(itemName))
            return;

        Vector3 pos = GetLaunchPosition();

        if (IsPositionBlocked(pos))
            return;

        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

        Vector3 velocity = direction * Mathf.Lerp(minThrowForce, maxThrowForce, charge);

        GameObject heldVisual = inventory.GetSlotHeldPrefab(slot);

        inventory.RemoveItem(slot);

        if (IsSpawned)
        {
            if (!IsOwner)
                return;

            byte handIndex = playerController != null
                ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
                : (byte)0;

            LaunchServerRpc(slot, new FixedString64Bytes(itemName), handIndex, velocity);

            if (heldVisual != null)
                Destroy(heldVisual);
        }
        else
        {
            Quaternion rot = Quaternion.LookRotation(direction);
            GameObject dropObj = BuildDropItem(pos, rot, heldVisual, itemName);
            Rigidbody rb = dropObj.GetComponent<Rigidbody>();

            if (rb != null)
                rb.linearVelocity = velocity;

            if (heldVisual != null)
                Destroy(heldVisual);
        }
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

        GameObject obj;

        if (idx >= 0 && idx < items.Length && items[idx].WorldDropPrefab != null)
        {
            obj = Instantiate(items[idx].WorldDropPrefab, position, rotation);
        }
        else
        {
            obj = new GameObject($"Dropped_{name}");
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.AddComponent<BoxCollider>();
            obj.AddComponent<Rigidbody>().useGravity = true;
            var pickable = obj.AddComponent<PickableItem>();
            pickable.SetupItem(name, null);
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.linearVelocity = velocity;

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
            netObj = obj.AddComponent<NetworkObject>();

        if (!netObj.IsSpawned)
            netObj.Spawn(true);
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
