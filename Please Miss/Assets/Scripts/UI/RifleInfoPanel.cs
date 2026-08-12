using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Панель информации о винтовке в меню инвентаря (изначально скрыта).
/// Показывается при клике на карточку оружия во вкладке Guns у снайпера.
/// Отличается от ItemInfoPanel: характеристики вместо цен, кнопка Equip/Equiped.
/// </summary>
public class RifleInfoPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;

    private Action onEquip;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        // НЕ вызывать SetActive(false) здесь: если панель стартует неактивной в префабе,
        // Awake откладывается до первого показа и гасит панель после SetActive(true).
    }

    public void Show(RifleCatalog.RifleInfo info, bool isEquipped, Action onEquip)
    {
        this.onEquip = onEquip;

        if (nameText != null)
            nameText.text = info != null
                ? ItemLocalization.GetName(info.RifleId, info.DisplayName)
                : "";

        if (descriptionText != null)
            descriptionText.text = info != null
                ? ItemLocalization.GetDescription(info.RifleId, info.Description)
                : "";

        if (statsText != null)
            statsText.text = string.Join("\n", BuildStatsLines(info));

        if (equipButton != null)
        {
            equipButton.interactable = !isEquipped;
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(OnEquipClicked);
        }

        if (equipButtonText != null)
            equipButtonText.text = isEquipped ? "Equiped" : "Equip";

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnEquipClicked()
    {
        onEquip?.Invoke();
    }

    private static string Loc(string key)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Table", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    private static string[] BuildStatsLines(RifleCatalog.RifleInfo info)
    {
        SniperRifleDefinition d = info != null ? info.Definition : null;
        if (d == null)
            return new[] { "Stats unavailable" };

        return new[]
        {
            $"{Loc("Magazine:")} {d.MagazineSize}",
            $"{Loc("Velocity:")} {d.MuzzleVelocity:0.##}",
            $"{Loc("Sway:")} {d.SwayAmplitude:0.##}",
            $"{Loc("Scope:")} {d.MinimumMagnification:0.#}-{d.MaximumMagnification:0.#}",
            $"{Loc("Recoil:")} {d.RecoilPitchAmount:0.##}"
        };
    }
}
