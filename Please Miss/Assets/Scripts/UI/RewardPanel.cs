using TMPro;
using UnityEngine;

public class RewardPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Текст действия, например \"Kill\", \"Finish\", \"Heal\"")]
    [SerializeField] private TMP_Text actionText;
    [Tooltip("Полученные очки, например \"+50\"")]
    [SerializeField] private TMP_Text mainPointsText;
    [Tooltip("Бонусные очки, например \"+10\". Скрывается, если бонуса нет")]
    [SerializeField] private TMP_Text bonusPointsText;

    public void Setup(string actionLabel, int mainPoints, int bonusPoints)
    {
        ResolveElements();

        if (actionText != null)
            actionText.text = string.IsNullOrWhiteSpace(actionLabel) ? "Reward" : actionLabel;

        if (mainPointsText != null)
            mainPointsText.text = mainPoints > 0 ? $"+{mainPoints}" : "";

        if (bonusPointsText != null)
            bonusPointsText.text = bonusPoints > 0 ? $"+{bonusPoints}" : "";
    }

    private void ResolveElements()
    {
        if (actionText == null)
        {
            Transform t = FindChildRecursive(transform, "ActionText");
            if (t != null)
                actionText = t.GetComponent<TMP_Text>();
        }

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
