using System.Collections.Generic;
using UnityEngine;

// 유도 미사일 (리마스터에서 다시 짬)
// - 미사일마다 자기 목표를 정해요 (화면 안의 가까운 적 3마리 중 하나 → 여러 발이 한 적에 몰리지 않게)
// - 목표가 죽으면 새 목표를 찾아요. 목표가 없으면 직진해요.
// - 일정한 속도로 돌면서 날아가요.
// 예전 문제: 속도에 프레임 시간을 곱해서 프레임이 높을수록 느려졌고, 모든 미사일이 화면 밖 적까지 같은 적 하나만 쫓았어요.
// 증강: 추적 강화(선회), 폭발 탄두(명중 폭발)
// 합체 "록온 미사일" (2026-11-21, 예전 "사냥개 탄" 자리): 유도탄이 록온한 적(lockTarget)에게 쏘는 미사일.
//  처음 lockon_ignite 초는 힘없이 부채꼴로 흩어지다(선회 없음) 점화하면서 빨라지고 세게 휘어 들어가요. 하얀 연기 꼬리, 꽂히면 폭발
public class TrackingBullet : Bullet
{
    Rigidbody2D rb;

    [Header("추적 미사일 전용")]
    public float RotationControl;          // 돌 수 있는 정도 (초당 RotationControl × 4도, 프리팹 100 = 400도)

    [Header("#----Stats")]
    public long bullet_Damage;
    public float missileSpeedScale;
    public float bullet_CritDamage;

    // 록온 미사일: 쏠 때 정한 목표 (SetActive 전에 넣어요. 사라질 때 비워요)
    [HideInInspector] public Enemy lockTarget;
    [HideInInspector] public float speedMul = 1f, smokeMul = 1f;
    [HideInInspector] public bool passThrough;   // 전탄 발사: 목표 말고 다른 적은 지나가요 (사라질 때 false)   // 마이크로 미사일: 더 빠르고 연기가 가늘어요 (사라질 때 1로)

    const float SpeedFactor = 50f / 60f;   // 예전 60프레임 기준 속도와 같게
    const float MaxLife = 6f;

    Enemy target;
    float life;
    bool lockOn, retargeted;
    SpriteRenderer sr;
    TrailRenderer smoke;
    static Material smokeMat;
    static readonly List<Enemy> candidates = new List<Enemy>();

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Tracking;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.TrackingMissile;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        // 물리엔진이 막거나 돌리지 못하게 Kinematic 으로 (충돌 알림은 그대로 받아요).
        // 예전: 질량 0.0001 의 Dynamic 이라 맞힌 적에 끼거나, 부딪히며 빙글 돌았어요
        if(rb != null && rb.bodyType == RigidbodyType2D.Dynamic) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }
    }

    void OnEnable()
    {
        life = 0f;
        lockOn = lockTarget != null;
        retargeted = false;
        target = lockTarget;
        if(sr != null) sr.color = Color.white;
        SetupSmoke(lockOn);
    }

    // 록온 미사일만 하얀 연기 꼬리 (풀에서 다시 꺼낼 때 예전 꼬리가 남지 않게 비워요)
    void SetupSmoke(bool on)
    {
        if(smoke == null) {
            if(!on) return;
            smoke = gameObject.AddComponent<TrailRenderer>();
            if(smokeMat == null) smokeMat = new Material(Shader.Find("Sprites/Default"));
            smoke.sharedMaterial = smokeMat;
            smoke.minVertexDistance = 0.05f;
            smoke.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.7f, 0.75f), 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            smoke.colorGradient = g;
            smoke.sortingLayerName = RemasterFx.FxLayer;
            smoke.sortingOrder = 40;
        }
        smoke.time = Tuning.Get("lockon_smoke_time", 0.45f);
        smoke.widthMultiplier = Tuning.Get("lockon_smoke_width", 0.12f) * smokeMul;
        smoke.Clear();
        smoke.emitting = on;
        smoke.enabled = on;
    }

    void Update()
    {
        life += Time.deltaTime;
        if(target == null || target._state == Enemy.EnemyState.dead || !target.gameObject.activeInHierarchy) {
            if(lockOn) {   // 록온 미사일: 앞쪽 적만 (없으면 곧게)
                // 마이크로 미사일 (2026-11-23 사용자: 마이크로 미사일 나눠 쏘기): 노리던 적이 먼저 죽으면 아직 표식 없는 가장 가까운 적에게 (없으면 가장 가까운 적)
                target = AugmentManager.Fused(AugmentDefs.FzMicro) && Tuning.Get("micro_spread", 0f) > 0f ? PickUnmarked() : PickAhead();
                retargeted = true;
            }
            else target = PickTarget();
        }

        float ignite = lockOn ? Tuning.Get("lockon_ignite", 0.18f) : 0f;
        bool launching = life < ignite;   // 록온 미사일: 점화 전엔 흩어지기만 (선회 없음)
        if(target != null && !launching) {
            Vector2 to = (Vector2)target.pos - (Vector2)transform.position;
            float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
            int hl = AugmentManager.WL("tk_homing");
            float turnRate = Mathf.Max(90f, RotationControl * 4f) * (hl > 0 ? 1f + AugmentDefs.TkHoming(hl) : 1f);
            if(lockOn && !retargeted) turnRate *= Tuning.Get("lockon_turn_mul", 3f);   // 점화하면 세게 휘어 들어가요 (목표를 바꾼 뒤엔 보통 선회로 부드럽게)
            // 가까우면 더 세게 꺾어요: 선회 반경(속도 ÷ 선회)이 목표까지 거리보다 작게 → 목표 둘레를 뱅글 돌지 않아요 (2026-11-21)
            float curSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
            if(curSpeed > 0.01f && to.magnitude > 0.01f) turnRate = Mathf.Max(turnRate, curSpeed / to.magnitude * Mathf.Rad2Deg * 1.5f);
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, want, turnRate * Time.deltaTime);   // 곡선을 그리며 따라가요
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }

        // 유도탄 속도 배율 (tuning.txt tracking_speed_mul, 2026-11-18: 조금 느리다고 해서 1.2배)
        float speed = BulletSpeed * SpeedMultiplier * (1 + missileSpeedScale) * SpeedFactor * Tuning.Get("tracking_speed_mul", 1.2f);
        if(lockOn) {   // 록온 미사일: 툭 튀어나갔다가(느리게) 점화하면 빨라져요
            float boost = Tuning.Get("lockon_speed_mul", 2f);
            speed *= (launching ? 0.45f : Mathf.Lerp(0.45f, boost, Mathf.Clamp01((life - ignite) / 0.25f))) * speedMul;
        }
        if(rb != null) {
            rb.angularVelocity = 0f;
            rb.linearVelocity = transform.up * speed;
        } else {
            transform.Translate(Vector3.up * speed * Time.deltaTime);
        }

        if(life >= MaxLife || IsOffScreen()) DestroyBullet();
    }

    // 화면 안의 적 중 미사일에서 가까운 3마리 중 무작위 하나
    Enemy PickTarget()
    {
        var em = EnemyManager.Instance;
        if(em == null) return null;
        candidates.Clear();
        foreach(var list in new[] { em.bossData, em.enemyData })
            foreach(var e in list)
                if(e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy && e.IsInRange) candidates.Add(e);
        if(candidates.Count == 0) return null;
        Vector3 p = transform.position;
        candidates.Sort((a, b) => (a.pos - p).sqrMagnitude.CompareTo((b.pos - p).sqrMagnitude));
        return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
    }

    // 록온 미사일 목표가 먼저 죽었을 때: 진행 방향 ±lockon_retarget_angle(70)도 안의 가장 가까운 적 (뒤로 홱 돌지 않게, 2026-11-21)
    Enemy PickUnmarked()
    {
        var list = AugmentManager.NearestInRange(transform.position, 999);
        foreach(var e in list) if(!LockOnMark.IsMarked(e)) return e;
        return list.Count > 0 ? list[0] : null;
    }

    Enemy PickAhead()
    {
        var list = AugmentManager.NearestInRange(transform.position, 999);
        float limit = Mathf.Cos(Tuning.Get("lockon_retarget_angle", 70f) * Mathf.Deg2Rad);
        Vector2 fwd = transform.up;
        foreach(var e in list) {
            Vector2 to = (Vector2)e.pos - (Vector2)transform.position;
            if(to.sqrMagnitude < 0.0001f || Vector2.Dot(fwd, to.normalized) >= limit) return e;
        }
        return null;
    }

    public void setBulletStats(long a, float b, float c)
    {
        bullet_Damage = a;
        missileSpeedScale = b;
        bullet_CritDamage = c;
    }

    public override void DestroyBullet()
    {
        if(lockOn && lockTarget != null) LockOnMark.Release(lockTarget);   // 이 미사일이 노리던 록온 표식
        lockTarget = null;
        lockOn = false;
        speedMul = smokeMul = 1f;
        passThrough = false;
        if(smoke != null) smoke.emitting = false;
        base.DestroyBullet();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        collision.gameObject.TryGetComponent(out Enemy enemy);
        if(lockOn && life < Tuning.Get("lockon_ignite", 0.18f) && enemy != null && enemy != lockTarget) return;   // 흩어지는 중엔 지나가요
        if(passThrough && enemy != null && target != null && enemy != target) return;   // 전탄 발사: 노리는 적만 맞혀요 (2026-11-21 사용자)
        long dmg = bullet_Damage;
        if(!Hit(collision.gameObject, dmg, bullet_CritDamage)) return;
        if(enemy != null) AugmentManager.WeaponHitEvent(SideWeapon.TrackingGun);   // 종결 증강 게이지

        // 폭발 탄두: 맞힌 자리에 작은 폭발
        int wh = AugmentManager.WL("tk_warhead");
        if(wh > 0) AugmentManager.WeaponBlast(transform.position, WeaponBase.ClampDamage(dmg * (double)AugmentDefs.TkWarhead(wh)), 0.7f);

        if(lockOn) {   // 록온 미사일: 꽂히면 쾅 (고폭 탄두면 범위 피해)
            Vector3 p = transform.position;
            RemasterFx.Burst(p, 0.45f, new Color(1f, 0.6f, 0.25f));
            RemasterFx.Burst(p, 0.18f, Color.white);
            int hb = AugmentManager.WL("lk_blast");
            if(hb > 0) {
                float r = Tuning.Get("lockon_blast_radius", 0.9f);
                RemasterFx.Ring(p, r, new Color(1f, 0.75f, 0.4f), 0.2f);
                AugmentManager.WeaponBlast(p, WeaponBase.ClampDamage(dmg * (double)AugmentDefs.LkBlast(hb)), r);
            }
        }
        DestroyBullet();
    }
}

// 록온 표식: 적 위에 빨간 조준 고리 + 십자. 찍힐 때 크게 나타났다가 확 조여요. 적을 따라다니고, 그 적을 노리던 미사일이 모두 끝나면 사라져요
public class LockOnMark : MonoBehaviour
{
    static readonly Dictionary<Enemy, LockOnMark> marks = new Dictionary<Enemy, LockOnMark>();
    static Material mat;

    Enemy enemy;
    int refs;
    float age, timeout;
    LineRenderer ring;
    readonly LineRenderer[] ticks = new LineRenderer[4];   // 위·아래·왼쪽·오른쪽 눈금

    public static void Place(Enemy e, float timeout = 3f)
    {
        if(e == null) return;
        if(marks.TryGetValue(e, out var m) && m != null) { m.refs++; m.timeout = Mathf.Max(m.timeout, m.age + timeout); return; }
        var go = new GameObject("LockOnMark");
        m = go.AddComponent<LockOnMark>();
        m.enemy = e;
        m.refs = 1;
        m.timeout = timeout;
        var red = new Color(1f, 0.2f, 0.25f, 0.95f);
        m.ring = m.Line(red, 0.04f, 17);
        for(int i = 0; i < 4; i++) m.ticks[i] = m.Line(red, 0.035f, 2);
        marks[e] = m;
        m.Update();
    }

    public static bool IsMarked(Enemy e) => e != null && marks.TryGetValue(e, out var m) && m != null;

    // 그 적을 노리던 미사일 하나가 끝났어요 (모두 끝나면 사라져요)
    public static void Release(Enemy e)
    {
        if(e == null || !marks.TryGetValue(e, out var m) || m == null) return;
        if(--m.refs <= 0) m.Kill();
    }

    LineRenderer Line(Color c, float w, int points)
    {
        var go = new GameObject("l");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        if(mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.startWidth = lr.endWidth = w;
        lr.startColor = lr.endColor = c;
        lr.sortingLayerName = RemasterFx.FxLayer;
        lr.sortingOrder = 545;
        return lr;
    }

    void Kill()
    {
        if(enemy != null && marks.TryGetValue(enemy, out var m) && m == this) marks.Remove(enemy);
        Destroy(gameObject);
    }

    void Update()
    {
        age += Time.deltaTime;
        if(age > timeout || enemy == null || enemy._state == Enemy.EnemyState.dead || !enemy.gameObject.activeInHierarchy) { Kill(); return; }
        Vector3 c = enemy.transform.position;
        c.z = 0f;
        float k = Mathf.Clamp01(age / 0.12f);
        float r = Mathf.Lerp(0.9f, 0.42f, k * (2f - k)) * (1f + 0.06f * Mathf.Sin(age * 25f));   // 확 조이고 살짝 떨려요
        for(int i = 0; i < ring.positionCount; i++) {
            float a = i * Mathf.PI * 2f / (ring.positionCount - 1);
            ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
        }
        float s = r * 0.55f, g = r * 1.35f;
        for(int i = 0; i < 4; i++) {
            var d = i == 0 ? Vector3.up : i == 1 ? Vector3.down : i == 2 ? Vector3.left : Vector3.right;
            ticks[i].SetPosition(0, c + d * s);
            ticks[i].SetPosition(1, c + d * g);
        }
        transform.position = c;
    }
}
