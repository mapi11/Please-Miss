using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccountPanelUI : MonoBehaviour
{
    [Tooltip("Сбрасывает все данные профиля (PlayerPrefs)")]
    [SerializeField] private Button resetButton;
    [Tooltip("Текст статуса после сброса (можно не назначать)")]
    [SerializeField] private TextMeshProUGUI statusText;

    private void Awake()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);
    }

    private void OnResetClicked()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        if (statusText != null)
            statusText.text = "All data cleared";
    }
}
