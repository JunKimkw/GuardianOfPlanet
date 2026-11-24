using UnityEngine;

// 튕김탄: 목표 적을 맞히면(또는 목표가 먼저 죽으면) 다음 적들로 갈라져 튕겨요.
public class BounceBullet : Bullet
{
    public BounceEnemy bounceEnemy;
    public int BounceTime;
    public long bullet_Damage;
    public long rootDamage;   // 처음 쏜 탄의 피해 (튕긴 탄도 이어받아요)
    public Enemy TargetEnemy;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.BounceBullet;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.BounceGun;

    void Update()
    {
        // 탄성 가속: 튈 때마다 탄속 +
        int acc = AugmentManager.WL("bn_accel");
        MoveForward(acc > 0 ? AugmentDefs.BnAccel(acc) * BounceTime : 0f);

        if(IsOffScreen()) {
            Finish();
            return;
        }

        if(TargetEnemy != null && TargetEnemy._state == Enemy.EnemyState.dead)
            HandleBounce();
    }

    // 튕김 처리 (리마스터: 튕김 강화 증강으로 추가 튕김. 추가분은 1발씩만 이어져서 탄이 폭증하지 않아요)
    void HandleBounce()
    {
        BounceTime++;
        int baseMax = BounceGun.instance.ExtraBounce ? 3 : 2;
        int extra = AugmentManager.Instance != null ? AugmentManager.Instance.BounceExtraCount : 0;
        if(BounceTime < baseMax + extra) {
            int count = BounceTime >= baseMax ? 1 : (BounceTime == 1 ? 2 : 3);
            for(int i = 0; i < count; i++)
                CreateBullet();
            DestroyBullet();
        } else {
            Finish();
        }
    }

    void Finish()
    {
        BounceGun.instance.bounceEnemyList_Bullet.Remove(bounceEnemy);
        DestroyBullet();
    }

    public override void DestroyBullet()
    {
        TargetEnemy = null;   // 풀에서 다시 꺼낼 때 예전 목표가 남아 있으면 바로 튕겨 버리던 문제
        base.DestroyBullet();
    }

    public void setBulletStats(long damage) { bullet_Damage = damage; rootDamage = damage; }

    public void CreateBullet()
    {
        BounceBullet b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.BounceBullet).GetComponent<BounceBullet>();

        // 증식 튕김·탄성 가속: 튈 때마다 처음 피해의 +% 를 더해요 (2026-11-22 증강 하나씩 측정 → 조정안 v3 (탄 개수는 그대로, 피해만))
        //  예전엔 증식 튕김이 앞 탄 피해에 곱해져서 튕김 강화와 같이 받으면 ×1.4^n 으로 불어났어요 (측정: 하나씩 ×1.8·×1.2 인데 같이 ×7.5)
        int grow = AugmentManager.WL("bouncegrow"), acc = AugmentManager.WL("bn_accel");
        long root = rootDamage > 0 ? rootDamage : bullet_Damage;
        if(grow > 0 || acc > 0) {
            double baseMul = grow > 0 || BounceGun.instance.NoDamageReduce ? 1.0 : System.Math.Pow(0.9, BounceTime);
            double per = (grow > 0 ? AugmentDefs.BounceGrowDamage(grow) : 0f) + (acc > 0 ? AugmentDefs.BnAccelDamage(acc) : 0f);
            b.setBulletStats(WeaponBase.ClampDamage(root * (baseMul + per * BounceTime)));
        }
        else if(BounceGun.instance.NoDamageReduce)
            b.setBulletStats(bullet_Damage);
        else
            b.setBulletStats((long)(bullet_Damage * System.Math.Pow(0.9, BounceTime)));

        b.rootDamage = root;
        b.isCritical = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        b.isStunning = Random.Range(1, 101) <= Player.instance.FinalStunPower;
        b.transform.position = transform.position;
        b.bounceEnemy = bounceEnemy;
        b.BounceTime = BounceTime;
        b.AimAtNext(transform.rotation);
        b.gameObject.SetActive(true);
        ApplyScale(b.transform);
    }

    // 첫 탄과 튕긴 탄 크기를 똑같이 (예전엔 첫 탄 0.2, 튕긴 탄은 프리팹 크기 0.5였어요) + 증강 탄 크기
    public static void ApplyScale(Transform t)
    {
        float mult = AugmentManager.Instance != null ? AugmentManager.Instance.BulletScaleMultiplier : 1f;
        t.localScale = Vector3.one * (0.2f * mult);
    }

    // 아직 안 맞힌 가장 가까운 적을 향해요 (없으면 그대로 직진)
    public void AimAtNext(Quaternion fallback)
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy(bounceEnemy);
        TargetEnemy = e;
        if(e != null) {
            Vector3 d = e.pos - transform.position;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        } else {
            transform.rotation = fallback;
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out Enemy enemy)) {
            if(enemy != TargetEnemy) return;
            enemy.OnHit(bullet_Damage, isStunning, isCritical, PlayerCritDamage, Armor.Pen(TraitType), SideWeapon.BounceGun);
            bounceEnemy.BouncedEnemies.Add(enemy);
            HandleBounce();
        } else if(collision.TryGetComponent(out Dummy dummy)) {
            dummy.addDamage(bullet_Damage, isCritical, PlayerCritDamage);
        }
    }
}
