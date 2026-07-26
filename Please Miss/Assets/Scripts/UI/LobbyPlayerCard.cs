using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Dropdown roleDropdown;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;

    [Header("Config")]
    [SerializeField] private LobbyRoleConfig roleConfig;

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
            if (roleConfig != null)
                roleConfig.PopulateDropdown(roleDropdown);

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
            if (roleComponent.IsReady)
            {
                readyButtonText.text = "Ready";
                readyButtonText.color = Color.green;
            }
            else
            {
                readyButtonText.text = "Not Ready";
                readyButtonText.color = Color.red;
            }
        }
    }

    private void OnRoleUpdated(PlayerRole oldRole, PlayerRole newRole)
    {
        if (!isLocalCard && roleDropdown != null)
            roleDropdown.SetValueWithoutNotify(RoleToDropdown(newRole));
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

        if (role == PlayerRole.Sniper && IsSniperTaken())
        {
            role = PlayerRole.Runner;
            roleDropdown.SetValueWithoutNotify(RoleToDropdown(role));
        }

        if (roleComponent != null)
            roleComponent.RequestSetRole(role);
    }

    private bool IsSniperTaken()
    {
        if (NetworkManager.Singleton == null) return false;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == clientId) continue;
            if (client.PlayerObject == null) continue;

            var other = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (other != null && other.CurrentRole == PlayerRole.Sniper)
                return true;
        }

        return false;
    }

    private void OnReadyClicked()
    {
        if (roleComponent != null)
            roleComponent.RequestToggleReady();
    }
}
