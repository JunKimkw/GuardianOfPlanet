using UnityEngine;

// 궤도: 우주선 주위를 돌며(MoveInCircle) 닿는 적을 계속 공격
public class Orbital : ContactWeapon
{
    public static Orbital instance;

    [HideInInspector] public int OribitalDamage;

    public bool extraRange;
    public float ExtraStunPercent;   // % 단위 (15 = +15%)
    public bool extraSpeed;
    public bool hasScaled;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.CodeA;
    public override int BaseDamage => OribitalDamage;
    protected override float HitInterval => 0.15f;
    protected override bool RollHitStun() => RollStun(ExtraStunPercent);

    void Awake() => instance = this;

    void Update() => doubleCheckEnemyOnLazer();

    public override void SetDamage() =>
        OribitalDamage = Tiered(UpgradeLevel, 100, (0, 7), (50, 8), (100, 6), (200, 7), (300, 6), (500, 2));
}
