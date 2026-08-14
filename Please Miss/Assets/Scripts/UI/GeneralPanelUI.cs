using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GeneralPanelUI : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Display")]
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Quality")]
    [SerializeField] private TMP_Dropdown presetDropdown;

    [Header("Volumes")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeValueText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;
    [SerializeField] private Slider voiceChatVolumeSlider;
    [SerializeField] private TextMeshProUGUI voiceChatVolumeValueText;

    [Header("Diktor")]
    [SerializeField] private Toggle diktorToggle;
    [SerializeField] private Slider diktorVolumeSlider;
    [SerializeField] private TextMeshProUGUI diktorVolumeValueText;

    private List<Resolution> resolutions;

    private void Awake()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        InitLanguageDropdown();
        InitDisplayMode();
        InitPreset();
    }

    private string Loc(string key)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Table", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    private void InitLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.onValueChanged.RemoveAllListeners();
        languageDropdown.ClearOptions();

        var locales = LocalizationSettings.AvailableLocales.Locales;
        var options = new List<string>();

        for (int i = 0; i < locales.Count; i++)
            options.Add(locales[i].Identifier.CultureInfo?.NativeName ?? locales[i].LocaleName);

        languageDropdown.AddOptions(options);

        int selected = locales.IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.SetValueWithoutNotify(selected >= 0 ? selected : 0);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(int index)
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;

        if (index >= 0 && index < locales.Count)
        {
            LocalizationSettings.SelectedLocale = locales[index];
            PlayerPrefs.SetString("Locale", locales[index].Identifier.Code);
            PlayerPrefs.Save();
        }
    }

    private void Start()
    {
        InitLanguageDropdown();
        InitDisplayMode();
        InitResolution();
        InitPreset();
        InitVolumes();
        InitDiktor();
    }

    private void InitDisplayMode()
    {
        if (displayModeDropdown == null)
            return;

        displayModeDropdown.onValueChanged.RemoveAllListeners();
        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string> { Loc("FullScreen"), Loc("Borderless"), Loc("Windowed") });
        displayModeDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("DisplayMode", 1), 0, 2));
        displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);
    }

    private void OnDisplayModeChanged(int index)
    {
        Screen.fullScreenMode = index switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };

        PlayerPrefs.SetInt("DisplayMode", index);
        PlayerPrefs.Save();
    }

    private void InitResolution()
    {
        if (resolutionDropdown == null)
            return;

        resolutions = GetUniqueResolutions();

        resolutionDropdown.ClearOptions();

        int currentIndex = 0;
        var options = new List<string>();

        for (int i = 0; i < resolutions.Count; i++)
        {
            options.Add(resolutions[i].width + "x" + resolutions[i].height);

            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                currentIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(currentIndex);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        if (resolutions == null || index < 0 || index >= resolutions.Count)
            return;

        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ScreenWidth", res.width);
        PlayerPrefs.SetInt("ScreenHeight", res.height);
        PlayerPrefs.Save();
    }

    private static List<Resolution> GetUniqueResolutions()
    {
        var unique = new List<Resolution>();
        var seen = new HashSet<Vector2Int>();

        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            Resolution res = Screen.resolutions[i];

            if (!seen.Add(new Vector2Int(res.width, res.height)))
                continue;

            unique.Add(res);
        }

        return unique;
    }

    private void InitPreset()
    {
        if (presetDropdown == null)
            return;

        presetDropdown.onValueChanged.RemoveAllListeners();
        presetDropdown.ClearOptions();
        presetDropdown.AddOptions(new List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Epic") });

        int saved = PlayerPrefs.GetInt("QualityPreset", -1);
        presetDropdown.SetValueWithoutNotify(Mathf.Clamp(saved, 0, 3));
        presetDropdown.onValueChanged.AddListener(OnPresetChanged);
    }

    private void OnPresetChanged(int index)
    {
        ApplyPreset(index);
    }

    private void ApplyPreset(int index)
    {
        Camera cam = GetTargetCamera();

        switch (index)
        {
            case 0: // Low
                SetTextureQuality(2);
                SetShadowQuality(1);
                PlayerPrefs.SetInt("VfxQuality", 1);
                ApplyMsaa(2);
                PlayerPrefs.SetInt("AntiAliasing", 1);
                ApplyDrawDistance(cam, 500f);
                ApplyPostProcessing(cam, true);
                GraphicsSettingsUI.ApplyUrpShadowDistance(50f);
                break;

            case 1: // Medium
                SetTextureQuality(1);
                SetShadowQuality(2);
                PlayerPrefs.SetInt("VfxQuality", 2);
                ApplyMsaa(4);
                PlayerPrefs.SetInt("AntiAliasing", 2);
                ApplyDrawDistance(cam, 1000f);
                ApplyPostProcessing(cam, true);
                GraphicsSettingsUI.ApplyUrpShadowDistance(70f);
                break;

            case 2: // High
                SetTextureQuality(0);
                SetShadowQuality(3);
                PlayerPrefs.SetInt("VfxQuality", 3);
                ApplyMsaa(8);
                PlayerPrefs.SetInt("AntiAliasing", 3);
                ApplyDrawDistance(cam, 1500f);
                ApplyPostProcessing(cam, true);
                GraphicsSettingsUI.ApplyUrpShadowDistance(120f);
                break;

            case 3: // Epic
                SetTextureQuality(0);
                SetShadowQuality(3);
                PlayerPrefs.SetInt("VfxQuality", 3);
                ApplyMsaa(8);
                PlayerPrefs.SetInt("AntiAliasing", 3);
                ApplyDrawDistance(cam, 2500f);
                ApplyPostProcessing(cam, true);
                GraphicsSettingsUI.ApplyUrpShadowDistance(150f);
                break;
        }

        PlayerPrefs.SetInt("QualityPreset", index);
        PlayerPrefs.Save();
    }

    private static void SetTextureQuality(int index)
    {
        QualitySettings.globalTextureMipmapLimit = index;
        PlayerPrefs.SetInt("TextureQuality", index);
    }

    private static void SetShadowQuality(int index)
    {
        GraphicsSettingsUI.ApplyUrpShadowSettings(index);
        PlayerPrefs.SetInt("ShadowQuality", index);
        PlayerPrefs.SetInt("ShadowResolution", index);
        PlayerPrefs.Save();
    }

    private static void ApplyMsaa(int sampleCount)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.msaaSampleCount = sampleCount;
    }

    private static void ApplyDrawDistance(Camera cam, float value)
    {
        if (cam != null)
            cam.farClipPlane = value;

        PlayerPrefs.SetFloat("DrawDistance", value);
    }

    private static void ApplyPostProcessing(Camera cam, bool enabled)
    {
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();

            if (data != null)
                data.renderPostProcessing = enabled;
        }

        PlayerPrefs.SetInt("PostProcessing", enabled ? 1 : 0);
    }

    private void InitVolumes()
    {
        InitMasterVolume();
        InitMusicVolume();
        InitVoiceChatVolume();
    }

    private void InitMasterVolume()
    {
        if (masterVolumeSlider == null)
            return;

        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 1f;
        AudioListener.volume = 0.8f;
        masterVolumeSlider.value = AudioListener.volume;
        UpdateMasterVolumeText(AudioListener.volume);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
        PlayerPrefs.Save();
        UpdateMasterVolumeText(value);
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (masterVolumeValueText != null)
            masterVolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private void InitMusicVolume()
    {
        if (musicVolumeSlider == null)
            return;

        float current = MusicManager.Instance != null
            ? MusicManager.Instance.Volume
            : PlayerPrefs.GetFloat("MusicVolume", 0.8f);

        musicVolumeSlider.minValue = 0f;
        musicVolumeSlider.maxValue = 1f;
        musicVolumeSlider.value = current;
        UpdateMusicVolumeText(current);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(value);

        UpdateMusicVolumeText(value);
    }

    private void UpdateMusicVolumeText(float value)
    {
        if (musicVolumeValueText != null)
            musicVolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private void InitVoiceChatVolume()
    {
        if (voiceChatVolumeSlider == null)
            return;

        float current = PlayerPrefs.GetFloat("VoiceChatVolume", 1f);

        voiceChatVolumeSlider.minValue = 0f;
        voiceChatVolumeSlider.maxValue = 1f;
        voiceChatVolumeSlider.value = current;
        UpdateVoiceChatVolumeText(current);
        voiceChatVolumeSlider.onValueChanged.AddListener(OnVoiceChatVolumeChanged);
    }

    private void OnVoiceChatVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("VoiceChatVolume", value);
        PlayerPrefs.Save();
        ProximityVoiceManager.RefreshVoiceChatVolume();
        UpdateVoiceChatVolumeText(value);
    }

    private void UpdateVoiceChatVolumeText(float value)
    {
        if (voiceChatVolumeValueText != null)
            voiceChatVolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private void InitDiktor()
    {
        if (diktorToggle != null)
        {
            diktorToggle.SetIsOnWithoutNotify(DiktorManager.IsDiktorEnabled());
            diktorToggle.onValueChanged.AddListener(OnDiktorToggleChanged);
        }

        if (diktorVolumeSlider != null)
        {
            float current = DiktorManager.GetDiktorVolume();

            diktorVolumeSlider.minValue = 0f;
            diktorVolumeSlider.maxValue = 1f;
            diktorVolumeSlider.value = current;
            UpdateDiktorVolumeText(current);
            diktorVolumeSlider.onValueChanged.AddListener(OnDiktorVolumeChanged);
        }
    }

    private void OnDiktorToggleChanged(bool enabled)
    {
        if (DiktorManager.Instance != null)
            DiktorManager.Instance.SetEnabled(enabled);
    }

    private void OnDiktorVolumeChanged(float value)
    {
        if (DiktorManager.Instance != null)
            DiktorManager.Instance.SetVolume(value);

        UpdateDiktorVolumeText(value);
    }

    private void UpdateDiktorVolumeText(float value)
    {
        if (diktorVolumeValueText != null)
            diktorVolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private static Camera GetTargetCamera()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;

            if (localClient != null && localClient.PlayerObject != null)
            {
                var cam = localClient.PlayerObject.GetComponentInChildren<Camera>();

                if (cam != null)
                    return cam;
            }
        }

        return Camera.main;
    }
}
