using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель информации о предмете в меню инвентаря (изначально скрыта).
/// Показывается при клике на карточку предмета или на слот снаряжения.
/// </summary>
public class ItemInfoPanel : MonoBehaviour
{
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text buyPriceText;
    [SerializeField] private TMP_Text sellPriceText;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;
    [SerializeField] private Button sellButton;

    private Action onEquip;
    private Action onSell;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        // НЕ вызывать SetActive(false) здесь: если панель стартует неактивной в префабе,
        // Awake откладывается до первого показа, гасит панель сразу после SetActive(true),
        // и первый клик по карточке «не работает» (второй уже работает).
    }

    /// <summary>
    /// Режим предмета: иконка, имя, описание, цены и кнопка действия.
    /// Для карточки — Equip (скрывается, если слоты снаряжения заполнены или предмет уже в слотах); для слота — без кнопок, только Sell.
    /// </summary>
    public void ShowItem(ItemDefinition def, bool canEquip, string equipLabel, bool showSell, Action onEquip, Action onSell)
    {
        this.onEquip = onEquip;
        this.onSell = onSell;

        ApplyDefVisuals(def);

        if (nameText != null)
            nameText.text = def != null ? def.DisplayName : "";

        if (descriptionText != null)
            descriptionText.text = def != null ? def.Description : "";

        if (buyPriceText != null)
            buyPriceText.text = def != null ? $"Buy: {def.BuyPrice}" : "";

        if (sellPriceText != null)
            sellPriceText.text = def != null ? $"Sell: {def.SellPrice}" : "";

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(canEquip);
            equipButton.interactable = canEquip;
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(OnEquipClicked);
        }

        if (equipButtonText != null)
            equipButtonText.text = equipLabel;

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(showSell);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellClicked);
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        PlayShowAnimation();
    }

    /// <summary>
    /// Режим просмотра слота снаряжения: иконка, имя, описание, цены и кнопка Sell.
    /// Кнопка Unequip отсутствует — снятие предмета происходит на самой карточке (ButtonUnequip).
    /// </summary>
    public void ShowInfoOnly(ItemDefinition def, Action onSell = null)
    {
        onEquip = null;
        this.onSell = onSell;

        ApplyDefVisuals(def);

        if (nameText != null)
            nameText.text = def != null ? def.DisplayName : "";

        if (descriptionText != null)
            descriptionText.text = def != null ? def.Description : "";

        if (buyPriceText != null)
            buyPriceText.text = def != null ? $"Buy: {def.BuyPrice}" : "";

        if (sellPriceText != null)
            sellPriceText.text = def != null ? $"Sell: {def.SellPrice}" : "";

        if (equipButton != null)
            equipButton.gameObject.SetActive(false);

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(onSell != null);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellClicked);
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        PlayShowAnimation();
    }

    public void Hide()
    {
        transform.DOKill();
        gameObject.SetActive(false);
    }

    private void PlayShowAnimation()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * 0.85f;
        transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack, 1.2f);
    }

    private void ApplyDefVisuals(ItemDefinition def)
    {
        if (iconImg == null)
            return;

        bool hasDef = def != null;
        bool hasSprite = hasDef && def.IconSprite != null;

        iconImg.enabled = hasDef;
        iconImg.sprite = hasDef ? def.IconSprite : null;
        iconImg.color = hasSprite ? Color.white : (hasDef ? def.IconColor : Color.white);
    }

    private void OnEquipClicked()
    {
        onEquip?.Invoke();
    }

    private void OnSellClicked()
    {
        onSell?.Invoke();
    }
}
