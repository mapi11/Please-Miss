using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyPlayerCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Dropdown roleDropdown;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private GameObject cardBlock;
    [SerializeField] private Image outlineImage;
    [SerializeField] private Image outlineReady;

    [Header("Card Colors")]
    [SerializeField] private Color localCardColor = new Color32(0x20, 0x96, 0xF3, 0xFF);
    [SerializeField] private Color otherCardColor = new Color32(0x2E, 0x2E, 0x2E, 0xFF);
    [SerializeField] private Color readyColor = new Color32(0x1E, 0xCC, 0x00, 0xFF);
    [SerializeField] private Color notReadyColor = new Color32(0xCC, 0x04, 0x00, 0xFF);

    [Header("Role Icons")]
    [SerializeField] private Image roleIcon;
    [SerializeField] private Sprite runnerIcon;
    [SerializeField] private Sprite sniperIcon;

    private ulong clientId;
    private bool isLocalCard;

    private NetworkPlayerRole roleComponent;
    private NetworkPlayerName nameComponent;
    private NetworkPlayerColor colorComponent;

    private static int RoleToDropdown(PlayerRole role)
    {
        if (role == PlayerRole.None) return 0;
        return (int)role - 1;
    }

    private static PlayerRole DropdownToRole(int index)
    {
        return (PlayerRole)(index + 1);
    }

    public void Setup(ulong id, NetworkObject obj, bool isLocal)
    {
        clientId = id;
        isLocalCard = isLocal;

        roleComponent = obj.GetComponent<NetworkPlayerRole>();
        nameComponent = obj.GetComponent<NetworkPlayerName>();
        colorComponent = obj.GetComponent<NetworkPlayerColor>();

        if (roleDropdown != null)
        {
            roleDropdown.ClearOptions();
            roleDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                PlayerRole.Runner.ToString(),
                PlayerRole.Sniper.ToString()
            });

            roleDropdown.onValueChanged.RemoveAllListeners();
            roleDropdown.interactable = isLocalCard;

            PlayerRole currentRole = roleComponent != null ? roleComponent.CurrentRole : PlayerRole.None;
            roleDropdown.SetValueWithoutNotify(RoleToDropdown(currentRole));

            if (isLocalCard)
            {
                if (currentRole == PlayerRole.None)
                    roleComponent.RequestSetRole(PlayerRole.Runner);

                roleDropdown.onValueChanged.AddListener(OnRoleChanged);
            }
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.interactable = isLocalCard;
            if (isLocalCard)
                readyButton.onClick.AddListener(OnReadyClicked);
        }

        if (roleComponent != null)
        {
            roleComponent.OnRoleChanged += OnRoleUpdated;
            roleComponent.OnReadyChanged += OnReadyUpdated;
        }

        if (nameComponent != null)
            nameComponent.OnNameUpdated += OnNameUpdated;

        if (colorComponent != null)
            colorComponent.OnColorUpdated += OnColorUpdated;

        if (cardBlock != null)
            cardBlock.SetActive(!isLocalCard);

        if (outlineImage != null)
            outlineImage.color = isLocalCard ? localCardColor : otherCardColor;

        if (!isLocalCard)
        {
            if (roleDropdown != null)
            {
                var et = roleDropdown.GetComponent<EventTrigger>();
                if (et != null) et.enabled = false;
            }

            if (readyButton != null)
            {
                var et = readyButton.GetComponent<EventTrigger>();
                if (et != null) et.enabled = false;
            }
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (roleComponent != null)
        {
            roleComponent.OnRoleChanged -= OnRoleUpdated;
            roleComponent.OnReadyChanged -= OnReadyUpdated;
        }

        if (nameComponent != null)
            nameComponent.OnNameUpdated -= OnNameUpdated;

        if (colorComponent != null)
            colorComponent.OnColorUpdated -= OnColorUpdated;
    }

    public void Refresh()
    {
        if (nameText != null && nameComponent != null)
            nameText.text = nameComponent.CurrentName;

        if (colorImage != null && colorComponent != null)
            colorImage.color = colorComponent.CurrentColor;

        if (roleDropdown != null && !isLocalCard && roleComponent != null)
            roleDropdown.SetValueWithoutNotify(RoleToDropdown(roleComponent.CurrentRole));

        if (readyButtonText != null && roleComponent != null)
        {
            readyButtonText.text = roleComponent.IsReady ? "Ready" : "Not Ready";
        }

        if (outlineReady != null && roleComponent != null)
        {
            outlineReady.color = roleComponent.IsReady ? readyColor : notReadyColor;
        }

        UpdateRoleIcon();
    }

    private void UpdateRoleIcon()
    {
        if (roleIcon == null || roleComponent == null) return;

        switch (roleComponent.CurrentRole)
        {
            case PlayerRole.Runner:
                roleIcon.sprite = runnerIcon;
                roleIcon.enabled = true;
                break;
            case PlayerRole.Sniper:
                roleIcon.sprite = sniperIcon;
                roleIcon.enabled = true;
                break;
            default:
                roleIcon.enabled = false;
                break;
        }
    }

    private void OnRoleUpdated(PlayerRole oldRole, PlayerRole newRole)
    {
        if (!isLocalCard && roleDropdown != null)
            roleDropdown.SetValueWithoutNotify(RoleToDropdown(newRole));

        UpdateRoleIcon();
    }

    private void OnReadyUpdated(bool oldReady, bool newReady)
    {
        Refresh();
    }

    private void OnNameUpdated(string newName)
    {
        if (nameText != null)
            nameText.text = newName;
    }

    private void OnColorUpdated(Color32 newColor)
    {
        if (colorImage != null)
            colorImage.color = newColor;
    }

    private void OnRoleChanged(int index)
    {
        PlayerRole role = DropdownToRole(index);

        if (roleComponent != null)
            roleComponent.RequestSetRole(role);
    }

    private void OnReadyClicked()
    {
        if (roleComponent != null)
            roleComponent.RequestToggleReady();
    }
}
