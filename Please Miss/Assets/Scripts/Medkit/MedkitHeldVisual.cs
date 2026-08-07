using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Medkit functionality while held. Add this component to the Medkit_Held prefab.
/// Hold RMB — heal yourself; hold LMB — heal the player inside the zone in front of you (the healer earns points).
/// Releasing the button cancels the channel. The server is the authority on health and consumption (MedkitController).
/// </summary>
public sealed class MedkitHeldVisual : MonoBehaviour
{
    [Header("Healing")]
    [Tooltip("Сколько HP восстанавливает одна аптечка")]
    [Min(0f)] [SerializeField] private float healAmount = 50f;

    [Header("Channel")]
    [Tooltip("Сколько секунд длится лечение")]
    [Min(0.2f)] [SerializeField] private float channelDuration = 3f;

    [Header("Debug")]
    [Tooltip("Логировать причины, почему лечение не началось")]
    [SerializeField] private bool logDebug;

    private enum ChannelMode : byte
    {
        None,
        Self,
        Other
    }

    private MedkitController controller;
    private PlayerHealth selfHealth;
    private Inventory inventory;

    private ChannelMode mode;
    private float channelProgress;
    private int channelSlot = -1;
    private string channelItemName;
    private NetworkObject targetLock;

    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly int healMask = ~GameLayers.InvisibleWallMask;

    private TMP_Text sliderLabel;

    private void Awake()
    {
        controller = GetComponentInParent<MedkitController>();
        selfHealth = GetComponentInParent<PlayerHealth>();
        inventory = GetComponentInParent<Inventory>();

        if (controller == null)
        {
            Debug.LogWarning(
                "[MedkitHeldVisual] MedkitController is missing on the player prefab. " +
                "Add MedkitController to the player root so the medkit can heal.",
                this
            );
        }
    }

    private void Update()
    {
        if (controller == null || !controller.IsOwner || !controller.IsSpawned)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mode != ChannelMode.None)
        {
            bool held = mode == ChannelMode.Self
                ? mouse.rightButton.isPressed
                : mouse.leftButton.isPressed;

            if (!held)
                CancelChannel();

            if (mode != ChannelMode.None)
                TickChannel();

            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
            TryStartSelfHeal();
        else if (mouse.leftButton.wasPressedThisFrame)
            TryStartHealOther();
    }

    private void TickChannel()
    {
        if (!IsChannelValid())
        {
            CancelChannel();
            return;
        }

        channelProgress += Time.deltaTime / Mathf.Max(0.2f, channelDuration);

        if (controller != null && controller.HealSlider != null)
            controller.HealSlider.value = Mathf.Clamp01(channelProgress);

        if (channelProgress >= 1f)
            CompleteChannel();
    }

    private bool IsChannelValid()
    {
        if (selfHealth == null || selfHealth.IsDead)
            return false;

        if (mode == ChannelMode.Other)
        {
            if (targetLock == null || !targetLock.IsSpawned)
                return false;

            PlayerHealth targetHealth = targetLock.GetComponent<PlayerHealth>();
            if (targetHealth == null || targetHealth.IsDead)
                return false;

            float range = controller != null ? controller.MaxHealRange : 3f;
            if (Vector3.Distance(controller.transform.position, targetLock.transform.position) > range)
                return false;
        }

        return true;
    }

    private void TryStartSelfHeal()
    {
        if (selfHealth == null || selfHealth.IsDead)
        {
            if (logDebug) Debug.Log("[MedkitDebug] Self heal: self is dead or no PlayerHealth", this);
            return;
        }
        if (selfHealth.CurrentHealth >= selfHealth.MaximumHealth - 0.01f)
        {
            if (logDebug) Debug.Log($"[MedkitDebug] Self heal: already at full HP ({selfHealth.CurrentHealth}/{selfHealth.MaximumHealth})", this);
            return;
        }

        if (!CaptureActiveItem())
        {
            if (logDebug) Debug.Log("[MedkitDebug] Self heal: no medkit in active slot", this);
            return;
        }

        mode = ChannelMode.Self;
        targetLock = null;
        channelProgress = 0f;
        ShowSlider(BuildLabel("Self Heal"));
    }

    private void TryStartHealOther()
    {
        if (!TryFindHealTarget(out NetworkObject target, out PlayerHealth targetHealth))
        {
            if (logDebug) Debug.Log("[MedkitDebug] Heal other: no valid target in view", this);
            return;
        }

        if (targetHealth.IsDead)
        {
            if (logDebug) Debug.Log("[MedkitDebug] Heal other: target is dead", this);
            return;
        }
        if (targetHealth.CurrentHealth >= targetHealth.MaximumHealth - 0.01f)
        {
            if (logDebug) Debug.Log($"[MedkitDebug] Heal other: target at full HP ({targetHealth.CurrentHealth}/{targetHealth.MaximumHealth})", this);
            return;
        }

        if (!CaptureActiveItem())
        {
            if (logDebug) Debug.Log("[MedkitDebug] Heal other: no medkit in active slot", this);
            return;
        }

        mode = ChannelMode.Other;
        targetLock = target;
        channelProgress = 0f;
        ShowSlider(BuildLabel("Healing Ally"));
    }

    private bool CaptureActiveItem()
    {
        if (inventory == null) return false;

        channelSlot = inventory.ActiveSlot;
        if (channelSlot < 0) return false;

        channelItemName = inventory.GetItemAtSlot(channelSlot);
        return !string.IsNullOrEmpty(channelItemName);
    }

    private void CompleteChannel()
    {
        if (controller == null)
        {
            CancelChannel();
            return;
        }

        switch (mode)
        {
            case ChannelMode.Self:
                controller.HealSelfServerRpc(healAmount);
                break;
            case ChannelMode.Other:
                if (targetLock != null)
                    controller.HealOtherServerRpc(new NetworkObjectReference(targetLock), healAmount);
                break;
        }

        controller.ConsumeMedkitServerRpc(channelSlot, new FixedString32Bytes(channelItemName ?? ""));

        HideSlider();

        if (inventory != null && inventory.ActiveSlot == channelSlot)
            inventory.RemoveItem(channelSlot);

        mode = ChannelMode.None;
        channelProgress = 0f;
        channelSlot = -1;
        channelItemName = null;
        targetLock = null;
    }

    private void CancelChannel()
    {
        HideSlider();

        mode = ChannelMode.None;
        channelProgress = 0f;
        channelSlot = -1;
        channelItemName = null;
        targetLock = null;
    }

    private void OnDisable()
    {
        CancelChannel();

        sliderLabel = null;
    }

    // --- Поиск цели лечения (ЛКМ) ---

    private bool TryFindHealTarget(out NetworkObject target, out PlayerHealth targetHealth)
    {
        target = null;
        targetHealth = null;

        if (controller == null)
        {
            if (logDebug) Debug.Log("[MedkitDebug] FindTarget: no controller", this);
            return false;
        }

        float range = Mathf.Max(0.5f, controller.MaxHealRange);
        Vector3 zoneCenter = controller.transform.position + controller.transform.forward * (range * 0.5f);
        Vector3 halfExtents = new Vector3(
            controller.ZoneWidth * 0.5f,
            controller.ZoneHeight * 0.5f,
            range * 0.5f
        );

        int count = Physics.OverlapBoxNonAlloc(
            zoneCenter,
            halfExtents,
            overlapBuffer,
            controller.transform.rotation,
            healMask,
            QueryTriggerInteraction.Collide
        );
        if (logDebug) Debug.Log($"[MedkitDebug] FindTarget: {count} collider(s) in zone", this);

        float nearestDistance = float.MaxValue;
        PlayerHealth bestHealth = null;
        NetworkObject bestNetObj = null;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            if (col.transform == controller.transform ||
                col.transform.IsChildOf(controller.transform))
                continue;

            PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
            if (health == null || health == selfHealth || !health.IsSpawned)
            {
                if (logDebug)
                    Debug.Log(
                        $"[MedkitDebug] FindTarget: skipped '{col.name}' " +
                        $"(layer {col.gameObject.layer}, trigger {col.isTrigger}): " +
                        $"health={health != null}, self={health == selfHealth}, " +
                        $"spawned={health != null && health.IsSpawned}",
                        this
                    );
                continue;
            }

            NetworkObject netObj = health.GetComponent<NetworkObject>();
            if (netObj == null) continue;

            if (health.CurrentHealth >= health.MaximumHealth - 0.01f)
            {
                if (logDebug)
                    Debug.Log(
                        $"[MedkitDebug] FindTarget: skipped '{col.name}': full HP " +
                        $"({health.CurrentHealth}/{health.MaximumHealth})",
                        this
                    );
                continue;
            }

            float distance = Vector3.Distance(controller.transform.position, health.transform.position);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            bestHealth = health;
            bestNetObj = netObj;
        }

        target = bestNetObj;
        targetHealth = bestHealth;

        if (logDebug)
            Debug.Log(
                $"[MedkitDebug] FindTarget: result {(target != null ? target.gameObject.name : "NONE")} " +
                $"at {nearestDistance:0.00}m",
                this
            );

        return target != null;
    }

    // --- Слайдер лечения ---

    private void ShowSlider(string label)
    {
        if (controller == null || controller.HealSliderContent == null)
            return;

        if (sliderLabel == null)
            sliderLabel = controller.HealSliderContent.GetComponentInChildren<TMP_Text>(true);

        controller.HealSliderContent.SetActive(true);

        if (controller.HealSlider != null)
            controller.HealSlider.value = 0f;

        if (sliderLabel != null)
            sliderLabel.text = label;
    }

    private void HideSlider()
    {
        if (controller != null && controller.HealSliderContent != null)
            controller.HealSliderContent.SetActive(false);
    }

    private string BuildLabel(string action)
    {
        return $"{action} (+{healAmount:0} HP)";
    }
}
