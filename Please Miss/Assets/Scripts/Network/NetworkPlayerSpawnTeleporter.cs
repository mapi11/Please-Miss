using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawnTeleporter : NetworkBehaviour
{
    [SerializeField] private float teleportDelay = 0.15f;
    [SerializeField] private float postTeleportDelay = 0.5f;
    [SerializeField] private bool teleportOnlyOwner = true;

    private CharacterController characterController;
    private Coroutine teleportCoroutine;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScheduleSpawn();
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void ScheduleSpawn()
    {
        if (!IsSpawned) return;
        if (teleportOnlyOwner && !IsOwner) return;

        var all = GetComponents<NetworkPlayerSpawnTeleporter>();
        if (all.Length > 1 && all[0] != this) return;

        if (teleportCoroutine != null)
            StopCoroutine(teleportCoroutine);
        teleportCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleSpawn();
    }

    private IEnumerator SpawnRoutine()
    {
        if (characterController != null)
            characterController.enabled = false;

        yield return new WaitForSeconds(teleportDelay);

        Transform target = FindTargetSpawn();

        if (target != null)
        {
            TeleportTo(target.position, target.rotation);
        }

        yield return new WaitForSeconds(postTeleportDelay);

        if (characterController != null)
            characterController.enabled = true;

        DismissConnectionScreen();
    }

    private void DismissConnectionScreen()
    {
        var screen = FindObjectOfType<ConnectionScreenManager>();
        if (screen != null)
            screen.Dismiss();
    }

    private Transform FindTargetSpawn()
    {
        PlayerRole role = PlayerRole.None;
        var roleComponent = GetComponent<NetworkPlayerRole>();
        if (roleComponent != null)
            role = roleComponent.CurrentRole;

        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        foreach (var sp in spawnPoints)
        {
            if (sp.Role == role)
                return sp.transform;
        }

        foreach (var sp in spawnPoints)
        {
            if (sp.Role == PlayerRole.None)
                return sp.transform;
        }

        return spawnPoints[0].transform;
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }
}
