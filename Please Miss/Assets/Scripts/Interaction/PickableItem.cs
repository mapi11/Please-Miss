using Unity.Netcode;
using UnityEngine;

public class PickableItem : Interactable
{
    [Header("Pickup")]
    [SerializeField] private string itemName = "Item";
    [SerializeField] private Sprite inventoryIcon;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualPrefab;

    public string ItemName => itemName;
    public Sprite InventoryIcon => inventoryIcon;
    public GameObject HeldVisualPrefab => heldVisualPrefab;

    public void SetupItem(string name, Sprite icon)
    {
        itemName = name;
        inventoryIcon = icon;
    }

    public override void OnHandBegin(PlayerController player)
    {
        var inventory = player.PlayerInventory;
        if (inventory == null) return;

        GameObject heldVisual;
        if (heldVisualPrefab != null)
        {
            heldVisual = heldVisualPrefab;
        }
        else
        {
            heldVisual = BuildVisualFromSelf();
            if (heldVisual != null)
                DontDestroyOnLoad(heldVisual);
        }

        if (heldVisual == null) return;

        int slot = inventory.AddItem(itemName, inventoryIcon, heldVisual, IsSpawned ? gameObject : null);
        if (slot < 0)
        {
            if (heldVisualPrefab == null) Destroy(heldVisual);
            return;
        }

        if (player.PlayerSfx != null)
            player.PlayerSfx.PlayPickup();

        SetCanInteract(false);

        if (IsSpawned)
        {
            var playerNetObj = player.GetComponent<NetworkObject>();
            if (playerNetObj != null)
                PickupServerRpc(playerNetObj);
            else
                NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickupServerRpc(NetworkObjectReference playerRef)
    {
        NetworkObject.Despawn(true);
    }

    public override void OnHandHold(PlayerController player, float deltaTime) { }

    public override void OnHandEnd(PlayerController player) { }

    private GameObject BuildVisualFromSelf()
    {
        var meshFilters = GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return null;

        GameObject visual = new GameObject($"{gameObject.name}_HeldVisual");

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            GameObject child = new GameObject(mf.name);
            child.transform.SetParent(visual.transform, false);
            child.transform.localPosition = transform.InverseTransformPoint(mf.transform.position);
            child.transform.localRotation = Quaternion.Inverse(transform.rotation) * mf.transform.rotation;

            Vector3 parentScale = transform.lossyScale;
            Vector3 childScale = mf.transform.lossyScale;
            child.transform.localScale = new Vector3(
                parentScale.x > 0.0001f ? childScale.x / parentScale.x : 1f,
                parentScale.y > 0.0001f ? childScale.y / parentScale.y : 1f,
                parentScale.z > 0.0001f ? childScale.z / parentScale.z : 1f
            );

            child.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;

            var sourceRenderer = mf.GetComponent<MeshRenderer>();
            var targetRenderer = child.AddComponent<MeshRenderer>();
            if (sourceRenderer != null)
                targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

            targetRenderer.enabled = false;
        }

        return visual;
    }
}
