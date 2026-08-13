using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class AudioPanelUI : MonoBehaviour
{
    [Header("Volumes")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeValueText;

    [Header("Audio")]
    [Tooltip("Единственная опция System Default: Unity не предоставляет список устройств вывода")]
    [SerializeField] private TMP_Dropdown outputDeviceDropdown;

    [Header("Diktor")]
    [SerializeField] private Toggle diktorToggle;
    [SerializeField] private Slider diktorVolumeSlider;
    [SerializeField] private TextMeshProUGUI diktorVolumeValueText;

    [Header("Voice Chat")]
    [SerializeField] private Toggle voiceChatToggle;
    [SerializeField] private Slider voiceChatVolumeSlider;
    [SerializeField] private TextMeshProUGUI voiceChatVolumeValueText;
    [Tooltip("Нажми, затем нажми клавишу — она станет Push to Talk")]
    [SerializeField] private Button pushToTalkButton;
    [SerializeField] private TextMeshProUGUI pushToTalkLabel;

    [Header("Microphone")]
    [SerializeField] private TMP_Dropdown micDropdown;
    [SerializeField] private Slider micVolumeSlider;
    [SerializeField] private TextMeshProUGUI micVolumeValueText;

    private bool capturingKey;

    private void Start()
    {
        InitVolume();
        InitMusicVolume();
        InitDiktor();
        InitOutputDevice();
        InitVoiceChat();
        InitVoiceChatVolume();
        InitPushToTalk();
        InitMicDropdown();
        InitMicVolume();
    }

    private void Update()
    {
        if (!capturingKey)
            return;

        if (Keyboard.current == null)
            return;

        foreach (var control in Keyboard.current.allControls)
        {
            if (control is not KeyControl keyControl)
                continue;

            if (!keyControl.isPressed)
                continue;

            Key key = keyControl.keyCode;

            if (key == Key.None)
                continue;

            if (key == Key.Escape)
            {
                CancelKeyCapture();
                return;
            }

            ProximityVoiceManager.SetPushToTalkKey(key);
            UpdatePushToTalkLabel(key);
            CancelKeyCapture();
            return;
        }
    }

    private void InitVolume()
    {
        if (volumeSlider == null)
            return;

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = AudioListener.volume;
        UpdateVolumeText(AudioListener.volume);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
        PlayerPrefs.Save();
        UpdateVolumeText(value);
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
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

    private void InitOutputDevice()
    {
        if (outputDeviceDropdown == null)
            return;

        outputDeviceDropdown.ClearOptions();
        outputDeviceDropdown.AddOptions(new System.Collections.Generic.List<string> { "System Default" });
        outputDeviceDropdown.SetValueWithoutNotify(0);
        outputDeviceDropdown.onValueChanged.AddListener(OnOutputDeviceChanged);
    }

    private void OnOutputDeviceChanged(int index)
    {
        PlayerPrefs.SetInt("AudioOutputDevice", index);
        PlayerPrefs.Save();
    }

    private void InitVoiceChat()
    {
        if (voiceChatToggle == null)
            return;

        voiceChatToggle.SetIsOnWithoutNotify(ProximityVoiceManager.IsVoiceChatEnabled());
        voiceChatToggle.onValueChanged.AddListener(OnVoiceChatChanged);
    }

    private void OnVoiceChatChanged(bool enabled)
    {
        ProximityVoiceManager.SetVoiceChatEnabled(enabled);
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

    private void InitPushToTalk()
    {
        UpdatePushToTalkLabel(ProximityVoiceManager.GetPushToTalkKey());

        if (pushToTalkButton != null)
            pushToTalkButton.onClick.AddListener(StartKeyCapture);
    }

    private void StartKeyCapture()
    {
        capturingKey = true;

        if (pushToTalkLabel != null)
            pushToTalkLabel.text = "...";
    }

    private void CancelKeyCapture()
    {
        capturingKey = false;
        UpdatePushToTalkLabel(ProximityVoiceManager.GetPushToTalkKey());
    }

    private void UpdatePushToTalkLabel(Key key)
    {
        if (pushToTalkLabel != null)
            pushToTalkLabel.text = key.ToString();
    }

    private void InitMicDropdown()
    {
        if (micDropdown == null)
            return;

        micDropdown.ClearOptions();

        string[] devices = Microphone.devices;
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();

        if (devices.Length == 0)
        {
            options.Add(new TMP_Dropdown.OptionData("No Mic"));
        }
        else
        {
            for (int i = 0; i < devices.Length; i++)
                options.Add(new TMP_Dropdown.OptionData(devices[i]));
        }

        micDropdown.AddOptions(options);

        string saved = VoiceChatSettings.GetSelectedOrDefaultMicrophoneName();
        int savedIndex = 0;

        if (!string.IsNullOrEmpty(saved))
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i] == saved)
                {
                    savedIndex = i;
                    break;
                }
            }
        }

        micDropdown.SetValueWithoutNotify(Mathf.Clamp(savedIndex, 0, options.Count - 1));
        micDropdown.onValueChanged.AddListener(OnMicChanged);
    }

    private void OnMicChanged(int index)
    {
        string[] devices = Microphone.devices;

        if (index >= 0 && index < devices.Length)
        {
            VoiceChatSettings.SetSelectedMicrophone(devices[index]);

            if (ProximityVoiceManager.Instance != null)
                ProximityVoiceManager.Instance.RestartMicrophone();
        }
    }

    private void InitMicVolume()
    {
        if (micVolumeSlider == null)
            return;

        float current = ProximityVoiceManager.GetMicrophoneVolume();

        micVolumeSlider.minValue = 0f;
        micVolumeSlider.maxValue = 1f;
        micVolumeSlider.value = current;
        UpdateMicVolumeText(current);
        micVolumeSlider.onValueChanged.AddListener(OnMicVolumeChanged);
    }

    private void OnMicVolumeChanged(float value)
    {
        ProximityVoiceManager.SetMicrophoneVolume(value);
        UpdateMicVolumeText(value);
    }

    private void UpdateMicVolumeText(float value)
    {
        if (micVolumeValueText != null)
            micVolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }
}
