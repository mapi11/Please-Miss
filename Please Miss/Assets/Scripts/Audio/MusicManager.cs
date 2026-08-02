using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music Lists")]
    [Tooltip("Музыка для Main Menu и Lobby (случайный трек, loop)")]
    [SerializeField] private AudioClip[] menuMusicClips;
    [Tooltip("Музыка для Game (случайный трек, loop)")]
    [SerializeField] private AudioClip[] gameMusicClips;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "Game";

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField] private float fadeDuration = 1.5f;

    private AudioSource audioSource;
    private Coroutine transitionCoroutine;
    private bool isPlayingMenuMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string name = scene.name;
        bool menuScene = name == mainMenuSceneName || name == lobbySceneName;

        if (menuScene)
        {
            if (isPlayingMenuMusic)
                return;

            PlayMusic(menuMusicClips, true);
        }
        else if (name == gameSceneName)
        {
            PlayMusic(gameMusicClips, false);
        }
        else
        {
            PlayMusic(null, false);
        }
    }

    private void PlayMusic(AudioClip[] clips, bool menu)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        isPlayingMenuMusic = menu;

        AudioClip clip = null;

        if (clips != null && clips.Length > 0)
            clip = clips[Random.Range(0, clips.Length)];

        transitionCoroutine = StartCoroutine(FadeTransition(clip));
    }

    private IEnumerator FadeTransition(AudioClip clip)
    {
        float remaining = fadeDuration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            audioSource.volume = volume * Mathf.Clamp01(remaining / fadeDuration);
            yield return null;
        }

        audioSource.Stop();

        if (clip == null)
        {
            audioSource.volume = 0f;
            yield break;
        }

        audioSource.clip = clip;
        audioSource.Play();

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = volume * Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        audioSource.volume = volume;
    }
}
