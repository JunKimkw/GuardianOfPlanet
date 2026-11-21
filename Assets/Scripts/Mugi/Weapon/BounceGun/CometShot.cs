using System.Collections.Generic;
using UnityEngine;

// 혜성탄 (튕김총 진화, 2026-11-20 사용자 결정: "꼬리로 쓸기")
// - 튕김총이 쏠 때마다 혜성 1발이 갈라지지 않고, 아직 안 맞힌 가장 가까운 적으로 여러 번 튕겨요 (comet_bounces, 기본 6).
// - 혜성 뒤에 빛나는 꼬리가 따라오고, 꼬리에 닿은 적도 피해를 받아요 (적마다 comet_tail_cd 초에 한 번). 튕길수록 꼬리가 길어져요.
// - 꼬리는 혜성을 따라다니다 같이 사라져서 화면에 선이 남지 않아요 (별자리 선을 남기는 방식은 너무 어지러워서 종결로 옮겼어요).
// - 합체 전용 증강: 긴 꼬리(꼬리 길이·피해 +), 궤도 연장(튕김 +), 혜성 충돌(R, 마지막 튕김 뒤 큰 폭발)
// - 튕김총 패시브·증강의 "탄 추가"는 혜성을 더 쏘지 않고 튕김 횟수로 바꿔요 (혜성이 여러 개면 화면이 어지러워요)
// 숫자는 tuning.txt comet_* (시뮬: 작업 폴더 tools/weapon_dps_sim2.js --comet)
public class CometShot : MonoBehaviour
{
    static Material mat;   // 혜성마다 새 재질을 만들지 않게 같이 써요

    long hitDamage;        // 쏠 때 정한 1발 피해 (튕김총 HitDamage, 합체 위력 포함)
    int maxHits, hits;
    float speed, hopTime, fade = -1f;
    Enemy target;
    Vector3 pos, lastHitPos;
    readonly HashSet<Enemy> hitSet = new HashSet<Enemy>();
    readonly Dictionary<Enemy, float> tailCd = new Dictionary<Enemy, float>();
    readonly List<Vector3> trail = new List<Vector3>();   // 0번이 머리, 뒤로 갈수록 꼬리 끝
    SpriteRenderer head;
    LineRenderer tail;

    static readonly Color HeadColor = new Color(0.85f, 0.95f, 1f);
    static readonly Color ImpactColor = new Color(0.65f, 0.88f, 1f);

    public static void Fire(BounceGun gun, Vector3 from)
    {
        from.z = 0f;
        var first = Nearest(from, null);
        if(first == null) return;
        new GameObject("CometShot").AddComponent<CometShot>().Init(gun, from, first);
    }

    void Init(BounceGun gun, Vector3 from, Enemy first)
    {
        hitDamage = gun.HitDamage;
        maxHits = MaxHits(gun);
        PickPlan();
        speed = Tuning.Get("comet_speed", 10f);
        pos = from;
        target = first;
        trail.Add(pos);

        head = new GameObject("CometHead").AddComponent<SpriteRenderer>();
        head.transform.SetParent(transform, false);
        head.sprite = StarArt.Star;
        head.color = HeadColor;
        head.sortingLayerName = RemasterFx.FxLayer;
        head.sortingOrder = 502;
        head.transform.localScale = Vector3.one * Tuning.Get("comet_head_size", 0.55f);
        head.transform.position = pos;

        // 꼬리: 머리 쪽이 굵고 밝고, 끝으로 갈수록 가늘고 투명해요
        tail = gameObject.AddComponent<LineRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        tail.sharedMaterial = mat;
        tail.useWorldSpace = true;
        tail.sortingLayerName = RemasterFx.FxLayer;
        tail.sortingOrder = 500;
        tail.numCapVertices = 4;
        tail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tail.widthMultiplier = TailWidth;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.35f, 0.55f, 1f), 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tail.colorGradient = g;
        tail.positionCount = 1;
        tail.SetPosition(0, pos);
    }

    static float TailWidth => Tuning.Get("comet_tail_width", 0.35f);

    // 튕김 수: 기본 + 튕김총 패시브(튕김 +1, 탄 추가 → +2) + 탄 추가 증강(+1씩) + 궤도 연장
    static int MaxHits(BounceGun gun)
    {
        int n = Tuning.GetInt("comet_bounces", 6);
        if(gun.ExtraBounce) n += 1;
        if(gun.ExtraBullet) n += 2;
        n += AugmentManager.ExtraShots();
        int L = AugmentManager.WL("cm_bounce");
        if(L > 0) n += AugmentDefs.CmBounce(L);
        return Mathf.Max(1, n);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if(fade >= 0f) { Fade(dt); return; }
        head.transform.Rotate(0f, 0f, 540f * dt);

        if(!Alive(target)) {
            target = NextTarget();
            if(target == null) { Finish(); return; }
        }
        Vector3 to = target.transform.position;
        to.z = 0f;
        Vector3 d = to - pos;
        float step = speed * dt;
        hopTime += dt;
        if(d.magnitude <= Mathf.Max(step, 0.15f)) {
            pos = to;
            HitHead(target);
            hopTime = 0f;
            target = hits >= maxHits ? null : NextTarget();
        } else {
            pos += d.normalized * step;
        }
        UpdateTail();
        TailDamage();
        if(target == null || hopTime > 2.5f) Finish();   // 다 튕겼거나 다음 적이 없거나, 너무 오래 쫓으면 끝
    }

    void HitHead(Enemy e)
    {
        hitSet.Add(e);
        hits++;
        lastHitPos = pos;
        speed *= 1f + Tuning.Get("comet_speed_up", 0.08f);   // 튕길수록 빨라져요
        Deal(e, Tuning.Get("comet_hit_mul", 0.4f));
        RemasterFx.Burst(pos, 0.3f, HeadColor);
        if(planK > 0 && planPts.Count < planK) {
            planPts.Add(pos);
            if(planPts.Count == planK) FinishPlan();
        }
    }

    // ---- 별자리 그리기 (2026-11-20 사용자 요청: 혜성탄이 임팩트가 작아서)
    // 쏠 때 별자리 하나(별 3·5·7개, 가중치 comet_plan_w*, 튕김 수보다 큰 건 빼요)를 정하고,
    // 다음 적을 고를 때 "지금까지 맞힌 자리로 맞춘 별자리에서 다음 별이 있어야 할 곳"에 가장 가까운 적을 골라요 (처음 2개는 가장 가까운 적).
    // 다 맞힌 뒤 모양이 충분히 비슷하면(comet_match_*) 종결 "운명의 별자리"와 같은 연출로 그 자리에 별자리가 그려지고,
    // 별·선 아래 적에게 피해 (comet_const_mul_*). 12개(황도 12궁)는 종결에서만 (사용자 결정). 별자리를 다 그린 뒤엔 가까운 적으로 튕겨요.
    int planK;
    Vector2[] planStars;
    readonly List<Vector3> planPts = new List<Vector3>();

    void PickPlan()
    {
        float w3 = Tuning.Get("comet_plan_w3", 50f), w5 = maxHits >= 5 ? Tuning.Get("comet_plan_w5", 35f) : 0f, w7 = maxHits >= 7 ? Tuning.Get("comet_plan_w7", 15f) : 0f;
        if(maxHits < 3 || w3 + w5 + w7 <= 0f) return;
        float x = Random.value * (w3 + w5 + w7);
        planK = x < w3 ? 3 : x < w3 + w5 ? 5 : 7;
        planStars = StarArt.Shape(planK).stars;
    }

    // 다음 적: 별자리를 그리는 중이면 다음 별 자리에 가장 가까운 적, 아니면 가장 가까운 적
    Enemy NextTarget()
    {
        if(planK == 0 || planPts.Count < 2 || planPts.Count >= planK) return Nearest(pos, hitSet);
        Vector2 want0 = Predict(false), want1 = Predict(true);
        Enemy best = null;
        float bd = float.MaxValue;
        foreach(var e in AugmentManager.NearestInRange(pos, 999, hitSet)) {
            Vector2 p = e.transform.position;
            float d = Mathf.Min((p - want0).sqrMagnitude, (p - want1).sqrMagnitude);
            if(d < bd) { bd = d; best = e; }
        }
        return best;
    }

    // 지금까지 맞힌 자리에 별자리를 맞춰서(위치·크기·회전, mirror 면 좌우 뒤집기) 다음 별이 있어야 할 곳
    Vector2 Predict(bool mirror)
    {
        int c = planPts.Count;
        Vector2 ca = Vector2.zero, cb = Vector2.zero;
        for(int i = 0; i < c; i++) { ca += Star(i, mirror); cb += (Vector2)planPts[i]; }
        ca /= c; cb /= c;
        float re = 0f, im = 0f, na = 0f;
        for(int i = 0; i < c; i++) {
            Vector2 a = Star(i, mirror) - ca, b = (Vector2)planPts[i] - cb;
            re += a.x * b.x + a.y * b.y;
            im += a.x * b.y - a.y * b.x;
            na += a.sqrMagnitude;
        }
        if(na < 0.000001f) return cb;
        re /= na; im /= na;   // 복소수 s = 크기 × 회전
        Vector2 t = Star(c, mirror) - ca;
        return cb + new Vector2(re * t.x - im * t.y, re * t.y + im * t.x);
    }

    Vector2 Star(int i, bool mirror) => mirror ? new Vector2(-planStars[i].x, planStars[i].y) : planStars[i];

    void FinishPlan()
    {
        var pts = planPts.ToArray();
        float tol = planK >= 7 ? Tuning.Get("comet_match_7", 0.1f) : planK >= 5 ? Tuning.Get("comet_match_5", 0.08f) : Tuning.Get("comet_match_3", 0.06f);
        if(StarArt.ShapeError(planStars, pts, out bool rev) >= tol) return;   // 적 배치가 안 맞아서 모양이 안 나왔어요
        if(rev) System.Array.Reverse(pts);
        float mul = planK >= 7 ? Tuning.Get("comet_const_mul_7", 2.5f) : planK >= 5 ? Tuning.Get("comet_const_mul_5", 1.5f) : Tuning.Get("comet_const_mul_3", 1f);
        UltimateFx.PlayConstellation(pts, StarArt.Shape(planK), WeaponBase.ClampDamage(hitDamage * (double)mul), Tuning.Get("comet_const_seg_time", 0.05f),
                                     Tuning.Get("comet_const_band", 0.6f), Tuning.Get("comet_const_star_r", 0.8f), Armor.Pen(TraitsData.WeaponType.BounceGun));
    }

    float TailLength()
    {
        float len = Mathf.Min(Tuning.Get("comet_tail_len", 1.2f) + Tuning.Get("comet_tail_grow", 0.5f) * hits, Tuning.Get("comet_tail_max", 4f));
        int L = AugmentManager.WL("cm_tail");
        return L > 0 ? len * (1f + AugmentDefs.CmTail(L)) : len;
    }

    // 지나온 길의 최근 TailLength 칸만 남겨요
    void UpdateTail()
    {
        trail.Insert(0, pos);
        float max = TailLength(), acc = 0f;
        for(int i = 1; i < trail.Count; i++) {
            float seg = Vector3.Distance(trail[i - 1], trail[i]);
            if(acc + seg >= max) {
                trail[i] = Vector3.Lerp(trail[i - 1], trail[i], (max - acc) / Mathf.Max(seg, 0.0001f));
                if(i + 1 < trail.Count) trail.RemoveRange(i + 1, trail.Count - i - 1);
                break;
            }
            acc += seg;
        }
        tail.positionCount = trail.Count;
        for(int i = 0; i < trail.Count; i++) tail.SetPosition(i, trail[i]);
        head.transform.position = pos;
    }

    // 꼬리에 닿은 적 (머리에 맞은 적도 꼬리가 지나가면 한 번 더 맞아요)
    void TailDamage()
    {
        float now = Time.time, cd = Tuning.Get("comet_tail_cd", 0.5f), r = Tuning.Get("comet_tail_hit", 0.45f);
        float mul = Tuning.Get("comet_tail_mul", 0.3f);
        int L = AugmentManager.WL("cm_tail");
        if(L > 0) mul *= 1f + AugmentDefs.CmTail(L);
        foreach(var e in AugmentManager.EnemiesNear(pos, TailLength() + r)) {
            if(tailCd.TryGetValue(e, out float until) && now < until) continue;
            Vector3 p = e.transform.position;
            p.z = 0f;
            if(DistToTrail(p) > r) continue;
            tailCd[e] = now + cd;
            Deal(e, mul);
        }
    }

    float DistToTrail(Vector3 p)
    {
        if(trail.Count == 1) return Vector3.Distance(p, trail[0]);
        float best = float.MaxValue;
        for(int i = 1; i < trail.Count; i++) {
            Vector3 a = trail[i - 1], ab = trail[i] - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
        }
        return best;
    }

    void Deal(Enemy e, float mul)
    {
        if(!Alive(e) || mul <= 0f) return;
        bool crit = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        AugmentManager.WeaponHit(e, WeaponBase.ClampDamage(hitDamage * (double)mul), crit, Player.instance.FinalCriticalDamage,
                                 HitKind.Weapon, Armor.Pen(TraitsData.WeaponType.BounceGun));
        AugmentManager.WeaponHitEvent(SideWeapon.BounceGun);   // 종결 "운명의 별자리" 게이지
    }

    void Finish()
    {
        if(fade >= 0f) return;
        fade = 0f;
        // 혜성 충돌 (합체 전용 R): 마지막으로 맞힌 자리에서 크게 터져요. 튕긴 적이 많을수록 세져요
        int L = AugmentManager.WL("cm_impact");
        if(L > 0 && hits > 0) {
            float r = Tuning.Get("comet_impact_radius", 1.3f);
            float mul = AugmentDefs.CmImpact(L) * (1f + Tuning.Get("comet_impact_per_hit", 0.1f) * hits);
            RemasterFx.Burst(lastHitPos, r, ImpactColor);
            RemasterFx.Ring(lastHitPos, r, Color.white, 0.3f);
            foreach(var e in AugmentManager.EnemiesNear(lastHitPos, r)) Deal(e, mul);
        }
    }

    // 끝나면 0.25초 동안 꼬리가 가늘어지며 사라져요 (피해 없음)
    void Fade(float dt)
    {
        fade += dt;
        float k = Mathf.Clamp01(fade / 0.25f);
        tail.widthMultiplier = TailWidth * (1f - k);
        var c = head.color;
        c.a = 1f - k;
        head.color = c;
        if(k >= 1f) Destroy(gameObject);
    }

    static bool Alive(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;

    static Enemy Nearest(Vector3 from, ICollection<Enemy> exclude)
    {
        var list = AugmentManager.NearestInRange(from, 1, exclude);
        return list.Count > 0 ? list[0] : null;
    }
}
