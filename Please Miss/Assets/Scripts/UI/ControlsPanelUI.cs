using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ControlsPanelUI : MonoBehaviour
{
    [Header("Mouse")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;

    [Header("Keyboard")]
    [Tooltip("Переназначение клавиш: функционал будет добавлен позже")]

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetLocalPlayerController();
        InitSensitivity();
    }

    private void InitSensitivity()
    {
        if (sensitivitySlider == null)
            return;

        sensitivitySlider.minValue = 0.01f;
        sensitivitySlider.maxValue = 0.3f;

        float saved = PlayerPrefs.GetFloat("MouseSensitivity", -1f);

        if (saved >= 0f)
        {
            sensitivitySlider.value = saved;
            ApplySensitivity(saved);
        }
        else if (playerController != null)
        {
            sensitivitySlider.value = playerController.mouseSensitivity;
        }

        UpdateSensitivityText(sensitivitySlider.value);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private void OnSensitivityChanged(float value)
    {
        ApplySensitivity(value);
        UpdateSensitivityText(value);
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
    }

    private void ApplySensitivity(float value)
    {
        if (playerController != null)
            playerController.mouseSensitivity = value;
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = (value * 100f).ToString("F0");
    }

    private static PlayerController GetLocalPlayerController()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;

            if (localClient != null && localClient.PlayerObject != null)
            {
                var pc = localClient.PlayerObject.GetComponentInChildren<PlayerController>();

                if (pc != null)
                    return pc;
            }
        }

        var all = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].IsOwner)
                return all[i];
        }

        return null;
    }
}
