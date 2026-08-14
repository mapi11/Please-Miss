using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    private const int MaxFpsMax = 399;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettingsEarly()
    {
        if (PlayerPrefs.GetInt("QualityConfigCreated", 0) == 0)
        {
            PlayerPrefs.SetInt("QualityConfigCreated", 1);

            PlayerPrefs.SetInt("QualityPreset", 2);
            PlayerPrefs.SetInt("TextureQuality", 0);
            PlayerPrefs.SetInt("ShadowQuality", 3);
            PlayerPrefs.SetInt("ShadowResolution", 3);
            PlayerPrefs.SetInt("VfxQuality", 3);
            PlayerPrefs.SetInt("AntiAliasing", 3);
            PlayerPrefs.SetFloat("DrawDistance", 1500f);
            PlayerPrefs.SetInt("PostProcessing", 1);
            PlayerPrefs.SetFloat("ShadowDistance", 120f);
            PlayerPrefs.Save();
        }

        int savedWidth = PlayerPrefs.GetInt("ScreenWidth", 0);
        int savedHeight = PlayerPrefs.GetInt("ScreenHeight", 0);

        if (savedWidth > 0 && savedHeight > 0)
            Screen.SetResolution(savedWidth, savedHeight, (FullScreenMode)PlayerPrefs.GetInt("DisplayMode", 1));

        Screen.fullScreenMode = (FullScreenMode)PlayerPrefs.GetInt("DisplayMode", 1);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync", 1);
        Application.targetFrameRate = PlayerPrefs.GetInt("MaxFps", 144);

        int texture = PlayerPrefs.GetInt("TextureQuality", 1);
        QualitySettings.globalTextureMipmapLimit = texture;

        ApplyUrpShadowSettings(PlayerPrefs.GetInt("ShadowQuality", 2));
        ApplyUrpShadowDistance(PlayerPrefs.GetFloat("ShadowDistance", 100f));

        int aa = PlayerPrefs.GetInt("AntiAliasing", 2);
        ApplyMsaa(new[] { 1, 2, 4, 8 }[Mathf.Clamp(aa, 0, 3)]);

        ApplyRenderScale(PlayerPrefs.GetFloat("RenderScale", 1f));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void ApplySavedSettingsAfterSceneLoad()
    {
        Camera cam = GetTargetCamera();

        if (cam != null)
        {
            cam.fieldOfView = PlayerPrefs.GetFloat("Fov", 90f);
            cam.farClipPlane = PlayerPrefs.GetFloat("DrawDistance", 1000f);

            var data = cam.GetUniversalAdditionalCameraData();

            if (data != null)
                data.renderPostProcessing = PlayerPrefs.GetInt("PostProcessing", 1) == 1;
        }

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.supportsHDR = PlayerPrefs.GetInt("HDR", 1) == 1;

        SetVolumeEffectActive<Bloom>(PlayerPrefs.GetInt("Bloom", 1) == 1);
        SetVolumeEffectActive<MotionBlur>(PlayerPrefs.GetInt("MotionBlur", 0) == 1);
        SetVolumeEffectActive<ChromaticAberration>(PlayerPrefs.GetInt("ChromaticAberration", 0) == 1);
        SetVolumeEffectActive<DepthOfField>(PlayerPrefs.GetInt("DepthOfField", 0) == 1);
        SetVolumeEffectActive<ScreenSpaceLensFlare>(PlayerPrefs.GetInt("LensFlare", 0) == 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSettingsApplierExists()
    {
        if (GraphicsSettingsApplier.Instance != null)
            return;

        var applier = new GameObject("GraphicsSettingsApplier");
        applier.AddComponent<GraphicsSettingsApplier>();
    }

    public static void ApplyUrpShadowSettings(int index)
    {
        int clamped = Mathf.Clamp(index, 0, 3);
        int[] resolutions = { 1024, 2048, 4096, 8192 };
        int[] cascades = { 2, 2, 4, 4 };
        int[] additionalResolutions = { 256, 512, 1024, 2048 };
        float[] depthBias = { 0.2f, 0.2f, 0.2f, 0.2f };
        float[] normalBias = { 0.5f, 0.5f, 0.4f, 0.4f };

        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)clamped;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp == null)
            return;

        urp.mainLightShadowmapResolution = resolutions[clamped];
        urp.shadowCascadeCount = cascades[clamped];
        urp.additionalLightsShadowmapResolution = additionalResolutions[clamped];
        urp.shadowDepthBias = depthBias[clamped];
        urp.shadowNormalBias = normalBias[clamped];

        var softShadowsField = typeof(UniversalRenderPipelineAsset).GetField(
            "m_SoftShadowsSupported",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );

        if (softShadowsField != null)
            softShadowsField.SetValue(urp, true);
    }

    public static void ApplyUrpShadowDistance(float value)
    {
        QualitySettings.shadowDistance = value;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.shadowDistance = value;
    }

    private static void ApplyMsaa(int sampleCount)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.msaaSampleCount = sampleCount;
    }

    private static void ApplyRenderScale(float scale)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.renderScale = scale;
    }

    private static void SetVolumeEffectActive<T>(bool enabled) where T : VolumeComponent
    {
        var volume = FindFirstObjectByType<Volume>();

        if (volume == null || volume.profile == null)
            return;

        if (volume.profile.TryGet<T>(out var effect))
            effect.active = enabled;
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

    [Header("Display")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Slider maxFpsSlider;
    [SerializeField] private TMP_Text maxFpsValueText;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private TMP_Text fovValueText;

    [Header("Advanced")]
    [SerializeField] private Slider shadowDistanceSlider;
    [SerializeField] private TMP_Text shadowDistanceValueText;
    [SerializeField] private Toggle motionBlurToggle;
    [SerializeField] private Toggle bloomToggle;

    [Header("Quality")]
    [SerializeField] private TMP_Dropdown presetDropdown;
    [SerializeField] private TMP_Dropdown textureQualityDropdown;
    [SerializeField] private TMP_Dropdown shadowQualityDropdown;
    [SerializeField] private TMP_Dropdown effectsQualityDropdown;
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;
    [SerializeField] private TMP_Dropdown viewDistanceDropdown;
    [SerializeField] private Toggle postProcessingToggle;

    [Header("Other")]
    [SerializeField] private Toggle hdrToggle;
    [SerializeField] private Toggle chromaticAberrationToggle;
    [SerializeField] private Toggle depthOfFieldToggle;
    [SerializeField] private Toggle lensFlareToggle;
    [SerializeField] private Slider renderScaleSlider;
    [SerializeField] private TMP_Text renderScaleValueText;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private bool ignorePresetChange;
    private List<Resolution> resolutions;

    private int SavedPreset => PlayerPrefs.GetInt("QualityPreset", -1);

    private string Loc(string key)
    {
        string value = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Table", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        ignorePresetChange = true;
        InitResolution();
        InitDisplayMode();
        InitVSync();
        InitMaxFps();
        InitFov();
        InitShadowDistance();
        InitMotionBlur();
        InitBloom();
        InitPreset();
        InitTextureQuality();
        InitShadowQuality();
        InitEffectsQuality();
        InitAntiAliasing();
        InitViewDistance();
        InitPostProcessing();
        InitHDR();
        InitChromaticAberration();
        InitDepthOfField();
        InitLensFlare();
        InitRenderScale();
        ignorePresetChange = false;

        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    private void AnimateIn()
    {
        transform.localScale = Vector3.one * 0.8f;
        transform.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, animInDuration * 0.6f).SetUpdate(true).OnComplete(() =>
            {
                if (canvasGroup != null)
                    canvasGroup.interactable = true;
            });
        }
    }

    public void AnimateOut(Action onComplete)
    {
        transform.DOScale(0.8f, animOutDuration).SetEase(Ease.InBack).SetUpdate(true);

        if (canvasGroup != null)
            canvasGroup.DOFade(0f, animOutDuration * 0.6f).SetUpdate(true);

        DOVirtual.DelayedCall(animOutDuration, () =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        }, true);
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        InitResolution();
        InitDisplayMode();
        InitPreset();
        InitTextureQuality();
        InitShadowQuality();
        InitEffectsQuality();
        InitAntiAliasing();
        InitViewDistance();

        if (maxFpsSlider != null)
            UpdateMaxFpsText(maxFpsSlider.value);
    }

    private void OnBack()
    {
        AnimateOut(null);
    }

    private void MarkPresetChanged()
    {
        if (!ignorePresetChange && presetDropdown != null)
            presetDropdown.SetValueWithoutNotify(-1);
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

    private void InitResolution()
    {
        if (resolutionDropdown == null)
            return;

        resolutions = GetUniqueResolutions();

        resolutionDropdown.onValueChanged.RemoveAllListeners();
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

    private void InitDisplayMode()
    {
        if (displayModeDropdown == null) return;

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

    private void InitVSync()
    {
        if (vSyncToggle == null) return;

        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        SetVSync(PlayerPrefs.GetInt("VSync", 1) == 1);
    }

    private void OnVSyncChanged(bool enabled)
    {
        SetVSync(enabled);
    }

    private void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt("VSync", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitMaxFps()
    {
        if (maxFpsSlider == null) return;

        maxFpsSlider.minValue = 60f;
        maxFpsSlider.maxValue = MaxFpsMax;
        maxFpsSlider.wholeNumbers = true;
        maxFpsSlider.onValueChanged.AddListener(OnMaxFpsChanged);
        SetMaxFps(PlayerPrefs.GetInt("MaxFps", 144));
    }

    private void OnMaxFpsChanged(float value)
    {
        SetMaxFps(value);
    }

    private void SetMaxFps(float value)
    {
        int fps = Mathf.RoundToInt(value);
        Application.targetFrameRate = fps >= MaxFpsMax ? -1 : fps;
        PlayerPrefs.SetInt("MaxFps", fps);
        PlayerPrefs.Save();
        UpdateMaxFpsText(fps);

        if (maxFpsSlider != null)
            maxFpsSlider.SetValueWithoutNotify(fps);
    }

    private void UpdateMaxFpsText(float value)
    {
        int fps = Mathf.RoundToInt(value);

        if (maxFpsValueText != null)
            maxFpsValueText.text = fps >= MaxFpsMax ? Loc("Unlimited") : fps.ToString();
    }

    private void InitFov()
    {
        if (fovSlider == null) return;

        fovSlider.minValue = 80f;
        fovSlider.maxValue = 120f;
        fovSlider.wholeNumbers = true;
        fovSlider.onValueChanged.AddListener(OnFovChanged);
        SetFov(PlayerPrefs.GetFloat("Fov", 90f));
    }

    private void OnFovChanged(float value)
    {
        SetFov(value);
    }

    private void SetFov(float value)
    {
        Camera cam = GetTargetCamera();

        if (cam != null)
            cam.fieldOfView = value;

        PlayerPrefs.SetFloat("Fov", value);
        PlayerPrefs.Save();
        UpdateFovText(value);

        if (fovSlider != null)
            fovSlider.SetValueWithoutNotify(value);
    }

    private void UpdateFovText(float value)
    {
        if (fovValueText != null)
            fovValueText.text = Mathf.RoundToInt(value).ToString();
    }

    private void InitShadowDistance()
    {
        if (shadowDistanceSlider == null) return;

        shadowDistanceSlider.minValue = 50f;
        shadowDistanceSlider.maxValue = 200f;
        shadowDistanceSlider.onValueChanged.AddListener(OnShadowDistanceChanged);
        SetShadowDistance(PlayerPrefs.GetFloat("ShadowDistance", 100f));
    }

    private void OnShadowDistanceChanged(float value)
    {
        SetShadowDistance(value);
    }

    private void SetShadowDistance(float value)
    {
        ApplyUrpShadowDistance(value);
        PlayerPrefs.SetFloat("ShadowDistance", value);
        PlayerPrefs.Save();
        UpdateShadowDistanceText(value);

        if (shadowDistanceSlider != null)
            shadowDistanceSlider.SetValueWithoutNotify(value);
    }

    private void UpdateShadowDistanceText(float value)
    {
        if (shadowDistanceValueText != null)
            shadowDistanceValueText.text = value.ToString("F0");
    }

    private void InitMotionBlur()
    {
        if (motionBlurToggle == null) return;

        motionBlurToggle.onValueChanged.AddListener(OnMotionBlurChanged);
        SetMotionBlur(PlayerPrefs.GetInt("MotionBlur", 0) == 1);
    }

    private void OnMotionBlurChanged(bool enabled)
    {
        SetMotionBlur(enabled);
    }

    private void SetMotionBlur(bool enabled)
    {
        SetVolumeEffectActive<MotionBlur>(enabled);
        PlayerPrefs.SetInt("MotionBlur", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (motionBlurToggle != null)
            motionBlurToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitBloom()
    {
        if (bloomToggle == null) return;

        bloomToggle.onValueChanged.AddListener(OnBloomChanged);
        SetBloom(PlayerPrefs.GetInt("Bloom", 1) == 1);
    }

    private void OnBloomChanged(bool enabled)
    {
        SetBloom(enabled);
    }

    private void SetBloom(bool enabled)
    {
        SetVolumeEffectActive<Bloom>(enabled);
        PlayerPrefs.SetInt("Bloom", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (bloomToggle != null)
            bloomToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitPreset()
    {
        if (presetDropdown == null) return;

        presetDropdown.onValueChanged.RemoveAllListeners();
        presetDropdown.ClearOptions();
        presetDropdown.AddOptions(new List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Epic") });
        presetDropdown.SetValueWithoutNotify(Mathf.Clamp(SavedPreset, 0, 3));
        presetDropdown.onValueChanged.AddListener(OnPresetChanged);
    }

    private void ApplyPreset(int index)
    {
        ignorePresetChange = true;

        switch (index)
        {
            case 0: // Low
                SetTextureQuality(2);
                SetShadowQuality(1);
                SetEffectsQuality(1);
                SetAntiAliasing(1);
                SetViewDistance(0);
                SetPostProcessing(true);
                SetShadowDistance(50f);
                break;

            case 1: // Medium
                SetTextureQuality(1);
                SetShadowQuality(2);
                SetEffectsQuality(2);
                SetAntiAliasing(2);
                SetViewDistance(1);
                SetPostProcessing(true);
                SetShadowDistance(70f);
                break;

            case 2: // High
                SetTextureQuality(0);
                SetShadowQuality(3);
                SetEffectsQuality(3);
                SetAntiAliasing(3);
                SetViewDistance(2);
                SetPostProcessing(true);
                SetShadowDistance(120f);
                break;

            case 3: // Epic
                SetTextureQuality(0);
                SetShadowQuality(3);
                SetEffectsQuality(3);
                SetAntiAliasing(3);
                SetViewDistance(3);
                SetPostProcessing(true);
                SetShadowDistance(150f);
                break;
        }

        PlayerPrefs.SetInt("QualityPreset", index);
        PlayerPrefs.Save();

        ignorePresetChange = false;
        presetDropdown.SetValueWithoutNotify(index);
    }

    private void OnPresetChanged(int index)
    {
        ApplyPreset(index);
    }

    private void InitTextureQuality()
    {
        if (textureQualityDropdown == null) return;

        textureQualityDropdown.onValueChanged.RemoveAllListeners();
        textureQualityDropdown.ClearOptions();
        textureQualityDropdown.AddOptions(new List<string> { Loc("Epic"), Loc("High"), Loc("Medium"), Loc("Low") });
        textureQualityDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("TextureQuality", 1), 0, 3));
        textureQualityDropdown.onValueChanged.AddListener(OnTextureQualityChanged);
    }

    private void OnTextureQualityChanged(int index)
    {
        MarkPresetChanged();
        SetTextureQuality(index);
    }

    private void SetTextureQuality(int index)
    {
        // index 0=Epic → mipmapLimit 0 (full res), index 3=Low → mipmapLimit 3 (most compressed)
        QualitySettings.globalTextureMipmapLimit = index;
        PlayerPrefs.SetInt("TextureQuality", index);
        PlayerPrefs.Save();

        if (textureQualityDropdown != null)
            textureQualityDropdown.SetValueWithoutNotify(index);
    }

    private void InitShadowQuality()
    {
        if (shadowQualityDropdown == null) return;

        shadowQualityDropdown.onValueChanged.RemoveAllListeners();
        shadowQualityDropdown.ClearOptions();
        shadowQualityDropdown.AddOptions(new List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Epic") });
        shadowQualityDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("ShadowQuality", 2), 0, 3));
        shadowQualityDropdown.onValueChanged.AddListener(OnShadowQualityChanged);
    }

    private void OnShadowQualityChanged(int index)
    {
        MarkPresetChanged();
        SetShadowQuality(index);
    }

    private void SetShadowQuality(int index)
    {
        ApplyUrpShadowSettings(index);
        PlayerPrefs.SetInt("ShadowQuality", index);
        PlayerPrefs.SetInt("ShadowResolution", index);
        PlayerPrefs.Save();

        if (shadowQualityDropdown != null)
            shadowQualityDropdown.SetValueWithoutNotify(index);
    }

    private void InitEffectsQuality()
    {
        if (effectsQualityDropdown == null) return;

        effectsQualityDropdown.onValueChanged.RemoveAllListeners();
        effectsQualityDropdown.ClearOptions();
        effectsQualityDropdown.AddOptions(new List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Epic") });
        effectsQualityDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("VfxQuality", 2), 0, 3));
        effectsQualityDropdown.onValueChanged.AddListener(OnEffectsQualityChanged);
    }

    private void OnEffectsQualityChanged(int index)
    {
        MarkPresetChanged();
        SetEffectsQuality(index);
    }

    private void SetEffectsQuality(int index)
    {
        PlayerPrefs.SetInt("VfxQuality", index);
        PlayerPrefs.Save();

        if (effectsQualityDropdown != null)
            effectsQualityDropdown.SetValueWithoutNotify(index);
    }

    private void InitAntiAliasing()
    {
        if (antiAliasingDropdown == null) return;

        antiAliasingDropdown.onValueChanged.RemoveAllListeners();
        antiAliasingDropdown.ClearOptions();
        antiAliasingDropdown.AddOptions(new List<string> { "Off", "MSAA 2x", "MSAA 4x", "MSAA 8x" });
        antiAliasingDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("AntiAliasing", 2), 0, 3));
        antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
    }

    private void OnAntiAliasingChanged(int index)
    {
        MarkPresetChanged();
        SetAntiAliasing(index);
    }

    private void SetAntiAliasing(int index)
    {
        ApplyMsaa(new[] { 1, 2, 4, 8 }[Mathf.Clamp(index, 0, 3)]);
        PlayerPrefs.SetInt("AntiAliasing", index);
        PlayerPrefs.Save();

        if (antiAliasingDropdown != null)
            antiAliasingDropdown.SetValueWithoutNotify(index);
    }

    private void InitViewDistance()
    {
        if (viewDistanceDropdown == null) return;

        viewDistanceDropdown.onValueChanged.RemoveAllListeners();
        viewDistanceDropdown.ClearOptions();
        viewDistanceDropdown.AddOptions(new List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Epic") });
        viewDistanceDropdown.SetValueWithoutNotify(Mathf.Clamp(ViewDistanceToIndex(PlayerPrefs.GetFloat("DrawDistance", 1000f)), 0, 3));
        viewDistanceDropdown.onValueChanged.AddListener(OnViewDistanceChanged);
    }

    private void OnViewDistanceChanged(int index)
    {
        MarkPresetChanged();
        SetViewDistance(index);
    }

    private void SetViewDistance(int index)
    {
        float[] distances = { 500f, 1000f, 1500f, 2500f };
        float value = distances[Mathf.Clamp(index, 0, distances.Length - 1)];

        Camera cam = GetTargetCamera();

        if (cam != null)
            cam.farClipPlane = value;

        PlayerPrefs.SetFloat("DrawDistance", value);
        PlayerPrefs.Save();

        if (viewDistanceDropdown != null)
            viewDistanceDropdown.SetValueWithoutNotify(index);
    }

    private static int ViewDistanceToIndex(float distance)
    {
        if (distance <= 750f) return 0;
        if (distance <= 1250f) return 1;
        if (distance <= 2000f) return 2;
        return 3;
    }

    private void InitPostProcessing()
    {
        if (postProcessingToggle == null) return;

        postProcessingToggle.onValueChanged.AddListener(OnPostProcessingChanged);
        SetPostProcessing(PlayerPrefs.GetInt("PostProcessing", 1) == 1);
    }

    private void OnPostProcessingChanged(bool enabled)
    {
        MarkPresetChanged();
        SetPostProcessing(enabled);
    }

    private void SetPostProcessing(bool enabled)
    {
        Camera cam = GetTargetCamera();

        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();

            if (data != null)
                data.renderPostProcessing = enabled;
        }

        PlayerPrefs.SetInt("PostProcessing", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (postProcessingToggle != null)
            postProcessingToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitHDR()
    {
        if (hdrToggle == null) return;

        hdrToggle.onValueChanged.AddListener(OnHDRChanged);
        SetHDR(PlayerPrefs.GetInt("HDR", 1) == 1);
    }

    private void OnHDRChanged(bool enabled)
    {
        SetHDR(enabled);
    }

    private void SetHDR(bool enabled)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.supportsHDR = enabled;

        PlayerPrefs.SetInt("HDR", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (hdrToggle != null)
            hdrToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitChromaticAberration()
    {
        if (chromaticAberrationToggle == null) return;

        chromaticAberrationToggle.onValueChanged.AddListener(OnChromaticAberrationChanged);
        SetChromaticAberration(PlayerPrefs.GetInt("ChromaticAberration", 0) == 1);
    }

    private void OnChromaticAberrationChanged(bool enabled)
    {
        SetChromaticAberration(enabled);
    }

    private void SetChromaticAberration(bool enabled)
    {
        SetVolumeEffectActive<ChromaticAberration>(enabled);
        PlayerPrefs.SetInt("ChromaticAberration", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (chromaticAberrationToggle != null)
            chromaticAberrationToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitDepthOfField()
    {
        if (depthOfFieldToggle == null) return;

        depthOfFieldToggle.onValueChanged.AddListener(OnDepthOfFieldChanged);
        SetDepthOfField(PlayerPrefs.GetInt("DepthOfField", 0) == 1);
    }

    private void OnDepthOfFieldChanged(bool enabled)
    {
        SetDepthOfField(enabled);
    }

    private void SetDepthOfField(bool enabled)
    {
        SetVolumeEffectActive<DepthOfField>(enabled);
        PlayerPrefs.SetInt("DepthOfField", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (depthOfFieldToggle != null)
            depthOfFieldToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitLensFlare()
    {
        if (lensFlareToggle == null) return;

        lensFlareToggle.onValueChanged.AddListener(OnLensFlareChanged);
        SetLensFlare(PlayerPrefs.GetInt("LensFlare", 0) == 1);
    }

    private void OnLensFlareChanged(bool enabled)
    {
        SetLensFlare(enabled);
    }

    private void SetLensFlare(bool enabled)
    {
        SetVolumeEffectActive<ScreenSpaceLensFlare>(enabled);
        PlayerPrefs.SetInt("LensFlare", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (lensFlareToggle != null)
            lensFlareToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitRenderScale()
    {
        if (renderScaleSlider == null) return;

        renderScaleSlider.minValue = 0.5f;
        renderScaleSlider.maxValue = 1.5f;
        renderScaleSlider.onValueChanged.AddListener(OnRenderScaleChanged);
        SetRenderScale(PlayerPrefs.GetFloat("RenderScale", 1f));
    }

    private void OnRenderScaleChanged(float value)
    {
        SetRenderScale(value);
    }

    private void SetRenderScale(float value)
    {
        ApplyRenderScale(value);
        PlayerPrefs.SetFloat("RenderScale", value);
        PlayerPrefs.Save();
        UpdateRenderScaleText(value);

        if (renderScaleSlider != null)
            renderScaleSlider.SetValueWithoutNotify(value);
    }

    private void UpdateRenderScaleText(float value)
    {
        if (renderScaleValueText != null)
            renderScaleValueText.text = value.ToString("0%");
    }
}
