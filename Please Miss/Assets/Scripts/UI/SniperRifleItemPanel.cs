using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SniperRifleItemPanel : MonoBehaviour
{
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;

    [Header("Stats Info")]
    [Tooltip("Кнопка-иконка \"i\". Ховер по ней показывает панель характеристик")]
    [SerializeField] private Button statsInfoButton;
    [Tooltip("Контейнер внутри панели, в котором появляется панелька со статами")]
    [SerializeField] private RectTransform statsContainer;
    [Tooltip("Панелька характеристик (дочерняя контейнеру, по умолчанию выключена)")]
    [SerializeField] private GameObject statsInfoPanel;
    [SerializeField] private TMP_Text statsText;

    public void Setup(string displayName, Sprite icon, bool showEquipButton, string equipLabel, System.Action onEquip, SniperRifleDefinition definition)
    {
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(showEquipButton);

            if (showEquipButton)
            {
                equipButton.onClick.RemoveAllListeners();
                equipButton.onClick.AddListener(() => onEquip?.Invoke());
            }
        }

        if (equipButtonText != null)
            equipButtonText.text = equipLabel;

        bool isEmpty = string.IsNullOrEmpty(displayName);

        if (iconImg != null)
        {
            iconImg.enabled = !isEmpty;
            iconImg.sprite = icon;
            iconImg.color = icon != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (nameTxt != null)
            nameTxt.text = isEmpty ? "Empty" : displayName;

        SetupStatsInfo(definition);
    }

    private void SetupStatsInfo(SniperRifleDefinition definition)
    {
        if (statsInfoPanel != null)
            statsInfoPanel.SetActive(false);

        bool hasStats = definition != null;

        if (statsInfoButton != null)
            statsInfoButton.gameObject.SetActive(hasStats);

        if (!hasStats)
            return;

        if (statsText != null)
            statsText.text = string.Join("\n", BuildStatsLines(definition));

        if (statsInfoButton != null)
        {
            EventTrigger trigger = statsInfoButton.GetComponent<EventTrigger>();

            if (trigger == null)
                trigger = statsInfoButton.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            EventTrigger.Entry enter = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(_ => SetStatsPanel(true));
            trigger.triggers.Add(enter);

            EventTrigger.Entry exit = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            exit.callback.AddListener(_ => SetStatsPanel(false));
            trigger.triggers.Add(exit);
        }
    }

    private void SetStatsPanel(bool show)
    {
        if (statsInfoPanel != null)
            statsInfoPanel.SetActive(show);
    }

    private static string[] BuildStatsLines(SniperRifleDefinition d)
    {
        if (d == null)
            return new[] { "Stats unavailable" };

        return new[]
        {
            $"Magazine: {d.MagazineSize}",
            $"Muzzle velocity: {d.MuzzleVelocity:0.##}",
            $"Sway amplitude: {d.SwayAmplitude:0.##}",
            $"Scope {d.MinimumMagnification:0.#}-{d.MaximumMagnification:0.#}",
            $"Recoil pitch: {d.RecoilPitchAmount:0.##}"
        };
    }
}
