using UnityEngine;

public class TrackingBullet : Bullet
{
    Rigidbody2D rb;

    [Header("추적 미사일 전용")]
    public float RotationControl;

    [Header("#----Stats")]
    public int bullet_Damage;
    public float missileSpeedScale;
    public float bullet_CritDamage;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Tracking;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.TrackingMissile;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        Enemy target = EnemyManager.Instance.FindCloseEnemyWithoutRange();   // 예전엔 프레임마다 3번 찾았어요
        if(target != null) {
            Vector2 direction = ((Vector2)target.pos + (Vector2)target.transform.forward * 2) - rb.position;
            direction.Normalize();
            float rotateAmount = Vector3.Cross(direction, transform.up).z;
            rb.angularVelocity = -rotateAmount * RotationControl * 15;
            rb.linearVelocity = 50 * transform.up * BulletSpeed * SpeedMultiplier * (1 + missileSpeedScale) * Time.deltaTime;
        } else {
            transform.Translate(Vector3.up * 3f * Time.deltaTime);
        }

        if(IsOffScreen()) DestroyBullet();
    }

    public void setBulletStats(int a, float b, float c)
    {
        bullet_Damage = a;
        missileSpeedScale = b;
        bullet_CritDamage = c;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 더미는 예전엔 배율 없이 기본 피해만 받았어요 → 실제 탄 피해로 통일
        if(Hit(collision.gameObject, bullet_Damage, bullet_CritDamage))
            DestroyBullet();
    }
}
