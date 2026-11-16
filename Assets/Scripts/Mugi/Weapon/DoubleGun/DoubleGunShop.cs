using UnityEngine;

public class DoubleGunShop : WeaponShopBase
{
    public static DoubleGunShop Instance;
    [HideInInspector] public DoubleGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 0;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "두개의 총으로 몬스터를 공격한다";

    void Awake() => Instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        w.ExtraAttackSpeedScale = 0;
        if(Passive(0, 10)) w.ExtraAttackSpeedScale += 0.25f;
        if(Passive(1, 50)) w.ExtraDamagePercent += 0.5f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.005f);
        if(Passive(3, 200)) w.ExtraAttackSpeedScale += 0.3f;
        if(Passive(4, 300)) w.ExtraBulletSpeedScale = 0.75f;
        if(Passive(5, 500)) w.doubleAttack = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 5f;
    }
}
