using System.Collections.Generic;
using UnityEngine;

// 합체 "별빛 섬광" (2026-11-21 사용자 결정, 360 레이저 + 운석. 예전 "프리즘 빔" 자리: 무지개가 유치하고 빙빙 도는 빔이 눈 아프다고 해서)
//  StarlightFall: 하늘에서 하얀 별이 가는 꼬리를 그리며 적 위로 떨어져요 (떨어지는 동안 그 적을 따라가요).
//  StarFlash: 닿는 순간 가운데가 번쩍이고 가로·세로로 길고 가는 빛살이 뻗었다가 사라져요 (망원경 사진 속 별빛 갈래처럼). 돌지 않아요.
//  빛살 줄 위의 적은 모두 한 번씩 맞아요 (StarFlash.Collect).
//  무기 규칙(간격·별 수·피해)은 Lazer.cs, 종결 "북극성"은 Ultimates.cs. 숫자 tuning.txt starlight_*
public class StarFlash : MonoBehaviour
{
    public static readonly Color Glow = new Color(0.62f, 0.82f, 1f);   // 빛살 빛무리 (하늘색)
    public static readonly Color Gold = new Color(1f, 0.84f, 0.45f);   // 대각선 빛살 빛무리 (여덟 갈래, 금색)

    // 빛살 그림 (코드로 만들어요): 가로로 긴 빛줄기. 가운데가 가장 밝고 굵으며 양 끝으로 갈수록 가늘고 흐려져요.
    // 그림 크기 = 가로 1칸 × 세로 RayH 칸 (localScale 로 늘려요). 흰색이라 색은 SpriteRenderer 로 입혀요
    static Sprite ray;
    public static Sprite RaySprite => ray != null ? ray : (ray = MakeRay());
    const float RayH = 0.25f;

    static Sprite MakeRay()
    {
        const int W = 128, H = 32;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for(int y = 0; y < H; y++) for(int x = 0; x < W; x++) {
            float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
            float taper = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.4f);   // 양 끝으로 갈수록 흐려지고
            float s = Mathf.Lerp(0.12f, 0.5f, taper);                          // 가늘어져요
            px[y * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(taper * Mathf.Exp(-(v * v) / (s * s))));
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
    }

    class Spike { public SpriteRenderer glow, core; public float len; public Color tint; }
    readonly List<Spike> spikes = new List<Spike>();
    SpriteRenderer flare;
    float t, life, thick, flareSize;
    const float Grow = 0.06f, Hold = 0.14f;   // 0.06초 만에 뻗고, 0.14초까지 그대로, 그다음 흐려져요

    // c: 가운데, len: 가운데에서 빛살 끝까지(칸), thick: 빛살 굵기(칸), diagLen: 대각선 빛살 길이(0 이면 없음),
    // life: 보이는 시간(초), angle: 빛살 방향(0 = 가로·세로, 45 = 대각선), flareSize: 가운데 번쩍 크기(칸, 음수면 굵기에 맞춰요)
    public static StarFlash Spawn(Vector3 c, float len, float thick, float diagLen, float life, float angle = 0f, float flareSize = -1f, int order = 515)
    {
        c.z = 0f;
        var go = new GameObject("StarFlash");
        go.transform.position = c;
        var f = go.AddComponent<StarFlash>();
        f.life = Mathf.Max(Hold + 0.05f, life);
        f.thick = thick;
        f.flareSize = flareSize > 0f ? flareSize : thick * 3.2f;
        f.AddSpike(angle, len, Glow, order);
        f.AddSpike(angle + 90f, len, Glow, order);
        if(diagLen > 0f) {
            f.AddSpike(angle + 45f, diagLen, Gold, order);
            f.AddSpike(angle + 135f, diagLen, Gold, order);
        }
        f.flare = f.Part("Flare", StarArt.Star, Color.white, order + 2);
        f.Apply();
        return f;
    }

    SpriteRenderer Part(string name, Sprite s, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingLayerName = RemasterFx.FxLayer;
        sr.sortingOrder = order;
        return sr;
    }

    // 빛살 한 줄 = 하늘색(또는 금색) 빛무리 + 하얀 심. 가운데를 지나 양쪽으로 뻗어요
    void AddSpike(float deg, float len, Color tint, int order)
    {
        var sp = new Spike { len = len, tint = tint };
        sp.glow = Part("RayGlow", RaySprite, tint, order);
        sp.core = Part("RayCore", RaySprite, Color.white, order + 1);
        sp.glow.transform.localRotation = sp.core.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
        spikes.Add(sp);
    }

    void Update()
    {
        t += Time.deltaTime;
        if(t >= life) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply()
    {
        float g = Mathf.Clamp01(t / Grow);
        g = 1f - (1f - g) * (1f - g);                                   // 빠르게 뻗었다가
        float f = t <= Hold ? 1f : 1f - (t - Hold) / (life - Hold);    // 천천히 흐려져요
        float y = thick / RayH;
        foreach(var sp in spikes) {
            float L = 2f * sp.len * g * (1f + 0.08f * (1f - f));       // 사라지면서 살짝 더 뻗어요
            sp.glow.transform.localScale = new Vector3(L, y * (0.55f + 0.45f * f), 1f);
            sp.core.transform.localScale = new Vector3(L * 0.92f, y * 0.35f * (0.5f + 0.5f * f), 1f);
            sp.glow.color = new Color(sp.tint.r, sp.tint.g, sp.tint.b, 0.85f * f);
            sp.core.color = new Color(1f, 1f, 1f, f);
        }
        flare.transform.localScale = Vector3.one * flareSize * (0.5f + 0.8f * g) * (0.55f + 0.45f * f);
        flare.color = new Color(1f, 1f, 1f, f);
    }

    // 빛살에 닿는 적: 가운데에서 len 안, 빛살 중심선에서 band 안 (적 중심 기준). 대각선은 diagLen (0 이면 없음)
    public static void Collect(Vector3 c, float len, float band, float diagLen, float angle, List<Enemy> into)
    {
        into.Clear();
        c.z = 0f;
        float a = angle * Mathf.Deg2Rad;
        var ux = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        var uy = new Vector2(-ux.y, ux.x);
        Vector2 dx = (ux + uy) * 0.70710678f, dy = (uy - ux) * 0.70710678f;   // 대각선 (45도, 135도)
        foreach(var e in AugmentManager.EnemiesNear(c, Mathf.Max(len, diagLen) + band)) {
            Vector2 d = e.transform.position - c;
            if(OnLine(d, ux, len, band) || OnLine(d, uy, len, band)
               || (diagLen > 0f && (OnLine(d, dx, diagLen, band) || OnLine(d, dy, diagLen, band))))
                into.Add(e);
        }
    }

    static bool OnLine(Vector2 d, Vector2 dir, float len, float band)
    {
        float along = Vector2.Dot(d, dir);
        if(Mathf.Abs(along) > len) return false;
        return Mathf.Abs(d.x * dir.y - d.y * dir.x) <= band;   // 빛살 중심선까지 거리
    }
}

// 별빛 섬광의 별 하나: 하늘에서 비스듬히 떨어지며 (목표 적을 따라가요, 죽으면 그 자리로) 닿으면 StarFlash + 빛살 위 적에게 피해
public class StarlightFall : MonoBehaviour
{
    Lazer owner;
    Enemy target;
    Vector3 at, at0, from;
    long dmg;
    float len, diag, t, fall;
    SpriteRenderer head;
    TrailRenderer trail;
    bool landed;
    static Material mat;

    public static bool Alive(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;

    // target 이 null 이면 at 자리에 떨어져요 (적보다 별이 많을 때 둘레에)
    public static void Drop(Lazer owner, Enemy target, Vector3 at, long dmg, float len, float diag)
    {
        at.z = 0f;
        var go = new GameObject("StarlightFall");
        var s = go.AddComponent<StarlightFall>();
        s.owner = owner;
        s.target = target;
        s.at = s.at0 = at;
        s.dmg = dmg;
        s.len = len;
        s.diag = diag;
        s.fall = Mathf.Max(0.08f, Tuning.Get("starlight_fall_time", 0.3f));
        // 비스듬히 위에서 (별마다 출발점이 조금씩 달라서 한 줄로 안 보여요)
        s.from = at + new Vector3(Tuning.Get("starlight_from_x", -1.6f) + Random.Range(-0.8f, 0.8f), Tuning.Get("starlight_from_y", 6.5f), 0f);
        go.transform.position = s.from;
        s.Build();
    }

    void Build()
    {
        var hgo = new GameObject("Head");
        hgo.transform.SetParent(transform, false);
        hgo.transform.localScale = Vector3.one * 0.55f;
        head = hgo.AddComponent<SpriteRenderer>();
        head.sprite = StarArt.Star;
        head.color = Color.white;
        head.sortingLayerName = RemasterFx.FxLayer;
        head.sortingOrder = 512;
        trail = gameObject.AddComponent<TrailRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        trail.sharedMaterial = mat;
        trail.time = 0.14f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.widthMultiplier = 0.1f;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(StarFlash.Glow, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.sortingLayerName = RemasterFx.FxLayer;
        trail.sortingOrder = 511;
        trail.Clear();
    }

    void Update()
    {
        if(landed) return;
        t += Time.deltaTime;
        if(target != null) {
            if(Alive(target)) { at = target.transform.position; at.z = 0f; }
            else target = null;   // 죽으면 그 자리로
        }
        float k = Mathf.Clamp01(t / fall);
        float u = k * (0.55f + 0.45f * k);   // 끝으로 갈수록 빨라져요
        transform.position = Vector3.Lerp(from + (at - at0), at, u);   // 적이 움직인 만큼 하늘 출발점도 같이 옮겨요 (휘어 보이지 않게)
        head.transform.localScale = Vector3.one * 0.55f * (1f + 0.2f * Mathf.Sin(t * 50f));   // 반짝반짝
        if(k >= 1f) Land();
    }

    void Land()
    {
        landed = true;
        head.enabled = false;
        trail.autodestruct = true;     // 꼬리가 다 사라지면 저절로 지워져요
        Destroy(gameObject, 1f);       // 혹시 안 지워지면
        StarFlash.Spawn(at, len, Tuning.Get("starlight_ray_width", 0.4f), diag, Tuning.Get("starlight_ray_time", 0.45f));
        if(owner == null) return;
        var hits = new List<Enemy>();
        StarFlash.Collect(at, len, Tuning.Get("starlight_hit_band", 0.35f), diag, 0f, hits);
        foreach(var e in hits) owner.StarHit(e, dmg);
        if(hits.Count > 0) AugmentManager.WeaponHitEvent(SideWeapon.Lazer);   // 종결 증강 게이지
    }
}
