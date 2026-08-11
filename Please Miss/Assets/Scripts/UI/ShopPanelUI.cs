using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

public class ShopPanelUI : MonoBehaviour
{
    [System.Serializable]
    public sealed class ShopItemEntry
    {
        public PickableItem itemPrefab;
        [Range(0f, 100f)] public int discountPercent;
    }

    [System.Serializable]
    public sealed class GunEntry
    {
        public SniperRifleHeldVisual heldPrefab;
        [Tooltip("Иконка для карточки в магазине")]
        public Sprite icon;
        [Min(0)] public int buyPrice;
    }

    [System.Serializable]
    public sealed class PurposeFilterEntry
    {
        [Tooltip("Кнопка фильтра по назначению")]
        public Button button;
        [Tooltip("Какая цель (как в PickableItem)")]
        public ItemPurpose purpose;
    }

    [Header("Window")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Button closeButton1;
    [SerializeField] private Button closeButton2;
    [SerializeField] private TMP_Text pointsText;

    [Header("Items Tab")]
    [SerializeField] private RectTransform shopItemsContainer;
    [Tooltip("Префаб карточки предмета в магазине")]
    [FormerlySerializedAs("buyItemSlotPrefab")]
    [SerializeField] private BuyItemSlot itemCardPrefab;
    [SerializeField] private List<ShopItemEntry> shopItems = new List<ShopItemEntry>();

    [Header("Role Filters")]
    [Tooltip("Основные фильтры по роли: All (все предметы всех ролей), Sniper, Runner")]
    [SerializeField] private Button roleAllButton;
    [SerializeField] private Button roleSniperButton;
    [SerializeField] private Button roleRunnerButton;

    [Header("Purpose Filters")]
    [Tooltip("Кнопка сброса фильтра по назначению (показывает все предметы выбранной роли)")]
    [SerializeField] private Button purposeAllButton;
    [Tooltip("Массив кнопок фильтра по назначению: кнопка + Purpose. " +
             "Кнопка скрывается, если у выбранной роли нет ни одного предмета с этим Purpose")]
    [SerializeField] private List<PurposeFilterEntry> purposeFilters = new List<PurposeFilterEntry>();

    [Header("Guns Tab")]
    [SerializeField] private Button itemsTabButton;
    [SerializeField] private Button gunsTabButton;
    [SerializeField] private RectTransform itemsRoot;
    [SerializeField] private RectTransform gunsRoot;
    [Tooltip("Контейнер списка карточек оружия (иконка, название, цена, кнопка Info)")]
    [SerializeField] private RectTransform gunsContainer;
    [SerializeField] private GunShopCard gunCardPrefab;
    [SerializeField] private List<GunEntry> gunItems = new List<GunEntry>();
    [Tooltip("Панель информации о винтовке (открывается с карточки оружия)")]
    [SerializeField] private GunInfoPanel gunInfoPanel;
    [Tooltip("Цепная скидка: первая по порядку некупленная винтовка получает скидку. " +
             "Купил её — скидка переходит к следующей некупленной")]
    [Range(0f, 100f)]
    [SerializeField] private int chainDiscountPercent = 10;

    private enum RoleFilter
    {
        All,
        Sniper,
        Runner
    }

    private bool gunsTabOpen;
    private RoleFilter roleFilter = RoleFilter.All;
    private bool purposeAll = true;
    private ItemPurpose selectedPurpose;

    private void Awake()
    {
        RegisterCatalogItems();
        RegisterRifleCatalog();
    }

    private void RegisterRifleCatalog()
    {
        foreach (GunEntry entry in gunItems)
        {
            if (entry == null || entry.heldPrefab == null)
                continue;

            RifleCatalog.Register(entry.heldPrefab, entry.icon);
        }
    }

    private void RegisterCatalogItems()
    {
        foreach (ShopItemEntry entry in shopItems)
        {
            if (entry == null || entry.itemPrefab == null)
                continue;

            string itemName = entry.itemPrefab.ItemName;

            if (string.IsNullOrEmpty(itemName) || ItemCatalog.Get(itemName) != null)
                continue;

            ItemDefinition def = new ItemDefinition(
                itemName,
                itemName,
                entry.itemPrefab.Purpose,
                new Color(0.5f, 0.72f, 1f, 1f))
            {
                IconSprite = entry.itemPrefab.InventoryIcon,
                SellPrice = entry.itemPrefab.SellPrice,
                BuyPrice = entry.itemPrefab.BuyPrice,
                Description = entry.itemPrefab.Description,
                Class = entry.itemPrefab.ItemClass
            };

            ItemCatalog.Register(def);
        }
    }

    private void OnEnable()
    {
        EnsureWindow();
        AnimateIn();
        EnsureCloseButtons();
        BindTabs();
        ShowGunsTab(false);
        BindFilterButtons();
        LocalPlayerSettings.PointsChanged += OnPointsChanged;
        RefreshPoints();
        Refresh();
        RebuildGunCards();
    }

    private void OnDisable()
    {
        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void OnPointsChanged(int points)
    {
        RefreshPoints();
        RefreshSlotsInteractable();

        if (gunInfoPanel != null && gunInfoPanel.IsOpen)
            gunInfoPanel.OnPlayerPointsChanged();
    }

    private void BindTabs()
    {
        if (itemsTabButton == null)
        {
            Transform t = transform.Find("ItemsButton");
            if (t != null)
                itemsTabButton = t.GetComponent<Button>();
        }

        if (gunsTabButton == null)
        {
            Transform t = transform.Find("GunsButton");
            if (t != null)
                gunsTabButton = t.GetComponent<Button>();
        }

        if (itemsTabButton != null)
        {
            itemsTabButton.onClick.RemoveAllListeners();
            itemsTabButton.onClick.AddListener(() => ShowGunsTab(false));
        }

        if (gunsTabButton != null)
        {
            gunsTabButton.onClick.RemoveAllListeners();
            gunsTabButton.onClick.AddListener(() => ShowGunsTab(true));
        }
    }

    private void ShowGunsTab(bool open)
    {
        gunsTabOpen = open;

        if (itemsRoot != null)
            itemsRoot.gameObject.SetActive(!open);

        if (gunsRoot != null)
            gunsRoot.gameObject.SetActive(open);

        if (itemsTabButton != null)
            itemsTabButton.interactable = open;

        if (gunsTabButton != null)
            gunsTabButton.interactable = !open;
    }

    private void RebuildGunCards()
    {
        if (gunInfoPanel != null)
            gunInfoPanel.Hide();

        if (gunsContainer == null || gunCardPrefab == null)
            return;

        ClearChildren(gunsContainer);

        foreach (GunEntry entry in gunItems)
        {
            if (entry == null || entry.heldPrefab == null)
                continue;

            GunShopCard card = Instantiate(gunCardPrefab, gunsContainer);
            card.Setup(
                entry.heldPrefab,
                entry.icon,
                () => ShowGunInfo(entry),
                GetGunPrice(entry),
                IsChainDiscounted(entry));
        }
    }

    private void ShowGunInfo(GunEntry entry)
    {
        if (gunInfoPanel == null || entry == null || entry.heldPrefab == null)
            return;

        gunInfoPanel.Show(
            entry.heldPrefab,
            entry.icon,
            GetGunPrice(entry),
            IsChainDiscounted(entry),
            chainDiscountPercent,
            () => BuyRifle(entry));
    }

    /// <summary>
    /// Цепная скидка: скидку получает первая по порядку некупленная винтовка.
    /// После покупки скидка переходит к следующей некупленной.
    /// </summary>
    private bool IsChainDiscounted(GunEntry entry)
    {
        if (chainDiscountPercent <= 0 || entry == null || entry.heldPrefab == null)
            return false;

        foreach (GunEntry e in gunItems)
        {
            if (e == null || e.heldPrefab == null)
                continue;

            if (!IsGunOwned(e))
                return e == entry;
        }

        return false;
    }

    private bool IsGunOwned(GunEntry entry)
    {
        string rifleId = GetGunId(entry);
        return !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);
    }

    private string GetGunId(GunEntry entry)
    {
        if (entry == null || entry.heldPrefab == null)
            return "";

        return entry.heldPrefab.Definition != null
            ? entry.heldPrefab.Definition.RifleId
            : entry.heldPrefab.name;
    }

    /// <summary>Финальная цена винтовки: базовая или со скидкой, если винтовка — текущая цель цепочки.</summary>
    private int GetGunPrice(GunEntry entry)
    {
        int basePrice = Mathf.Max(0, entry != null ? entry.buyPrice : 0);

        if (entry != null && IsChainDiscounted(entry))
            return Mathf.Max(0, Mathf.RoundToInt(basePrice * (1f - chainDiscountPercent / 100f)));

        return basePrice;
    }

    private void BuyRifle(GunEntry entry)
    {
        if (entry == null || entry.heldPrefab == null)
            return;

        string rifleId = GetGunId(entry);

        if (string.IsNullOrEmpty(rifleId) || IsGunOwned(entry))
            return;

        int price = GetGunPrice(entry);

        if (LocalPlayerSettings.PlayerPoints < price)
            return;

        LocalPlayerSettings.SetPoints(LocalPlayerSettings.PlayerPoints - price);
        LocalPlayerSettings.AddOwnedSniperRifle(rifleId);
        LocalPlayerSettings.SetSniperRifle(rifleId);
        RebuildGunCards();
    }

    private void RefreshSlotsInteractable()
    {
        if (shopItemsContainer == null)
            return;

        for (int i = 0; i < shopItemsContainer.childCount; i++)
        {
            BuyItemSlot slot = shopItemsContainer.GetChild(i).GetComponent<BuyItemSlot>();

            if (slot != null)
                slot.OnPlayerPointsChanged();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Close()
    {
        if (windowRoot != null)
            windowRoot.gameObject.SetActive(false);
        else
            gameObject.SetActive(false);
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

    private void EnsureWindow()
    {
        if (windowRoot == null)
            windowRoot = transform as RectTransform;
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
            Transform t = root.Find("CloseBgButton");
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

    public void Refresh()
    {
        if (shopItemsContainer == null)
        {
            Transform t = transform.Find("Frame/Scroll View/Viewport/ShopItemsContainer");
            if (t != null)
                shopItemsContainer = t as RectTransform;
        }

        if (shopItemsContainer == null || itemCardPrefab == null)
            return;

        ClearChildren(shopItemsContainer);

        foreach (ShopItemEntry entry in shopItems)
        {
            if (entry == null || entry.itemPrefab == null)
                continue;

            if (!MatchesFilters(entry.itemPrefab))
                continue;

            BuyItemSlot slot = Instantiate(itemCardPrefab, shopItemsContainer);
            slot.Setup(entry);
        }
    }

    /// <summary>
    /// Роль (All — все классы; Sniper/Runner — свой класс + Universal) в сочетании с
    /// назначением (All — любой Purpose, иначе конкретный ItemPurpose).
    /// </summary>
    private bool MatchesFilters(PickableItem item)
    {
        switch (roleFilter)
        {
            case RoleFilter.Sniper:
                if (item.ItemClass != ItemClass.Sniper && item.ItemClass != ItemClass.Universal)
                    return false;
                break;

            case RoleFilter.Runner:
                if (item.ItemClass != ItemClass.Runner && item.ItemClass != ItemClass.Universal)
                    return false;
                break;
        }

        if (!purposeAll && item.Purpose != selectedPurpose)
            return false;

        return true;
    }

    private void BindFilterButtons()
    {
        roleFilter = RoleFilter.All;
        purposeAll = true;

        if (roleAllButton != null)
        {
            roleAllButton.onClick.RemoveAllListeners();
            roleAllButton.onClick.AddListener(() => ApplyRoleFilter(RoleFilter.All));
        }

        if (roleSniperButton != null)
        {
            roleSniperButton.onClick.RemoveAllListeners();
            roleSniperButton.onClick.AddListener(() => ApplyRoleFilter(RoleFilter.Sniper));
        }

        if (roleRunnerButton != null)
        {
            roleRunnerButton.onClick.RemoveAllListeners();
            roleRunnerButton.onClick.AddListener(() => ApplyRoleFilter(RoleFilter.Runner));
        }

        if (purposeAllButton != null)
        {
            purposeAllButton.onClick.RemoveAllListeners();
            purposeAllButton.onClick.AddListener(ApplyPurposeAll);
        }

        foreach (PurposeFilterEntry entry in purposeFilters)
        {
            if (entry == null || entry.button == null)
                continue;

            entry.button.onClick.RemoveAllListeners();
            entry.button.onClick.AddListener(() => ApplyPurposeFilter(entry.purpose));
        }

        RefreshFilterButtons();
    }

    private void ApplyRoleFilter(RoleFilter filter)
    {
        if (roleFilter == filter)
            return;

        roleFilter = filter;

        if (!purposeAll && !GetAvailablePurposes(roleFilter).Contains(selectedPurpose))
            purposeAll = true;

        RefreshFilterButtons();
        Refresh();
    }

    private void ApplyPurposeFilter(ItemPurpose purpose)
    {
        purposeAll = false;
        selectedPurpose = purpose;
        RefreshFilterButtons();
        Refresh();
    }

    private void ApplyPurposeAll()
    {
        if (purposeAll)
            return;

        purposeAll = true;
        RefreshFilterButtons();
        Refresh();
    }

    /// <summary>Какие Purpose есть у предметов текущей роли (для All — по всем предметам).</summary>
    private HashSet<ItemPurpose> GetAvailablePurposes(RoleFilter filter)
    {
        HashSet<ItemPurpose> result = new HashSet<ItemPurpose>();

        foreach (ShopItemEntry entry in shopItems)
        {
            if (entry == null || entry.itemPrefab == null)
                continue;

            if (filter == RoleFilter.Sniper &&
                entry.itemPrefab.ItemClass != ItemClass.Sniper &&
                entry.itemPrefab.ItemClass != ItemClass.Universal)
                continue;

            if (filter == RoleFilter.Runner &&
                entry.itemPrefab.ItemClass != ItemClass.Runner &&
                entry.itemPrefab.ItemClass != ItemClass.Universal)
                continue;

            result.Add(entry.itemPrefab.Purpose);
        }

        return result;
    }

    private void RefreshFilterButtons()
    {
        if (roleAllButton != null)
            roleAllButton.interactable = roleFilter != RoleFilter.All;

        if (roleSniperButton != null)
            roleSniperButton.interactable = roleFilter != RoleFilter.Sniper;

        if (roleRunnerButton != null)
            roleRunnerButton.interactable = roleFilter != RoleFilter.Runner;

        if (purposeAllButton != null)
            purposeAllButton.interactable = !purposeAll;

        HashSet<ItemPurpose> available = GetAvailablePurposes(roleFilter);

        foreach (PurposeFilterEntry entry in purposeFilters)
        {
            if (entry == null || entry.button == null)
                continue;

            bool isAvailable = available.Contains(entry.purpose);
            entry.button.gameObject.SetActive(isAvailable);
            entry.button.interactable = !(!purposeAll && selectedPurpose == entry.purpose);
        }
    }

    private static void ClearChildren(RectTransform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
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

    private void RefreshPoints()
    {
        if (pointsText == null)
            pointsText = CreatePointsText();

        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
    }

    private TMP_Text CreatePointsText()
    {
        RectTransform root = windowRoot != null ? windowRoot : transform as RectTransform;

        if (root == null)
            return null;

        GameObject go = new GameObject("PointsText", typeof(RectTransform));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(320f, 40f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
        text.alignment = TextAlignmentOptions.Right;
        text.fontSize = 28f;
        text.color = Color.white;
        text.font = FindFont();
        return text;
    }
}