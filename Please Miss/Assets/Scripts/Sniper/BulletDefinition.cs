using UnityEngine;

public enum BulletSpecialProperty
{
    None,
    IgniteGround
}

[CreateAssetMenu(fileName = "Bullet_Standard", menuName = "Game/Weapons/Bullet Definition")]
public sealed class BulletDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string bulletId = "standard";
    [SerializeField] private string displayName = "Standard Bullet";
    [SerializeField] private Sprite uiIcon;

    [Header("Visual")]
    [SerializeField] private Color headColor = Color.white;

    [Header("Ballistics")]
    [Min(0f)] [SerializeField] private float damage = 25f;
    [Min(0.01f)] [SerializeField] private float speedMultiplier = 1f;
    [Min(0f)] [SerializeField] private float accelerationPerSecond;

    [Header("Future special property")]
    [SerializeField] private BulletSpecialProperty specialProperty = BulletSpecialProperty.None;
    [Min(0f)] [SerializeField] private float effectRadius;

    public string BulletId => bulletId;
    public string DisplayName => displayName;
    public Sprite UiIcon => uiIcon;
    public Color HeadColor => headColor;
    public float Damage => damage;
    public float SpeedMultiplier => speedMultiplier;
    public float AccelerationPerSecond => accelerationPerSecond;
    public BulletSpecialProperty SpecialProperty => specialProperty;
    public float EffectRadius => effectRadius;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(bulletId))
            bulletId = name;

        speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        damage = Mathf.Max(0f, damage);
        accelerationPerSecond = Mathf.Max(0f, accelerationPerSecond);
        effectRadius = Mathf.Max(0f, effectRadius);
    }
}
