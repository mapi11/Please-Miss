using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiedPlayerPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text zoneText;

    public void Setup(string playerName, Color32 color, float survivedTime, string hitZone)
    {
        ResolveElements();

        if (colorImage != null)
            colorImage.color = color;

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;

        if (timeText != null)
            timeText.text = "Died: " + FormatTime(survivedTime);

        if (zoneText != null)
            zoneText.text = "Hit: " + (string.IsNullOrWhiteSpace(hitZone) ? "None" : hitZone);
    }

    private void ResolveElements()
    {
        if (colorImage == null)
            colorImage = GetComponentInChildren<Image>(true);

        if (nameText == null)
        {
            Transform t = FindChildRecursive(transform, "NameText");
            if (t != null)
                nameText = t.GetComponent<TMP_Text>();
        }

        if (timeText == null)
        {
            Transform t = FindChildRecursive(transform, "TimeText");
            if (t != null)
                timeText = t.GetComponent<TMP_Text>();
        }

        if (zoneText == null)
        {
            Transform t = FindChildRecursive(transform, "ZoneText");
            if (t != null)
                zoneText = t.GetComponent<TMP_Text>();
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

    private static string FormatTime(float seconds)
    {
        int totalSec = Mathf.CeilToInt(seconds);
        int mins = totalSec / 60;
        int secs = totalSec % 60;
        return $"{mins}:{secs:D2}";
    }
}
