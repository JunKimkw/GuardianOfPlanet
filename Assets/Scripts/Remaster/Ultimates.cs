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
        UltimateFx.Play(f, WeaponBase.ClampDamage(weapon.HitDamage * (double)AugmentDefs.UltScale(f)), () => ultBusy[f] = false);   // 종결 배율 (2026-11-25 사용자: 종결도 분류 점수대로 (A))
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
        curUlt = f;   // 보스 배율 (두 종결이 겹치면 나중 것 기준이에요)
        IEnumerator body;
        switch(f) {
            case AugmentDefs.FzGuided: body = CarpetRaid(dmg); break;
            case AugmentDefs.FzSatLaser: body = OrbitalStrike(dmg); break;
            case AugmentDefs.FzRailgun: body = Piercing(dmg); break;
            case AugmentDefs.FzPlague: body = PlagueRain(dmg); break;
            case AugmentDefs.FzSplitDbl: body = StarShower(dmg); break;
            case AugmentDefs.FzMicro: body = LockOnBarrage(dmg); break;
            case AugmentDefs.FzToxSat: body = ToxicVeil(dmg); break;
            case AugmentDefs.FzSingularity: body = EventHorizon(dmg); break;
            case AugmentDefs.FzGravBomb: body = GravityCollapse(dmg); break;
            case AugmentDefs.FzConstellation: body = FateConstellation(dmg); break;
            case AugmentDefs.FzBrand: body = Judgment(dmg); break;
            case AugmentDefs.FzImpact: body = AsteroidImpact(dmg); break;
            case AugmentDefs.FzStarlight:
            default: body = Polaris(dmg); break;
        }
        yield return StartCoroutine(body);
        done?.Invoke();
    }

    // ---- 공통
    // 종결 보스 배율 (2026-11-25 사용자: 종결도 분류 점수대로 (A)): 종결이 보스를 때릴 때만 곱해요 (처형은 일반 적만이라 빼요)
    static int curUlt = -1;
    static void UltHit(Enemy e, long dmg, bool crit, float critDamage, HitKind kind, float pen)
    {
        if(e != null && e.IsBoss && curUlt >= 0) dmg = WeaponBase.ClampDamage(dmg * (double)AugmentDefs.UltBossMul(curUlt));
        AugmentManager.WeaponHit(e, dmg, crit, critDamage, kind, pen);
    }

    static float HalfW => GameManager.instance.mainCamera.orthographicSize * GameManager.instance.mainCamera.aspect;
    static float HalfH => GameManager.instance.mainCamera.orthographicSize;
    static Vector3 Ship => WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
    static long Mul(long dmg, double m) => WeaponBase.ClampDamage(dmg * m);
    static float M(string key, float def) => Tuning.Get(key, def);

    static void Hit(Enemy e, long dmg)
    {
        bool crit = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        UltHit(e, dmg, crit, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
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

    //  (2026-11-24 사용자: 집중 포화도 상시 무기 + 종결은 가장 강한 적 하나에 폭탄 집중) 집중 포화 종결: 화면에서 가장 강한 적 하나(보스 먼저)에 표적을 찍고, 유도 폭탄 ult_focus_count 발이 사방으로 퍼졌다가
    //  모두 그 적에게 꽂혀요 (표적이 먼저 죽으면 다음으로 강한 적). 다 꽂히면 표적 자리에 거대 폭발 (× ult_focus_final_mul)
    IEnumerator CarpetRaid(long dmg)
    {
        Vector3 o0 = Ship;
        var focus = AugmentManager.StrongestInRange();
        if(focus == null) yield break;
        var targets = new List<Enemy> { focus };
        LockOnMark.Place(focus, 4f);
        RemasterFx.Ring(focus.transform.position, 1.6f, new Color(1f, 0.2f, 0.2f), 0.4f);
        int n = Mathf.Max(1, Mathf.RoundToInt(M("ult_focus_count", 30f)));
        float launch = M("ult_guided_launch", 0.6f);
        long hit = Mul(dmg, M("ult_focus_mul", 1.2f));
        Vector3 lastAt = focus.transform.position;
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
                if(!Alive(m.target)) m.target = Alive(focus) ? focus : (focus = AugmentManager.StrongestInRange());   // 표적이 죽으면 다음으로 강한 적
                if(m.target != null) lastAt = m.target.transform.position;
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
                    Impact(m.pos, 0.6f, hit, fire);   // 좁은 폭발 (집중 포화)
                    RemasterFx.Spark(m.pos, 0.6f, new Color(1f, 0.85f, 0.35f));
                    EndProjectile(m.root, m.head);
                    ms.RemoveAt(i);
                }
            }
            yield return null;
        }
        // 마무리: 표적 자리에 거대 폭발
        Vector3 end = Alive(focus) ? focus.transform.position : lastAt;
        end.z = 0f;
        yield return new WaitForSeconds(0.15f);
        RemasterFx.Ring(end, 2.4f, Color.white, 0.35f);
        Impact(end, 1.4f, Mul(dmg, M("ult_focus_final_mul", 6f)), new Color(1f, 0.35f, 0.15f));
        if(focus != null) LockOnMark.Release(focus);
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
    //  3) 360도 저격: 우주선 둘레로 빨간 조준선 16줄(ult_railgun_rays)이 촤라락 생겨 겨눈 뒤, 저격총처럼 한꺼번에 탕 (2026-11-21 사용자).
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

    // 360도 저격 (2026-11-21 사용자: "360도 빨간 줄이 나오고 그게 저격총처럼 쏴지게"):
    //  1) 조준: 우주선 둘레로 빨간 조준선 n 줄이 한 바퀴 돌며 촤라락 생기고 ult_railgun_aim 초 동안 겨눠요 (떨리며 점점 진해져요)
    //  2) 발사: 화면이 번쩍이며 n 줄이 한꺼번에 "탕" — 저격총처럼 금빛 탄도선 + 하얀 심이 순간 번쩍하고 사라져요. 총구 불꽃, 맞은 적마다 불꽃.
    //     선(굵기 width) 위의 적은 한 번씩 치명타, 보스는 저격총처럼 보스 추가 피해
    //  3) 여파: 모든 줄을 따라 폭발이 바깥으로 동시에 퍼져 나가요
    IEnumerator RadialRail(Vector3 o, int n, float width, long hit, long zipHit, HashSet<Enemy> pierced)
    {
        const float len = 30f;
        var red = new Color(1f, 0.2f, 0.25f);
        var gold = new Color(1f, 0.93f, 0.65f);
        float half = width * 0.5f + 0.25f;
        var dirs = new List<Vector3>();
        var aims = new List<LineRenderer>();
        float a0 = Random.Range(0f, 360f);
        for(int i = 0; i < n; i++) {
            float ang = (a0 + 360f * i / n) * Mathf.Deg2Rad;
            dirs.Add(new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f));
        }

        // 1) 조준선이 한 바퀴 돌며 생겨요 (0.25초) → 겨누기
        float aimT = M("ult_railgun_aim", 0.5f), appear = 0.25f, clock = 0f;
        for(float t = 0f; t < appear + aimT; t += Time.deltaTime) {
            clock += Time.deltaTime;
            Vector3 c = Ship;
            c.z = 0f;
            while(aims.Count < n && aims.Count <= t / appear * n) aims.Add(NewLine(new Color(red.r, red.g, red.b, 0.5f), 0.03f));
            float k = Mathf.Clamp01((t - appear) / Mathf.Max(0.01f, aimT));
            for(int i = 0; i < aims.Count; i++) {
                SetSeg(aims[i], c + dirs[i] * 0.3f, c + dirs[i] * len);
                float flick = 1f + 0.25f * Mathf.Sin(clock * 50f + i);
                aims[i].startWidth = aims[i].endWidth = Mathf.Lerp(0.025f, 0.05f, k) * flick;
                aims[i].startColor = aims[i].endColor = new Color(red.r, red.g, red.b, Mathf.Lerp(0.45f, 0.95f, k));
            }
            yield return null;
        }
        foreach(var l in aims) Destroy(l.gameObject);
        o = Ship;
        o.z = 0f;

        // 2) 탕 — 저격총처럼 한꺼번에
        StartCoroutine(Flash(new Color(1f, 0.95f, 0.85f), 0.5f, 0.25f));
        RemasterFx.Burst(o, 1.2f, Color.white);
        RemasterFx.Ring(o, 2f, gold, 0.25f);
        float beamW = M("ult_railgun_beam_width", 0.3f);
        foreach(var dir in dirs) {
            RemasterFx.Beam(o, o + dir * len, gold, beamW);
            RemasterFx.Beam(o, o + dir * len, new Color(1f, 1f, 1f, 0.95f), beamW * 0.35f);
            RemasterFx.Burst(o + dir * 0.5f, 0.3f, gold);   // 총구 불꽃
        }
        var once = new HashSet<Enemy>();
        foreach(var e in AugmentManager.EnemiesNear(o, len)) {
            Vector3 d = e.transform.position - o;
            d.z = 0f;
            foreach(var dir in dirs) {
                float along = Vector3.Dot(d, dir);
                if(along <= 0f || (d - dir * along).magnitude > half) continue;
                if(!once.Add(e)) break;
                pierced.Add(e);
                RemasterFx.Burst(e.transform.position, 0.5f, gold);
                UltHit(e, SniperBullet.BossBonus(e, hit), true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
                break;
            }
        }
        yield return new WaitForSeconds(0.15f);

        // 3) 여파: 모든 줄을 따라 폭발이 바깥으로 동시에 (화면 안까지만, 줄마다 ult_railgun_zip_step 칸 간격)
        Vector3 cam = Cam;
        float w = HalfW + 0.5f, h = HalfH + 0.5f;
        float step = Mathf.Max(0.5f, M("ult_railgun_zip_step", 1.4f));
        int rings = Mathf.CeilToInt(Mathf.Sqrt(w * w + h * h) * 2f / step);
        float ringGap = M("ult_railgun_zip_time", 0.8f) / Mathf.Max(1, rings);
        float zr = width * 0.55f, wait = 0f;
        var cyan = new Color(1f, 0.75f, 0.4f);
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
            wait += ringGap;
            if(wait >= Time.deltaTime) { yield return new WaitForSeconds(wait); wait = 0f; }
        }
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
            UltHit(e, SniperBullet.BossBonus(e, hit), true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);   // 레일건처럼 모두 치명타, 보스 추가 피해
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

    // 3 역병의 비 (2026-11-21 다시 만듦, 같은 날 다듬음: 사용자 "역병의 비를 다듬어줘")
    //  1) 먹구름: 화면 위쪽으로 어두운 초록 먹구름이 밀려오고 초록 번개가 번쩍, 화면이 초록빛으로 물들어요
    //  2) 비: 빗방울이 하늘에서 비스듬히 떨어져 땅에 닿으면 톡 튀어요. 보슬비 → 장대비로 점점 세져요 (ult_plague_drops 는 가장 셀 때 1초 수).
    //     빗방울에 맞은 적은 독, 비 오는 동안 모든 적 30% 느리게, 적이 몰린 곳에 독 장판이 차례로. 독에 걸린 채 죽은 적은 터지며 독을 옮겨요
    //  3) 역병 개화: 비가 그치면 화면에서 독에 걸린 적이 가운데부터 바깥으로 초록 꽃처럼 한꺼번에 터져요 (독 중첩이 많을수록 크게·세게, 옆 적에게 독)
    //  숫자 ult_plague_*
    class Drop { public LineRenderer lr; public Vector3 from, to; public float t, dur; }

    IEnumerator PlagueRain(long dmg)
    {
        float w = HalfW, h = HalfH;
        var green = new Color(0.5f, 0.95f, 0.35f);
        var pale = new Color(0.75f, 1f, 0.6f);
        float dur = M("ult_plague_time", 3.2f), rate = Mathf.Max(1f, M("ult_plague_drops", 40f));
        long drop = Mul(dmg, M("ult_plague_drop_mul", 0.4f)), burst = Mul(dmg, M("ult_plague_burst_mul", 2f));
        int puddles = Mathf.RoundToInt(M("ult_plague_puddles", 8f));
        var tint = Shape(new Color(0.25f, 0.6f, 0.15f, 0f), Vector2.one * Mathf.Max(w, h) * 3f, 505);
        var watch = new Dictionary<Enemy, Vector3>();   // 독에 걸린 적 → 마지막 자리 (죽으면 그 자리에서 터져요)

        // 1) 먹구름 (화면 위쪽 띠, 위에서 내려와요)
        var clouds = new List<(SpriteRenderer sr, float x, float y)>();
        const int cn = 9;
        for(int i = 0; i < cn; i++) {
            float x = Mathf.Lerp(-w - 0.5f, w + 0.5f, i / (cn - 1f)) + Random.Range(-0.4f, 0.4f);
            float y = h - Random.Range(0.1f, 1.1f);
            var sr = Shape(new Color(0.16f, 0.24f, 0.13f, 0f), Vector2.one * Random.Range(2.2f, 3.4f), 506 + i % 2);
            clouds.Add((sr, x, y));
        }
        var pool = new Stack<LineRenderer>();
        var drops = new List<Drop>();
        float t = 0f, nextDrop = 0.35f, nextSlow = 0f, nextPuddle = 0.5f, nextBolt = 0.15f;
        bool firstBolt = true;
        int made = 0;
        while(t < dur + 0.5f || drops.Count > 0) {
            float dt = Time.deltaTime;
            t += dt;
            Vector3 cam = Cam;
            tint.transform.position = cam;
            float fadeIn = Mathf.Clamp01(t / 0.5f), fadeOut = Mathf.Clamp01((dur + 0.5f - t) / 0.5f);
            tint.color = new Color(0.25f, 0.6f, 0.15f, 0.24f * fadeIn * fadeOut);
            for(int i = 0; i < clouds.Count; i++) {   // 먹구름: 위에서 내려와 천천히 흘러요
                var c = clouds[i];
                float drift = Mathf.Sin(t * 0.8f + i) * 0.25f;
                c.sr.transform.position = cam + new Vector3(c.x + drift, c.y + 1.5f * (1f - fadeIn), 0f);
                c.sr.color = new Color(0.16f, 0.24f, 0.13f, 0.6f * fadeIn * fadeOut);
            }
            if(t < dur && t >= nextBolt) {   // 초록 번개 (처음에 크게 한 번, 그다음 가끔 약하게)
                nextBolt = t + Random.Range(0.8f, 1.4f);
                StartCoroutine(Flash(pale, firstBolt ? 0.35f : 0.15f, 0.12f));
                firstBolt = false;
            }
            if(t < dur) {
                // 2) 빗방울: 보슬비 → 장대비 (처음 20% 세기에서 1초 동안 100% 로)
                float r = rate * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01((t - 0.35f) / 1f));
                while(nextDrop <= t) {
                    nextDrop += 1f / r;
                    var to = cam + new Vector3(Random.Range(-w, w), Random.Range(-h, h - 0.5f), 0f);
                    var lr = pool.Count > 0 ? pool.Pop() : NewLine(new Color(pale.r, pale.g, pale.b, 0.7f), 0.035f);
                    lr.enabled = true;
                    drops.Add(new Drop { lr = lr, from = to + new Vector3(0.6f, 2.4f, 0f), to = to, dur = Random.Range(0.16f, 0.24f) });
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
            for(int i = drops.Count - 1; i >= 0; i--) {   // 떨어지는 빗방울 (짧은 줄이 내려가다 땅에 닿으면 톡)
                var d = drops[i];
                d.t += dt;
                float k = Mathf.Clamp01(d.t / d.dur);
                Vector3 head = Vector3.Lerp(d.from, d.to, k);
                SetSeg(d.lr, head + (d.from - d.to).normalized * 0.45f, head);
                if(k < 1f) continue;
                d.lr.enabled = false;
                pool.Push(d.lr);
                drops.RemoveAt(i);
                RemasterFx.Burst(d.to, 0.2f, green);
                foreach(var e in AugmentManager.EnemiesNear(d.to, 0.6f)) {
                    e.AddPoison(2, 5f);
                    Hit(e, drop);
                    watch[e] = e.transform.position;
                }
            }
            foreach(var e in new List<Enemy>(watch.Keys)) {   // 독에 걸린 채 죽은 적은 터지며 옮겨요
                if(Alive(e)) { watch[e] = e.transform.position; continue; }
                Vector3 p = watch[e];
                watch.Remove(e);
                RemasterFx.Burst(p, 1.2f, green);
                RemasterFx.Ring(p, 1.4f, pale, 0.25f);
                foreach(var n2 in AugmentManager.EnemiesNear(p, 1.2f)) {
                    n2.AddPoison(3, 5f);
                    Hit(n2, burst);
                    watch[n2] = n2.transform.position;
                }
            }
            yield return null;
        }
        foreach(var lr in pool) if(lr != null) Destroy(lr.gameObject);
        foreach(var c in clouds) Destroy(c.sr.gameObject);
        Destroy(tint.gameObject);

        // 3) 역병 개화: 독에 걸린 화면의 적이 가운데부터 바깥으로 한꺼번에 터져요 (0.4초 동안 물결처럼)
        Vector3 cc = Cam;
        var bloom = AugmentManager.EnemiesNear(cc, 999f);
        bloom.RemoveAll(e => !e.IsInRange || !e.IsPoisoned);
        if(bloom.Count == 0) yield break;
        bloom.Sort((a, b) => (a.transform.position - cc).sqrMagnitude.CompareTo((b.transform.position - cc).sqrMagnitude));
        StartCoroutine(Flash(green, 0.3f, 0.3f));
        float bmul = M("ult_plague_bloom_mul", 1.5f), per = M("ult_plague_bloom_per_stack", 0.05f);
        float gap = 0.4f / bloom.Count, wait = 0f;
        foreach(var e in bloom) {
            if(Alive(e)) {
                int st = e.PoisonStacks;
                Vector3 p = e.transform.position;
                p.z = 0f;
                float size = 0.6f + 0.03f * st;
                RemasterFx.Burst(p, size, green);
                RemasterFx.Burst(p, size * 0.4f, Color.white);
                for(int k = 0; k < 5; k++) {   // 꽃잎
                    float a = (k * 72f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    RemasterFx.Line(p + d * size * 0.3f, p + d * size * 1.2f, pale);
                }
                Hit(e, Mul(dmg, bmul * (1f + per * st)));
                foreach(var n2 in AugmentManager.EnemiesNear(p, size)) if(n2 != e) n2.AddPoison(2, 5f);
            }
            wait += gap;
            if(wait >= Time.deltaTime) { yield return new WaitForSeconds(wait); wait = 0f; }
        }
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

    // 5 전탄 발사 (2026-11-21 사용자 결정, 마이크로 미사일(유도탄 + 쌍발총) 종결. 예전 "사냥개 무리" 자리)
    //  유도탄과 똑같은 미사일·록온 표식으로 (사용자: "유도탄 나가는 거랑 똑같이, 발사량은 훨씬 많이, 대신 피해는 줄여서")
    //  1) 화면의 모든 적(최대 ult_lockon_max)에게 가까운 순으로 록온 표식이 촤라락 찍혀요 (0.6초)
    //  2) 유도탄이 적마다 ult_lockon_per 발씩 일제 발사 — 넓은 부채꼴로 퍼졌다가 점화해 휘어 꽂혀요 (한 바퀴마다 ult_lockon_gap 초)
    //  1발 피해 = 유도탄 1발 × ult_lockon_mul. 숫자 ult_lockon_*
    IEnumerator LockOnBarrage(long dmg)
    {
        var gun = TrackingGun.instance;
        Vector3 o = Ship;
        o.z = 0f;
        var targets = AugmentManager.NearestInRange(o, Mathf.Max(1, Mathf.RoundToInt(M("ult_lockon_max", 40f))));
        if(targets.Count == 0 || gun == null) yield break;

        // 1) 록온
        float gap = 0.6f / targets.Count, wait = 0f;
        foreach(var e in targets) {
            LockOnMark.Place(e, 8f);
            wait += gap;
            if(wait >= Time.deltaTime) { yield return new WaitForSeconds(wait); wait = 0f; }
        }
        yield return new WaitForSeconds(0.15f);

        // 2) 일제 발사 (유도탄 미사일 그대로, 훨씬 많이)
        int per = Mathf.Max(1, Mathf.RoundToInt(M("ult_lockon_per", 3f)));
        long hit = Mul(dmg, M("ult_lockon_mul", 0.6f));
        yield return gun.StartCoroutine(gun.Salvo(targets, per, hit, 1f, 1f, 1f, M("ult_lockon_gap", 0.12f), 25f, M("ult_lockon_spread", 200f), true));   // 다른 적은 지나가요
        yield return new WaitForSeconds(1.2f);   // 미사일이 다 꽂힐 때까지
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
        // 걸린 적의 마지막 자리 (2026-11-21 사용자: "가끔 눌러도 반응이 없어" — 예전엔 걸린 적이 죽으면 거기서 안 번져서,
        // 처음 문 적이 바로 죽으면 줄 하나만 보이고 끝났어요. 이제 죽은 자리에서도 계속 번지고 줄도 그 자리에 남아요)
        var lastPos = new Dictionary<Enemy, Vector3>();
        System.Func<Enemy, Vector3> posOf = en => {
            if(en == null) return o;
            if(Alive(en)) { var q = en.transform.position; q.z = 0f; lastPos[en] = q; return q; }
            return lastPos.TryGetValue(en, out var lp) ? lp : o;
        };
        var web = new List<(LineRenderer lr, Enemy from, Enemy to)>();   // from 이 없으면 우주선에서
        var frontier = new List<Enemy>();
        var pending = new List<(LineRenderer lr, Enemy from, Enemy to)> { (WebLine(green), null, start[0]) };
        infected.Add(start[0]);
        posOf(start[0]);
        float t = 0f;
        while(pending.Count > 0) {
            // 줄이 뻗어 나가요
            for(float k = 0f; k < hop; k += Time.deltaTime) {
                t += Time.deltaTime;
                foreach(var p in pending) {
                    Vector3 a = posOf(p.from), b = posOf(p.to);
                    SetSeg(p.lr, a, Vector3.Lerp(a, b, k / hop));
                }
                UpdateWeb(web, posOf);
                yield return null;
            }
            frontier.Clear();
            foreach(var p in pending) {
                web.Add(p);
                frontier.Add(p.to);   // 죽었어도 그 자리에서 번져요
                if(!Alive(p.to)) continue;
                p.to.AddPoison(5, 6f);
                p.to.AddSlow(0.3f, 3f);
                RemasterFx.Burst(p.to.transform.position, 0.45f, green);
                Hit(p.to, infect);
            }
            UpdateWeb(web, posOf);
            pending.Clear();
            if(t >= limit) break;
            // 다음으로 옮을 적
            foreach(var src in frontier) {
                Vector3 sp = posOf(src);
                foreach(var e in AugmentManager.NearestInRange(sp, fan, infected)) {
                    if((e.transform.position - sp).magnitude > reach) break;
                    infected.Add(e);
                    posOf(e);
                    pending.Add((WebLine(green), src, e));
                }
            }
            if(pending.Count == 0) {   // 근처에 없으면: 걸린 적 중 가장 가까운 쪽에서 안 걸린 적에게 길게
                Enemy bestE = null, bestFrom = null;
                float bd = float.MaxValue;
                foreach(var e in AugmentManager.NearestInRange(o, 999, infected)) {
                    foreach(var s in infected) {
                        float d = (e.transform.position - posOf(s)).sqrMagnitude;
                        if(d < bd) { bd = d; bestE = e; bestFrom = s; }
                    }
                }
                if(bestE == null) break;
                infected.Add(bestE);
                posOf(bestE);
                pending.Add((WebLine(green), bestFrom, bestE));
            }
        }
        // 그물이 두 번 맥동
        for(int pz = 0; pz < 2; pz++) {
            for(float k = 0f; k < 0.25f; k += Time.deltaTime) {
                float wv = Mathf.Lerp(0.2f, 0.05f, k / 0.25f);
                foreach(var p in web) p.lr.startWidth = p.lr.endWidth = wv;
                UpdateWeb(web, posOf);
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
            UpdateWeb(web, posOf);
            yield return null;
        }
        foreach(var p in web) Destroy(p.lr.gameObject);
    }

    static LineRenderer WebLine(Color c) => NewLine(new Color(c.r, c.g, c.b, 0.75f), 0.05f);

    // 그물 줄이 적을 따라 움직여요 (죽은 적 쪽 끝은 그 적이 죽은 자리에 남아요, from 이 없으면 우주선)
    static void UpdateWeb(List<(LineRenderer lr, Enemy from, Enemy to)> web, System.Func<Enemy, Vector3> posOf)
    {
        foreach(var p in web) {
            if(p.lr == null) continue;
            SetSeg(p.lr, posOf(p.from), posOf(p.to));
        }
    }


    // 7 북극성 (2026-11-21 사용자 결정, 별빛 섬광(360 레이저 + 운석) 종결. 예전 "무지개 심판" 자리: 무지개가 유치하다고 해서)
    //  1) 배경이 밤하늘처럼 어두워지고(적·우주선은 그대로) 작은 별들이 반짝이며, 화면 위쪽 가운데(북쪽)에 거대한 별이 떠올라요
    //  2) 북극성이 ult_polaris_twinkles(3)번 반짝여요. 반짝일 때마다 화면을 가로지르는 빛살이 번쩍이고
    //     (가로·세로 → 대각선 → 마지막은 8갈래. 방향만 바뀌고 돌지 않아요), 화면의 모든 적 위에서 별빛이 함께 터져요 (적마다 1번씩 피해)
    //  3) 마지막 반짝임은 8갈래로 크게 터지며 화면이 하얗게 번쩍이고, 모든 적에게 큰 피해
    //  숫자 ult_polaris_* (피해 숫자는 나중에 한꺼번에)
    IEnumerator Polaris(long dmg)
    {
        float w = HalfW, h = HalfH;
        Vector3 cam = Cam;
        float reach = Mathf.Sqrt(w * w + h * h) * 2.2f;   // 위쪽에서 뻗어도 화면 아래 끝까지 닿게
        int twinkles = Mathf.Clamp(Mathf.RoundToInt(M("ult_polaris_twinkles", 3f)), 1, 8);
        float gap = M("ult_polaris_gap", 0.5f), dim = M("ult_polaris_dim", 0.55f), size = M("ult_polaris_size", 1.6f);
        long hit = Mul(dmg, M("ult_polaris_hit_mul", 0.6f)), finalHit = Mul(dmg, M("ult_polaris_final_mul", 2f));
        var nightCol = new Color(0.02f, 0.03f, 0.1f);

        // 2026-11-22 사용자: "애니메이션 시작할 때부터 적들 멈춰" → 시작부터 마지막 연타가 끝날 때까지 모든 적이 멈춰요 (보스도)
        float freeze = M("ult_polaris_rise", 0.6f) + (twinkles - 1) * gap + M("ult_polaris_charge", 0.7f) + M("ult_polaris_hold", 0.12f)
                       + M("ult_polaris_burst_hits", 8f) * M("ult_polaris_burst_gap", 0.06f) + 0.3f;
        foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) e.Stun(freeze);

        // 1) 밤하늘 + 북극성 떠오르기. 어둠은 배경 층(Map)에만 깔아서 적·우주선은 그대로 보여요
        var night = Shape(new Color(nightCol.r, nightCol.g, nightCol.b, 0f), Vector2.one * Mathf.Max(w, h) * 3f, 100);
        night.sortingLayerName = "Map";
        night.transform.position = cam;
        var dots = new List<SpriteRenderer>();
        for(int i = 0; i < 26; i++) {
            var d = Sprite2(StarArt.Star, new Color(1f, 1f, 1f, 0f), Random.Range(0.12f, 0.3f), 101);
            d.sortingLayerName = "Map";
            d.transform.position = cam + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0f);
            dots.Add(d);
        }
        Vector3 top = cam + new Vector3(0f, h * M("ult_polaris_height", 0.62f), 0f);
        var halo = Shape(new Color(StarFlash.Glow.r, StarFlash.Glow.g, StarFlash.Glow.b, 0f), Vector2.one, 538);
        var star = Sprite2(StarArt.Star, Color.white, 0.1f, 540);
        halo.transform.position = star.transform.position = top;
        float rise = M("ult_polaris_rise", 0.6f);
        for(float t = 0f; t < rise; t += Time.deltaTime) {
            float k = t / rise, e = 1f - (1f - k) * (1f - k);
            night.color = new Color(nightCol.r, nightCol.g, nightCol.b, dim * k);
            star.transform.localScale = Vector3.one * size * Mathf.Lerp(0.1f, 1f, e);
            halo.transform.localScale = Vector3.one * size * 1.4f * e;
            halo.color = new Color(StarFlash.Glow.r, StarFlash.Glow.g, StarFlash.Glow.b, 0.35f * k);
            TwinkleDots(dots, k);
            yield return null;
        }

        // 2) 반짝임
        for(int i = 0; i < twinkles; i++) {
            bool last = i == twinkles - 1;
            if(last) {
                // 마지막 한 방 전 모으기 (2026-11-22 사용자: "한방이 없어 보여, 임팩트가"):
                // 밤하늘이 더 어두워지고, 하늘의 작은 별들과 빛 조각이 북극성으로 빨려 들어가며 북극성이 작고 뜨겁게 졸아들어요 → 잠깐 숨 멈춤 → 폭발
                float charge = M("ult_polaris_charge", 0.7f);
                var starts = new List<Vector3>();
                foreach(var d in dots) starts.Add(d.transform.position);
                for(float t = 0f; t < charge; t += Time.deltaTime) {
                    float k = t / charge, e = k * k;
                    night.color = new Color(nightCol.r, nightCol.g, nightCol.b, Mathf.Lerp(dim, Mathf.Min(0.9f, dim * 1.6f), k));
                    for(int j = 0; j < dots.Count; j++) {
                        dots[j].transform.position = Vector3.Lerp(starts[j], top, e);
                        dots[j].color = new Color(1f, 1f, 1f, 0.9f * (1f - 0.5f * k));
                    }
                    if(Random.value < 0.6f) {   // 사방에서 빛 조각이 빨려 들어와요
                        Vector3 dir = Random.insideUnitCircle.normalized;
                        RemasterFx.Line(top + dir * Random.Range(1.5f, 4f), top + dir * 0.4f, StarFlash.Glow);
                    }
                    star.transform.localScale = Vector3.one * size * Mathf.Lerp(1f, 0.45f, e) * (1f + 0.15f * Mathf.Sin(t * 60f));
                    halo.transform.localScale = Vector3.one * size * Mathf.Lerp(1.4f, 3.5f, e);
                    halo.color = new Color(StarFlash.Glow.r, StarFlash.Glow.g, StarFlash.Glow.b, Mathf.Lerp(0.35f, 0.8f, k));
                    yield return null;
                }
                foreach(var d in dots) Destroy(d.gameObject);
                dots.Clear();
                yield return new WaitForSeconds(M("ult_polaris_hold", 0.12f));   // 숨 멈춤
            }
            float ang = last || i % 2 == 0 ? 0f : 45f;
            float thick = M("ult_polaris_ray_width", 0.9f) * (last ? M("ult_polaris_final_width", 2.2f) : 1f);
            StarFlash.Spawn(top, reach, thick, last ? reach : 0f, last ? 0.9f : 0.45f, ang, size * (last ? 4.5f : 2.2f), 545);
            RemasterFx.Ring(top, size * (last ? 2.5f : 1.4f), StarFlash.Glow, 0.3f);
            if(last) {   // 폭발: 화면을 덮는 충격파 고리 + 화면 흔들림 + 강한 번쩍
                RemasterFx.Burst(top, size * 3f, Color.white);
                RemasterFx.Ring(top, reach * 0.55f, Color.white, 0.6f);
                RemasterFx.Ring(top, reach * 0.4f, StarFlash.Gold, 0.5f);
                RemasterFx.Ring(top, reach * 0.28f, StarFlash.Glow, 0.4f);
                var camT = GameManager.instance.mainCamera.transform;
                camT.DOComplete();
                camT.DOShakePosition(M("ult_polaris_shake_time", 0.5f), M("ult_polaris_shake", 0.4f), 25, 90f, false, true);
                night.color = new Color(nightCol.r, nightCol.g, nightCol.b, dim * 0.5f);   // 빛에 밤하늘이 걷혀요
            }
            var targets = last ? AugmentManager.EnemiesNear(Vector3.zero, 999f) : AugmentManager.NearestInRange(cam, 999);
            var pts = new List<Vector3>();
            foreach(var e in targets) {
                Vector3 p = e.transform.position;
                p.z = 0f;
                pts.Add(p);
                if(last) RemasterFx.Burst(p, 0.6f, StarFlash.Gold);
                else Hit(e, hit);
            }
            if(last) StartCoroutine(PolarisBarrage(targets, finalHit));   // 마지막: 피해를 빠빠빠빠박 연타로
            StartCoroutine(EnemyTwinkles(pts, last ? 1.4f : 0.75f, last, last ? 0.6f : 0.35f));
            if(last) StartCoroutine(Flash(Color.white, M("ult_polaris_flash", 0.85f), 0.6f));
            for(float t = 0f; t < gap; t += Time.deltaTime) {   // 북극성이 번쩍 커졌다가 돌아와요
                float k = 1f - t / gap;
                star.transform.localScale = Vector3.one * size * (1f + (last ? 1.6f : 0.45f) * k * k);
                if(last) halo.transform.localScale = Vector3.one * size * Mathf.Lerp(1.4f, 3.5f, k);
                TwinkleDots(dots, 1f);
                yield return null;
            }
        }

        // 3) 밤하늘 걷히기
        float fade = 0.45f, nightA = night.color.a;   // 마지막 폭발에 이미 반쯤 걷힌 밤하늘에서 이어서
        for(float t = 0f; t < fade; t += Time.deltaTime) {
            float k = 1f - t / fade;
            night.color = new Color(nightCol.r, nightCol.g, nightCol.b, nightA * k);
            star.color = new Color(1f, 1f, 1f, k);
            star.transform.localScale = Vector3.one * size * k;
            halo.color = new Color(StarFlash.Glow.r, StarFlash.Glow.g, StarFlash.Glow.b, 0.35f * k);
            TwinkleDots(dots, k);
            yield return null;
        }
        Destroy(night.gameObject);
        Destroy(star.gameObject);
        Destroy(halo.gameObject);
        foreach(var d in dots) Destroy(d.gameObject);
    }

    // 북극성 마지막 연타 (2026-11-22 사용자: "딱 터질 때 데미지가 한 번에 빠빠빠빠박"):
    //  모든 적에게 ult_polaris_burst_hits 번을 ult_polaris_burst_gap 초 간격으로 몰아쳐요. 합치면 마지막 피해(ult_polaris_final_mul)와 같아요
    IEnumerator PolarisBarrage(List<Enemy> targets, long total)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(M("ult_polaris_burst_hits", 8f)), 1, 30);
        float gap = M("ult_polaris_burst_gap", 0.06f);
        long each = Mul(total, 1.0 / n);
        for(int i = 0; i < n; i++) {
            var col = i % 2 == 0 ? Color.white : StarFlash.Gold;
            foreach(var e in targets) {
                if(!Alive(e)) continue;
                Vector3 p = e.transform.position;
                p.z = 0f;
                RemasterFx.Burst(p + (Vector3)(Random.insideUnitCircle * 0.2f), 0.3f, col);
                Hit(e, each);
            }
            if(gap > 0f) yield return new WaitForSeconds(gap);
        }
    }

    // 밤하늘 작은 별들이 제각각 깜빡여요 (k: 전체 밝기)
    static void TwinkleDots(List<SpriteRenderer> dots, float k)
    {
        float t = Time.time;
        for(int i = 0; i < dots.Count; i++)
            dots[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * (0.45f + 0.4f * Mathf.Sin(t * (3f + i % 5) + i * 1.7f))));
    }

    // 적 위 별빛: 반짝이 그림이 톡 커졌다 사라져요. eight 면 45도 기울인 금색 반짝이를 겹쳐 8갈래로
    IEnumerator EnemyTwinkles(List<Vector3> pts, float size, bool eight, float dur)
    {
        var main = new List<SpriteRenderer>();
        var diag = new List<SpriteRenderer>();
        foreach(var p in pts) {
            var a = Sprite2(StarArt.Star, Color.white, 0.01f, 537);
            a.transform.position = p;
            main.Add(a);
            if(!eight) continue;
            var b = Sprite2(StarArt.Star, StarFlash.Gold, 0.01f, 536);
            b.transform.position = p;
            b.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            diag.Add(b);
        }
        for(float t = 0f; t < dur; t += Time.deltaTime) {
            float k = t / dur;
            float s = size * Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) * (1f - 0.3f * k);
            float a = 1f - k * k;
            foreach(var sr in main) { sr.transform.localScale = Vector3.one * s; sr.color = new Color(1f, 1f, 1f, a); }
            foreach(var sr in diag) { sr.transform.localScale = Vector3.one * s * 0.75f; sr.color = new Color(StarFlash.Gold.r, StarFlash.Gold.g, StarFlash.Gold.b, a); }
            yield return null;
        }
        foreach(var sr in main) Destroy(sr.gameObject);
        foreach(var sr in diag) Destroy(sr.gameObject);
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
        var shot = AugmentManager.EnemiesNear(c, R);
        foreach(var e in shot)
            UltHit(e, hit, true, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.UltPen);
        Destroy(hole);
        // 2026-11-22 사용자: 저격에 맞은 적이 우주선 반대쪽으로 부채꼴로 퍼지며 ult_singularity_push 칸 밀려요 (특이점 저격 무기와 같은 방식, 무기는 2칸)
        //  밀어내는 중심을 구멍보다 singularity_fan_back 칸 뒤(우주선 쪽)에 둬요. 우주선 쪽으로 가는 적은 반대로 꺾어요
        shot.RemoveAll(e => !Alive(e));
        if(shot.Count > 0) new GameObject("UltSingularityPush").AddComponent<MeteorScatter>().Run(c - sdir * M("singularity_fan_back", 0.5f), shot, M("ult_singularity_push", 2.5f), 0.3f, -sdir, 0.3f);
        // 2026-11-22 사용자: 블랙홀로 끌려가던 적이 블랙홀이 사라질 때 우주선 근처에 멈추면 안 돼요 (소행성 충돌과 같은 조치)
        //  우주선에서 ult_singularity_ship_safe 칸 안의 적은 우주선 반대쪽으로 그만큼 밀어내고, 0.8초 동안 우주선에 닿아도 피해 없음
        Vector3 ship = Ship;
        ship.z = 0f;
        float safe = M("ult_singularity_ship_safe", 2.5f);
        var nearShip = AugmentManager.EnemiesNear(ship, safe);
        if(nearShip.Count > 0) {
            foreach(var e in nearShip) e.gravityUntil = Time.time + 0.8f;
            new GameObject("UltShipPush").AddComponent<MeteorScatter>().Run(ship, nearShip, safe, 0.35f);
        }
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
        // 2026-11-24 사용자: 종결은 모였다 터질 때 중력 폭탄 예전만큼(ult_gravbomb_push 칸) 우주선 반대쪽으로 부채꼴로 밀려요
        Vector3 pdir = c - Ship;
        pdir.z = 0f;
        pdir = pdir.sqrMagnitude < 0.0001f ? Vector3.up : pdir.normalized;
        var pushed = AugmentManager.EnemiesNear(c, w * 0.8f).FindAll(e => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy);
        if(pushed.Count > 0)
            new GameObject("UltGravPush").AddComponent<MeteorScatter>().Run(c - pdir * Tuning.Get("singularity_fan_back", 0.5f), pushed, M("ult_gravbomb_push", 2f), 0.25f, -pdir, 0.3f);
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
        // 떨어질 자리: 항상 우주선 바로 위(북쪽) ult_impact_dist 칸 (2026-11-21 사용자: 적이 몰린 곳을 따라가면 너무 멀거나 가까워서). 화면 안으로 맞춰요
        Vector3 ship0 = Ship;
        ship0.z = 0f;
        Vector3 hitAt = ship0 + Vector3.up * M("ult_impact_dist", 4.5f);
        hitAt.x = Mathf.Clamp(hitAt.x, cam.x - HalfW + 1f, cam.x + HalfW - 1f);
        hitAt.y = Mathf.Clamp(hitAt.y, cam.y - HalfH + 1.5f, cam.y + HalfH - 1.5f);
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
        // 충돌이 모인 적을 멀리 흩어 놓아요. 우주선 쪽으로는 안 보내요 (2026-11-21 사용자: 다 모아 둔 뒤 대충돌 블랙홀이 그 뭉친 무리를 계속 잡았어요)
        {
            Vector3 ship = Ship;
            ship.z = 0f;
            Vector3 toShip = ship - hitAt;
            toShip.z = 0f;
            var scatterList = AugmentManager.EnemiesNear(hitAt, R * M("ult_impact_scatter_radius", 1f));
            if(scatterList.Count > 0)
                new GameObject("UltScatter").AddComponent<MeteorScatter>().Run(hitAt, scatterList, M("ult_impact_scatter", 3.5f), 0.45f,
                    toShip.sqrMagnitude > 0.0001f ? toShip.normalized : (Vector3?)null, 0.4f);
        }
        // 끌려오다 우주선 옆에서 멈춘 적은 우주선 반대쪽으로 밀어내요. 밀려나는 동안은 우주선에 닿아도 피해 없음
        // (2026-11-21 사용자: 블랙홀에 끌려오다 소행성이 떨어지며 끊긴 자리가 우주선 앞이면 바로 피해가 들어가요)
        {
            Vector3 ship = Ship;
            ship.z = 0f;
            float safe = M("ult_impact_ship_safe", 2.5f);
            var nearShip = AugmentManager.EnemiesNear(ship, safe);
            if(nearShip.Count > 0) {
                foreach(var e in nearShip) e.gravityUntil = Time.time + 0.8f;
                new GameObject("UltShipPush").AddComponent<MeteorScatter>().Run(ship, nearShip, safe, 0.35f);
            }
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
            UltHit(e, hit, crit, Player.instance.FinalCriticalDamage, HitKind.Weapon, pen);
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
