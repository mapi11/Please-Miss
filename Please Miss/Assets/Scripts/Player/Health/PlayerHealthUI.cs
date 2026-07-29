using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject healthContent;
    [SerializeField] private GameObject deadPanel;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged += RefreshHealth;
        playerHealth.OnDeathStateChanged += RefreshDeath;
        RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaximumHealth);
    }

    private void OnDisable()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged -= RefreshHealth;
        playerHealth.OnDeathStateChanged -= RefreshDeath;
    }

    private void Update()
    {
        if (playerHealth == null || healthContent == null)
            return;

        PlayerRoleState role = playerHealth.GetComponent<PlayerRoleState>();
        bool isRunner = role != null && role.IsRunner;
        bool shouldShow = playerHealth.IsSpawned && playerHealth.IsOwner && isRunner && !playerHealth.IsDead;

        if (healthContent.activeSelf != shouldShow)
            healthContent.SetActive(shouldShow);
    }

    private void RefreshHealth(float current, float maximum)
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maximum;
            healthSlider.value = current;
        }

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
    }

    private void RefreshDeath(bool dead)
    {
        if (deadPanel == null)
            return;

        if (playerHealth != null && playerHealth.IsSpawned && !playerHealth.IsOwner)
            return;

        deadPanel.SetActive(dead);
    }
}
