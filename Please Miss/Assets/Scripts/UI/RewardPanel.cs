using TMPro;
using UnityEngine;

public class RewardPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Очки за основное действие, например \"+50 Kill\" или \"+250 Finish\"")]
    [SerializeField] private TMP_Text mainPointsText;
    [Tooltip("Бонусные очки, например \"+10 Left Hand\"")]
    [SerializeField] private TMP_Text bonusPointsText;

    public void Setup(int mainPoints, string mainLabel, int bonusPoints, string bonusLabel)
    {
        ResolveElements();

        if (mainPointsText != null)
            mainPointsText.text = $"+{mainPoints} {mainLabel}";

        if (bonusPointsText != null)
            bonusPointsText.text = bonusPoints > 0 && !string.IsNullOrWhiteSpace(bonusLabel)
                ? $"+{bonusPoints} {bonusLabel}"
                : "";
    }

    private void ResolveElements()
    {
        if (mainPointsText == null)
        {
            Transform t = FindChildRecursive(transform, "MainPointsText");
            if (t != null)
                mainPointsText = t.GetComponent<TMP_Text>();
        }

        if (bonusPointsText == null)
        {
            Transform t = FindChildRecursive(transform, "BonusPointsText");
            if (t != null)
                bonusPointsText = t.GetComponent<TMP_Text>();
        }
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
