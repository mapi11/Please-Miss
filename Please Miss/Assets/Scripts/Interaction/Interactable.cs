using Unity.Netcode;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField] protected Transform handTarget;
    [SerializeField] private bool canInteract = true;

    [Header("Settings")]
    [SerializeField] protected bool cancelOnCanInteractFail = true;

    public bool CancelOnCanInteractFail => cancelOnCanInteractFail;

    public Transform HandTarget => handTarget != null ? handTarget : transform;

    public virtual bool CanInteract(PlayerController player)
    {
        return canInteract;
    }

    public virtual void OnHandBegin(PlayerController player) { }
    public virtual void OnHandHold(PlayerController player, float deltaTime) { }
    public virtual void OnHandEnd(PlayerController player) { }

    public virtual void OnServerInteractionBegin(ulong clientId) { }
    public virtual void OnServerInteractionEnd(ulong clientId) { }
    public virtual void OnLocalInteractionBegin(PlayerController player) { }
    public virtual void OnLocalInteractionEnd(PlayerController player) { }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }
}
