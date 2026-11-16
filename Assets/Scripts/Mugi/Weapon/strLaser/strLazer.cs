using UnityEngine;

// 직선 레이저: 12초마다 3초(패시브 시 7초) 동안 조준 방향으로 관통 레이저
public class strLazer : ContactWeapon
{
    public static strLazer instance;

    [HideInInspector] public int strLazerDamage;

    [Header("Lazer Shoot")]
    public ShootLazer shootLazer;

    public bool longerLaserTime;

    float TimerAs;
    Rigidbody2D rb;

    const float Wait = 12f;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.StrLaser;
    public override int BaseDamage => strLazerDamage;
    protected override float HitInterval => 0.15f;

    void Awake()
    {
        instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;
    }

    void Update()
    {
        TimerAs += Time.deltaTime;
        float end = longerLaserTime ? 19f : 15f;

        if(TimerAs <= Wait) {
            rb.isKinematic = true;
        } else if(TimerAs < end) {
            if(!SoundManager.Instance.strLaserSFXSource.isPlaying)
                SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.strLaser);
            doubleCheckEnemyOnLazer();
            shootLazer.isShooting = true;
            rb.isKinematic = false;
        } else {
            if(SoundManager.Instance.strLaserSFXSource.isPlaying)
                SoundManager.Instance.strLaserSFXSource.Stop();
            shootLazer.isShooting = false;
            transform.localRotation = Quaternion.identity;
            rb.isKinematic = true;
            ClearTargets();
            TimerAs = 0;
        }
    }

    public override void SetDamage() =>
        strLazerDamage = Tiered(UpgradeLevel, 10, (0, 1), (10, 1), (50, 1), (100, 2), (200, 3), (300, 3), (500, 2));

    public override void DonShoot()
    {
        transform.rotation = Quaternion.identity;
        TimerAs = 0;
    }

    public override void NormalShooting() => Face(MouseWorld);

    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        Face(e != null ? e.pos : Vector3.zero);
    }
}
