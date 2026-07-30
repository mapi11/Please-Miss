using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public static bool LocalRunnerFinished { get; set; }

    public enum GameState : byte
    {
        Preparing,
        Playing,
        Ended
    }

    [Header("Timers")]
    [SerializeField] private float prepareDuration = 10f;
    [SerializeField] private float gameDuration = 180f;

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

    private float prepareTimer;
    private float gameTimer;
    private readonly Dictionary<ulong, PlayerHealth> trackedRunners = new Dictionary<ulong, PlayerHealth>();
    private readonly HashSet<ulong> finishedRunners = new HashSet<ulong>();
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
        localPlayerHealth = NetworkManager.Singleton.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();

        if (!IsServer) return;

        State.Value = GameState.Preparing;
        prepareTimer = prepareDuration;
        PrepareTimeRemaining.Value = prepareDuration;
        gameTimer = gameDuration;
        GameTimeRemaining.Value = gameDuration;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

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
        health.OnDeathStateChanged += OnRunnerDeathStateChanged;
    }

    private void UntrackRunner(ulong clientId)
    {
        if (trackedRunners.TryGetValue(clientId, out var health))
        {
            health.OnDeathStateChanged -= OnRunnerDeathStateChanged;
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

    private void OnRunnerDeathStateChanged(bool dead)
    {
        if (State.Value != GameState.Playing) return;

        if (!HasActiveRunners())
            SniperWins();
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
        if (timerText == null) return;

        bool localDead = localPlayerHealth != null && localPlayerHealth.IsDead;
        bool hide = State.Value == GameState.Ended ||
                    (State.Value == GameState.Playing && (LocalRunnerFinished || localDead));

        if (timerPanel != null)
            timerPanel.SetActive(!hide);

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

        finishedRunners.Add(clientId);
        NotifyRunnerFinishedClientRpc(clientId);

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
            if (!finishedRunners.Contains(kvp.Key))
                return false;
        }
        return trackedRunners.Count > 0;
    }

    private bool HasActiveRunners()
    {
        foreach (var kvp in trackedRunners)
        {
            if (finishedRunners.Contains(kvp.Key)) continue;
            if (kvp.Value != null && !kvp.Value.IsDead)
                return true;
        }
        return false;
    }

    private void SniperWins()
    {
        State.Value = GameState.Ended;
        KillAllRunners();
        SniperWinsClientRpc();
    }

    private void RunnerWins()
    {
        State.Value = GameState.Ended;
        RunnerWinsClientRpc();
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
