using System.Collections;
using UnityEngine;

public class DeathController : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float fallDuration = 0.8f;
    [SerializeField] private float fallAngleX = 90f;
    [SerializeField] private float freezeDelay = 2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController characterController;
    private PlayerHealth playerHealth;
    private bool isDead;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
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
        if (!playerHealth.IsOwner) return;

        isDead = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (characterController != null)
            characterController.enabled = false;

        yield return StartCoroutine(WaitUntilGrounded());

        Quaternion startRot = visualRoot.localRotation;
        Quaternion endRot = Quaternion.Euler(fallAngleX, 0f, 0f);
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            visualRoot.localRotation = Quaternion.Slerp(startRot, endRot, elapsed / fallDuration);
            yield return null;
        }

        visualRoot.localRotation = endRot;

        yield return new WaitForSeconds(freezeDelay);

        var rb = visualRoot.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;
    }

    private IEnumerator WaitUntilGrounded()
    {
        float waitTimeout = 5f;
        float elapsed = 0f;

        while (elapsed < waitTimeout)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f, groundMask))
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
