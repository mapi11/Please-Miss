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

    private enum SfxId : byte
    {
        Dash,
        Damage,
        Landing,
        Shove,
        Footstep,
        Throw
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
        PlayRandom(jumpClips);
    }

    public void PlayCrouch()
    {
        PlayRandom(crouchClips);
    }

    public void PlayPickup()
    {
        PlayRandom(pickupClips);
    }

    public void PlayDash()
    {
        PlayReplicated(SfxId.Dash, dashClips);
    }

    public void PlayDamage()
    {
        PlayReplicated(SfxId.Damage, damageClips);
    }

    public void PlayLanding()
    {
        PlayReplicated(SfxId.Landing, landingClips);
    }

    public void PlayShove()
    {
        PlayReplicated(SfxId.Shove, shoveClips);
    }

    public void PlayDeath()
    {
        PlayRandom(deathClips);
    }

    public void PlayFootstep()
    {
        PlayReplicated(SfxId.Footstep, footstepClips);
    }

    public void PlayThrow()
    {
        PlayReplicated(SfxId.Throw, throwClips);
    }

    public void PlayOneShot(AudioClip[] clips)
    {
        PlayRandom(clips);
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
