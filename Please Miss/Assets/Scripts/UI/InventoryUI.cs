using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private InventorySlot slotPrefab;

    [Header("Colors")]
    [SerializeField] private Color activeSlotColor = Color.white;
    [SerializeField] private Color inactiveSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);

    private InventorySlot[] slots;

    private void Start()
    {
        if (FindObjectOfType<LobbyManager>() != null)
        {
            gameObject.SetActive(false);
            return;
        }

        var localInv = FindLocalPlayerInventory();
        if (localInv != null)
        {
            BindInventory(localInv);
            return;
        }

        StartCoroutine(WaitForPlayer());
    }

    private IEnumerator WaitForPlayer()
    {
        for (int i = 0; i < 60; i++)
        {
            var localInv = FindLocalPlayerInventory();
            if (localInv != null)
            {
                BindInventory(localInv);
                yield break;
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }

    private void BindInventory(Inventory newInventory)
    {
        if (newInventory == null) return;

        inventory = newInventory;

        if (slotsParent == null) return;

        CreateSlots();

        inventory.OnSlotChanged += OnSlotChanged;
        inventory.OnActiveSlotChanged += OnActiveSlotChanged;
        inventory.OnSlotIconChanged += OnSlotIconChanged;

        bool hasItems = false;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            OnSlotChanged(i, inventory.GetItemAtSlot(i));
            OnSlotIconChanged(i, inventory.GetSlotIcon(i));
            if (!string.IsNullOrEmpty(inventory.GetItemAtSlot(i)))
                hasItems = true;
        }

        OnActiveSlotChanged(inventory.ActiveSlot);

        gameObject.SetActive(hasItems);
    }

    private void OnDestroy()
    {
        if (inventory == null) return;
        inventory.OnSlotChanged -= OnSlotChanged;
        inventory.OnActiveSlotChanged -= OnActiveSlotChanged;
        inventory.OnSlotIconChanged -= OnSlotIconChanged;
    }

    private Inventory FindLocalPlayerInventory()
    {
        var net = NetworkManager.Singleton;
        if (net != null && net.IsClient)
        {
            var localObj = net.LocalClient?.PlayerObject;
            if (localObj != null)
                return localObj.GetComponent<Inventory>();
        }
        return null;
    }

    private void CreateSlots()
    {
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
        }

        slots = new InventorySlot[inventory.MaxSlots];

        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot;
            if (slotPrefab != null)
            {
                slot = Instantiate(slotPrefab, slotsParent);
            }
            else
            {
                var obj = new GameObject($"Slot_{i}", typeof(RectTransform));
                obj.AddComponent<Image>();
                slot = obj.AddComponent<InventorySlot>();
                slot.transform.SetParent(slotsParent, false);
            }

            slot.gameObject.name = $"Slot_{i}";

            if (slot.ItemNameTxt != null)
                slot.ItemNameTxt.text = (i + 1).ToString();

            slots[i] = slot;
        }
    }

    private void OnSlotChanged(int slotIndex, string itemName)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        var slot = slots[slotIndex];
        if (slot == null) return;

        if (slot.ItemNameTxt != null)
            slot.ItemNameTxt.text = string.IsNullOrEmpty(itemName) ? (slotIndex + 1).ToString() : itemName;

        RefreshSlotColor(slotIndex);
        UpdateVisibility();
    }

    private void OnSlotIconChanged(int slotIndex, Sprite icon)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        var slot = slots[slotIndex];
        if (slot == null || slot.ObjectImg == null) return;

        slot.ObjectImg.sprite = icon;
        slot.ObjectImg.enabled = icon != null;
        RefreshSlotColor(slotIndex);
    }

    private void OnActiveSlotChanged(int slotIndex)
    {
        for (int i = 0; i < slots.Length; i++)
            RefreshSlotColor(i);
    }

    private void UpdateVisibility()
    {
        if (inventory == null) return;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            if (!string.IsNullOrEmpty(inventory.GetItemAtSlot(i)))
            {
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
                return;
            }
        }

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void RefreshSlotColor(int slotIndex)
    {
        if (inventory == null || slots == null) return;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var slot = slots[slotIndex];
        if (slot == null || slot.ObjectImg == null) return;

        if (slotIndex == inventory.ActiveSlot)
            slot.ObjectImg.color = activeSlotColor;
        else if (inventory.GetItemAtSlot(slotIndex) == null)
            slot.ObjectImg.color = emptySlotColor;
        else
            slot.ObjectImg.color = inactiveSlotColor;
    }
}
