using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GeneralPanelUI : MonoBehaviour
{
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

    private List<Resolution> resolutions;

    private void Start()
    {
        InitDisplayMode();
        InitResolution();
        InitPreset();
        InitVolumes();
    }

    private void InitDisplayMode()
    {
        if (displayModeDropdown == null)
            return;

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless", "Windowed" });
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

        presetDropdown.ClearOptions();
        presetDropdown.AddOptions(new List<string> { "Low", "Medium", "High", "Epic" });

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
                SetTextureQuality(3);
                SetShadowQuality(0);
                PlayerPrefs.SetInt("VfxQuality", 0);
                ApplyMsaa(1);
                PlayerPrefs.SetInt("AntiAliasing", 0);
                ApplyDrawDistance(cam, 200f);
                ApplyPostProcessing(cam, false);
                break;

            case 1: // Medium
                SetTextureQuality(2);
                SetShadowQuality(1);
                PlayerPrefs.SetInt("VfxQuality", 1);
                ApplyMsaa(2);
                PlayerPrefs.SetInt("AntiAliasing", 1);
                ApplyDrawDistance(cam, 500f);
                ApplyPostProcessing(cam, true);
                break;

            case 2: // High
                SetTextureQuality(1);
                SetShadowQuality(2);
                PlayerPrefs.SetInt("VfxQuality", 2);
                ApplyMsaa(4);
                PlayerPrefs.SetInt("AntiAliasing", 2);
                ApplyDrawDistance(cam, 1000f);
                ApplyPostProcessing(cam, true);
                break;

            case 3: // Epic
                SetTextureQuality(0);
                SetShadowQuality(3);
                PlayerPrefs.SetInt("VfxQuality", 3);
                ApplyMsaa(8);
                PlayerPrefs.SetInt("AntiAliasing", 3);
                ApplyDrawDistance(cam, 1500f);
                ApplyPostProcessing(cam, true);
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
        QualitySettings.shadows = (UnityEngine.ShadowQuality)Mathf.Clamp(index, 0, 2);

        if (index == 3)
        {
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            PlayerPrefs.SetInt("ShadowResolution", 3);
        }

        PlayerPrefs.SetInt("ShadowQuality", index);
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
