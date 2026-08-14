using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Переприменяет сохранённые настройки графики (камеры, постобработка,
/// volume-эффекты) после загрузки каждой сцены, чтобы они не «слетали».</summary>
public class GraphicsSettingsApplier : MonoBehaviour
{
    public static GraphicsSettingsApplier Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(ApplyDelayed());
    }

    private System.Collections.IEnumerator ApplyDelayed()
    {
        yield return null;
        yield return null;

        GraphicsSettingsUI.ApplySavedSettingsAfterSceneLoad();
    }
}