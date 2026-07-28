using UnityEngine;

[CreateAssetMenu(fileName = "Rifle_Default", menuName = "Game/Weapons/Sniper Rifle Definition")]
public sealed class SniperRifleDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string rifleId = "rifle_default";
    [SerializeField] private string displayName = "Sniper Rifle";

    [Header("Magazine")]
    [Min(1)] [SerializeField] private int magazineSize = 4;
    [SerializeField] private BulletDefinition defaultBullet;

    [Header("Shot")]
    [Min(0.01f)] [SerializeField] private float muzzleVelocity = 10f;
    [Min(0.01f)] [SerializeField] private float secondsBetweenShots = 1f;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Scope")]
    [Min(1f)] [SerializeField] private float minimumMagnification = 2f;
    [Min(1f)] [SerializeField] private float maximumMagnification = 8f;
    [Min(0.1f)] [SerializeField] private float zoomStep = 1f;

    public string RifleId => rifleId;
    public string DisplayName => displayName;
    public int MagazineSize => magazineSize;
    public BulletDefinition DefaultBullet => defaultBullet;
    public float MuzzleVelocity => muzzleVelocity;
    public float SecondsBetweenShots => secondsBetweenShots;
    public GameObject ProjectilePrefab => projectilePrefab;
    public float MinimumMagnification => minimumMagnification;
    public float MaximumMagnification => maximumMagnification;
    public float ZoomStep => zoomStep;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(rifleId))
            rifleId = name;

        magazineSize = Mathf.Max(1, magazineSize);
        muzzleVelocity = Mathf.Max(0.01f, muzzleVelocity);
        secondsBetweenShots = Mathf.Max(0.01f, secondsBetweenShots);
        minimumMagnification = Mathf.Max(1f, minimumMagnification);
        maximumMagnification = Mathf.Max(minimumMagnification, maximumMagnification);
        zoomStep = Mathf.Max(0.1f, zoomStep);
    }
}
