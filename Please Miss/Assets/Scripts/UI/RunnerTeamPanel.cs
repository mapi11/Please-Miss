using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RunnerTeamPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image colorImage;
    [SerializeField] private GameObject skullObject;
    [SerializeField] private GameObject exhaustionObject;
    [SerializeField] private Slider hpSlider;

    private NetworkPlayerName netName;
    private NetworkPlayerColor netColor;
    private PlayerHealth playerHealth;
    private Stamina stamina;

    public void Setup(ulong clientId, NetworkObject playerObj)
    {
        netName = playerObj.GetComponent<NetworkPlayerName>();
        netColor = playerObj.GetComponent<NetworkPlayerColor>();
        playerHealth = playerObj.GetComponent<PlayerHealth>();
        stamina = playerObj.GetComponent<Stamina>();

        if (nameText != null && netName != null)
        {
            nameText.text = netName.CurrentName;
            netName.OnNameUpdated += OnNameUpdated;
        }
        if (colorImage != null && netColor != null)
        {
            colorImage.color = netColor.CurrentColor;
            netColor.OnColorUpdated += OnColorUpdated;
        }
        if (skullObject != null && playerHealth != null)
        {
            skullObject.SetActive(playerHealth.IsDead);
            playerHealth.OnDeathStateChanged += OnDeathChanged;
            playerHealth.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaximumHealth);
        }
        if (exhaustionObject != null && stamina != null)
        {
            exhaustionObject.SetActive(stamina.IsSlowed);
            stamina.OnStaminaExhausted += OnStaminaExhausted;
            stamina.OnStaminaRecovered += OnStaminaRecovered;
        }
    }

    private void OnDestroy()
    {
        if (netName != null) netName.OnNameUpdated -= OnNameUpdated;
        if (netColor != null) netColor.OnColorUpdated -= OnColorUpdated;
        if (playerHealth != null)
        {
            playerHealth.OnDeathStateChanged -= OnDeathChanged;
            playerHealth.OnHealthChanged -= OnHealthChanged;
        }
        if (stamina != null)
        {
            stamina.OnStaminaExhausted -= OnStaminaExhausted;
            stamina.OnStaminaRecovered -= OnStaminaRecovered;
        }
    }

    private void OnNameUpdated(string name)
    {
        if (nameText != null) nameText.text = name;
    }

    private void OnColorUpdated(Color32 color)
    {
        if (colorImage != null) colorImage.color = color;
    }

    private void OnDeathChanged(bool dead)
    {
        if (skullObject != null) skullObject.SetActive(dead);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (hpSlider != null)
            hpSlider.value = max > 0f ? current / max : 0f;
    }

    private void OnStaminaExhausted()
    {
        if (exhaustionObject != null) exhaustionObject.SetActive(true);
    }

    private void OnStaminaRecovered()
    {
        if (exhaustionObject != null) exhaustionObject.SetActive(false);
    }
}
