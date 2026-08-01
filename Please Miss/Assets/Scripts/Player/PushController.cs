using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PushController : NetworkBehaviour
{
    [Header("Shove")]
    [SerializeField] private float shoveRange = 1.7f;
    [SerializeField] private float shoveViewDot = 0.55f;
    [SerializeField] private float shoveCooldown = 20f;
    [SerializeField] private float shoveForce = 220f;
    [SerializeField] private float shoveUpForce = 60f;
    [SerializeField] private float shoveTorque = 250f;

    [Header("Knockdown")]
    [SerializeField] private float knockdownDuration = 1.6f;
    [SerializeField] private float standUpDuration = 0.45f;

    [Header("Hands Push")]
    [SerializeField] private float handPushDistance = 0.75f;
    [SerializeField] private float handPushDuration = 0.22f;

    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private PlayerRoleState roleState;
    private CharacterController characterController;
    private Stamina stamina;
    private StaminaUI staminaUI;
    private Hand leftHand;
    private Hand rightHand;

    private Rigidbody bodyRigidbody;
    private CapsuleCollider fallCollider;
    private bool runtimeRigidbody;
    private bool runtimeFallCollider;

    private readonly NetworkVariable<bool> knockedDown = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> shoveDirection = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> shoveReadyNetworkTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float shoveReadyTime;
    private bool knockdownRoutineRunning;

    public bool IsKnockedDown => knockedDown.Value;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();
        roleState = GetComponent<PlayerRoleState>();
        characterController = GetComponent<CharacterController>();
        stamina = GetComponent<Stamina>();
        staminaUI = GetComponentInChildren<StaminaUI>(true);

        if (leftHand == null || rightHand == null)
        {
            Hand[] allHands = GetComponentsInChildren<Hand>(true);
            foreach (Hand hand in allHands)
            {
                if (hand.name.Contains("Left"))
                    leftHand = hand;
                else if (hand.name.Contains("Right"))
                    rightHand = hand;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (staminaUI == null)
            staminaUI = GetComponentInChildren<StaminaUI>(true);

        knockedDown.OnValueChanged += OnKnockedDownChanged;
    }

    public override void OnNetworkDespawn()
    {
        knockedDown.OnValueChanged -= OnKnockedDownChanged;
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (knockedDown.Value) return;

        float remaining = Mathf.Max(
            shoveReadyNetworkTime.Value - (float)NetworkManager.Singleton.ServerTime.Time,
            shoveReadyTime - Time.time);

        if (staminaUI != null)
            staminaUI.UpdateShoveCooldown(remaining);
        if (remaining > 0f) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.gKey.wasPressedThisFrame)
        {
            if (stamina != null && !stamina.CanConsume(stamina.ShoveCost)) return;
            if (!TryFindShoveVictim(out _)) return;

            shoveReadyTime = Time.time + shoveCooldown;
            RequestShoveServerRpc();
        }
    }

    private bool TryFindShoveVictim(out PushController victim)
    {
        victim = null;
        float bestScore = -1f;

        foreach (var pair in NetworkManager.Singleton.ConnectedClients)
        {
            NetworkObject victimObject = pair.Value.PlayerObject;
            if (victimObject == null || victimObject == NetworkObject) continue;
            if (!victimObject.TryGetComponent(out PushController candidate)) continue;
            if (candidate.knockedDown.Value) continue;

            PlayerHealth candidateHealth = candidate.playerHealth;
            if (candidateHealth == null || candidateHealth.IsDead) continue;

            PlayerRoleState candidateRole = candidate.roleState;
            if (candidateRole == null || candidateRole.IsSniper) continue;

            Vector3 toVictim = victimObject.transform.position - transform.position;
            toVictim.y = 0f;
            float distance = toVictim.magnitude;
            if (distance > shoveRange || distance < 0.05f) continue;

            float dot = Vector3.Dot(transform.forward, toVictim.normalized);
            if (dot < shoveViewDot) continue;

            float score = dot / Mathf.Max(distance, 0.1f);
            if (score > bestScore)
            {
                bestScore = score;
                victim = candidate;
            }
        }

        return victim != null;
    }

    [ServerRpc]
    private void RequestShoveServerRpc()
    {
        float remaining = shoveReadyNetworkTime.Value - (float)NetworkManager.Singleton.ServerTime.Time;
        if (remaining > 0f) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (knockedDown.Value) return;

        if (!TryFindShoveVictim(out PushController bestVictim)) return;

        shoveReadyNetworkTime.Value = (float)NetworkManager.Singleton.ServerTime.Time + shoveCooldown;

        Vector3 shoveDir = bestVictim.transform.position - transform.position;
        shoveDir.y = 0f;
        shoveDir.Normalize();

        PlayShoveHandsClientRpc();
        bestVictim.ServerApplyKnockdown(shoveDir);
    }

    private void ServerApplyKnockdown(Vector3 shoveDir)
    {
        if (!IsServer || !IsSpawned) return;
        if (knockedDown.Value) return;

        shoveDirection.Value = shoveDir;
        knockedDown.Value = true;
    }

    private void OnKnockedDownChanged(bool previous, bool current)
    {
        if (!IsSpawned) return;

        if (current)
        {
            if (characterController != null)
                characterController.enabled = false;

            if (playerController != null)
                playerController.SetFrozen(true);

            SetHandsRagdoll(true);

            if (IsServer || IsOwner)
            {
                if (knockdownRoutineRunning) return;
                knockdownRoutineRunning = true;

                Vector3 dir = shoveDirection.Value;
                if (dir.sqrMagnitude < 0.001f)
                    dir = -transform.forward;

                StartCoroutine(KnockdownRoutine(dir));
            }
        }
    }

    private IEnumerator KnockdownRoutine(Vector3 shoveDir)
    {
        try
        {
            yield return KnockdownRoutineInner(shoveDir);
        }
        finally
        {
            knockdownRoutineRunning = false;
        }
    }

    private IEnumerator KnockdownRoutineInner(Vector3 shoveDir)
    {
        bodyRigidbody = GetComponent<Rigidbody>();
        if (bodyRigidbody == null)
        {
            bodyRigidbody = gameObject.AddComponent<Rigidbody>();
            runtimeRigidbody = true;
        }

        bodyRigidbody.mass = 80f;
        bodyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        bodyRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        bodyRigidbody.useGravity = true;
        bodyRigidbody.isKinematic = false;
        bodyRigidbody.linearDamping = 1.5f;
        bodyRigidbody.angularDamping = 1.5f;
        bodyRigidbody.linearVelocity = Vector3.zero;
        bodyRigidbody.angularVelocity = Vector3.zero;

        fallCollider = GetComponent<CapsuleCollider>();
        if (fallCollider == null)
        {
            fallCollider = gameObject.AddComponent<CapsuleCollider>();
            fallCollider.height = characterController != null ? characterController.height : 1.75f;
            fallCollider.radius = characterController != null ? characterController.radius : 0.32f;
            fallCollider.center = characterController != null
                ? characterController.center
                : new Vector3(0f, 0.9f, 0f);
            runtimeFallCollider = true;
        }

        IgnoreFallColliderWithHands();

        Vector3 axis = Vector3.Cross(Vector3.up, shoveDir).normalized;
        bodyRigidbody.AddForce(shoveDir * shoveForce + Vector3.up * shoveUpForce, ForceMode.Impulse);
        bodyRigidbody.AddTorque(axis * shoveTorque, ForceMode.Impulse);

        yield return new WaitForSeconds(knockdownDuration);

        if (bodyRigidbody != null)
        {
            bodyRigidbody.isKinematic = true;
            bodyRigidbody.useGravity = false;
            bodyRigidbody.linearVelocity = Vector3.zero;
            bodyRigidbody.angularVelocity = Vector3.zero;
        }

        float yaw = transform.eulerAngles.y;
        float standTimer = 0f;
        while (standTimer < standUpDuration)
        {
            standTimer += Time.deltaTime;
            float t = Mathf.Clamp01(standTimer / standUpDuration);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), t);
            yield return null;
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (runtimeFallCollider && fallCollider != null)
            Destroy(fallCollider);

        if (runtimeRigidbody && bodyRigidbody != null)
        {
            Destroy(bodyRigidbody);
        }
        else if (bodyRigidbody != null)
        {
            bodyRigidbody.isKinematic = true;
            bodyRigidbody.useGravity = false;
        }

        if (characterController != null)
            characterController.enabled = true;

        if (playerController != null)
            playerController.SetFrozen(false);

        SetHandsRagdoll(false);

        if (IsServer && IsSpawned)
            knockedDown.Value = false;
    }

    private void IgnoreFallColliderWithHands()
    {
        if (fallCollider == null) return;

        Hand[] allHands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
        for (int i = 0; i < allHands.Length; i++)
        {
            Collider[] handColliders = allHands[i].Colliders;
            if (handColliders == null || handColliders.Length == 0) continue;

            for (int h = 0; h < handColliders.Length; h++)
            {
                if (handColliders[h] == null || handColliders[h] == fallCollider) continue;
                Physics.IgnoreCollision(fallCollider, handColliders[h], true);
            }
        }
    }

    private void SetHandsRagdoll(bool enabled)
    {
        if (leftHand != null)
            leftHand.SetRagdollMode(enabled);

        if (rightHand != null)
            rightHand.SetRagdollMode(enabled);
    }

    [ClientRpc]
    private void PlayShoveHandsClientRpc()
    {
        if (!IsOwner) return;

        if (stamina != null)
            stamina.Consume(stamina.ShoveCost);

        if (leftHand == null && rightHand == null) return;

        StartCoroutine(PushHandsRoutine());
    }

    private IEnumerator PushHandsRoutine()
    {
        Transform leftTarget = CreatePushTarget(-0.3f);
        Transform rightTarget = CreatePushTarget(0.3f);

        if (leftHand != null)
            leftHand.SetTarget(leftTarget);

        if (rightHand != null)
            rightHand.SetTarget(rightTarget);

        yield return new WaitForSeconds(handPushDuration);

        if (leftHand != null)
            leftHand.ClearTarget();

        if (rightHand != null)
            rightHand.ClearTarget();

        Destroy(leftTarget.gameObject);
        Destroy(rightTarget.gameObject);
    }

    private Transform CreatePushTarget(float lateralOffset)
    {
        GameObject targetObject = new GameObject("ShoveHandTarget");
        Transform target = targetObject.transform;
        target.position = transform.position + transform.forward * handPushDistance
                         + Vector3.up * 1.1f + transform.right * lateralOffset;
        return target;
    }
}
