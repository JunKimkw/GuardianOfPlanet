using System.Collections.Generic;
using UnityEngine;

// 드론 (예전 "궤도", 2026-11-18 사용자 결정으로 바꿈)
// 예전 궤도는 구슬이 닿아야 때려서, 안 맞거나(작은 판정) 밀기를 넣으면 너무 세지는(못 다가옴) 문제가 있었어요.
// 이제 드론이 우주선 주위를 돌며 가까운 적에게 스스로 탄을 쏴요.
// - 무기 레벨·피해 공식·상점·저장 id 는 예전 궤도 그대로 (프리팹의 구슬 그림과 판정은 숨겨요)
// - 증강: 드론 추가(+대), 속사(공격속도), 충격탄(기절). 합체 "독안개 드론": 독탄 + 드론 주변 독
// - 그림: 코드로 그린 임시 그림 (DroneArt). 숫자는 tuning.txt drone_*
public class Orbital : ContactWeapon
{
    public static Orbital instance;

    [HideInInspector] public int OribitalDamage;

    // 상점 패시브 (이름은 예전 그대로): extraRange = 도는 반경 ×1.2, extraSpeed = 공격속도 ×1.3, ExtraStunPercent = 기절 확률
    public bool extraRange;
    public float ExtraStunPercent;   // % 단위 (15 = +15%)
    public bool extraSpeed;
    public bool hasScaled;

    readonly List<Drone> drones = new List<Drone>();
    float orbitAngle;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.CodeA;
    public override SideWeapon Side => SideWeapon.Orbital;
    public override int BaseDamage => OribitalDamage;
    protected override float HitInterval => 0.15f;

    static bool ToxSat => AugmentManager.Fused(AugmentDefs.FzToxSat);

    public override void SetDamage() =>
        OribitalDamage = Tiered(UpgradeLevel, 100, (0, 7), (50, 8), (100, 6), (200, 7), (300, 6), (500, 2));

    void Awake()
    {
        instance = this;
        HideOldOrb();
    }

    // 예전 궤도 구슬(그림·판정·도는 움직임)은 쓰지 않아요
    void HideOldOrb()
    {
        foreach(var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
        foreach(var col in GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        var rb = GetComponent<Rigidbody2D>();
        if(rb != null) rb.simulated = false;
        var mc = GetComponent<MoveInCircle>();
        if(mc != null) mc.enabled = false;
    }

    // ---- 드론 수·속도
    int DroneCount {
        get {
            int n = 1 + AugmentManager.WL("ob_extra");
            int more = AugmentManager.WL("ts_more");
            if(ToxSat && more > 0) n += AugmentDefs.TsMore(more);
            return n;
        }
    }

    // 쏘는 간격 (초): 기본 × 특성·플레이어 공격속도 ÷ 속사·패시브·증강 공격속도
    float FireInterval {
        get {
            float t = Tuning.Get("drone_fire_interval", 0.6f)
                      * (1f - Trait(TraitsData.TraitsType.AttackSpeed))
                      * (1f - Player.instance.FinalAttack_SpeedPercent);
            int fast = AugmentManager.WL("ob_fast");
            if(fast > 0) t /= 1f + AugmentDefs.ObFast(fast);
            if(extraSpeed) t /= 1.3f;
            t /= AugmentManager.AttackSpeedFactor;
            return Mathf.Max(0.12f, t);
        }
    }

    float StunPercent => ExtraStunPercent;   // 상점 패시브 기절 (증강 "충격탄"은 "추가 발사"로 바뀌었어요)

    void Update()
    {
        var wm = WeaponManager.instance;
        if(wm == null || wm.mainWeapon == null) return;
        int want = DroneCount;
        while(drones.Count < want) drones.Add(Drone.Create(transform.parent));

        Vector3 ship = wm.mainWeapon.transform.position;
        float radius = Tuning.Get("drone_orbit_radius", 2f) * (extraRange ? 1.2f : 1f);
        orbitAngle = Mathf.Repeat(orbitAngle + Time.deltaTime * Tuning.Get("drone_orbit_speed", 1.2f), Mathf.PI * 2f);
        bool canShoot = wm.shootType != WeaponManager.ShootType.DontShoot;
        float interval = FireInterval;
        Color tint = ToxSat ? new Color(0.65f, 1f, 0.5f) : Color.white;

        for(int i = 0; i < drones.Count; i++) {
            var d = drones[i];
            if(d == null) continue;
            bool on = i < want;
            if(d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
            if(!on) continue;
            float a = orbitAngle + Mathf.PI * 2f * i / want;   // 고르게 나눠서 돌아요
            d.transform.position = ship + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            d.SetTint(tint);
            d.Tick(this, interval, canShoot);
        }
    }

    // 드론이 쏠 때 (Drone 이 불러요). 증강 "추가 발사": 탄 +L발이 부채꼴로 더 나가요 (추가 탄 피해 70%)
    public void FireFrom(Vector3 from, Enemy target)
    {
        Vector3 dir = target.pos - from;
        dir.z = 0f;
        if(dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        long dmg = ClampDamage(HitDamage * (double)Tuning.Get("drone_damage_mul", 1f));
        float speed = Tuning.Get("drone_bullet_speed", 7f);
        float critDmg = Player.instance.FinalCriticalDamage + ExtraCritDamage;
        DroneBullet.Spawn(from, dir, speed, dmg, RollCrit(ExtraCritChance), RollStun(StunPercent), critDmg, ToxSat);

        int extra = AugmentManager.WL("ob_multi");
        if(extra <= 0) return;
        long extraDmg = ClampDamage(dmg * (double)Tuning.Get("drone_extra_damage", 0.7f));
        float spread = Tuning.Get("drone_extra_spread", 12f);
        for(int i = 0; i < extra; i++) {
            float ang = (i % 2 == 0 ? 1f : -1f) * spread * (i / 2 + 1);   // +12°, -12°, +24° ...
            Vector3 d = Quaternion.Euler(0f, 0f, ang) * dir;
            DroneBullet.Spawn(from, d, speed, extraDmg, RollCrit(ExtraCritChance), RollStun(StunPercent), critDmg, ToxSat);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        foreach(var d in drones) if(d != null) d.gameObject.SetActive(false);
    }

    // ---- 독안개 드론 (2026-11-18 바꿈): 드론 탄에 맞은 적이 느려지고, 주변 적에게 독이 퍼져요.
    // 예전엔 드론 주변 범위에 들어오면 독이 묻었는데, 범위 표시가 눈 아프고 이상하다고 해서 탄 기준으로 바꿨어요.
    public static void PoisonHit(Enemy e)
    {
        if(e == null || e._state == Enemy.EnemyState.dead) return;
        e.AddPoison(2, 3f);
        e.AddSlow(Tuning.Get("toxsat_slow", 0.3f), Tuning.Get("toxsat_slow_time", 2f));
        int trail = AugmentManager.WL("ts_trail"), ring = AugmentManager.WL("ts_ring");
        float r = Tuning.Get("toxsat_spread_radius", 1f) * (trail > 0 ? 1f + AugmentDefs.TsTrail(trail) : 1f);
        int stacks = 1;
        if(ring > 0) { r = Mathf.Max(r, AugmentDefs.TsRing(ring)); stacks = 3; }   // 역병의 고리: 크게, 3중첩
        Vector3 at = e.transform.position;
        RemasterFx.Burst(at, Mathf.Min(r, 0.6f), new Color(0.5f, 0.95f, 0.4f));   // 작고 짧게
        foreach(var t in AugmentManager.EnemiesNear(at, r))
            if(t != e) t.AddPoison(stacks, 3f);
    }
}

// 드론 하나: 우주선 주위를 돌며(위치는 Orbital 이 정해요) 가까운 적을 보고 쏴요
public class Drone : MonoBehaviour
{
    SpriteRenderer sr;
    float fireTimer;

    public static Drone Create(Transform parent)
    {
        var go = new GameObject("Drone");
        go.transform.SetParent(parent, false);
        var d = go.AddComponent<Drone>();
        d.sr = go.AddComponent<SpriteRenderer>();
        d.sr.sprite = DroneArt.Drone;
        d.sr.sortingLayerName = "Weapons";   // Default 레이어는 배경(Map)보다 아래라 가려졌어요
        d.sr.sortingOrder = 50;
        d.fireTimer = Random.Range(0f, 0.3f);
        return d;
    }

    public void SetTint(Color c)
    {
        if(sr != null) sr.color = c;
    }

    public void Tick(Orbital owner, float interval, bool canShoot)
    {
        var near = AugmentManager.NearestInRange(transform.position, 1);
        Enemy target = near.Count > 0 ? near[0] : null;

        // 적 쪽으로 몸을 돌려요 (없으면 천천히 회전)
        float want = transform.eulerAngles.z + 90f * Time.deltaTime;
        if(target != null) {
            Vector2 to = target.pos - transform.position;
            want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(transform.eulerAngles.z, want, 720f * Time.deltaTime));

        fireTimer -= Time.deltaTime;
        if(fireTimer > 0f || !canShoot || target == null) return;
        fireTimer = interval;
        owner.FireFrom(transform.position + transform.up * 0.2f, target);
    }
}

// 드론 탄: 곧게 날아가 처음 닿는 적(또는 더미)을 맞혀요. 지나간 길 전체를 검사해서 빠른 게임 속도에도 안 지나쳐요
public class DroneBullet : MonoBehaviour
{
    static readonly Stack<DroneBullet> pool = new Stack<DroneBullet>();
    static Transform holder;

    SpriteRenderer sr;
    Vector3 dir;
    float speed, life, critDamage;
    long damage;
    bool crit, stun, poison;

    const float HitRadius = 0.1f;
    const float MaxLife = 4f;

    public static void Spawn(Vector3 pos, Vector3 dir, float speed, long damage, bool crit, bool stun, float critDamage, bool poison)
    {
        DroneBullet b = null;
        while(pool.Count > 0 && b == null) b = pool.Pop();   // 씬이 바뀌어 지워진 것은 건너뛰어요
        if(b == null) b = Create();
        b.dir = dir;
        b.speed = speed;
        b.damage = damage;
        b.crit = crit;
        b.stun = stun;
        b.critDamage = critDamage;
        b.poison = poison;
        b.life = 0f;
        b.transform.position = new Vector3(pos.x, pos.y, 0f);
        b.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        b.sr.color = poison ? new Color(0.55f, 1f, 0.4f) : new Color(1f, 0.92f, 0.45f);
        b.gameObject.SetActive(true);
    }

    static DroneBullet Create()
    {
        if(holder == null) holder = new GameObject("DroneBullets").transform;
        var go = new GameObject("DroneBullet");
        go.transform.SetParent(holder, false);
        var b = go.AddComponent<DroneBullet>();
        b.sr = go.AddComponent<SpriteRenderer>();
        b.sr.sprite = DroneArt.Bullet;
        b.sr.sortingLayerName = "Bullets";
        b.sr.sortingOrder = 50;
        return b;
    }

    void Recycle()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }

    void Update()
    {
        life += Time.deltaTime;
        Vector3 prev = transform.position;
        Vector3 next = prev + dir * speed * Time.deltaTime;
        if(FindHit(prev, next, out Collider2D hit)) {
            Apply(hit);
            Recycle();
            return;
        }
        transform.position = next;
        if(life > MaxLife || OffScreen(next)) Recycle();
    }

    static bool OffScreen(Vector3 p)
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize + 0.5f, w = cam.orthographicSize * cam.aspect + 0.5f;
        return p.x > w || p.x < -w || p.y > h || p.y < -h;
    }

    static bool IsLive(Collider2D c)
    {
        if(c == null) return false;
        if(c.TryGetComponent(out Enemy e)) return e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;
        return c.TryGetComponent(out Dummy _);
    }

    static bool FindHit(Vector3 prev, Vector3 next, out Collider2D hit)
    {
        hit = null;
        foreach(var c in Physics2D.OverlapCircleAll(prev, HitRadius))
            if(IsLive(c)) { hit = c; return true; }
        Vector2 move = next - prev;
        float dist = move.magnitude;
        if(dist < 0.00001f) return false;
        float best = float.MaxValue;
        foreach(var h in Physics2D.CircleCastAll(prev, HitRadius, move / dist, dist))
            if(IsLive(h.collider) && h.distance < best) { best = h.distance; hit = h.collider; }
        return hit != null;
    }

    void Apply(Collider2D c)
    {
        if(c.TryGetComponent(out Enemy e)) {
            e.OnHit(poison ? PoisonArea.StackBonus(e, damage) : damage, stun, crit, critDamage);   // 독안개 드론: 독 중첩 보너스
            if(poison) Orbital.PoisonHit(e);   // 독안개 드론: 맞은 적 둔화 + 주변으로 독
            AugmentManager.WeaponHitEvent(SideWeapon.Orbital);   // 종결 증강 게이지
        } else if(c.TryGetComponent(out Dummy d)) {
            d.addDamage(damage, crit, critDamage);
        }
    }
}

// 드론·드론 탄 그림 (코드로 그린 임시 그림. 진짜 그림은 P7 에서 바꿔요)
// 흰색·회색 위주라 SpriteRenderer.color 로 색을 입힐 수 있어요 (독안개 드론 초록, 위성 레이저 드론 하늘색)
public static class DroneArt
{
    static Sprite drone, bullet;

    // 위에서 본 쿼드콥터: 프로펠러 4개 + 팔 + 몸통 + 앞쪽 불빛. 크기 약 0.5칸, 앞이 위(+y)
    public static Sprite Drone {
        get {
            if(drone == null) drone = Make(64, 64, 128f, DrawDrone);
            return drone;
        }
    }

    // 빛나는 캡슐 탄. 크기 약 0.17 × 0.33칸
    public static Sprite Bullet {
        get {
            if(bullet == null) bullet = Make(16, 32, 96f, DrawBullet);
            return bullet;
        }
    }

    static Sprite Make(int w, int h, float ppu, System.Func<float, float, Color> draw)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for(int y = 0; y < h; y++)
            for(int x = 0; x < w; x++)
                px[y * w + x] = draw(x + 0.5f, y + 0.5f);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    // 덮어 그리기 (알파 합성)
    static Color Over(Color under, Color top, float cover)
    {
        float a = Mathf.Clamp01(cover) * top.a;
        float outA = a + under.a * (1f - a);
        if(outA <= 0.0001f) return new Color(0, 0, 0, 0);
        Color c = (top * a + under * under.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    static float Disc(float x, float y, float cx, float cy, float r) => Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
    static float Ring(float x, float y, float cx, float cy, float r, float thick)
    {
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return Mathf.Clamp01(thick / 2f - Mathf.Abs(d - r) + 0.5f);
    }
    static float Segment(float x, float y, Vector2 a, Vector2 b, float thick)
    {
        Vector2 p = new Vector2(x, y), ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Mathf.Clamp01(thick / 2f - Vector2.Distance(p, a + ab * t) + 0.5f);
    }

    static Color DrawDrone(float x, float y)
    {
        const float c = 32f;
        var col = new Color(0, 0, 0, 0);
        var arm = new Color(0.55f, 0.58f, 0.65f);
        var rotorIn = new Color(0.85f, 0.9f, 1f, 0.35f);
        var rotorRim = new Color(0.8f, 0.83f, 0.9f);
        // 팔 (대각선 4개)
        foreach(var s in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            col = Over(col, arm, Segment(x, y, new Vector2(c, c), new Vector2(c + s.x * 18f, c + s.y * 18f), 4f));
        // 프로펠러 (반투명 원 + 테두리)
        foreach(var s in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) }) {
            float rx = c + s.x * 18f, ry = c + s.y * 18f;
            col = Over(col, rotorIn, Disc(x, y, rx, ry, 9f));
            col = Over(col, rotorRim, Ring(x, y, rx, ry, 9f, 2f));
            col = Over(col, rotorRim, Disc(x, y, rx, ry, 2f));
        }
        // 몸통 (짙은 남색 + 밝은 테두리)
        col = Over(col, new Color(0.22f, 0.27f, 0.38f), Disc(x, y, c, c, 11f));
        col = Over(col, new Color(0.65f, 0.75f, 0.9f), Ring(x, y, c, c, 11f, 2f));
        // 앞쪽 불빛 (하늘색)
        col = Over(col, new Color(0.45f, 1f, 1f), Disc(x, y, c, c + 6f, 3.5f));
        return col;
    }

    static Color DrawBullet(float x, float y)
    {
        // 세로 캡슐: 빛 번짐 + 흰 심지
        Vector2 p = new Vector2(x, y);
        Vector2 a = new Vector2(8f, 9f), b = new Vector2(8f, 23f);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        float dist = Vector2.Distance(p, a + (b - a) * t);
        float glow = Mathf.Clamp01(1f - dist / 7.5f);
        float core = Mathf.Clamp01(3.2f - dist + 0.5f);
        var col = new Color(1f, 1f, 1f, glow * glow * 0.7f);
        col = Over(col, Color.white, core);
        return col;
    }
}
