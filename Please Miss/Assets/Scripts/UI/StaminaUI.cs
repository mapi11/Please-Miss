using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private GameObject staminaPanel;
    [SerializeField] private TMP_Text shoveCooldownText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color exhaustedColor = Color.red;

    [Header("Insufficient Flash")]
    [SerializeField] private Color insufficientColor = Color.red;
    [SerializeField] private float insufficientDuration = 0.5f;

    private Image staminaFill;
    private bool exhausted;
    private Coroutine insufficientRoutine;

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

    public void SetExhausted(bool value)
    {
        exhausted = value;
        if (staminaFill != null)
            staminaFill.color = value ? exhaustedColor : normalColor;
    }

    public void FlashInsufficient()
    {
        if (staminaFill == null || !isActiveAndEnabled)
            return;

        if (insufficientRoutine != null)
            StopCoroutine(insufficientRoutine);

        insufficientRoutine = StartCoroutine(InsufficientRoutine());
    }

    private System.Collections.IEnumerator InsufficientRoutine()
    {
        staminaFill.color = insufficientColor;
        yield return new WaitForSecondsRealtime(insufficientDuration);
        staminaFill.color = exhausted ? exhaustedColor : normalColor;
        insufficientRoutine = null;
    }

    public void UpdateShoveCooldown(float remaining)
    {
        if (shoveCooldownText == null) return;

        if (remaining <= 0f)
        {
            if (shoveCooldownText.gameObject.activeSelf)
                shoveCooldownText.gameObject.SetActive(false);
            return;
        }

        if (!shoveCooldownText.gameObject.activeSelf)
            shoveCooldownText.gameObject.SetActive(true);

        shoveCooldownText.text = remaining.ToString("0.0");
    }
}
