using UnityEngine;

public sealed class SniperRifleHeldVisual : MonoBehaviour
{
    [Header("Rifle")]
    [SerializeField] private SniperRifleDefinition definition;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform laserOrigin;

    [Header("Laser")]
    [SerializeField] private LineRenderer laserLine;

    [Header("Laser Dot")]
    [SerializeField] private Transform laserDot;
    [SerializeField] private Color dotColor = new Color(1f, 0f, 0f, 0.5f);

    private bool laserStarted;

    public SniperRifleDefinition Definition => definition;
    public Transform Muzzle => muzzle != null ? muzzle : transform;
    public Transform LaserOrigin => laserOrigin != null ? laserOrigin : Muzzle;

    private void Awake()
    {
        SetLaser(false, LaserOrigin.position);
    }

    public void SetLaser(bool visible, Vector3 endPoint)
    {
        if (laserLine == null)
            return;

        if (!laserStarted)
        {
            laserLine.useWorldSpace = true;
            laserLine.positionCount = 2;
            laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLine.receiveShadows = false;
            laserStarted = true;
        }

        laserLine.enabled = visible;

        if (laserDot != null)
        {
            laserDot.gameObject.SetActive(visible);
            if (visible)
            {
                laserDot.position = endPoint;

                var renderer = laserDot.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.material.color = dotColor;
            }
        }

        if (!visible)
            return;

        laserLine.SetPosition(0, LaserOrigin.position);
        laserLine.SetPosition(1, endPoint);
    }
}
