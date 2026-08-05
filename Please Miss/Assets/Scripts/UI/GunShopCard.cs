using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GunShopCard : MonoBehaviour
{
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameTxt;
    [SerializeField] private TMP_Text magazineText;
    [SerializeField] private TMP_Text velocityText;
    [SerializeField] private TMP_Text swayText;
    [SerializeField] private TMP_Text scopeText;
    [SerializeField] private TMP_Text recoilText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    [Tooltip("Optional. If null, resolved from SniperRifleHeldVisual.Definition")]
    [SerializeField] private SniperRifleDefinition definition;

    [Header("Purchased")]
    [SerializeField] private Color purchasedButtonColor = new Color(0.25f, 0.55f, 0.3f, 1f);

    private SniperRifleHeldVisual heldVisual;
    private Action onBuy;
    private int price;
    private Color defaultButtonColor;
    private bool hasDefaultButtonColor;

    public void Setup(SniperRifleHeldVisual held, Sprite icon, Action onBuy, int price = -1)
    {
        heldVisual = held;
        this.onBuy = onBuy;
        this.price = Mathf.Max(0, price);

        if (heldVisual == null)
            return;

        if (definition == null)
            definition = heldVisual.Definition;

        if (iconImg != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = icon;
            iconImg.color = icon != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (nameTxt != null)
            nameTxt.text = definition != null && !string.IsNullOrEmpty(definition.DisplayName)
                ? definition.DisplayName
                : heldVisual.name;

        SetStats(BuildStatsLines(definition));

        Image buttonImage = buyButton != null ? buyButton.image : null;
        if (buttonImage != null)
        {
            defaultButtonColor = buttonImage.color;
            hasDefaultButtonColor = true;
        }

        if (buyButton != null)
            buyButton.onClick.RemoveAllListeners();

        ApplyState();
    }

    public void OnPlayerPointsChanged()
    {
        ApplyState();
    }

    private void OnEnable()
    {
        Image buttonImage = buyButton != null ? buyButton.image : null;
        if (buttonImage != null && !hasDefaultButtonColor)
        {
            defaultButtonColor = buttonImage.color;
            hasDefaultButtonColor = true;
        }

        ApplyState();
    }

    private void ApplyState()
    {
        if (buyButton == null)
            return;

        string rifleId = definition != null ? definition.RifleId : (heldVisual != null ? heldVisual.name : "");
        bool owned = !string.IsNullOrEmpty(rifleId) && LocalPlayerSettings.IsSniperRifleOwned(rifleId);

        if (owned)
        {
            buyButton.interactable = false;

            if (buyButton.image != null)
                buyButton.image.color = purchasedButtonColor;

            if (buyButtonText != null)
                buyButtonText.text = "Purchased";

            return;
        }

        if (buyButton.image != null && hasDefaultButtonColor)
            buyButton.image.color = defaultButtonColor;

        buyButton.interactable = LocalPlayerSettings.PlayerPoints >= price;

        if (buyButtonText != null)
            buyButtonText.text = price > 0 ? $"Buy - {price}" : "Buy";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke());
    }

    private string[] BuildStatsLines(SniperRifleDefinition d)
    {
        if (d == null)
            return new[] { "Stats unavailable" };

        return new[]
        {
            $"Magazine: {d.MagazineSize}",
            $"Muzzle velocity: {d.MuzzleVelocity:0.##}",
            $"Sway amplitude: {d.SwayAmplitude:0.##}",
            $"Scope {d.MinimumMagnification:0.#}-{d.MaximumMagnification:0.#}",
            $"Recoil pitch: {d.RecoilPitchAmount:0.##}"
        };
    }

    private void SetStats(string[] lines)
    {
        AssignStat(magazineText, 0, lines);
        AssignStat(velocityText, 1, lines);
        AssignStat(swayText, 2, lines);
        AssignStat(scopeText, 3, lines);
        AssignStat(recoilText, 4, lines);
    }

    private static void AssignStat(TMP_Text text, int index, string[] lines)
    {
        if (text == null)
            return;

        bool hasLine = index < lines.Length;
        text.gameObject.SetActive(hasLine);

        if (hasLine)
            text.text = lines[index];
    }
}
