using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Server-authoritative medkit logic. Add this component to the player root NetworkObject.
/// MedkitHeldVisual (placed on the Medkit_Held prefab) calls these RPCs when the channel completes.
/// </summary>
public sealed class MedkitController : NetworkBehaviour
{
    [Header("Healing")]
    [Tooltip("Максимальная дистанция до союзника для лечения (длина зоны поиска цели)")]
    [Min(0.1f)] [SerializeField] private float maxHealRange = 3f;

    [Header("Target Zone")]
    [Tooltip("Полуширина зоны перед игроком, где можно лечить союзников")]
    [Min(0.1f)] [SerializeField] private float zoneWidth = 2f;
    [Tooltip("Полувысота зоны перед игроком, где можно лечить союзников")]
    [Min(0.1f)] [SerializeField] private float zoneHeight = 2f;

    [Header("UI")]
    [Tooltip("Контент со слайдером лечения на HUD игрока (обычно выключен). Включается, когда начинается лечение.")]
    [SerializeField] private GameObject healSliderContent;
    [Tooltip("Слайдер, который заполняется по прогрессу лечения")]
    [SerializeField] private Slider healSlider;

    public float MaxHealRange => maxHealRange;
    public float ZoneWidth => zoneWidth;
    public float ZoneHeight => zoneHeight;
    public GameObject HealSliderContent => healSliderContent;
    public Slider HealSlider => healSlider;

    [Rpc(SendTo.Server)]
    public void HealSelfServerRpc(float amount)
    {
        if (!IsServer) return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health == null || health.IsDead) return;
        if (health.CurrentHealth >= health.MaximumHealth - 0.01f) return;

        health.ServerAddHealth(Mathf.Clamp(amount, 0f, health.MaximumHealth));
    }

    [Rpc(SendTo.Server)]
    public void HealOtherServerRpc(NetworkObjectReference targetRef, float amount)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out NetworkObject target)) return;

        PlayerHealth healerHealth = GetComponent<PlayerHealth>();
        if (healerHealth == null || healerHealth.IsDead) return;

        PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth.IsDead) return;
        if (targetHealth.CurrentHealth >= targetHealth.MaximumHealth - 0.01f) return;

        if (Vector3.Distance(transform.position, target.transform.position) > maxHealRange) return;

        targetHealth.ServerAddHealth(Mathf.Clamp(amount, 0f, targetHealth.MaximumHealth));

        int reward = GameManager.Instance != null ? GameManager.Instance.HealReward : 50;
        if (reward <= 0) return;

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        HealRewardClientRpc(reward, rpcParams);
    }

    [ClientRpc]
    private void HealRewardClientRpc(int reward, ClientRpcParams rpcParams = default)
    {
        if (reward <= 0) return;

        LocalPlayerSettings.AddPoints(reward);

        if (GameManager.Instance != null)
            GameManager.Instance.NotifyHealReward(reward);
    }

    [Rpc(SendTo.Server)]
    public void ConsumeMedkitServerRpc(int slot, FixedString32Bytes itemName)
    {
        if (!IsServer) return;

        NetworkInventorySync sync = GetComponent<NetworkInventorySync>();
        if (sync == null) return;

        string name = itemName.ToString();
        if (!string.IsNullOrEmpty(name))
            sync.ServerUntrackItem(name);

        if (slot >= 0 && sync.Inventory != null)
            sync.Inventory.RemoveItem(slot);
    }
}
