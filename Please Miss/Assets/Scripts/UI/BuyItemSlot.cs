using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuyItemSlot : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private TMP_Text priceTxt;
    [SerializeField] private TMP_Text discountTxt;

    [Header("Buy")]
    [SerializeField] private Button buyButton;

    private PickableItem item;
    private int discountPercent;
    private int finalPrice;

    public void Setup(ShopPanelUI.ShopItemEntry entry)
    {
        item = entry != null ? entry.itemPrefab : null;
        discountPercent = entry != null ? Mathf.Clamp(entry.discountPercent, 0, 100) : 0;
        Refresh();
    }

    private void OnEnable()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(TryBuy);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (item == null)
            return;

        if (iconImg != null)
        {
            iconImg.sprite = item.InventoryIcon;
            iconImg.color = item.InventoryIcon != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (nameTxt != null)
            nameTxt.text = item.ItemName;

        UpdatePrice();
    }

    private void UpdatePrice()
    {
        if (item == null)
            return;

        finalPrice = Mathf.Max(0, Mathf.RoundToInt(item.BuyPrice * (1f - discountPercent / 100f)));

        if (discountTxt != null)
        {
            bool hasDiscount = discountPercent > 0;
            discountTxt.gameObject.SetActive(hasDiscount);
            discountTxt.text = $"{discountPercent}% off";
        }

        if (priceTxt != null)
        {
            priceTxt.text = discountPercent <= 0
                ? $"Price: {finalPrice}"
                : $"Price: {finalPrice} (was {item.BuyPrice})";
        }

        if (buyButton != null)
            buyButton.interactable = LocalPlayerSettings.PlayerPoints >= finalPrice;
    }

    public void OnPlayerPointsChanged()
    {
        UpdatePrice();
    }

    private void TryBuy()
    {
        if (item == null)
            return;

        if (LocalPlayerSettings.PlayerPoints < finalPrice)
            return;

        LocalPlayerSettings.SetPoints(LocalPlayerSettings.PlayerPoints - finalPrice);
        EnsureCatalogItem();
        LocalPlayerSettings.AddInventoryItem(item.ItemName);
    }

    private void EnsureCatalogItem()
    {
        if (item == null)
            return;

        if (ItemCatalog.Get(item.ItemName) != null)
            return;

        ItemDefinition def = new ItemDefinition(
            item.ItemName,
            item.ItemName,
            item.Purpose,
            new Color(0.5f, 0.72f, 1f, 1f))
        {
            IconSprite = item.InventoryIcon,
            SellPrice = item.SellPrice,
            Class = item.ItemClass
        };

        ItemCatalog.Register(def);
    }
}