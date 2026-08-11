using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguagePanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown languageDropdown;

    private void Awake()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        InitLanguageDropdown();
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        InitLanguageDropdown();
    }

    private void InitLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.onValueChanged.RemoveAllListeners();
        languageDropdown.ClearOptions();

        var locales = LocalizationSettings.AvailableLocales.Locales;
        var options = new System.Collections.Generic.List<string>();

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
}
