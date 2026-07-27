using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private GameObject staminaPanel;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color exhaustedColor = Color.red;

    private Image staminaFill;

    private void Awake()
    {
        if (staminaSlider == null)
            staminaSlider = GetComponentInChildren<Slider>();

        if (staminaSlider != null && staminaSlider.fillRect != null)
            staminaFill = staminaSlider.fillRect.GetComponent<Image>();

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 1f;
        }
    }

    public void Show()
    {
        if (staminaPanel != null)
            staminaPanel.SetActive(true);
    }

    public void Hide()
    {
        if (staminaPanel != null)
            staminaPanel.SetActive(false);
    }

    public void UpdateValue(float normalized)
    {
        if (staminaSlider != null)
            staminaSlider.value = normalized;
    }

    public void SetExhausted(bool exhausted)
    {
        if (staminaFill != null)
            staminaFill.color = exhausted ? exhaustedColor : normalColor;
    }
}
