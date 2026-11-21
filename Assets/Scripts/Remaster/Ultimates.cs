using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// 합체 무기 종결 증강 (2026-11-18 사용자 결정)
// - 합체 전용 증강 3개를 다 모으면 종결 증강(프리즘 ★1)이 뽑기에 나와요.
// - 그 무기가 싸우는 동안(최근 4초 안에 적을 맞힘) 게이지가 차고, 가득 차면(약 30초) 자동 발동.
// - 그 무기가 맞힐 때 1% 확률로 게이지 없이 바로 발동 (1초에 한 번만 굴려요).
// 연출은 그림 없이 코드로 그린 임시 연출이에요 (그림은 P7).
public partial class AugmentManager
{
    readonly float[] ultGauge = new float[AugmentDefs.Fusions.Length];
    readonly float[] ultLastHit = new float[AugmentDefs.Fusions.Length];
    readonly float[] ultNextRoll = new float[AugmentDefs.Fusions.Length];
    readonly bool[] ultBusy = new bool[AugmentDefs.Fusions.Length];

    void ResetUltimates()
    {
        System.Array.Clear(ultGauge, 0, ultGauge.Length);
        System.Array.Clear(ultBusy, 0, ultBusy.Length);
        for(int i = 0; i < ultLastHit.Length; i++) ultLastHit[i] = -99f;
    }

    bool UltOwned(int f) => f >= 0 && FusionOwned(f) && Level(AugmentDefs.UltIds[f]) > 0;

    // 게이지 (0~1). UI 에서 쓸 수 있게 열어 둬요
    public float UltGauge(int f) => f >= 0 && f < ultGauge.Length ? ultGauge[f] : 0f;

    // 무기가 적을 맞혔을 때 (무기 코드에서 불러요). 합체 무기 A 의 종결 증강 게이지·즉시 발동
    public static void WeaponHitEvent(SideWeapon a)
    {
        var m = Instance;
        if(m == null || !m.Active) return;
        int f = m.FusionOf(a);
        if(!m.UltOwned(f)) return;
        float now = Time.time;
        m.ultLastHit[f] = now;
        if(now < m.ultNextRoll[f]) return;
        m.ultNextRoll[f] = now + 1f;   // 1초에 한 번만 굴려요 (계속 때리는 무기도 "가끔 대박"이 되게)
        if(Random.value < Tuning.Get("ult_instant_chance", 0.01f)) m.TriggerUltimate(f, true);
    }

    void TickUltimates(float dt)
    {
        float fill = 1f / Mathf.Max(1f, Tuning.Get("ult_charge_seconds", 30f));
        for(int f = 0; f < AugmentDefs.Fusions.Length; f++) {
            if(!UltOwned(f) || ultBusy[f]) continue;
            if(Time.time - ultLastHit[f] > 4f) continue;   // 싸우는 중일 때만 차요
            ultGauge[f] += dt * fill;
            if(ultGauge[f] >= 1f) TriggerUltimate(f, false);
        }
    }

    void TriggerUltimate(int f, bool lucky)
    {
        if(ultBusy[f]) return;
        var weapon = SideWeaponOf(AugmentDefs.Fusions[f].a);
        if(weapon == null) return;
        ultGauge[f] = 0f;
        ultBusy[f] = true;
        if(f != AugmentDefs.FzImpact)   // 2026-11-21 사용자: 소행성 충돌은 발동 문구를 띄우지 않아요
            ui.Toast($"{(lucky ? "대박! " : "")}종결: {AugmentDefs.UltNames[f]}", AugmentDefs.GradeColor(AugGrade.Prism), true);
        UltimateFx.Play(f, weapon.HitDamage, () => ultBusy[f] = false);
    }

    // 개발용: 지금 싸우는 합체 무기 (연습장에서 고른 것, 아니면 출격 중인 합체 무기 중 처음 것). 없으면 -1
    public int CurrentUltFusion()
    {
        var solo = DebugSoloWeapon();
        if(solo != SideWeapon.None) return FusionOf(solo);
        for(int f = 0; f < AugmentDefs.Fusions.Length; f++)
            if(FusionOwned(f) && IsEquipped(AugmentDefs.Fusions[f].a)) return f;
        return -1;
    }

    // 개발용 (연습장 "종결 발동" 버튼, U 키): 종결 증강을 안 받았어도 바로 발동
    public void DebugTriggerCurrentUlt()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int f = CurrentUltFusion();
        if(f < 0) { ui.Toast("[개발] 합체 무기를 먼저 골라 주세요", Color.white); return; }
        DebugTriggerUltimate(f);
#endif
    }

    public void DebugTriggerUltimate(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(!FusionOwned(f)) { ui.Toast("[개발] 먼저 합체해 주세요", Color.white); return; }
        ultBusy[f] = false;
        TriggerUltimate(f, false);
#endif
    }
}

// 종결 증강 연출 + 피해 (코드로 그린 임시 연출)
public class UltimateFx : MonoBehaviour
{
    static UltimateFx inst;

    static UltimateFx Get()
    {
        if(inst == null) inst = new GameObject("UltimateFx").AddComponent<UltimateFx>();
        return inst;
    }

    public static void Play(int f, long dmg, System.Action done)
    {
        var fx = Get();
        fx.StartCoroutine(fx.Run(f, dmg, done));
    }

    IEnumerator Run(int f, long dmg, System.Action done)
    {
        IEnumerator body;
        switch(f) {
            case AugmentDefs.FzGuided: body = CarpetRaid(dmg); break;
            case AugmentDefs.FzSatLaser: body = OrbitalStrike(dmg); break;
            case AugmentDefs.FzRailgun: body = Piercing(dmg); break;
            case AugmentDefs.FzPlague: body = PlagueRain(dmg); break;
            case AugmentDefs.FzSplitDbl: body = StarShower(dmg); break;
            case AugmentDefs.FzHound: body = HoundPack(dmg); break;
            case AugmentDefs.FzToxSat: body = ToxicVeil(dmg); break;
            case AugmentDefs.FzSingularity: body = EventHorizon(dmg); break;
            case AugmentDefs.FzGravBomb: body = GravityCollapse(dmg); break;
            case AugmentDefs.FzConstellation: body = FateConstellation(dmg); break;
            case AugmentDefs.FzBrand: body = Judgment(dmg); break;
            case AugmentDefs.FzImpact: body = AsteroidImpact(dmg); break;
            default: body = RainbowJudgment(dmg); break;
        }
        yield return StartCoroutine(body);
        done?.Invoke();
    }

    // ---- 공통
    static float HalfW => GameManager.instance.mainCamera.orthographicSize * GameManager.instance.mainCamera.aspect;
    static float HalfH => GameManager.instance.mainCamera.orthographicSize;
    static Vector3 Ship => WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
    static long Mul(long dmg, double m) => WeaponBase.ClampDamage(dmg * m);
    static float M(string key, float def) => Tuning.Get(key, def);

    static void Hit(Enemy e, long dmg)
    {
        bool crit = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        AugmentManager.WeaponHit(e, dmg, crit, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
    }

    static void Boom(Vector3 p, float r, long dmg, Color c)
    {
        RemasterFx.Burst(p, r, c);
        foreach(var e in AugmentManager.EnemiesNear(p, r)) Hit(e, dmg);
    }

    static SpriteRenderer Shape(Color c, Vector2 size, int order = 520)
    {
        var go = new GameObject("ultShape");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RemasterFx.CircleSprite;
        sr.color = c;
        sr.sortingLayerName = RemasterFx.FxLayer;
        sr.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return sr;
    }

    // ---- 종결 7개 공통 (2026-11-21 사용자 요청: 유도 폭격·위성 레이저·레일건·역병 폭탄·분열 쌍발·사냥개 탄·독안개 드론 종결을
    //      각자 개성 있게 다시 만들었어요. 피해 숫자는 나중에 한꺼번에 정해요)
    static Vector3 Cam { get { var p = GameManager.instance.mainCamera.transform.position; p.z = 0f; return p; } }

    static bool Alive(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;

    static SpriteRenderer Sprite2(Sprite s, Color c, float size, int order)
    {
        var sr = Shape(c, Vector2.one * size, order);
        sr.sprite = s;
        return sr;
    }

    // 화면 안에서 적이 가장 몰린 곳 (반경 r 안 적이 가장 많은 적 둘레의 평균). 없으면 fallback
    static Vector3 Densest(Vector3 fallback, float r = 2f)
    {
        var list = AugmentManager.EnemiesNear(Vector3.zero, 999f);
        list.RemoveAll(e => !e.IsInRange);
        if(list.Count == 0) { fallback.z = 0f; return fallback; }
        int best = -1;
        Vector3 bestPos = fallback;
        float r2 = r * r;
        foreach(var a in list) {
            int cnt = 0;
            Vector3 sum = Vector3.zero;
            foreach(var b in list) {
                if((a.transform.position - b.transform.position).sqrMagnitude > r2) continue;
                cnt++;
                sum += b.transform.position;
            }
            if(cnt > best) { best = cnt; bestPos = sum / cnt; }
        }
        bestPos.z = 0f;
        return bestPos;
    }

    // 화면 번쩍 (투명도를 정할 수 있게)
    IEnumerator Flash(Color c, float alpha, float dur)
    {
        var sr = Shape(c, Vector2.one * Mathf.Max(HalfW, HalfH) * 3f, 600);
        sr.transform.position = Cam;
        for(float t = 0f; t < dur; t += Time.deltaTime) {
            sr.color = new Color(c.r, c.g, c.b, alpha * (1f - t / dur));
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // 폭발 한 번: 색 원 + 하얀 속 + 범위 안 적에게 피해
    static void Impact(Vector3 p, float r, long dmg, Color c)
    {
        RemasterFx.Burst(p, r, c);
        RemasterFx.Burst(p, r * 0.4f, Color.white);
        foreach(var e in AugmentManager.EnemiesNear(p, r)) Hit(e, dmg);
    }

    // 꼬리 달린 날아가는 것 (유도탄·유성·사냥개): 뿌리에 꼬리(TrailRenderer, 시간이 지나면 저절로 줄어요), 자식에 머리 그림.
    // 끝낼 땐 EndProjectile: 머리만 숨기고 꼬리가 다 사라지면 저절로 지워져요 (2026-11-21: 예전 꼬리는 멈추면 안 줄어들어서 굳은 줄이 남았어요)
    static (GameObject root, SpriteRenderer head) Projectile(Vector3 at, Sprite sprite, Color c, float size, int order)
    {
        at.z = 0f;
        var root = new GameObject("ultProj");
        root.transform.position = at;
        var hgo = new GameObject("head");
        hgo.transform.SetParent(root.transform, false);
        hgo.transform.localScale = Vector3.one * size;
        var sr = hgo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingLayerName = RemasterFx.FxLayer;
        sr.sortingOrder = order;
        return (root, sr);
    }

    static void AddTrail(GameObject root, Color head, Color tail, float width, float time)
    {
        var tr = root.AddComponent<TrailRenderer>();
        if(lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
        tr.sharedMaterial = lineMat;
        tr.time = time;
        tr.minVertexDistance = 0.04f;
        tr.numCapVertices = 2;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.widthMultiplier = width;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 1f) },
                  new[] { new GradientAlphaKey(head.a, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.sortingLayerName = RemasterFx.FxLayer;
        tr.sortingOrder = 527;
        tr.Clear();
    }

    static void EndProjectile(GameObject root, SpriteRenderer head)
    {
        if(root == null) return;
        if(head != null) head.enabled = false;
        if(root.TryGetComponent(out TrailRenderer tr)) tr.autodestruct = true;
        else Object.Destroy(root);
    }

    // 작은 폭발 (조각용, 그림 개수를 아껴요): 주황 원 + 불꽃 선 4개 + 범위 피해
    static void SmallBlast(Vector3 p, float r, long dmg, Color c)
    {
        RemasterFx.Burst(p, r, c);
        float a0 = Random.Range(0f, 360f);
        for(int i = 0; i < 4; i++) {
            float a = (a0 + i * 90f) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            RemasterFx.Line(p + d * r * 0.2f, p + d * r * 1.1f, c);
        }
        foreach(var e in AugmentManager.EnemiesNear(p, r)) Hit(e, dmg);
    }

    // 0 유도탄 세례 (2026-11-21 두 번째, 예전 "융단 공습": 나선 폭격이 허전하다고 해서)
    //  우주선에서 유도탄 수십 발이 사방으로 부채처럼 퍼져 나간 뒤, 흰 연기 꼬리를 그리며 휘어져 화면의 적 하나하나에게 꽂혀요.
    //  적마다 1~2발 (가까운 적부터 차례로 나눠요). 목표가 먼저 죽으면 가까운 적으로 다시 꺾어요. 숫자 ult_guided_*
    class Missile { public GameObject root; public SpriteRenderer head; public Vector3 pos, vel; public Enemy target; public float age; }

    IEnumerator CarpetRaid(long dmg)
    {
        Vector3 o0 = Ship;
        var targets = AugmentManager.NearestInRange(o0, 999);
        if(targets.Count == 0) yield break;
        int n = Mathf.Clamp(targets.Count * 2, Mathf.RoundToInt(M("ult_guided_min", 24f)), Mathf.RoundToInt(M("ult_guided_max", 60f)));
        float launch = M("ult_guided_launch", 0.6f);
        long hit = Mul(dmg, M("ult_guided_mul", 1.2f));
        var fire = new Color(1f, 0.55f, 0.2f);
        var ms = new List<Missile>();
        int fired = 0;
        RemasterFx.Burst(o0, 1f, Color.white);
        for(float t = 0f; fired < n || ms.Count > 0; t += Time.deltaTime) {
            float dt = Time.deltaTime;
            while(fired < n && fired <= t / launch * n) {   // launch 초 동안 고르게 쏴요
                Vector3 o = Ship;
                o.z = 0f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                var (root, head) = Projectile(o, StarArt.Star, new Color(1f, 0.85f, 0.55f), 0.35f, 548);
                AddTrail(root, new Color(1f, 1f, 1f, 0.85f), new Color(0.65f, 0.65f, 0.7f), 0.16f, 0.45f);
                ms.Add(new Missile { root = root, head = head, pos = o, vel = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(4f, 7f), target = targets[fired % targets.Count] });
                fired++;
            }
            for(int i = ms.Count - 1; i >= 0; i--) {
                var m = ms[i];
                m.age += dt;
                if(!Alive(m.target)) {
                    var nn = AugmentManager.NearestInRange(m.pos, 1);
                    m.target = nn.Count > 0 ? nn[0] : null;
                }
                Vector3 goal = m.target != null ? m.target.transform.position : m.pos + m.vel;
                goal.z = 0f;
                // 처음 0.2초는 퍼져 나가고, 그다음 점점 세게 꺾으며 빨라져요
                float steer = m.age < 0.2f ? 0f : Mathf.Lerp(4f, 18f, Mathf.Clamp01((m.age - 0.2f) / 0.6f));
                float speed = Mathf.Lerp(6f, 16f, Mathf.Clamp01(m.age / 0.8f));
                m.vel = Vector3.Lerp(m.vel, (goal - m.pos).normalized * speed, Mathf.Clamp01(steer * dt));
                m.pos += m.vel * dt;
                m.root.transform.position = m.pos;
                m.head.transform.Rotate(0f, 0f, 900f * dt);
                if((m.target != null && (goal - m.pos).magnitude <= 0.3f) || m.age > 2.5f) {
                    Impact(m.pos, 0.9f, hit, fire);
                    RemasterFx.Spark(m.pos, 0.9f, new Color(1f, 0.85f, 0.35f));
                    EndProjectile(m.root, m.head);
                    ms.RemoveAt(i);
                }
            }
            yield return null;
        }
    }

    // 1 궤도 포격 (2026-11-21 두 번째: "다른 종결보다 위력이 너무 약해") 궤도 일제 사격
    //  화면 둘레에 위성 6대가 나타나 화면 끝까지 닿는 굵은 레이저로 각자 부채꼴(100도)을 쓸며 지나가는 적을 지져요 (같은 적은 0.1초에 한 번).
    //  그다음 6대가 모두 한 점(보스, 없으면 적이 가장 몰린 곳)으로 레이저를 모아 점점 굵게 → 하얀 대폭발. 숫자 ult_satlaser_*
    IEnumerator OrbitalStrike(long dmg)
    {
        float w = HalfW, h = HalfH;
        Vector3 cam = Cam;
        int n = Mathf.Clamp(Mathf.RoundToInt(M("ult_satlaser_sats", 6f)), 2, 12);
        float sweepT = M("ult_satlaser_time", 2f), focusT = M("ult_satlaser_focus_time", 0.6f), width = M("ult_satlaser_width", 0.5f);
        long tick = Mul(dmg, M("ult_satlaser_tick_mul", 0.5f)), blast = Mul(dmg, M("ult_satlaser_blast_mul", 4f));
        var cyan = new Color(0.45f, 0.95f, 1f);
        var sats = new SpriteRenderer[n];
        var glow = new LineRenderer[n];
        var core = new LineRenderer[n];
        var home = new Vector3[n];
        var a0 = new float[n];
        var a1 = new float[n];
        for(int i = 0; i < n; i++) {
            float a = (i + 0.5f) / n * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            float k = Mathf.Min(Mathf.Abs(d.x) > 0.001f ? w * 0.85f / Mathf.Abs(d.x) : 999f, Mathf.Abs(d.y) > 0.001f ? h * 0.88f / Mathf.Abs(d.y) : 999f);
            home[i] = cam + d * k;   // 화면 가장자리 안쪽
            sats[i] = Sprite2(SatArt.Sprite, Color.white, 0.9f, 545);
            sats[i].transform.position = home[i] + d * 3f;
            glow[i] = NewLine(new Color(cyan.r, cyan.g, cyan.b, 0.65f), width);
            core[i] = NewLine(Color.white, width * 0.35f);
            core[i].sortingOrder = glow[i].sortingOrder + 1;
            glow[i].enabled = core[i].enabled = false;
            float inward = Mathf.Atan2(-d.y, -d.x) * Mathf.Rad2Deg;   // 화면 가운데 쪽을 기준으로 ±50도 (반은 반대로 돌아요)
            float s = i % 2 == 0 ? 1f : -1f;
            a0[i] = inward - 50f * s;
            a1[i] = inward + 50f * s;
        }
        var tickAt = new Dictionary<Enemy, float>();
        var dirs = new Vector3[n];
        float clock = 0f, nextSpark = 0f;

        // 1) 위성이 미끄러져 들어와 부채꼴로 쓸어요
        for(float t = 0f; t < sweepT + 0.3f; t += Time.deltaTime) {
            clock += Time.deltaTime;
            float slide = Mathf.Clamp01(t / 0.3f), k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / sweepT));
            for(int i = 0; i < n; i++) {
                Vector3 outward = (home[i] - cam).normalized;
                sats[i].transform.position = home[i] + outward * 3f * (1f - slide);
                float ang = Mathf.Lerp(a0[i], a1[i], k);
                sats[i].transform.rotation = Quaternion.Euler(0f, 0f, ang - 90f);
                dirs[i] = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0f);
                if(slide < 1f) continue;
                float wv = width * (1f + 0.12f * Mathf.Sin(clock * 40f + i));
                SatBeam(glow[i], core[i], sats[i].transform.position, dirs[i], wv, 30f);
                SatBeamTick(sats[i].transform.position, dirs[i], 30f, wv, tick, tickAt, clock);
            }
            if(slide >= 1f && clock >= nextSpark) {   // 레이저 위로 불꽃
                nextSpark = clock + 0.04f;
                int i = Random.Range(0, n);
                RemasterFx.Burst(sats[i].transform.position + dirs[i] * Random.Range(1f, 12f), width * 0.7f, Color.Lerp(cyan, Color.white, Random.value));
            }
            yield return null;
        }

        // 2) 모든 위성이 한 점으로 모아요 (점점 굵게, 집중점이 하얗게 달아올라요)
        var boss = AugmentManager.StrongestInRange();
        bool chaseBoss = boss != null && boss.IsBoss;
        Vector3 focus = chaseBoss ? boss.transform.position : Densest(cam);
        focus.z = 0f;
        for(float f = 0f; f < focusT; f += Time.deltaTime) {
            clock += Time.deltaTime;
            float k = f / focusT;
            if(chaseBoss && Alive(boss)) { focus = boss.transform.position; focus.z = 0f; }
            for(int i = 0; i < n; i++) {
                Vector3 sp = sats[i].transform.position, want = (focus - sp).normalized;
                dirs[i] = Vector3.Slerp(dirs[i], want, Mathf.Clamp01(Time.deltaTime * 12f)).normalized;
                sats[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dirs[i].y, dirs[i].x) * Mathf.Rad2Deg - 90f);
                float wv = Mathf.Lerp(width, width * 2.4f, k) * (1f + 0.15f * Mathf.Sin(clock * 50f + i));
                float len = Vector3.Dot(focus - sp, dirs[i]) > 0f ? (focus - sp).magnitude : 30f;
                SatBeam(glow[i], core[i], sp, dirs[i], wv, len);
                SatBeamTick(sp, dirs[i], len, wv, tick, tickAt, clock);
            }
            if(Random.value < 0.6f) RemasterFx.Burst(focus, Mathf.Lerp(0.5f, 1.6f, k), Color.Lerp(cyan, Color.white, k));
            yield return null;
        }

        // 3) 하얀 대폭발
        float br = M("ult_satlaser_blast_radius", 3f);
        StartCoroutine(Flash(Color.white, 0.45f, 0.3f));
        RemasterFx.Burst(focus, br, Color.white);
        RemasterFx.Ring(focus, br * 1.25f, cyan, 0.4f);
        RemasterFx.Ring(focus, br * 0.7f, Color.white, 0.3f);
        RemasterFx.Spark(focus, br, cyan);
        foreach(var e in AugmentManager.EnemiesNear(focus, br)) Hit(e, blast);
        for(int i = 0; i < n; i++) { Destroy(glow[i].gameObject); Destroy(core[i].gameObject); }
        for(float t2 = 0f; t2 < 0.3f; t2 += Time.deltaTime) {   // 위성이 물러나요
            for(int i = 0; i < n; i++) sats[i].transform.position += (home[i] - cam).normalized * 10f * Time.deltaTime;
            yield return null;
        }
        foreach(var s in sats) Destroy(s.gameObject);
    }

    static void SatBeam(LineRenderer glow, LineRenderer core, Vector3 from, Vector3 dir, float width, float len)
    {
        glow.enabled = core.enabled = true;
        SetSeg(glow, from, from + dir * len);
        SetSeg(core, from, from + dir * len);
        glow.startWidth = glow.endWidth = width;
        core.startWidth = core.endWidth = width * 0.35f;
    }

    // 레이저에 닿은 적 (같은 적은 0.1초에 한 번, 레이저 여러 개가 겹쳐도)
    static void SatBeamTick(Vector3 from, Vector3 dir, float len, float width, long dmg, Dictionary<Enemy, float> tickAt, float now)
    {
        float half = width * 0.5f + 0.2f;
        foreach(var e in AugmentManager.EnemiesNear(from, len)) {
            Vector3 d = e.transform.position - from;
            d.z = 0f;
            float along = Vector3.Dot(d, dir);
            if(along <= 0f || along > len || (d - dir * along).magnitude > half) continue;
            if(tickAt.TryGetValue(e, out float next) && now < next) continue;
            tickAt[e] = now + 0.1f;
            Hit(e, dmg);
        }
    }


    // 2 천공 (2026-11-21 다섯 번째: 사용자 결정 "마지막 한 방은 우주선 기준 360도로 저격총을 레이저 느낌으로") 연사 → 충전 → 360도 초전자포 → 줄줄이 폭발 → 처형
    //  1) 연사: 적이 가장 많이 늘어선 방향으로 레일건 4발을 탕탕탕탕 (쏜 방향 ±20도는 피해서 매번 다른 줄)
    //  2) 충전: 우주선에 전기가 모여요 (사방에서 번개 줄기가 빨려 들고, 빛 구슬이 떨리며 커져요)
    //  3) 360도 초전자포: 우주선에서 사방으로 레이저 같은 저격 빔 16줄(ult_railgun_rays)이 한 바퀴 돌며 0.25초 안에 촤라락 뻗어요.
    //     맞은 적은 모두 치명타, 보스는 저격총처럼 보스 추가 피해
    //  4) 여파: 모든 줄을 따라 폭발이 바깥으로 동시에 퍼져 나가요
    //  5) 처형: 이번 종결에서 레일건에 꿰뚫린 적 중 체력이 ult_railgun_execute(30%) 이하로 남은 일반 적은 한꺼번에 X자로 처치돼요
    //  → 다른 종결과 다른 점: 꿰뚫린 적은 "남김없이" 정리 (레일건 = 한 줄 관통 + 확실한 마무리). 숫자 ult_railgun_*
    IEnumerator Piercing(long dmg)
    {
        var cyan = new Color(0.45f, 0.9f, 1f);
        int shots = Mathf.Max(0, Mathf.RoundToInt(M("ult_railgun_shots", 4f)));
        float width = M("ult_railgun_width", 0.8f);
        long hit = Mul(dmg, M("ult_railgun_mul", 3f));
        var used = new List<float>();
        var pierced = new HashSet<Enemy>();   // 이번 종결에서 꿰뚫린 적 (처형 대상)

        // 1) 연사
        for(int s = 0; s < shots; s++) {
            Vector3 o = Ship;
            o.z = 0f;
            float ang = BestRay(o, width, used);
            used.Add(ang);
            var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            var aim = NewLine(new Color(1f, 0.25f, 0.3f, 0.9f), 0.03f);
            for(float t = 0f; t < 0.08f; t += Time.deltaTime) {
                SetSeg(aim, o, o + dir * 30f);
                yield return null;
            }
            Destroy(aim.gameObject);
            StartCoroutine(RailShot(o, dir, width, hit, pierced));
            yield return new WaitForSeconds(0.16f);
        }

        // 2) 충전
        float charge = M("ult_railgun_charge", 0.7f);
        var orb = Shape(new Color(0.5f, 0.85f, 1f, 0.85f), Vector2.one * 0.3f, 535);
        for(float t = 0f; t < charge; t += Time.deltaTime) {
            float k = t / charge;
            Vector3 o = Ship;
            o.z = 0f;
            orb.transform.position = o;
            orb.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, k * k) * (1f + 0.15f * Mathf.Sin(t * 60f));
            orb.color = Color.Lerp(new Color(0.5f, 0.85f, 1f, 0.85f), Color.white, k);
            if(Random.value < 0.6f) {   // 번개 줄기가 빨려 들어요 (두 마디 지그재그)
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.8f, 3.2f);
                Vector3 a = o + (Vector3)r, mid = o + (Vector3)r * 0.5f + (Vector3)(Random.insideUnitCircle * 0.4f);
                RemasterFx.Line(a, mid, cyan);
                RemasterFx.Line(mid, o, Color.white);
            }
            if(Random.value < 0.08f) RemasterFx.Ring(o, Mathf.Lerp(2.5f, 0.8f, k), cyan, 0.2f);
            yield return null;
        }
        Destroy(orb.gameObject);

        // 3) 360도 초전자포 + 4) 여파 + 5) 처형
        Vector3 o3 = Ship;
        o3.z = 0f;
        int rays = Mathf.Clamp(Mathf.RoundToInt(M("ult_railgun_rays", 16f)), 4, 48);
        yield return StartCoroutine(RadialRail(o3, rays, M("ult_railgun_mega_width", 0.9f),
            Mul(dmg, M("ult_railgun_mega_mul", 8f)), Mul(dmg, M("ult_railgun_zip_mul", 1.5f)), pierced));
        yield return StartCoroutine(Execute(pierced, M("ult_railgun_execute", 0.3f)));
    }

    // 점 p 가 꺾인 길(굵기 width)에 걸리는지
    static bool OnPath(List<Vector3> pts, Vector3 p, float half)
    {
        p.z = 0f;
        for(int i = 0; i + 1 < pts.Count; i++) {
            Vector3 a = pts[i], ab = pts[i + 1] - a;
            float l2 = ab.sqrMagnitude;
            float k = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            if((p - (a + ab * k)).sqrMagnitude <= half * half) return true;
        }
        return false;
    }

    // 360도 초전자포: 우주선에서 사방으로 레이저 같은 저격 빔 n 줄이 한 바퀴 돌며 0.25초 안에 촤라락 뻗어요 (맞은 적은 한 번씩 치명타).
    // 0.45초 동안 떨리며 전기가 기어다니고, 그다음 모든 줄을 따라 폭발이 바깥으로 동시에 달려간 뒤 빔이 사라져요
    IEnumerator RadialRail(Vector3 o, int n, float width, long hit, long zipHit, HashSet<Enemy> pierced)
    {
        const float len = 30f;
        var cyan = new Color(0.45f, 0.9f, 1f);
        float half = width * 0.5f + 0.25f;
        StartCoroutine(Flash(new Color(0.85f, 0.95f, 1f), 0.55f, 0.35f));
        RemasterFx.Burst(o, 1.6f, Color.white);
        RemasterFx.Ring(o, 2.4f, cyan, 0.3f);
        var glows = new List<LineRenderer>();
        var cores = new List<LineRenderer>();
        var dirs = new List<Vector3>();
        var once = new HashSet<Enemy>();
        float a0 = Random.Range(0f, 360f);
        float gap = 0.25f / n;
        float wait = 0f;
        for(int i = 0; i < n; i++) {
            float ang = (a0 + 360f * i / n) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            Vector3 a = o + dir * 0.3f, b = o + dir * len;
            var glow = NewLine(new Color(cyan.r, cyan.g, cyan.b, 0.8f), width);
            var core = NewLine(Color.white, width * 0.35f);
            core.sortingOrder = glow.sortingOrder + 1;
            SetSeg(glow, a, b);
            SetSeg(core, a, b);
            glows.Add(glow);
            cores.Add(core);
            dirs.Add(dir);
            RemasterFx.Burst(o + dir * 0.5f, width * 0.8f, Color.white);   // 총구 번쩍
            var seg = new List<Vector3> { o, b };
            foreach(var e in AugmentManager.EnemiesNear(o, len)) {
                if(once.Contains(e) || !OnPath(seg, e.transform.position, half)) continue;
                once.Add(e);
                pierced.Add(e);
                RemasterFx.Burst(e.transform.position, 0.5f, Color.white);
                AugmentManager.WeaponHit(e, SniperBullet.BossBonus(e, hit), true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
            }
            wait += gap;   // 한 프레임보다 짧은 간격은 모아서 기다려요
            if(wait >= Time.deltaTime) { yield return new WaitForSeconds(wait); wait = 0f; }
        }
        for(float t = 0f; t < 0.45f; t += Time.deltaTime) {
            float k = t / 0.45f;
            float wv = width * (k < 0.15f ? Mathf.Lerp(0.6f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 0.9f, (k - 0.15f) / 0.85f)) * (1f + 0.06f * Mathf.Sin(t * 70f));
            for(int i = 0; i < glows.Count; i++) {
                glows[i].startWidth = glows[i].endWidth = wv;
                cores[i].startWidth = cores[i].endWidth = wv * 0.35f;
            }
            if(Random.value < 0.8f) {   // 빔을 따라 전기가 기어다녀요
                var dir = dirs[Random.Range(0, dirs.Count)];
                var side = new Vector3(-dir.y, dir.x, 0f);
                Vector3 p0 = o + dir * Random.Range(1f, 12f) + side * Random.Range(-wv * 0.6f, wv * 0.6f);
                RemasterFx.Line(p0, p0 + dir * Random.Range(0.5f, 1.2f) + side * Random.Range(-0.5f, 0.5f), new Color(0.75f, 0.95f, 1f));
            }
            yield return null;
        }
        // 여파: 모든 줄을 따라 폭발이 바깥으로 동시에 (화면 안까지만, 줄마다 ult_railgun_zip_step 칸 간격)
        Vector3 cam = Cam;
        float w = HalfW + 0.5f, h = HalfH + 0.5f;
        float step = Mathf.Max(0.5f, M("ult_railgun_zip_step", 1.4f));
        int rings = Mathf.CeilToInt(Mathf.Sqrt(w * w + h * h) * 2f / step);
        float ringGap = M("ult_railgun_zip_time", 0.8f) / Mathf.Max(1, rings);
        float zr = width * 0.55f, fade = 1f;
        wait = 0f;
        for(int z = 1; z <= rings; z++) {
            float d = z * step;
            bool any = false;
            foreach(var dir in dirs) {
                Vector3 p = o + dir * d;
                if(Mathf.Abs(p.x - cam.x) > w || Mathf.Abs(p.y - cam.y) > h) continue;
                any = true;
                Boom(p, zr, zipHit, cyan);   // 그림 1개짜리 폭발 (줄이 많아서 그림 개수를 아껴요)
            }
            if(!any) break;
            fade = Mathf.Max(0.3f, 1f - (float)z / rings);
            for(int i = 0; i < glows.Count; i++) {
                glows[i].startWidth = glows[i].endWidth = width * 0.9f * fade;
                cores[i].startWidth = cores[i].endWidth = width * 0.32f * fade;
            }
            wait += ringGap;
            if(wait >= Time.deltaTime) { yield return new WaitForSeconds(wait); wait = 0f; }
        }
        for(float t = 0f; t < 0.2f; t += Time.deltaTime) {
            float k = 1f - t / 0.2f;
            for(int i = 0; i < glows.Count; i++) {
                glows[i].startWidth = glows[i].endWidth = width * 0.9f * fade * k;
                cores[i].startWidth = cores[i].endWidth = width * 0.32f * fade * k;
            }
            yield return null;
        }
        foreach(var g in glows) Destroy(g.gameObject);
        foreach(var c in cores) Destroy(c.gameObject);
    }

    // 처형: 꿰뚫린 적 중 체력이 line 이하로 남은 일반 적(보스 제외)을 한꺼번에 처치. 하얀 X 자국 + 번쩍
    IEnumerator Execute(HashSet<Enemy> pierced, float line)
    {
        var targets = new List<Enemy>();
        foreach(var e in pierced)
            if(Alive(e) && !e.IsBoss && e.EnemyHealth > 0 && e.CurrentHealth / e.EnemyHealth <= line) targets.Add(e);
        if(targets.Count == 0) yield break;
        StartCoroutine(Flash(Color.white, 0.3f, 0.2f));
        foreach(var e in targets) {
            Vector3 p = e.transform.position;
            p.z = 0f;
            StartCoroutine(Slash(p + new Vector3(-0.45f, 0.45f, 0f), p + new Vector3(0.45f, -0.45f, 0f), Color.white, 0.14f));
            StartCoroutine(Slash(p + new Vector3(0.45f, 0.45f, 0f), p + new Vector3(-0.45f, -0.45f, 0f), Color.white, 0.14f));
            RemasterFx.Spark(p, 0.6f, new Color(0.75f, 0.95f, 1f));
            // 남은 체력을 넘는 피해 (종결은 방어력을 무시해요)
            long kill = WeaponBase.ClampDamage(e.CurrentHealth / System.Math.Max(1e-9, e.NumberScale) * 1.05 + 1);
            AugmentManager.WeaponHit(e, kill, false, 0f, HitKind.Weapon, Armor.UltPen);
        }
        yield return new WaitForSeconds(0.35f);
    }

    // 우주선에서 적이 가장 많이 늘어선 방향 (5도 간격, 이미 쏜 방향 ±20도는 빼요)
    static float BestRay(Vector3 o, float width, List<float> used)
    {
        var list = AugmentManager.EnemiesNear(o, 30f);
        float best = Random.Range(0f, Mathf.PI * 2f);
        int bestN = -1;
        for(int k = 0; k < 72; k++) {
            float deg = k * 5f;
            bool taken = false;
            foreach(var u in used) if(Mathf.Abs(Mathf.DeltaAngle(deg, u * Mathf.Rad2Deg)) < 20f) { taken = true; break; }
            if(taken) continue;
            var dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);
            int n = 0;
            foreach(var e in list) {
                Vector3 d = e.transform.position - o;
                d.z = 0f;
                float along = Vector3.Dot(d, dir);
                if(along > 0f && (d - dir * along).magnitude <= width * 0.5f + 0.25f) n++;
            }
            if(n > bestN) { bestN = n; best = deg * Mathf.Deg2Rad; }
        }
        return best;
    }

    // 레일건 한 발: 하얀 심 + 하늘색 빛무리, 화면 번쩍, 빔을 따라 전기 지지직, 맞은 적마다 하얀 불꽃. 0.35초 동안 가늘어지며 사라져요
    IEnumerator RailShot(Vector3 o, Vector3 dir, float width, long hit, HashSet<Enemy> pierced)
    {
        const float len = 30f;
        var glow = NewLine(new Color(0.45f, 0.9f, 1f, 0.8f), width);
        var core = NewLine(Color.white, width * 0.35f);
        core.sortingOrder = glow.sortingOrder + 1;
        SetSeg(glow, o, o + dir * len);
        SetSeg(core, o, o + dir * len);
        RemasterFx.Burst(o + dir * 0.4f, width * 1.2f, Color.white);
        StartCoroutine(Flash(new Color(0.85f, 0.95f, 1f), 0.25f, 0.15f));
        var side = new Vector3(-dir.y, dir.x, 0f);
        for(int z = 0; z < 6; z++) {
            Vector3 p0 = o + dir * Random.Range(1f, 12f) + side * Random.Range(-width, width);
            RemasterFx.Line(p0, p0 + dir * Random.Range(0.6f, 1.4f) + side * Random.Range(-width, width), new Color(0.7f, 0.95f, 1f));
        }
        foreach(var e in AugmentManager.EnemiesNear(o, len)) {
            Vector3 d = e.transform.position - o;
            d.z = 0f;
            float along = Vector3.Dot(d, dir);
            if(along <= 0f || (d - dir * along).magnitude > width * 0.5f + 0.25f) continue;
            pierced.Add(e);
            RemasterFx.Burst(e.transform.position, 0.45f, Color.white);
            AugmentManager.WeaponHit(e, SniperBullet.BossBonus(e, hit), true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);   // 레일건처럼 모두 치명타, 보스 추가 피해
        }
        for(float t = 0f; t < 0.35f; t += Time.deltaTime) {
            float k = 1f - t / 0.35f;
            glow.startWidth = glow.endWidth = width * k;
            core.startWidth = core.endWidth = width * 0.35f * k;
            yield return null;
        }
        Destroy(glow.gameObject);
        Destroy(core.gameObject);
    }

    // 3 역병의 비 (2026-11-21 다시 만듦): 날씨
    //  화면이 초록빛으로 물들고 독비가 쏟아져요. 빗방울에 맞은 적은 독, 비가 오는 동안 모든 적이 느려지고, 적이 몰린 곳에 독 장판이 차례로 생겨요.
    //  독에 걸린 채 죽은 적은 초록 폭발로 터지며 옆 적에게 독을 옮겨요 (옮은 적도 죽으면 또 터져요). 숫자 ult_plague_*
    IEnumerator PlagueRain(long dmg)
    {
        float w = HalfW, h = HalfH;
        var green = new Color(0.5f, 0.95f, 0.35f);
        float dur = M("ult_plague_time", 3.2f), rate = Mathf.Max(1f, M("ult_plague_drops", 40f));
        long drop = Mul(dmg, M("ult_plague_drop_mul", 0.4f)), burst = Mul(dmg, M("ult_plague_burst_mul", 2f));
        int puddles = Mathf.RoundToInt(M("ult_plague_puddles", 8f));
        var tint = Shape(new Color(0.25f, 0.6f, 0.15f, 0f), Vector2.one * Mathf.Max(w, h) * 3f, 505);
        var watch = new Dictionary<Enemy, Vector3>();   // 독에 걸린 적 → 마지막 자리 (죽으면 그 자리에서 터져요)
        float t = 0f, nextDrop = 0f, nextSlow = 0f, nextPuddle = 0.3f;
        int made = 0;
        while(t < dur + 0.4f) {
            t += Time.deltaTime;
            Vector3 cam = Cam;
            tint.transform.position = cam;
            tint.color = new Color(0.25f, 0.6f, 0.15f, 0.22f * Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((dur + 0.4f - t) / 0.4f));
            if(t < dur) {
                while(nextDrop <= t) {   // 빗줄기 (비스듬히)
                    nextDrop += 1f / rate;
                    var p = cam + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0f);
                    RemasterFx.Line(p + new Vector3(0.35f, 1.3f, 0f), p, green);
                    RemasterFx.Burst(p, 0.22f, green);
                    foreach(var e in AugmentManager.EnemiesNear(p, 0.6f)) {
                        e.AddPoison(2, 5f);
                        Hit(e, drop);
                        watch[e] = e.transform.position;
                    }
                }
                if(t >= nextSlow) {   // 비 오는 동안 모든 적 30% 느리게
                    nextSlow = t + 0.4f;
                    foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) e.AddSlow(0.3f, 0.6f);
                }
                if(made < puddles && t >= nextPuddle) {   // 독 장판: 적이 몰린 곳부터 차례로
                    nextPuddle = t + dur / Mathf.Max(1, puddles);
                    made++;
                    var near = AugmentManager.NearestInRange(Densest(Ship), 1);
                    SpawnPuddle(near.Count > 0 ? near[0].transform.position : cam + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0f), dmg);
                }
            }
            foreach(var e in new List<Enemy>(watch.Keys)) {
                if(Alive(e)) { watch[e] = e.transform.position; continue; }
                Vector3 p = watch[e];
                watch.Remove(e);
                RemasterFx.Burst(p, 1.2f, green);
                RemasterFx.Ring(p, 1.4f, new Color(0.7f, 1f, 0.5f), 0.25f);
                foreach(var n2 in AugmentManager.EnemiesNear(p, 1.2f)) {
                    n2.AddPoison(3, 5f);
                    Hit(n2, burst);
                    watch[n2] = n2.transform.position;
                }
            }
            yield return null;
        }
        Destroy(tint.gameObject);
    }

    static void SpawnPuddle(Vector3 at, long dmg)
    {
        var a = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.PoisonArea).GetComponent<PoisonArea>();
        a.poisonDamage = dmg;
        a.spreadLeft = 0;
        a.slowAmount = PoisonBullet.PlagueSlow;
        a.transform.position = new Vector3(at.x, at.y, 0f);
        a.SetScaleMul(1.3f * AugmentManager.PoisonAreaScale);
        a.gameObject.SetActive(true);
    }

    // 4 별똥별 폭풍 (2026-11-21 두 번째: "허전하고 약해 보여") 분열 유성우
    //  긴 불꽃 꼬리를 단 유성 12개가 화면의 적 위로 비스듬히 쏟아져요. 떨어지면 쾅 터지고 조각 3개로 갈라져 튀고,
    //  조각이 터지면 또 2개로 갈라져 터져요 (분열 쌍발처럼). 마지막엔 거대 유성이 적이 가장 몰린 곳에 떨어져 조각 12개가 사방으로 퍼져요.
    //  조각은 근처(1칸 안) 적에게 붙어 떨어져요. 숫자 ult_splitdbl_*
    IEnumerator StarShower(long dmg)
    {
        float w = HalfW, h = HalfH;
        Vector3 cam = Cam;
        int count = Mathf.Max(1, Mathf.RoundToInt(M("ult_splitdbl_meteors", 12f)));
        float rain = M("ult_splitdbl_time", 1.6f);
        long hit = Mul(dmg, M("ult_splitdbl_mul", 2f));
        for(int i = 0; i < count; i++) {
            var list = AugmentManager.NearestInRange(cam, 999);
            Vector3 land = list.Count > 0 ? list[Random.Range(0, list.Count)].transform.position : cam + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0f);
            land.z = 0f;
            StartCoroutine(Meteor(land + new Vector3(-2.2f, 5.5f, 0f), land, 0.4f, 0.9f, hit, 1.2f, 3, 2));
            yield return new WaitForSeconds(rain / count);
        }
        yield return new WaitForSeconds(0.55f);
        // 거대 유성
        Vector3 c = Densest(cam);
        yield return StartCoroutine(Meteor(c + new Vector3(-3f, 9f, 0f), c, 0.6f, 2.2f, Mul(hit, 3), 2.2f, 12, 1, true));
        yield return new WaitForSeconds(0.6f);
    }

    // 유성(또는 조각) 하나: from → to 로 날아가 쾅. 터지면 fan 개 조각으로 갈라지고, 조각은 gens-1 세대까지 2개씩 더 갈라져요
    IEnumerator Meteor(Vector3 from, Vector3 to, float time, float size, long dmg, float radius, int fan, int gens, bool big = false)
    {
        bool fragment = !big && size < 0.6f;
        var (root, head) = Projectile(from, StarArt.Star, Color.white, size, 547);
        AddTrail(root, new Color(1f, 0.9f, 0.55f, 0.95f), new Color(1f, 0.3f, 0.1f), size * (fragment ? 0.5f : 0.65f), fragment ? 0.18f : 0.35f);
        Vector3 side = Vector3.Cross(to - from, Vector3.forward).normalized;
        for(float t = 0f; t < time; t += Time.deltaTime) {
            float k = t / time;
            // 유성은 점점 빨라지며 떨어지고, 조각은 살짝 휘며 튀어요
            Vector3 p = fragment ? Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k)) + side * Mathf.Sin(k * Mathf.PI) * 0.35f : Vector3.Lerp(from, to, k * k);
            root.transform.position = p;
            head.transform.Rotate(0f, 0f, 600f * Time.deltaTime);
            yield return null;
        }
        root.transform.position = to;
        EndProjectile(root, head);
        if(big) {
            StartCoroutine(Flash(new Color(1f, 0.85f, 0.6f), 0.4f, 0.3f));
            RemasterFx.Ring(to, radius * 1.6f, new Color(1f, 0.75f, 0.4f), 0.35f);
        }
        if(fragment) SmallBlast(to, radius, dmg, new Color(1f, 0.6f, 0.25f));
        else {
            Impact(to, radius, dmg, new Color(1f, 0.5f, 0.2f));
            RemasterFx.Spark(to, radius * 1.1f, new Color(1f, 0.85f, 0.35f));
        }
        if(gens <= 0 || fan <= 0) yield break;
        float a0 = Random.Range(0f, 360f);
        for(int s = 0; s < fan; s++) {
            float a = (a0 + 360f * s / fan + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 land = to + d * (radius * 1.5f + 0.7f);
            var near = AugmentManager.NearestInRange(land, 1);
            if(near.Count > 0 && (near[0].transform.position - land).magnitude <= 1f) { land = near[0].transform.position; land.z = 0f; }
            StartCoroutine(Meteor(to, land, 0.22f, Mathf.Clamp(size * 0.45f, 0.3f, 0.55f), Mul(dmg, 0.6), Mathf.Max(0.6f, radius * 0.6f), 2, gens - 1));
        }
    }

    // 5 사냥개 무리 (2026-11-21 세 번째: "딱 한 방 터지는 임팩트가 없어")
    //  1) 사냥: 붉은 사냥개 혼 8마리가 사방으로 튀어나가 적을 차례로 물어뜯어요 (발톱 자국 + 잠깐 기절, 물 때마다 빨라짐).
    //     문 적에게는 붉은 사냥 표식이 남아요. 물 적이 다 떨어지면 이미 문 적도 다시 물고, 적이 없으면 우주선 둘레를 돌아요.
    //  2) 포위: 사냥개들이 멈춰 붉게 부풀며 울부짖어요.
    //  3) 일제 돌격: 8마리가 가장 강한 적(보스, 없으면 적이 가장 몰린 곳)으로 긴 꼬리를 그리며 동시에 덮쳐서 —
    //  4) 한 방: 화면이 붉게 번쩍, 거대한 발톱 자국 3줄 + 붉은 충격파, 큰 피해와 기절. 같은 순간 사냥 표식이 붙은 적이 모두 함께 터져요.
    //  숫자 ult_hound_*
    IEnumerator HoundPack(long dmg)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(M("ult_hound_count", 8f)));
        float dur = M("ult_hound_time", 2.2f);
        long bite = Mul(dmg, M("ult_hound_mul", 1.2f));
        long finalHit = Mul(dmg, M("ult_hound_final_mul", 6f)), markHit = Mul(dmg, M("ult_hound_mark_mul", 1.5f));
        var red = new Color(1f, 0.3f, 0.25f);
        var roots = new GameObject[n];
        var heads = new SpriteRenderer[n];
        var pos = new Vector3[n];
        var speed = new float[n];
        var target = new Enemy[n];
        var bitten = new HashSet<Enemy>[n];
        var marks = new Dictionary<Enemy, SpriteRenderer>();   // 사냥 표식
        Vector3 o = Ship;
        o.z = 0f;
        RemasterFx.Ring(o, 1.6f, red, 0.3f);   // 울부짖음
        for(int i = 0; i < n; i++) {
            float a = i * Mathf.PI * 2f / n;
            pos[i] = o + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f;
            var (root, head) = Projectile(pos[i], RemasterFx.CircleSprite, new Color(1f, 0.55f, 0.45f), 0.38f, 547);
            AddTrail(root, new Color(1f, 0.35f, 0.25f, 0.9f), new Color(0.6f, 0.05f, 0.05f), 0.3f, 0.25f);
            roots[i] = root;
            heads[i] = head;
            speed[i] = M("ult_hound_speed", 9f);
            bitten[i] = new HashSet<Enemy>();
        }

        // 1) 사냥
        float clock = 0f;
        for(float t = 0f; t < dur; t += Time.deltaTime) {
            float dt = Time.deltaTime;
            clock += dt;
            for(int i = 0; i < n; i++) {
                if(!Alive(target[i])) {
                    var list = AugmentManager.NearestInRange(pos[i], 3, bitten[i]);
                    if(list.Count == 0 && bitten[i].Count > 0) { bitten[i].Clear(); list = AugmentManager.NearestInRange(pos[i], 3); }   // 다 물었으면 다시
                    target[i] = list.Count > 0 ? list[Random.Range(0, list.Count)] : null;   // 가까운 3마리 중 하나 (개마다 흩어지게)
                }
                if(target[i] == null) {   // 적이 하나도 없으면 우주선 둘레를 돌아요
                    Vector3 ship = Ship;
                    ship.z = 0f;
                    float a = t * 4f + i * Mathf.PI * 2f / n;
                    pos[i] = Vector3.MoveTowards(pos[i], ship + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.2f, speed[i] * dt);
                } else {
                    Vector3 goal = target[i].transform.position;
                    goal.z = 0f;
                    Vector3 d = goal - pos[i];
                    float step = speed[i] * dt;
                    if(d.magnitude <= Mathf.Max(step, 0.2f)) {
                        pos[i] = goal;
                        Claw(goal, red);
                        Hit(target[i], bite);
                        if(target[i]._state != Enemy.EnemyState.dead) target[i].Stun(0.4f);
                        if(!marks.ContainsKey(target[i])) marks[target[i]] = Shape(new Color(1f, 0.15f, 0.15f, 0.7f), Vector2.one * 0.5f, 549);
                        bitten[i].Add(target[i]);
                        target[i] = null;
                        speed[i] = Mathf.Min(speed[i] * 1.12f, M("ult_hound_max_speed", 22f));   // 물 때마다 빨라져요
                    } else pos[i] += d.normalized * step;
                }
                roots[i].transform.position = pos[i];
            }
            MoveMarks(marks, clock);
            yield return null;
        }

        // 2) 포위: 멈춰서 붉게 부풀며 울부짖어요
        var boss = AugmentManager.StrongestInRange();
        bool chaseBoss = boss != null && boss.IsBoss;
        Vector3 hitAt = chaseBoss ? boss.transform.position : Densest(Cam);
        hitAt.z = 0f;
        for(int i = 0; i < n; i++) RemasterFx.Ring(pos[i], 0.9f, red, 0.25f);
        for(float t = 0f; t < 0.3f; t += Time.deltaTime) {
            clock += Time.deltaTime;
            float k = t / 0.3f;
            for(int i = 0; i < n; i++) heads[i].transform.localScale = Vector3.one * 0.38f * (1f + 0.9f * k) * (1f + 0.1f * Mathf.Sin(clock * 40f + i));
            MoveMarks(marks, clock);
            yield return null;
        }

        // 3) 일제 돌격
        if(chaseBoss && Alive(boss)) { hitAt = boss.transform.position; hitAt.z = 0f; }
        var from = (Vector3[])pos.Clone();
        for(int i = 0; i < n; i++)
            if(roots[i].TryGetComponent(out TrailRenderer tr)) { tr.time = 0.4f; tr.widthMultiplier = 0.5f; }   // 긴 꼬리
        for(float t = 0f; t < 0.18f; t += Time.deltaTime) {
            float k = t / 0.18f;
            for(int i = 0; i < n; i++) {
                pos[i] = Vector3.Lerp(from[i], hitAt, k * k);
                roots[i].transform.position = pos[i];
            }
            yield return null;
        }

        // 4) 한 방
        for(int i = 0; i < n; i++) {
            roots[i].transform.position = hitAt;
            EndProjectile(roots[i], heads[i]);
        }
        float R = M("ult_hound_final_radius", 2.5f);
        StartCoroutine(Flash(new Color(1f, 0.3f, 0.25f), 0.45f, 0.3f));
        RemasterFx.Burst(hitAt, R, red);
        RemasterFx.Burst(hitAt, R * 0.4f, Color.white);
        RemasterFx.Ring(hitAt, R * 1.2f, red, 0.35f);
        RemasterFx.Ring(hitAt, R * 0.75f, new Color(1f, 0.6f, 0.5f), 0.25f);
        float sa = Random.Range(25f, 65f) * Mathf.Deg2Rad;   // 거대 발톱 자국 3줄
        var sd = new Vector3(Mathf.Cos(sa), -Mathf.Sin(sa), 0f);
        var side = new Vector3(-sd.y, sd.x, 0f);
        for(int k = -1; k <= 1; k++) StartCoroutine(Slash(hitAt + side * (k * 0.55f) - sd * R, hitAt + side * (k * 0.55f) + sd * R, red, 0.32f));
        foreach(var e in AugmentManager.EnemiesNear(hitAt, R)) {
            Hit(e, finalHit);
            if(e._state != Enemy.EnemyState.dead) e.Stun(0.8f);
        }
        foreach(var kv in marks) {   // 같은 순간 사냥 표식이 붙은 적이 모두 터져요
            if(Alive(kv.Key)) {
                Vector3 p = kv.Key.transform.position;
                p.z = 0f;
                RemasterFx.Burst(p, 0.75f, red);
                Claw(p, new Color(1f, 0.45f, 0.4f));
                Hit(kv.Key, markHit);
            }
            if(kv.Value != null) Destroy(kv.Value.gameObject);
        }
        yield return new WaitForSeconds(0.3f);
    }

    // 사냥 표식이 적을 따라다니며 두근거려요 (죽은 적의 표식은 그 자리에 남았다가 마지막에 같이 사라져요)
    static void MoveMarks(Dictionary<Enemy, SpriteRenderer> marks, float clock)
    {
        foreach(var kv in marks) {
            if(kv.Value == null || !Alive(kv.Key)) continue;
            Vector3 p = kv.Key.transform.position;
            p.z = 0f;
            kv.Value.transform.position = p;
            kv.Value.transform.localScale = Vector3.one * (0.45f + 0.08f * Mathf.Sin(clock * 12f));
        }
    }

    // 큰 발톱 자국: a → b 로 순식간에 그어지고(0.06초) 0.35초 동안 사라져요 (가운데가 굵은 칼날 모양)
    IEnumerator Slash(Vector3 a, Vector3 b, Color c, float width)
    {
        var lr = NewLine(c, width);
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.1f));
        lr.widthMultiplier = width;
        lr.sortingOrder = 560;
        for(float t = 0f; t < 0.06f; t += Time.deltaTime) {
            SetSeg(lr, a, Vector3.Lerp(a, b, t / 0.06f));
            yield return null;
        }
        SetSeg(lr, a, b);
        for(float t = 0f; t < 0.35f; t += Time.deltaTime) {
            float k = 1f - t / 0.35f;
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, k);
            lr.widthMultiplier = width * (0.6f + 0.4f * k);
            yield return null;
        }
        Destroy(lr.gameObject);
    }


    // 발톱 자국: 비스듬한 붉은 줄 3개
    static void Claw(Vector3 p, Color c)
    {
        float a = Random.Range(20f, 70f) * Mathf.Deg2Rad;
        var d = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);
        var side = new Vector3(-d.y, d.x, 0f);
        for(int k = -1; k <= 1; k++) {
            Vector3 c0 = p + side * (k * 0.16f);
            RemasterFx.Line(c0 - d * 0.45f, c0 + d * 0.45f, c);
        }
        RemasterFx.Burst(p, 0.3f, c);
    }

    // 6 독의 그물 (2026-11-21 다시 만듦, 예전 "독의 장막"): 전염
    //  우주선에서 가장 가까운 적에게 독침 → 독 줄기가 적에서 적으로(가까운 적 최대 3마리씩, 3칸 안) 번지며 화면의 적을 그물처럼 이어요.
    //  가까이에 없으면 가장 가까운 안 걸린 적에게 길게 뻗어요. 옮을 때마다 독 + 둔화 + 피해.
    //  다 번지면 그물이 두 번 맥동하며 걸린 적 모두에게 피해. 숫자 ult_toxsat_*
    IEnumerator ToxicVeil(long dmg)
    {
        var green = new Color(0.55f, 1f, 0.4f);
        long infect = Mul(dmg, M("ult_toxsat_mul", 1.5f)), pulse = Mul(dmg, M("ult_toxsat_pulse_mul", 1f));
        float hop = Mathf.Max(0.03f, M("ult_toxsat_hop", 0.1f)), reach = M("ult_toxsat_reach", 3f), limit = M("ult_toxsat_time", 3f);
        int fan = Mathf.Max(1, Mathf.RoundToInt(M("ult_toxsat_fan", 3f)));
        Vector3 o = Ship;
        o.z = 0f;
        var start = AugmentManager.NearestInRange(o, 1);
        if(start.Count == 0) yield break;
        var infected = new HashSet<Enemy>();
        var web = new List<(LineRenderer lr, Enemy from, Enemy to)>();   // from 이 없으면 우주선에서
        var frontier = new List<Enemy>();
        var pending = new List<(LineRenderer lr, Enemy from, Enemy to)> { (WebLine(green), null, start[0]) };
        infected.Add(start[0]);
        float t = 0f;
        while(pending.Count > 0) {
            // 줄이 뻗어 나가요
            for(float k = 0f; k < hop; k += Time.deltaTime) {
                t += Time.deltaTime;
                foreach(var p in pending) {
                    Vector3 a = WebEnd(p.from, o), b = Alive(p.to) ? p.to.transform.position : a;
                    b.z = 0f;
                    SetSeg(p.lr, a, Vector3.Lerp(a, b, k / hop));
                }
                UpdateWeb(web, o);
                yield return null;
            }
            frontier.Clear();
            foreach(var p in pending) {
                web.Add(p);
                if(!Alive(p.to)) continue;
                p.to.AddPoison(5, 6f);
                p.to.AddSlow(0.3f, 3f);
                RemasterFx.Burst(p.to.transform.position, 0.45f, green);
                Hit(p.to, infect);
                frontier.Add(p.to);
            }
            UpdateWeb(web, o);
            pending.Clear();
            if(t >= limit) break;
            // 다음으로 옮을 적
            foreach(var src in frontier) {
                if(!Alive(src)) continue;
                Vector3 sp = src.transform.position;
                foreach(var e in AugmentManager.NearestInRange(sp, fan, infected)) {
                    if((e.transform.position - sp).magnitude > reach) break;
                    infected.Add(e);
                    pending.Add((WebLine(green), src, e));
                }
            }
            if(pending.Count == 0) {   // 근처에 없으면: 걸린 적 중 가장 가까운 쪽에서 안 걸린 적에게 길게
                Enemy bestE = null, bestFrom = null;
                float bd = float.MaxValue;
                foreach(var e in AugmentManager.NearestInRange(o, 999, infected)) {
                    foreach(var s in infected) {
                        if(!Alive(s)) continue;
                        float d = (e.transform.position - s.transform.position).sqrMagnitude;
                        if(d < bd) { bd = d; bestE = e; bestFrom = s; }
                    }
                }
                if(bestE == null) break;
                infected.Add(bestE);
                pending.Add((WebLine(green), bestFrom, bestE));
            }
        }
        // 그물이 두 번 맥동
        for(int pz = 0; pz < 2; pz++) {
            for(float k = 0f; k < 0.25f; k += Time.deltaTime) {
                float wv = Mathf.Lerp(0.2f, 0.05f, k / 0.25f);
                foreach(var p in web) p.lr.startWidth = p.lr.endWidth = wv;
                UpdateWeb(web, o);
                yield return null;
            }
            foreach(var e in infected) {
                if(!Alive(e)) continue;
                RemasterFx.Burst(e.transform.position, 0.6f, green);
                Hit(e, pulse);
            }
        }
        for(float k = 0f; k < 0.3f; k += Time.deltaTime) {   // 사라지기
            foreach(var p in web) p.lr.startColor = p.lr.endColor = new Color(green.r, green.g, green.b, 0.75f * (1f - k / 0.3f));
            UpdateWeb(web, o);
            yield return null;
        }
        foreach(var p in web) Destroy(p.lr.gameObject);
    }

    static LineRenderer WebLine(Color c) => NewLine(new Color(c.r, c.g, c.b, 0.75f), 0.05f);

    static Vector3 WebEnd(Enemy e, Vector3 ship)
    {
        Vector3 p = Alive(e) ? e.transform.position : ship;
        p.z = 0f;
        return p;
    }

    // 그물 줄이 적을 따라 움직여요 (죽은 적 쪽 끝은 그 자리에 남아요)
    static void UpdateWeb(List<(LineRenderer lr, Enemy from, Enemy to)> web, Vector3 ship)
    {
        foreach(var p in web) {
            if(p.lr == null) continue;
            Vector3 a = p.from == null ? ship : (Alive(p.from) ? p.from.transform.position : p.lr.GetPosition(0));
            Vector3 b = Alive(p.to) ? p.to.transform.position : p.lr.GetPosition(1);
            a.z = b.z = 0f;
            SetSeg(p.lr, a, b);
        }
    }


    // 7 무지개 심판 (2026-11-21 사용자 요청: "네 입맛대로 시원하게, 임팩트 있게")
    //  1) 모으기: 하늘에서 하얀 빛기둥이 우주선(프리즘)에 꽂히고, 우주선에 빛이 점점 모여요
    //  2) 분광: 빛이 7색 빔 부채로 갈라져 우주선을 축으로 화면을 한 바퀴 쓸어요. 빔마다 지나가는 적을 한 번씩 (최대 7번)
    //  3) 마무리: 빔이 사라지는 순간 하얀 충격파가 화면 끝까지 퍼지며 모든 적에게 한 번 더
    //  tuning.txt ult_prism_* (피해 숫자는 나중에 한꺼번에)
    IEnumerator RainbowJudgment(long dmg)
    {
        float w = HalfW, h = HalfH;
        float reach = Mathf.Sqrt(w * w + h * h) * 1.15f;   // 화면 대각선보다 조금 길게
        int n = Mathf.Clamp(Mathf.RoundToInt(M("ult_prism_beams", 7f)), 1, 31);
        float spread = M("ult_prism_spread", 70f), width = M("ult_prism_width", 0.75f);
        long sweepHit = Mul(dmg, M("ult_prism_hit_mul", 0.6f)), finalHit = Mul(dmg, M("ult_prism_final_mul", 2f));

        // 1) 모으기
        float charge = M("ult_prism_charge", 0.55f);
        var pillar = NewLine(Color.white, 0.05f);
        var pillarGlow = NewLine(new Color(0.8f, 0.9f, 1f, 0.5f), 0.2f);
        var orb = Shape(Color.white, Vector2.one * 0.2f, 535);
        for(float t = 0f; t < charge; t += Time.deltaTime) {
            float k = t / charge;
            Vector3 s = Ship; s.z = 0f;
            SetSeg(pillar, s + Vector3.up * reach, s);
            SetSeg(pillarGlow, s + Vector3.up * reach, s);
            pillar.startWidth = pillar.endWidth = Mathf.Lerp(0.05f, 0.35f, k);
            pillarGlow.startWidth = pillarGlow.endWidth = Mathf.Lerp(0.2f, 1.1f, k);
            orb.transform.position = s;
            orb.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.4f, k * k) * (1f + 0.15f * Mathf.Sin(t * 50f));
            if(Random.value < 0.35f) {   // 사방에서 빛 조각이 빨려 들어와요
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
                RemasterFx.Line(s + (Vector3)r, s + (Vector3)r * 0.3f, Color.HSVToRGB(Random.value, 0.5f, 1f));
            }
            yield return null;
        }
        Destroy(pillar.gameObject);
        Destroy(pillarGlow.gameObject);
        Destroy(orb.gameObject);

        // 2) 분광: 7색 빔 부채가 한 바퀴
        Vector3 c0 = Ship; c0.z = 0f;
        RemasterFx.Burst(c0, 2.2f, Color.white);
        RemasterFx.Ring(c0, 3.5f, Color.white, 0.35f);
        var glow = new LineRenderer[n];
        var core = new LineRenderer[n];
        for(int i = 0; i < n; i++) {
            glow[i] = NewLine(Color.white, width);
            core[i] = NewLine(Color.white, width * 0.3f);
            core[i].sortingOrder = glow[i].sortingOrder + 1;
        }
        var hitMask = new Dictionary<Enemy, int>();   // 빔마다 한 번씩
        float sweep = M("ult_prism_sweep", 1.6f), start = Random.Range(0f, 360f), nextSpark = 0f;
        for(float t = 0f; t < sweep; t += Time.deltaTime) {
            float k = t / sweep;
            float turn = 360f * (k - Mathf.Sin(k * Mathf.PI * 2f) / (Mathf.PI * 2f));   // 천천히 → 빠르게 → 천천히 한 바퀴
            float open = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((sweep - t) / 0.15f);   // 처음엔 부채가 펼쳐지고, 끝에 접혀요
            Vector3 c = Ship; c.z = 0f;
            bool spark = t >= nextSpark;
            if(spark) nextSpark = t + 0.06f;
            for(int i = 0; i < n; i++) {
                float f = n > 1 ? (float)i / (n - 1) : 0.5f;
                float ang = (start + turn + (f - 0.5f) * spread * open) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 end = c + dir * reach * Mathf.Clamp01(t / 0.12f);
                var col = Color.HSVToRGB(f * 0.83f, 0.75f, 1f);   // 빨강 → 보라
                float flick = 1f + 0.15f * Mathf.Sin(t * 45f + i);
                SetSeg(glow[i], c, end);
                SetSeg(core[i], c, end);
                glow[i].startWidth = width * 0.4f; glow[i].endWidth = width * flick;
                core[i].startWidth = width * 0.12f; core[i].endWidth = width * 0.3f * flick;
                glow[i].startColor = glow[i].endColor = new Color(col.r, col.g, col.b, 0.7f);
                core[i].startColor = core[i].endColor = new Color(1f, 1f, 1f, 0.95f);
                if(spark) RemasterFx.Burst(c + dir * Random.Range(1f, reach), width * 0.6f, col);
                // 빔에 닿은 적 (빔마다 한 번)
                float half = width * 0.5f + 0.2f;
                foreach(var e in AugmentManager.EnemiesNear(c, reach)) {
                    Vector3 d = e.transform.position - c; d.z = 0f;
                    float along = Vector3.Dot(d, dir);
                    if(along <= 0f || (d - dir * along).magnitude > half) continue;
                    hitMask.TryGetValue(e, out int m);
                    if((m & (1 << i)) != 0) continue;
                    hitMask[e] = m | (1 << i);
                    RemasterFx.Burst(e.transform.position, 0.35f, col);
                    Hit(e, sweepHit);
                }
            }
            yield return null;
        }
        for(int i = 0; i < n; i++) { Destroy(glow[i].gameObject); Destroy(core[i].gameObject); }

        // 3) 마무리: 하얀 충격파가 화면 끝까지
        Vector3 cf = Ship; cf.z = 0f;
        RemasterFx.Shockwave(cf);
        RemasterFx.Burst(cf, Mathf.Max(w, h) * 1.8f, new Color(1f, 1f, 1f, 0.6f));
        for(int i = 0; i < 7; i++) RemasterFx.Ring(cf, 2f + i * 1.2f, Color.HSVToRGB(i / 7f, 0.6f, 1f), 0.3f + i * 0.05f);
        foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) Hit(e, finalHit);
        yield return new WaitForSeconds(0.3f);
    }

    // 8 사건의 지평선 (2026-11-20 사용자 요청: 모으고 → 레이저로 지지다가 → 텀 → 저격으로 마무리)
    //  1) 거대 블랙홀이 화면의 적을 빨아들여 둘레를 돌게 해요 (우주선에서 떨어진 곳, 끌려가는 동안엔 우주선에 닿아도 피해 없음)
    //  2) 우주선에서 구멍 한가운데로 레이저를 계속 쏴서 모인 적을 지져요 (점점 굵어져요)
    //  3) 레이저가 꺼지고 잠깐 조준선만 (긴장)
    //  4) 초거대 저격 한 발로 마무리 (모두 치명타)
    IEnumerator EventHorizon(long dmg)
    {
        float w = HalfW;
        Vector3 c = BlackHole.AwayFromShip(Tuning.Get("ult_hole_min_dist", 3f));
        // 크기 (2026-11-20: 너무 커서 줄였어요 — 그림 지름 화면 반폭 x1.4 → x0.8, 도는 원 x0.35 → x0.15)
        float size = w * M("ult_singularity_size", 0.8f);
        var hole = BlackHole.Visual(c, size);
        float hold = w * M("ult_singularity_hold", 0.15f);
        float t = 0f;
        float R = Mathf.Max(size * 0.5f, hold * 1.6f);   // 피해 범위 (둘레를 도는 적이 다 들어가요)

        // 1) 모으기
        float gather = M("ult_singularity_gather_time", 2.2f);
        while(t < gather) {
            t += Time.deltaTime;
            hole.transform.localScale = Vector3.one * size * Mathf.Clamp01(t / 0.4f);
            BlackHole.PullToSlots(c, AugmentManager.EnemiesNear(c, 999f), hold, 6f, t, Time.deltaTime);
            yield return null;
        }

        // 2) 레이저로 지지기
        var laser = NewLine(new Color(0.6f, 0.9f, 1f), 0.1f);
        var core = NewLine(Color.white, 0.04f);
        core.sortingOrder = laser.sortingOrder + 1;
        float laserTime = M("ult_singularity_laser_time", 2f), lt = 0f, tick = 0f;
        long laserHit = Mul(dmg, M("ult_singularity_laser_mul", 0.15f));
        while(lt < laserTime) {
            float dt = Time.deltaTime;
            lt += dt; t += dt;
            BlackHole.PullToSlots(c, AugmentManager.EnemiesNear(c, 999f), hold, 6f, t, dt);
            Vector3 from = Ship; from.z = 0f;
            float k = lt / laserTime, flick = 1f + 0.1f * Mathf.Sin(lt * 50f);
            Vector3 far = c + (c - from).normalized * 30f;   // 블랙홀에서 끊지 않고 화면 끝까지 쭉 (2026-11-20)
            SetLine(laser, from, far, Mathf.Lerp(0.08f, 0.5f, k) * flick);
            SetLine(core, from, far, Mathf.Lerp(0.03f, 0.18f, k) * flick);
            tick -= dt;
            if(tick <= 0f) {
                tick = 0.1f;
                // 블랙홀에 모인 적 + 빔이 지나가는 길의 적
                var hitSet = new HashSet<Enemy>(AugmentManager.EnemiesNear(c, R));
                Vector3 dir = (c - from).normalized;
                float half = Mathf.Lerp(0.08f, 0.5f, k) * 0.5f + 0.2f;
                foreach(var e in AugmentManager.EnemiesNear(from, 999f)) {
                    Vector3 d = e.transform.position - from; d.z = 0f;
                    float along = Vector3.Dot(d, dir);
                    if(along > 0f && (d - dir * along).magnitude <= half) hitSet.Add(e);
                }
                foreach(var e in hitSet) Hit(e, laserHit);
            }
            yield return null;
        }
        Destroy(laser.gameObject);
        Destroy(core.gameObject);

        // 3) 텀: 조준선만 깜빡이며 잠깐 멈춰요
        var aim = NewLine(new Color(1f, 0.25f, 0.3f, 0.9f), 0.03f);
        float aimTime = M("ult_singularity_aim_time", 0.7f), at = 0f;
        while(at < aimTime) {
            float dt = Time.deltaTime;
            at += dt; t += dt;
            BlackHole.PullToSlots(c, AugmentManager.EnemiesNear(c, 999f), hold, 6f, t, dt);
            Vector3 from = Ship; from.z = 0f;
            SetLine(aim, from, c + (c - from).normalized * 30f, 0.03f);
            aim.enabled = Mathf.Repeat(at, 0.16f) < 0.1f;
            yield return null;
        }
        Destroy(aim.gameObject);

        // 4) 저격으로 마무리
        long hit = Mul(dmg, M("ult_singularity_mul", 6f));
        // 저격총처럼 (2026-11-20): 총구 불꽃 → 밝은 탄도선 "탕" → 맞은 자리 불꽃 + 보라 고리 (예전엔 보라색 굵은 빔)
        Vector3 muzzle = Ship; muzzle.z = 0f;
        Vector3 sdir = (c - muzzle).normalized;
        var gold = new Color(1f, 0.93f, 0.65f);
        RemasterFx.Burst(muzzle + sdir * 0.35f, 0.4f, gold);
        float ultBeam = Tuning.Get("ult_singularity_beam_width", 0.6f);   // 2026-11-21: 너무 얇아서 굵게 (예전 0.14)
        RemasterFx.Beam(muzzle, muzzle + sdir * 30f, gold, ultBeam);
        RemasterFx.Beam(muzzle, muzzle + sdir * 30f, new Color(1f, 1f, 1f, 0.9f), ultBeam * 0.35f);
        RemasterFx.Burst(c, 1f, gold);
        RemasterFx.Ring(c, R, new Color(0.75f, 0.4f, 1f), 0.35f);
        foreach(var e in AugmentManager.EnemiesNear(c, R))
            AugmentManager.WeaponHit(e, hit, true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
        Destroy(hole);
    }

    static Material lineMat;   // 선마다 새 재질을 만들면 쌓여서 하나를 같이 써요 (색은 선마다 따로)

    static LineRenderer NewLine(Color color, float width)
    {
        var go = new GameObject("ultLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        if(lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
        lr.sharedMaterial = lineMat;
        lr.sortingLayerName = RemasterFx.FxLayer;
        lr.sortingOrder = 530;
        lr.numCapVertices = 4;
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = width;
        return lr;
    }

    static void SetLine(LineRenderer lr, Vector3 from, Vector3 to, float width)
    {
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = width * 0.7f;
        lr.endWidth = width;
    }

    // 9 중력 대폭격 (예전 이름 "중력 붕괴", 중력 계열 증강과 겹쳐서 바꿈): 화면의 모든 적을 끌어모아 둘레를 돌게 한 뒤 거대 폭탄이 연달아 (사건의 지평선과 같은 방식으로 모아요)
    IEnumerator GravityCollapse(long dmg)
    {
        float w = HalfW;
        Vector3 c = BlackHole.AwayFromShip(Tuning.Get("ult_hole_min_dist", 3f));
        var hole = BlackHole.Visual(c, w);
        float t = 0f, hold = w * Tuning.Get("ult_hole_hold", 0.35f) * 0.8f;
        while(t < 1.8f) {
            t += Time.deltaTime;
            hole.transform.localScale = Vector3.one * w * Mathf.Clamp01(t / 0.4f);
            BlackHole.PullToSlots(c, AugmentManager.EnemiesNear(c, 999f), hold, 6f, t, Time.deltaTime);
            yield return null;
        }
        Destroy(hole);
        long hit = Mul(dmg, M("ult_gravbomb_mul", 4f));
        var orange = new Color(1f, 0.6f, 0.2f);
        Boom(c, w * 0.8f, hit, orange);
        for(int i = 0; i < 6; i++) {
            yield return new WaitForSeconds(0.12f);
            float a = i * 60f * Mathf.Deg2Rad;
            Boom(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * w * 0.55f, w * 0.4f, Mul(hit, 0.5), orange);
        }
    }

    // 11 천벌 (2026-11-21 사용자 결정, 별의 낙인 종결)
    //  1) 화면의 모든 적(최대 ult_brand_max)에게 빨간 낙인이 촤라락 찍혀요 (0.5초 동안 가까운 적부터)
    //  2) 하늘 여기저기서 운석이 낙인 찍힌 적들에게 돌아가며 ult_brand_per(4)개씩 2초 동안 쏟아져요. 못 피하고 모두 치명타, 보스 추가 피해
    //  3) 보스가 있으면 마지막에 금빛 거대 운석이 보스에게 (화면 번쩍). 숫자 ult_brand_*
    IEnumerator Judgment(long dmg)
    {
        var red = new Color(1f, 0.2f, 0.25f);
        Vector3 o = Ship;
        o.z = 0f;
        var list = AugmentManager.NearestInRange(o, Mathf.Max(1, Mathf.RoundToInt(M("ult_brand_max", 40f))));
        if(list.Count == 0) yield break;
        var marks = new Dictionary<Enemy, SpriteRenderer>();
        float clock = 0f, wait = 0f, step = 0.5f / list.Count;
        StartCoroutine(Flash(red, 0.18f, 0.25f));
        foreach(var e in list) {   // 1) 낙인
            if(!Alive(e)) continue;
            marks[e] = Shape(new Color(1f, 0.15f, 0.2f, 0.6f), Vector2.one * 0.5f, 549);
            RemasterFx.Ring(e.transform.position, 0.6f, red, 0.2f);
            wait += step;
            while(wait >= Time.deltaTime) { wait -= Time.deltaTime; clock += Time.deltaTime; MoveMarks(marks, clock); yield return null; }
        }
        // 2) 운석 비 (2026-11-21 사용자: "한 적에 더 많이, 피해는 줄이고" → "한 줄로 떨어뜨리지 말고 2초 동안"):
        //    낙인 적마다 운석 ult_brand_per 개. 한 적씩 몰아서가 아니라 모든 낙인 적에게 돌아가며 섞어서 ult_brand_time 초 동안 떨어지고,
        //    운석마다 하늘 출발점이 달라서 한 줄로 안 보여요. 1발 피해 = ult_brand_mul ÷ ult_brand_per. 그 적의 마지막 운석이 떨어지면 낙인이 사라져요
        int per = Mathf.Max(1, Mathf.RoundToInt(M("ult_brand_per", 4f)));
        long hit = Mul(dmg, M("ult_brand_mul", 2f) / per);
        float r = M("ult_brand_radius", 0.8f), rain = M("ult_brand_time", 2f);
        int landed = 0, dropped = 0;
        var targets = new List<Enemy>(marks.Keys);
        int total = targets.Count * per;
        var left = new Dictionary<Enemy, int>();
        foreach(var e in targets) left[e] = per;
        float gap = rain / Mathf.Max(1, total), next = 0f;
        for(float t = 0f; landed < dropped || dropped < total; t += Time.deltaTime) {
            clock += Time.deltaTime;
            while(dropped < total && t >= next) {
                var e = targets[dropped % targets.Count];   // 모든 낙인 적에게 돌아가며 (한 바퀴마다 1개씩)
                next += gap;
                dropped++;
                var mark = marks[e];
                if(!Alive(e)) { landed++; if(--left[e] <= 0 && mark != null) Destroy(mark.gameObject); continue; }
                var who = e;
                MeteorFall.Drop(new MeteorFall.Opts {
                    at = e.transform.position, follow = e, dmg = hit, radius = r, forceCrit = true, bossBonus = true, pen = Armor.UltPen,
                    fallTime = Random.Range(0.3f, 0.5f), sound = dropped % 6 == 1, randomSky = true,
                    onLand = p => { landed++; if(--left[who] <= 0 && mark != null) Destroy(mark.gameObject); },
                });
            }
            MoveMarks(marks, clock);
            if(t > rain + 3f) break;   // 안전장치
            yield return null;
        }
        foreach(var kv in marks) if(kv.Value != null) Destroy(kv.Value.gameObject);
        // 3) 보스에게 거대 운석
        var boss = AugmentManager.StrongestInRange();
        if(boss != null && boss.IsBoss) {
            bool done = false;
            var gold = new Color(1f, 0.82f, 0.35f);
            MeteorFall.Drop(new MeteorFall.Opts {
                at = boss.transform.position, follow = boss, dmg = Mul(dmg, M("ult_brand_boss_mul", 10f)), radius = M("ult_brand_boss_radius", 2f),
                forceCrit = true, bossBonus = true, pen = Armor.UltPen, big = true, brand = true, fallTime = 0.7f, sound = true, color = gold,
                onLand = p => { done = true; StartCoroutine(Flash(new Color(1f, 0.9f, 0.6f), 0.5f, 0.3f)); },
            });
            for(float t = 0f; !done && t < 2f; t += Time.deltaTime) yield return null;
        }
        yield return new WaitForSeconds(0.3f);
    }

    // 12 소행성 충돌 (2026-11-21 사용자 결정, 대충돌 종결)
    //  1) 블랙홀이 먼저 열려 ult_impact_gather 초 동안 화면의 모든 적을 끌어모아요 (보스는 약하게)
    //  2) 다 모이면 하늘에서 거대 소행성이 불꽃 꼬리를 끌며 내려와요 (ult_impact_time). 블랙홀은 그동안 계속 붙잡고 있어요
    //  3) 충돌: 화면이 번쩍이고 흔들리며, 반경 ult_impact_radius 안은 큰 피해, 그 밖의 화면 적은 충격파 피해. 그다음 여진: 둘레에 작은 폭발이 고리처럼 터져요. 숫자 ult_impact_*
    IEnumerator AsteroidImpact(long dmg)
    {
        Vector3 cam = Cam;
        // 떨어질 자리: 적이 가장 몰린 곳. 우주선에서 ult_impact_min_dist 칸 안이면 우주선 반대쪽으로 밀고 화면 안으로 (2026-11-21: 우주선 근처라서)
        Vector3 hitAt = Densest(cam);
        hitAt.z = 0f;
        {
            Vector3 ship = Ship;
            ship.z = 0f;
            float md = M("ult_impact_min_dist", 4f);
            Vector3 d = hitAt - ship;
            if(d.sqrMagnitude < 0.0001f) d = cam - ship;
            if(d.sqrMagnitude < 0.0001f) d = Vector3.up;
            if(d.magnitude < md) hitAt = ship + d.normalized * md;
            hitAt.x = Mathf.Clamp(hitAt.x, cam.x - HalfW + 1f, cam.x + HalfW - 1f);
            hitAt.y = Mathf.Clamp(hitAt.y, cam.y - HalfH + 1.5f, cam.y + HalfH - 1.5f);
        }
        float gather = M("ult_impact_gather", 1.3f), dur = M("ult_impact_time", 1f), R = M("ult_impact_radius", 4.5f);
        float pull = M("ult_impact_pull", 8f);
        float orbit = M("ult_impact_orbit", 0.8f);   // 적들이 블랙홀 둘레를 도는 거리 (2026-11-21 사용자: 지름을 조금 줄여 달라고 해서 1.35 → 0.8)
        var orange = new Color(1f, 0.55f, 0.2f);
        var purple = new Color(0.7f, 0.4f, 1f);
        float clock = 0f;
        // 1) 블랙홀이 먼저 열려 화면의 적을 모두 끌어모아요 (2026-11-21 사용자: "애들이 모이기 전에 소행성이 떨어져")
        var hole = BlackHole.Visual(hitAt, 0f);
        float holeD = M("ult_impact_hole", 2.4f);
        RemasterFx.Ring(hitAt, R, purple, 0.35f);
        for(float t = 0f; t < gather; t += Time.deltaTime) {
            clock += Time.deltaTime;
            hole.transform.localScale = Vector3.one * holeD * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.3f)) * (1f + 0.05f * Mathf.Sin(clock * 10f));
            var all = AugmentManager.EnemiesNear(hitAt, 999f);
            all.RemoveAll(e => !e.IsInRange);
            BlackHole.PullToSlots(hitAt, all, orbit, pull, clock, Time.deltaTime);
            if(Random.value < 0.4f) {   // 빨려 드는 보라 줄기
                Vector2 rr = Random.insideUnitCircle.normalized * Random.Range(R * 0.8f, R * 1.4f);
                RemasterFx.Line(hitAt + (Vector3)rr, hitAt + (Vector3)rr * 0.55f, purple);
            }
            yield return null;
        }
        // 2) 다 모이면 하늘에서 거대 소행성 (블랙홀은 계속 붙잡고 있어요)
        var shadow = Shape(new Color(0.35f, 0.05f, 0.05f, 0.1f), Vector2.zero, 3);
        shadow.sortingLayerName = "Mine";
        shadow.transform.position = hitAt;
        var (root, head) = Projectile(hitAt + new Vector3(-3f, HalfH + 3f, 0f), RemasterFx.CircleSprite, new Color(0.62f, 0.4f, 0.28f), 2.4f, 547);
        var rim = Sprite2(RemasterFx.CircleSprite, new Color(1f, 0.5f, 0.2f, 0.55f), 2.9f, 546);
        rim.transform.SetParent(root.transform, false);
        var hot = Sprite2(RemasterFx.CircleSprite, new Color(1f, 0.92f, 0.7f), 1.1f, 548);
        hot.transform.SetParent(root.transform, false);
        hot.transform.localPosition = new Vector3(0.25f, -0.35f, 0f);
        AddTrail(root, new Color(1f, 0.8f, 0.45f, 0.95f), new Color(0.8f, 0.15f, 0.05f), 2.2f, 0.5f);
        Vector3 from = root.transform.position;
        for(float t = 0f; t < dur; t += Time.deltaTime) {
            clock += Time.deltaTime;
            float k = t / dur;
            root.transform.position = Vector3.Lerp(from, hitAt, k * k);
            root.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.3f, k);
            head.transform.Rotate(0f, 0f, 60f * Time.deltaTime);
            shadow.transform.localScale = Vector3.one * R * 2f * Mathf.Lerp(0.3f, 1f, k);
            shadow.color = new Color(0.45f, 0.05f, 0.05f, 0.08f + 0.25f * k);
            hole.transform.localScale = Vector3.one * holeD * (1f + 0.08f * Mathf.Sin(clock * 14f));
            var all = AugmentManager.EnemiesNear(hitAt, 999f);
            all.RemoveAll(e => !e.IsInRange);
            BlackHole.PullToSlots(hitAt, all, orbit, pull, clock, Time.deltaTime);
            if(Random.value < 0.5f) {   // 부서져 날리는 돌가루
                Vector3 p = root.transform.position + (Vector3)(Random.insideUnitCircle * 1.2f);
                RemasterFx.Line(p, p + (from - hitAt).normalized * Random.Range(0.6f, 1.4f), new Color(1f, 0.7f, 0.4f));
            }
            yield return null;
        }
        Destroy(hole);
        // 3) 충돌
        root.transform.position = hitAt;
        rim.enabled = hot.enabled = false;
        EndProjectile(root, head);
        Destroy(shadow.gameObject);
        StartCoroutine(Flash(new Color(1f, 0.85f, 0.6f), 0.7f, 0.45f));
        var camT = GameManager.instance.mainCamera.transform;
        camT.DOComplete();
        camT.DOShakePosition(M("ult_impact_shake_time", 0.45f), M("ult_impact_shake", 0.35f), 25, 90f, false, true);
        RemasterFx.Burst(hitAt, R, orange);
        RemasterFx.Burst(hitAt, R * 0.5f, Color.white);
        RemasterFx.Ring(hitAt, R * 1.3f, orange, 0.5f);
        RemasterFx.Ring(hitAt, R * 0.8f, Color.white, 0.35f);
        RemasterFx.Spark(hitAt, R, new Color(1f, 0.85f, 0.4f));
        long core = Mul(dmg, M("ult_impact_mul", 12f)), wave = Mul(dmg, M("ult_impact_edge_mul", 3f));
        var hitList = AugmentManager.EnemiesNear(hitAt, 999f);
        hitList.RemoveAll(e => !e.IsInRange);
        foreach(var e in hitList) {
            Vector3 d = e.transform.position - hitAt;
            d.z = 0f;
            Hit(e, d.magnitude <= R ? core : wave);
        }
        // 3) 여진
        yield return new WaitForSeconds(0.15f);
        int n = 10;
        for(int i = 0; i < n; i++) {
            float a = (i * 360f / n + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            SmallBlast(hitAt + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * R * 0.85f, 0.9f, dmg, orange);
            yield return new WaitForSeconds(0.04f);
        }
        yield return new WaitForSeconds(0.4f);
    }

    // 10 운명의 별자리 (2026-11-20 사용자 결정, 혜성탄 종결)
    //  항상 황도 12궁 (사용자 요청). 우주선에서 혜성 12개가 날아가 화면에 12개 별 자리에 서고,
    //  고리 한 칸 → 안쪽 별 한 칸씩 날아가며 꼬리로 별자리를 그려요 (적은 안 쫓아가요. 대신 지나가다 닿은 적은 피해).
    //  다 그리면 금빛으로 반짝인 뒤 화면 전체가 한 번에 펑. 글자는 띄우지 않아요 (사용자 요청).
    //  tuning.txt: ult_const_mul_12 (마지막 펑), ult_zodiac_comet_mul (혜성에 닿으면), ult_zodiac_launch·ult_zodiac_seg (시간)
    IEnumerator FateConstellation(long dmg)
    {
        var shape = StarArt.Shape(12);
        float w = HalfW, scale = w * 0.9f;
        Vector3 cam = GameManager.instance.mainCamera.transform.position;
        cam.z = 0f;
        Vector3 ship = Ship;
        ship.z = 0f;
        var pts = new Vector3[12];
        for(int i = 0; i < 12; i++) pts[i] = cam + (Vector3)(shape.stars[i] * scale);

        var starCol = new Color(1f, 0.96f, 0.82f);
        var glowCol = new Color(0.55f, 0.75f, 1f, 0.6f);
        var gold = new Color(1f, 0.85f, 0.35f);
        long cometHit = Mul(dmg, M("ult_zodiac_comet_mul", 1.5f));

        // 혜성 12개 (머리 + 짧은 꼬리). 혜성마다 한 번 닿은 적은 다시 안 때려요
        var heads = new SpriteRenderer[12];
        var tails = new LineRenderer[12];
        var touched = new HashSet<Enemy>[12];
        var at = new Vector3[12];
        for(int i = 0; i < 12; i++) {
            heads[i] = Shape(new Color(0.85f, 0.95f, 1f), Vector2.one * 0.5f, 545);
            heads[i].sprite = StarArt.Star;
            heads[i].transform.position = ship;
            tails[i] = NewLine(new Color(0.6f, 0.85f, 1f, 0.7f), 0.14f);
            touched[i] = new HashSet<Enemy>();
            at[i] = ship;
        }
        var stars = new SpriteRenderer[12];
        var lines = new List<LineRenderer>();   // [빛무리, 심] 짝으로

        // 1) 발사: 우주선에서 각자 별 자리로 (처음엔 빠르게, 도착할 땐 느리게)
        float launch = M("ult_zodiac_launch", 0.35f);
        for(float t = 0f; t < launch; t += Time.deltaTime) {
            float k = 1f - (1f - t / launch) * (1f - t / launch);
            for(int i = 0; i < 12; i++) MoveComet(i, Vector3.Lerp(ship, pts[i], k), heads, tails, touched, at, cometHit);
            yield return null;
        }
        for(int i = 0; i < 12; i++) { MoveComet(i, pts[i], heads, tails, touched, at, cometHit); ShowStar(stars, i, pts, 0.5f, starCol); }

        // 2) 그리기: 고리 한 칸(i → i+1) → 안쪽 별 한 칸(i → i+5). 혜성이 지나간 자리가 별자리 선이 돼요
        float seg = M("ult_zodiac_seg", 0.25f);
        foreach(int step in new[] { 1, 5 }) {
            var glow = new LineRenderer[12];
            var core = new LineRenderer[12];
            for(int i = 0; i < 12; i++) {
                glow[i] = NewLine(glowCol, 0.1f);
                core[i] = NewLine(new Color(1f, 1f, 1f, 0.95f), 0.035f);
                core[i].sortingOrder = glow[i].sortingOrder + 1;
                lines.Add(glow[i]);
                lines.Add(core[i]);
            }
            for(float t = 0f; t <= seg; t += Time.deltaTime) {
                float k = Mathf.Clamp01(t / seg);
                for(int i = 0; i < 12; i++) {
                    Vector3 a = pts[i], b = pts[(i + step) % 12], p = Vector3.Lerp(a, b, k);
                    MoveComet(i, p, heads, tails, touched, at, cometHit);
                    SetSeg(glow[i], a, p);
                    SetSeg(core[i], a, p);
                }
                yield return null;
            }
            for(int i = 0; i < 12; i++) {
                Vector3 a = pts[i], b = pts[(i + step) % 12];
                SetSeg(glow[i], a, b);
                SetSeg(core[i], a, b);
                MoveComet(i, b, heads, tails, touched, at, cometHit);
            }
        }
        for(int i = 0; i < 12; i++) { Destroy(heads[i].gameObject); Destroy(tails[i].gameObject); }

        // 3) 금빛으로 반짝
        const float flashTime = 0.25f;
        for(float t = 0f; t < flashTime; t += Time.deltaTime) {
            float k = t / flashTime;
            for(int i = 0; i < lines.Count; i += 2) {
                lines[i].startWidth = lines[i].endWidth = Mathf.Lerp(0.1f, 0.32f, k);
                lines[i].startColor = lines[i].endColor = Color.Lerp(glowCol, new Color(gold.r, gold.g, gold.b, 0.85f), k);
            }
            foreach(var sr in stars) if(sr != null) { sr.transform.localScale = Vector3.one * 0.5f * (1f + 0.8f * k); sr.color = Color.Lerp(starCol, gold, k); }
            yield return null;
        }

        // 4) 한 번에 펑: 화면 전체
        long hit = Mul(dmg, StarArt.TierMul(12));
        foreach(var p in pts) RemasterFx.Burst(p, 0.9f, gold);
        RemasterFx.Burst(cam, scale * 1.2f, new Color(gold.r, gold.g, gold.b, 0.5f));
        RemasterFx.Ring(cam, scale, gold, 0.5f);
        foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) {
            RemasterFx.Burst(e.transform.position, 0.45f, gold);
            Hit(e, hit);
        }

        // 5) 천천히 사라져요
        const float fadeTime = 0.45f;
        for(float t = 0f; t < fadeTime; t += Time.deltaTime) {
            float a = 1f - t / fadeTime;
            for(int i = 0; i < lines.Count; i++)
                lines[i].startColor = lines[i].endColor = i % 2 == 0 ? new Color(gold.r, gold.g, gold.b, 0.85f * a) : new Color(1f, 1f, 1f, 0.95f * a);
            foreach(var sr in stars) if(sr != null) sr.color = new Color(gold.r, gold.g, gold.b, a);
            yield return null;
        }
        foreach(var lr in lines) Destroy(lr.gameObject);
        foreach(var sr in stars) if(sr != null) Destroy(sr.gameObject);
    }

    // 종결 혜성 하나를 p 로 옮기고 (짧은 꼬리), 닿은 적에게 피해 (혜성마다 적 한 번)
    static void MoveComet(int i, Vector3 p, SpriteRenderer[] heads, LineRenderer[] tails, HashSet<Enemy>[] touched, Vector3[] at, long hit)
    {
        Vector3 d = p - at[i];
        if(d.sqrMagnitude > 0.000001f) {
            heads[i].transform.position = p;
            SetSeg(tails[i], p - d.normalized * 0.7f, p);
        }
        heads[i].transform.Rotate(0f, 0f, 9f);
        at[i] = p;
        foreach(var e in AugmentManager.EnemiesNear(p, 0.45f))
            if(touched[i].Add(e)) Hit(e, hit);
    }

    static void SetSeg(LineRenderer lr, Vector3 a, Vector3 b)
    {
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }


    // 혜성탄이 별자리를 완성했을 때 (2026-11-20 사용자 요청): 종결과 같은 연출·피해 방식으로, 혜성이 맞힌 자리에 그려요
    public static void PlayConstellation(Vector3[] pts, StarArt.ShapeDef shape, long hit, float seg, float band, float starR, float pen)
    {
        var fx = Get();
        fx.StartCoroutine(fx.DrawAndStrike(pts, shape, false, hit, seg, band, starR, pen, true, Vector3.zero, 0f));
    }

    // 별자리 그리기 → 금빛 반짝 → 별·선 아래 적에게 피해 → 사라지기. zodiac 이면 화면 전체
    // weaponGauge: 혜성탄이 부르면 종결 게이지도 채워요
    IEnumerator DrawAndStrike(Vector3[] pts, StarArt.ShapeDef shape, bool zodiac, long hit, float seg, float band, float starR, float pen,
                              bool weaponGauge, Vector3 center, float scale)
    {
        float starSize = zodiac ? 0.5f : 0.6f;
        var starCol = new Color(1f, 0.96f, 0.82f);
        var glowCol = new Color(0.55f, 0.75f, 1f, 0.6f);
        var gold = new Color(1f, 0.85f, 0.35f);
        var stars = new SpriteRenderer[pts.Length];
        var lines = new List<LineRenderer>();   // [빛무리, 심] 짝으로

        // 1) 그리기: 별이 하나씩 켜지고 선이 다음 별까지 뻗어요
        foreach(var path in shape.paths) {
            var glow = NewLine(glowCol, 0.1f);
            var core = NewLine(new Color(1f, 1f, 1f, 0.95f), 0.035f);
            core.sortingOrder = glow.sortingOrder + 1;
            lines.Add(glow);
            lines.Add(core);
            glow.positionCount = core.positionCount = 1;
            glow.SetPosition(0, pts[path[0]]);
            core.SetPosition(0, pts[path[0]]);
            ShowStar(stars, path[0], pts, starSize, starCol);
            for(int k = 1; k < path.Length; k++) {
                Vector3 a = pts[path[k - 1]], b = pts[path[k]];
                glow.positionCount = core.positionCount = k + 1;
                for(float t = 0f; t < seg; t += Time.deltaTime) {
                    Vector3 p = Vector3.Lerp(a, b, t / seg);
                    glow.SetPosition(k, p);
                    core.SetPosition(k, p);
                    yield return null;
                }
                glow.SetPosition(k, b);
                core.SetPosition(k, b);
                ShowStar(stars, path[k], pts, starSize, starCol);
            }
        }

        // 2) 완성: 선이 굵어지며 금빛으로 반짝
        const float flashTime = 0.25f;
        for(float t = 0f; t < flashTime; t += Time.deltaTime) {
            float k = t / flashTime;
            for(int i = 0; i < lines.Count; i += 2) {
                lines[i].startWidth = lines[i].endWidth = Mathf.Lerp(0.1f, 0.32f, k);
                lines[i].startColor = lines[i].endColor = Color.Lerp(glowCol, new Color(gold.r, gold.g, gold.b, 0.85f), k);
            }
            foreach(var sr in stars) {
                if(sr == null) continue;
                sr.transform.localScale = Vector3.one * starSize * (1f + 0.8f * k);
                sr.color = Color.Lerp(starCol, gold, k);
            }
            yield return null;
        }

        // 3) 별과 선 아래의 적이 쓰러져요 (12개는 화면 전체)
        foreach(var p in pts) RemasterFx.Burst(p, zodiac ? 0.9f : starR, gold);
        if(zodiac) RemasterFx.Ring(center, scale, gold, 0.5f);
        foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) {
            if(!zodiac && !Under(e.transform.position, pts, shape.paths, band, starR)) continue;
            RemasterFx.Burst(e.transform.position, 0.45f, gold);
            bool crit = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
            AugmentManager.WeaponHit(e, hit, crit, Player.instance.FinalCriticalDamage, HitKind.Weapon, pen);
            if(weaponGauge) AugmentManager.WeaponHitEvent(SideWeapon.BounceGun);
        }

        // 4) 천천히 사라져요
        const float fadeTime = 0.45f;
        for(float t = 0f; t < fadeTime; t += Time.deltaTime) {
            float a = 1f - t / fadeTime;
            for(int i = 0; i < lines.Count; i++)
                lines[i].startColor = lines[i].endColor = i % 2 == 0 ? new Color(gold.r, gold.g, gold.b, 0.85f * a) : new Color(1f, 1f, 1f, 0.95f * a);
            foreach(var sr in stars) if(sr != null) sr.color = new Color(gold.r, gold.g, gold.b, a);
            yield return null;
        }
        foreach(var lr in lines) Destroy(lr.gameObject);
        foreach(var sr in stars) if(sr != null) Destroy(sr.gameObject);
    }

    static void ShowStar(SpriteRenderer[] stars, int i, Vector3[] pts, float size, Color color)
    {
        if(stars[i] != null) return;
        var sr = Shape(color, Vector2.one * size, 540);
        sr.sprite = StarArt.Star;
        sr.transform.position = pts[i];
        stars[i] = sr;
        RemasterFx.Burst(pts[i], 0.35f, color);   // 별이 반짝 켜져요
    }

    // 별 근처이거나 선에서 band 안이면 별자리 아래
    static bool Under(Vector3 p, Vector3[] pts, int[][] paths, float band, float starR)
    {
        p.z = 0f;
        foreach(var s in pts) if((p - s).sqrMagnitude <= starR * starR) return true;
        foreach(var path in paths)
            for(int k = 1; k < path.Length; k++)
                if(DistToSegment(p, pts[path[k - 1]], pts[path[k]]) <= band) return true;
        return false;
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float l2 = ab.sqrMagnitude;
        float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
        return (p - (a + ab * t)).magnitude;
    }
}

// 별 그림과 별자리 모양 (코드로 그려요). 혜성탄·종결 "운명의 별자리"가 같이 써요
public static class StarArt
{
    static Sprite star;
    // 가운데가 밝은 네 갈래 반짝이 + 옅은 빛무리 (흰색, 색은 SpriteRenderer 로). 그림 폭 1 = 1칸
    public static Sprite Star => star != null ? star : (star = Make());

    static Sprite Make()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for(int y = 0; y < S; y++) for(int x = 0; x < S; x++) {
            float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
            float r2 = dx * dx + dy * dy;
            float core = Mathf.Exp(-r2 / (0.16f * 0.16f));
            float rayX = Mathf.Exp(-dy * dy / (0.05f * 0.05f)) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(dx)), 2f);
            float rayY = Mathf.Exp(-dx * dx / (0.05f * 0.05f)) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(dy)), 2f);
            float halo = 0.3f * Mathf.Exp(-r2 / (0.4f * 0.4f));
            px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, Mathf.Max(rayX, rayY)) + halo));
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    public class ShapeDef
    {
        public Vector2[] stars;   // 가로 -1~1 기준 좌표
        public int[][] paths;     // 한 획으로 그리는 별 순서 (여러 획이면 차례로)
    }

    // 삼각형자리(3) · 카시오페이아(5, W 모양) · 북두칠성(7, 국자) · 황도 12궁(12, 고리 + 안쪽 12각 별)
    public static ShapeDef Shape(int n)
    {
        switch(n) {
            case 3:
                return new ShapeDef {
                    stars = new[] { new Vector2(-0.85f, 0.3f), new Vector2(0.8f, 0.05f), new Vector2(0.45f, -0.5f) },
                    paths = new[] { new[] { 0, 1, 2, 0 } },
                };
            case 5:
                return new ShapeDef {
                    stars = new[] { new Vector2(-1f, 0.3f), new Vector2(-0.5f, -0.35f), new Vector2(0f, 0.1f), new Vector2(0.5f, -0.4f), new Vector2(1f, 0.35f) },
                    paths = new[] { new[] { 0, 1, 2, 3, 4 } },
                };
            case 7:
                // 손잡이 끝(알카이드)부터 그리고 마지막에 국자를 닫아요
                return new ShapeDef {
                    stars = new[] { new Vector2(-1f, 0.05f), new Vector2(-0.6f, 0.3f), new Vector2(-0.25f, 0.25f), new Vector2(0.2f, 0.15f),
                                    new Vector2(0.3f, -0.35f), new Vector2(0.95f, -0.25f), new Vector2(1f, 0.35f) },
                    paths = new[] { new[] { 0, 1, 2, 3, 4, 5, 6, 3 } },
                };
            default: {
                var s = new Vector2[12];
                for(int i = 0; i < 12; i++) {
                    float a = Mathf.PI / 2f - i * Mathf.PI * 2f / 12f;   // 12시부터 시계 방향
                    s[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                }
                var ring = new int[13];
                var inner = new int[13];
                for(int i = 0; i <= 12; i++) { ring[i] = i % 12; inner[i] = i * 5 % 12; }
                return new ShapeDef { stars = s, paths = new[] { ring, inner } };
            }
        }
    }

    // 점들(맞힌 순서)이 별자리 별 순서와 얼마나 다른지 (0 = 같은 모양, 1 = 전혀 다름).
    // 위치·크기·회전·좌우 뒤집기·순서 뒤집기는 맞춰서 봐요. reversed 면 pts 를 거꾸로 맞춘 것
    public static float ShapeError(Vector2[] tmpl, Vector3[] pts, out bool reversed)
    {
        reversed = false;
        float best = 1f;
        int k = tmpl.Length;
        if(pts.Length != k) return 1f;
        for(int m = 0; m < 2; m++) for(int r = 0; r < 2; r++) {
            Vector2 ca = Vector2.zero, cb = Vector2.zero;
            for(int i = 0; i < k; i++) { ca += new Vector2(tmpl[i].x * (m == 0 ? 1f : -1f), tmpl[i].y); cb += (Vector2)pts[r == 0 ? i : k - 1 - i]; }
            ca /= k; cb /= k;
            float re = 0f, im = 0f, na = 0f, nb = 0f;
            for(int i = 0; i < k; i++) {
                Vector2 a = new Vector2(tmpl[i].x * (m == 0 ? 1f : -1f), tmpl[i].y) - ca;
                Vector2 b = (Vector2)pts[r == 0 ? i : k - 1 - i] - cb;
                re += a.x * b.x + a.y * b.y;
                im += a.x * b.y - a.y * b.x;
                na += a.sqrMagnitude;
                nb += b.sqrMagnitude;
            }
            if(nb < 0.000001f) continue;
            float e = 1f - Mathf.Sqrt(re * re + im * im) / Mathf.Sqrt(na * nb);
            if(e < best) { best = e; reversed = r == 1; }
        }
        return best;
    }

    // 종결 피해 배율 (튕김총 1발 기준, 피해 숫자는 나중에 한꺼번에 정해요)
    public static float TierMul(int n) =>
        n >= 12 ? Tuning.Get("ult_const_mul_12", 20f) : n >= 7 ? Tuning.Get("ult_const_mul_7", 12f) : n >= 5 ? Tuning.Get("ult_const_mul_5", 10f) : Tuning.Get("ult_const_mul_3", 8f);
}
