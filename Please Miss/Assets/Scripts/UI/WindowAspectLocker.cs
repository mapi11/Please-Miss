using UnityEngine;

/// <summary>
/// Держит окно в пропорциях 16:9: устанавливает стартовое разрешение и
/// корректирует размер, когда игрок растягивает окно вручную.
/// Вешается на любой объект стартовой сцены.
/// </summary>
public class WindowAspectLocker : MonoBehaviour
{
    [Header("Startup Resolution")]
    [Tooltip("Установить стартовое разрешение при запуске игры")]
    [SerializeField] private bool setStartupResolution = true;
    [Tooltip("Ширина окна при запуске")]
    [SerializeField] private int startupWidth = 1920;
    [Tooltip("Высота окна при запуске")]
    [SerializeField] private int startupHeight = 1080;

    [Header("Aspect Lock")]
    [Tooltip("Целевое соотношение сторон (ширина : высота)")]
    [SerializeField] private Vector2 targetAspect = new Vector2(16f, 9f);
    [Tooltip("Допустимое отклонение соотношения перед коррекцией")]
    [SerializeField] private float tolerance = 0.02f;
    [Tooltip("Интервал проверки размера окна, в секундах")]
    [SerializeField] private float checkInterval = 0.3f;
    [Tooltip("Сохранять объект между сценами")]
    [SerializeField] private bool persistAcrossScenes = true;

    private float targetRatio;
    private float checkTimer;

    private void Awake()
    {
        targetRatio = targetAspect.x / Mathf.Max(0.0001f, targetAspect.y);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        if (setStartupResolution && !Screen.fullScreen)
        {
            int savedWidth = PlayerPrefs.GetInt("ScreenWidth", 0);
            int savedHeight = PlayerPrefs.GetInt("ScreenHeight", 0);

            if (savedWidth > 0 && savedHeight > 0)
            {
                Screen.SetResolution(savedWidth, savedHeight, FullScreenMode.Windowed);
                return;
            }

            Screen.SetResolution(startupWidth, startupHeight, FullScreenMode.Windowed);
            return;
        }

        ApplyAspect();
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;

        checkTimer = checkInterval;

        if (!Screen.fullScreen && Mathf.Abs(GetAspectRatio() - targetRatio) > tolerance)
            ApplyAspect();
    }

    private void ApplyAspect()
    {
        int height = Mathf.Max(1, Screen.height);
        int width = Mathf.Max(1, Mathf.RoundToInt(height * targetRatio));
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    private float GetAspectRatio()
    {
        int height = Mathf.Max(1, Screen.height);
        return (float)Screen.width / height;
    }
}
