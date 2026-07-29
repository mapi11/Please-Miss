using System.Collections;
using UnityEngine;

public class DeathController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Collider leftHand;
    [SerializeField] private Collider rightHand;

    [SerializeField] private float fallTorque = 300f;
    [SerializeField] private float freezeDelay = 2f;

    private CharacterController characterController;
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

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 80f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.AddTorque(transform.right * -fallTorque, ForceMode.Impulse);

        StartCoroutine(FreezeAfterDelay(rb));
    }

    private IEnumerator FreezeAfterDelay(Rigidbody rb)
    {
        yield return new WaitForSeconds(freezeDelay);

        if (rb != null)
            rb.isKinematic = true;
    }
}
