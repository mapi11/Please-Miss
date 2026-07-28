using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject deadPanel;
    [SerializeField] private GameObject healthContent;
    [SerializeField] private bool showOnlyForOwner;

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
        RefreshAll();
    }

    private void Start()
    {
        if (playerHealth == null)
            return;

        UpdateLobbyState();
    }

    private void Update()
    {
        UpdateLobbyState();
    }

    private void UpdateLobbyState()
    {
        if (playerHealth == null)
            return;

        if (FindObjectOfType<LobbyManager>() != null && LobbyManager.IsInLobby)
        {
            SetContentActive(false);
            return;
        }

        if (playerHealth.IsSpawned && !playerHealth.IsOwner)
        {
            if (showOnlyForOwner)
            {
                SetContentActive(false);
                return;
            }
        }

        PlayerRoleState role = playerHealth.GetComponent<PlayerRoleState>();
        if (role != null && role.IsSniper)
        {
            SetContentActive(false);
            return;
        }

        SetContentActive(true);
    }

    private void OnDisable()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged -= RefreshHealth;
        playerHealth.OnDeathStateChanged -= RefreshDeath;
    }

    private void RefreshAll()
    {
        if (playerHealth == null)
            return;

        RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaximumHealth);
        RefreshDeath(playerHealth.IsDead);
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

    private void SetContentActive(bool active)
    {
        if (healthContent != null)
            healthContent.SetActive(active);
        else
            gameObject.SetActive(active);
    }
}
