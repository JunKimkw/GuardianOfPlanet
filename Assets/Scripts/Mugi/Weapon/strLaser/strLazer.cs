using System.Collections.Generic;
using UnityEngine;

// 블랙홀 (2026-11-20, 예전 "직선 레이저" 자리): 몇 초마다 적이 가장 많이 모인 곳에 블랙홀을 열어요.
// 열려 있는 동안 반경 안의 적을 가운데로 빨아들이며 계속 피해를 주고, 닫힐 때 붕괴하며 한 번 크게 터져요.
// 레벨·상점·저장 id 는 직선 레이저 것을 그대로 이어 써요 (클래스 이름도 프리팹 연결 때문에 그대로).
// 직선 레이저 빔(프리팹)은 숨겨요. 레일건·프리즘 빔 합체는 저격총·360 레이저 쪽 코드라 그대로 동작해요.
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
    public override int BaseDamage => Balanced("blackhole", 9.5f);   // 무기 밸런스 (WeaponBase.LevelCurve)
    protected override float HitInterval => 0.25f;

    public override void SetDamage() => strLazerDamage = Balanced("blackhole", 9.5f);

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
        timer += Time.deltaTime;
        if(timer < Interval) return;
        if(Open()) timer = 0f;
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
        Enemy best = null;
        int bestCount = 0;
        foreach(var e in all) {
            Vector3 p = e.pos; p.z = 0f;
            if((p - ship).magnitude < minDist) continue;
            int n = 0;
            foreach(var o in all) if((o.pos - e.pos).sqrMagnitude <= r * r) n++;
            if(n > bestCount) { bestCount = n; best = e; }
        }

        Vector3 at;
        if(best != null) {
            at = best.pos;
        } else {
            // 모두 가까이 있으면: 가장 가까운 적에서 우주선 반대쪽으로 밀어 최소 거리에 열어요
            Vector3 d = all[0].pos - ship; d.z = 0f;
            if(d.sqrMagnitude < 0.0001f) d = Vector3.up;
            at = ship + d.normalized * minDist;
        }
        BlackHole.Spawn(this, at);
        return true;
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
        float bossPull = Tuning.Get("blackhole_boss_pull", 0.3f);
        foreach(var e in enemies) {
            if(e == null || e._state == Enemy.EnemyState.dead) continue;
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
            long dmg = WeaponBase.ClampDamage(owner.HitDamage * (double)Tuning.Get("blackhole_tick_mul", 1f));
            foreach(var e in AugmentManager.EnemiesNear(c, radius)) Hit(e, dmg);
        }

        // 특이점 저격: 닫히기 직전 저격총처럼 빨간 조준선이 블랙홀을 겨눠요 (2026-11-20)
        if(AugmentManager.Fused(AugmentDefs.FzSingularity) && age >= duration - Tuning.Get("singularity_aim_time", 0.4f)) DrawAim(c);

        if(age >= duration) Collapse();
    }

    // pen: 방어 관통력 (음수면 블랙홀 관통력, 특이점 저격은 저격총 관통력)
    void Hit(Enemy e, long dmg, bool forceCrit = false, float pen = -1f)
    {
        if(e == null || e._state == Enemy.EnemyState.dead) return;
        bool crit = forceCrit || Random.Range(1, 101) <= Player.instance.FinalCriticalChance + owner.ExtraCritChance;
        AugmentManager.WeaponHit(e, dmg, crit, Player.instance.FinalCriticalDamage + owner.ExtraCritDamage, HitKind.Weapon,
                                 pen >= 0f ? pen : Armor.Pen(TraitsData.WeaponType.StrLaser));
        AugmentManager.WeaponHitEvent(SideWeapon.StrLaser);   // 합체 종결 증강 게이지
    }

    // 붕괴: 반경 안을 한 번 크게 때려요. 합체하면 저격(특이점 저격) 또는 거대 폭탄(중력 폭탄)
    void Collapse()
    {
        Vector3 c = transform.position;
        int gathered = AugmentManager.EnemiesNear(c, radius).Count;   // 모인 적 수 (많을수록 세져요)
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
        foreach(var e in AugmentManager.EnemiesNear(c, radius)) if(done.Add(e)) Hit(e, dmg, true, sniperPen);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        foreach(var col in Physics2D.OverlapBoxAll(ship + dir * (len / 2f), new Vector2(width, len), ang))
            if(col != null && col.TryGetComponent(out Enemy e) && done.Add(e)) Hit(e, dmg, true, sniperPen);
        // 여진: 확률로 작은 블랙홀이 다시 열려요
        int echoL = AugmentManager.WL("sg_echo");
        if(!echo && echoL > 0 && Random.value < AugmentDefs.SgEcho(echoL))
            Spawn(owner, c, 0.6f, 1.5f, true);
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
        int big = AugmentManager.WL("gv_big"), mass = AugmentManager.WL("gv_mass"), chain = AugmentManager.WL("gv_chain");
        float r = holeRadius * Tuning.Get("gravbomb_radius", 1.3f) * (big > 0 ? 1f + AugmentDefs.GvBig(big) : 1f);
        double per = Tuning.Get("gravbomb_per_enemy", 0.08f) + (mass > 0 ? AugmentDefs.GvMass(mass) : 0f);
        int counted = Mathf.Min(gathered, Tuning.GetInt("gravbomb_bonus_cap", 10));   // 보너스는 이 수까지만
        long dmg = WeaponBase.ClampDamage(owner.HitDamage * (double)Tuning.Get("gravbomb_mul", 14f) * (1.0 + per * counted));
        // 떨어지는 예고 (줄어드는 고리)
        RemasterFx.Ring(c, r, orange, 0.25f);
        yield return new WaitForSeconds(0.25f);
        Boom(owner, c, r, dmg, orange);
        if(chain > 0) {
            int n = AugmentDefs.GvChain(chain);
            yield return new WaitForSeconds(0.3f);
            for(int i = 0; i < n; i++) {
                float a = (360f * i / n + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                Boom(owner, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r * 0.8f, 0.6f, WeaponBase.ClampDamage(dmg * 0.4), orange);
                yield return new WaitForSeconds(0.08f);
            }
        }
        Destroy(go);
    }

    static void Boom(strLazer owner, Vector3 p, float r, long dmg, Color color)
    {
        RemasterFx.Burst(p, r, color);
        foreach(var e in AugmentManager.EnemiesNear(p, r)) {
            if(e == null || e._state == Enemy.EnemyState.dead) continue;
            AugmentManager.WeaponHit(e, dmg, Random.Range(1, 101) <= Player.instance.FinalCriticalChance + owner.ExtraCritChance,
                                     Player.instance.FinalCriticalDamage + owner.ExtraCritDamage, HitKind.Weapon, Armor.Pen(TraitsData.WeaponType.Bomber));   // 중력 폭탄 = 폭격기 관통력
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
