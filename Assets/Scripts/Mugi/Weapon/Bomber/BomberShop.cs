using UnityEngine;

public class BomberShop : WeaponShopBase
{
    public static BomberShop instance;
    [HideInInspector] public Bomber bomber;

    public override WeaponBase Weapon => bomber;
    protected override int UnlockPlanetIndex => 2;
    protected override string StatLabel => "폭격기 데미지의";
    protected override string Intro => "폭탄으로 범위 공격을 하는 폭격기";

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = bomber;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 100;
        if(Passive(1, 50)) w.ExtraDamagePercent += 0.75f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.012f);
        if(Passive(3, 200)) w.ExploseRangeIncrease = true;
        if(Passive(4, 300)) w.ExtraAttackSpeedScale = 0.35f;
        if(Passive(5, 500)) w.ExtraDamagePercent += 3f;
        if(Passive(6, 750)) w.ExtraDamagePercent += 5f;
    }
}
