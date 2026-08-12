using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Карточка оружия в списке магазина (вкладка Guns): иконка, название, цена и кнопка,
/// открывающая панель информации о винтовке. Покупка происходит в панели.
/// </summary>
public class GunShopCard : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private TMP_Text priceTxt;
    [SerializeField] private Button infoButton;

    [Header("Colors")]
    [Tooltip("Обычный цвет цены")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Цвет цены при активной скидке")]
    [SerializeField] private Color discountColor = Color.yellow;
    [Tooltip("Цвет текста у уже купленной винтовки")]
    [SerializeField] private Color ownedColor = new Color(0.45f, 0.75f, 0.5f, 1f);
    private int price;
    private bool owned;
    private bool hasDiscount;
    private SniperRifleHeldVisual heldVisual;

    private void Awake()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        RefreshName();
        UpdatePrice();
    }

    private string Loc(string key)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Table", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public void Setup(SniperRifleHeldVisual held, Sprite icon, Action onInfo, int price, bool hasDiscount)
    {
        this.price = Mathf.Max(0, price);
        this.hasDiscount = hasDiscount;
        heldVisual = held;

        SniperRifleDefinition def = held != null ? held.Definition : null;
        string rifleId = def != null ? def.RifleId : (held != null ? held.name : "");
        owned = !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);

        if (iconImg != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = icon;
            iconImg.color = Color.white;
        }

        RefreshName();

        UpdatePrice();

        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(() => onInfo?.Invoke());
        }
    }

    /// <summary>Обновляет цену/статус без пересоздания карточки (например, после покупки).</summary>
    public void RefreshState()
    {
        SniperRifleDefinition def = heldVisual != null ? heldVisual.Definition : null;
        string rifleId = def != null ? def.RifleId : (heldVisual != null ? heldVisual.name : "");
        owned = !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);
        UpdatePrice();
    }

    private void RefreshName()
    {
        if (nameTxt == null)
            return;

        SniperRifleDefinition def = heldVisual != null ? heldVisual.Definition : null;
        string rifleId = def != null ? def.RifleId : (heldVisual != null ? heldVisual.name : "");
        string fallback = def != null && !string.IsNullOrEmpty(def.DisplayName)
            ? def.DisplayName
            : (heldVisual != null ? heldVisual.name : "");

        nameTxt.text = ItemLocalization.GetName(rifleId, fallback);
    }

    private void UpdatePrice()
    {
        if (priceTxt == null)
            return;

        if (owned)
        {
            priceTxt.text = Loc("Owned");
            priceTxt.color = ownedColor;
            return;
        }

        priceTxt.text = price.ToString();
        priceTxt.color = hasDiscount ? discountColor : normalColor;
    }
}
