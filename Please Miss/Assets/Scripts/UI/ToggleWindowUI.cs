using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

public class ToggleWindowUI : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private GameObject windowContainer;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.3f;
    [SerializeField] private float animOutDuration = 0.2f;

    private bool animating;

    private void Awake()
    {
        if (windowContainer == null)
            windowContainer = gameObject;

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();

        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (backgroundButton != null)
            backgroundButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!windowContainer.activeSelf)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open()
    {
        if (animating || windowContainer.activeSelf)
            return;

        animating = true;
        windowContainer.SetActive(true);

        var rect = (RectTransform)windowContainer.transform;
        rect.localScale = Vector3.one * 0.85f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        rect.DOScale(Vector3.one, animInDuration).SetEase(Ease.OutBack, 1.2f);

        if (canvasGroup != null)
            canvasGroup.DOFade(1f, animInDuration * 0.6f);

        DOVirtual.DelayedCall(animInDuration, () => animating = false);
    }

    public void Close()
    {
        if (animating || !windowContainer.activeSelf)
            return;

        animating = true;

        var rect = (RectTransform)windowContainer.transform;
        rect.DOScale(Vector3.one * 0.85f, animOutDuration).SetEase(Ease.InBack);

        if (canvasGroup != null)
            canvasGroup.DOFade(0f, animOutDuration * 0.6f);

        DOVirtual.DelayedCall(animOutDuration, () =>
        {
            windowContainer.SetActive(false);
            animating = false;
        });
    }

    public void Toggle()
    {
        if (windowContainer.activeSelf)
            Close();
        else
            Open();
    }
}
