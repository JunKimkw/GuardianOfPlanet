using UnityEngine;

public class PoisonShop : WeaponShopBase
{
    public static PoisonShop instance;
    [HideInInspector] public PoisonPlane weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 3;
    protected override string StatLabel => "독가스 데미지의";
    protected override string DamageLabel => "초당 데미지";
    protected override string Intro => "독가스 발사 후 특정 구역 안\n몬스터에게 중독효과 부여";

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 25;
        if(Passive(1, 50)) w.extraBullet = true;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.005f);
        if(Passive(3, 200)) w.extraHit = true;
        if(Passive(4, 300)) w.extraSizeOfPoisonArea = true;
        if(Passive(5, 500)) w.Poison_canCrit = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 2.5f;
    }
}
