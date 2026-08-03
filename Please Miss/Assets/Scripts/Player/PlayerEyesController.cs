using Unity.Netcode;
using UnityEngine;

public class PlayerEyesController : MonoBehaviour
{
    [Header("Eye Roots")]
    [SerializeField] private Transform leftEyeRoot;
    [SerializeField] private Transform rightEyeRoot;

    [Header("Pupils To Move")]
    [SerializeField] private Transform leftPupil;
    [SerializeField] private Transform rightPupil;

    [Header("Reference")]
    [SerializeField] private Transform headReference;

    public Transform HeadReference => headReference;

    [Header("Look For Players")]
    [SerializeField] private float playerLookRadius = 3.0f;

    [Header("Pupil Movement")]
    [SerializeField] private float pupilMoveDistance = 0.075f;
    [SerializeField] private float horizontalMultiplier = 1.25f;
    [SerializeField] private float verticalMultiplier = 1.65f;
    [SerializeField] private float lookSmooth = 22f;

    [Header("Clamp")]
    [SerializeField] private float maxHorizontal = 1.0f;
    [SerializeField] private float maxVertical = 1.0f;

    [Header("Idle / Movement Bounce")]
    [SerializeField] private float idleAmount = 0.008f;
    [SerializeField] private float walkBounceAmount = 0.018f;
    [SerializeField] private float walkSideAmount = 0.018f;
    [SerializeField] private float jumpBounceAmount = 0.035f;
    [SerializeField] private float movementSpeedForMaxBounce = 5f;

    [Header("Debug")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private Vector2 currentPupilOffset;
    [SerializeField] private Vector3 debugTargetPoint;

    private Vector3 leftPupilStartLocalPosition;
    private Vector3 rightPupilStartLocalPosition;

    private Vector3 lastWorldPosition;
    private Vector3 velocity;

    private PlayerController ownPlayer;
    private PlayerController[] cachedPlayers;
    private SpectatorController[] cachedSpectators;
    private float searchTimer;

    public Vector3 EyesWorldCenter => GetOwnEyesCenter();

    private void Awake()
    {
        ownPlayer = GetComponentInParent<PlayerController>();

        if (headReference == null)
        {
            headReference = transform;
        }

        if (leftPupil != null)
        {
            leftPupilStartLocalPosition = leftPupil.localPosition;
        }

        if (rightPupil != null)
        {
            rightPupilStartLocalPosition = rightPupil.localPosition;
        }

        lastWorldPosition = transform.position;
    }

    private void Update()
    {
        UpdateVelocity();
        UpdatePlayersCache();

        currentTarget = FindNearestTarget();

        Vector2 targetOffset;

        if (currentTarget != null)
        {
            targetOffset = GetLookAtPlayerOffset(currentTarget);
        }
        else
        {
            targetOffset = GetMovementBounceOffset();
        }

        currentPupilOffset = Vector2.Lerp(
            currentPupilOffset,
            targetOffset,
            Time.deltaTime * lookSmooth
        );

        ApplyPupilOffset(currentPupilOffset);
    }

    private void UpdateVelocity()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        velocity = (transform.position - lastWorldPosition) / deltaTime;
        lastWorldPosition = transform.position;
    }

    private void UpdatePlayersCache()
    {
        searchTimer -= Time.deltaTime;

        if (searchTimer > 0f)
            return;

        searchTimer = 0.25f;
        cachedPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        cachedSpectators = FindObjectsByType<SpectatorController>(FindObjectsSortMode.None);
    }

    private Transform FindNearestTarget()
    {
        Vector3 myPosition = transform.position;

        Transform bestTarget = null;
        float bestDistance = playerLookRadius;

        if (cachedPlayers != null)
        {
            for (int i = 0; i < cachedPlayers.Length; i++)
            {
                PlayerController player = cachedPlayers[i];

                if (player == null)
                    continue;

                if (player == ownPlayer)
                    continue;

                if (!player.gameObject.activeInHierarchy)
                    continue;

                float distance = Vector3.Distance(myPosition, player.transform.position);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                bestTarget = player.transform;
            }
        }

        // летающие головы спектаторов — тоже цели
        if (cachedSpectators != null)
        {
            for (int i = 0; i < cachedSpectators.Length; i++)
            {
                SpectatorController spectator = cachedSpectators[i];

                if (spectator == null)
                    continue;

                if (!spectator.gameObject.activeInHierarchy)
                    continue;

                if (IsOwnSpectator(spectator))
                    continue;

                float distance = Vector3.Distance(myPosition, spectator.transform.position);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                bestTarget = spectator.transform;
            }
        }

        return bestTarget;
    }

    private bool IsOwnSpectator(SpectatorController spectator)
    {
        if (ownPlayer == null)
            return false;

        NetworkObject ownNet = ownPlayer.GetComponent<NetworkObject>();
        NetworkObject spectatorNet = spectator.GetComponent<NetworkObject>();

        if (ownNet == null || spectatorNet == null)
            return false;

        return ownNet.OwnerClientId == spectatorNet.OwnerClientId;
    }

    private Vector2 GetLookAtPlayerOffset(Transform targetPlayer)
    {
        Vector3 eyesCenter = GetOwnEyesCenter();
        Vector3 targetPoint = GetTargetEyesPoint(targetPlayer);

        debugTargetPoint = targetPoint;

        Vector3 directionWorld = targetPoint - eyesCenter;

        if (directionWorld.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        directionWorld.Normalize();

        Vector3 directionLocal = headReference.InverseTransformDirection(directionWorld);

        float x = Mathf.Clamp(directionLocal.x * horizontalMultiplier, -maxHorizontal, maxHorizontal);
        float y = Mathf.Clamp(directionLocal.y * verticalMultiplier, -maxVertical, maxVertical);

        return new Vector2(x, y) * pupilMoveDistance;
    }

    private Vector3 GetTargetEyesPoint(Transform target)
    {
        PlayerEyesController targetEyes = target.GetComponentInChildren<PlayerEyesController>();

        if (targetEyes != null)
        {
            return targetEyes.HeadReference != null ? targetEyes.HeadReference.position : targetEyes.EyesWorldCenter;
        }

        return target.position;
    }

    private Vector3 GetOwnEyesCenter()
    {
        if (leftPupil != null && rightPupil != null)
        {
            return (leftPupil.position + rightPupil.position) * 0.5f;
        }

        if (leftEyeRoot != null && rightEyeRoot != null)
        {
            return (leftEyeRoot.position + rightEyeRoot.position) * 0.5f;
        }

        if (leftPupil != null)
        {
            return leftPupil.position;
        }

        if (rightPupil != null)
        {
            return rightPupil.position;
        }

        return headReference != null ? headReference.position : transform.position;
    }

    private Vector2 GetMovementBounceOffset()
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;

        float speedRatio = Mathf.Clamp01(horizontalSpeed / movementSpeedForMaxBounce);

        Vector3 localVelocity = transform.InverseTransformDirection(horizontalVelocity);

        float sideOffset = 0f;

        if (horizontalSpeed > 0.05f)
        {
            sideOffset = -Mathf.Clamp(localVelocity.x, -1f, 1f) * walkSideAmount;
        }

        float walkBob = Mathf.Sin(Time.time * Mathf.Lerp(3f, 9f, speedRatio))
                        * walkBounceAmount
                        * speedRatio;

        float idleX = Mathf.Sin(Time.time * 1.7f) * idleAmount;
        float idleY = Mathf.Cos(Time.time * 1.3f) * idleAmount;

        float jumpBob = Mathf.Clamp(-velocity.y * 0.012f, -jumpBounceAmount, jumpBounceAmount);

        return new Vector2(
            sideOffset + idleX,
            walkBob + idleY + jumpBob
        );
    }

    private void ApplyPupilOffset(Vector2 offset)
    {
        Vector3 localOffset = new Vector3(offset.x, offset.y, 0f);

        // currentPupilOffset уже сглажен в Update — тут применяем напрямую
        if (leftPupil != null)
        {
            leftPupil.localPosition = leftPupilStartLocalPosition + localOffset;
        }

        if (rightPupil != null)
        {
            rightPupil.localPosition = rightPupilStartLocalPosition + localOffset;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, playerLookRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(debugTargetPoint, 0.06f);
    }
}