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

    [Header("Scope Sway")]
    [SerializeField] private float swayAmplitude = 0.1f;
    [SerializeField] private float swayFrequency = 0.5f;
    [SerializeField] private float swaySmoothTime = 0.3f;

    [Header("Breath Hold")]
    [SerializeField] private float maxBreath = 5f;
    [SerializeField] private float breathDepletionRate = 1f;
    [SerializeField] private float breathRecoveryRate = 0.5f;
    [SerializeField] private float breathRecoveryDelay = 1f;
    [SerializeField] [Range(0f, 1f)] private float breathRecoveryThreshold = 0.3f;
    [SerializeField] private float breathPunishmentDelay = 1f;
    [SerializeField] private float breathPunishmentMultiplier = 3f;
    [SerializeField] [Range(0f, 1f)] private float breathStabilityMultiplier = 0.05f;

    [Header("Recoil")]
    [SerializeField] private float recoilPitchAmount = 0.15f;
    [SerializeField] private float recoilRecoverySpeed = 3f;

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
    public float SwayAmplitude => swayAmplitude;
    public float SwayFrequency => swayFrequency;
    public float SwaySmoothTime => swaySmoothTime;
    public float MaxBreath => maxBreath;
    public float BreathDepletionRate => breathDepletionRate;
    public float BreathRecoveryRate => breathRecoveryRate;
    public float BreathRecoveryDelay => breathRecoveryDelay;
    public float BreathRecoveryThreshold => breathRecoveryThreshold;
    public float BreathPunishmentDelay => breathPunishmentDelay;
    public float BreathPunishmentMultiplier => breathPunishmentMultiplier;
    public float BreathStabilityMultiplier => breathStabilityMultiplier;
    public float RecoilPitchAmount => recoilPitchAmount;
    public float RecoilRecoverySpeed => recoilRecoverySpeed;

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
