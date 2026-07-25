using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null && playerCamera != null)
            audioListener = playerCamera.GetComponent<AudioListener>();

        SetLocalState(false);
    }

    public override void OnNetworkSpawn()
    {
        SetLocalState(IsOwner);
    }

    private void SetLocalState(bool local)
    {
        if (characterController != null)
            characterController.enabled = local;

        if (playerCamera != null)
            playerCamera.enabled = local;

        if (audioListener != null)
            audioListener.enabled = local;
    }
}
