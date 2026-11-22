using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 운석 (2026-11-21 사용자 요청, 10번째 무기): 1초마다 화면의 서로 다른 적 위에 붉은 경고 원이 생기고,
// 하늘에서 운석이 비스듬히 떨어져 그 구역(반경)에 피해를 줘요. 적이 다가오는 만큼 조금 앞을 노려요.
// 폭격기와 다른 점: 우주선에서 날아가지 않고 하늘에서 바로 떨어져서, 멀리 있거나 흩어진 적을 바로 잡아요.
// 대신 경고가 뜬 동안(meteor_fall_time) 적이 움직이면 비켜 갈 수 있어요.
// 프리팹 없이 코드로 만들어요 (WeaponManager). 상점 칸은 튕김총 칸을 복제해서 만들어요 (MeteorShop.CreateFrom).
// 증강: 유성우(운석 +), 거대 운석(범위 +), 불타는 구덩이(떨어진 자리가 잠깐 불타요). 숫자 tuning.txt meteor_*
// 피해 계수 6.5 는 임시값이에요 (2026-11-21 사용자: 피해 줄이고 더 자주 두 번 → 26 · 4초 → 13 · 2초 → 6.5 · 1초, 보스 DPS 는 그대로. 시뮬: tools/weapon_dps_sim2.js)
// 합체 (2026-11-21):
//  - 별의 낙인 (+저격총): 낙인(빨간 조준 표식)이 찍힌 적을 운석이 따라가 떨어져요. 못 피하고, 폭발은 모두 치명타, 보스 추가 피해.
//    증강 연속 낙인(운석 +), 약점 조준(치명 피해 +), 처형 낙인(R: 낙인 운석에 죽으면 가까운 적에게 낙인이 옮겨 또 떨어져요)
//  - 대충돌 (+블랙홀): 떨어진 자리가 잠깐 중력 우물이 돼 적을 끌어모으고, 우물이 끝나면 가장 많이 모인 곳에 거대 운석.
//    증강 강한 중력(우물 범위 +), 중력 지속(우물 시간 +), 연쇄 충돌(R: 거대 운석 자리에 우물이 한 번 더 생기고 거대 운석이 또)
public class MeteorGun : GunWeapon
{
    public static MeteorGun instance;
    public const string SaveId = "remaster-meteor";   // 저장 id (UpgradeLevel_Weapon)

    [HideInInspector] public int Damage;

    // 상점 패시브 (MeteorShop)
    [HideInInspector] public bool ExtraMeteor;   // 운석 +1
    [HideInInspector] public bool BigRadius;     // 범위 ×1.3
    [HideInInspector] public bool Burning;       // 떨어진 자리가 불타요

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Meteor;
    public override SideWeapon Side => SideWeapon.Meteor;
    public override int BaseDamage => Balanced("meteor", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    static bool Brand => AugmentManager.Fused(AugmentDefs.FzBrand);
    // 운석 전용 증강(유성우·거대 운석·불타는 구덩이)은 기본 운석에만 (2026-11-21 사용자: "[운석] 운석 +3 인데 대충돌도 늘어나").
    // 합체 무기는 운석 코드 위에서 돌아서 예전엔 같이 먹었어요 (실제 게임에선 운석으로 합체하면 이 증강이 사라지지만, B 쪽 합체·테스트 창에선 남아요)
    static int MtL(string id) => Brand || Impact ? 0 : AugmentManager.WL(id);
    static bool Impact => AugmentManager.Fused(AugmentDefs.FzImpact);

    void Awake()
    {
        instance = this;
        id = SaveId;
    }

    public override void SetDamage() => Damage = Balanced("meteor", 25f);

    // 장전 시간: meteor_reload 초 × 특성·플레이어 공격속도·패시브 ÷ 증강 공격속도
    public override float getAttackSpeed()
    {
        float t = Tuning.Get("meteor_reload", 1f) * (1 - Trait(TraitsData.TraitsType.AttackSpeed))
                  * (1 - Player.instance.FinalAttack_SpeedPercent) * (1 - ExtraAttackSpeedScale);
        t = Mathf.Max(t, Tuning.Get("meteor_reload_min", 0.4f));
        return t / AugmentManager.AttackSpeedFactor;
    }

    float holeTimer;   // 대충돌: 다음 블랙홀까지
    readonly List<Vector3> lastWells = new List<Vector3>();   // 대충돌: 직전 블랙홀 자리

    protected override void Update()
    {
        if(!IsShooting) return;
        // 대충돌 (2026-11-21 사용자 결정): 운석은 기본처럼 계속 떨어지고, 그것과 따로 impact_hole_interval 초마다 블랙홀 하나가
        // 적이 가장 몰린 곳을 모은 뒤 큰 운석이 떨어져요 (예전엔 블랙홀이 운석마다 열려서 적이 다가오질 못했어요)
        if(Impact) {
            holeTimer += Time.deltaTime * AugmentManager.AttackSpeedFactor;
            if(holeTimer >= Tuning.Get("impact_hole_interval", 5f)) {
                var all = AugmentManager.NearestInRange(transform.position, 999);
                if(all.Count > 0) { holeTimer = 0f; StartCoroutine(ImpactVolley(all, 1, HitDamage, Radius)); }
            }
        }
        TimerAs += Time.deltaTime;
        if(TimerAs < getAttackSpeed()) return;
        if(AugmentManager.NearestInRange(transform.position, 1).Count == 0) return;   // 화면에 적이 없으면 기다렸다가 나타나면 바로
        TimerAs = 0;
        Fire();
    }

    // 한 번에 떨어지는 운석 수: 기본 + 패시브 + 유성우 + 다중 사격 같은 탄 추가 증강 (+ 연속 낙인)
    protected override void Fire()
    {
        int n = Mathf.Max(1, Tuning.GetInt("meteor_count", 3)) + (ExtraMeteor ? 1 : 0) + AugmentManager.ExtraShots();
        int rain = MtL("mt_rain");
        if(rain > 0) n += AugmentDefs.MtRain(rain);
        StartCoroutine(Volley(n));
    }

    public override void CreateBullet() => StartCoroutine(Volley(1));

    // 화면의 적을 섞어서 서로 다른 적에게 하나씩 (적이 운석보다 적으면 같은 적 둘레에 흩어서)
    IEnumerator Volley(int n)
    {
        var list = AugmentManager.NearestInRange(transform.position, 999);
        for(int i = list.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        long dmg = HitDamage;
        float r = Radius, gap = Tuning.Get("meteor_gap", 0.12f);
        if(Brand) { StartCoroutine(BrandVolley(n, dmg, r)); yield break; }
        for(int i = 0; i < n && list.Count > 0; i++) {
            var e = list[i % list.Count];
            if(e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) continue;
            var o = new MeteorFall.Opts { dmg = dmg, radius = r, sound = i == 0, owner = this, at = Lead(e) };
            if(i >= list.Count) o.at += (Vector3)(Random.insideUnitCircle.normalized * r * 0.9f);
            MeteorFall.Drop(o);
            if(gap > 0f) yield return new WaitForSeconds(gap);
        }
    }

    // 별의 낙인 (2026-11-21 사용자 결정): 장전마다 적 brand_targets(3)마리를 인식해 낙인을 찍어요 (보스 먼저, 그다음 체력이 많은 순).
    // 증강 "연속 낙인"으로 인식하는 적이 늘어요. 인식한 적마다 운석 n개(기본 3, 운석 수를 늘리는 효과가 여기에 더해져요)가 차례로 떨어져요.
    // 운석은 비슷한 하늘 자리에서 출발하되 출발점이 조금씩 흔들려서(brand_sky_jitter) 경로가 살짝씩 달라요 (예전엔 모두 같은 경로라 한 줄로 보였어요).
    // 운석이 많아진 만큼 1발 피해는 brand_damage_div(3)로 나눠요. 낙인 적이 도중에 죽으면 남은 운석은 아직 안 고른 다음 강한 적에게
    IEnumerator BrandVolley(int n, long dmg, float r)
    {
        int count = Mathf.Max(1, Tuning.GetInt("brand_targets", 3));
        int more = AugmentManager.WL("bd_more");
        if(more > 0) count += AugmentDefs.BdMore(more);
        dmg = WeaponBase.ClampDamage(dmg / (double)Mathf.Max(1f, Tuning.Get("brand_damage_div", 3f)));
        float gap = Tuning.Get("brand_gap", 0.15f), jitter = Tuning.Get("brand_sky_jitter", 0.9f);
        var chosen = new HashSet<Enemy>();
        var targets = new List<Enemy>();
        foreach(var e in StrongestList()) { if(targets.Count >= count) break; targets.Add(e); chosen.Add(e); }
        if(targets.Count == 0) yield break;
        for(int k = 0; k < n; k++) {
            for(int i = 0; i < targets.Count; i++) {
                var e = targets[i];
                if(!Alive(e)) {   // 죽었으면 아직 안 고른 다음 강한 적
                    e = null;
                    foreach(var c in StrongestList()) if(!chosen.Contains(c)) { e = c; break; }
                    if(e == null) continue;
                    targets[i] = e;
                    chosen.Add(e);
                }
                MeteorFall.Drop(new MeteorFall.Opts {
                    at = e.pos, follow = e, dmg = dmg, radius = r, sound = k == 0 && i == 0, owner = this,
                    brand = true, forceCrit = true, critExtra = BrandCritExtra, bossBonus = true, chain = ChainCount, skyJitter = jitter,
                });
            }
            if(gap > 0f) yield return new WaitForSeconds(gap);
        }
    }

    static bool Alive(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;

    // 화면 안 적: 보스 먼저, 그다음 지금 체력이 많은 순
    static List<Enemy> StrongestList()
    {
        var list = AugmentManager.NearestInRange(Vector3.zero, 999);
        list.Sort((a, b) => a.IsBoss != b.IsBoss ? (a.IsBoss ? -1 : 1) : b.CurrentHealth.CompareTo(a.CurrentHealth));
        return list;
    }

    // 대충돌 (2026-11-21 사용자 결정. 마지막: "블랙홀이 너무 자주·많이 당겨서 적이 다가오질 못해, 사기"):
    //  1) impact_hole_interval(5)초마다 블랙홀 impact_wells(1)개, 적이 가장 몰린 곳(우주선에서 impact_min_dist 칸 밖, 가까우면 반대쪽으로 밀어요)에 열려
    //     impact_well_time 초 동안 끌어모아요. 보통 운석은 이것과 따로 기본 무기처럼 계속 떨어져요 (Update)
    //  2) 다 모은 뒤 impact_drop_delay(1초) 텀을 두고, 블랙홀이 닫히는 순간 그 안에 운석이 모두 떨어져요:
    //     큰 운석 1개(impact_core_mul, 모인 적 1마리당 +). 증강 "소행성대"면 블랙홀 안에 보통 운석이 더 (0.12초 간격)
    //  3) 충돌 에너지 (R): 큰 운석 피해 +
    IEnumerator ImpactVolley(List<Enemy> list, int n, long dmg, float r)
    {
        Vector3 ship = transform.position;
        ship.z = 0f;
        float minDist = Tuning.Get("impact_min_dist", 3f), wr = WellRadius, life = WellTime;
        var far = list.FindAll(e => { Vector3 d = e.pos - ship; d.z = 0f; return d.magnitude >= minDist; });
        if(far.Count > 0) list = far;
        // 너무 멀지 않게 (2026-11-21 사용자: "블랙홀이 우주선에서 너무 멀어"): 우주선에서 impact_max_dist 칸 안의 적 중에서 골라요.
        // 그 안에 적이 없으면 우주선에 가장 가까운 적 3마리 둘레에서
        float maxDist = Mathf.Max(minDist, Tuning.Get("impact_max_dist", 5f));
        var near = list.FindAll(e => { Vector3 d = e.pos - ship; d.z = 0f; return d.magnitude <= maxDist; });
        if(near.Count > 0) list = near;
        else {
            list.Sort((x, y) => (x.pos - ship).sqrMagnitude.CompareTo((y.pos - ship).sqrMagnitude));
            if(list.Count > 3) list.RemoveRange(3, list.Count - 3);
        }
        if(list.Count == 0) yield break;
        // 블랙홀 자리: 반경 안 적이 가장 많은 적 자리부터, 서로 겹치지 않게 impact_wells 개
        int wells = Mathf.Max(1, Tuning.GetInt("impact_wells", 1));
        var scored = new List<(Vector3 p, int c)>();
        foreach(var e in list) {
            Vector3 p = AwayFromShip(e.pos, ship, minDist);
            scored.Add((p, AugmentManager.EnemiesNear(p, wr).Count));
        }
        scored.Sort((a, b) => b.c.CompareTo(a.c));
        // 직전 블랙홀 자리 근처(반경 × impact_avoid_last)는 피해요 (2026-11-21 사용자: 모은 무리가 또 가장 몰린 곳이 돼서 계속 당겨졌어요).
        // 피할 수 있는 자리가 없으면(적이 한 무리로 뭉쳐 오면) 이번엔 블랙홀 없이 큰 운석만 그 무리에 떨어져요
        // (2026-11-21 사용자 스크린샷: 뭉친 무리를 5초마다 계속 붙잡았어요). 그러면 같은 무리는 많아야 두 번에 한 번만 붙잡혀요
        float avoid = wr * Tuning.Get("impact_avoid_last", 2.9f);
        var spots = new List<Vector3>();
        foreach(var sc in scored) {
            if(spots.Count >= wells) break;
            bool apart = true;
            foreach(var s in spots) if((s - sc.p).magnitude < wr * 1.2f) { apart = false; break; }
            foreach(var l in lastWells) if((l - sc.p).magnitude < avoid) { apart = false; break; }
            if(apart) spots.Add(sc.p);
        }
        lastWells.Clear();
        lastWells.AddRange(spots);
        if(spots.Count == 0) {   // 블랙홀 없이 큰 운석만 (다음 회차엔 다시 블랙홀을 열 수 있어요)
            if(scored.Count == 0) yield break;
            float cf = Mathf.Max(Tuning.Get("meteor_fall_time", 0.45f), Tuning.Get("impact_core_fall", 0.5f));
            DropCore(scored[0].p, dmg, r, scored[0].c, cf, new List<MeteorWell>());
            yield break;
        }

        // 1) 블랙홀이 먼저 열려요 (운석이 다 떨어질 때까지 붙잡아요)
        int moreL = AugmentManager.WL("im_more");
        int extra = moreL > 0 ? AugmentDefs.ImMore(moreL) : 0;
        float extraGap = Tuning.Get("impact_more_gap", 0.12f);
        var opened = new List<MeteorWell>();
        foreach(var p in spots) opened.Add(MeteorWell.Open(p, wr, life + 0.1f + extra * extraGap));
        float fall = Mathf.Min(life, Tuning.Get("meteor_fall_time", 0.45f));
        float coreFall = Mathf.Min(life, Mathf.Max(fall, Tuning.Get("impact_core_fall", 0.5f)));
        yield return new WaitForSeconds(life - coreFall);

        // 2) 가장 많이 모인 블랙홀 → 거대 운석. 운석 n 개를 블랙홀마다 나눠서 (거대 운석이 1개 몫)
        int best = 0, bestN = -1;
        for(int i = 0; i < spots.Count; i++) {
            int c = AugmentManager.EnemiesNear(spots[i], wr).Count;
            if(c > bestN) { bestN = c; best = i; }
        }
        DropCore(spots[best], dmg, r, bestN, coreFall, opened);
        if(coreFall > fall) yield return new WaitForSeconds(coreFall - fall);
        int per = Mathf.Max(1, n / spots.Count);
        for(int i = 0; i < spots.Count; i++)
            for(int k = i == best ? 1 : 0; k < per; k++)
                MeteorFall.Drop(new MeteorFall.Opts { at = InWell(spots[i], wr), dmg = dmg, radius = r, owner = this, fallTime = fall, skyJitter = 0.8f });
        // 소행성대: 블랙홀마다 운석 +extra 개 (조금씩 뒤에)
        for(int k = 0; k < extra; k++) {
            if(extraGap > 0f) yield return new WaitForSeconds(extraGap);
            foreach(var p in spots)
                MeteorFall.Drop(new MeteorFall.Opts { at = InWell(p, wr), dmg = dmg, radius = r, owner = this, fallTime = fall, skyJitter = 0.8f });
        }
    }

    static Vector3 InWell(Vector3 c, float wr) => c + (Vector3)(Random.insideUnitCircle * wr * 0.45f);

    // 큰 운석: 운석 1발 × impact_core_mul × (1 + 모인 적 1마리당 impact_per_enemy) × (1 + 충돌 에너지). 떨어지면 모인 적을 사방으로 튕겨 내요
    // (2026-11-21 사용자: "연쇄 충돌 말고 피해를 올리는 증강" → 연쇄 충돌(블랙홀 한 번 더 + 큰 운석 한 번 더)은 지우고 충돌 에너지로)
    void DropCore(Vector3 at, long dmg, float r, int gathered, float coreFall, List<MeteorWell> wells)
    {
        int cap = Tuning.GetInt("impact_bonus_cap", 10);
        int energy = AugmentManager.WL("im_energy");
        double mul = Tuning.Get("impact_core_mul", 4f) * (1.0 + Tuning.Get("impact_per_enemy", 0.08f) * Mathf.Min(Mathf.Max(0, gathered), cap))
                     * (energy > 0 ? 1.0 + AugmentDefs.ImEnergy(energy) : 1.0);
        var o = new MeteorFall.Opts {
            at = at, dmg = WeaponBase.ClampDamage(dmg * mul), radius = r * Tuning.Get("impact_core_radius", 1.6f),
            sound = true, owner = this, big = true, fallTime = coreFall,
        };
        // 떨어지는 순간 블랙홀을 닫고(더 안 끌어요) 모인 적을 사방으로 튕겨 내요
        // (2026-11-21 사용자: "퍼지는데 블랙홀 때문에 다시 모여" — 소행성대 운석을 기다리느라 블랙홀이 조금 더 열려 있었어요)
        o.onLand = p => {
            foreach(var w in wells) if(w != null) w.Close();
            MeteorFall.Scatter(p, o.radius * Tuning.Get("impact_scatter_radius", 1.3f));
        };
        MeteorFall.Drop(o);
    }

    // p 가 우주선에서 minDist 보다 가까우면 우주선 반대쪽으로 minDist 까지 밀어요
    static Vector3 AwayFromShip(Vector3 p, Vector3 ship, float minDist)
    {
        p.z = 0f;
        Vector3 d = p - ship;
        if(d.magnitude >= minDist) return p;
        if(d.sqrMagnitude < 0.0001f) d = Vector3.up;
        return ship + d.normalized * minDist;
    }

    // 떨어질 때까지 적이 우주선 쪽으로 다가오는 만큼 앞을 노려요 (폭격기와 같은 방식)
    Vector3 Lead(Enemy e)
    {
        Vector3 ship = transform.position;
        Vector3 p = e.pos;
        float dist = Vector3.Distance(ship, p);
        float lead = Mathf.Min(dist * 0.8f, e.enemyMovingSpeed * Tuning.Get("meteor_fall_time", 0.45f));
        p = Vector3.MoveTowards(p, ship, lead);
        p.z = 0f;
        return p;
    }

    // 범위: 기본 × 패시브 × 거대 운석 × 탄 크기 증강 × 폭발 범위 증강 (화약 연구 등, 폭격기와 같이)
    public float Radius {
        get {
            float r = Tuning.Get("meteor_radius", 0.9f);
            if(BigRadius) r *= 1.3f;
            int big = MtL("mt_big");
            if(big > 0) r *= 1f + AugmentDefs.MtBig(big);
            if(AugmentManager.Instance != null) r *= AugmentManager.Instance.BulletScaleMultiplier;
            return r * AugmentManager.BombRadiusMultiplier;
        }
    }

    // 불타는 구덩이: 0.5초마다 운석 피해의 이 비율 (0 이면 안 불타요)
    public float BurnMul {
        get {
            int l = MtL("mt_burn");
            return (l > 0 ? AugmentDefs.MtBurn(l) : 0f) + (Burning ? Tuning.Get("meteor_burn_passive", 0.1f) : 0f);
        }
    }

    // 별의 낙인 숫자
    float BrandCritExtra {
        get {
            int l = AugmentManager.WL("bd_aim");
            return l > 0 ? AugmentDefs.BdAim(l) : 0f;
        }
    }
    int ChainCount {
        get {
            int l = AugmentManager.WL("bd_chain");
            return l > 0 ? AugmentDefs.BdChain(l) : 0;
        }
    }

    // 대충돌 숫자
    float WellRadius {
        get {
            int l = AugmentManager.WL("im_well");
            return Radius * Tuning.Get("impact_well_radius", 1.4f) * (l > 0 ? 1f + AugmentDefs.ImWell(l) : 1f);
        }
    }
    float WellTime {
        get {
            // 모으는 시간 + 운석이 떨어지기 전 텀 (2026-11-21 사용자: "모으고 운석이 너무 바로 떨어져, 1초 텀"). 텀 동안에도 블랙홀이 붙잡고 있어요
            // (예전 증강 "중력 지속"은 블랙홀이 너무 오래 당겨서 지웠어요)
            return Tuning.Get("impact_well_time", 1f) + Tuning.Get("impact_drop_delay", 1f);
        }
    }

    // 하늘에서 떨어지니까 조준(회전)은 안 해요
    public override void NormalShooting() => IsShooting = true;

    public override void AutoShooting() => IsShooting = EnemyManager.Instance.FindCloseEnemy() != null;

    public override void DonShoot() => IsShooting = false;
}

// 운석 하나: 경고 원(바닥) → 하늘에서 비스듬히 떨어짐(불꽃 꼬리) → 쾅 (반경 안 적·더미에게 한 번) → (불타는 구덩이 / 중력 우물)
// 무기·합체·종결(Ultimates.cs)이 같이 써요 (Opts 로 정해요)
public class MeteorFall : MonoBehaviour
{
    public class Opts
    {
        public Vector3 at;               // 떨어질 자리 (follow 가 있으면 그 적을 따라가요)
        public long dmg;
        public float radius = 0.9f;
        public bool sound;
        public MeteorGun owner;          // 불타는 구덩이 (없으면 안 불타요)
        public Enemy follow;             // 별의 낙인: 이 적을 따라 떨어져요
        public bool brand;               // 낙인 표식 (빨간 조준 원 + 십자, 우주선에서 조준선)
        public bool forceCrit;           // 폭발 모두 치명타
        public float critExtra;          // 치명 피해 +
        public bool bossBonus;           // 보스 추가 피해 (저격총과 같아요)
        public int chain;                // 처형 낙인: 낙인 적이 죽으면 가까운 적에게 옮겨 가는 남은 횟수
        public float wellRadius;         // 대충돌: 0보다 크면 떨어진 자리가 중력 우물
        public float wellTime;
        public System.Action<Vector3> onLand;
        public bool big;                 // 거대 운석 (그림이 크고 번쩍)
        public float fallTime = -1f;     // 음수면 meteor_fall_time
        public float pen = -1f;          // 방어 관통 (음수면 운석 관통력)
        public Color color = new Color(1f, 0.62f, 0.32f);
        public float skyJitter;          // 하늘 출발점을 이만큼 흔들어요 (별의 낙인: 비슷한 자리에서 살짝 다른 경로)
        public bool randomSky;           // 하늘 출발점을 운석마다 다르게 (천벌: 한 줄로 안 보이게)
    }

    static Material mat;
    static readonly Color Fire = new Color(1f, 0.55f, 0.2f);
    static readonly Color Warn = new Color(1f, 0.25f, 0.2f);

    Opts o;
    Vector3 from, at0;   // at0: 처음 노린 자리 (따라가는 적이 움직인 만큼 하늘 출발점도 옮겨요)
    float t, fall;
    SpriteRenderer warnOuter, warnInner, rock, glow;
    GameObject rockRoot;
    LineRenderer aimLine, crossA, crossB;

    public static void Drop(Opts o)
    {
        o.at.z = 0f;
        var go = new GameObject("Meteor");
        go.transform.position = o.at;
        var m = go.AddComponent<MeteorFall>();
        m.o = o;
        m.fall = Mathf.Max(0.1f, o.fallTime > 0f ? o.fallTime : Tuning.Get("meteor_fall_time", 0.45f));
        float h = o.big ? 1.4f : 1f;
        m.at0 = o.at;
        m.from = o.at + new Vector3(Tuning.Get("meteor_from_x", -2.2f) * h, Tuning.Get("meteor_from_y", 7f) * h, 0f);
        if(o.skyJitter > 0f) m.from += new Vector3(Random.Range(-o.skyJitter, o.skyJitter), Random.Range(-o.skyJitter, o.skyJitter) * 0.6f, 0f);
        if(o.randomSky) m.from = o.at + new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(5.5f, 8f), 0f);
        m.Build();
    }

    // 대충돌 큰 운석 (2026-11-21 사용자): 떨어진 자리 반경 안 적을 사방으로 impact_scatter 칸 튕겨 내요 (0.25초, 보스는 30%).
    // 모인 무리가 풀려서 다음 블랙홀이 같은 무리를 또 끌어가지 않게 해요
    public static void Scatter(Vector3 c, float radius)
    {
        var list = AugmentManager.EnemiesNear(c, radius);
        if(list.Count == 0) return;
        var go = new GameObject("MeteorScatter");
        go.AddComponent<MeteorScatter>().Run(c, list, Tuning.Get("impact_scatter", 0.35f));
    }

    // 예전 호출 모양 (무기 기본 운석)
    public static void Drop(MeteorGun owner, Vector3 at, long dmg, float r, bool sound) =>
        Drop(new Opts { owner = owner, at = at, dmg = dmg, radius = r, sound = sound });

    Vector3 Target {
        get {
            if(o.follow != null && Alive(o.follow)) { o.at = o.follow.transform.position; o.at.z = 0f; }
            return o.at;
        }
    }

    static bool Alive(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;

    void Build()
    {
        float r = o.radius;
        // 경고 원: 흐린 붉은 원 + 떨어질수록 차오르는 안쪽 원 (적 아래에 그려요)
        warnOuter = Disc(transform, "WarnOuter", new Color(Warn.r, Warn.g, Warn.b, 0.18f), "Mine", 3);
        warnOuter.transform.localScale = Vector3.one * r * 2f;
        warnInner = Disc(transform, "WarnInner", new Color(Warn.r, Warn.g, Warn.b, 0.28f), "Mine", 4);
        warnInner.transform.localScale = Vector3.zero;
        if(o.brand) {   // 낙인: 적 위 십자 + 우주선에서 조준선 (저격총처럼)
            crossA = Line(new Color(1f, 0.2f, 0.25f, 0.95f), 0.05f, 540);
            crossB = Line(new Color(1f, 0.2f, 0.25f, 0.95f), 0.05f, 540);
            aimLine = Line(new Color(1f, 0.2f, 0.25f, 0.6f), 0.025f, 539);
        }

        // 운석: 돌 + 하얀 속불 + 불꽃 꼬리
        rockRoot = new GameObject("Rock");
        rockRoot.transform.position = from;
        float size = Tuning.Get("meteor_size", 0.42f) * Mathf.Clamp(r / 0.9f, 0.8f, o.big ? 3f : 1.8f);
        rock = Disc(rockRoot.transform, "Body", o.color, RemasterFx.FxLayer, 505);
        rock.transform.localScale = Vector3.one * size;
        glow = Disc(rockRoot.transform, "Core", new Color(1f, 0.95f, 0.8f), RemasterFx.FxLayer, 506);
        glow.transform.localScale = Vector3.one * size * 0.5f;
        var tr = rockRoot.AddComponent<TrailRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        tr.sharedMaterial = mat;
        tr.time = o.big ? 0.35f : 0.22f;
        tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.widthMultiplier = size * 0.9f;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f) },
                  new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.sortingLayerName = RemasterFx.FxLayer;
        tr.sortingOrder = 504;
        tr.Clear();
    }

    static SpriteRenderer Disc(Transform parent, string name, Color c, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RemasterFx.CircleSprite;
        sr.color = c;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    LineRenderer Line(Color c, float w, int order)
    {
        var go = new GameObject("BrandLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = w;
        lr.startColor = lr.endColor = c;
        lr.sortingLayerName = RemasterFx.FxLayer;
        lr.sortingOrder = order;
        return lr;
    }

    void Update()
    {
        if(rockRoot == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / fall);
        Vector3 at = Target;
        transform.position = at;
        warnInner.transform.localScale = Vector3.one * o.radius * 2f * k;
        var oc = warnOuter.color;
        oc.a = 0.14f + 0.08f * Mathf.Sin(t * 30f);
        warnOuter.color = oc;
        if(o.brand) {
            float s = 0.45f * (1.4f - 0.4f * k);   // 조여 오는 십자
            crossA.SetPosition(0, at + new Vector3(-s, 0f)); crossA.SetPosition(1, at + new Vector3(s, 0f));
            crossB.SetPosition(0, at + new Vector3(0f, -s)); crossB.SetPosition(1, at + new Vector3(0f, s));
            Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
            ship.z = 0f;
            aimLine.SetPosition(0, ship);
            aimLine.SetPosition(1, at);
        }
        Vector3 start = from + (at - at0);   // 따라가는 적이 움직여도 하늘 쪽 출발점도 같이 (곧게 보여요)
        rockRoot.transform.position = Vector3.Lerp(start, at, k * k);   // 점점 빨라지며 떨어져요
        rock.transform.Rotate(0f, 0f, 500f * Time.deltaTime);
        if(k >= 1f) Land(at);
    }

    void Land(Vector3 at)
    {
        float r = o.radius;
        RemasterFx.Burst(at, r, Fire);
        RemasterFx.Burst(at, r * 0.45f, Color.white);
        RemasterFx.Ring(at, r * 1.25f, new Color(1f, 0.75f, 0.4f), 0.3f);
        if(o.big) { RemasterFx.Ring(at, r * 1.8f, Color.white, 0.35f); RemasterFx.Spark(at, r, new Color(1f, 0.85f, 0.4f)); }
        if(o.brand) RemasterFx.Spark(at, r * 0.9f, new Color(1f, 0.4f, 0.35f));
        if(o.sound && SoundManager.Instance != null) SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.BomberExplosion);
        var victim = o.follow;
        bool victimAlive = Alive(victim);   // 이 운석이 죽였을 때만 낙인이 옮겨 가요 (같은 적에 여러 발이 떨어지니까)
        bool any = Blast(at, r, o.dmg, o);
        if(any) AugmentManager.WeaponHitEvent(SideWeapon.Meteor);   // 합체 종결 게이지
        o.onLand?.Invoke(at);

        // 처형 낙인: 낙인 적이 죽었으면 가까운 다른 적에게 낙인이 옮겨 가요
        if(o.brand && o.chain > 0 && victimAlive && !Alive(victim)) {
            var near = AugmentManager.NearestInRange(at, 1, new HashSet<Enemy> { victim });
            if(near.Count > 0) {
                var n = near[0];
                Drop(new Opts {
                    at = n.pos, follow = n, dmg = o.dmg, radius = o.radius, owner = o.owner, brand = true, forceCrit = o.forceCrit,
                    critExtra = o.critExtra, bossBonus = o.bossBonus, chain = o.chain - 1, pen = o.pen, sound = false,
                    fallTime = Tuning.Get("brand_chain_fall", 0.3f), wellRadius = o.wellRadius, wellTime = o.wellTime,
                });
            }
        }

        // 운석은 꼬리가 다 사라지면 저절로 지워져요
        rock.enabled = glow.enabled = false;
        if(rockRoot.TryGetComponent(out TrailRenderer tr)) tr.autodestruct = true;
        else Destroy(rockRoot);
        rockRoot = null;
        if(aimLine != null) { Destroy(aimLine.gameObject); Destroy(crossA.gameObject); Destroy(crossB.gameObject); }

        float burn = o.owner != null ? o.owner.BurnMul : 0f;
        enabled = false;   // Update 는 그만 (뒤는 코루틴)
        if(o.wellRadius > 0f) StartCoroutine(Well(at, burn));
        else if(burn > 0f) StartCoroutine(BurnZone(at, WeaponBase.ClampDamage(o.dmg * (double)burn), true));
        else Destroy(gameObject);
    }

    // 반경 안 적·더미에게 한 번씩. 맞힌 적이 있으면 true
    static bool Blast(Vector3 p, float r, long dmg, Opts o)
    {
        bool crit = o.forceCrit || Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        float critDmg = Player.instance.FinalCriticalDamage + o.critExtra;
        float pen = o.pen >= 0f ? o.pen : Armor.Pen(TraitsData.WeaponType.Meteor);
        bool any = false;
        var done = new HashSet<GameObject>();
        foreach(var c in Physics2D.OverlapCircleAll(p, r)) {
            if(c == null || !done.Add(c.gameObject)) continue;
            if(c.TryGetComponent(out Enemy e)) {
                if(e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) continue;
                AugmentManager.WeaponHit(e, o.bossBonus ? SniperBullet.BossBonus(e, dmg) : dmg, crit, critDmg, HitKind.Weapon, pen);
                any = true;
            } else if(c.TryGetComponent(out Dummy d)) {
                d.addDamage(dmg, crit, critDmg);
                any = true;
            }
        }
        return any;
    }

    // 대충돌 중력 우물: wellTime 동안 반경 안 적을 가운데 둘레로 끌어모아요 (블랙홀과 같은 방식, 보스는 약하게)
    IEnumerator Well(Vector3 at, float burn)
    {
        float wr = o.wellRadius, life = Mathf.Max(0.1f, o.wellTime);
        var fx = BlackHole.Visual(at, 0f);
        warnOuter.enabled = false;
        warnInner.color = new Color(0.6f, 0.35f, 1f, 0.16f);
        warnInner.transform.localScale = Vector3.one * wr * 2f;
        long tick = WeaponBase.ClampDamage(o.dmg * (double)burn);
        float next = 0f;
        for(float t2 = 0f; t2 < life; t2 += Time.deltaTime) {
            float open = Mathf.Clamp01(t2 / 0.15f), close = Mathf.Clamp01((life - t2) / 0.15f);
            if(fx != null) fx.transform.localScale = Vector3.one * wr * 0.9f * Mathf.Min(open, close);
            BlackHole.PullToSlots(at, AugmentManager.EnemiesNear(at, wr), wr * 0.22f, Tuning.Get("impact_pull", 3f), t2, Time.deltaTime);
            if(burn > 0f && t2 >= next) { next += 0.5f; Blast(at, o.radius * 0.85f, tick, o); }
            yield return null;
        }
        if(fx != null) Destroy(fx);
        Destroy(gameObject);
    }

    // 불타는 구덩이: meteor_burn_time 초 동안 0.5초마다 반경 ×0.85 안을 지져요. 일렁이다 사라져요
    IEnumerator BurnZone(Vector3 at, long tick, bool show)
    {
        float r = o.radius;
        warnInner.color = new Color(Fire.r, Fire.g, Fire.b, 0.35f);
        warnOuter.enabled = false;
        float life = Tuning.Get("meteor_burn_time", 2f), br = r * 0.85f, next = 0f;
        warnInner.transform.localScale = Vector3.one * br * 2f;
        for(float t2 = 0f; t2 < life; t2 += Time.deltaTime) {
            float a = 0.3f * (1f - t2 / life) + 0.06f * Mathf.Sin(t2 * 18f);
            if(show) warnInner.color = new Color(Fire.r, Fire.g * 0.8f, Fire.b, Mathf.Max(0f, a));
            if(t2 >= next) {
                next += 0.5f;
                Blast(at, br, tick, o);
                if(Random.value < 0.7f) RemasterFx.Burst(at + (Vector3)(Random.insideUnitCircle * br * 0.6f), br * 0.35f, Fire);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

// 대충돌 중력 우물 (2026-11-21): 운석보다 먼저 열려 life 초 동안 반경 안 적을 가운데 둘레로 끌어모아요 (블랙홀과 같은 방식, 보스는 약하게).
// 블랙홀 그림 + 흐린 보라 범위 원. 열릴 때·닫힐 때 0.15초 동안 커지고 작아져요
public class MeteorWell : MonoBehaviour
{
    float radius, life, age;
    bool closing;
    GameObject fx;
    SpriteRenderer area;

    public static MeteorWell Open(Vector3 at, float radius, float life)
    {
        at.z = 0f;
        var go = new GameObject("MeteorWell");
        go.transform.position = at;
        var w = go.AddComponent<MeteorWell>();
        w.radius = radius;
        w.life = Mathf.Max(0.1f, life);
        w.fx = BlackHole.Visual(at, 0f);
        w.fx.transform.SetParent(go.transform, true);
        var ag = new GameObject("Area");
        ag.transform.SetParent(go.transform, false);
        w.area = ag.AddComponent<SpriteRenderer>();
        w.area.sprite = RemasterFx.CircleSprite;
        w.area.color = new Color(0.6f, 0.35f, 1f, 0.14f);
        w.area.sortingLayerName = "Mine";
        w.area.sortingOrder = 0;
        ag.transform.localScale = Vector3.zero;
        RemasterFx.Ring(at, radius, new Color(0.7f, 0.4f, 1f), 0.25f);
        return w;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float open = Mathf.Clamp01(age / 0.15f), close = Mathf.Clamp01((life - age) / 0.15f), k = Mathf.Min(open, close);
        if(fx != null) fx.transform.localScale = Vector3.one * radius * 0.9f * k;
        area.transform.localScale = Vector3.one * radius * 2f * k;
        Vector3 c = transform.position;
        if(!closing) BlackHole.PullToSlots(c, AugmentManager.EnemiesNear(c, radius), radius * 0.22f, Tuning.Get("impact_pull", 3f), age, dt);
        if(age >= life) Destroy(gameObject);
    }

    // 바로 닫기: 더 안 끌어당기고 0.15초 동안 작아지며 사라져요
    public void Close()
    {
        if(closing) return;
        closing = true;
        life = Mathf.Min(life, age + 0.15f);
    }
}

// 큰 운석이 모인 적을 튕겨 내는 움직임 (0.25초 동안 바깥으로, 점점 느려지게)
public class MeteorScatter : MonoBehaviour
{
    readonly List<(Enemy e, Vector3 dir, float dist)> items = new List<(Enemy, Vector3, float)>();
    float t, Dur = 0.25f;

    // away: 이 방향(단위 벡터)으로는 안 밀려요 (그쪽으로 가는 적은 거울처럼 꺾어서). jitter: 적마다 거리를 (1 - jitter) ~ 1 배로 흩어요
    public void Run(Vector3 c, List<Enemy> enemies, float dist, float dur = 0.25f, Vector3? away = null, float jitter = 0f)
    {
        c.z = 0f;
        Dur = Mathf.Max(0.05f, dur);
        foreach(var e in enemies) {
            if(e == null) continue;
            Vector3 d = e.transform.position - c;
            d.z = 0f;
            if(d.sqrMagnitude < 0.0001f) d = Random.insideUnitCircle.normalized;
            d.Normalize();
            if(away.HasValue) {
                Vector3 a = away.Value;
                float dot = Vector3.Dot(d, a);
                if(dot > 0f) d = (d - a * (2f * dot)).normalized;   // 우주선 쪽이면 반대쪽으로 꺾어요
            }
            float k = jitter > 0f ? Random.Range(1f - jitter, 1f) : 1f;
            items.Add((e, d, dist * k * (e.IsBoss ? 0.3f : 1f)));
        }
    }

    void Update()
    {
        float dt = Time.deltaTime, k0 = t / Dur;
        t += dt;
        float k1 = Mathf.Clamp01(t / Dur);
        float step = (1f - (1f - k1) * (1f - k1)) - (1f - (1f - k0) * (1f - k0));   // 처음에 빠르고 점점 느려지게
        foreach(var it in items) {
            if(it.e == null || it.e._state == Enemy.EnemyState.dead || !it.e.gameObject.activeInHierarchy) continue;
            it.e.transform.position += it.dir * it.dist * step;
        }
        if(t >= Dur) Destroy(gameObject);
    }
}
