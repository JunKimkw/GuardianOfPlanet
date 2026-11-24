using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonArea : MonoBehaviour
{
    public long poisonDamage;
    [HideInInspector] public float slowAmount;   // 합체 "역병 폭탄": 지대 위의 적 둔화 (0 = 없음)
    [HideInInspector] public int spreadLeft;   // 합체 "역병 확산": 사라질 때 이어 만들 작은 독 지대 수

    [HideInInspector]
    public bool hasScaled;

    Vector3 baseScale;
    bool baseScaleSaved;

    // 원래 크기 × 배율 (독 비행기 패시브 1.3배, 증강 "독구름 확장", 역병 폭탄)
    // 원래 크기는 Scaler(화면 폭 / Division) 기준이에요. Scaler 는 처음 켜질 때 한 번만 크기를 덮어써서,
    // 예전엔 처음 꺼낸 지대만 작고 두 번째부터 3~4배로 커졌어요. 이제 Scaler 를 끄고 여기서 직접 정해요.
    public void SetScaleMul(float mul)
    {
        if(!baseScaleSaved) {
            var sc = GetComponent<Scaler>();
            if(sc != null && sc.Division > 0f) {
                baseScale = Vector3.one * ScreenSize.GetScreenToWorldWidth / sc.Division;
                sc.enabled = false;
            } else {
                baseScale = transform.localScale;
            }
            baseScaleSaved = true;
        }
        transform.localScale = baseScale * mul;
        hasScaled = mul != 1f;
    }

    Rigidbody2D rb;

    // 독 지대는 제자리에 있어야 해요. 예전엔 물리 몸체(Dynamic)라 적과 겹치면 밀려나 화면을 떠다녔어요.
    // Kinematic 으로 바꾸고 충돌 알림(적이 위에 있는지)은 그대로 받아요
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if(rb != null && rb.bodyType == RigidbodyType2D.Dynamic) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }
    }

    // 독 무기 기본 특성 (2026-11-18): 맞는 적의 독 중첩 1당 피해 +1% (최대 30중첩 = +30%).
    // 독 비행기 독 지대, 역병 폭탄 폭발, 독안개 드론 탄에 붙어요. tuning.txt poison_stack_bonus
    public static long StackBonus(Enemy e, long damage)
    {
        if(e == null || e.PoisonStacks <= 0) return damage;
        return WeaponBase.ClampDamage(damage * (1.0 + Tuning.Get("poison_stack_bonus", 0.01f) * e.PoisonStacks));
    }

    public List<Enemy> enemyOnPoison;
    public List<Dummy> dummyOnPoison;

    public IEnumerator poisonAttack(Enemy enemy)
    {
        while(enemyOnPoison.Find(d => d.gameObject == enemy.gameObject) != null) {
            // 리마스터: 독 지대 피해 (증강 맹독·변이 배율, 전염 계열이면 독 상태)
            AugmentManager.CurrentHitKind = HitKind.PoisonArea;
            try { enemy.OnHit(StackBonus(enemy, poisonDamage), false, PoisonPlane.instance.Poison_canCrit ? Random.Range(1, 101) <= Player.instance.FinalCriticalChance : false, Player.instance.FinalCriticalDamage, Armor.Pen(TraitsData.WeaponType.Poison), SideWeapon.Poison); }
            finally { AugmentManager.CurrentHitKind = HitKind.Weapon; }
            if(slowAmount > 0f && enemy != null) enemy.AddSlow(slowAmount, 1f);   // 지대 위에 있는 동안 계속 느려요 (나오면 1초 뒤 풀려요)
            AugmentManager.WeaponHitEvent(SideWeapon.Poison);   // 종결 증강 게이지
            yield return new WaitForSeconds(PoisonPlane.instance.extraHit ? 0.35f : 0.7f);
        }
    }

    public IEnumerator poisonAttack(Dummy dummy)
    {
        while(dummyOnPoison.Find(d => d.gameObject == dummy.gameObject) != null) {
            dummy.addDamage(poisonDamage, PoisonPlane.instance.Poison_canCrit ? Random.Range(1, 101) <= Player.instance.FinalCriticalChance : false, Player.instance.FinalCriticalDamage);
            yield return new WaitForSeconds(PoisonPlane.instance.extraHit ? 0.35f : 0.7f);
        }
    }

    public void addEnemyToList(Enemy data)
    {
        if(enemyOnPoison.Find(d => d.gameObject == data.gameObject) == null) {
            enemyOnPoison.Add(data);
            StartCoroutine(poisonAttack(data));
        }
    }

    public void addDummyToList(Dummy dummy)
    {
        if(dummyOnPoison.Find(d => d.gameObject == dummy.gameObject) == null) {
            dummyOnPoison.Add(dummy);
            StartCoroutine(poisonAttack(dummy));
        }
    }


    public void OnEnable()
    {
        if(rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        // 풀에서 다시 꺼낼 때 예전 대상이 남아 있으면 그 적은 새 지대에서 안 맞아요 (2026-11-21 점검, 예방)
        if(enemyOnPoison != null) enemyOnPoison.Clear();
        if(dummyOnPoison != null) dummyOnPoison.Clear();
        if(!baseScaleSaved) SetScaleMul(1f);   // 배율 없이 꺼낸 지대도 같은 기준 크기로
        StartCoroutine(LifeTime());
    }

    public IEnumerator LifeTime()
    {
        // 무기 전용 "오래가는 독"
        int longer = AugmentManager.WL("ps_long");
        yield return new WaitForSeconds(6f + (longer > 0 ? AugmentDefs.PsLong(longer) : 0f));
        SpreadPlague();

        if(baseScaleSaved) transform.localScale = baseScale;
        hasScaled = false;

        ObjectPoolBullet.Instance.CoolObject(this.gameObject, PoolObjectTypeBullet.PoisonArea);
    }

    // 역병 확산: 가까운 적 자리에 작은 독 지대 (이어진 지대는 더 퍼지지 않아요)
    void SpreadPlague()
    {
        int n = spreadLeft;
        spreadLeft = 0;
        if(n <= 0) return;
        var near = AugmentManager.NearestInRange(transform.position, n);
        foreach(var e in near) {
            if((e.transform.position - transform.position).sqrMagnitude > 25f) continue;
            PoisonArea a = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.PoisonArea).GetComponent<PoisonArea>();
            a.poisonDamage = poisonDamage;
            a.spreadLeft = 0;
            a.slowAmount = slowAmount;
            a.transform.position = e.transform.position;
            a.SetScaleMul(0.6f * AugmentManager.PoisonAreaScale * PoisonBullet.PlagueAreaScale);
            a.gameObject.SetActive(true);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if(collision.gameObject.GetComponent<Enemy>() != null) {
            addEnemyToList(collision.gameObject.GetComponent<Enemy>());
        } else if(collision.gameObject.GetComponent<Dummy>() != null) {
            addDummyToList(collision.gameObject.GetComponent<Dummy>());
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if(collision.gameObject.GetComponent<Enemy>() != null) {
            enemyOnPoison.Remove(collision.gameObject.GetComponent<Enemy>());
        } else if(collision.gameObject.GetComponent<Dummy>() != null) {
            dummyOnPoison.Remove(collision.gameObject.GetComponent<Dummy>());
        }
    }
}
