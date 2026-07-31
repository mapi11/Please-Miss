using TMPro;
using UnityEngine;

public class SpectatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text targetNameText;

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
    }
}
