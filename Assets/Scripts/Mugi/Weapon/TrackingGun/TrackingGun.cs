using UnityEngine;

// 유도탄
public class TrackingGun : GunWeapon
{
    public static TrackingGun instance;

    [HideInInspector] public int Damage;

    [Header("#-----Weapon Info")]
    public Transform ShootingPoint;

    public float missileSpeedScale;
    public float ExtraCritDamage;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.TrackingMissile;
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 50, (0, 3), (10, 4), (50, 5), (100, 6), (200, 4), (300, 3), (500, 2));

    // (Lv.500 패시브 "공격 속도"는 예전엔 장전 계산에 안 들어가서 효과가 없었어요. 이제 GunWeapon 에서 반영돼요)
    public override void CreateBullet()
    {
        var b = TakeBullet<TrackingBullet>(PoolObjectTypeBullet.Tracking, ShootingPoint.position, transform.rotation);
        // 치명타 피해: 예전엔 패시브 값만 넘겨서 플레이어 치명타 피해가 빠져 있었어요
        b.setBulletStats(HitDamage, missileSpeedScale, Player.instance.FinalCriticalDamage + ExtraCritDamage);
        b.gameObject.SetActive(true);
    }
}
