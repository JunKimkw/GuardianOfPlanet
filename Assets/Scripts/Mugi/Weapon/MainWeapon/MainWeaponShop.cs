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

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_main, tools/passive_balance.js)
    protected override string PassiveKey => "main";
    protected override float PassiveKDefault => 1f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "피해 +{d50}", "레벨마다 피해 +{per}", "치명타 확률 +15%", "치명타 피해 +100%", "맞힐 때 가끔 다이아를 얻어요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = mainWeapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraCritChance = 15f;
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;
        if(Passive(5, 500)) w.diamondPerHit = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
