using System.Collections.Generic;
using UnityEngine;

// 360 레이저: 대기 후 5초(패시브 시 10초) 동안 빙글 돌며 닿는 적을 계속 공격
// 증강: 지속 연장, 굵은 광선, 2중 회전(반대 방향 빔)
// 합체: "위성 레이저"(+궤도, 레이저 드론), "프리즘 빔"(+직선 레이저, 3갈래 무지개 빔)
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
    BeamRig rig;
    float stormClock;
    readonly List<LaserDrone> drones = new List<LaserDrone>();

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.CircleLaser;
    public override SideWeapon Side => SideWeapon.Lazer;
    public override int BaseDamage => Balanced("laser", 52.3f);   // 무기 밸런스 (WeaponBase.LevelCurve)
    protected override float HitInterval => ExtraHit ? 0.05f : 0.1f;

    static bool Prism => AugmentManager.Fused(AugmentDefs.FzPrism);
    static bool SatLaser => AugmentManager.Fused(AugmentDefs.FzSatLaser);

    void Awake()
    {
        instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rig = new BeamRig(transform, shootLazer);
    }

    // 쏘는 시간 (초): 기본 5 (패시브 10) + 지속 연장 + 레이저 연장
    float BeamTime {
        get {
            float t = SpinTwoTimes ? 10f : 5f;
            int l = AugmentManager.WL("lz_long");
            if(l > 0) t += AugmentDefs.LzLong(l);
            int s = AugmentManager.WL("sat_long");
            if(SatLaser && s > 0) t += AugmentDefs.SatLong(s);
            return t;
        }
    }

    // 빔 갈래 수: 기본 1, 2중 회전 2, 프리즘 빔 3 (+갈래 추가, 무지개 폭풍 중엔 12)
    int BeamCount {
        get {
            if(Prism) {
                int storm = AugmentManager.WL("pr_storm");
                if(storm > 0 && stormClock % AugmentDefs.PrStormEvery(storm) < 3f) return 12;
                int more = AugmentManager.WL("pr_more");
                return 3 + (more > 0 ? AugmentDefs.PrMore(more) : 0);
            }
            return AugmentManager.WL("lz_twin") > 0 ? 2 : 1;
        }
    }

    float WidthMul {
        get {
            int l = AugmentManager.WL("lz_thick");
            return l > 0 ? 1f + AugmentDefs.BeamWidth(l) : 1f;
        }
    }

    float SpinSpeed {
        get {
            int l = AugmentManager.WL("pr_spin");
            return 72f * (Prism && l > 0 ? 1f + AugmentDefs.PrSpin(l) : 1f);
        }
    }

    void Update()
    {
        stormClock += Time.deltaTime;
        SyncDrones();
        if(!canShoot) return;
        float wait = 10 * (1 - Trait(TraitsData.TraitsType.AttackSpeed));
        // 기다리는 동안은 증강 공격속도(시간 가속 등)만큼 빨리 차요. 쏘는 시간은 그대로 (2026-11-21 점검: 예전엔 증강 공격속도가 안 먹었어요)
        TimerAs += Time.deltaTime * (TimerAs <= wait ? AugmentManager.AttackSpeedFactor : 1f);
        float end = wait + BeamTime;

        if(TimerAs <= wait) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rig.Apply(false, BeamCount, WidthMul, Prism);
        } else if(TimerAs < end) {
            if(!SoundManager.Instance.laserSFXSource.isPlaying)
                SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.laser);
            rb.bodyType = RigidbodyType2D.Dynamic;
            shootLazer.isShooting = true;
            transform.rotation = Quaternion.Euler(0, 0, SpinSpeed * (TimerAs - wait));
            rig.Apply(true, BeamCount, WidthMul, Prism);
            rig.CollectTargets(this);   // 몸이 빔에 닿은 적 (예전엔 중심점만 봤어요)
            TickContacts();
            if(enemyOnLaser.Count > 0) AugmentManager.WeaponHitEvent(SideWeapon.Lazer);   // 종결 증강 게이지
        } else {
            StopBeam();
            TimerAs = 0;
        }
    }

    public override void SetDamage() =>
        LazerDamage = Balanced("laser", 52.3f);

    // 레이저 끄기: 그림·소리·판정·대상 목록 (예전엔 쏘는 도중 멈추면 레이저와 소리가 남았어요)
    void StopBeam()
    {
        if(SoundManager.Instance != null && SoundManager.Instance.laserSFXSource != null && SoundManager.Instance.laserSFXSource.isPlaying)
            SoundManager.Instance.laserSFXSource.Stop();
        if(rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        if(shootLazer != null) shootLazer.isShooting = false;
        rig?.Hide();
        transform.localRotation = Quaternion.identity;
        ClearTargets();
    }

    // 위성 레이저 드론 (합체했을 때만)
    void SyncDrones()
    {
        int want = 0;
        if(SatLaser) {
            int more = AugmentManager.WL("sat_more");
            want = 2 + (more > 0 ? AugmentDefs.SatMore(more) : 0);
        }
        while(drones.Count < want) drones.Add(LaserDrone.Create(transform.parent));
        for(int i = 0; i < drones.Count; i++) {
            var d = drones[i];
            if(d == null) continue;
            bool on = i < want;
            if(d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
            d.index = i;
            d.count = want;
        }
    }

    public override void DonShoot()
    {
        if(shootLazer.isShooting) StopBeam();
        TimerAs = 0;
        canShoot = false;
    }

    protected override void OnDisable()
    {
        StopBeam();
        TimerAs = 0;
        foreach(var d in drones) if(d != null) d.gameObject.SetActive(false);
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
