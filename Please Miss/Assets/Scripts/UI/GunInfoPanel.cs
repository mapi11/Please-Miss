using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Панель информации о винтовке в магазине (вкладка Guns): иконка, название, описание,
/// каждая характеристика отдельной строкой и кнопка покупки.
/// Открывается кнопкой на карточке оружия. Анимация показа — как в инвентаре.
/// </summary>
public class GunInfoPanel : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text magazineText;
    [SerializeField] private TMP_Text velocityText;
    [SerializeField] private TMP_Text swayText;
    [SerializeField] private TMP_Text scopeText;
    [SerializeField] private TMP_Text recoilText;
    [SerializeField] private TMP_Text priceTxt;
    [SerializeField] private TMP_Text discountTxt;
    [SerializeField] private Button buyButton;
    [Tooltip("Контейнер с контентом кнопки покупки (текст и т.п.). Скрывается у купленной винтовки, сама кнопка остаётся")]
    [SerializeField] private RectTransform buyButtonContent;
    [SerializeField] private TMP_Text buyButtonText;

    [Header("Colors")]
    [SerializeField] private Color normalPriceColor = Color.white;
    [SerializeField] private Color discountPriceColor = Color.yellow;
    [SerializeField] private Color normalBuyButtonColor = Color.green;
    [SerializeField] private Color notEnoughBuyButtonColor = new Color(0.6f, 0.25f, 0.25f, 1f);

    [Header("Animation")]
    [Tooltip("Начальный масштаб анимации появления")]
    [SerializeField] private float showFromScale = 0.85f;
    [Tooltip("Длительность анимации появления")]
    [SerializeField] private float showDuration = 0.25f;

    private SniperRifleHeldVisual heldVisual;
    private Action onBuy;
    private int price;
    private bool owned;
    private bool hasDiscount;
    private int discountPercent;
    private bool discountActive;
    private string cachedDescription;

    public bool IsOpen => gameObject.activeSelf;

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
        UpdateState();
    }

    private string Loc(string key)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Table", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    /// <summary>Показывает панель с информацией о винтовке. Awake не гасит панель:
    /// префаб должен стартовать неактивным, иначе панель будет скрываться при первом показе.</summary>
    public void Show(SniperRifleHeldVisual held, Sprite icon, string description, int price, bool hasDiscount, int discountPercent, Action onBuy)
    {
        heldVisual = held;
        this.onBuy = onBuy;
        this.price = Mathf.Max(0, price);
        this.hasDiscount = hasDiscount;
        this.discountPercent = Mathf.Max(0, discountPercent);

        SniperRifleDefinition def = held != null ? held.Definition : null;
        string rifleId = def != null ? def.RifleId : (held != null ? held.name : "");
        owned = !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);
        cachedDescription = description ?? "";

        if (iconImg != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = icon;
            iconImg.color = Color.white;
        }

        RefreshTexts();

        if (magazineText != null)
            magazineText.text = def != null ? def.MagazineSize.ToString() : "";
        if (velocityText != null)
            velocityText.text = def != null ? def.MuzzleVelocity.ToString() : "";
        if (swayText != null)
            swayText.text = def != null ? def.SwayAmplitude.ToString() : "";
        if (scopeText != null)
            scopeText.text = def != null ? $"{def.MinimumMagnification}x - {def.MaximumMagnification}x" : "";
        if (recoilText != null)
            recoilText.text = def != null ? def.SecondsBetweenShots.ToString() : "";

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy?.Invoke());
        }

        UpdateState();

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        PlayShowAnimation();
    }

    /// <summary>Обновляет кнопку покупки (доступность, цвет) и цену при изменении очков.</summary>
    public void OnPlayerPointsChanged()
    {
        UpdateState();
    }

    public void Hide()
    {
        transform.DOKill();
        gameObject.SetActive(false);
    }

    private void RefreshTexts()
    {
        if (nameTxt == null && descriptionText == null)
            return;

        SniperRifleDefinition def = heldVisual != null ? heldVisual.Definition : null;
        string rifleId = def != null ? def.RifleId : (heldVisual != null ? heldVisual.name : "");

        if (nameTxt != null)
        {
            string fallback = def != null && !string.IsNullOrEmpty(def.DisplayName)
                ? def.DisplayName
                : (heldVisual != null ? heldVisual.name : "");
            nameTxt.text = ItemLocalization.GetName(rifleId, fallback);
        }

        if (descriptionText != null)
            descriptionText.text = ItemLocalization.GetDescription(rifleId, cachedDescription);
    }

    private void UpdateState()
    {
        SniperRifleDefinition def = heldVisual != null ? heldVisual.Definition : null;
        string rifleId = def != null ? def.RifleId : (heldVisual != null ? heldVisual.name : "");
        owned = !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);

        if (buyButton != null)
        {
            if (buyButtonContent != null)
                buyButtonContent.gameObject.SetActive(!owned);

            if (!owned)
            {
                bool enoughPoints = LocalPlayerSettings.PlayerPoints >= price;
                Color buttonColor = enoughPoints ? normalBuyButtonColor : notEnoughBuyButtonColor;

                ColorBlock colors = buyButton.colors;
                colors.normalColor = buttonColor;
                colors.highlightedColor = buttonColor;
                colors.pressedColor = buttonColor;
                colors.disabledColor = buttonColor;
                buyButton.colors = colors;
                buyButton.interactable = enoughPoints;

                if (buyButtonText != null)
                    buyButtonText.text = Loc("Buy");
            }
        }

        if (priceTxt != null)
        {
            if (owned)
            {
                priceTxt.text = Loc("Owned");
                priceTxt.color = normalPriceColor;
            }
            else
            {
                priceTxt.text = price.ToString();
                priceTxt.color = hasDiscount ? discountPriceColor : normalPriceColor;
            }
        }

        if (discountTxt != null)
        {
            if (owned || !hasDiscount)
            {
                discountTxt.gameObject.SetActive(false);
                discountTxt.rectTransform.DOKill();
                discountTxt.rectTransform.localScale = Vector3.one;
                discountActive = false;
            }
            else
            {
                discountTxt.gameObject.SetActive(true);
                discountTxt.text = string.Format(Loc("Sell discount"), discountPercent);

                if (!discountActive)
                    PlayDiscountPulse();
            }
        }

        RefreshTexts();
    }

    /// <summary>
    /// Мягкая пульсация текста скидки (как у карточек предметов в магазине).
    /// Случайная начальная задержка расинхронизирует пульс.
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
            .SetDelay(UnityEngine.Random.Range(0f, 0.8f));

        discountActive = true;
    }

    private void PlayShowAnimation()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * showFromScale;
        transform.DOScale(Vector3.one, showDuration)
            .SetEase(Ease.OutBack);
    }
}
