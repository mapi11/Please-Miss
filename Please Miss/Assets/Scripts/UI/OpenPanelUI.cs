using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class OpenPanelUI : MonoBehaviour
{
    private static readonly List<OpenPanelUI> openStack = new List<OpenPanelUI>();

    [Header("Open")]
    [Tooltip("Кнопка, открывающая окно")]
    [SerializeField] private Button openButton;
    [Tooltip("Префаб окна, который будет создаваться")]
    [SerializeField] private GameObject windowPrefab;
    [Tooltip("Контейнер, в который создаётся окно. Если пусто - окно создаётся на корне канваса")]
    [SerializeField] private RectTransform windowContainer;

    [Header("Behaviour")]
    [Tooltip("Имя кнопки закрытия внутри префаба окна")]
    [SerializeField] private string closeButtonName = "CloseBgButton";

    [Header("Animation")]
    [SerializeField] private float openDuration = 0.3f;
    [SerializeField] private float closeDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    private GameObject currentWindow;
    private bool animating;

    private void Awake()
    {
        if (openButton != null)
            openButton.onClick.AddListener(OpenWindow);
    }

    private void Update()
    {
        if (openStack.Count == 0 || openStack[openStack.Count - 1] != this)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseWindow();
    }

    private void OnDestroy()
    {
        openStack.Remove(this);
    }

    public void OpenWindow()
    {
        if (currentWindow != null || animating || windowPrefab == null)
            return;

        Transform parent = windowContainer != null ? windowContainer : transform.root;

        currentWindow = Instantiate(windowPrefab, parent);
        currentWindow.transform.SetAsLastSibling();
        currentWindow.SetActive(true);

        openStack.Add(this);

        AnimateIn(currentWindow);
    }

    public void CloseWindow()
    {
        if (currentWindow == null || animating)
            return;

        animating = true;

        AnimateOut(currentWindow, () =>
        {
            openStack.Remove(this);

            Destroy(currentWindow);
            currentWindow = null;
            animating = false;
        });
    }

    private void AnimateIn(GameObject window)
    {
        var rect = (RectTransform)window.transform;
        rect.localScale = Vector3.one * 0.85f;

        var group = window.GetComponentInChildren<CanvasGroup>();

        if (group != null)
            group.alpha = 0f;

        rect.DOScale(Vector3.one, openDuration).SetEase(openEase).OnComplete(() => animating = false);

        if (group != null)
            group.DOFade(1f, openDuration * 0.6f);

        FindCloseButton(window)?.onClick.AddListener(CloseWindow);
    }

    private void AnimateOut(GameObject window, Action onDone)
    {
        var rect = (RectTransform)window.transform;
        rect.DOScale(Vector3.one * 0.85f, closeDuration).SetEase(closeEase);

        var group = window.GetComponentInChildren<CanvasGroup>();

        if (group != null)
            group.DOFade(0f, closeDuration * 0.6f);

        DOVirtual.DelayedCall(closeDuration, () => onDone());
    }

    private Button FindCloseButton(GameObject window)
    {
        if (string.IsNullOrEmpty(closeButtonName))
            return null;

        foreach (var button in window.GetComponentsInChildren<Button>(true))
        {
            if (string.Equals(button.gameObject.name, closeButtonName, StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }
}
