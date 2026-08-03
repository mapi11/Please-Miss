using Unity.Netcode;
using UnityEngine;

public class EyeTracker : MonoBehaviour
{
    [Header("Eyes")]
    [SerializeField] private Transform leftEyeCenter;
    [SerializeField] private Transform rightEyeCenter;
    [SerializeField] private Transform leftPupil;
    [SerializeField] private Transform rightPupil;
    [Tooltip("Максимальное смещение зрачка от центра глаза (в мировых единицах)")]
    [SerializeField] private float maxPupilOffset = 0.05f;
    [SerializeField] private float eyeTrackingSpeed = 8f;

    [Header("Tracking")]
    [Tooltip("Дистанция, в пределах которой глаза следят за ближайшим игроком")]
    [SerializeField] private float trackingDistance = 10f;

    private void Update()
    {
        if (leftEyeCenter == null || rightEyeCenter == null || leftPupil == null || rightPupil == null)
            return;

        Transform target = FindNearestPlayer();

        MovePupil(leftPupil, leftEyeCenter, target);
        MovePupil(rightPupil, rightEyeCenter, target);
    }

    private Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return null;

        NetworkObject self = GetComponent<NetworkObject>();
        ulong selfOwner = self != null ? self.OwnerClientId : ulong.MaxValue;

        Transform nearest = null;
        float nearestDist = trackingDistance;

        // все игроки, включая мёртвых
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            if (self != null && client.PlayerObject == self)
                continue;

            if (client.PlayerObject.OwnerClientId == selfOwner)
                continue;

            float dist = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
            if (dist <= nearestDist)
            {
                nearestDist = dist;
                nearest = client.PlayerObject.transform;
            }
        }

        // спектаторы (летающие головы)
        foreach (var spect in FindObjectsByType<SpectatorController>(FindObjectsSortMode.None))
        {
            NetworkObject spectNet = spect.GetComponent<NetworkObject>();
            if (spectNet == null)
                continue;

            if (spectNet == self)
                continue;

            if (spectNet.OwnerClientId == selfOwner)
                continue;

            float dist = Vector3.Distance(transform.position, spectNet.transform.position);
            if (dist <= nearestDist)
            {
                nearestDist = dist;
                nearest = spectNet.transform;
            }
        }

        return nearest;
    }

    private void MovePupil(Transform pupil, Transform eyeCenter, Transform target)
    {
        if (target == null)
            return;

        Vector3 toTarget = target.position - eyeCenter.position;
        Vector3 localOffset = eyeCenter.InverseTransformDirection(toTarget);
        localOffset.z = 0f;

        if (localOffset.magnitude > maxPupilOffset)
            localOffset = localOffset.normalized * maxPupilOffset;

        Vector3 targetPos = eyeCenter.position + eyeCenter.TransformDirection(localOffset);
        pupil.position = Vector3.Lerp(pupil.position, targetPos, eyeTrackingSpeed * Time.deltaTime);
    }
}
