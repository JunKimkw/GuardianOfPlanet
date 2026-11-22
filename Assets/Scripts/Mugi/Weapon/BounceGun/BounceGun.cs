using System.Collections.Generic;
using UnityEngine;

// 튕김총: 가장 가까운 적을 노리고, 맞히면 다음 적들로 튕겨요
public class BounceGun : GunWeapon
{
    public static BounceGun instance;

    [HideInInspector] public int Damage;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    public bool ExtraBullet;
    public bool ExtraBounce;
    public bool NoDamageReduce;

    public List<BounceEnemy> bounceEnemyList_Bullet = new List<BounceEnemy>();

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.BounceGun;
    public override SideWeapon Side => SideWeapon.BounceGun;
    public override int BaseDamage => Balanced("bounce", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Balanced("bounce", 25f);

    // 한 번 발사하는 탄들은 "이미 맞힌 적" 목록을 같이 써요
    protected override void Fire()
    {
        // 혜성탄 (튕김총 진화): 튕김탄 대신 혜성 1발. 탄 추가는 튕김 횟수로 바뀌어요 (CometShot.MaxHits)
        if(AugmentManager.Fused(AugmentDefs.FzConstellation)) {
            CometShot.Fire(this, ShootingPoint.position);
            return;
        }
        var be = new BounceEnemy();
        CreateBullet(be);
        if(ExtraBullet) CreateBullet(be);
        for(int i = AugmentManager.ExtraShots(); i > 0; i--) CreateBullet(be);
    }

    public override void CreateBullet() => CreateBullet(new BounceEnemy());

    public void CreateBullet(BounceEnemy be)
    {
        var b = TakeBullet<BounceBullet>(PoolObjectTypeBullet.BounceBullet, ShootingPoint.position, transform.rotation);
        b.setBulletStats(HitDamage);
        b.bounceEnemy = be;
        b.BounceTime = 0;
        b.AimAtNext(transform.rotation);
        if(b.TargetEnemy != null) bounceEnemyList_Bullet.Add(be);
        b.gameObject.SetActive(true);
        BounceBullet.ApplyScale(b.transform);
    }
}

[System.Serializable]
public class BounceEnemy
{
    public List<Enemy> BouncedEnemies = new List<Enemy>();

    public bool checkBouncedEnemyisInList(Enemy enemy) => BouncedEnemies.Contains(enemy);
}
