using UnityEngine;

public class PlayerUIConfigurator : MonoBehaviour
{
    [SerializeField] private GameObject[] sniperElements;
    [SerializeField] private GameObject[] runnerElements;
    [SerializeField] private GameObject[] commonElements;
    [SerializeField] private GameObject[] hideOnDeathElements;
    [SerializeField] private GameObject[] spectatorElements;

    private PlayerHealth playerHealth;
    private PlayerRoleState roleState;

    private void Awake()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
        roleState = GetComponentInParent<PlayerRoleState>();
    }

    private void Update()
    {
        if (playerHealth == null || !playerHealth.IsSpawned)
            return;

        bool isOwner = playerHealth.IsOwner;

        if (!isOwner)
        {
            SetElementsActive(sniperElements, false);
            SetElementsActive(runnerElements, false);
            SetElementsActive(commonElements, false);
            SetElementsActive(hideOnDeathElements, false);
            SetElementsActive(spectatorElements, false);
            return;
        }

        bool dead = playerHealth.IsDead;

        PlayerRole role = roleState != null ? roleState.CurrentRole : PlayerRole.None;
        bool isSniper = role == PlayerRole.Sniper;
        bool isRunner = role == PlayerRole.Runner;

        SetElementsActive(sniperElements, !dead && isSniper);
        SetElementsActive(runnerElements, !dead && isRunner);
        SetElementsActive(commonElements, !dead);
        SetElementsActive(hideOnDeathElements, !dead);
        SetElementsActive(spectatorElements, dead);
    }

    private static void SetElementsActive(GameObject[] elements, bool active)
    {
        if (elements == null) return;
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] != null && elements[i].activeSelf != active)
                elements[i].SetActive(active);
        }
    }
}
