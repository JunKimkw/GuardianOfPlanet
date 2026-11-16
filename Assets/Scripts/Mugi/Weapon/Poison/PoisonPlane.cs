using UnityEngine;

// 독 비행기: 10초마다 독탄을 여러 방향으로 뿌려요 (조준과 상관없이 계속 발사)
public class PoisonPlane : WeaponBase
{
    public static PoisonPlane instance;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    [HideInInspector] public int PoisonDamage;

    public bool extraBullet;
    public bool extraHit;
    public bool extraSizeOfPoisonArea;
    public bool Poison_canCrit;

    public float TimerAs;

    static readonly int[] Angles3 = { 300, 180, 60 };
    static readonly int[] Angles4 = { 0, 180, 90, 270 };

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Poison;
    public override int BaseDamage => PoisonDamage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        PoisonDamage = Tiered(UpgradeLevel, 20, (0, 1), (50, 2), (100, 3), (200, 4), (300, 3), (500, 2));

    void Update()
    {
        TimerAs += Time.deltaTime;
        if(TimerAs >= 10 * (1 - Trait(TraitsData.TraitsType.AttackSpeed))) {
            TimerAs = 0;
            long dmg = HitDamage;
            foreach(int a in extraBullet ? Angles4 : Angles3)
                CreateBullet(a, dmg);
        }
    }

    public void CreateBullet(int angle) => CreateBullet(angle, HitDamage);

    void CreateBullet(int angle, long dmg)
    {
        PoisonBullet b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Poison).GetComponent<PoisonBullet>();
        b.BulletDamage = dmg;
        b.transform.position = ShootingPoint.position;
        b.transform.rotation = Quaternion.Euler(0, 0, angle) * transform.rotation;
        b.gameObject.SetActive(true);
    }

    public override void DonShoot() => transform.rotation = Quaternion.identity;
    public override void NormalShooting() => Face(MouseWorld);
    public override void AutoShooting() => NormalShooting();   // 예전 동작 그대로 (자동 모드에서도 마우스 방향)
}
