using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Секция голосования после конца игры, встроенная в End panel (бегуна или снайпера).
/// Показывается ТОЛЬКО когда игра завершена (GameState.Ended), а не при смерти отдельного
/// игрока. Кнопки: "Back to Lobby" и "Play again". После нажатия появляются карточки
/// проголосовавших (имя + цвет). Выбор можно менять: клик по другой кнопке меняет голос,
/// повторный клик по той же — отменяет его.
/// Сервер выполняет вариант, за который проголосовали все (100%), либо запускает таймер
/// при большинстве (>50%).
///
/// Все ссылки назначаются в inspector. Скрипт размещается на End panel. Кнопки
/// голосования включаются/выключаются при конце игры.
/// </summary>
public class GameEndVoteUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private Button playAgainButton;

    [Header("Voters")]
    [Tooltip("Контейнер карточек проголосовавших за Back to Lobby")]
    [SerializeField] private RectTransform lobbyVotersContainer;
    [Tooltip("Контейнер карточек проголосовавших за Play again")]
    [SerializeField] private RectTransform playAgainVotersContainer;
    [Tooltip("Префаб карточки игрока (имя + цвет)")]
    [SerializeField] private GameObject voterCardPrefab;

    [Header("Timer")]
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private TMP_Text timerText;

    [Header("Temporary")]
    [Tooltip("Временно отключает кнопку Play Again (галочка в inspector)")]
    [SerializeField] private bool disablePlayAgainButton;

    public Button BackToLobbyButton => backToLobbyButton;
    public Button PlayAgainButton => playAgainButton;

    private int lastLobbyCount = -1;
    private int lastPlayAgainCount = -1;

    private void Awake()
    {
        BindButtons();
    }

    private void Update()
    {
        if (GameManager.Instance == null || NetworkManager.Singleton == null)
            return;

        bool show = GameManager.Instance.State.Value == GameManager.GameState.Ended
                    && GameManager.Instance.IsSpawned;

        if (backToLobbyButton != null)
            backToLobbyButton.gameObject.SetActive(show);

        if (playAgainButton != null)
            playAgainButton.gameObject.SetActive(show && !disablePlayAgainButton);

        if (!show)
        {
            if (timerPanel != null)
                timerPanel.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Refresh();
    }

    private void Vote(GameManager.GameEndVoteOption option)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.SubmitGameEndVote(option);
    }

    private void BindButtons()
    {
        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(() => Vote(GameManager.GameEndVoteOption.BackToLobby));
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveAllListeners();
            playAgainButton.onClick.AddListener(() => Vote(GameManager.GameEndVoteOption.PlayAgain));
        }
    }

    private void SetButtonsActive(bool active)
    {
        if (backToLobbyButton != null)
            backToLobbyButton.gameObject.SetActive(active);

        if (playAgainButton != null)
            playAgainButton.gameObject.SetActive(active);
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.LobbyVotes.Count != lastLobbyCount)
        {
            lastLobbyCount = gm.LobbyVotes.Count;
            RebuildVoters(lobbyVotersContainer, gm.LobbyVotes);
        }

        if (gm.PlayAgainVotes.Count != lastPlayAgainCount)
        {
            lastPlayAgainCount = gm.PlayAgainVotes.Count;
            RebuildVoters(playAgainVotersContainer, gm.PlayAgainVotes);
        }

        UpdateTimer(gm.VoteTimerRemaining.Value);
    }

    private void RebuildVoters(RectTransform container, NetworkList<ulong> votes)
    {
        if (container == null || voterCardPrefab == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        foreach (ulong clientId in votes)
        {
            GameObject cardObject = Instantiate(voterCardPrefab, container, false);

            var card = cardObject.GetComponent<VotePlayerCard>();
            if (card != null && TryGetPlayerInfo(clientId, out string playerName, out Color32 color))
                card.Setup(playerName, color);
        }
    }

    private static bool TryGetPlayerInfo(ulong clientId, out string playerName, out Color32 color)
    {
        playerName = $"Player {clientId}";
        color = Color.white;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) ||
            client.PlayerObject == null)
            return false;

        var nameComponent = client.PlayerObject.GetComponent<NetworkPlayerName>();
        if (nameComponent != null)
            playerName = nameComponent.CurrentName;

        var colorComponent = client.PlayerObject.GetComponent<NetworkPlayerColor>();
        if (colorComponent != null)
            color = colorComponent.CurrentColor;

        return true;
    }

    private void UpdateTimer(int seconds)
    {
        if (timerPanel != null)
            timerPanel.SetActive(seconds >= 0);

        if (timerText != null && seconds >= 0)
            timerText.text = seconds.ToString();
    }
}
