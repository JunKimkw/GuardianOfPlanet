using UnityEngine;

// 기본총
public class MainWeapon : GunWeapon
{
    public static MainWeapon instance;

    [HideInInspector] public int Damage;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    public float ExtraCritChance;
    public float ExtraCritDamage;
    public bool diamondPerHit;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.MainWeapon;
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 0, (0, 1), (20, 2), (100, 3), (200, 4), (300, 3), (500, 1));

    public override void CreateBullet()
    {
        var b = TakeBullet<MainBullet>(PoolObjectTypeBullet.Main, ShootingPoint.position, transform.rotation, ExtraCritChance);
        b.setBulletStats(HitDamage, Player.instance.FinalCriticalDamage + ExtraCritDamage);
        b.gameObject.SetActive(true);
    }
}
