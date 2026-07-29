using Unity.Netcode;
using UnityEngine;

public class HideOnDeath : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject[] targets;

    private void Update()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<PlayerHealth>();

            if (playerHealth == null && NetworkManager.Singleton != null)
            {
                var localClient = NetworkManager.Singleton.LocalClient;
                if (localClient?.PlayerObject != null)
                    playerHealth = localClient.PlayerObject.GetComponent<PlayerHealth>();
            }

            return;
        }

        if (!playerHealth.IsDead)
            return;

        foreach (var obj in targets)
            if (obj != null)
                obj.SetActive(false);
    }
}
