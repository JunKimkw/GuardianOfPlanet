using System.Collections;
using UnityEngine;

// 폭탄 (리마스터에서 다시 짬)
// 1) 쏠 때 정한 목표 지점 쪽으로 날아가요. 적에 닿으면 그 자리에서 바로 터져요 (융단 폭격 옆 폭탄도 각자).
// 2) 터지면 반경 안의 적(과 연습용 더미)에게 한 번 피해를 줘요. 안 닿으면 목표 지점을 지나 계속 날아가 화면 밖에서 사라져요.
// 3) 폭발 그림은 반경에 맞춰 크기를 정해요.
//
// 예전 문제: 프리팹 크기 5배 × 충돌 반경 1.1 = 날아가는 중에도 반경 5.5칸이라 쏘자마자 우주선 앞에서 터졌고,
// 폭발 때 반경 10 × 5 = 50칸(화면 전체)을 때렸어요. 폭발 그림(explosion.png, 픽셀당 1.5)도 화면의 몇 배로 그려졌어요.
public class BomberBullet : Bullet
{
    [HideInInspector] public bool hasScaled;          // 예전 프리팹 호환용 (안 써요)
    [HideInInspector] public long Bullet_Damage;

    public SpriteRenderer sprite;
    public Sprite DefaultSprite;
    public Animator ExplosionEffect;
    public CircleCollider2D circleCollider;

    public static float BaseRadius => Tuning.Get("bomber_radius", 0.6f);   // 폭발 반경 (월드 단위, 화면 높이는 14칸). 키우는 건 패시브·증강으로
    const float ExplosionSpriteSize = 26.7f;           // 폭발 그림의 가장 큰 프레임 너비 (크기 1일 때)
    const float ExplosionShowTime = 0.6f;              // 폭발 그림을 보여 주는 시간
    public static float BaseFlySpeed => Tuning.Get("bomber_fly_speed", 2.5f);   // 기본 비행 속도 (칸/초). 느린 게 폭격기의 단점 (범위 공격 대신). 탄속 능력치·특성만큼 빨라져요
    const float MaxFlightTime = 8f;                    // 안전장치: 이 시간이 지나면 사라져요
    const float FlightSize = 0.35f;                    // 날아가는 폭탄 그림 크기 (월드 단위)

    Vector3 target;
    Enemy homing;                                      // 합체 "유도 폭격": 쫓아가는 적
    float explosionScale;
    float flightTime;
    float flySpeed;
    bool exploded;
    Rigidbody2D rb;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Bomber;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Bomber;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        exploded = false;
        flightTime = 0f;
        // 물리 충돌은 쓰지 않아요 (피해는 폭발할 때 직접 계산)
        if(circleCollider != null) circleCollider.enabled = false;
        if(rb != null) rb.simulated = false;
        if(ExplosionEffect != null) ExplosionEffect.SetBool("isMoving", true);
    }

    void OnDisable()
    {
        exploded = false;
        homing = null;
    }

    // 크기는 프리팹 값(5배)에 맡기지 않고 매 프레임 직접 맞춰요.
    // 날아갈 때 그림(Bomb_2_Explosion_004, 810px)은 원래 크기면 16칸이라, 풀에서 다시 꺼낸 폭탄이 화면을 덮었어요.
    void LateUpdate()
    {
        if(exploded) {
            transform.localScale = new Vector3(explosionScale, explosionScale, 1f);
            return;
        }
        var sr = sprite != null ? sprite : GetComponent<SpriteRenderer>();
        if(sr == null || sr.sprite == null) return;
        Vector3 b = sr.sprite.bounds.size;
        float size = Mathf.Max(b.x, b.y);
        if(size <= 0.0001f) return;
        float s = FlightSize * SizeMul / size;
        transform.localScale = new Vector3(s, s, 1f);
    }

    // 비행 속도 = 기본 × (1 + 탄속 능력치) × (1 + 폭격기 탄속 특성). Bomber 가 조준(앞을 노리는 거리)에도 써요
    public static float FlySpeed =>
        BaseFlySpeed * (1 + Player.instance.FinalAttack_Bullet_SpeedPercent)
                     * (1 + TraitsManager.instance.GetStats(TraitsData.WeaponType.Bomber, TraitsData.TraitsType.BulletSpeed));

    static float SizeMul => AugmentManager.Instance != null ? AugmentManager.Instance.BulletScaleMultiplier : 1f;

    // Bomber 가 쏠 때 불러요
    public void Launch(long damage, Vector3 targetPos, Enemy follow = null)
    {
        Bullet_Damage = damage;
        homing = follow;
        target = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        Vector3 d = target - transform.position;
        // 예전엔 초당 2칸 + 2.5초 시간 제한이라 멀리 있는 적까지 가기 전에 허공에서 터졌어요
        flySpeed = FlySpeed * (Guided ? 1.6f : 1f);   // 유도 폭격은 미사일처럼 빨라요
        flyDir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.up;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flyDir.y, flyDir.x) * Mathf.Rad2Deg - 90f);
    }

    Vector3 flyDir;   // 날아가는 방향 (목표 지점을 지나도 이 방향으로 계속)

    public void setBulletStats(long a) => Bullet_Damage = a;   // 예전 호출 호환

    void Update()
    {
        if(exploded) return;
        flightTime += Time.deltaTime;
        if(homing != null) {
            if(homing._state == Enemy.EnemyState.dead || !homing.gameObject.activeInHierarchy) homing = null;   // 쫓던 적이 죽으면 그 방향으로 직진
            else {
                Vector3 d = new Vector3(homing.pos.x, homing.pos.y, transform.position.z) - transform.position;
                if(d.sqrMagnitude > 0.0001f) {
                    flyDir = d.normalized;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
                }
            }
        }
        float step = flySpeed * Time.deltaTime;
        Vector3 prev = transform.position;
        Vector3 next = prev + flyDir * step;   // 목표 지점에서 멈추지 않고 계속 날아가요

        // 날아가다 적(또는 연습용 더미)에 닿으면 그 자리에서 바로 터져요 (2026-11-18 사용자 결정).
        // 한 프레임에 지나간 길 전체를 검사해요 (게임 속도를 올려도 작은 적을 지나치지 않게)
        if(FindContact(prev, next, out Vector3 hitPos)) {
            transform.position = hitPos;
            Explode();
            return;
        }

        transform.position = next;
        // 아무 적에도 안 닿았으면 터지지 않고, 화면 밖으로 나가면 사라져요 (2026-11-18 사용자 결정)
        if(IsOutOfScreen() || flightTime >= MaxFlightTime)
            DestroyBullet();
    }

    bool IsOutOfScreen()
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize + 0.5f, w = cam.orthographicSize * cam.aspect + 0.5f;
        Vector3 p = transform.position;
        return p.x > w || p.x < -w || p.y > h || p.y < -h;
    }

    // 닿는 판정 반경: 날아가는 폭탄 그림 크기쯤 (tuning.txt bomber_contact_radius)
    static float ContactRadius => Tuning.Get("bomber_contact_radius", 0.18f) * SizeMul;

    static bool IsLiveTarget(Collider2D c)
    {
        if(c == null) return false;
        if(c.TryGetComponent(out Enemy e)) return e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;
        return c.TryGetComponent(out Dummy _);
    }

    // prev → next 사이에서 처음 닿는 적. 닿으면 그 지점(폭탄 중심)을 돌려줘요
    bool FindContact(Vector3 prev, Vector3 next, out Vector3 hitPos)
    {
        hitPos = prev;
        float r = ContactRadius;
        // 출발 자리에서 이미 겹쳐 있는 적
        foreach(var c in Physics2D.OverlapCircleAll(prev, r))
            if(IsLiveTarget(c)) return true;

        Vector2 move = next - prev;
        float dist = move.magnitude;
        if(dist < 0.00001f) return false;
        Vector2 dir = move / dist;
        float best = float.MaxValue;
        foreach(var h in Physics2D.CircleCastAll(prev, r, dir, dist))
            if(IsLiveTarget(h.collider) && h.distance < best) best = h.distance;
        if(best == float.MaxValue) return false;
        hitPos = prev + (Vector3)(dir * best);
        return true;
    }

    static bool Guided => AugmentManager.Fused(AugmentDefs.FzGuided);

    float Radius()
    {
        float r = BaseRadius;
        if(Bomber.instance != null && Bomber.instance.ExploseRangeIncrease) r *= 1.5f;   // 패시브
        r *= SizeMul;                                                                     // 탄 크기 증강 (큰 탄 등)
        r *= AugmentManager.BombRadiusMultiplier;                                         // 폭발 범위 증강 (화약 연구, 연쇄 세트 2)
        int big = AugmentManager.WL("bm_big");
        if(big > 0) r *= 1f + AugmentDefs.BmBig(big);                                     // 무기 전용 "대형 폭탄"
        if(Guided) r *= 1.4f;                                                             // 합체 "유도 폭격"
        return r;
    }

    void Explode()
    {
        exploded = true;
        float r = Radius();
        SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.BomberExplosion);

        // 폭발 그림: 가장 큰 프레임이 지름 2r 이 되게
        explosionScale = 2f * r / ExplosionSpriteSize;
        transform.localScale = new Vector3(explosionScale, explosionScale, 1f);
        transform.rotation = Quaternion.identity;
        if(ExplosionEffect != null) ExplosionEffect.SetBool("isMoving", false);

        Blast(r, Bullet_Damage);

        // 2차 폭발 (무기 전용) / 이중 폭발 (합체): 0.5초 뒤 한 번 더
        int dbl = Guided ? AugmentManager.WL("gb_double") : 0;
        int second = AugmentManager.WL("bm_second");
        if(dbl > 0) StartCoroutine(SecondBlast(r * 1.3f, AugmentDefs.GbDouble(dbl)));
        else if(second > 0) StartCoroutine(SecondBlast(r, AugmentDefs.BmSecond(second)));

        StartCoroutine(FinishAfter(ExplosionShowTime));
    }

    // 반경 안의 적·더미에게 한 번씩
    void Blast(float r, long damage)
    {
        var done = new System.Collections.Generic.HashSet<GameObject>();
        bool any = false;
        foreach(var c in Physics2D.OverlapCircleAll(transform.position, r))
            if(c != null && c.gameObject != gameObject && done.Add(c.gameObject) && Hit(c.gameObject, damage, PlayerCritDamage)) any = true;
        if(any) AugmentManager.WeaponHitEvent(SideWeapon.Bomber);   // 종결 증강 게이지
    }

    IEnumerator SecondBlast(float r, float mul)
    {
        yield return new WaitForSeconds(0.5f);
        RemasterFx.Burst(transform.position, r, new Color(1f, 0.6f, 0.25f));
        Blast(r, WeaponBase.ClampDamage(Bullet_Damage * (double)mul));
    }

    IEnumerator FinishAfter(float t)
    {
        yield return new WaitForSeconds(t);
        DestroyBullet();
    }
}
