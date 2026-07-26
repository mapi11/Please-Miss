using UnityEngine;
using UnityEngine.UI;

public class ChargeSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private GameObject panel;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();

        if (panel == null)
            panel = gameObject;

        Hide();
    }

    public void Show(float value)
    {
        if (panel != null)
            panel.SetActive(true);

        if (slider != null)
        {
            slider.value = value;
            slider.gameObject.SetActive(true);
        }
    }

    public void UpdateValue(float normalized)
    {
        if (slider != null)
            slider.value = normalized;
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
