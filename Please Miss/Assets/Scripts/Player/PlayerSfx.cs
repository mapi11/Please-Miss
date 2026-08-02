using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSfx : NetworkBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sound Arrays")]
    [SerializeField] private AudioClip[] jumpClips;
    [SerializeField] private AudioClip[] crouchClips;
    [SerializeField] private AudioClip[] dashClips;
    [SerializeField] private AudioClip[] pickupClips;
    [SerializeField] private AudioClip[] damageClips;
    [SerializeField] private AudioClip[] landingClips;
    [SerializeField] private AudioClip[] shoveClips;
    [SerializeField] private AudioClip[] deathClips;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioClip[] throwClips;

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.1f;

    [Header("Who Hears (кто слышит)")]
    [Tooltip("Слышат ли другие игроки (если выкл — только сам игрок)")]
    [SerializeField] private bool replicateJump;
    [SerializeField] private bool replicateCrouch;
    [SerializeField] private bool replicatePickup;
    [SerializeField] private bool replicateDash = true;
    [SerializeField] private bool replicateDamage = true;
    [SerializeField] private bool replicateLanding = true;
    [SerializeField] private bool replicateShove = true;
    [SerializeField] private bool replicateFootstep = true;
    [SerializeField] private bool replicateThrow = true;
    [Tooltip("Смерть всегда слышат все — звук играет на каждой машине при синхронизации смерти")]
    [SerializeField] private bool replicateDeath;

    private enum SfxId : byte
    {
        Dash,
        Damage,
        Landing,
        Shove,
        Footstep,
        Throw,
        Jump,
        Crouch,
        Pickup
    }

    private ulong lastSfxSender;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    public void PlayJump()
    {
        Play(SfxId.Jump, jumpClips);
    }

    public void PlayCrouch()
    {
        Play(SfxId.Crouch, crouchClips);
    }

    public void PlayPickup()
    {
        Play(SfxId.Pickup, pickupClips);
    }

    public void PlayDash()
    {
        Play(SfxId.Dash, dashClips);
    }

    public void PlayDamage()
    {
        Play(SfxId.Damage, damageClips);
    }

    public void PlayLanding()
    {
        Play(SfxId.Landing, landingClips);
    }

    public void PlayShove()
    {
        Play(SfxId.Shove, shoveClips);
    }

    public void PlayDeath()
    {
        PlayRandom(deathClips);
    }

    public void PlayFootstep()
    {
        Play(SfxId.Footstep, footstepClips);
    }

    public void PlayThrow()
    {
        Play(SfxId.Throw, throwClips);
    }

    public void PlayOneShot(AudioClip[] clips)
    {
        PlayRandom(clips);
    }

    private void Play(SfxId id, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return;

        if (IsReplicated(id))
            PlayReplicated(id, clips);
        else
            PlayRandom(clips);
    }

    private bool IsReplicated(SfxId id)
    {
        switch (id)
        {
            case SfxId.Dash: return replicateDash;
            case SfxId.Damage: return replicateDamage;
            case SfxId.Landing: return replicateLanding;
            case SfxId.Shove: return replicateShove;
            case SfxId.Footstep: return replicateFootstep;
            case SfxId.Throw: return replicateThrow;
            case SfxId.Jump: return replicateJump;
            case SfxId.Crouch: return replicateCrouch;
            case SfxId.Pickup: return replicatePickup;
            default: return false;
        }
    }

    private void PlayReplicated(SfxId id, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return;

        if (!IsSpawned)
        {
            PlayRandom(clips);
            return;
        }

        if (IsOwner)
        {
            PlayRandom(clips);

            if (IsServer)
                BroadcastSfxToOthers(id, NetworkManager.Singleton.LocalClientId);
            else
                NotifySfxServerRpc(id);
        }
        else if (IsServer)
        {
            PlayRandom(clips);
            BroadcastSfxToOthers(id, lastSfxSender);
        }
        else
        {
            PlayRandom(clips);
        }
    }

    [Rpc(SendTo.Server)]
    private void NotifySfxServerRpc(SfxId id, RpcParams rpcParams = default)
    {
        lastSfxSender = rpcParams.Receive.SenderClientId;
        PlayReplicated(id, GetClips(id));
    }

    [ClientRpc]
    private void BroadcastSfxClientRpc(SfxId id, ClientRpcParams rpcParams = default)
    {
        PlayRandom(GetClips(id));
    }

    private void BroadcastSfxToOthers(SfxId id, ulong excludeClientId)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        List<ulong> targets = new List<ulong>();
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            if (clientId == excludeClientId || clientId == localClientId)
                continue;

            targets.Add(clientId);
        }

        if (targets.Count == 0)
            return;

        BroadcastSfxClientRpc(id, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = targets }
        });
    }

    private AudioClip[] GetClips(SfxId id)
    {
        switch (id)
        {
            case SfxId.Dash:
                return dashClips;
            case SfxId.Damage:
                return damageClips;
            case SfxId.Landing:
                return landingClips;
            case SfxId.Shove:
                return shoveClips;
            case SfxId.Footstep:
                return footstepClips;
            case SfxId.Throw:
                return throwClips;
            case SfxId.Jump:
                return jumpClips;
            case SfxId.Crouch:
                return crouchClips;
            case SfxId.Pickup:
                return pickupClips;
            default:
                return null;
        }
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        if (pitchVariation > 0f)
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        audioSource.PlayOneShot(clip, volume);
    }
}
