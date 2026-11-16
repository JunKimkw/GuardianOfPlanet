using UnityEngine;

public class MainWeaponShop : WeaponShopBase
{
    public static MainWeaponShop Instance;
    [HideInInspector] public MainWeapon mainWeapon;

    public override WeaponBase Weapon => mainWeapon;
    protected override int UnlockPlanetIndex => -1;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "몬스터를 공격하는 기본 총";
    protected override int LowBase => 15;
    protected override int[] LowSlopes => new[] { 8, 9, 10, 10 };

    void Awake() => Instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = mainWeapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 50;
        if(Passive(1, 50)) w.ExtraDamagePercent += 1f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.015f);
        if(Passive(3, 200)) w.ExtraCritChance = 15f;
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;
        if(Passive(5, 500)) w.diamondPerHit = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 10f;
    }
}
