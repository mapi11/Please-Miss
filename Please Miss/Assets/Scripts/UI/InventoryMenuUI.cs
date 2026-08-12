using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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

    private enum SniperSubTab
    {
        Items,
        Guns
    }

    [Header("Prefabs")]
    [Tooltip("Префаб карточки предмета (InventoryContainer, список Guns)")]
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("Префаб карточки для слотов снаряжения (Runner Slots / Sniper Slots). Если не задан — используется slotPrefab")]
    [SerializeField] private GameObject equipmentSlotPrefab;
    [Tooltip("Префаб карточки оружия (список Guns): название, иконка, кнопка открытия информации. Если не задан — используется slotPrefab")]
    [SerializeField] private GameObject rifleCardPrefab;
    [Tooltip("Префаб карточки оружия в слоте снаряжения (RifleSlotContainer): название, иконка, кнопка открытия информации. Если не задан — используется equipmentSlotPrefab")]
    [SerializeField] private GameObject rifleSlotPrefab;
    [Tooltip("Винтовки (PickableItem), показываемые в списке Guns. Только снайперские винтовки")]
    [SerializeField] private List<RifleEntry> rifleItems = new List<RifleEntry>();

    [Header("Tabs")]
    [Tooltip("Кнопка переключения на вкладку бегуна")]
    [SerializeField] private Button runnerTabButton;
    [Tooltip("Кнопка переключения на вкладку снайпера")]
    [SerializeField] private Button sniperTabButton;
    [Tooltip("Корневой объект вкладки бегуна (скрывается при выборе снайпера)")]
    [SerializeField] private GameObject runnerView;
    [Tooltip("Корневой объект вкладки снайпера (скрывается при выборе бегуна)")]
    [SerializeField] private GameObject sniperView;
    [Tooltip("Кнопка Items (список предметов) внутри вкладки снайпера")]
    [SerializeField] private Button itemsTabButton;
    [Tooltip("Кнопка Guns (список винтовок) внутри вкладки снайпера")]
    [SerializeField] private Button gunsTabButton;

    [Header("Containers")]
    [Tooltip("Общая сетка предметов: на вкладке бегуна Universal+Runner, на вкладке снайпера Universal+Sniper")]
    [SerializeField] private RectTransform inventoryContainer;
    [Tooltip("Контейнер слотов снаряжения бегуна (3 слота)")]
    [SerializeField] private RectTransform runnerSlotsContainer;
    [Tooltip("Контейнер слотов снаряжения снайпера (2 слота)")]
    [SerializeField] private RectTransform sniperSlotsContainer;
    [Tooltip("Контейнер карточек винтовок (вкладка Guns, подвкладка Guns)")]
    [FormerlySerializedAs("rifleListContainer")]
    [SerializeField] private RectTransform gunsContainer;
    [Tooltip("Контейнер выделенного слота винтовки")]
    [SerializeField] private RectTransform rifleSlotContainer;

    [Header("Info Panels")]
    [Tooltip("Панель информации о предмете (изначально скрыта). Показывается по клику на карточку или слот")]
    [SerializeField] private ItemInfoPanel itemInfoPanel;
    [Tooltip("Панель информации о винтовке (изначально скрыта). Показывается по клику на карточку оружия")]
    [SerializeField] private RifleInfoPanel rifleInfoPanel;
    [Tooltip("Кнопка-фон на весь экран: клик скрывает панели информации")]
    [SerializeField] private Button backgroundCloseButton;

    [Header("Loadout")]
    [Tooltip("Сколько предметов игрок может взять с собой в слоты снаряжения")]
    [Min(1)] [SerializeField] private int maxLoadoutItems = 2;
    [Tooltip("Текст счётчика снаряжения на вкладке бегуна (например 1/2)")]
    [SerializeField] private TMP_Text runnerLoadoutCounterText;
    [Tooltip("Текст счётчика снаряжения на вкладке снайпера (например 1/2)")]
    [SerializeField] private TMP_Text sniperLoadoutCounterText;

    [Header("Window")]
    [Tooltip("Корень окна меню (анимация появления, поиск элементов). Если не задан — сам InventoryMenuUI")]
    [SerializeField] private RectTransform windowRoot;
    [Tooltip("Кнопка закрытия меню (первая/основная)")]
    [SerializeField] private Button closeButton1;
    [Tooltip("Кнопка закрытия меню (запасная)")]
    [SerializeField] private Button closeButton2;

    [Header("Points")]
    [Tooltip("Текст количества очков игрока")]
    [SerializeField] private TMP_Text pointsText;

    private const string LastTabKey = "InventoryLastTab";

    private Tab currentTab;
    private SniperSubTab sniperSubTab = SniperSubTab.Items;
    private bool gunsBuilt;

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
        RegisterRifleItems();
        EnsureDefaultRifle();
    }

    private void OnEnable()
    {
        LocalPlayerSettings.PointsChanged += OnPointsChanged;

        EnsureWindow();
        EnsureTabs();
        EnsureCloseButtons();
        EnsureBackgroundButton();
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

        // Никакого масштабирования окна: при scale-анимации карточки двигаются под курсором
        // и первый клик после открытия промахивается. Только фейд — он не влияет на клики.
        group.blocksRaycasts = true;
        group.interactable = true;
        target.localScale = Vector3.one;

        group.alpha = 0f;
        group.DOFade(1f, 0.15f);
    }

    public void Close()
    {
        RectTransform target = windowRoot != null ? windowRoot : transform as RectTransform;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
        {
            Destroy(gameObject);
            return;
        }

        target.DOKill();
        group.DOKill();

        group.interactable = false;
        group.blocksRaycasts = false;

        target.DOScale(0.8f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        group.DOFade(0f, 0.12f).SetUpdate(true);

        DOVirtual.DelayedCall(0.2f, () => Destroy(gameObject), true);
    }

    private void OnPointsChanged(int newPoints)
    {
        RefreshPoints();
    }

    private void RefreshPoints()
    {
        if (pointsText != null)
            pointsText.text = $"<color=#2096F3>Points: </color><color=#FFFFFF>{LocalPlayerSettings.PlayerPoints}</color>";
    }

    private void EnsureWindow()
    {
        if (windowRoot == null)
            windowRoot = transform as RectTransform;
    }

    private void EnsureTabs()
    {
        if (runnerTabButton != null)
            BindTab(runnerTabButton, Tab.Runner);

        if (sniperTabButton != null)
            BindTab(sniperTabButton, Tab.Sniper);

        if (itemsTabButton != null)
            BindSubTab(itemsTabButton, SniperSubTab.Items);

        if (gunsTabButton != null)
            BindSubTab(gunsTabButton, SniperSubTab.Guns);
    }

    private void BindTab(Button button, Tab tab)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ApplyTab(tab));
    }

    private void BindSubTab(Button button, SniperSubTab subTab)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ApplySubTab(subTab));
    }

    private void ApplyTab(Tab tab)
    {
        currentTab = tab;
        sniperSubTab = SniperSubTab.Items;

        if (runnerView != null)
            runnerView.SetActive(tab == Tab.Runner);

        if (sniperView != null)
            sniperView.SetActive(tab == Tab.Sniper);

        if (runnerTabButton != null)
            runnerTabButton.interactable = tab != Tab.Runner;

        if (sniperTabButton != null)
            sniperTabButton.interactable = tab != Tab.Sniper;

        // Кнопки Items/Guns видны только на вкладке снайпера
        bool showSubTabs = tab == Tab.Sniper;

        if (itemsTabButton != null)
            itemsTabButton.gameObject.SetActive(showSubTabs);

        if (gunsTabButton != null)
            gunsTabButton.gameObject.SetActive(showSubTabs);

        UpdateSubTabButtons();

        PlayerPrefs.SetString(LastTabKey, tab == Tab.Sniper ? "Sniper" : "Runner");
        PlayerPrefs.Save();

        Refresh();
    }

    private void ApplySubTab(SniperSubTab subTab)
    {
        if (currentTab != Tab.Sniper)
            return;

        sniperSubTab = subTab;
        UpdateSubTabButtons();
        Refresh();
    }

    private void UpdateSubTabButtons()
    {
        if (itemsTabButton != null)
            itemsTabButton.interactable = sniperSubTab != SniperSubTab.Items;

        if (gunsTabButton != null)
            gunsTabButton.interactable = sniperSubTab != SniperSubTab.Guns;
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
                BuyPrice = rifle.BuyPrice,
                Description = rifle.Description,
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
                Description = info != null ? info.Description : "",
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
        HideInfoPanels();

        if (currentTab == Tab.Runner)
            RefreshRunner();
        else
            RefreshSniper();
    }

    private void RefreshRunner()
    {
        if (inventoryContainer != null)
            inventoryContainer.gameObject.SetActive(true);

        if (gunsContainer != null)
            gunsContainer.gameObject.SetActive(false);

        if (runnerSlotsContainer != null)
            BuildEquipmentSlots(runnerSlotsContainer, Tab.Runner, LocalPlayerSettings.GetRunnerEquipmentSlot, LocalPlayerSettings.SetRunnerEquipmentSlot);

        if (inventoryContainer != null)
            BuildInventoryGrid(inventoryContainer, Tab.Runner);

        UpdateLoadoutCounter(runnerLoadoutCounterText, Tab.Runner);
    }

    private void RefreshSniper()
    {
        if (sniperSlotsContainer != null)
            BuildEquipmentSlots(sniperSlotsContainer, Tab.Sniper, LocalPlayerSettings.GetSniperEquipmentSlot, LocalPlayerSettings.SetSniperEquipmentSlot);

        bool showGuns = sniperSubTab == SniperSubTab.Guns;

        if (inventoryContainer != null)
        {
            inventoryContainer.gameObject.SetActive(!showGuns);

            if (!showGuns)
                BuildInventoryGrid(inventoryContainer, Tab.Sniper);
        }

        if (gunsContainer != null)
        {
            gunsContainer.gameObject.SetActive(showGuns);

            if (showGuns && !gunsBuilt)
            {
                BuildRifleCards(gunsContainer);
                gunsBuilt = true;
            }
        }

        if (rifleSlotContainer != null)
            BuildRifleSlot(rifleSlotContainer);

        UpdateLoadoutCounter(sniperLoadoutCounterText, Tab.Sniper);
    }

    private int GetSlotCount(Tab tab)
    {
        return tab == Tab.Runner
            ? LocalPlayerSettings.RunnerEquipmentSlotsCount
            : LocalPlayerSettings.SniperEquipmentSlotsCount;
    }

    private void BuildEquipmentSlots(RectTransform container, Tab tab, System.Func<int, string> getSlot, System.Action<int, string> setSlot)
    {
        ClearChildren(container);

        int slotCount = GetSlotCount(tab);

        for (int i = 0; i < slotCount; i++)
        {
            string itemId = getSlot(i);
            ItemDefinition def = ItemCatalog.Get(itemId);

            GameObject go = BuildSlot("EquipmentSlot_" + i, container, true);

            if (go == null)
                continue;

            if (def == null)
            {
                SetSlotEmpty(go);
                continue;
            }

            int slotIndex = i;

            SetSlotFilled(go, def, () =>
            {
                if (itemInfoPanel == null)
                {
                    Debug.LogWarning("[InventoryMenuUI] ItemInfoPanel is not assigned in the inspector", this);
                    return;
                }

                itemInfoPanel.ShowInfoOnly(def, () =>
                {
                    setSlot(slotIndex, "");
                    LocalPlayerSettings.RemoveInventoryItem(itemId);
                    LocalPlayerSettings.AddPoints(def != null ? def.SellPrice : 0);
                    Refresh();
                });
            }, () =>
            {
                setSlot(slotIndex, "");
                LocalPlayerSettings.AddInventoryItem(itemId);
                Refresh();
            });
        }
    }

    /// <summary>Заполненный слот снаряжения: иконка, имя, Purpose. Клик по плашке открывает InfoPanel, кнопка на карточке снимает предмет.</summary>
    private void SetSlotFilled(GameObject go, ItemDefinition def, System.Action onInfo, System.Action onUnequip)
    {
        SetFilledVisuals(go, def);

        TMP_Text purpose = GetPurposeText(go);
        if (purpose != null)
        {
            purpose.gameObject.SetActive(true);
            purpose.text = def.PurposeText;
        }

        Button slotButton = GetSlotButton(go);
        if (slotButton != null)
        {
            slotButton.gameObject.SetActive(true);
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onInfo?.Invoke());
        }

        Button unequipButton = GetUnequipButton(go);
        if (unequipButton != null)
        {
            unequipButton.gameObject.SetActive(true);
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(() => onUnequip?.Invoke());
        }
    }

    /// <summary>Пустой слот: визуал остаётся как в префабе (пустышка с изначальным текстом "Empty slot"). Кнопки скрыты.</summary>
    private void SetSlotEmpty(GameObject go)
    {
        SetEmptySlotPanelActive(go, true);
        SetButtonActive(go, false);
        SetUnequipButtonActive(go, false);
    }

    private void SetFilledVisuals(GameObject go, ItemDefinition def)
    {
        InventorySlot invSlot = go.GetComponent<InventorySlot>();
        if (invSlot == null)
            return;

        Image icon = invSlot.ObjectImg;
        if (icon != null)
        {
            icon.enabled = true;

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

        TMP_Text nameText = invSlot.ItemNameTxt;
        if (nameText != null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = ItemLocalization.GetName(def.ItemId);
        }

        if (invSlot.EmptySlotPanel != null)
            invSlot.EmptySlotPanel.SetActive(false);
    }

    private GameObject GetEmptySlotPanel(GameObject root)
    {
        InventorySlot invSlot = root.GetComponent<InventorySlot>();
        return invSlot != null ? invSlot.EmptySlotPanel : null;
    }

    private void SetEmptySlotPanelActive(GameObject root, bool active)
    {
        GameObject panel = GetEmptySlotPanel(root);
        if (panel != null)
            panel.SetActive(active);
    }

    private void SetButtonActive(GameObject root, bool active)
    {
        Button button = GetSlotButton(root);
        if (button != null)
            button.gameObject.SetActive(active);
    }

    private Button GetUnequipButton(GameObject root)
    {
        InventorySlot invSlot = root.GetComponent<InventorySlot>();
        return invSlot != null ? invSlot.UnequipButton : null;
    }

    private void SetUnequipButtonActive(GameObject root, bool active)
    {
        Button button = GetUnequipButton(root);
        if (button != null)
            button.gameObject.SetActive(active);
    }

    private void BuildInventoryGrid(RectTransform container, Tab tab)
    {
        ClearChildren(container);

        bool loadoutFull = IsRoleFull(tab);

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

            CreateInventoryCard(itemId, def, container, tab, loadoutFull);
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
        return CountFilledSlots(tab) >= maxLoadoutItems;
    }

    private int CountFilledSlots(Tab tab)
    {
        int filled = 0;
        int slotCount = GetSlotCount(tab);

        for (int i = 0; i < slotCount; i++)
        {
            string itemId = tab == Tab.Runner
                ? LocalPlayerSettings.GetRunnerEquipmentSlot(i)
                : LocalPlayerSettings.GetSniperEquipmentSlot(i);

            if (!string.IsNullOrEmpty(itemId))
                filled++;
        }

        return filled;
    }

    private void CreateInventoryCard(string itemId, ItemDefinition def, RectTransform container, Tab tab, bool loadoutFull)
    {
        GameObject go = BuildSlot("Card_" + itemId, container);

        if (go == null)
            return;

        SetFilledVisuals(go, def);

        SetUnequipButtonActive(go, false);

        TMP_Text purpose = GetPurposeText(go);
        if (purpose != null)
        {
            purpose.gameObject.SetActive(true);
            purpose.text = def.PurposeText;
        }

        bool canEquip = !loadoutFull;

        Button cardButton = GetSlotButton(go);
        if (cardButton == null)
        {
            Debug.LogWarning($"[InventoryMenuUI] CardButton is not assigned on InventorySlot for card '{def.ItemId}'. Assign it in the slot prefab inspector", this);
            return;
        }

        cardButton.gameObject.SetActive(true);
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() =>
        {
            if (itemInfoPanel == null)
            {
                Debug.LogWarning("[InventoryMenuUI] ItemInfoPanel is not assigned in the inspector", this);
                return;
            }

            itemInfoPanel.ShowItem(def, canEquip, "Equip", true,
                () => MoveToActiveEquipment(itemId, tab),
                () => SellItem(itemId, def));
        });
    }

    private void MoveToActiveEquipment(string itemId, Tab tab)
    {
        int slotCount = GetSlotCount(tab);

        for (int i = 0; i < slotCount; i++)
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

    private void UpdateLoadoutCounter(TMP_Text text, Tab tab)
    {
        if (text == null)
            return;

        text.text = $"{CountFilledSlots(tab)}/{maxLoadoutItems}";
    }

    private void BuildRifleSlot(RectTransform container)
    {
        ClearChildren(container);

        string rifleId = LocalPlayerSettings.SniperRifle;
        ItemDefinition def = ItemCatalog.Get(rifleId);
        PickableItem rifle = FindRiflePrefab(rifleId);
        RifleCatalog.RifleInfo info = RifleCatalog.Get(rifleId);

        GameObject go = BuildSlot("RifleSlot", container, false, rifleSlotPrefab);

        if (go == null)
            return;

        if (def == null && info == null && rifle == null)
        {
            SetSlotEmpty(go);
            return;
        }

        if (def == null)
            def = CreateFallbackDef(rifle, info);

        SetFilledVisuals(go, def);

        // Винтовку нельзя снять — только заменить, поэтому кнопки снятия на карточке нет.
        SetUnequipButtonActive(go, false);

        Button slotButton = GetSlotButton(go);
        if (slotButton != null)
        {
            slotButton.gameObject.SetActive(true);
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() =>
            {
                if (rifleInfoPanel == null)
                {
                    Debug.LogWarning("[InventoryMenuUI] RifleInfoPanel is not assigned in the inspector", this);
                    return;
                }

                rifleInfoPanel.Show(info, true, () => { });
            });
        }
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
                    Description = entry.riflePrefab.Description,
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

            ItemDefinition def = ItemCatalog.Get(rifleId) ?? CreateFallbackDef(rifle, info);

            GameObject go = BuildSlot("RifleCard_" + rifleId, container, false, rifleCardPrefab);

            if (go == null)
                continue;

            SetFilledVisuals(go, def);

            SetUnequipButtonActive(go, false);

            Button cardButton = GetSlotButton(go);
            if (cardButton == null)
            {
                Debug.LogWarning($"[InventoryMenuUI] CardButton is not assigned on InventorySlot for rifle '{rifleId}'. Assign it in the rifle card prefab inspector", this);
                continue;
            }

            cardButton.gameObject.SetActive(true);
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() =>
            {
                if (rifleInfoPanel == null)
                {
                    Debug.LogWarning("[InventoryMenuUI] RifleInfoPanel is not assigned in the inspector", this);
                    return;
                }

                rifleInfoPanel.Show(info, equipped, () =>
                {
                    LocalPlayerSettings.SetSniperRifle(rifleId);
                    Refresh();
                });
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
            Description = rifle != null ? rifle.Description : "",
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

    private void HideInfoPanels()
    {
        if (itemInfoPanel != null)
            itemInfoPanel.Hide();

        if (rifleInfoPanel != null)
            rifleInfoPanel.Hide();
    }

    private GameObject BuildSlot(string name, RectTransform parent, bool equipment = false, GameObject overridePrefab = null)
    {
        GameObject prefab = overridePrefab != null
            ? overridePrefab
            : (equipment && equipmentSlotPrefab != null ? equipmentSlotPrefab : slotPrefab);

        if (prefab == null)
        {
            Debug.LogError($"[InventoryMenuUI] Slot prefab is not assigned in the inspector ({nameof(slotPrefab)} / {nameof(equipmentSlotPrefab)} / {nameof(rifleCardPrefab)} / {nameof(rifleSlotPrefab)})", this);
            return null;
        }

        GameObject go = Instantiate(prefab);
        go.name = name;
        go.transform.SetParent(parent, false);
        return go;
    }

    private Button GetSlotButton(GameObject root)
    {
        InventorySlot invSlot = root.GetComponent<InventorySlot>();
        return invSlot != null ? invSlot.CardButton : null;
    }

    private TMP_Text GetPurposeText(GameObject root)
    {
        InventorySlot invSlot = root.GetComponent<InventorySlot>();
        return invSlot != null ? invSlot.PurposeText : null;
    }

    private void EnsureBackgroundButton()
    {
        if (backgroundCloseButton != null)
            BindBackgroundButton(backgroundCloseButton);
    }

    private void BindBackgroundButton(Button button)
    {
        button.transform.SetAsFirstSibling();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HideInfoPanels);
    }

    private void EnsureCloseButtons()
    {
        BindCloseButton(closeButton1);
        BindCloseButton(closeButton2);
    }

    private void BindCloseButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Close);
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
}
