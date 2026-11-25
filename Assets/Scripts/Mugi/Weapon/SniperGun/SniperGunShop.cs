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

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_sniper, tools/passive_balance.js)
    protected override string PassiveKey => "sniper";
    protected override float PassiveKDefault => 0.21f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "피해 +{d50}", "레벨마다 피해 +{per}", "총알이 적을 꿰뚫어요", "치명타 피해 +200%", "쏠 때마다 한 번 더 쏴요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.penetratingEffect = true;
        if(Passive(4, 300)) w.ExtraCritDamge = 2f;
        if(Passive(5, 500)) w.DoubleAttack = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
