using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public static bool LocalRunnerFinished { get; set; }

    /// <summary>Raised on clients that are the sniper when a runner is killed by the sniper.</summary>
    public event Action<string, Color32, float, string, int, int> OnSniperKillRecorded;

    /// <summary>Raised on clients that are runners when their end-of-game reward is granted.</summary>
    public event Action<int, string> OnRunnerRewardRecorded;

    public enum GameState : byte
    {
        Preparing,
        Playing,
        Ended
    }

    [Header("Timers")]
    [SerializeField] private float prepareDuration = 10f;
    [SerializeField] private float gameDuration = 180f;

    public float GameDuration => gameDuration;

    [Header("Rewards")]
    [Tooltip("Сколько очков снайпер получает за каждое убийство бегуна в конце игры")]
    [SerializeField] private int sniperKillReward = 50;
    [Tooltip("Сколько очков бегун получает за добегание до победной зоны")]
    [SerializeField] private int runnerFinishReward = 250;
    [Tooltip("Сколько очков бегун получает за смерть")]
    [SerializeField] private int runnerDeathReward = 100;

    [Header("References")]
    [SerializeField] private GameObject[] startWalls;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject timerPanel;

    public readonly NetworkVariable<GameState> State = new NetworkVariable<GameState>(
        GameState.Preparing,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<float> PrepareTimeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<float> GameTimeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<uint> LocationSeed = new NetworkVariable<uint>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float prepareTimer;
    private float gameTimer;
    private int sniperKills;
    private int sniperBonusPoints;
    private readonly Dictionary<ulong, PlayerHealth> trackedRunners = new Dictionary<ulong, PlayerHealth>();

    private readonly Dictionary<ulong, Action<bool>> runnerDeathCallbacks = new Dictionary<ulong, Action<bool>>();
    private readonly Dictionary<ulong, Action<DamageInfo, float, string>> runnerDamageCallbacks =
        new Dictionary<ulong, Action<DamageInfo, float, string>>();
    private readonly Dictionary<ulong, DamageInfo> lastRunnerDamage = new Dictionary<ulong, DamageInfo>();
    private readonly Dictionary<ulong, string> lastRunnerZone = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, int> lastRunnerBonusPoints = new Dictionary<ulong, int>();

    public readonly NetworkList<ulong> FinishedRunners = new NetworkList<ulong>();
    private ulong? sniperClientId;
    private PlayerHealth sniperHealth;
    private PlayerHealth localPlayerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LocalRunnerFinished = false;
    }

    public override void OnNetworkSpawn()
    {
        FinishedRunners.Initialize(this);

        localPlayerHealth = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();

        if (!IsServer) return;

        State.Value = GameState.Preparing;
        prepareTimer = prepareDuration;
        PrepareTimeRemaining.Value = prepareDuration;
        gameTimer = gameDuration;
        GameTimeRemaining.Value = gameDuration;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkInventorySync.ClearAllTrackedServer();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            TrackRunner(client.ClientId, client.PlayerObject);

        ConfigureAllPlayers();
        TrackSniper();
    }

    public override void OnNetworkDespawn()
    {
        UntrackSniper();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            TrackRunner(clientId, client.PlayerObject);
            ConfigurePlayer(clientId, client.PlayerObject);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UntrackRunner(clientId);

        if (sniperClientId.HasValue && sniperClientId.Value == clientId)
        {
            UntrackSniper();
            if (State.Value == GameState.Playing)
                RunnerWins();
            return;
        }

        if (State.Value != GameState.Playing) return;

        if (!HasActiveRunners())
            SniperWins();
    }

    private void TrackRunner(ulong clientId, NetworkObject playerObj)
    {
        if (playerObj == null) return;

        var role = playerObj.GetComponent<NetworkPlayerRole>();
        if (role == null || !role.IsRunner) return;

        var health = playerObj.GetComponent<PlayerHealth>();
        if (health == null) return;

        trackedRunners[clientId] = health;

        Action<bool> deathCallback = dead => OnRunnerDeathStateChanged(clientId, dead);
        Action<DamageInfo, float, string> damageCallback = (info, finalDamage, zoneName) =>
            OnRunnerDamageApplied(clientId, info, zoneName);

        runnerDeathCallbacks[clientId] = deathCallback;
        runnerDamageCallbacks[clientId] = damageCallback;

        health.OnDeathStateChanged += deathCallback;
        health.OnDamageAppliedOnServer += damageCallback;
    }

    private void UntrackRunner(ulong clientId)
    {
        if (trackedRunners.TryGetValue(clientId, out var health))
        {
            if (runnerDeathCallbacks.TryGetValue(clientId, out var deathCallback))
                health.OnDeathStateChanged -= deathCallback;

            if (runnerDamageCallbacks.TryGetValue(clientId, out var damageCallback))
                health.OnDamageAppliedOnServer -= damageCallback;

            runnerDeathCallbacks.Remove(clientId);
            runnerDamageCallbacks.Remove(clientId);
            lastRunnerDamage.Remove(clientId);
            lastRunnerZone.Remove(clientId);
            lastRunnerBonusPoints.Remove(clientId);
            trackedRunners.Remove(clientId);
        }
    }

    private void TrackSniper()
    {
        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var role = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (role == null || !role.IsSniper) continue;

            sniperClientId = client.ClientId;
            sniperHealth = client.PlayerObject.GetComponent<PlayerHealth>();
            if (sniperHealth != null)
                sniperHealth.OnDeathStateChanged += OnSniperDeathStateChanged;
            return;
        }
    }

    private void UntrackSniper()
    {
        if (sniperHealth != null)
        {
            sniperHealth.OnDeathStateChanged -= OnSniperDeathStateChanged;
            sniperHealth = null;
        }
        sniperClientId = null;
    }

    private void OnSniperDeathStateChanged(bool dead)
    {
        if (!dead || State.Value != GameState.Playing) return;

        RunnerWins();
    }

    private void OnRunnerDamageApplied(ulong clientId, DamageInfo damageInfo, string zoneName)
    {
        lastRunnerDamage[clientId] = damageInfo;
        lastRunnerZone[clientId] = zoneName;

        int bonus = 0;

        if (trackedRunners.TryGetValue(clientId, out var health) && health != null &&
            damageInfo.HitCollider != null)
        {
            PlayerHitZone zone = health.FindHitZone(damageInfo.HitCollider);
            if (zone != null)
                bonus = zone.KillPoints;
        }

        lastRunnerBonusPoints[clientId] = bonus;
    }

    private void OnRunnerDeathStateChanged(ulong clientId, bool dead)
    {
        if (dead)
            NotifySniperKill(clientId);

        if (State.Value != GameState.Playing) return;

        if (!HasActiveRunners())
            SniperWins();
    }

    private void NotifySniperKill(ulong runnerClientId)
    {
        if (State.Value != GameState.Playing) return;
        if (!sniperClientId.HasValue) return;

        if (!lastRunnerDamage.TryGetValue(runnerClientId, out var damageInfo)) return;
        if (damageInfo.AttackerClientId != sniperClientId.Value) return;

        sniperKills++;

        int bonus = lastRunnerBonusPoints.TryGetValue(runnerClientId, out var storedBonus)
            ? storedBonus
            : 0;
        sniperBonusPoints += bonus;

        string playerName = "Player";
        Color32 color = Color.white;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(runnerClientId, out var runnerClient) &&
            runnerClient.PlayerObject != null)
        {
            var nameComponent = runnerClient.PlayerObject.GetComponent<NetworkPlayerName>();
            if (nameComponent != null)
                playerName = nameComponent.CurrentName;

            var colorComponent = runnerClient.PlayerObject.GetComponent<NetworkPlayerColor>();
            if (colorComponent != null)
                color = colorComponent.CurrentColor;
        }

        string zoneName = lastRunnerZone.TryGetValue(runnerClientId, out var storedZone)
            ? storedZone
            : "None";

        float survivedTime = Mathf.Max(0f, GameDuration - GameTimeRemaining.Value);

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sniperClientId.Value }
            }
        };

        SniperKillRecordedClientRpc(
            new FixedString64Bytes(playerName),
            LocalPlayerSettings.PackColor(color),
            survivedTime,
            new FixedString32Bytes(zoneName),
            sniperKillReward,
            bonus,
            rpcParams
        );
    }

    [ClientRpc]
    private void SniperKillRecordedClientRpc(
        FixedString64Bytes playerName,
        int packedColor,
        float survivedTime,
        FixedString32Bytes zoneName,
        int mainPoints,
        int bonusPoints,
        ClientRpcParams rpcParams = default)
    {
        OnSniperKillRecorded?.Invoke(
            playerName.ToString(),
            LocalPlayerSettings.UnpackColor(packedColor),
            survivedTime,
            zoneName.ToString(),
            mainPoints,
            bonusPoints
        );
    }

    private void Update()
    {
        if (IsServer)
        {
            switch (State.Value)
            {
                case GameState.Preparing:
                    TickPrepare();
                    break;
                case GameState.Playing:
                    TickPlaying();
                    break;
            }
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        bool timerRunning = State.Value == GameState.Preparing || State.Value == GameState.Playing;
        bool localDead = localPlayerHealth != null && localPlayerHealth.IsDead;
        bool hide = !timerRunning ||
                    (State.Value == GameState.Playing && (LocalRunnerFinished || localDead));

        if (timerPanel != null)
            timerPanel.SetActive(!hide);

        if (timerText == null)
            return;

        if (hide)
        {
            timerText.text = "";
            return;
        }

        switch (State.Value)
        {
            case GameState.Preparing:
                timerText.text = "Prepare: " + FormatTime(PrepareTimeRemaining.Value);
                break;
            case GameState.Playing:
                timerText.text = "Time: " + FormatTime(GameTimeRemaining.Value);
                break;
        }
    }

    private void TickPrepare()
    {
        prepareTimer -= Time.deltaTime;
        PrepareTimeRemaining.Value = Mathf.Max(0f, prepareTimer);

        if (prepareTimer <= 0f)
        {
            State.Value = GameState.Playing;
            PrepareTimeRemaining.Value = 0f;
            gameTimer = gameDuration;
            GameTimeRemaining.Value = gameDuration;

            DisableStartWallsClientRpc();
        }
    }

    private void TickPlaying()
    {
        gameTimer -= Time.deltaTime;
        GameTimeRemaining.Value = Mathf.Max(0f, gameTimer);

        if (gameTimer <= 0f)
        {
            GameTimeRemaining.Value = 0f;
            RunnerWins();
        }
    }

    public void OnRunnerReachedFinish(ulong clientId)
    {
        if (State.Value != GameState.Playing) return;
        if (FinishedRunners.Contains(clientId)) return;

        FinishedRunners.Add(clientId);
        NotifyRunnerFinishedClientRpc(clientId);

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            client.PlayerObject.transform.position = new Vector3(0f, -9999f, 0f);
            HideFinishedRunnerClientRpc(client.PlayerObject);
        }

        if (!HasActiveRunners())
        {
            if (AllRunnersFinished())
                RunnerWins();
            else
                SniperWins();
        }
    }

    private bool AllRunnersFinished()
    {
        foreach (var kvp in trackedRunners)
        {
            if (!FinishedRunners.Contains(kvp.Key))
                return false;
        }
        return trackedRunners.Count > 0;
    }

    public bool HasActiveRunners()
    {
        foreach (var kvp in trackedRunners)
        {
            if (FinishedRunners.Contains(kvp.Key)) continue;
            if (kvp.Value != null && !kvp.Value.IsDead)
                return true;
        }
        return false;
    }

    public int CountAliveRunners()
    {
        if (NetworkManager.Singleton == null) return 0;

        int count = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var role = playerObj.GetComponent<NetworkPlayerRole>();
            if (role == null || !role.IsRunner) continue;

            if (FinishedRunners.Contains(client.ClientId)) continue;

            var health = playerObj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) continue;

            count++;
        }
        return count;
    }

    private void SniperWins()
    {
        State.Value = GameState.Ended;
        KillAllRunners();
        SniperWinsClientRpc();
        SendSniperReward();
        SendRunnerRewards();
    }

    private void RunnerWins()
    {
        State.Value = GameState.Ended;
        RunnerWinsClientRpc();
        SendSniperReward();
        SendRunnerRewards();
    }

    private void SendRunnerRewards()
    {
        if (NetworkManager.Singleton == null) return;

        foreach (var kvp in trackedRunners)
        {
            ulong clientId = kvp.Key;
            int reward = 0;
            string reason = "";

            if (FinishedRunners.Contains(clientId))
            {
                reward = runnerFinishReward;
                reason = "Finish";
            }
            else if (kvp.Value != null && kvp.Value.IsDead)
            {
                reward = runnerDeathReward;
                reason = "Death";
            }

            if (reward <= 0) continue;

            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            RunnerRewardClientRpc(reward, new FixedString32Bytes(reason), rpcParams);
        }
    }

    [ClientRpc]
    private void RunnerRewardClientRpc(int reward, FixedString32Bytes reason, ClientRpcParams rpcParams = default)
    {
        if (reward <= 0) return;

        LocalPlayerSettings.AddPoints(reward);
        OnRunnerRewardRecorded?.Invoke(reward, reason.ToString());
    }

    private void SendSniperReward()
    {
        if (!sniperClientId.HasValue || (sniperKills <= 0 && sniperBonusPoints <= 0)) return;

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sniperClientId.Value }
            }
        };

        SniperRewardClientRpc(sniperKills, sniperKillReward, sniperBonusPoints, rpcParams);
    }

    [ClientRpc]
    private void SniperRewardClientRpc(int totalKills, int rewardPerKill, int totalBonus, ClientRpcParams rpcParams = default)
    {
        if (totalKills <= 0 && totalBonus <= 0) return;
        if (rewardPerKill < 0) return;

        LocalPlayerSettings.AddPoints(totalKills * rewardPerKill + totalBonus);
    }

    private void ConfigureAllPlayers()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            ConfigurePlayer(client.ClientId, client.PlayerObject);
    }

    private void ConfigurePlayer(ulong clientId, NetworkObject player)
    {
        if (player == null) return;

        var role = player.GetComponent<NetworkPlayerRole>();
        if (role == null) return;

        if (role.IsSniper)
        {
            var stamina = player.GetComponent<Stamina>();
            if (stamina != null) stamina.enabled = false;
        }
        else
        {
            var weapon = player.GetComponent<SniperWeaponController>();
            if (weapon != null) weapon.enabled = false;
        }

    }

    private void KillAllRunners()
    {
        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var health = client.PlayerObject.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead) continue;

            var role = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (role != null && role.IsRunner)
                health.ServerSetHealth(0f);
        }
    }

    [ClientRpc]
    private void NotifyRunnerFinishedClientRpc(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
            LocalRunnerFinished = true;
    }

    [ClientRpc]
    private void HideFinishedRunnerClientRpc(NetworkObjectReference runnerRef)
    {
        if (!runnerRef.TryGet(out NetworkObject runner)) return;

        runner.transform.SetPositionAndRotation(new Vector3(0f, -9999f, 0f), Quaternion.identity);
        Physics.SyncTransforms();

        foreach (var renderer in runner.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        var cc = runner.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;
    }

    [ClientRpc]
    private void SniperWinsClientRpc()
    {
        Debug.Log("Sniper wins!");
    }

    [ClientRpc]
    private void RunnerWinsClientRpc()
    {
        Debug.Log("Runners win!");
    }

    [ClientRpc]
    private void DisableStartWallsClientRpc()
    {
        if (startWalls == null) return;
        foreach (var wall in startWalls)
            if (wall != null)
                wall.SetActive(false);
    }

    private static string FormatTime(float seconds)
    {
        int totalSec = Mathf.CeilToInt(seconds);
        int mins = totalSec / 60;
        int secs = totalSec % 60;
        return $"{mins}:{secs:D2}";
    }
}
