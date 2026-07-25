using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private PlayerRole role;

    public int Index => index;
    public PlayerRole Role => role;
}
