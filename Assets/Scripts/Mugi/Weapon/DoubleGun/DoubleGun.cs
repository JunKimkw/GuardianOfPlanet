using System.Collections;
using UnityEngine;

// 쌍발총: 총구 두 개가 각각 조준해서 쏴요
// 증강: 쌍둥이 연사(공속), 탄 가속(탄속), 교차 사격(두 총구가 다른 적), 합체 "분열 쌍발"(DoubleGunBullet)
public class DoubleGun : GunWeapon
{
    public static DoubleGun instance;

    [HideInInspector] public int Damage;

    [Header("#-----Weapon Info")]
    public Transform ShootingPoint1;
    public Transform ShootingPoint2;

    public float ExtraBulletSpeedScale;
    public bool doubleAttack;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.DoubleGun;
    public override SideWeapon Side => SideWeapon.DoubleGun;
    public override int BaseDamage => Damage;

    Transform Barrel0 => transform.GetChild(0);
    Transform Barrel1 => transform.GetChild(1);

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 5, (0, 1), (50, 1), (100, 2), (200, 3), (300, 2), (500, 1));

    public override float getAttackSpeed()
    {
        int l = AugmentManager.WL("dg_rapid");
        return base.getAttackSpeed() / (l > 0 ? 1f + AugmentDefs.DgRapid(l) : 1f);
    }

    protected override void Fire()
    {
        base.Fire();
        if(doubleAttack) StartCoroutine(DelayedAttack());
    }

    // 패시브(Lv.500): 잠시 뒤 1% 확률로 한 번 더
    IEnumerator DelayedAttack()
    {
        yield return new WaitForSeconds(MaximumAttackSpeed / 2);
        if(Random.Range(0f, 100f) < 1f) CreateBullet();
    }

    public override void CreateBullet()
    {
        long dmg = HitDamage;
        // 예전엔 두 번째 탄의 피해를 첫 번째 탄에 다시 넣어서, 두 번째 탄은 지난번 피해(처음엔 0)로 날아갔어요
        Shoot(ShootingPoint1, Barrel1, dmg);
        Shoot(ShootingPoint2, Barrel0, dmg);
    }

    float SpeedScale {
        get {
            int l = AugmentManager.WL("dg_accel");
            return ExtraBulletSpeedScale + (l > 0 ? AugmentDefs.DgAccel(l) : 0f);
        }
    }

    void Shoot(Transform point, Transform barrel, long dmg)
    {
        var b = TakeBullet<DoubleGunBullet>(PoolObjectTypeBullet.Double, point.position, barrel.rotation);
        b.setBulletStats(dmg, SpeedScale);
        b.splitGen = 0;
        b.rootSpeedScale = b.bullet_BulletSpeedScale;   // 갈라진 탄들의 속도 기준
        b.ignore = null;
        b.SetSplitLook();
        b.gameObject.SetActive(true);
    }

    public override void NormalShooting()
    {
        AimAll(MouseWorld);
        IsShooting = true;
    }

    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        if(e == null) {
            Face(Vector3.zero);
            IsShooting = false;
        } else {
            AimAll(e.pos);
            IsShooting = true;
        }
    }

    void AimAll(Vector3 target)
    {
        Face(target);
        Barrel0.rotation = LookRotation(Barrel0.position, target);
        Vector3 second = target;
        // 교차 사격: 두 번째 총구는 첫 목표 다음으로 가까운 다른 적을 노려요
        if(AugmentManager.WL("dg_cross") > 0) {
            var near = AugmentManager.NearestInRange(target, 2);
            if(near.Count >= 2) second = near[1].pos;
        }
        Barrel1.rotation = LookRotation(Barrel1.position, second);
    }
}
