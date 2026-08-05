using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Content")]
    [SerializeField] private RectTransform shopItemsContainer;
    [SerializeField] private BuyItemSlot buyItemSlotPrefab;
    [SerializeField] private List<ShopItemEntry> shopItems = new List<ShopItemEntry>();

    [Header("Window")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Button closeButton1;
    [SerializeField] private Button closeButton2;
    [SerializeField] private TMP_Text pointsText;

    private void Awake()
    {
        RegisterCatalogItems();
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
        LocalPlayerSettings.PointsChanged += OnPointsChanged;
        RefreshPoints();
        Refresh();
    }

    private void OnDisable()
    {
        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void OnPointsChanged(int points)
    {
        RefreshPoints();
        RefreshSlotsInteractable();
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

        target.localScale = Vector3.one * 0.8f;
        target.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f);

        group.alpha = 0f;
        group.interactable = false;
        group.DOFade(1f, animInDuration * 0.6f).OnComplete(() =>
        {
            group.interactable = true;
        });
    }

    private const float animInDuration = 0.35f;

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

        if (shopItemsContainer == null || buyItemSlotPrefab == null)
            return;

        ClearChildren(shopItemsContainer);

        foreach (ShopItemEntry entry in shopItems)
        {
            if (entry == null || entry.itemPrefab == null)
                continue;

            BuyItemSlot slot = Instantiate(buyItemSlotPrefab, shopItemsContainer);
            slot.Setup(entry);
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