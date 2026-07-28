using UnityEngine;
using UnityEngine.UI;

public sealed class BulletPanelUI : MonoBehaviour
{
    [SerializeField] private Image bulletImage;

    public void Setup(Sprite icon, Color color)
    {
        if (bulletImage == null)
            return;

        bulletImage.sprite = icon;
        bulletImage.color = color;
        bulletImage.enabled = icon != null;
    }
}
