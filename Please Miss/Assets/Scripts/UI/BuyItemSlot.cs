using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuyItemSlot : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private TMP_Text descriptionTxt;
    [Tooltip("Текст скидки (например \"Sell 15%\"). Пульсирует, когда скидка активна")]
    [SerializeField] private TMP_Text discountTxt;
    [SerializeField] private TMP_Text priceTxt;

    [Header("Buy")]
    [SerializeField] private Button buyButton;

    [Header("Colors")]
    [Tooltip("Рамка карточки (Image). Краснеет при нехватке очков")]
    [SerializeField] private Image frameImg;
    [Tooltip("Обычный цвет рамки и текста")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Цвет рамки и текста цены при нехватке очков")]
    [SerializeField] private Color notEnoughColor = Color.red;
    [Tooltip("Цвет рамки и текста цены при активной скидке")]
    [SerializeField] private Color discountColor = Color.yellow;

    private PickableItem item;
    private int discountPercent;
    private int finalPrice;
    private bool discountActive;

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

        if (descriptionTxt != null)
        {
            bool hasDescription = !string.IsNullOrWhiteSpace(item.Description);
            descriptionTxt.gameObject.SetActive(hasDescription);

            if (hasDescription)
                descriptionTxt.text = item.Description;
        }

        UpdatePrice();
    }

    private void UpdatePrice()
    {
        if (item == null)
            return;

        finalPrice = Mathf.Max(0, Mathf.RoundToInt(item.BuyPrice * (1f - discountPercent / 100f)));
        bool hasDiscount = discountPercent > 0;
        bool canAfford = LocalPlayerSettings.PlayerPoints >= finalPrice;

        if (discountTxt != null)
        {
            discountTxt.gameObject.SetActive(hasDiscount);

            if (hasDiscount)
            {
                discountTxt.text = $"Sell {discountPercent}%";

                if (!discountActive)
                    PlayDiscountPulse();
            }
            else
            {
                discountTxt.rectTransform.DOKill();
                discountTxt.rectTransform.localScale = Vector3.one;
            }
        }

        discountActive = hasDiscount;

        if (priceTxt != null)
        {
            priceTxt.text = finalPrice.ToString();
            priceTxt.color = !canAfford ? notEnoughColor : (hasDiscount ? discountColor : normalColor);
        }

        if (frameImg != null)
            frameImg.color = !canAfford ? notEnoughColor : (hasDiscount ? discountColor : normalColor);

        if (buyButton != null)
            buyButton.interactable = canAfford;
    }

    public void OnPlayerPointsChanged()
    {
        UpdatePrice();
    }

    /// <summary>
    /// Мягкая пульсация текста скидки. Случайная начальная задержка расинхронизирует
    /// пульс у разных карточек магазина.
    /// </summary>
    private void PlayDiscountPulse()
    {
        if (discountTxt == null)
            return;

        RectTransform rect = discountTxt.rectTransform;

        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOScale(Vector3.one * 1.15f, 0.6f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetDelay(Random.Range(0f, 0.8f));
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
            BuyPrice = item.BuyPrice,
            Description = item.Description,
            Class = item.ItemClass
        };

        ItemCatalog.Register(def);
    }
}