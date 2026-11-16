using UnityEngine;

public class TrackingGunShop : WeaponShopBase
{
    public static TrackingGunShop instance;
    [HideInInspector] public TrackingGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 1;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "몬스터를 추격하는 미사일";

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        w.missileSpeedScale = 0;
        if(Passive(0, 10)) w.missileSpeedScale += 0.5f;
        if(Passive(1, 50)) w.ExtraDamagePercent += 0.5f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.01f);
        if(Passive(3, 200)) w.missileSpeedScale += 0.5f;
        if(Passive(4, 300)) w.ExtraCritDamage = 0.75f;
        if(Passive(5, 500)) w.ExtraAttackSpeedScale = 0.4f;
        if(Passive(6, 750)) w.ExtraDamagePercent += 5f;
    }
}
