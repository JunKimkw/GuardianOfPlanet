using UnityEngine;

// 튕김탄: 목표 적을 맞히면(또는 목표가 먼저 죽으면) 다음 적들로 갈라져 튕겨요.
public class BounceBullet : Bullet
{
    public BounceEnemy bounceEnemy;
    public int BounceTime;
    public long bullet_Damage;
    public Enemy TargetEnemy;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.BounceBullet;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.BounceGun;

    void Update()
    {
        MoveForward();

        if(IsOffScreen()) {
            Finish();
            return;
        }

        if(TargetEnemy != null && TargetEnemy._state == Enemy.EnemyState.dead)
            HandleBounce();
    }

    // 튕김 처리 (리마스터: 증식 튕김 증강으로 추가 튕김. 추가분은 1발씩만 이어져서 탄이 폭증하지 않아요)
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

    public void setBulletStats(long damage) => bullet_Damage = damage;

    public void CreateBullet()
    {
        BounceBullet b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.BounceBullet).GetComponent<BounceBullet>();

        if(AugmentManager.Instance != null && AugmentManager.Instance.BounceExtraCount > 0)
            b.setBulletStats((long)System.Math.Min(9e18, bullet_Damage * 1.2));
        else if(BounceGun.instance.NoDamageReduce)
            b.setBulletStats(bullet_Damage);
        else
            b.setBulletStats((long)(bullet_Damage * System.Math.Pow(0.9, BounceTime)));

        b.isCritical = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        b.isStunning = Random.Range(1, 101) <= Player.instance.FinalStunPower;
        b.transform.position = transform.position;
        b.bounceEnemy = bounceEnemy;
        b.BounceTime = BounceTime;
        b.AimAtNext(transform.rotation);
        b.gameObject.SetActive(true);
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
            enemy.OnHit(bullet_Damage, isStunning, isCritical, PlayerCritDamage);
            bounceEnemy.BouncedEnemies.Add(enemy);
            HandleBounce();
        } else if(collision.TryGetComponent(out Dummy dummy)) {
            dummy.addDamage(bullet_Damage, isCritical, PlayerCritDamage);
        }
    }
}
