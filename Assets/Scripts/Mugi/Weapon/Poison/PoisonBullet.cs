using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 독탄 (독 비행기)
// 적(또는 연습용 더미)에 닿거나, 일정 거리(화면 폭의 약 40%)를 날면 터져서 독 지대를 깔아요.
// 리마스터: 물리 몸체를 Kinematic 으로 (적과 겹치면 튕겨 나가던 문제), 지나간 길 전체로 닿는 판정.
public class PoisonBullet : MonoBehaviour
{
    [HideInInspector]
    public long BulletDamage;

    public GameObject bulletAni;
    public GameObject BlastAni;

    public Vector3 initialPosition;          // 날아간 거리 확인용 (터지는 데는 안 써요)
    public float distanceTraveled = 0f;
    [HideInInspector] public float burstDistance = -1f;   // 0보다 크면 이 거리에서 터져요 (역병 폭탄 조준 사격: 노린 적 자리)

    const float Speed = 3f;                   // 칸/초 (예전과 같음)
    const float OffScreenMargin = 0.5f;       // 화면 밖으로 이만큼 나가면 사라져요

    bool isExplode;
    Rigidbody2D rb;
    CircleCollider2D col;
    Animator blastAnimator;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        if(BlastAni != null) blastAnimator = BlastAni.GetComponent<Animator>();
        // 물리엔진이 밀지 못하게 Kinematic 으로 (질량 0.0001 이라 적과 겹치면 튕겨 나갔어요). 충돌 알림은 그대로 받아요
        if(rb != null && rb.bodyType == RigidbodyType2D.Dynamic) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }
    }

    private void OnEnable()
    {
        initialPosition = transform.position;
        distanceTraveled = 0f;
        isExplode = false;
        //Animation
        bulletAni.SetActive(true);
        BlastAni.SetActive(false);
        if(rb != null) {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void Update()
    {
        if(isExplode) {
            // 터지는 그림이 끝나면 풀로 돌아가요
            if(blastAnimator == null || blastAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1)
                DestroyBullet();
            return;
        }

        Vector3 prev = transform.position;
        transform.Translate(Vector3.down * Speed * Time.deltaTime);
        distanceTraveled = Vector3.Distance(initialPosition, transform.position);

        // 지나간 길에 적이 있으면 그 자리에서 터져요 (게임 속도를 올려도 지나치지 않게 길 전체를 검사)
        if(FindContact(prev, transform.position, out Vector3 hitPos)) {
            transform.position = hitPos;
            Burst();
            return;
        }

        // 안 닿아도 일정 거리(화면 폭의 약 40%)를 날면 터져서 독 지대를 깔아요 (원래 동작)
        Camera cam = Camera.main;
        float limit = burstDistance > 0f ? burstDistance : (cam != null ? 2f * cam.orthographicSize * cam.aspect / 2.3f : 3f);
        if(distanceTraveled >= limit) {
            Burst();
            return;
        }
        if(IsOffScreen()) DestroyBullet();   // 안전장치
    }

    // 터지기: 그림 + 독 지대 (+ 역병 폭탄) + 소리
    void Burst()
    {
        if(isExplode) return;
        isExplode = true;
        Explode();
        SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.poison);
    }

    bool IsOffScreen()
    {
        Camera cam = Camera.main;
        if(cam == null) return false;
        float h = cam.orthographicSize + OffScreenMargin;
        float w = cam.orthographicSize * cam.aspect + OffScreenMargin;
        Vector3 p = transform.position;
        return p.x > w || p.x < -w || p.y > h || p.y < -h;
    }

    static bool IsLiveTarget(Collider2D c)
    {
        if(c == null) return false;
        if(c.TryGetComponent(out Enemy e)) return e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy;
        return c.TryGetComponent(out Dummy _);
    }

    // prev → now 사이에서 처음 닿는 적 (판정 원은 프리팹의 충돌 원 크기·위치 그대로). 닿으면 그때의 탄 위치를 돌려줘요
    bool FindContact(Vector3 prev, Vector3 now, out Vector3 hitPos)
    {
        hitPos = now;
        Vector3 offset = col != null ? transform.TransformVector(col.offset) : Vector3.zero;
        Vector3 scale = transform.lossyScale;
        float r = col != null ? col.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) : 0.3f;
        Vector2 from = prev + offset;

        foreach(var c in Physics2D.OverlapCircleAll(from, r))
            if(IsLiveTarget(c)) { hitPos = prev; return true; }

        Vector2 move = now - prev;
        float dist = move.magnitude;
        if(dist < 0.00001f) return false;
        Vector2 dir = move / dist;
        float best = float.MaxValue;
        foreach(var h in Physics2D.CircleCastAll(from, r, dir, dist))
            if(IsLiveTarget(h.collider) && h.distance < best) best = h.distance;
        if(best == float.MaxValue) return false;
        hitPos = prev + (Vector3)(dir * best);
        return true;
    }

    public void Explode()
    {
        bulletAni.SetActive(false);
        BlastAni.SetActive(true);
        if(AugmentManager.Fused(AugmentDefs.FzPlague)) PlagueBlast();
        createPoisonArea();
    }

    public void createPoisonArea()
    {
        PoisonArea poisonArea = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.PoisonArea).GetComponent<PoisonArea>();
        // 무기 전용 "맹독 지대": 독 지대 피해 증가
        int toxic = AugmentManager.WL("ps_toxic");
        poisonArea.poisonDamage = toxic > 0 ? WeaponBase.ClampDamage(BulletDamage * (1.0 + AugmentDefs.PsToxic(toxic))) : BulletDamage;
        poisonArea.spreadLeft = AugmentManager.Fused(AugmentDefs.FzPlague) ? AugmentManager.WL("pb_spread") : 0;
        poisonArea.slowAmount = PlagueSlow;
        poisonArea.transform.position = transform.position;

        //Scale if Passive
        poisonArea.SetScaleMul((PoisonPlane.instance.extraSizeOfPoisonArea ? 1.3f : 1f) * AugmentManager.PoisonAreaScale * PlagueAreaScale);

        poisonArea.gameObject.SetActive(true);
    }


    // 합체 "역병 폭탄": 둔화 (tuning.txt plague_slow)
    public static float PlagueSlow => AugmentManager.Fused(AugmentDefs.FzPlague) ? Tuning.Get("plague_slow", 0.3f) : 0f;

    // 합체 "역병 폭탄": 독 지대 1.5배 (+ 독 장판 확대)
    public static float PlagueAreaScale {
        get {
            if(!AugmentManager.Fused(AugmentDefs.FzPlague)) return 1f;
            int l = AugmentManager.WL("pb_area");
            return 1.5f * (l > 0 ? 1f + AugmentDefs.PbArea(l) : 1f);
        }
    }

    // 합체 "역병 폭탄": 독탄이 터질 때 주변 피해 (맹독 폭발이면 더 세고 독 3중첩)
    void PlagueBlast()
    {
        int tl = AugmentManager.WL("pb_toxic");
        long dmg = WeaponBase.ClampDamage(BulletDamage * 2.0 * (tl > 0 ? 1.0 + AugmentDefs.PbToxic(tl) : 1.0));
        float r = 1.2f * PlagueAreaScale / 1.5f;
        RemasterFx.Burst(transform.position, r, new Color(0.5f, 0.9f, 0.3f));
        bool crit = PoisonPlane.instance.Poison_canCrit && Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        foreach(var e in AugmentManager.EnemiesNear(transform.position, r)) {
            AugmentManager.WeaponHit(e, PoisonArea.StackBonus(e, dmg), crit, Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.Pen(TraitsData.WeaponType.Poison));   // 독 중첩 보너스
            if(tl > 0) e.AddPoison(3, 4f);
            e.AddSlow(PlagueSlow, 2f);   // 폭발에 맞으면 2초 둔화
        }
    }

    public void DestroyBullet()
    {
        ObjectPoolBullet.Instance.CoolObject(this.gameObject, PoolObjectTypeBullet.Poison);
    }

    // 물리 충돌 알림도 받아요 (위의 길 검사와 둘 중 먼저 오는 쪽)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(isExplode) return;
        if(collision.gameObject.GetComponent<Enemy>() != null || collision.gameObject.GetComponent<Dummy>() != null)
            Burst();
    }
}
