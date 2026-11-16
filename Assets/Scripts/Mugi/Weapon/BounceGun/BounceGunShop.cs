using UnityEngine;

public class BounceGunShop : WeaponShopBase
{
    public static BounceGunShop Instance;
    [HideInInspector] public BounceGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 4;
    protected override string StatLabel => "추적 우주선 데미지의";
    protected override string Intro => "적을 추적하고 튕기는 총알을\n발사하는 우주선";

    void Awake() => Instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 30;
        if(Passive(1, 50)) w.ExtraBullet = true;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.01f);
        if(Passive(3, 200)) w.ExtraAttackSpeedScale = 0.25f;
        if(Passive(4, 300)) w.ExtraBounce = true;
        if(Passive(5, 500)) w.NoDamageReduce = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 3.75f;
    }
}
