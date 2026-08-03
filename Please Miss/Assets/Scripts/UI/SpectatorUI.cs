using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text targetNameText;
    [SerializeField] private Slider visibilitySlider;
    [SerializeField] private TMP_Text visibilityTimerText;

    private SpectatorController spectator;

    public void Initialize(SpectatorController controller)
    {
        spectator = controller;
    }

    private void Update()
    {
        if (spectator == null)
            spectator = GetComponentInParent<SpectatorController>();

        if (spectator == null)
            return;

        if (targetNameText != null)
            targetNameText.text = spectator.IsSpectating ? spectator.GetCurrentTargetName() : "";

        if (visibilitySlider != null)
            visibilitySlider.value = spectator.VisibilitySliderFill;

        if (visibilityTimerText != null)
        {
            if (spectator.VisibilityActive)
                visibilityTimerText.text = $"{spectator.VisibilityTimeLeft:0.0}";
            else if (spectator.CooldownActive)
                visibilityTimerText.text = $"{spectator.CooldownTimeLeft:0.0}";
            else
                visibilityTimerText.text = "";
        }
    }
}
