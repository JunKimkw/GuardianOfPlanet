using System.Collections;
using UnityEngine;

// 쌍발총: 총구 두 개가 각각 조준해서 쏴요
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
    public override int BaseDamage => Damage;

    Transform Barrel0 => transform.GetChild(0);
    Transform Barrel1 => transform.GetChild(1);

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 5, (0, 1), (50, 1), (100, 2), (200, 3), (300, 2), (500, 1));

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

    void Shoot(Transform point, Transform barrel, long dmg)
    {
        var b = TakeBullet<DoubleGunBullet>(PoolObjectTypeBullet.Double, point.position, barrel.rotation);
        b.setBulletStats(dmg, ExtraBulletSpeedScale);
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
        Barrel1.rotation = LookRotation(Barrel1.position, target);
    }
}
