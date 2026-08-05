using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

public class InventoryMenuUI : MonoBehaviour
{
    [System.Serializable]
    public sealed class RifleEntry
    {
        [Tooltip("Rifle prefab (PickableItem). Only sniper rifles should be added here")]
        public PickableItem riflePrefab;
    }

    private enum Tab
    {
        Runner,
        Sniper
    }

    [Header("Tabs")]
    [SerializeField] private Button runnerTabButton;
    [SerializeField] private Button sniperTabButton;
    [SerializeField] private GameObject runnerView;
    [SerializeField] private GameObject sniperView;

    [Header("Containers")]
    [Tooltip("Shared inventory grid: shows Universal+Runner on Runner tab, Universal+Sniper on Sniper tab")]
    [SerializeField] private RectTransform inventoryContainer;
    [SerializeField] private RectTransform runnerSlotsContainer;
    [SerializeField] private RectTransform sniperSlotsContainer;
    [SerializeField] private RectTransform rifleListContainer;
    [SerializeField] private RectTransform rifleSlotContainer;
    [Tooltip("Sniper rifle prefabs shown as cards with an Equip dropdown")]
    [SerializeField] private List<RifleEntry> rifleItems = new List<RifleEntry>();
    [Tooltip("Panel prefab with icon, name and Equip button. If null, cards are built at runtime with a dropdown")]
    [SerializeField] private SniperRifleItemPanel rifleItemPanelPrefab;

    [Header("Window")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Button closeButton1;
    [SerializeField] private Button closeButton2;

    [Header("Points")]
    [SerializeField] private TMP_Text pointsText;

    [Header("Slots")]
    [SerializeField] private GameObject slotPrefab;

    private const string LastTabKey = "InventoryLastTab";
    private const float animInDuration = 0.35f;

    private Canvas canvas;
    private Tab currentTab;

    /// <summary>
    /// Registers rifle items in the catalog and ensures a default rifle is set,
    /// without showing the menu. Must be called once before the game starts.
    /// </summary>
    public static void WarmUp(GameObject panelPrefab)
    {
        if (panelPrefab == null)
            return;

        GameObject go = Instantiate(panelPrefab);

        if (!go.activeSelf)
            go.SetActive(true);

        Destroy(go);
    }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        RegisterRifleItems();
        EnsureDefaultRifle();
    }

    private void OnEnable()
    {
        LocalPlayerSettings.PointsChanged += OnPointsChanged;

        EnsureWindow();
        EnsureContainers();
        EnsureTabs();
        EnsureCloseButtons();
        ApplyTab(LoadSavedTab());
        AnimateIn();
        RefreshPoints();
    }

    private void OnDisable()
    {
        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void AnimateIn()
    {
        RectTransform target = windowRoot != null ? windowRoot : transform as RectTransform;

        if (target == null)
            return;

        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
            group = target.gameObject.AddComponent<CanvasGroup>();

        target.DOKill();
        group.DOKill();

        target.localScale = Vector3.one * 0.8f;
        target.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f);

        group.alpha = 0f;
        group.interactable = false;
        group.DOFade(1f, animInDuration * 0.6f).OnComplete(() =>
        {
            group.interactable = true;
        });
    }

    public void Close()
    {
        Destroy(gameObject);
    }

    private void OnPointsChanged(int newPoints)
    {
        RefreshPoints();
    }

    private void RefreshPoints()
    {
        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
    }

    private void EnsureWindow()
    {
        if (windowRoot == null)
            windowRoot = transform as RectTransform;
    }

    private void EnsureContainers()
    {
        Transform root = windowRoot != null ? windowRoot : transform;

        if (runnerView == null)
        {
            Transform t = FindInChildren(root, "RunnerView");
            if (t != null)
                runnerView = t.gameObject;
        }

        if (sniperView == null)
        {
            Transform t = FindInChildren(root, "SniperView");
            if (t != null)
                sniperView = t.gameObject;
        }

        if (inventoryContainer == null)
            inventoryContainer = FindInChildren(root, "InventoryContainer") as RectTransform;

        if (runnerSlotsContainer == null)
            runnerSlotsContainer = FindInChildren(root, "RunnerSlotsContainer") as RectTransform;

        if (sniperSlotsContainer == null)
            sniperSlotsContainer = FindInChildren(root, "SniperSlotsContainer") as RectTransform;

        if (rifleListContainer == null)
            rifleListContainer = FindInChildren(root, "RifleListContainer") as RectTransform;

        if (rifleSlotContainer == null)
            rifleSlotContainer = FindInChildren(root, "RifleSlotContainer") as RectTransform;
    }

    private void EnsureTabs()
    {
        if (runnerTabButton != null)
            BindTab(runnerTabButton, Tab.Runner);

        if (sniperTabButton != null)
            BindTab(sniperTabButton, Tab.Sniper);
    }

    private void BindTab(Button button, Tab tab)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ApplyTab(tab));
    }

    private void ApplyTab(Tab tab)
    {
        currentTab = tab;

        if (runnerView != null)
            runnerView.SetActive(tab == Tab.Runner);

        if (sniperView != null)
            sniperView.SetActive(tab == Tab.Sniper);

        if (runnerTabButton != null)
            runnerTabButton.interactable = tab != Tab.Runner;

        if (sniperTabButton != null)
            sniperTabButton.interactable = tab != Tab.Sniper;

        PlayerPrefs.SetString(LastTabKey, tab == Tab.Sniper ? "Sniper" : "Runner");
        PlayerPrefs.Save();

        Refresh();
    }

    private Tab LoadSavedTab()
    {
        return PlayerPrefs.GetString(LastTabKey, "Runner") == "Sniper" ? Tab.Sniper : Tab.Runner;
    }

    private void RegisterRifleItems()
    {
        foreach (RifleEntry entry in rifleItems)
        {
            if (entry == null || entry.riflePrefab == null)
                continue;

            PickableItem rifle = entry.riflePrefab;

            if (string.IsNullOrEmpty(rifle.ItemName) || ItemCatalog.Get(rifle.ItemName) != null)
                continue;

            ItemDefinition def = new ItemDefinition(
                rifle.ItemName,
                rifle.ItemName,
                rifle.Purpose,
                new Color(0.7f, 0.8f, 0.4f, 1f))
            {
                IconSprite = rifle.InventoryIcon,
                SellPrice = rifle.SellPrice,
                Class = rifle.ItemClass
            };

            ItemCatalog.Register(def);
        }

        foreach (RifleCatalog.RifleInfo info in RifleCatalog.All)
        {
            if (string.IsNullOrEmpty(info.RifleId) || ItemCatalog.Get(info.RifleId) != null)
                continue;

            ItemDefinition def = new ItemDefinition(
                info.RifleId,
                info.DisplayName,
                ItemPurpose.Boost,
                new Color(0.7f, 0.8f, 0.4f, 1f))
            {
                IconSprite = info.Icon,
                SellPrice = 0,
                Class = ItemClass.Sniper
            };

            ItemCatalog.Register(def);
        }
    }

    private void EnsureDefaultRifle()
    {
        if (!string.IsNullOrEmpty(LocalPlayerSettings.SniperRifle))
        {
            LocalPlayerSettings.AddOwnedSniperRifle(LocalPlayerSettings.SniperRifle);
            return;
        }

        foreach (RifleEntry entry in rifleItems)
        {
            if (entry != null && entry.riflePrefab != null && !string.IsNullOrEmpty(entry.riflePrefab.ItemName))
            {
                LocalPlayerSettings.SetSniperRifle(entry.riflePrefab.ItemName);
                LocalPlayerSettings.AddOwnedSniperRifle(entry.riflePrefab.ItemName);
                return;
            }
        }
    }

    public void Refresh()
    {
        if (currentTab == Tab.Runner)
            RefreshRunner();
        else
            RefreshSniper();
    }

    private void RefreshRunner()
    {
        if (runnerSlotsContainer != null)
            BuildEquipmentSlots(runnerSlotsContainer, LocalPlayerSettings.GetRunnerEquipmentSlot, LocalPlayerSettings.SetRunnerEquipmentSlot);

        if (inventoryContainer != null)
            BuildInventoryGrid(inventoryContainer, Tab.Runner);
    }

    private void RefreshSniper()
    {
        if (sniperSlotsContainer != null)
            BuildEquipmentSlots(sniperSlotsContainer, LocalPlayerSettings.GetSniperEquipmentSlot, LocalPlayerSettings.SetSniperEquipmentSlot);

        if (inventoryContainer != null)
            BuildInventoryGrid(inventoryContainer, Tab.Sniper);

        if (rifleSlotContainer != null)
            BuildRifleSlot(rifleSlotContainer);

        if (rifleListContainer != null)
            BuildRifleCards(rifleListContainer);
    }

    private void BuildEquipmentSlots(RectTransform container, System.Func<int, string> getSlot, System.Action<int, string> setSlot)
    {
        ClearChildren(container);

        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            string itemId = getSlot(i);
            ItemDefinition def = ItemCatalog.Get(itemId);

            GameObject go = BuildSlot("EquipmentSlot_" + i, container, 140f, 140f);

            if (def == null)
            {
                TMP_Text emptyName = FindText(go, "Name", "ItemNameTxt");
                if (emptyName != null)
                    emptyName.text = "Empty";

                InventorySlot invSlot = go.GetComponent<InventorySlot>();

                if (invSlot != null && invSlot.LocationDropdown != null)
                    invSlot.LocationDropdown.gameObject.SetActive(false);

                Transform drop = go.transform.Find("Dropdown");
                if (drop != null)
                    drop.gameObject.SetActive(false);

                continue;
            }

            ApplyVisuals(go, def);

            int slotIndex = i;

            CreateDropdown(go, new[] { "", "Take off", "Sell - " + def.SellPrice }, 0, option =>
            {
                if (option == "Take off")
                {
                    setSlot(slotIndex, "");
                    LocalPlayerSettings.AddInventoryItem(itemId);
                    Refresh();
                }
                else if (option.StartsWith("Sell"))
                {
                    setSlot(slotIndex, "");
                    LocalPlayerSettings.AddPoints(def.SellPrice);
                    Refresh();
                }
            });
        }
    }

    private void BuildInventoryGrid(RectTransform container, Tab tab)
    {
        ClearChildren(container);

        bool playerFull = IsRoleFull(tab);

        List<string> inventory = LocalPlayerSettings.Inventory;

        for (int i = 0; i < inventory.Count; i++)
        {
            string itemId = inventory[i];

            if (string.IsNullOrEmpty(itemId))
                continue;

            ItemDefinition def = ResolveDef(itemId);

            if (def == null)
                continue;

            if (!MatchesTab(def, tab))
                continue;

            CreateInventorySlot(itemId, def, container, tab, playerFull);
        }
    }

    private bool MatchesTab(ItemDefinition def, Tab tab)
    {
        if (def.Class == ItemClass.Universal)
            return true;

        return tab == Tab.Runner ? def.Class == ItemClass.Runner : def.Class == ItemClass.Sniper;
    }

    private bool IsRoleFull(Tab tab)
    {
        int filled = 0;

        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            string itemId = tab == Tab.Runner
                ? LocalPlayerSettings.GetRunnerEquipmentSlot(i)
                : LocalPlayerSettings.GetSniperEquipmentSlot(i);

            if (!string.IsNullOrEmpty(itemId))
                filled++;
        }

        return filled >= LocalPlayerSettings.EquipmentSlotsCount;
    }

    private void CreateInventorySlot(string itemId, ItemDefinition def, RectTransform container, Tab tab, bool playerFull)
    {
        GameObject go = BuildSlot("Slot_" + itemId, container, 140f, 140f);
        ApplyVisuals(go, def);

        string[] options = playerFull
            ? new[] { "", "Sell - " + def.SellPrice }
            : new[] { "", "Take", "Sell - " + def.SellPrice };

        CreateDropdown(go, options, 0, option =>
        {
            if (option == "Take")
                MoveToActiveEquipment(itemId, tab);
            else if (option.StartsWith("Sell"))
                SellItem(itemId, def);
        });
    }

    private void MoveToActiveEquipment(string itemId, Tab tab)
    {
        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            string current = tab == Tab.Runner
                ? LocalPlayerSettings.GetRunnerEquipmentSlot(i)
                : LocalPlayerSettings.GetSniperEquipmentSlot(i);

            if (!string.IsNullOrEmpty(current))
                continue;

            if (tab == Tab.Runner)
                LocalPlayerSettings.SetRunnerEquipmentSlot(i, itemId);
            else
                LocalPlayerSettings.SetSniperEquipmentSlot(i, itemId);

            LocalPlayerSettings.RemoveInventoryItem(itemId);
            Refresh();
            return;
        }
    }

    private void SellItem(string itemId, ItemDefinition def)
    {
        LocalPlayerSettings.RemoveInventoryItem(itemId);
        LocalPlayerSettings.AddPoints(def != null ? def.SellPrice : 0);
        Refresh();
    }

    private void BuildRifleSlot(RectTransform container)
    {
        ClearChildren(container);

        string rifleId = LocalPlayerSettings.SniperRifle;
        ItemDefinition def = ItemCatalog.Get(rifleId);
        PickableItem rifle = FindRiflePrefab(rifleId);
        RifleCatalog.RifleInfo info = RifleCatalog.Get(rifleId);

        if (rifleItemPanelPrefab != null && (rifle != null || info != null || def == null))
        {
            SniperRifleItemPanel panel = Instantiate(rifleItemPanelPrefab, container);
            panel.Setup(GetRifleName(rifle, info), GetRifleIcon(rifle, info), false, "Equip", null, info != null ? info.Definition : null);
            return;
        }

        GameObject go = BuildSlot("RifleSlot", container, 140f, 140f);

        if (def == null && info == null && rifle == null)
        {
            TMP_Text emptyName = FindText(go, "Name", "ItemNameTxt");
            if (emptyName != null)
                emptyName.text = "Empty";

            return;
        }

        if (def == null)
            def = CreateFallbackDef(rifle, info);

        ApplyVisuals(go, def);
    }

    private void BuildRifleCards(RectTransform container)
    {
        ClearChildren(container);

        List<RifleCatalog.RifleInfo> entries = new List<RifleCatalog.RifleInfo>();

        if (RifleCatalog.All.Count > 0)
        {
            entries.AddRange(RifleCatalog.All);
        }
        else
        {
            foreach (RifleEntry entry in rifleItems)
            {
                if (entry == null || entry.riflePrefab == null)
                    continue;

                entries.Add(new RifleCatalog.RifleInfo
                {
                    RifleId = entry.riflePrefab.ItemName,
                    DisplayName = entry.riflePrefab.ItemName,
                    Icon = entry.riflePrefab.InventoryIcon
                });
            }
        }

        foreach (RifleCatalog.RifleInfo info in entries)
        {
            if (info == null || string.IsNullOrEmpty(info.RifleId))
                continue;

            string rifleId = info.RifleId;

            if (!LocalPlayerSettings.IsSniperRifleOwned(rifleId))
                continue;

            PickableItem rifle = FindRiflePrefab(rifleId);
            bool equipped = LocalPlayerSettings.SniperRifle == rifleId;

            if (rifleItemPanelPrefab != null)
            {
                SniperRifleItemPanel panel = Instantiate(rifleItemPanelPrefab, container);
                panel.Setup(GetRifleName(rifle, info), GetRifleIcon(rifle, info), !equipped, "Equip", () =>
                {
                    LocalPlayerSettings.SetSniperRifle(rifleId);
                    Refresh();
                }, info != null ? info.Definition : null);
                continue;
            }

            ItemDefinition def = ItemCatalog.Get(rifleId) ?? CreateFallbackDef(rifle, info);

            GameObject go = BuildSlot("RifleCard_" + rifleId, container, 140f, 140f);
            ApplyVisuals(go, def);

            if (equipped)
            {
                InventorySlot invSlot = go.GetComponent<InventorySlot>();

                if (invSlot != null && invSlot.LocationDropdown != null)
                    invSlot.LocationDropdown.gameObject.SetActive(false);

                Transform drop = go.transform.Find("Dropdown");
                if (drop != null)
                    drop.gameObject.SetActive(false);

                continue;
            }

            CreateDropdown(go, new[] { "", "Equip" }, 0, option =>
            {
                LocalPlayerSettings.SetSniperRifle(rifleId);
                Refresh();
            });
        }
    }

    private PickableItem FindRiflePrefab(string rifleId)
    {
        foreach (RifleEntry entry in rifleItems)
        {
            if (entry != null && entry.riflePrefab != null && entry.riflePrefab.ItemName == rifleId)
                return entry.riflePrefab;
        }

        return null;
    }

    private static string GetRifleName(PickableItem rifle, RifleCatalog.RifleInfo info)
    {
        if (info != null && !string.IsNullOrEmpty(info.DisplayName))
            return info.DisplayName;

        return rifle != null ? rifle.ItemName : "Empty";
    }

    private static Sprite GetRifleIcon(PickableItem rifle, RifleCatalog.RifleInfo info)
    {
        if (info != null && info.Icon != null)
            return info.Icon;

        return rifle != null ? rifle.InventoryIcon : null;
    }

    private ItemDefinition CreateFallbackDef(PickableItem rifle, RifleCatalog.RifleInfo info)
    {
        string id = GetRifleName(rifle, info);

        ItemDefinition def = new ItemDefinition(
            id,
            GetRifleName(rifle, info),
            ItemPurpose.Boost,
            new Color(0.7f, 0.8f, 0.4f, 1f))
        {
            IconSprite = GetRifleIcon(rifle, info),
            SellPrice = 0,
            Class = ItemClass.Sniper
        };

        ItemCatalog.Register(def);
        return def;
    }

    private ItemDefinition ResolveDef(string itemId)
    {
        ItemDefinition def = ItemCatalog.Get(itemId);

        if (def != null)
            return def;

        ItemDefinition fallback = new ItemDefinition(itemId, itemId, ItemPurpose.Boost, new Color(0.6f, 0.6f, 0.65f, 1f));
        ItemCatalog.Register(fallback);
        return fallback;
    }

    private GameObject BuildSlot(string name, RectTransform parent, float width, float height)
    {
        if (slotPrefab != null)
        {
            GameObject go = Instantiate(slotPrefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }

        GameObject built = new GameObject(name, typeof(RectTransform));
        built.transform.SetParent(parent, false);

        LayoutElement element = built.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;

        Image bg = built.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

        RectTransform iconRect = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRect.SetParent(built.transform, false);
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -8f);
        iconRect.sizeDelta = new Vector2(52f, 52f);
        iconRect.gameObject.AddComponent<Image>();

        RectTransform nameRect = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRect.SetParent(built.transform, false);
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -66f);
        nameRect.sizeDelta = new Vector2(-10f, 20f);

        TMP_Text nameText = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
        nameText.font = FindFont();
        nameText.fontSize = 13f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;

        return built;
    }

    private void ApplyVisuals(GameObject slot, ItemDefinition def)
    {
        Image icon = null;
        TMP_Text nameText = null;

        InventorySlot invSlot = slot.GetComponent<InventorySlot>();

        if (invSlot != null)
        {
            icon = invSlot.ObjectImg;
            nameText = invSlot.ItemNameTxt;
        }

        if (icon == null)
            icon = FindImage(slot, "Icon", "ObjectImg");

        if (nameText == null)
            nameText = FindText(slot, "Name", "ItemNameTxt");

        if (icon != null)
        {
            if (def.IconSprite != null)
            {
                icon.sprite = def.IconSprite;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = def.IconColor;
            }
        }

        if (nameText != null)
            nameText.text = def.DisplayName;
    }

    private void CreateDropdown(GameObject slot, string[] options, int value, System.Action<string> onOptionSelected)
    {
        TMP_Dropdown tmp = null;

        InventorySlot invSlot = slot.GetComponent<InventorySlot>();
        if (invSlot != null)
            tmp = invSlot.LocationDropdown;

        if (tmp == null)
        {
            Transform existing = slot.transform.Find("Dropdown");
            if (existing != null)
                tmp = existing.GetComponent<TMP_Dropdown>();
        }

        if (tmp != null)
        {
            var texts = new List<string>(options);

            tmp.ClearOptions();
            tmp.AddOptions(texts);
            tmp.SetValueWithoutNotify(value);
            tmp.onValueChanged.RemoveAllListeners();
            tmp.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < options.Length)
                    onOptionSelected(options[index]);
            });

            tmp.gameObject.SetActive(true);

            return;
        }

        GameObject dropdownGo;

        if (slot.transform.Find("Dropdown") is Transform existingDrop && existingDrop.GetComponent<InventorySlotDropdown>() != null)
        {
            dropdownGo = existingDrop.gameObject;
        }
        else
        {
            Transform existingChild = slot.transform.Find("Dropdown");
            if (existingChild != null)
                Destroy(existingChild.gameObject);

            dropdownGo = new GameObject("Dropdown", typeof(RectTransform));
            dropdownGo.transform.SetParent(slot.transform, false);

            RectTransform rect = dropdownGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 36f);
            rect.sizeDelta = new Vector2(118f, 28f);

            dropdownGo.AddComponent<InventorySlotDropdown>();
        }

        var custom = dropdownGo.GetComponent<InventorySlotDropdown>();
        custom.Init(canvas, new List<string>(options), value);
        custom.onOptionSelected = onOptionSelected;
    }

    private void EnsureCloseButtons()
    {
        if (closeButton1 != null && closeButton2 != null)
        {
            BindCloseButton(closeButton1);
            BindCloseButton(closeButton2);
            return;
        }

        Transform root = windowRoot != null ? windowRoot : transform;

        if (closeButton1 == null)
        {
            Transform t = root.Find("CloseButton");
            if (t != null)
                closeButton1 = t.GetComponent<Button>();
        }

        if (closeButton2 == null)
        {
            Transform t = root.Find("CloseButton2");
            if (t != null)
                closeButton2 = t.GetComponent<Button>();
        }

        if (closeButton1 == null)
            closeButton1 = CreateCloseButton("CloseButton", new Vector2(1f, 1f), new Vector2(-10f, -10f), "X", 40f, 40f);

        if (closeButton2 == null)
            closeButton2 = CreateCloseButton("CloseButton2", new Vector2(1f, 0f), new Vector2(-10f, 10f), "Close", 110f, 40f);

        BindCloseButton(closeButton1);
        BindCloseButton(closeButton2);
    }

    private Button CreateCloseButton(string name, Vector2 anchor, Vector2 position, string label, float width, float height)
    {
        RectTransform root = windowRoot != null ? windowRoot : transform as RectTransform;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, height);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.font = FindFont();

        return button;
    }

    private void BindCloseButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Close);
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (child.name == name)
                return child;

            Transform nested = FindInChildren(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Image FindImage(GameObject root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = root.transform.Find(name);
            if (t != null)
            {
                Image img = t.GetComponent<Image>();
                if (img != null)
                    return img;
            }
        }

        return null;
    }

    private TMP_Text FindText(GameObject root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = root.transform.Find(name);
            if (t != null)
            {
                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null)
                    return text;
            }
        }

        return null;
    }

    private static void ClearChildren(RectTransform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);

            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.font != null)
                return text.font;
        }

        return null;
    }
}

public class InventorySlotDropdown : MonoBehaviour
{
    public System.Action<string> onOptionSelected;

    private Canvas canvas;
    private RectTransform canvasRect;
    private TMP_Text caption;
    private List<string> options = new List<string>();
    private int value;
    private GameObject popup;
    private GameObject blocker;

    public void Init(Canvas canvas, List<string> options, int value)
    {
        this.canvas = canvas;

        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;

        this.options = new List<string>(options);
        this.value = Mathf.Clamp(value, 0, this.options.Count - 1);

        EnsureVisuals();
        UpdateCaption();
    }

    public void SetValue(int newValue)
    {
        value = Mathf.Clamp(newValue, 0, this.options.Count - 1);
        UpdateCaption();
    }

    private void EnsureVisuals()
    {
        Image bg = GetComponent<Image>();

        if (bg == null)
        {
            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        }

        Button button = GetComponent<Button>();

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OpenPopup);

        Transform capT = transform.Find("Caption");

        if (capT == null)
        {
            GameObject capGo = new GameObject("Caption", typeof(RectTransform));
            capGo.transform.SetParent(transform, false);

            RectTransform capRect = capGo.GetComponent<RectTransform>();
            capRect.anchorMin = Vector2.zero;
            capRect.anchorMax = Vector2.one;
            capRect.offsetMin = Vector2.zero;
            capRect.offsetMax = Vector2.zero;

            caption = capGo.AddComponent<TextMeshProUGUI>();
            caption.font = FindFont();
            caption.fontSize = 13f;
            caption.color = Color.white;
            caption.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            caption = capT.GetComponent<TMP_Text>();
        }
    }

    private void OpenPopup()
    {
        ClosePopup();

        if (canvasRect == null)
            return;

        RectTransform myRect = transform as RectTransform;

        GameObject blockerGo = new GameObject("DropdownBlocker", typeof(RectTransform));
        blockerGo.transform.SetParent(canvasRect, false);

        RectTransform blockerRect = blockerGo.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImg = blockerGo.AddComponent<Image>();
        blockerImg.color = new Color(0f, 0f, 0f, 0.001f);

        Button blockerButton = blockerGo.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(ClosePopup);
        blocker = blockerGo;

        GameObject pop = new GameObject("DropdownList", typeof(RectTransform));
        pop.transform.SetParent(canvasRect, false);
        pop.transform.SetAsLastSibling();

        RectTransform popRect = pop.GetComponent<RectTransform>();
        float optionHeight = 30f;
        popRect.sizeDelta = new Vector2(140f, options.Count * optionHeight + 8f);
        popRect.pivot = new Vector2(0.5f, 1f);

        Image popBg = pop.AddComponent<Image>();
        popBg.color = new Color(0.13f, 0.13f, 0.17f, 1f);

        VerticalLayoutGroup layout = pop.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 2f;

        if (myRect != null)
        {
            Vector2 screen = new Vector2(myRect.position.x, myRect.position.y);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                popRect.anchoredPosition = local + new Vector2(0f, -6f);
        }

        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            string optionText = options[i];

            GameObject opt = new GameObject(optionText, typeof(RectTransform));
            opt.transform.SetParent(pop.transform, false);

            LayoutElement optElement = opt.AddComponent<LayoutElement>();
            optElement.preferredHeight = optionHeight;

            Image optBg = opt.AddComponent<Image>();
            optBg.color = index == value
                ? new Color(0.35f, 0.42f, 0.55f, 1f)
                : new Color(0.2f, 0.2f, 0.25f, 1f);

            Button optButton = opt.AddComponent<Button>();
            optButton.targetGraphic = optBg;

            TMP_Text optText = CreateOptionText(opt.transform, optionText);
            optText.color = index == value ? Color.white : new Color(0.75f, 0.8f, 1f);

            optButton.onClick.AddListener(() =>
            {
                value = index;
                UpdateCaption();
                ClosePopup();

                if (onOptionSelected != null)
                    onOptionSelected(optionText);
            });
        }

        popup = pop;
    }

    private TMP_Text CreateOptionText(Transform parent, string text)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);

        TMP_Text label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = FindFont();
        label.fontSize = 14f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;

        return label;
    }

    private void UpdateCaption()
    {
        if (caption == null)
            return;

        if (value >= 0 && value < options.Count)
            caption.text = options[value];
    }

    private void ClosePopup()
    {
        if (popup != null)
            Destroy(popup);

        if (blocker != null)
            Destroy(blocker);

        popup = null;
        blocker = null;
    }

    private void OnDisable()
    {
        ClosePopup();
    }

    private static TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.font != null)
                return text.font;
        }

        return null;
    }
}
