using System.Collections.Generic;
using UnityEngine;

// 유도 미사일 (리마스터에서 다시 짬)
// - 미사일마다 자기 목표를 정해요 (화면 안의 가까운 적 3마리 중 하나 → 여러 발이 한 적에 몰리지 않게)
// - 목표가 죽으면 새 목표를 찾아요. 목표가 없으면 직진해요.
// - 일정한 속도로 돌면서 날아가요.
// 예전 문제: 속도에 프레임 시간을 곱해서 프레임이 높을수록 느려졌고, 모든 미사일이 화면 밖 적까지 같은 적 하나만 쫓았어요.
// 증강: 추적 강화(선회), 폭발 탄두(명중 폭발), 합체 "사냥개 탄"(맞히고 다음 적으로, 보스 우선, 새 미사일)
public class TrackingBullet : Bullet
{
    Rigidbody2D rb;

    [Header("추적 미사일 전용")]
    public float RotationControl;          // 돌 수 있는 정도 (초당 RotationControl × 4도, 프리팹 100 = 400도)

    [Header("#----Stats")]
    public long bullet_Damage;
    public float missileSpeedScale;
    public float bullet_CritDamage;

    [HideInInspector] public bool packSpawned;   // "무리 사냥"으로 생긴 미사일 (또 만들지 않아요)

    const float SpeedFactor = 50f / 60f;   // 예전 60프레임 기준 속도와 같게
    const float MaxLife = 6f;

    Enemy target;
    float life;
    int houndLeft;                          // 사냥개 탄: 남은 다음 적 쫓기 횟수
    int bites;                              // 사냥개 탄 "흥분": 이 미사일이 문 횟수 (물 때마다 피해·속도 +)
    SpriteRenderer sr;

    // 흥분: 문 횟수만큼 피해·속도 배율 (tuning.txt hound_frenzy)
    float Frenzy => 1f + Tuning.Get("hound_frenzy", 0.15f) * bites;
    readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    static readonly List<Enemy> candidates = new List<Enemy>();

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Tracking;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.TrackingMissile;

    static bool Hound => AugmentManager.Fused(AugmentDefs.FzHound);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        // 물리엔진이 막거나 돌리지 못하게 Kinematic 으로 (충돌 알림은 그대로 받아요).
        // 예전: 질량 0.0001 의 Dynamic 이라 사냥개 탄이 맞힌 적에 끼거나, 부딪히며 빙글 돌았어요
        if(rb != null && rb.bodyType == RigidbodyType2D.Dynamic) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }
    }

    // 사냥개 탄: 튕길 때 새 목표 쪽으로 바로 꺾여요 (천천히 선회하면 맞고 빙글 도는 것처럼 보였어요)
    void SnapToTarget()
    {
        if(target == null) return;
        Vector2 to = (Vector2)target.pos - (Vector2)transform.position;
        if(to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f);
        if(rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnEnable()
    {
        target = null;
        life = 0f;
        hitEnemies.Clear();
        bites = 0;
        if(sr != null) sr.color = Color.white;
        int hb = AugmentManager.WL("hd_bounce");
        houndLeft = Hound ? 2 + (hb > 0 ? AugmentDefs.HdBounce(hb) : 0) : 0;
    }

    void Update()
    {
        life += Time.deltaTime;
        if(target == null || target._state == Enemy.EnemyState.dead || !target.gameObject.activeInHierarchy)
            target = PickTarget();

        if(target != null) {
            Vector2 to = (Vector2)target.pos - (Vector2)transform.position;
            float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
            int hl = AugmentManager.WL("tk_homing");
            float turnRate = Mathf.Max(90f, RotationControl * 4f) * (hl > 0 ? 1f + AugmentDefs.TkHoming(hl) : 1f) * (Hound ? 1.5f : 1f);
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, want, turnRate * Time.deltaTime);   // 곡선을 그리며 따라가요
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }

        // 유도탄·사냥개 탄 속도 배율 (tuning.txt tracking_speed_mul, 2026-11-18: 조금 느리다고 해서 1.2배)
        float speed = BulletSpeed * SpeedMultiplier * (1 + missileSpeedScale) * SpeedFactor * Tuning.Get("tracking_speed_mul", 1.2f) * Frenzy;
        if(rb != null) {
            rb.angularVelocity = 0f;
            rb.linearVelocity = transform.up * speed;
        } else {
            transform.Translate(Vector3.up * speed * Time.deltaTime);
        }

        if(life >= MaxLife || IsOffScreen()) DestroyBullet();
    }

    // 화면 안의 적 중 미사일에서 가까운 3마리 중 무작위 하나 (사냥 본능: 보스 먼저, 사냥개 탄: 맞힌 적 빼고)
    Enemy PickTarget()
    {
        var em = EnemyManager.Instance;
        if(em == null) return null;
        bool bossFirst = Hound && AugmentManager.WL("hd_boss") > 0;
        candidates.Clear();
        foreach(var list in new[] { em.bossData, em.enemyData })
            foreach(var e in list)
                if(e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy && e.IsInRange && !hitEnemies.Contains(e)) candidates.Add(e);
        if(candidates.Count == 0) return null;
        if(bossFirst) {
            foreach(var e in candidates) if(e.IsBoss) return e;
        }
        Vector3 p = transform.position;
        candidates.Sort((a, b) => (a.pos - p).sqrMagnitude.CompareTo((b.pos - p).sqrMagnitude));
        return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
    }

    public void setBulletStats(long a, float b, float c)
    {
        bullet_Damage = a;
        missileSpeedScale = b;
        bullet_CritDamage = c;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        collision.gameObject.TryGetComponent(out Enemy enemy);
        if(enemy != null && hitEnemies.Contains(enemy)) return;   // 사냥개 탄: 이미 문 적은 지나가요

        long dmg = WeaponBase.ClampDamage(bullet_Damage * (double)Frenzy);   // 흥분: 물수록 세져요
        int boss = AugmentManager.WL("hd_boss");
        if(enemy != null && enemy.IsBoss && Hound && boss > 0) dmg = WeaponBase.ClampDamage(dmg * (1.0 + AugmentDefs.HdBoss(boss)));
        if(!Hit(collision.gameObject, dmg, bullet_CritDamage)) return;
        if(enemy != null) AugmentManager.WeaponHitEvent(SideWeapon.TrackingGun);   // 종결 증강 게이지

        // 폭발 탄두: 맞힌 자리에 작은 폭발
        int wh = AugmentManager.WL("tk_warhead");
        if(wh > 0) AugmentManager.WeaponBlast(transform.position, WeaponBase.ClampDamage(dmg * (double)AugmentDefs.TkWarhead(wh)), 0.7f);

        // 사냥개 탄: 다음 적을 쫓아가요
        if(enemy != null && houndLeft > 0) {
            houndLeft--;
            bites++;
            // 흥분할수록 붉어져요 (5번 물면 가장 붉게)
            if(sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.25f), Mathf.Clamp01(bites / 5f));
            hitEnemies.Add(enemy);
            target = PickTarget();
            life = 0f;
            int pack = AugmentManager.WL("hd_pack");
            if(pack > 0 && !packSpawned && Random.value < AugmentDefs.HdPack(pack)) SpawnPackMissile();
            if(target != null) {
                SnapToTarget();
                RemasterFx.Burst(transform.position, 0.25f, new Color(1f, 0.75f, 0.35f));   // 튕긴 자리 표시
                return;
            }
        }
        DestroyBullet();
    }

    // 무리 사냥: 같은 자리에서 새 미사일 (이 미사일은 또 만들지 않아요)
    void SpawnPackMissile()
    {
        var b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Tracking).GetComponent<TrackingBullet>();
        b.isCritical = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        b.isStunning = false;
        b.transform.position = transform.position;
        b.transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, Random.Range(-90f, 90f));
        b.setBulletStats(bullet_Damage, missileSpeedScale, bullet_CritDamage);
        b.packSpawned = true;
        b.gameObject.SetActive(true);
        foreach(var e in hitEnemies) b.hitEnemies.Add(e);
    }
}
