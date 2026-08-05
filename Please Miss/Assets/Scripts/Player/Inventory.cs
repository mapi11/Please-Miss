using UnityEngine;

public class Inventory : MonoBehaviour
{
    public const int RIFLE_SLOT_INDEX = 0;

    [SerializeField] private int maxSlots = 5;

    private string[] slots;
    private Sprite[] slotIcons;
    private GameObject[] slotHeldPrefabs;
    private GameObject[] slotDropPrefabs;

    private int activeSlot = -1;

    public int MaxSlots => maxSlots;
    public int ActiveSlot => activeSlot;
    public string ActiveItemType => activeSlot >= 0 && activeSlot < slots.Length ? slots[activeSlot] : null;

    public event System.Action<int, string> OnSlotChanged;
    public event System.Action<int> OnActiveSlotChanged;
    public event System.Action<int, Sprite> OnSlotIconChanged;

    private void Awake()
    {
        slots = new string[maxSlots];
        slotIcons = new Sprite[maxSlots];
        slotHeldPrefabs = new GameObject[maxSlots];
        slotDropPrefabs = new GameObject[maxSlots];
        activeSlot = -1;
    }

    public int AddItem(string itemName, Sprite icon = null, GameObject heldPrefab = null, GameObject dropPrefab = null)
    {
        if (string.IsNullOrEmpty(itemName)) return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = itemName;
                slotIcons[i] = icon;
                slotHeldPrefabs[i] = heldPrefab;
                slotDropPrefabs[i] = dropPrefab;

                OnSlotChanged?.Invoke(i, itemName);
                OnSlotIconChanged?.Invoke(i, icon);

                if (activeSlot < 0)
                    SetActiveSlot(i);

                return i;
            }
        }

        return -1;
    }

    public bool IsRifleSlot(int slot) => slot == RIFLE_SLOT_INDEX;

    public int AddItemToSlot(int slot, string itemName, Sprite icon = null, GameObject heldPrefab = null, GameObject dropPrefab = null)
    {
        if (string.IsNullOrEmpty(itemName)) return -1;
        if (slot < 0 || slot >= slots.Length || slots[slot] != null) return -1;

        slots[slot] = itemName;
        slotIcons[slot] = icon;
        slotHeldPrefabs[slot] = heldPrefab;
        slotDropPrefabs[slot] = dropPrefab;

        OnSlotChanged?.Invoke(slot, itemName);
        OnSlotIconChanged?.Invoke(slot, icon);

        if (activeSlot < 0)
            SetActiveSlot(slot);

        return slot;
    }

    public bool CanThrowFromSlot(int slot)
    {
        if (slot != RIFLE_SLOT_INDEX)
            return true;

        string item = GetItemAtSlot(slot);
        return string.IsNullOrEmpty(item) || item != LocalPlayerSettings.SniperRifle;
    }

    public bool RemoveItem(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return false;

        slots[slot] = null;
        slotIcons[slot] = null;
        slotHeldPrefabs[slot] = null;
        slotDropPrefabs[slot] = null;

        OnSlotChanged?.Invoke(slot, null);
        OnSlotIconChanged?.Invoke(slot, null);

        if (activeSlot == slot)
        {
            int newSlot = FindNextFilledSlot();
            if (newSlot >= 0)
                SetActiveSlot(newSlot);
            else
                ClearActiveSlot();
        }

        return true;
    }

    public bool SwitchToSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return false;
        if (activeSlot == slot) { ClearActiveSlot(); return true; }
        if (slots[slot] == null) { ClearActiveSlot(); return true; }
        SetActiveSlot(slot);
        return true;
    }

    private void SetActiveSlot(int slot)
    {
        activeSlot = slot;
        OnActiveSlotChanged?.Invoke(activeSlot);
    }

    public void ClearActiveSlot()
    {
        activeSlot = -1;
        OnActiveSlotChanged?.Invoke(-1);
    }

    public string GetItemAtSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return null;
        return slots[slot];
    }

    public Sprite GetSlotIcon(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return null;
        return slotIcons[slot];
    }

    public GameObject GetSlotHeldPrefab(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return null;
        return slotHeldPrefabs[slot];
    }

    private int FindNextFilledSlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) return i;
        return -1;
    }
}
