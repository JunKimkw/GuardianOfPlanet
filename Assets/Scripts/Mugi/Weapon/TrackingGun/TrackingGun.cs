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
    public override SideWeapon Side => SideWeapon.TrackingGun;
    public override int BaseDamage => Balanced("track", 31.7f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Balanced("track", 31.7f);

    // (Lv.500 패시브 "공격 속도"는 예전엔 장전 계산에 안 들어가서 효과가 없었어요. 이제 GunWeapon 에서 반영돼요)
    // 무기 전용 "다탄두": 미사일이 부채꼴로 더 나가요
    protected override void Fire()
    {
        base.Fire();
        int l = AugmentManager.WL("tk_multi");
        int extra = l > 0 ? AugmentDefs.TkMulti(l) : 0;
        for(int i = 0; i < extra; i++) {
            float side = (i % 2 == 0 ? 1f : -1f) * (20f * (i / 2 + 1));
            CreateBullet(side);
        }
    }

    public override void CreateBullet() => CreateBullet(0f);

    void CreateBullet(float angle)
    {
        var b = TakeBullet<TrackingBullet>(PoolObjectTypeBullet.Tracking, ShootingPoint.position, transform.rotation * Quaternion.Euler(0f, 0f, angle));
        b.packSpawned = false;
        // 치명타 피해: 예전엔 패시브 값만 넘겨서 플레이어 치명타 피해가 빠져 있었어요
        b.setBulletStats(HitDamage, missileSpeedScale, Player.instance.FinalCriticalDamage + ExtraCritDamage);
        b.gameObject.SetActive(true);
    }
}
