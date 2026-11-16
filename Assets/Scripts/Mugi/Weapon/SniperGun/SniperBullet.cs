using UnityEngine;

public class SniperBullet : Bullet
{
    public long Bullet_Damage;
    public bool penetrateEffect;
    public float critDamage_Bullet;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Sniper;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.SniperGun;

    void Update()
    {
        MoveForward();
        if(IsOffScreen()) DestroyBullet();
    }

    public void setBulletStats(long damage, bool penetrate, float critDamage)
    {
        Bullet_Damage = damage;
        penetrateEffect = penetrate;
        critDamage_Bullet = critDamage;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(Hit(collision.gameObject, Bullet_Damage, PlayerCritDamage + critDamage_Bullet) && !penetrateEffect)
            DestroyBullet();
    }
}
