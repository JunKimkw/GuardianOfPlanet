using UnityEngine;

// 360 레이저: 대기 후 5초(패시브 시 10초) 동안 빙글 돌며 닿는 적을 계속 공격
public class Lazer : ContactWeapon
{
    public static Lazer instance;

    [HideInInspector] public int LazerDamage;

    [Header("Lazer Shoot")]
    public ShootLazer shootLazer;

    public bool ExtraHit;
    public bool SpinTwoTimes;

    float TimerAs;
    bool canShoot;
    Rigidbody2D rb;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.CircleLaser;
    public override int BaseDamage => LazerDamage;
    protected override float HitInterval => ExtraHit ? 0.05f : 0.1f;

    void Awake()
    {
        instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;
    }

    void Update()
    {
        if(!canShoot) return;
        TimerAs += Time.deltaTime;

        float wait = 10 * (1 - Trait(TraitsData.TraitsType.AttackSpeed));
        float end = wait + (SpinTwoTimes ? 10 : 5);

        if(TimerAs <= wait) {
            rb.isKinematic = true;
        } else if(TimerAs < end) {
            if(!SoundManager.Instance.laserSFXSource.isPlaying)
                SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.laser);
            doubleCheckEnemyOnLazer();
            rb.isKinematic = false;
            shootLazer.isShooting = true;
            transform.rotation = Quaternion.Euler(0, 0, 72 * (TimerAs - wait));
        } else {
            if(SoundManager.Instance.laserSFXSource.isPlaying)
                SoundManager.Instance.laserSFXSource.Stop();
            rb.isKinematic = true;
            shootLazer.isShooting = false;
            transform.localRotation = Quaternion.identity;
            TimerAs = 0;
            ClearTargets();
        }
    }

    public override void SetDamage() =>
        LazerDamage = Tiered(UpgradeLevel, 100, (0, 6), (20, 7), (100, 8), (200, 9), (300, 7), (500, 2));

    public override void DonShoot()
    {
        shootLazer.isShooting = false;
        transform.localRotation = Quaternion.identity;
        TimerAs = 0;
        canShoot = false;
    }

    // 회전 중에는 조준하지 않아요 (예전엔 매 프레임 조준이 회전을 덮어썼어요)
    public override void NormalShooting()
    {
        if(!shootLazer.isShooting) Face(MouseWorld);
        canShoot = true;
    }

    public override void AutoShooting()
    {
        if(!shootLazer.isShooting) {
            Enemy e = EnemyManager.Instance.FindCloseEnemy();
            Face(e != null ? e.pos : Vector3.zero);
        }
        canShoot = true;
    }
}
