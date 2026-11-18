using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 모든 무기의 공통 부모.
// 필드 이름(id, UpgradeLevel, ExtraDamage, ExtraDamagePercent ...)은 예전 그대로라서 프리팹·저장 데이터가 그대로 이어져요.
public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField] public string id;

    [ContextMenu("Generate guid for id")]
    void GenerateGuid() => id = System.Guid.NewGuid().ToString();

    [HideInInspector] public int UpgradeLevel;

    [Header("#-----Passive Stats")]
    public int ExtraDamage;
    public float ExtraDamagePercent;

    // 증강 시스템에서 이 무기를 가리키는 종류 (기본총은 None)
    public virtual SideWeapon Side => SideWeapon.None;

    // 특성(Traits)에서 이 무기를 가리키는 종류
    protected abstract TraitsData.WeaponType TraitType { get; }

    // 레벨로 정해지는 기본 피해 (무기마다 공식이 달라요)
    public abstract int BaseDamage { get; }
    public abstract void SetDamage();

    public void UpgradeWeapon()
    {
        UpgradeLevel++;
        SetDamage();
    }

    // 조준/발사 (무기마다 필요한 것만 덮어써요)
    public virtual void NormalShooting() { }
    public virtual void AutoShooting() { }
    public virtual void DonShoot() { }

    // ---- 공통 계산
    protected float Trait(TraitsData.TraitsType t) => TraitsManager.instance.GetStats(TraitType, t);

    // (1 + 패시브 % + 특성 %)
    public float DamageMultiplier => 1f + ExtraDamagePercent + Trait(TraitsData.TraitsType.DamagePercent);

    // 한 발 피해 = 플레이어 공격력 + (기본 + 추가) × 배율. long 이라 후반(수십억 이상)에도 넘치지 않아요.
    // 증강: 무기 합체 위력(×3~×5)과 무기 전용 증강 보너스 (AugmentManager.WeaponPower)
    public long HitDamage => ClampDamage((Player.instance.FinalAttack_Damage + (double)(BaseDamage + ExtraDamage) * DamageMultiplier) * AugmentManager.WeaponPower(Side));

    public static long ClampDamage(double v) => v >= 9e18 ? (long)9e18 : v <= 0 ? 0 : (long)v;

    // 1~100 주사위 (예전 코드 일부는 0~100 이라 확률이 1% 더 높았어요)
    protected static bool Roll(float percent) => Random.Range(1, 101) <= percent;
    protected static bool RollCrit(float bonus = 0f) => Roll(Player.instance.FinalCriticalChance + bonus);
    protected static bool RollStun(float bonus = 0f) => Roll(Player.instance.FinalStunPower + bonus);

    // 레벨 구간 공식: base + Σ perLevel × max(0, 레벨 - from)
    protected static int Tiered(int level, int baseValue, params (int from, int perLevel)[] tiers)
    {
        long v = baseValue;
        foreach(var t in tiers)
            if(level >= t.from) v += (long)(level - t.from) * t.perLevel;
        return (int)System.Math.Min(int.MaxValue, v);
    }

    // ---- 조준
    protected static Vector3 MouseWorld => GameManager.instance.mainCamera.ScreenToWorldPoint(Input.mousePosition);

    protected static Quaternion LookRotation(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
    }

    protected void Face(Vector3 target) => transform.rotation = LookRotation(transform.position, target);
}

// 장전 시간마다 탄을 쏘는 무기 (기본총, 쌍발총, 폭격기, 유도탄, 저격총, 튕김총)
public abstract class GunWeapon : WeaponBase
{
    [Header("#-----Reload")]
    public float MaximumAttackSpeed;
    public float RelodingSpeed;
    public float ExtraAttackSpeedScale;

    protected bool IsShooting;
    protected float TimerAs;

    // 장전 시간(초). 최소값은 MaximumAttackSpeed
    public virtual float getAttackSpeed()
    {
        float t = RelodingSpeed * (1 - Trait(TraitsData.TraitsType.AttackSpeed)) * (1 - Player.instance.FinalAttack_SpeedPercent) * (1 - ExtraAttackSpeedScale);
        return Mathf.Max(t, MaximumAttackSpeed) / AugmentManager.AttackSpeedFactor;   // 증강: 시간 가속, 무거운 사슬, 강철 장갑
    }

    protected virtual void Update()
    {
        if(!IsShooting) return;
        TimerAs += Time.deltaTime;
        if(TimerAs >= getAttackSpeed()) {
            TimerAs = 0;
            Fire();
        }
    }

    // 한 번 발사 (+ 증강 "다중 사격", "쌍둥이탄", 증식 세트 6)
    protected virtual void Fire()
    {
        CreateBullet();
        for(int i = AugmentManager.ExtraShots(); i > 0; i--) CreateBullet();
    }

    public abstract void CreateBullet();

    public override void NormalShooting()
    {
        Face(MouseWorld);
        IsShooting = true;
    }

    public override void AutoShooting()
    {
        Enemy target = EnemyManager.Instance.FindCloseEnemy();
        Face(target != null ? target.pos : Vector3.zero);
        IsShooting = target != null;
    }

    public override void DonShoot()
    {
        transform.rotation = Quaternion.identity;
        IsShooting = false;
    }

    // 풀에서 탄을 꺼내 위치·방향·치명/스턴을 세팅 (활성화는 호출한 쪽에서)
    protected T TakeBullet<T>(PoolObjectTypeBullet type, Vector3 pos, Quaternion rot, float critBonus = 0f) where T : Bullet
    {
        T b = ObjectPoolBullet.Instance.GetPoolObject(type).GetComponent<T>();
        b.isCritical = RollCrit(critBonus);
        b.isStunning = RollStun();
        b.transform.position = pos;
        b.transform.rotation = rot;
        return b;
    }
}

// 닿아 있는 동안 일정 간격으로 피해를 주는 무기 (360 레이저, 직선 레이저, 궤도)
// 리마스터: 예전엔 적마다 코루틴을 돌렸는데, 적이 판정에서 잠깐 빠졌다 들어오면 코루틴이 하나 더 생겨
// 공격 속도가 2배·3배로 불어났어요. 이제 대상마다 "다음에 때릴 시각"만 기억하고 TickContacts 에서 한 번에 처리해요.
public abstract class ContactWeapon : WeaponBase
{
    public List<Enemy> enemyOnLaser = new List<Enemy>();
    public List<Dummy> dummyOnLaser = new List<Dummy>();

    public float ExtraCritChance;
    public float ExtraCritDamage;

    protected abstract float HitInterval { get; }
    protected virtual bool RollHitStun() => false;
    protected virtual double ContactDamageMul => 1;   // 직선 레이저 "관통 증폭"

    readonly Dictionary<Object, float> nextHit = new Dictionary<Object, float>();
    readonly List<Enemy> enemyBuffer = new List<Enemy>();
    readonly List<Dummy> dummyBuffer = new List<Dummy>();

    // 닿아 있는 대상마다 HitInterval 간격으로 한 번씩 때려요 (무기의 Update 에서 불러요)
    protected void TickContacts()
    {
        enemyOnLaser.RemoveAll(e => e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy);
        dummyOnLaser.RemoveAll(d => d == null);
        float now = Time.time;

        enemyBuffer.Clear();
        enemyBuffer.AddRange(enemyOnLaser);   // 때리는 중에 목록이 바뀌어도 안전하게
        foreach(var e in enemyBuffer) {
            if(e == null || !Due(e, now)) continue;
            AugmentManager.CurrentHitKind = HitKind.Laser;   // 증강 "굴절"
            try { e.OnHit(ClampDamage(HitDamage * ContactDamageMul), RollHitStun(), RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage); }
            finally { AugmentManager.CurrentHitKind = HitKind.Weapon; }
        }

        dummyBuffer.Clear();
        dummyBuffer.AddRange(dummyOnLaser);
        foreach(var d in dummyBuffer)
            if(d != null && Due(d, now))
                d.addDamage(HitDamage, RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage);
    }

    // 때릴 차례면 다음 시각을 정하고 true (판정에서 나갔다 들어와도 간격은 그대로 지켜요)
    bool Due(Object target, float now)
    {
        if(nextHit.TryGetValue(target, out float t) && now < t) return false;
        nextHit[target] = now + HitInterval;
        return true;
    }

    public void addEnemyToList(Enemy e)
    {
        if(e != null && !enemyOnLaser.Contains(e)) enemyOnLaser.Add(e);
    }

    public void addDummyToList(Dummy d)
    {
        if(d != null && !dummyOnLaser.Contains(d)) dummyOnLaser.Add(d);
    }

    // 레이저 범위(Player 레이어)에서 벗어난 적은 목록에서 빼요
    public void doubleCheckEnemyOnLazer()
    {
        int mask = LayerMask.GetMask("Player");
        enemyOnLaser.RemoveAll(e => e == null || Physics2D.OverlapPoint(e.transform.position, mask) == null);
    }

    // 원 안에 조금이라도 닿은 적·더미를 HitInterval 간격으로 때려요 (궤도처럼 판정이 작은 무기용).
    // 적의 "중심"이 판정 안에 있어야 하는 레이저용 검사(doubleCheckEnemyOnLazer)를 안 써요.
    // timers: 판정 원마다 따로 (본체·위성). 맞힌 수를 돌려줘요
    protected int HitInCircle(Vector2 center, float radius, Dictionary<Object, float> timers)
    {
        int hits = 0;
        float now = Time.time;
        if(timers.Count > 300) {   // 죽은 적 기록 정리
            var stale = new List<Object>();
            foreach(var kv in timers) if(kv.Key == null || kv.Value < now) stale.Add(kv.Key);
            foreach(var k in stale) timers.Remove(k);
        }
        foreach(var c in Physics2D.OverlapCircleAll(center, radius)) {
            if(c == null) continue;
            if(c.TryGetComponent(out Enemy e)) {
                if(e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) continue;
                if(timers.TryGetValue(e, out float t) && now < t) continue;
                timers[e] = now + HitInterval;
                AugmentManager.CurrentHitKind = HitKind.Laser;
                try { e.OnHit(ClampDamage(HitDamage * ContactDamageMul), RollHitStun(), RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage); }
                finally { AugmentManager.CurrentHitKind = HitKind.Weapon; }
                hits++;
                OnCircleHit(e, center);
            } else if(c.TryGetComponent(out Dummy d)) {
                if(timers.TryGetValue(d, out float t) && now < t) continue;
                timers[d] = now + HitInterval;
                d.addDamage(HitDamage, RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage);
                hits++;
            }
        }
        return hits;
    }

    protected virtual void OnCircleHit(Enemy e, Vector2 center) { }

    protected void ClearTargets()
    {
        enemyOnLaser.Clear();
        dummyOnLaser.Clear();
        nextHit.Clear();
    }

    protected virtual void OnDisable() => ClearTargets();

    void OnCollisionStay2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent(out Enemy e)) addEnemyToList(e);
        else if(collision.gameObject.TryGetComponent(out Dummy d)) addDummyToList(d);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent(out Enemy e)) enemyOnLaser.Remove(e);
        else if(collision.gameObject.TryGetComponent(out Dummy d)) dummyOnLaser.Remove(d);
    }
}
