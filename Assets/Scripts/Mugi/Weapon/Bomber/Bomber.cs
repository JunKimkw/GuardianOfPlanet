using UnityEngine;

// 폭격기: 적이 가장 많이 모인 곳으로 폭탄을 던져요
public class Bomber : GunWeapon
{
    public static Bomber instance;

    [HideInInspector] public int Damage;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;
    public Quaternion rotate;

    public bool ExploseRangeIncrease;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Bomber;
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 100, (0, 4), (20, 6), (100, 7), (200, 6), (300, 5), (500, 3));

    public override void CreateBullet()
    {
        int dmg = HitDamage;
        SpawnBomb(Vector3.zero, dmg);

        //리마스터: 융단 폭격 - 진행 방향과 수직으로 폭탄을 일렬 추가
        int extra = AugmentManager.Instance != null ? AugmentManager.Instance.CarpetBombCount : 0;
        Vector3 side = rotate * Vector3.right;
        for(int i = 0; i < extra; i++) {
            int step = i / 2 + 1;
            float sign = (i % 2 == 0) ? 1f : -1f;
            SpawnBomb(side * (0.7f * step * sign), dmg);
        }
    }

    void SpawnBomb(Vector3 offset, int dmg)
    {
        var b = TakeBullet<BomberBullet>(PoolObjectTypeBullet.Bomber, ShootingPoint.position + offset, rotate);
        b.setBulletStats(dmg);
        b.gameObject.SetActive(true);
    }

    public override void NormalShooting()
    {
        Vector3 mouse = MouseWorld;
        Face(mouse);
        IsShooting = true;
        Enemy crowd = EnemyManager.Instance.FindCrowdedEnemy();
        rotate = LookRotation(transform.position, crowd != null ? crowd.pos : mouse);
    }

    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        if(e == null) {
            Face(Vector3.zero);
            IsShooting = false;
            return;
        }
        Face(e.pos);
        IsShooting = true;
        Enemy crowd = EnemyManager.Instance.FindCrowdedEnemy();
        rotate = LookRotation(transform.position, crowd != null ? crowd.pos : Vector3.zero);
    }
}
