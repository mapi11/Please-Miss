using TMPro;
using UnityEngine;

public class SpectatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text targetNameText;

    private SpectatorController spectator;

    private void Update()
    {
        if (spectator == null)
        {
            var local = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (local != null)
                spectator = local.GetComponent<SpectatorController>();
            return;
        }

        if (targetNameText != null)
            targetNameText.text = spectator.IsSpectating ? spectator.GetCurrentTargetName() : "";
    }
}
