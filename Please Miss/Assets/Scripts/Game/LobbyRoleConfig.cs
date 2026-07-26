using TMPro;
using UnityEngine;

public class LobbyRoleConfig : MonoBehaviour
{
    [SerializeField] private string[] roleNames = { "Runner", "Sniper" };

    public void PopulateDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(roleNames));
    }
}
