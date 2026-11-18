using System.Collections.Generic;
using UnityEngine;

public class DoubleGunBullet : Bullet
{
    public long bullet_Damage;
    public float bullet_BulletSpeedScale;

    // 합체 "분열 쌍발": 맞히면 주변 적 2마리에게 갈라져요
    [HideInInspector] public int splitGen;       // 몇 번 갈라진 탄인지 (총에서 쏜 탄은 0)
    [HideInInspector] public float rootSpeedScale;   // 쌍발총이 처음 쏜 탄의 탄속 배율 (갈라진 탄 속도는 항상 이걸 기준으로)
    [HideInInspector] public Enemy ignore;       // 갈라진 자리의 적은 다시 맞히지 않아요

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Double;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.DoubleGun;
    protected override float OffScreenMargin => 1f;

    SpriteRenderer sr;
    static readonly Color SplitColor = new Color(0.45f, 1f, 1f);

    Rigidbody2D rb;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        StopPhysics();
        BeginMeasure();
    }

    // 이동은 코드로만 해요. 충돌에서 밀려난 물리 속도가 남으면 탄이 엄청 빨라지거나 방향이 틀어졌어요
    // (갈라진 탄은 적과 겹친 자리에서 생겨서 밀려나며 속도가 붙었고, 풀에서 다시 꺼낸 탄도 예전 속도가 남아 있었어요)
    void StopPhysics()
    {
        if(rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    // 갈라진 탄은 하늘색 (총에서 쏜 탄은 원래 색)
    public void SetSplitLook()
    {
        if(sr != null) sr.color = splitGen > 0 ? SplitColor : Color.white;
    }

    void Update()
    {
        StopPhysics();
        MoveForward(bullet_BulletSpeedScale);
        Measure();
        if(IsOffScreen()) DestroyBullet();
    }

    // ---- 속도 측정 (에디터, tuning.txt 의 debug_split_log = 1 일 때): 실제로 움직인 거리로 잰 속도를 콘솔에 찍어요
    Vector3 measureFrom;
    float measureAge;
    bool measured;
    static float nextLogTime;

    public void BeginMeasure()
    {
        measureFrom = transform.position;
        measureAge = 0f;
        measured = false;
    }

    void Measure()
    {
#if UNITY_EDITOR
        if(measured) return;
        measureAge += Time.deltaTime;
        if(measureAge < 0.3f) return;
        measured = true;
        if(Tuning.Get("debug_split_log", 0f) < 0.5f || Time.realtimeSinceStartup < nextLogTime) return;
        nextLogTime = Time.realtimeSinceStartup + 0.5f;
        float real = Vector3.Distance(measureFrom, transform.position) / measureAge;
        float calc = BulletSpeed * SpeedMultiplier * (1f + bullet_BulletSpeedScale);
        Debug.Log($"[분열 측정] {(splitGen == 0 ? "쌍발총 탄" : splitGen + "번 갈라진 탄")}: 실제 {real:0.0}칸/초 (계산 {calc:0.0}칸/초, 탄속 배율 {bullet_BulletSpeedScale:0.00}, 게임 속도 x{Time.timeScale:0.#})");
#endif
    }

    public void setBulletStats(long damage, float bulletSpeedScale)
    {
        bullet_Damage = damage;
        bullet_BulletSpeedScale = bulletSpeedScale;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(ignore != null && collision.gameObject == ignore.gameObject) return;
        if(!Hit(collision.gameObject, bullet_Damage, PlayerCritDamage)) return;
        collision.gameObject.TryGetComponent(out Enemy enemy);
        if(enemy != null) { Split(enemy); AugmentManager.WeaponHitEvent(SideWeapon.DoubleGun); }   // 종결 증강 게이지
        // 무한 분열: 가끔 사라지지 않고 계속 날아가요
        int endless = AugmentManager.WL("sd_endless");
        if(enemy != null && endless > 0 && AugmentManager.Fused(AugmentDefs.FzSplitDbl) && Random.value < AugmentDefs.SdEndless(endless)) {
            ignore = enemy;
            return;
        }
        DestroyBullet();
    }

    void Split(Enemy from)
    {
        if(!AugmentManager.Fused(AugmentDefs.FzSplitDbl)) return;
        int more = AugmentManager.WL("sd_more");
        int maxGen = 1 + (more > 0 ? AugmentDefs.SdMore(more) : 0);
        if(splitGen >= maxGen) return;
        int accel = AugmentManager.WL("sd_accel");
        // 갈라진 탄 1발 피해: 갈라지기 전 탄의 70% (또 갈라지면 70%의 70%), 튕김 가속이면 85%
        float dmgMul = accel > 0 ? Tuning.Get("split_damage_accel", 0.85f) : Tuning.Get("split_damage", 0.7f);
        long dmg = WeaponBase.ClampDamage(bullet_Damage * (double)dmgMul);
        // 갈라진 탄 속도 = 원래 탄과 같음 (tuning.txt split_bullet_speed). 튕김 가속이면 그만큼 빨라져요
        float SplitSpeed = Tuning.Get("split_bullet_speed", 1f);
        // 기준은 바로 앞 탄이 아니라 쌍발총이 처음 쏜 탄이에요.
        // (예전엔 앞 탄 기준이라 튕김 가속이 갈라질 때마다 또 곱해져서 1.7배 → 2.9배 → 4.9배 → 8배로 점점 빨라졌어요)
        float speed = (1f + rootSpeedScale) * SplitSpeed * (accel > 0 ? 1f + AugmentDefs.SdAccel(accel) : 1f) - 1f;
        Vector3 at = from.transform.position;
        RemasterFx.Burst(at, 0.25f, SplitColor);   // 갈라지는 자리 표시 (작게)

        // 목표: 조금 떨어진 적 2마리 (바로 붙은 적을 노리면 날아가는 게 안 보이고 순간이동처럼 보였어요)
        // 없으면 날아오던 방향 기준 좌우 V 자로 갈라져요
        float minDist = Tuning.Get("split_min_dist", 1.5f);
        var targets = AugmentManager.NearestInRange(at, 12, new HashSet<Enemy> { from });
        targets.RemoveAll(e => (e.transform.position - at).sqrMagnitude < minDist * minDist);
        float vAngle = Tuning.Get("split_v_angle", 30f);
        Vector3 heading = transform.up;
        for(int i = 0; i < 2; i++) {
            Vector3 dir = i < targets.Count
                ? (targets[i].transform.position - at)
                : Quaternion.Euler(0f, 0f, i == 0 ? vAngle : -vAngle) * heading;
            dir.z = 0f;
            if(dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
            dir.Normalize();
            var b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Double).GetComponent<DoubleGunBullet>();
            b.isCritical = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
            b.isStunning = false;
            b.transform.position = at + dir * 0.25f;
            b.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            b.setBulletStats(dmg, speed);
            b.splitGen = splitGen + 1;
            b.rootSpeedScale = rootSpeedScale;
            b.ignore = from;
            b.SetSplitLook();
            b.gameObject.SetActive(true);
            b.BeginMeasure();
            // (예전엔 여기서 0.8칸짜리 선을 순식간에 그렸는데, 이게 엄청 빠른 탄처럼 보였어요. 뺐어요)
        }
    }
}
