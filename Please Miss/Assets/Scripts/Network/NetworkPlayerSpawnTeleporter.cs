using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawnTeleporter : NetworkBehaviour
{
    [SerializeField] private float teleportDelay = 0.15f;
    [SerializeField] private float postTeleportDelay = 0.5f;
    [SerializeField] private bool teleportOnlyOwner = true;

    private static readonly List<NetworkPlayerSpawnTeleporter> activeTeleporters =
        new List<NetworkPlayerSpawnTeleporter>();

    private CharacterController characterController;
    private PlayerController playerController;
    private Coroutine teleportCoroutine;
    private bool finishedSpawning;

    private readonly NetworkVariable<byte> spawnIndex =
        new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        activeTeleporters.Add(this);
        finishedSpawning = false;

        if (IsServer)
            AssignSpawnIndex();

        IgnorePlayerCollisions(true);

        SceneManager.sceneLoaded += OnSceneLoaded;
        ScheduleSpawn();
    }

    private void AssignSpawnIndex()
    {
        byte index = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            if (client.PlayerObject == NetworkObject)
                break;

            index++;
        }

        spawnIndex.Value = index;
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        activeTeleporters.Remove(this);
    }

    private void ScheduleSpawn()
    {
        if (!IsSpawned) return;

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

    private static readonly List<Collider> selfColliders = new List<Collider>(16);
    private static readonly List<Collider> otherColliders = new List<Collider>(16);

    private void IgnorePlayerCollisions(bool ignore)
    {
        selfColliders.Clear();
        GetComponentsInChildren<Collider>(true, selfColliders);
        if (selfColliders.Count == 0) return;

        foreach (var other in activeTeleporters)
        {
            if (other == null || other == this) continue;

            otherColliders.Clear();
            other.GetComponentsInChildren<Collider>(true, otherColliders);
            if (otherColliders.Count == 0) continue;

            foreach (var mine in selfColliders)
            {
                foreach (var theirs in otherColliders)
                {
                    if (mine == theirs) continue;
                    Physics.IgnoreCollision(mine, theirs, ignore);
                }
            }
        }
    }

    private static bool IsHandCollider(Collider c)
    {
        return c != null && c.GetComponentInParent<Hand>() != null;
    }

    private void EnableCollisionsWithSpawnedPlayers()
    {
        selfColliders.Clear();
        GetComponentsInChildren<Collider>(true, selfColliders);
        if (selfColliders.Count == 0) return;

        foreach (var other in activeTeleporters)
        {
            if (other == null || other == this) continue;
            if (!other.finishedSpawning) continue;

            otherColliders.Clear();
            other.GetComponentsInChildren<Collider>(true, otherColliders);
            if (otherColliders.Count == 0) continue;

            foreach (var mine in selfColliders)
            {
                foreach (var theirs in otherColliders)
                {
                    if (mine == theirs) continue;
                    if (IsHandCollider(mine) || IsHandCollider(theirs)) continue;

                    Physics.IgnoreCollision(mine, theirs, false);
                }
            }
        }
    }

    private IEnumerator SpawnRoutine()
    {
        if (characterController != null)
            characterController.enabled = false;

        yield return new WaitForSeconds(teleportDelay);

        if (!teleportOnlyOwner || IsOwner)
        {
            Transform target = FindTargetSpawn();

            if (target != null)
            {
                TeleportTo(target.position, target.rotation);
            }
        }

        yield return new WaitForSeconds(postTeleportDelay);

        bool inLobby = FindObjectOfType<LobbyManager>() != null;

        if (IsOwner && !inLobby && playerController != null)
            playerController.SetFrozen(false);

        if (characterController != null)
            characterController.enabled = true;

        finishedSpawning = true;
        EnableCollisionsWithSpawnedPlayers();

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

        // поочерёдный спавн: каждый игрок идёт на свою точку по индексу (0, 1, 2...)
        foreach (var sp in spawnPoints)
        {
            if (sp.Index == spawnIndex.Value)
                return sp.transform;
        }

        // если точек меньше, чем игроков — замыкаем по кругу
        if (spawnIndex.Value >= spawnPoints.Length)
            return spawnPoints[spawnIndex.Value % spawnPoints.Length].transform;

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
