using UnityEngine;

public class DoubleGunBullet : Bullet
{
    public int bullet_Damage;
    public float bullet_BulletSpeedScale;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Double;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.DoubleGun;
    protected override float OffScreenMargin => 1f;

    void Update()
    {
        MoveForward(bullet_BulletSpeedScale);
        if(IsOffScreen()) DestroyBullet();
    }

    public void setBulletStats(int damage, float bulletSpeedScale)
    {
        bullet_Damage = damage;
        bullet_BulletSpeedScale = bulletSpeedScale;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(Hit(collision.gameObject, bullet_Damage, PlayerCritDamage))
            DestroyBullet();
    }
}
