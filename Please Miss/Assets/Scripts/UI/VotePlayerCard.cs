using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка проголосовавшего игрока в панели голосования после конца игры:
/// имя игрока и его цвет. Ссылки назначаются в inspector.
/// </summary>
public class VotePlayerCard : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image colorImage;

    public void Setup(string playerName, Color32 color)
    {
        if (nameText != null)
            nameText.text = playerName;

        if (colorImage != null)
        {
            colorImage.enabled = true;
            colorImage.color = color;
        }
    }
}
