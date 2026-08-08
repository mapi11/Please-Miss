using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image objectImg;
    [SerializeField] private TMP_Text itemNameTxt;

    [Header("Slot Texts")]
    [Tooltip("Текст назначения предмета (Purpose). Используется на карточках в InventoryContainer")]
    [SerializeField] private TMP_Text purposeText;

    [Header("Empty State")]
    [Tooltip("Панель-пустышка пустого слота (например, рамка с текстом \"Empty slot\"). Видна, пока слот пуст; отключается, когда предмет встаёт в слот")]
    [SerializeField] private GameObject emptySlotPanel;

    [Header("Slot Button")]
    [Tooltip("Единственная кнопка плашки — покрывает весь фон слота. Клик открывает ItemInfo/RifleInfo. Кнопки взаимодействия (Equip/Sell/Unequip) находятся внутри InfoPanel")]
    [SerializeField] private Button cardButton;

    [Header("Card Buttons")]
    [Tooltip("Кнопка на карточке слота снаряжения: убирает предмет обратно в инвентарь. На карточке винтовки не показывается (винтовку можно только заменить)")]
    [SerializeField] private Button unequipButton;

    public Image ObjectImg => objectImg;
    public TMP_Text ItemNameTxt => itemNameTxt;
    public TMP_Text PurposeText => purposeText;
    public GameObject EmptySlotPanel => emptySlotPanel;
    public Button CardButton => cardButton;
    public Button UnequipButton => unequipButton;
}
