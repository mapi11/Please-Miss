using UnityEngine;

[CreateAssetMenu(fileName = "Rifle_Default", menuName = "Game/Weapons/Sniper Rifle Definition")]
public sealed class SniperRifleDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Внутренний ID винтовки (должен быть уникальным)")]
    [SerializeField] private string rifleId = "rifle_default";
    [Tooltip("Название, отображаемое в UI")]
    [SerializeField] private string displayName = "Sniper Rifle";
    [Tooltip("Описание винтовки, показывается в панели информации в меню инвентаря")]
    [SerializeField] private string description;

    [Header("Magazine")]
    [Tooltip("Количество патронов в обойме")]
    [Min(1)] [SerializeField] private int magazineSize = 4;
    [Tooltip("Тип пули по умолчанию (скорость, цвет, урон)")]
    [SerializeField] private BulletDefinition defaultBullet;

    [Header("Shot")]
    [Tooltip("Начальная скорость пули (м/с). Умножается на SpeedMultiplier пули")]
    [Min(0.01f)] [SerializeField] private float muzzleVelocity = 10f;
    [Tooltip("Задержка между выстрелами (сек)")]
    [Min(0.01f)] [SerializeField] private float secondsBetweenShots = 1f;
    [Tooltip("Префаб снаряда с NetworkProjectile")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Scope")]
    [Tooltip("Минимальное увеличение прицела")]
    [Min(1f)] [SerializeField] private float minimumMagnification = 2f;
    [Tooltip("Максимальное увеличение прицела")]
    [Min(1f)] [SerializeField] private float maximumMagnification = 8f;
    [Tooltip("Шаг зума (колёсико мыши)")]
    [Min(0.1f)] [SerializeField] private float zoomStep = 1f;

    [Header("Scope Sway")]
    [Tooltip("Амплитуда покачивания прицела")]
    [SerializeField] private float swayAmplitude = 0.1f;
    [Tooltip("Частота покачивания прицела")]
    [SerializeField] private float swayFrequency = 0.5f;
    [Tooltip("Время сглаживания покачивания")]
    [SerializeField] private float swaySmoothTime = 0.3f;

    [Header("Breath Hold")]
    [Tooltip("Максимальная задержка дыхания (сек)")]
    [SerializeField] private float maxBreath = 5f;
    [Tooltip("Скорость расхода дыхания (ед/сек)")]
    [SerializeField] private float breathDepletionRate = 1f;
    [Tooltip("Скорость восстановления дыхания (ед/сек)")]
    [SerializeField] private float breathRecoveryRate = 0.5f;
    [Tooltip("Задержка перед началом восстановления (сек)")]
    [SerializeField] private float breathRecoveryDelay = 1f;
    [Tooltip("Порог восстановления (0-1). Ниже него дыхание не восстанавливается")]
    [SerializeField] [Range(0f, 1f)] private float breathRecoveryThreshold = 0.3f;
    [Tooltip("Задержка перед штрафом при недостатке дыхания (сек)")]
    [SerializeField] private float breathPunishmentDelay = 1f;
    [Tooltip("Множитель дрожи при пустом дыхании")]
    [SerializeField] private float breathPunishmentMultiplier = 3f;
    [Tooltip("Множитель стабильности при полном дыхании (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float breathStabilityMultiplier = 0.05f;

    [Header("Death")]
    [Tooltip("Момент вращения при смерти (вокруг правой оси персонажа)")]
    [SerializeField] private float deathTorque = 300f;

    [Header("Recoil")]
    [Tooltip("Сила отдачи по вертикали")]
    [SerializeField] private float recoilPitchAmount = 0.15f;
    [Tooltip("Скорость возврата прицела после отдачи")]
    [SerializeField] private float recoilRecoverySpeed = 3f;

    [Header("Sound Pack")]
    [Tooltip("Пак звуков этой винтовки (выстрел, затвор, прицел, зум, дыхание)")]
    [SerializeField] private SniperSoundPack soundPack = new SniperSoundPack();

    public string RifleId => rifleId;
    public string DisplayName => displayName;
    public string Description => description;
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
    public float DeathTorque => deathTorque;
    public float RecoilPitchAmount => recoilPitchAmount;
    public float RecoilRecoverySpeed => recoilRecoverySpeed;
    public SniperSoundPack SoundPack => soundPack;

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
