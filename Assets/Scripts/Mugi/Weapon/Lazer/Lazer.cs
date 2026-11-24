using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 360 레이저: 대기 후 5초(패시브 시 10초) 동안 빙글 돌며 닿는 적을 계속 공격
// 증강: 지속 연장, 굵은 광선, 2중 회전(반대 방향 빔)
// 합체: "위성 레이저"(+궤도, 레이저 드론), "별빛 섬광"(+운석, 하늘에서 별이 떨어져 가로·세로 빛살. 그림은 StarFlash.cs)
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
    public override int BaseDamage => Balanced("laser", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)
    protected override float HitInterval => ExtraHit ? 0.05f : 0.1f;

    static bool Starlight => AugmentManager.Fused(AugmentDefs.FzStarlight);
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

    // 빔 갈래 수: 기본 1, 2중 회전 2
    int BeamCount {
        get {
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
            return 72f;
        }
    }

    void Update()
    {
        stormClock += Time.deltaTime;
        SyncDrones();
        if(!canShoot) return;
        if(Starlight) { StarlightTick(); return; }   // 별빛 섬광: 돌지 않고 별이 떨어져요 (회전 빔·판정 상자는 안 써요)
        float wait = 10 * (1 - Trait(TraitsData.TraitsType.AttackSpeed));
        // 기다리는 동안은 증강 공격속도(시간 가속 등)만큼 빨리 차요. 쏘는 시간은 그대로 (2026-11-21 점검: 예전엔 증강 공격속도가 안 먹었어요)
        TimerAs += Time.deltaTime * (TimerAs <= wait ? AugmentManager.AttackSpeedFactor : 1f);
        float end = wait + BeamTime;

        if(TimerAs <= wait) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rig.Apply(false, BeamCount, WidthMul);
        } else if(TimerAs < end) {
            if(!SoundManager.Instance.laserSFXSource.isPlaying)
                SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.laser);
            rb.bodyType = RigidbodyType2D.Dynamic;
            shootLazer.isShooting = true;
            transform.rotation = Quaternion.Euler(0, 0, SpinSpeed * (TimerAs - wait));
            rig.Apply(true, BeamCount, WidthMul);
            rig.CollectTargets(this);   // 몸이 빔에 닿은 적 (예전엔 중심점만 봤어요)
            TickContacts();
            if(enemyOnLaser.Count > 0) AugmentManager.WeaponHitEvent(SideWeapon.Lazer);   // 종결 증강 게이지
        } else {
            StopBeam();
            TimerAs = 0;
        }
    }

    public override void SetDamage() =>
        LazerDamage = Balanced("laser", 25f);

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

    // ---- 합체 "별빛 섬광" (2026-11-21 사용자 결정, 360 레이저 + 운석. 예전 "프리즘 빔" 자리: 무지개가 유치하고 빙빙 도는 빔이 눈 아프다고 해서)
    //  starlight_reload 초마다 화면의 서로 다른 적 starlight_count(3)마리(+ 별무리, + 탄 추가 증강) 위로 하얀 별이 차례로 떨어져요 (StarlightFall).
    //  닿는 순간 가로·세로로 길고 가는 빛살이 뻗어 그 줄 위의 적을 모두 한 번씩 맞혀요 (StarFlash). 돌지 않아요.
    //  증강: 별무리(별 +), 긴 빛살(빛살 길이 +), 여덟 갈래(R: 대각선 빛살 4개 더)
    //  피해: 맞은 적마다 1발 × starlight_hit_mul (임시값, 피해 숫자는 나중에 한꺼번에)
    float starTimer;

    void StarlightTick()
    {
        if(shootLazer.isShooting || TimerAs > 0f) { StopBeam(); TimerAs = 0f; }   // 합체하는 순간 돌던 빔은 꺼요
        float reload = Mathf.Max(Tuning.Get("starlight_reload", 1f) * (1 - Trait(TraitsData.TraitsType.AttackSpeed)), Tuning.Get("starlight_reload_min", 0.4f));
        starTimer += Time.deltaTime * AugmentManager.AttackSpeedFactor * (1f + AugmentManager.Silver("sl_reload"));   // 실버 "별 장전"   // 증강 공격속도(시간 가속 등)만큼 빨리 차요
        if(starTimer < reload) return;
        var list = AugmentManager.NearestInRange(transform.position, 999);
        if(list.Count == 0) return;   // 화면에 적이 없으면 기다렸다가 나타나면 바로
        starTimer = 0f;
        StartCoroutine(StarVolley(list));
    }

    // 화면의 적을 섞어서 서로 다른 적에게 하나씩 (적이 별보다 적으면 같은 적 둘레에)
    IEnumerator StarVolley(List<Enemy> list)
    {
        for(int i = list.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        int n = Mathf.Max(1, Tuning.GetInt("starlight_count", 3)) + AugmentManager.ExtraShots();
        int more = AugmentManager.WL("sl_more");
        if(more > 0) n += AugmentDefs.SlMore(more);
        long dmg = ClampDamage(HitDamage * (double)Tuning.Get("starlight_hit_mul", 0.4f) * (1.0 + AugmentManager.Silver("sl_power")));   // 실버 "별빛 응축"
        int lg = AugmentManager.WL("sl_long");
        float len = Tuning.Get("starlight_ray_len", 2.2f) * (lg > 0 ? 1f + AugmentDefs.SlLong(lg) : 1f);
        int eight = AugmentManager.WL("sl_eight");
        float diag = eight > 0 ? len * AugmentDefs.SlEight(eight) : 0f;
        float gap = Tuning.Get("starlight_gap", 0.12f);
        for(int i = 0; i < n; i++) {
            var e = list[i % list.Count];
            if(!StarlightFall.Alive(e)) continue;
            bool extra = i >= list.Count;   // 적보다 별이 많으면 같은 적 둘레에 (따라가지 않아요)
            Vector3 at = e.pos;
            if(extra) at += (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.5f, 1f));
            StarlightFall.Drop(this, extra ? null : e, at, dmg, len, diag);
            if(gap > 0f && i + 1 < n) yield return new WaitForSeconds(gap);
        }
    }

    // 별빛 섬광 한 대 (StarlightFall 에서 불러요)
    public void StarHit(Enemy e, long dmg) =>
        AugmentManager.WeaponHit(e, dmg, RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage, HitKind.Laser, Armor.Pen(TraitType), SideWeapon.Lazer);
}
