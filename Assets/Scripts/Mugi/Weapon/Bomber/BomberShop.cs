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

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_bomber, tools/passive_balance.js)
    protected override string PassiveKey => "bomber";
    protected override float PassiveKDefault => 0.66f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "피해 +{d50}", "레벨마다 피해 +{per}", "폭발 범위 50% 증가", "장전 시간 35% 감소", "피해 +{mid}", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = bomber;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExploseRangeIncrease = true;
        if(Passive(4, 300)) w.ExtraAttackSpeedScale = 0.35f;
        if(Passive(5, 500)) d += PvMid;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
