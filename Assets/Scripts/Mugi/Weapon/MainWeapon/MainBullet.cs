using UnityEngine;

public class MainBullet : Bullet
{
    public Sprite DiamondSprite;

    public long Bullet_Damage;
    public float Bullet_CritDamage;
    [HideInInspector] public int pierceLeft;   // 기본총 룰렛 "관통" (2026-11-25): 더 뚫을 수 있는 적 수

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Main;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.MainWeapon;
    protected override float OffScreenMargin => 1f;

    void Update()
    {
        MoveForward();
        if(IsOffScreen()) DestroyBullet();
    }

    public void setBulletStats(long damage, float critDamage)
    {
        Bullet_Damage = damage;
        Bullet_CritDamage = critDamage;
    }

    // 패시브(Lv.500): 적중마다 0.05% 확률로 다이아 1개
    public void getDiamondRandomNumber(Transform loc)
    {
        if(Random.Range(0f, 100f) < 0.05f) {
            MaterialPopUp.Create(loc.position, DiamondSprite, 1);
            GameManager.SetDiamond(1);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(MainWeapon.instance.diamondPerHit && collision.gameObject.TryGetComponent(out Enemy enemy))
            getDiamondRandomNumber(enemy.transform);
        if(Hit(collision.gameObject, Bullet_Damage, Bullet_CritDamage)) {
            if(pierceLeft > 0) { pierceLeft--; return; }
            DestroyBullet();
        }
    }
}
