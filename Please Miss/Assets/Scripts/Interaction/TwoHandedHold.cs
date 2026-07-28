using UnityEngine;

public class TwoHandedHold : MonoBehaviour
{
    [SerializeField] private Transform leftGrip;

    public Transform LeftGrip => leftGrip != null ? leftGrip : transform;
    public bool IsValid => leftGrip != null;
}
