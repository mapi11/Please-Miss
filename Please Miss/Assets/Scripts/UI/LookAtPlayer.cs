using Unity.Netcode;
using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetOverride;

    [Header("Billboard")]
    [SerializeField] private bool keepUpright = true;

    private Camera cachedCamera;
    private bool resolutionLogged;

    private void LateUpdate()
    {
        Transform target = ResolveTarget();

        if (target == null)
        {
            LogUnresolved();
            return;
        }

        if (!resolutionLogged)
        {
            resolutionLogged = true;
            Debug.Log($"[LookAtPlayer] '{name}' resolved target: '{target.name}' (path: {GetPath(target)})", this);
        }

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
        if (targetOverride != null && !IsSelfOrChild(targetOverride))
            return targetOverride;

        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera.transform;

        Camera camera = FindLocalCamera();
        if (camera != null)
        {
            cachedCamera = camera;
            return cachedCamera.transform;
        }

        return null;
    }

    private Camera FindLocalCamera()
    {
        if (NetworkManager.Singleton != null)
        {
            var playerObject = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObject != null)
            {
                Camera[] cameras = playerObject.GetComponentsInChildren<Camera>(true);
                foreach (Camera cam in cameras)
                {
                    if (cam.isActiveAndEnabled)
                        return cam;
                }

                if (cameras.Length > 0)
                    return cameras[0];
            }
        }

        if (Camera.main != null)
            return Camera.main;

        return Object.FindFirstObjectByType<Camera>();
    }

    private bool IsSelfOrChild(Transform candidate)
    {
        return candidate == transform || candidate.IsChildOf(transform);
    }

    private float nextLogTime;

    private void LogUnresolved()
    {
        if (Time.unscaledTime < nextLogTime)
            return;

        nextLogTime = Time.unscaledTime + 2f;

        string reason;
        if (NetworkManager.Singleton == null)
        {
            reason = "no NetworkManager in scene";
        }
        else
        {
            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient == null)
                reason = "no LocalClient yet";
            else if (localClient.PlayerObject == null)
                reason = "no PlayerObject yet (player not spawned)";
            else
                reason = "player has no camera AND no other camera found";
        }

        Debug.Log($"[LookAtPlayer] '{name}' cannot resolve target camera: {reason}", this);
    }

    private string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
