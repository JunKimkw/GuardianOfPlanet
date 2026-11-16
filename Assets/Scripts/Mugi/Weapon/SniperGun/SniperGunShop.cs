using UnityEngine;

public class SniperGunShop : WeaponShopBase
{
    public static SniperGunShop instance;
    [HideInInspector] public SniperGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 5;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "적을 타격하는 강력한 저격총";

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 100;
        if(Passive(1, 50)) w.ExtraDamagePercent += 1f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.01f);
        if(Passive(3, 200)) w.penetratingEffect = true;
        if(Passive(4, 300)) w.ExtraCritDamge = 2f;
        if(Passive(5, 500)) w.DoubleAttack = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 5f;
    }
}
