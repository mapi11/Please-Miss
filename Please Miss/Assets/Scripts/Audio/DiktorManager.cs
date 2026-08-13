using UnityEngine;

public class DiktorManager : MonoBehaviour
{
    public static DiktorManager Instance { get; private set; }

    [Header("Sound Clips")]
    [Tooltip("Звуки при входе в лобби игроков 1-4 (индекс = порядковый номер игрока)")]
    [SerializeField] private AudioClip[] joinClips;
    [SerializeField] private AudioClip readyClip;
    [SerializeField] private AudioClip beginClip;
    [SerializeField] private AudioSource audioSource;

    public bool Enabled { get; private set; } = true;
    public float Volume { get; private set; } = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Enabled = PlayerPrefs.GetInt("DiktorEnabled", 1) == 1;
        Volume = PlayerPrefs.GetFloat("DiktorVolume", 0.5f);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        ApplyVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool IsDiktorEnabled()
    {
        if (Instance != null)
            return Instance.Enabled;

        return PlayerPrefs.GetInt("DiktorEnabled", 1) == 1;
    }

    public static float GetDiktorVolume()
    {
        if (Instance != null)
            return Instance.Volume;

        return PlayerPrefs.GetFloat("DiktorVolume", 0.5f);
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        PlayerPrefs.SetInt("DiktorEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    public void SetVolume(float volume)
    {
        Volume = volume;
        PlayerPrefs.SetFloat("DiktorVolume", volume);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (audioSource != null)
            audioSource.volume = Enabled ? Volume : 0f;
    }

    public void PlayJoinSound(int index)
    {
        if (joinClips == null || index < 0 || index >= joinClips.Length)
            return;

        PlayLocal(joinClips[index]);
    }

    public void PlayReadySound()
    {
        PlayLocal(readyClip);
    }

    public void PlayBeginSound()
    {
        PlayLocal(beginClip);
    }

    private void PlayLocal(AudioClip clip)
    {
        if (audioSource == null || clip == null || !Enabled)
            return;

        audioSource.PlayOneShot(clip);
    }
}