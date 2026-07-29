using Unity.Netcode;
using UnityEngine;

public class WinZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        var role = networkObject.GetComponent<NetworkPlayerRole>();
        if (role == null || !role.IsRunner) return;

        var health = networkObject.GetComponent<PlayerHealth>();
        if (health == null || health.IsDead) return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnRunnerReachedFinish(networkObject.OwnerClientId);
    }
}
