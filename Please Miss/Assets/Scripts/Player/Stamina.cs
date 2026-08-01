using Unity.Netcode;
using UnityEngine;

public class Stamina : NetworkBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainRate = 40f;
    [SerializeField] private float regenRate = 25f;
    [SerializeField] private float regenDelay = 0.3f;

    [Header("Actions")]
    [SerializeField] private float jumpCost = 10f;
    [SerializeField] private float dashCost = 25f;

    [Header("Slow")]
    [SerializeField] private float slowMultiplier = 0.8f;
    [SerializeField, Range(0f, 1f)] private float regenThreshold = 0.1f;

    [Header("UI")]
    [SerializeField] private StaminaUI staminaUI;

    private float currentStamina;
    private float lastSprintTime;
    private bool uiShown;

    private readonly NetworkVariable<bool> isExhausted = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float Normalized => currentStamina / maxStamina;
    public bool CanSprint => !isExhausted.Value && currentStamina > 0f;
    public bool IsSlowed => isExhausted.Value;
    public float SpeedMultiplier => isExhausted.Value ? slowMultiplier : 1f;
    public float JumpCost => jumpCost;
    public float DashCost => dashCost;

    public bool CanConsume(float amount) => !isExhausted.Value && currentStamina >= amount;

    public event System.Action<float> OnStaminaChanged;
    public event System.Action OnStaminaExhausted;
    public event System.Action OnStaminaRecovered;

    private void Awake()
    {
        currentStamina = maxStamina;

        if (staminaUI == null)
            staminaUI = GetComponentInChildren<StaminaUI>();

        isExhausted.OnValueChanged += (_, newValue) =>
        {
            if (staminaUI != null)
                staminaUI.SetExhausted(newValue);
            if (newValue)
                OnStaminaExhausted?.Invoke();
            else
                OnStaminaRecovered?.Invoke();
        };
    }

    public void Consume(float amount)
    {
        currentStamina -= amount;
        lastSprintTime = Time.time;

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            isExhausted.Value = true;
        }

        OnStaminaChanged?.Invoke(Normalized);
        if (staminaUI != null)
            staminaUI.UpdateValue(Normalized);
    }

    public void Tick(float deltaTime, bool isSprinting)
    {
        if (staminaUI != null && !uiShown)
        {
            staminaUI.Show();
            staminaUI.UpdateValue(Normalized);
            uiShown = true;
        }

        if (isSprinting && CanSprint)
        {
            currentStamina -= drainRate * deltaTime;
            lastSprintTime = Time.time;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isExhausted.Value = true;
            }

            OnStaminaChanged?.Invoke(Normalized);
            if (staminaUI != null)
                staminaUI.UpdateValue(Normalized);
        }
        else
        {
            float timeSinceSprint = Time.time - lastSprintTime;

            if (timeSinceSprint >= regenDelay)
            {
                currentStamina += regenRate * deltaTime;

                if (currentStamina > maxStamina)
                    currentStamina = maxStamina;

                OnStaminaChanged?.Invoke(Normalized);
                if (staminaUI != null)
                    staminaUI.UpdateValue(Normalized);
            }

            if (isExhausted.Value && currentStamina >= maxStamina * regenThreshold)
            {
                isExhausted.Value = false;
            }
        }
    }
}
