using Unity.Netcode;
using UnityEngine;

public class SpectatorManager : NetworkBehaviour
{
    [SerializeField] private GameObject spectatorCameraPrefab;

    [Header("Player Camera to disable")]
    [SerializeField] private Camera playerCamera;

    public void EnterSpectatorMode()
    {
        if (playerCamera != null)
            playerCamera.enabled = false;

        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        if (IsServer)
            SpawnSpectator(transform.position);
        else
            RequestSpawnSpectatorServerRpc(transform.position);
    }

    public void ExitSpectatorMode()
    {
        if (playerCamera != null)
            playerCamera.enabled = true;

        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnSpectatorServerRpc(Vector3 position, ServerRpcParams rpcParams = default)
    {
        SpawnSpectator(position, rpcParams.Receive.SenderClientId);
    }

    private void SpawnSpectator(Vector3 position, ulong ownerClientId = ulong.MaxValue)
    {
        if (spectatorCameraPrefab == null)
        {
            Debug.LogError("[SpectatorManager] spectatorCameraPrefab is NULL!");
            return;
        }

        if (ownerClientId == ulong.MaxValue)
            ownerClientId = OwnerClientId;

        var instance = Instantiate(spectatorCameraPrefab, position, Quaternion.identity);
        var netObj = instance.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(ownerClientId);
    }

    /// <summary>Удаляет всех сетевых спектаторов (сервер). Нужно при перезапуске матча/возврате в лобби.</summary>
    public static void DespawnAllSpectators()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        foreach (var spectator in FindObjectsByType<SpectatorController>(FindObjectsSortMode.None))
        {
            if (spectator != null && spectator.IsSpawned)
                spectator.NetworkObject.Despawn();
        }
    }
}
