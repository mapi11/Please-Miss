using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SniperScopeUI : MonoBehaviour
{
    [Header("Root contains black frames and crosshair")]
    [SerializeField] private GameObject root;

    [Header("Zoom")]
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private TMP_Text zoomText;

    [Header("Ammo")]
    [SerializeField] private Transform bulletsContent;
    [SerializeField] private BulletPanelUI bulletPanelPrefab;

    private void Start()
    {
        if (bulletsContent != null && FindObjectOfType<LobbyManager>() != null && LobbyManager.IsInLobby)
            bulletsContent.gameObject.SetActive(false);
    }

    public void Show(bool visible)
    {
        GameObject target = root != null ? root : gameObject;
        target.SetActive(visible);
    }

    public void SetZoom(float current, float minimum, float maximum)
    {
        if (zoomSlider != null)
        {
            zoomSlider.minValue = minimum;
            zoomSlider.maxValue = maximum;
            zoomSlider.value = current;
        }

        if (zoomText != null)
            zoomText.text = $"{current:0.#}x-{maximum:0.#}x";
    }

    public void SetBullets(IReadOnlyList<BulletDefinition> bullets)
    {
        if (bulletsContent == null || bulletPanelPrefab == null)
            return;

        for (int i = bulletsContent.childCount - 1; i >= 0; i--)
            Destroy(bulletsContent.GetChild(i).gameObject);

        for (int i = 0; i < bullets.Count; i++)
        {
            BulletDefinition def = bullets[i];
            Sprite icon = def != null ? def.UiIcon : null;
            Color color = def != null ? def.HeadColor : Color.white;
            BulletPanelUI panel = Instantiate(bulletPanelPrefab, bulletsContent);
            panel.Setup(icon, color);
        }
    }
}
