using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image objectImg;
    [SerializeField] private TMP_Text itemNameTxt;
    [SerializeField] private Image lockImg;

    [Tooltip("Optional. If assigned, the slot shows a location dropdown (Inventory / Player / Sell)")]
    [SerializeField] private TMP_Dropdown locationDropdown;

    public Image ObjectImg => objectImg;
    public TMP_Text ItemNameTxt => itemNameTxt;
    public Image LockImg => lockImg;
    public TMP_Dropdown LocationDropdown => locationDropdown;
}
