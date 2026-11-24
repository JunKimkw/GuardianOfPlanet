using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 블랙홀 (2026-11-20, 예전 "직선 레이저" 자리): 몇 초마다 적이 가장 많이 모인 곳에 블랙홀을 열어요.
// 열려 있는 동안 반경 안의 적을 가운데로 빨아들이며 계속 피해를 주고, 닫힐 때 붕괴하며 한 번 크게 터져요.
// 레벨·상점·저장 id 는 직선 레이저 것을 그대로 이어 써요 (클래스 이름도 프리팹 연결 때문에 그대로).
// 직선 레이저 빔(프리팹)은 숨겨요. 레일건 합체는 저격총 쪽 코드라 그대로 동작해요.
// 증강: 거대 중력(반경), 중력 유지(시간), 초신성 붕괴(마지막 폭발). 숫자 tuning.txt blackhole_*
public class strLazer : ContactWeapon
{
    public static strLazer instance;

    [HideInInspector] public int strLazerDamage;

    [Header("Lazer Shoot")]
    public ShootLazer shootLazer;   // 예전 직선 레이저 빔 (숨겨요)

    public bool longerLaserTime;    // 패시브 Lv.500: 블랙홀 유지 +1.5초

    float timer;
    bool shooting;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.StrLaser;
    public override SideWeapon Side => SideWeapon.StrLaser;
    public override int BaseDamage => Balanced("blackhole", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)
    protected override float HitInterval => 0.25f;

    public override void SetDamage() => strLazerDamage = Balanced("blackhole", 25f);

    // ---- 블랙홀 숫자 (증강 포함)
    public float Radius {
        get {
            int l = AugmentManager.WL("bh_big");
            return Tuning.Get("blackhole_radius", 1.5f) * (l > 0 ? 1f + AugmentDefs.BhBig(l) : 1f);
        }
    }
    public float Duration {
        get {
            int l = AugmentManager.WL("bh_long");
            return Tuning.Get("blackhole_duration", 3f) + (longerLaserTime ? 1.5f : 0f) + (l > 0 ? AugmentDefs.BhLong(l) : 0f);
        }
    }
    public double CollapseMul {
        get {
            int l = AugmentManager.WL("bh_collapse");
            return Tuning.Get("blackhole_collapse_mul", 4f) * (l > 0 ? 1.0 + AugmentDefs.BhCollapse(l) : 1.0);
        }
    }
    float Interval {
        get {
            float t = Tuning.Get("blackhole_interval", 8f);
            int sg = AugmentManager.WL("sg_charge");   // 특이점 저격 "가속 특이점"
            if(sg > 0 && AugmentManager.Fused(AugmentDefs.FzSingularity)) t = Mathf.Max(2f, t - AugmentDefs.SgCharge(sg));
            return t * (1 - Trait(TraitsData.TraitsType.AttackSpeed)) / AugmentManager.AttackSpeedFactor;
        }
    }

    void Awake()
    {
        instance = this;
        HideOldBeam();
    }

    // 직선 레이저 그림·판정을 꺼요
    void HideOldBeam()
    {
        if(shootLazer != null) {
            shootLazer.isShooting = false;
            shootLazer.enabled = false;
            if(shootLazer.lazerRenderer != null) shootLazer.lazerRenderer.enabled = false;
        }
        foreach(var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        var rb = GetComponent<Rigidbody2D>();
        if(rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        foreach(var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;   // 총 그림도 숨겨요 (블랙홀은 따로 그려요)
    }

    void Update()
    {
        if(!shooting) return;
        // 상시 공격 (2026-11-24 사용자: 블랙홀만 기다리면 적이 가까이 올 때 쓸모가 없어요)
        if(AugmentManager.Fused(AugmentDefs.FzSingularity)) SingularityShots();
        else if(AugmentManager.Fused(AugmentDefs.FzGravBomb)) GravBombBase();
        timer += Time.deltaTime;
        if(timer < Interval) return;
        if(Open()) timer = 0f;
    }

    float baseShotTimer;
    static readonly Color SingShotColor = new Color(0.75f, 0.45f, 1f);

    Vector3 ShipPos {
        get {
            var wm = WeaponManager.instance;
            Vector3 p = wm != null && wm.mainWeapon != null ? wm.mainWeapon.transform.position : transform.position;
            p.z = 0f;
            return p;
        }
    }

    // 특이점 저격 상시 사격: 가장 가까운 적에게 작은 저격탄을 빠르게 계속 (sing_shot_*). 저격탄이라 항상 치명타·보스 추가 피해
    void SingularityShots()
    {
        baseShotTimer += Time.deltaTime * AugmentManager.AttackSpeedFactor;
        if(baseShotTimer < Mathf.Max(0.05f, Tuning.Get("sing_shot_interval", 0.35f) / (1f + AugmentManager.Silver("sg_scope")))) return;   // 실버 "속사 조준경"
        Vector3 from = ShipPos;
        var near = AugmentManager.NearestInRange(from, 1 + AugmentManager.WL("sg_twin"));   // 실버 "연속 조준": 다음으로 가까운 적에게도 (2026-11-24 사용자: 피해%만 말고 추가탄 같은 실버도)
        if(near.Count == 0) return;
        baseShotTimer = 0f;
        foreach(var e in near) SingShot(from, e);
    }

    void SingShot(Vector3 from, Enemy target)
    {
        var b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Sniper).GetComponent<SniperBullet>();
        b.transform.position = from;
        b.transform.rotation = LookRotation(from, target.pos);
        b.isCritical = true;
        b.isStunning = false;
        // (2026-11-24 사용자: 더 길게 + 적 통과) 관통탄, 굵기 sing_shot_size · 길이 sing_shot_length 배
        b.setBulletStats(ClampDamage(HitDamage * (double)Tuning.Get("sing_shot_mul", 0.5f) * (1.0 + AugmentManager.Silver("sg_power"))), true, ExtraCritDamage);
        b.pierceLeft = 0;
        Vector3 sc = b.transform.localScale;
        b.transform.localScale = new Vector3(sc.x * Tuning.Get("sing_shot_size", 0.5f), sc.y * Tuning.Get("sing_shot_length", 1.4f), sc.z);
        b.Tint(SingShotColor);   // 블랙홀 보라색 (기본 저격탄과 구분)
        b.gameObject.SetActive(true);
        RemasterFx.Burst(from, 0.25f, SingShotColor);   // 쏠 때 보라 불꽃
    }

    // 중력 폭탄 상시 공격: 일반 폭격기 폭탄을 가장 가까운 적에게 계속 (gravbomb_base_*)
    void GravBombBase()
    {
        baseShotTimer += Time.deltaTime * AugmentManager.AttackSpeedFactor;
        if(baseShotTimer < Mathf.Max(0.05f, Tuning.Get("gravbomb_base_interval", 0.8f) / (1f + AugmentManager.Silver("gv_support")))) return;   // 실버 "폭격 지원"
        Vector3 from = ShipPos;
        var near = AugmentManager.NearestInRange(from, 1 + AugmentManager.WL("gv_cluster"));   // 실버 "집속 폭격": 다음으로 가까운 적에게도 (2026-11-24 사용자: 피해%만 말고 추가탄 같은 실버도)
        if(near.Count == 0) return;
        baseShotTimer = 0f;
        foreach(var e in near) BaseBomb(from, e.pos);
    }

    void BaseBomb(Vector3 from, Vector3 to)
    {
        to.z = 0f;
        var b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Bomber).GetComponent<BomberBullet>();
        b.transform.position = from;
        b.transform.rotation = LookRotation(from, to);
        b.isCritical = RollCrit(ExtraCritChance);
        b.isStunning = false;
        b.gameObject.SetActive(true);
        b.Launch(ClampDamage(HitDamage * (double)Tuning.Get("gravbomb_base_mul", 0.4f) * (1.0 + AugmentManager.Silver("gv_power"))), to);
    }

    // 화면 안에서 적이 가장 많이 모인 곳에 열어요. 적이 없으면 false (다음 프레임에 다시)
    // 2026-11-20: 우주선 바로 옆에 열리면 손해라서 (곧 닿아 사라질 적만 잡고, 둘레 자리가 우주선과 겹쳐요)
    //   우주선에서 blackhole_min_dist 칸 밖의 무리를 먼저 골라요. 가까운 적밖에 없으면 그 적 뒤쪽(우주선 반대)으로 밀어서 열어요
    //   → 적이 우주선에서 멀어지는 쪽으로 빨려 가요
    bool Open()
    {
        var wm = WeaponManager.instance;
        if(wm == null || wm.mainWeapon == null) return false;
        Vector3 ship = wm.mainWeapon.transform.position;
        ship.z = 0f;
        float minDist = Tuning.Get("blackhole_min_dist", 2.5f), r = Radius;

        var all = AugmentManager.NearestInRange(ship, 999);
        if(all.Count == 0) return false;
        // 특이점 저격 (2026-11-22 사용자: "블랙홀이 계속 소환돼서 적이 한번 뭉치면 계속 뭉쳐"):
        //  직전 블랙홀 자리(singularity_avoid 반경 배수 안)는 먼저 피해요. 거기밖에 없으면 그래도 열어요
        bool sing = AugmentManager.Fused(AugmentDefs.FzSingularity);
        float avoid = r * Tuning.Get("singularity_avoid", 1.5f);
        Enemy best = null, bestAny = null;
        int bestCount = 0, bestAnyCount = 0;
        foreach(var e in all) {
            Vector3 p = e.pos; p.z = 0f;
            if((p - ship).magnitude < minDist) continue;
            int n = 0;
            foreach(var o in all) if((o.pos - e.pos).sqrMagnitude <= r * r) n++;
            if(n > bestAnyCount) { bestAnyCount = n; bestAny = e; }
            if(sing && hasLast && (p - lastAt).magnitude < avoid) continue;
            if(n > bestCount) { bestCount = n; best = e; }
        }
        if(best == null) best = bestAny;

        Vector3 at;
        if(best != null) {
            at = best.pos;
        } else {
            // 모두 가까이 있으면: 가장 가까운 적에서 우주선 반대쪽으로 밀어 최소 거리에 열어요
            Vector3 d = all[0].pos - ship; d.z = 0f;
            if(d.sqrMagnitude < 0.0001f) d = Vector3.up;
            at = ship + d.normalized * minDist;
        }
        if(AugmentManager.Fused(AugmentDefs.FzGravBomb)) {
            // 중력 폭탄 (2026-11-22): 블랙홀을 바로 열지 않고 블랙홀탄을 쏴요. 맞은 자리에 블랙홀 → 닫힐 때 거대 폭탄
            int ml = AugmentManager.WL("gv_more");
            int shells = 1 + (ml > 0 ? AugmentDefs.GvMore(ml) : 0);
            if(Tuning.GetInt("gravbomb_multi", 2) == 2) {   // 가까운 적 순 (기본)
                NearShells(ship, all, shells, r, minDist);
                lastAt = at;
                hasLast = true;
                return true;
            }
            Enemy tgt = best;
            if(Tuning.GetInt("gravbomb_target", 0) == 1) tgt = all[0];   // 1 = 가장 가까운 적
            Vector3 goal = tgt != null ? tgt.pos : at;
            goal.z = 0f;
            GravShell.Fire(this, ship, tgt, goal);
            if(shells > 1) ExtraShells(ship, tgt, goal, shells - 1, all, r, minDist);
        } else BlackHole.Spawn(this, at);
        lastAt = at;
        hasLast = true;
        return true;
    }
    Vector3 lastAt;
    bool hasLast;

    // 중력 폭탄 "블랙홀탄 추가" (2026-11-22 사용자: 고리 폭발 대신 폭격기처럼 여러 개). 방식은 gravbomb_multi 로 골라요 (사용자 고민 중):
    //  0 = 폭격기처럼 부채꼴: 첫 탄 방향에서 ±gravbomb_fan_angle 도씩 벌려 같은 거리에 떨어져요 (적이 없는 곳에도 열려서 덜 묶어요)
    //  1 = 무리 순: 아직 안 고른 무리 중 많은 순으로 (서로 블랙홀 반경 × 2 이상 떨어진 곳). 모자라면 부채꼴로.
    //      여러 무리를 동시에 붙잡아서 적이 우주선에 거의 못 오게 될 수 있어요
    // 2 = 가까운 적 순 (2026-11-22 사용자: 부채꼴은 가까운 적을 처리를 못해요): 모든 탄이 우주선에서 가까운 적부터,
    //  이미 고른 자리와 블랙홀 반경 × 2 이상 떨어진 적만 (한 무리에 여러 개 안 겹치게). 모자라면 남은 가까운 적에게.
    //  우주선에서 blackhole_min_dist 칸 안의 적이면 그 적 뒤쪽(우주선 반대)으로 min_dist 칸 자리에 열어서, 적이 우주선에서 멀어지는 쪽으로 빨려 가요
    void NearShells(Vector3 ship, List<Enemy> all, int count, float r, float minDist)
    {
        var picked = new List<Vector3>();
        var used = new HashSet<Enemy>();
        for(int pass = 0; pass < 2 && picked.Count < count; pass++) {
            foreach(var e in all) {   // all 은 가까운 순
                if(picked.Count >= count) break;
                if(used.Contains(e)) continue;
                Vector3 q = e.pos; q.z = 0f;
                Vector3 d = q - ship;
                bool close = d.magnitude < minDist;
                if(close) q = ship + (d.sqrMagnitude < 0.0001f ? Vector3.up : d.normalized) * minDist;
                if(pass == 0) {
                    bool apart = true;
                    foreach(var pk in picked) if((pk - q).magnitude < r * 2f) { apart = false; break; }
                    if(!apart) continue;
                }
                used.Add(e);
                picked.Add(q);
                GravShell.Fire(this, ship, close ? null : e, q);
            }
        }
    }

    void ExtraShells(Vector3 ship, Enemy first, Vector3 goal, int extra, List<Enemy> all, float r, float minDist)
    {
        int left = extra;
        if(Tuning.GetInt("gravbomb_multi", 0) == 1) {
            var picked = new List<Vector3> { goal };
            var scored = new List<(Enemy e, int n)>();
            foreach(var e in all) {
                Vector3 q = e.pos; q.z = 0f;
                if(e == first || (q - ship).magnitude < minDist) continue;
                int n = 0;
                foreach(var o in all) if((o.pos - e.pos).sqrMagnitude <= r * r) n++;
                scored.Add((e, n));
            }
            scored.Sort((x, y) => y.n.CompareTo(x.n));
            foreach(var sc in scored) {
                if(left <= 0 || sc.n < 2) break;
                Vector3 q = sc.e.pos; q.z = 0f;
                bool apart = true;
                foreach(var pk in picked) if((pk - q).magnitude < r * 2f) { apart = false; break; }
                if(!apart) continue;
                picked.Add(q);
                GravShell.Fire(this, ship, sc.e, q);
                left--;
            }
        }
        // 부채꼴 (폭격기처럼): 첫 탄 방향 기준으로 오른쪽·왼쪽 번갈아 gravbomb_fan_angle 도씩 벌려요
        Vector3 d = goal - ship;
        float dist = Mathf.Max(minDist, d.magnitude);
        float baseAng = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, fan = Tuning.Get("gravbomb_fan_angle", 25f);
        for(int i = 0; i < left; i++) {
            float a = (baseAng + fan * (i / 2 + 1) * (i % 2 == 0 ? 1f : -1f)) * Mathf.Deg2Rad;
            GravShell.Fire(this, ship, null, ClampToScreen(ship + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * dist));
        }
    }

    static Vector3 ClampToScreen(Vector3 p)
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize - 0.6f, w = cam.orthographicSize * cam.aspect - 0.6f;
        Vector3 c = cam.transform.position;
        return new Vector3(Mathf.Clamp(p.x, c.x - w, c.x + w), Mathf.Clamp(p.y, c.y - h, c.y + h), 0f);
    }

    // 특이점 저격 "여진": 저격에 밀린 적이 자리 잡을 때까지 singularity_echo_delay 초 기다렸다가, 그때 적이 가장 몰린 곳에 작은 블랙홀
    //  (우주선에서 blackhole_min_dist 칸 밖. 그런 무리가 없으면 원래 자리)
    public IEnumerator EchoLater(Vector3 fallback)
    {
        yield return new WaitForSeconds(Tuning.Get("singularity_echo_delay", 0.35f));
        BlackHole.Spawn(this, CrowdSpot(Radius * 0.6f) ?? fallback, 0.6f, 1.5f, true);
    }

    // 화면 안에서 반경 r 안 적이 가장 많은 적 자리 (우주선에서 blackhole_min_dist 칸 밖만). 없으면 null
    Vector3? CrowdSpot(float r)
    {
        var wm = WeaponManager.instance;
        if(wm == null || wm.mainWeapon == null) return null;
        Vector3 ship = wm.mainWeapon.transform.position;
        ship.z = 0f;
        float minDist = Tuning.Get("blackhole_min_dist", 2.5f);
        var all = AugmentManager.NearestInRange(ship, 999);
        Enemy best = null;
        int bestCount = 0;
        foreach(var e in all) {
            Vector3 q = e.pos; q.z = 0f;
            if((q - ship).magnitude < minDist) continue;
            int n = 0;
            foreach(var o in all) if((o.pos - e.pos).sqrMagnitude <= r * r) n++;
            if(n > bestCount) { bestCount = n; best = e; }
        }
        if(best == null) return null;
        Vector3 at = best.pos;
        at.z = 0f;
        return at;
    }

    public override void NormalShooting() => shooting = true;
    public override void AutoShooting() => shooting = true;
    public override void DonShoot()
    {
        shooting = false;
        timer = 0f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        timer = 0f;
    }
}

// 블랙홀 하나: 열림(커짐) → 빨아들이며 피해 → 붕괴 폭발
public class BlackHole : MonoBehaviour
{
    strLazer owner;
    float radius, duration, age, tickTimer;
    bool echo;   // 특이점 저격 "여진"으로 다시 열린 작은 블랙홀 (또 여진을 만들지 않아요)
    static Material aimMat;
    LineRenderer aim;   // 특이점 저격: 닫히기 직전 빨간 조준선 (저격총처럼)
    SpriteRenderer disk, core;
    static readonly Color Purple = new Color(0.7f, 0.35f, 1f);

    public static void Spawn(strLazer owner, Vector3 pos, float radiusMul = 1f, float durationOverride = -1f, bool echo = false)
    {
        var go = new GameObject("BlackHole");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var b = go.AddComponent<BlackHole>();
        b.owner = owner;
        b.radius = owner.Radius * radiusMul;
        b.duration = durationOverride > 0f ? durationOverride : owner.Duration;
        b.echo = echo;
        b.disk = MakeSprite(go.transform, "Disk", BlackHoleArt.Disk, 1);
        b.core = MakeSprite(go.transform, "Core", BlackHoleArt.Core, 2);
        go.transform.localScale = Vector3.zero;
    }

    // 적들을 구멍 둘레의 각자 자리로 끌어당겨 함께 돌게 해요 (블랙홀·종결 증강 공용).
    // 끌려가는 동안엔 우주선에 닿아도 피해를 안 줘요 (Enemy.gravityUntil). 보스는 blackhole_boss_pull 만큼만 끌려와요
    public static void PullToSlots(Vector3 c, List<Enemy> enemies, float hold, float pull, float age, float dt)
    {
        // 2026-11-25 사용자: "보스가 왜 아직도 밀려나가" → 보스는 넉백처럼 끌어당기기에도 안 움직여요 (예전 0.3)
        float bossPull = Tuning.Get("blackhole_boss_pull", 0f);
        foreach(var e in enemies) {
            if(e == null || e._state == Enemy.EnemyState.dead) continue;
            if(e.IsBoss && bossPull <= 0f) continue;
            Vector3 p = e.transform.position;
            float k = e.IsBoss ? bossPull : 1f;
            int id = e.GetInstanceID() & 0x7fffffff;
            float ang = (id % 360) * Mathf.Deg2Rad + age * 1.5f;              // 적마다 다른 각도, 천천히 함께 돌아요
            float dist = hold * (0.7f + 0.15f * (id / 360 % 5));             // 적마다 조금씩 다른 거리 (겹침 줄이기)
            Vector3 slot = c + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * dist;
            e.transform.position = Vector3.MoveTowards(p, new Vector3(slot.x, slot.y, p.z), pull * k * dt);
            e.gravityUntil = Time.time + 0.3f;
        }
    }

    // 종결 증강 블랙홀 자리: 우주선에서 화면 가운데 쪽으로, 최소 minDist 칸 떨어진 곳 (화면 안)
    public static Vector3 AwayFromShip(float minDist)
    {
        var cam = GameManager.instance.mainCamera;
        float hh = cam.orthographicSize, hw = hh * cam.aspect;
        Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
        ship.z = 0f;
        Vector3 d = -ship;
        if(d.sqrMagnitude < 0.25f) d = Vector3.up;
        Vector3 c = ship + d.normalized * Mathf.Max(minDist, d.magnitude);
        return new Vector3(Mathf.Clamp(c.x, -hw + 1f, hw - 1f), Mathf.Clamp(c.y, -hh + 1f, hh - 1f), 0f);
    }

    // 그림만 (종결 증강 연출용). 크기는 지름 = localScale
    public static GameObject Visual(Vector3 pos, float diameter)
    {
        var go = new GameObject("BlackHoleFx");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var d = MakeSprite(go.transform, "Disk", BlackHoleArt.Disk, 1);
        MakeSprite(go.transform, "Core", BlackHoleArt.Core, 2);
        go.AddComponent<Spinner>().target = d.transform;
        go.transform.localScale = Vector3.one * diameter;
        return go;
    }

    class Spinner : MonoBehaviour
    {
        public Transform target;
        void Update() { if(target != null) target.Rotate(0f, 0f, -220f * Time.deltaTime); }
    }

    static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sp, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingLayerName = "Mine";   // 배경(Map) 위, 적(Enemy) 아래
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        if(owner == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;

        // 그림: 0.3초에 열리고, 마지막 0.3초에 빨려 들어가듯 작아져요. 원반은 빙글빙글
        float open = Mathf.Clamp01(age / 0.3f), close = Mathf.Clamp01((duration - age) / 0.3f);
        float s = radius * 2f * Mathf.Min(open, close);   // 그림 지름 = 반경 × 2 (그림 1칸 = 지름 1)
        transform.localScale = new Vector3(s, s, 1f);
        disk.transform.Rotate(0f, 0f, -220f * dt);
        core.transform.localScale = Vector3.one * (0.9f + 0.08f * Mathf.Sin(age * 12f));

        // 빨아들이기: 반경 안의 적을 구멍 둘레로 (보스는 약하게).
        // 2026-11-20: 예전엔 정가운데 한 점으로 끌어서 끝나면 적들이 완전히 포개져 몇 마리인지 안 보였어요.
        // 이제 적마다 정해진 각도·거리의 "자리"로 끌려가서 구멍 둘레에 흩어져 돌아요.
        Vector3 c = transform.position;
        float hold = radius * Tuning.Get("blackhole_hold", 0.25f);   // 둘레 자리 거리 (반경 대비)
        PullToSlots(c, AugmentManager.EnemiesNear(c, radius), hold, Tuning.Get("blackhole_pull", 2.5f), age, dt);

        // 피해: 일정 간격으로 반경 안 모든 적
        tickTimer -= dt;
        if(tickTimer <= 0f) {
            tickTimer = Mathf.Max(0.05f, Tuning.Get("blackhole_tick", 0.25f));
            int bl = AugmentManager.WL("bh_big");   // 거대 중력: 빨아들이는 피해 + (2026-11-22 증강 하나씩 측정 → 조정안 v3 (탄 개수는 그대로, 피해만))
            long dmg = WeaponBase.ClampDamage(owner.HitDamage * (double)Tuning.Get("blackhole_tick_mul", 1f) * (bl > 0 ? 1.0 + AugmentDefs.BhBigDamage(bl) : 1.0));
            foreach(var e in AugmentManager.EnemiesNear(c, radius)) Hit(e, dmg);
        }

        // 특이점 저격: 닫히기 직전 저격총처럼 빨간 조준선이 블랙홀을 겨눠요 (2026-11-20)
        if(AugmentManager.Fused(AugmentDefs.FzSingularity) && age >= duration - Tuning.Get("singularity_aim_time", 1.2f)) DrawAim(c);   // 2026-11-22 사용자: 빨간 줄을 더 빨리 (0.4 → 1.2초 전부터)

        if(age >= duration) Collapse();
    }

    // pen: 방어 관통력 (음수면 블랙홀 관통력, 특이점 저격은 저격총 관통력)
    void Hit(Enemy e, long dmg, bool forceCrit = false, float pen = -1f)
    {
        if(e == null || e._state == Enemy.EnemyState.dead) return;
        bool crit = forceCrit || Random.Range(1, 101) <= Player.instance.FinalCriticalChance + owner.ExtraCritChance;
        AugmentManager.WeaponHit(e, dmg, crit, Player.instance.FinalCriticalDamage + owner.ExtraCritDamage, HitKind.Weapon,
                                 pen >= 0f ? pen : Armor.Pen(TraitsData.WeaponType.StrLaser), SideWeapon.StrLaser);
        AugmentManager.WeaponHitEvent(SideWeapon.StrLaser);   // 합체 종결 증강 게이지
    }

    // 붕괴: 반경 안을 한 번 크게 때려요. 합체하면 저격(특이점 저격) 또는 거대 폭탄(중력 폭탄)
    void Collapse()
    {
        Vector3 c = transform.position;
        var near = AugmentManager.EnemiesNear(c, radius);
        int gathered = near.Count;   // 모인 적 수 (많을수록 세져요)
        // (2026-11-23 사용자: 합체는 거의 종결 무기라 어떤 상황에서도 쓸 만하게 (상황별 차이 줄이기)) 보스는 모인 적 fusion_boss_gather(6)마리로 셈 (예전엔 보스 혼자면 보너스가 거의 없었어요)
        if(near.Exists(e => e != null && e.IsBoss)) gathered += Mathf.Max(0, Tuning.GetInt("fusion_boss_gather", 6) - 1);
        if(AugmentManager.Fused(AugmentDefs.FzSingularity)) Snipe(c, gathered);
        else if(AugmentManager.Fused(AugmentDefs.FzGravBomb)) GravBombFx.Drop(owner, c, radius, gathered);
        else {
            float r = radius * Tuning.Get("blackhole_collapse_radius", 0.8f) * (AugmentManager.WL("bh_collapse") > 0 ? 1.25f : 1f);
            RemasterFx.Burst(c, r, Purple);
            long dmg = WeaponBase.ClampDamage(owner.HitDamage * owner.CollapseMul);
            foreach(var e in AugmentManager.EnemiesNear(c, r)) Hit(e, dmg);
        }
        Destroy(gameObject);
    }

    void DrawAim(Vector3 c)
    {
        if(aim == null) {
            var go = new GameObject("Aim");
            go.transform.SetParent(transform, false);
            aim = go.AddComponent<LineRenderer>();
            aim.positionCount = 2;
            aim.useWorldSpace = true;
            if(aimMat == null) aimMat = new Material(Shader.Find("Sprites/Default"));   // 블랙홀마다 새 재질을 만들면 쌓여요 (2026-11-21 점검)
            aim.sharedMaterial = aimMat;
            aim.sortingLayerName = RemasterFx.FxLayer;
            aim.sortingOrder = 520;
            aim.startWidth = aim.endWidth = 0.03f;
            aim.startColor = aim.endColor = new Color(1f, 0.2f, 0.25f, 0.9f);
        }
        Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
        ship.z = 0f;
        aim.SetPosition(0, ship);
        aim.SetPosition(1, c + (c - ship).normalized * 30f);
    }

    // 특이점 저격: 우주선에서 구멍 한가운데를 꿰뚫는 한 발. 구멍에 모인 적 + 빔 위의 적 모두 치명타
    void Snipe(Vector3 c, int gathered)
    {
        int mark = AugmentManager.WL("sg_mark");
        double per = Tuning.Get("singularity_per_enemy", 0.1f) + (mark > 0 ? AugmentDefs.SgMark(mark) : 0f);
        int counted = Mathf.Min(gathered, Tuning.GetInt("singularity_bonus_cap", 10));   // 보너스는 이 수까지만 (2026-11-20, 많이 모이면 너무 셌어요)
        long dmg = WeaponBase.ClampDamage(owner.HitDamage * (double)Tuning.Get("singularity_mul", 7f) * (1.0 + per * counted));
        Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
        ship.z = 0f;
        Vector3 dir = c - ship;
        if(dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
        dir.Normalize();
        const float len = 30f;
        // 판정 폭과 그림 폭 (2026-11-21 사용자 요청: 빔이 너무 얇아 보여서 그림을 판정 폭에 맞게 굵게. 예전 그림 0.07)
        float width = Tuning.Get("singularity_hit_width", 0.6f), beamW = Tuning.Get("singularity_beam_width", 0.45f);
        // 저격총처럼: 총구 불꽃 → 가늘고 밝은 탄도선 "탕" → 맞은 자리 불꽃 (예전엔 보라색 굵은 빔이라 레이저 같았어요)
        var gold = new Color(1f, 0.93f, 0.65f);
        RemasterFx.Burst(ship + dir * 0.35f, 0.25f, gold);
        RemasterFx.Beam(ship, ship + dir * len, gold, beamW);
        RemasterFx.Beam(ship, ship + dir * len, new Color(1f, 1f, 1f, 0.9f), beamW * 0.35f);
        RemasterFx.Burst(c, 0.6f, gold);
        RemasterFx.Ring(c, radius * 0.8f, Purple, 0.25f);
        var done = new HashSet<Enemy>();
        float sniperPen = Armor.Pen(TraitsData.WeaponType.SniperGun);
        var gatheredList = AugmentManager.EnemiesNear(c, radius);
        foreach(var e in gatheredList) if(done.Add(e)) Hit(e, dmg, true, sniperPen);
        // 2026-11-22 사용자: 저격총에 맞으면 탄이 날아가는 쪽(우주선 반대쪽)으로 부채꼴로 퍼지며 singularity_push 칸 밀려요 (적마다 70~100%, 보스는 30%)
        //  (사방 2.5칸 → 다음 블랙홀이 다시 모음, 한 방향 1.5칸 → 같이 밀려서 뭉친 채라, 부채꼴로 바꿨어요)
        //  밀어내는 중심을 블랙홀보다 singularity_fan_back 칸 뒤(우주선 쪽)에 두면, 무리의 왼쪽은 왼쪽 앞, 오른쪽은 오른쪽 앞으로 갈라져요.
        //  값이 작을수록 부채가 넓어요. 우주선 쪽으로 가는 적은 반대로 꺾어요
        var alive = gatheredList.FindAll(e => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy);
        if(alive.Count > 0)
            new GameObject("SingularityPush").AddComponent<MeteorScatter>().Run(c - dir * Tuning.Get("singularity_fan_back", 0.5f), alive, Tuning.Get("singularity_push", 2f), 0.25f, -dir, 0.3f);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        foreach(var col in Physics2D.OverlapBoxAll(ship + dir * (len / 2f), new Vector2(width, len), ang))
            if(col != null && col.TryGetComponent(out Enemy e) && done.Add(e)) Hit(e, dmg, true, sniperPen);
        // 여진: 확률로 작은 블랙홀이 다시 열려요
        int echoL = AugmentManager.WL("sg_echo");
        if(!echo && echoL > 0 && Random.value < AugmentDefs.SgEcho(echoL))
        {
            // 2026-11-22 사용자: 저격에 적이 밀려나서 같은 자리에 열면 의미가 없어요 → 밀린 뒤 적이 가장 몰린 곳에 열어요
            if(owner.isActiveAndEnabled) owner.StartCoroutine(owner.EchoLater(c));
            else Spawn(owner, c, 0.6f, 1.5f, true);
        }
    }
}

// 중력 폭탄: 블랙홀이 닫힌 자리에 거대 폭탄이 떨어져 터지고, 고리 폭발이면 둘레에 작은 폭발
public class GravBombFx : MonoBehaviour
{
    public static void Drop(strLazer owner, Vector3 c, float holeRadius, int gathered)
    {
        var go = new GameObject("GravBomb");
        go.transform.position = c;
        go.AddComponent<GravBombFx>().StartCoroutine(Run(go, owner, c, holeRadius, gathered));
    }

    static System.Collections.IEnumerator Run(GameObject go, strLazer owner, Vector3 c, float holeRadius, int gathered)
    {
        var orange = new Color(1f, 0.6f, 0.2f);
        int big = AugmentManager.WL("gv_big"), mass = AugmentManager.WL("gv_mass");
        float r = holeRadius * Tuning.Get("gravbomb_radius", 1.3f) * (big > 0 ? 1f + AugmentDefs.GvBig(big) : 1f);
        double per = Tuning.Get("gravbomb_per_enemy", 0.08f) + (mass > 0 ? AugmentDefs.GvMass(mass) : 0f);
        int counted = Mathf.Min(gathered, Tuning.GetInt("gravbomb_bonus_cap", 10));   // 보너스는 이 수까지만
        long dmg = WeaponBase.ClampDamage(owner.HitDamage * (double)Tuning.Get("gravbomb_mul", 14f) * (1.0 + per * counted));
        // 떨어지는 예고 (줄어드는 고리)
        RemasterFx.Ring(c, r, orange, 0.25f);
        yield return new WaitForSeconds(0.25f);
        Boom(owner, c, r, dmg, orange);   // (2026-11-22: "고리 폭발" 증강은 "블랙홀탄 추가"로 바뀌었어요)
        // 2026-11-22 사용자: 특이점 저격처럼 폭발에 맞은 적이 우주선 반대쪽으로 부채꼴로 퍼지며 gravbomb_push 칸 밀려요 (적마다 70~100%, 보스 30%)
        //  중심을 폭발 자리보다 singularity_fan_back 칸 뒤(우주선 쪽)에 둬요. 우주선 쪽으로 가는 적은 반대로 꺾어요
        var wm = WeaponManager.instance;
        Vector3 ship = wm != null && wm.mainWeapon != null ? wm.mainWeapon.transform.position : Vector3.zero;
        ship.z = 0f;
        Vector3 dir = c - ship;
        dir.z = 0f;
        dir = dir.sqrMagnitude < 0.0001f ? Vector3.up : dir.normalized;
        var hitList = AugmentManager.EnemiesNear(c, r).FindAll(e => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy);
        if(hitList.Count > 0)
            new GameObject("GravBombPush").AddComponent<MeteorScatter>().Run(c - dir * Tuning.Get("singularity_fan_back", 0.5f), hitList, Tuning.Get("gravbomb_push", 0.8f), 0.25f, -dir, 0.3f);   // 2026-11-24 사용자: 덜 밀리게 (예전 2, 종결은 그대로 2)
        Destroy(go);
    }

    static void Boom(strLazer owner, Vector3 p, float r, long dmg, Color color)
    {
        RemasterFx.Burst(p, r, color);
        foreach(var e in AugmentManager.EnemiesNear(p, r)) {
            if(e == null || e._state == Enemy.EnemyState.dead) continue;
            AugmentManager.WeaponHit(e, dmg, Random.Range(1, 101) <= Player.instance.FinalCriticalChance + owner.ExtraCritChance,
                                     Player.instance.FinalCriticalDamage + owner.ExtraCritDamage, HitKind.Weapon, Armor.Pen(TraitsData.WeaponType.Bomber), SideWeapon.StrLaser);   // 중력 폭탄 = 폭격기 관통력
            AugmentManager.WeaponHitEvent(SideWeapon.StrLaser);
        }
    }
}

// 블랙홀 그림 (코드로 그려요): 보라색 소용돌이 원반 + 가운데 검은 구멍
public static class BlackHoleArt
{
    static Sprite disk, core;
    public static Sprite Disk => disk != null ? disk : (disk = Make(true));
    public static Sprite Core => core != null ? core : (core = Make(false));

    static Sprite Make(bool isDisk)
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for(int y = 0; y < S; y++) for(int x = 0; x < S; x++) {
            float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            Color c = Color.clear;
            if(isDisk) {
                if(r < 1f) {
                    // 소용돌이 팔 4개 (각도 + 반지름으로 비틀어요)
                    float ang = Mathf.Atan2(dy, dx);
                    float swirl = 0.5f + 0.5f * Mathf.Sin(ang * 4f + r * 9f);
                    float edge = Mathf.Clamp01((1f - r) / 0.35f);                // 바깥으로 갈수록 흐려요
                    float inner = Mathf.Clamp01((r - 0.25f) / 0.2f);             // 가운데는 비워요 (구멍 자리)
                    Color col = Color.Lerp(new Color(0.35f, 0.1f, 0.65f), new Color(0.95f, 0.6f, 1f), swirl * (1f - r * 0.5f));
                    c = new Color(col.r, col.g, col.b, (0.35f + 0.65f * swirl) * edge * inner * 0.9f);
                }
            } else {
                // 검은 구멍 + 얇은 밝은 테두리 (사건의 지평선)
                float hole = 0.3f;
                if(r < hole) c = new Color(0.02f, 0f, 0.05f, 1f);
                else if(r < hole + 0.06f) { float k = 1f - (r - hole) / 0.06f; c = new Color(1f, 0.85f, 1f, k); }
            }
            px[y * S + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);   // 그림 1칸 = 지름 1
    }
}

// 중력 폭탄의 블랙홀탄 (2026-11-22 사용자 결정: "블랙홀을 소환하는 게 아니라 블랙홀 폭격기니까 블랙홀탄을 쏘게").
//  우주선에서 보라빛 검은 탄이 목표 적에게 날아가요 (그 적을 따라가요, 지나가는 길의 다른 적은 무시해요).
//  닿으면 그 자리에 블랙홀이 열리고, 블랙홀이 닫힐 때 예전처럼 거대 폭탄이 터져요 (GravBombFx).
//  목표: gravbomb_target 0 = 가장 많이 몰린 무리 (기본), 1 = 가장 가까운 적. 숫자 gravbomb_shell_*
public class GravShell : MonoBehaviour
{
    strLazer owner;
    Enemy target;
    Vector3 goal;
    float age, speed;
    SpriteRenderer core;
    static Material mat;

    public static void Fire(strLazer owner, Vector3 from, Enemy target, Vector3 goal)
    {
        from.z = goal.z = 0f;
        var go = new GameObject("GravShell");
        go.transform.position = from;
        var s = go.AddComponent<GravShell>();
        s.owner = owner;
        s.target = target;
        s.goal = goal;
        s.speed = Tuning.Get("gravbomb_shell_speed", 9f);
        float size = Tuning.Get("gravbomb_shell_size", 0.4f);
        s.Part("Glow", new Color(0.65f, 0.35f, 1f, 0.55f), size * 1.8f, 522);
        s.core = s.Part("Core", new Color(0.06f, 0.02f, 0.12f, 1f), size, 523);
        var tr = go.AddComponent<TrailRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        tr.sharedMaterial = mat;
        tr.time = 0.25f;
        tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.widthMultiplier = size * 0.8f;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.7f, 0.4f, 1f), 0f), new GradientColorKey(new Color(0.25f, 0.05f, 0.45f), 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.sortingLayerName = RemasterFx.FxLayer;
        tr.sortingOrder = 521;
        tr.Clear();
    }

    SpriteRenderer Part(string name, Color c, float size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RemasterFx.CircleSprite;
        sr.color = c;
        sr.sortingLayerName = RemasterFx.FxLayer;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        age += Time.deltaTime;
        if(target != null) {
            if(target._state != Enemy.EnemyState.dead && target.gameObject.activeInHierarchy) { goal = target.transform.position; goal.z = 0f; }
            else target = null;   // 죽으면 그 자리로
        }
        Vector3 p = transform.position;
        p = Vector3.MoveTowards(p, goal, speed * Time.deltaTime);
        transform.position = p;
        core.transform.localScale = Vector3.one * Tuning.Get("gravbomb_shell_size", 0.4f) * (1f + 0.15f * Mathf.Sin(age * 30f));
        if((p - goal).sqrMagnitude <= 0.01f || age > 4f) {
            if(owner != null) BlackHole.Spawn(owner, p);
            RemasterFx.Ring(p, 0.8f, new Color(0.7f, 0.4f, 1f), 0.25f);
            if(TryGetComponent(out TrailRenderer tr)) tr.autodestruct = true;
            core.enabled = false;
            foreach(var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
            Destroy(gameObject, 1f);
            enabled = false;
        }
    }
}
