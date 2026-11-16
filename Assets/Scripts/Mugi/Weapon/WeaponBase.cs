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

    // 한 발 피해 = 플레이어 공격력 + (기본 + 추가) × 배율. 21억을 넘으면 int 가 넘쳐서 음수가 되던 문제를 막아요.
    public int HitDamage => ClampToInt(Player.instance.FinalAttack_Damage + (double)(BaseDamage + ExtraDamage) * DamageMultiplier);

    public static int ClampToInt(double v) => v >= int.MaxValue ? int.MaxValue : v <= 0 ? 0 : (int)v;

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
        return Mathf.Max(t, MaximumAttackSpeed);
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

    // 한 번 발사 (+ 증강 "다중 사격")
    protected virtual void Fire()
    {
        CreateBullet();
        if(AugmentManager.RollMultiShot()) CreateBullet();
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
public abstract class ContactWeapon : WeaponBase
{
    public List<Enemy> enemyOnLaser = new List<Enemy>();
    public List<Dummy> dummyOnLaser = new List<Dummy>();

    public float ExtraCritChance;
    public float ExtraCritDamage;

    protected abstract float HitInterval { get; }
    protected virtual bool RollHitStun() => false;

    IEnumerator HitEnemy(Enemy enemy)
    {
        while(enemyOnLaser.Contains(enemy) && enemy != null && enemy.gameObject.activeInHierarchy) {
            enemy.OnHit(HitDamage, RollHitStun(), RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage);
            yield return new WaitForSeconds(HitInterval);
        }
        enemyOnLaser.Remove(enemy);
    }

    IEnumerator HitDummy(Dummy dummy)
    {
        while(dummyOnLaser.Contains(dummy) && dummy != null) {
            dummy.addDamage(HitDamage, RollCrit(ExtraCritChance), Player.instance.FinalCriticalDamage + ExtraCritDamage);
            yield return new WaitForSeconds(HitInterval);
        }
    }

    public void addEnemyToList(Enemy e)
    {
        if(e == null || enemyOnLaser.Contains(e)) return;
        enemyOnLaser.Add(e);
        StartCoroutine(HitEnemy(e));
    }

    public void addDummyToList(Dummy d)
    {
        if(d == null || dummyOnLaser.Contains(d)) return;
        dummyOnLaser.Add(d);
        StartCoroutine(HitDummy(d));
    }

    // 레이저 범위(Player 레이어)에서 벗어난 적은 목록에서 빼요
    public void doubleCheckEnemyOnLazer()
    {
        int mask = LayerMask.GetMask("Player");
        enemyOnLaser.RemoveAll(e => e == null || Physics2D.OverlapPoint(e.transform.position, mask) == null);
    }

    protected void ClearTargets()
    {
        enemyOnLaser.Clear();
        dummyOnLaser.Clear();
    }

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
