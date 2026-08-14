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

    [Header("Dash Threshold")]
    [SerializeField] private RectTransform dashThresholdMarker;
    [SerializeField] private RectTransform regenThresholdMarker;

    [Header("Insufficient Flash")]
    [SerializeField] private Color insufficientColor = Color.red;
    [SerializeField] private float insufficientDuration = 0.5f;

    private Image staminaFill;
    private bool exhausted;
    private Coroutine insufficientRoutine;
    private float cachedDashThreshold = 0.25f;
    private float cachedRegenThreshold = 0.1f;

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

        if (dashThresholdMarker != null)
            StartCoroutine(PositionThresholdMarker(dashThresholdMarker, cachedDashThreshold));

        if (regenThresholdMarker != null)
            StartCoroutine(PositionThresholdMarker(regenThresholdMarker, cachedRegenThreshold));
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

    public void SetDashThreshold(float normalized)
    {
        cachedDashThreshold = Mathf.Clamp01(normalized);

        if (dashThresholdMarker != null)
            StartCoroutine(PositionThresholdMarker(dashThresholdMarker, cachedDashThreshold));
    }

    public void SetRegenThreshold(float normalized)
    {
        cachedRegenThreshold = Mathf.Clamp01(normalized);

        if (regenThresholdMarker != null)
            StartCoroutine(PositionThresholdMarker(regenThresholdMarker, cachedRegenThreshold));
    }

    private System.Collections.IEnumerator PositionThresholdMarker(RectTransform marker, float normalized)
    {
        yield return null;

        Canvas.ForceUpdateCanvases();

        var fillArea = staminaSlider != null && staminaSlider.fillRect != null
            ? staminaSlider.fillRect.parent as RectTransform
            : null;

        if (fillArea == null || marker == null)
            yield break;

        float markerWidth = marker.rect.width;
        float markerHeight = marker.rect.height;

        marker.anchorMin = new Vector2(0f, 0.5f);
        marker.anchorMax = new Vector2(0f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(markerWidth, markerHeight);
        marker.anchoredPosition = new Vector2(normalized * fillArea.rect.width, marker.anchoredPosition.y);
    }

    public void SetExhausted(bool value)
    {
        exhausted = value;
        if (staminaFill != null)
            staminaFill.color = value ? exhaustedColor : normalColor;
        if (regenThresholdMarker != null)
            regenThresholdMarker.gameObject.SetActive(value);
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
