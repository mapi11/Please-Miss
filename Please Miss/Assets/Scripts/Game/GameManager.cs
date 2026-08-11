using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public static bool LocalRunnerFinished { get; set; }

    /// <summary>Raised on clients that are the sniper when a runner is killed by the sniper.</summary>
    public event Action<string, Color32, float, string, int, int> OnSniperKillRecorded;

    /// <summary>Raised on clients that are runners when their end-of-game reward is granted.</summary>
    public event Action<int, string> OnRunnerRewardRecorded;

    /// <summary>Raised on a client when it receives a near-miss bonus for a bullet that passed close by.</summary>
    public event Action<int, string> OnNearMissRecorded;

    public void NotifyNearMissReward(int reward)
    {
        OnNearMissRecorded?.Invoke(reward, "Near miss");
    }

    public void NotifyHealReward(int reward)
    {
        if (reward <= 0) return;
        OnRunnerRewardRecorded?.Invoke(reward, "Heal");
    }

    public enum GameState : byte
    {
        Preparing,
        Playing,
        Ended
    }

    /// <summary>Варианты голосования после конца игры.</summary>
    public enum GameEndVoteOption : byte
    {
        BackToLobby,
        PlayAgain
    }

    [Header("Timers")]
    [SerializeField] private float prepareDuration = 10f;
    [SerializeField] private float gameDuration = 180f;

    public float GameDuration => gameDuration;

    /// <summary>Время, прошедшее с начала матча.</summary>
    public float ElapsedMatchTime => Mathf.Max(0f, GameDuration - GameTimeRemaining.Value);

    /// <summary>Очки игрока на момент старта игры (для подсчёта заработанных за матч).</summary>
    public int PointsAtGameStart { get; private set; }

    /// <summary>Очки, заработанные за текущую игру.</summary>
    public int TotalEarnedThisGame => LocalPlayerSettings.PlayerPoints - PointsAtGameStart;

    [Header("Rewards")]
    [Tooltip("Сколько очков снайпер получает за каждое убийство бегуна в конце игры")]
    [SerializeField] private int sniperKillReward = 50;
    [Tooltip("Сколько очков бегун получает за добегание до победной зоны")]
    [SerializeField] private int runnerFinishReward = 250;
    [Tooltip("Сколько очков бегун получает за смерть")]
    [SerializeField] private int runnerDeathReward = 100;
    [Tooltip("Бонус игроку за близко пролетевшую пулю (пересекла trigger, но не попала)")]
    [SerializeField] private int nearMissReward = 50;
    [Tooltip("Сколько очков получает игрок за вылеченного напарника")]
    [SerializeField] private int healReward = 50;

    public int NearMissReward => nearMissReward;
    public int HealReward => healReward;

    [Header("References")]
    [SerializeField] private GameObject[] startWalls;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject timerPanel;

    [Header("Game End Vote")]
    [Tooltip("Сцена лобби при выборе 'Back to Lobby'")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [Tooltip("Сцена игры при выборе 'Play again'")]
    [SerializeField] private string gameSceneName = "Game";
    [Tooltip("Таймер голосования при большинстве (>50%), в секундах")]
    [SerializeField] private float majorityTimerDuration = 5f;

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
    private readonly HashSet<ulong> rewardedRunners = new HashSet<ulong>();

    public readonly NetworkList<ulong> FinishedRunners = new NetworkList<ulong>();
    private ulong? sniperClientId;
    private PlayerHealth sniperHealth;
    private PlayerHealth localPlayerHealth;

    /// <summary>Клиенты, проголосовавшие за возврат в лобби (после конца игры).</summary>
    public readonly NetworkList<ulong> LobbyVotes = new NetworkList<ulong>();

    /// <summary>Клиенты, проголосовавшие за повторную игру.</summary>
    public readonly NetworkList<ulong> PlayAgainVotes = new NetworkList<ulong>();

    /// <summary>Обратный отсчёт при большинстве голосов (>= 0 — таймер активен).</summary>
    public readonly NetworkVariable<int> VoteTimerRemaining = new NetworkVariable<int>(-1);

    private readonly HashSet<ulong> votedClients = new HashSet<ulong>();
    private Coroutine majorityVoteCoroutine;
    private GameEndVoteOption? pendingVoteOption;

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
        LobbyVotes.Initialize(this);
        PlayAgainVotes.Initialize(this);

        localPlayerHealth = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();
        PointsAtGameStart = LocalPlayerSettings.PlayerPoints;

        if (!IsServer) return;

        State.Value = GameState.Preparing;
        prepareTimer = prepareDuration;
        PrepareTimeRemaining.Value = prepareDuration;
        gameTimer = gameDuration;
        GameTimeRemaining.Value = gameDuration;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkInventorySync.ClearAllTrackedServer();
        SpectatorManager.DespawnAllSpectators();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            TrackRunner(client.ClientId, client.PlayerObject);

            var health = client.PlayerObject?.GetComponent<PlayerHealth>();
            if (health != null)
                health.ServerRestoreFullHealth();
        }

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

        LobbyVotes.Remove(clientId);
        PlayAgainVotes.Remove(clientId);
        votedClients.Remove(clientId);
        ReEvaluateGameEndVotes();

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
            rewardedRunners.Remove(clientId);
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

        if (dead)
            GrantRunnerRewardImmediately(clientId, runnerDeathReward, "Death");

        if (!HasActiveRunners())
            SniperWins();
    }

    private void GrantRunnerRewardImmediately(ulong clientId, int reward, string reason)
    {
        if (reward <= 0) return;
        if (rewardedRunners.Contains(clientId)) return;

        rewardedRunners.Add(clientId);

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        RunnerRewardClientRpc(reward, new FixedString32Bytes(reason), rpcParams);
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
        GrantRunnerRewardImmediately(clientId, runnerFinishReward, "Finish");
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
            if (rewardedRunners.Contains(clientId)) continue;

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

    /// <summary>Отправляет голос на сервер (вызывается на клиенте).</summary>
    public void SubmitGameEndVote(GameEndVoteOption option)
    {
        if (!IsSpawned) return;
        SubmitGameEndVoteServerRpc(option);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitGameEndVoteServerRpc(GameEndVoteOption option, ServerRpcParams rpcParams = default)
    {
        if (State.Value != GameState.Ended) return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        // Голос можно менять: клик по другой кнопке меняет голос,
        // повторный клик по той же — отменяет его.
        if (option == GameEndVoteOption.BackToLobby)
        {
            if (LobbyVotes.Contains(clientId))
            {
                LobbyVotes.Remove(clientId);
            }
            else
            {
                if (PlayAgainVotes.Contains(clientId))
                    PlayAgainVotes.Remove(clientId);

                LobbyVotes.Add(clientId);
            }
        }
        else
        {
            if (PlayAgainVotes.Contains(clientId))
            {
                PlayAgainVotes.Remove(clientId);
            }
            else
            {
                if (LobbyVotes.Contains(clientId))
                    LobbyVotes.Remove(clientId);

                PlayAgainVotes.Add(clientId);
            }
        }

        votedClients.Add(clientId);
        ReEvaluateGameEndVotes();
    }

    /// <summary>
    /// Проверяет голоса: 100% за один вариант — сразу выполняем его;
    /// больше 50% — запускаем таймер, по истечении которого выполняется большинство.
    /// </summary>
    private void ReEvaluateGameEndVotes()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        int total = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (total <= 0) return;

        int lobby = LobbyVotes.Count;
        int again = PlayAgainVotes.Count;

        if (lobby == total)
        {
            ExecuteGameEndVote(GameEndVoteOption.BackToLobby);
            return;
        }

        if (again == total)
        {
            ExecuteGameEndVote(GameEndVoteOption.PlayAgain);
            return;
        }

        if (lobby * 2 > total)
            StartMajorityVoteTimer(GameEndVoteOption.BackToLobby);
        else if (again * 2 > total)
            StartMajorityVoteTimer(GameEndVoteOption.PlayAgain);
        else
            StopMajorityVoteTimer();
    }

    private void StartMajorityVoteTimer(GameEndVoteOption option)
    {
        if (majorityVoteCoroutine != null && pendingVoteOption == option)
            return;

        pendingVoteOption = option;

        if (majorityVoteCoroutine != null)
            StopCoroutine(majorityVoteCoroutine);

        majorityVoteCoroutine = StartCoroutine(MajorityVoteTimerRoutine(option));
    }

    private System.Collections.IEnumerator MajorityVoteTimerRoutine(GameEndVoteOption option)
    {
        float remaining = majorityTimerDuration;

        while (remaining > 0f)
        {
            VoteTimerRemaining.Value = Mathf.CeilToInt(remaining);
            remaining -= Time.deltaTime;
            yield return null;
        }

        VoteTimerRemaining.Value = -1;
        majorityVoteCoroutine = null;
        ExecuteGameEndVote(option);
    }

    private void StopMajorityVoteTimer()
    {
        if (majorityVoteCoroutine != null)
        {
            StopCoroutine(majorityVoteCoroutine);
            majorityVoteCoroutine = null;
        }

        pendingVoteOption = null;
        VoteTimerRemaining.Value = -1;
    }

    private void ExecuteGameEndVote(GameEndVoteOption option)
    {
        StopMajorityVoteTimer();

        string scene = option == GameEndVoteOption.BackToLobby ? lobbySceneName : gameSceneName;
        NetworkManager.Singleton.SceneManager.LoadScene(scene, LoadSceneMode.Single);
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
