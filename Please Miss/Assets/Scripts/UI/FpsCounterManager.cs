using UnityEngine;

public class FpsCounterManager : MonoBehaviour
{
    public static FpsCounterManager Instance { get; private set; }

    private const string PrefsKey = "ShowFps";

    private GameObject counterObject;
    private GameObject counterPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null)
            return;

        var go = new GameObject("FpsCounterManager");
        go.AddComponent<FpsCounterManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerPrefs.GetInt(PrefsKey, 0) == 1)
            ShowCounter(null);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool IsEnabled()
    {
        return PlayerPrefs.GetInt(PrefsKey, 0) == 1;
    }

    public static void SetEnabled(bool enabled, GameObject prefab)
    {
        PlayerPrefs.SetInt(PrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (Instance == null)
        {
            var go = new GameObject("FpsCounterManager");
            go.AddComponent<FpsCounterManager>();
        }

        if (enabled)
            Instance.ShowCounter(prefab);
        else
            Instance.HideCounter();
    }

    private void ShowCounter(GameObject prefab)
    {
        if (counterObject != null)
            return;

        if (prefab != null)
            counterPrefab = prefab;

        if (counterPrefab == null)
            return;

        counterObject = Instantiate(counterPrefab);
        counterObject.name = "FpsCounter";
        DontDestroyOnLoad(counterObject);
    }

    private void HideCounter()
    {
        if (counterObject == null)
            return;

        Destroy(counterObject);
        counterObject = null;
    }
}
