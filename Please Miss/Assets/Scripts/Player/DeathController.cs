using System.Collections;
using UnityEngine;

public class DeathController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Collider leftHand;
    [SerializeField] private Collider rightHand;

    private const float FallbackTorque = 300f;
    [SerializeField] private float groundedTimeToFreeze = 4f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController characterController;
    private Rigidbody rb;
    private bool isDead;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDeathStateChanged += OnDeathChanged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDeathStateChanged -= OnDeathChanged;
    }

    private void OnDeathChanged(bool dead)
    {
        if (!dead || isDead) return;

        isDead = true;

        if (characterController != null)
        {
            characterController.enabled = false;
            characterController.height = 0;
            characterController.radius = 0;
        }

        if (playerController != null)
            playerController.enabled = false;

        if (leftHand != null)
            leftHand.enabled = false;

        if (rightHand != null)
            rightHand.enabled = false;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 80f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        float torque = playerHealth != null && playerHealth.LastDeathTorque > 0f
            ? playerHealth.LastDeathTorque
            : FallbackTorque;
        rb.AddTorque(transform.right * -torque, ForceMode.Impulse);

        StartCoroutine(FreezeWhenGrounded());
    }

    private IEnumerator FreezeWhenGrounded()
    {
        float groundedTimer = 0f;

        while (groundedTimer < groundedTimeToFreeze)
        {
            if (rb == null)
                yield break;

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask))
                groundedTimer += Time.deltaTime;
            else
                groundedTimer = 0f;

            yield return null;
        }

        if (rb != null)
            rb.isKinematic = true;
    }
}
