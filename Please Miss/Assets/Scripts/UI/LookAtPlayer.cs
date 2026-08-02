using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetOverride;

    [Header("Billboard")]
    [SerializeField] private bool keepUpright = true;

    private Camera cachedCamera;

    private void LateUpdate()
    {
        Transform target = ResolveTarget();

        if (target == null)
            return;

        Vector3 direction = transform.position - target.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (keepUpright)
            direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Transform ResolveTarget()
    {
        if (targetOverride != null)
            return targetOverride;

        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera.transform;

        if (Camera.main != null)
        {
            cachedCamera = Camera.main;
            return cachedCamera.transform;
        }

        return null;
    }
}
