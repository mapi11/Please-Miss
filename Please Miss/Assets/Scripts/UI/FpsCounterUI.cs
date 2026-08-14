using TMPro;
using UnityEngine;

public class FpsCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;

    private float deltaTime;
    private float nextUpdateTime;

    private void Update()
    {
        if (Time.unscaledTime < nextUpdateTime)
            return;

        nextUpdateTime = Time.unscaledTime + 0.5f;

        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        if (fpsText != null)
            fpsText.text = Mathf.RoundToInt(1f / deltaTime) + " FPS";
    }
}
