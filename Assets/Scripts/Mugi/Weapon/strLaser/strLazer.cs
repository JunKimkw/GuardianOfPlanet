using UnityEngine;

// 직선 레이저: 12초마다 3초(패시브 시 7초) 동안 조준 방향으로 관통 레이저
// 증강: 빠른 충전(대기 -초), 넓은 빔(폭), 관통 증폭(닿은 적이 많을수록 피해)
public class strLazer : ContactWeapon
{
    public static strLazer instance;

    [HideInInspector] public int strLazerDamage;

    [Header("Lazer Shoot")]
    public ShootLazer shootLazer;

    public bool longerLaserTime;

    float TimerAs;
    Rigidbody2D rb;
    BeamRig rig;

    const float BaseWait = 12f;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.StrLaser;
    public override SideWeapon Side => SideWeapon.StrLaser;
    public override int BaseDamage => strLazerDamage;
    protected override float HitInterval => 0.15f;

    // 관통 증폭: 빔에 닿은 적 1마리마다 피해 + (최대 20마리)
    protected override double ContactDamageMul {
        get {
            int l = AugmentManager.WL("sl_pierce");
            return l > 0 ? 1.0 + AugmentDefs.SlPierce(l) * Mathf.Min(20, enemyOnLaser.Count) : 1.0;
        }
    }

    float Wait {
        get {
            int l = AugmentManager.WL("sl_charge");
            return l > 0 ? Mathf.Max(3f, BaseWait - AugmentDefs.SlCharge(l)) : BaseWait;
        }
    }

    float WidthMul {
        get {
            int l = AugmentManager.WL("sl_wide");
            return l > 0 ? 1f + AugmentDefs.BeamWidth(l) : 1f;
        }
    }

    void Awake()
    {
        instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rig = new BeamRig(transform, shootLazer);
    }

    void Update()
    {
        TimerAs += Time.deltaTime;
        float wait = Wait;
        float end = wait + (longerLaserTime ? 7f : 3f);

        if(TimerAs <= wait) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rig.Apply(false, 1, WidthMul, false);
        } else if(TimerAs < end) {
            if(!SoundManager.Instance.strLaserSFXSource.isPlaying)
                SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.strLaser);
            doubleCheckEnemyOnLazer();
            shootLazer.isShooting = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rig.Apply(true, 1, WidthMul, false);
            TickContacts();
        } else {
            StopBeam();
            TimerAs = 0;
        }
    }

    public override void SetDamage() =>
        strLazerDamage = Tiered(UpgradeLevel, 10, (0, 1), (10, 1), (50, 1), (100, 2), (200, 3), (300, 3), (500, 2));

    // 레이저 끄기 (예전엔 쏘는 도중 "공격 안 함"이 되면 레이저가 켜진 채로 남고 소리도 계속 났어요)
    void StopBeam()
    {
        if(SoundManager.Instance != null && SoundManager.Instance.strLaserSFXSource != null && SoundManager.Instance.strLaserSFXSource.isPlaying)
            SoundManager.Instance.strLaserSFXSource.Stop();
        if(shootLazer != null) shootLazer.isShooting = false;
        if(rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        transform.localRotation = Quaternion.identity;
        ClearTargets();
    }

    public override void DonShoot()
    {
        if(shootLazer.isShooting) StopBeam();
        transform.rotation = Quaternion.identity;
        TimerAs = 0;
    }

    protected override void OnDisable()
    {
        StopBeam();
        TimerAs = 0;
    }

    public override void NormalShooting() => Face(MouseWorld);

    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        Face(e != null ? e.pos : Vector3.zero);
    }
}
