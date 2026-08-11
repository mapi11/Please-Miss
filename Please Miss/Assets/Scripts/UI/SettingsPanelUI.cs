using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public class SettingsTabConfig
{
    [Tooltip("Кнопка, открывающая эту вкладку")]
    public Button tabButton;
    [Tooltip("Префаб панели вкладки")]
    public GameObject panelPrefab;
}

public class SettingsPanelUI : MonoBehaviour
{
    [Header("Tabs")]
    [Tooltip("Вкладки в порядке отображения. Первая (General) открыта при старте")]
    [SerializeField] private SettingsTabConfig[] tabs;
    [Tooltip("Контейнер, в который открывается панель активной вкладки")]
    [SerializeField] private RectTransform panelsRoot;

    [Header("Window")]
    [Tooltip("Root destroyed when ESC closes the settings window")]
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private Button backButton;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private readonly List<GameObject> activePanels = new List<GameObject>();
    private CanvasGroup canvasGroup;
    private CanvasGroup animationGroup;
    private int currentTabIndex = -1;

    private Transform AnimationTarget
    {
        get { return settingsRoot != null ? settingsRoot.transform : transform; }
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (backButton != null)
            backButton.onClick.AddListener(CloseWindow);
    }

    private void Start()
    {
        BindTabs();

        if (tabs != null && tabs.Length > 0)
            OpenTab(0);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        CloseWindow();
    }

    private void BindTabs()
    {
        if (tabs == null)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].tabButton == null)
                continue;

            int index = i;
            tabs[i].tabButton.onClick.AddListener(() => OpenTab(index));
        }

        RefreshTabButtons();
    }

    private void OpenTab(int index)
    {
        if (tabs == null || index < 0 || index >= tabs.Length || index == currentTabIndex)
            return;

        currentTabIndex = index;
        ClearPanels();

        if (tabs[index].panelPrefab != null)
        {
            GameObject panel = Instantiate(tabs[index].panelPrefab, panelsRoot);
            activePanels.Add(panel);
        }

        RefreshTabButtons();
    }

    private void ClearPanels()
    {
        for (int i = 0; i < activePanels.Count; i++)
        {
            if (activePanels[i] != null)
                Destroy(activePanels[i]);
        }

        activePanels.Clear();
    }

    private void RefreshTabButtons()
    {
        if (tabs == null)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].tabButton != null)
                tabs[i].tabButton.interactable = i != currentTabIndex;
        }
    }

    public void AnimateIn()
    {
        Transform target = AnimationTarget;
        target.localScale = Vector3.one * 0.8f;
        target.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);

        if (animationGroup != null)
        {
            animationGroup.alpha = 0f;
            animationGroup.DOFade(1f, animInDuration * 0.6f).SetUpdate(true).OnComplete(() =>
            {
                if (animationGroup != null)
                    animationGroup.interactable = true;
            });
        }
    }

    public void AnimateOut(Action onComplete)
    {
        Transform target = AnimationTarget;
        target.DOScale(0.8f, animOutDuration).SetEase(Ease.InBack).SetUpdate(true);

        if (animationGroup != null)
            animationGroup.DOFade(0f, animOutDuration * 0.6f).SetUpdate(true);

        DOVirtual.DelayedCall(animOutDuration, () =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        }, true);
    }

    private void CloseWindow()
    {
        if (settingsRoot != null)
        {
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen)
                SettingsMenu.Instance.OnPanelClosedExternally();
            else if (PauseMenu.Instance != null)
                PauseMenu.Instance.OnSettingsPanelClosedExternally();

            var panel = settingsRoot.GetComponentInChildren<SettingsPanelUI>(true);

            if (panel != null)
                panel.AnimateOut(() => Destroy(settingsRoot));
            else
                Destroy(settingsRoot);

            return;
        }

        if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen)
            SettingsMenu.Instance.Close();
        else if (PauseMenu.Instance != null)
            PauseMenu.Instance.CloseSettings();
    }
}
