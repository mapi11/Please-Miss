using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SniperRifleItemPanel : MonoBehaviour
{
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;

    public void Setup(PickableItem rifle, bool showEquipButton, string equipLabel, System.Action onEquip)
    {
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(showEquipButton);

            if (showEquipButton)
            {
                equipButton.onClick.RemoveAllListeners();
                equipButton.onClick.AddListener(() => onEquip?.Invoke());
            }
        }

        if (equipButtonText != null)
            equipButtonText.text = equipLabel;

        if (rifle == null)
        {
            if (iconImg != null)
            {
                iconImg.sprite = null;
                iconImg.enabled = false;
            }

            if (nameTxt != null)
                nameTxt.text = "Empty";

            return;
        }

        if (iconImg != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = rifle.InventoryIcon;
            iconImg.color = rifle.InventoryIcon != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (nameTxt != null)
            nameTxt.text = rifle.ItemName;
    }
}
