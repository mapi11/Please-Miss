using UnityEngine;

public class FarSound : MonoBehaviour
{
    [Header("Audio Source")]
    [Tooltip("Отдельный Audio Source для дальних звуков (добавляется автоматически при отсутствии)")]
    [SerializeField] private AudioSource audioSource;

    [Header("Far Shot")]
    [Tooltip("Радиус, с которого начинается затухание")]
    [SerializeField] private float minDistance = 5f;
    [Tooltip("Максимальный радиус слышимости дальнего выстрела")]
    [SerializeField] private float maxDistance = 300f;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.1f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0f;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    public void PlayFarShot(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        if (pitchVariation > 0f)
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        audioSource.PlayOneShot(clip, volume);
    }
}
