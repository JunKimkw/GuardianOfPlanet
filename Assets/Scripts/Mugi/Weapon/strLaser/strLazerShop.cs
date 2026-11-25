using UnityEngine;

public class strLazerShop : WeaponShopBase
{
    public static strLazerShop instance;
    [HideInInspector] public strLazer weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 8;
    protected override string StatLabel => "블랙홀 데미지의";
    protected override string Intro => "적을 빨아들이는 블랙홀";

    void Awake() => instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_blackhole, tools/passive_balance.js)
    protected override string PassiveKey => "blackhole";
    protected override float PassiveKDefault => 0.69f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "피해 +{d50}", "레벨마다 피해 +{per}", "치명타 확률 +15%", "치명타 피해 +100%", "블랙홀이 1.5초 더 남아요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraCritChance = 15f;  // 예전엔 레이저 치명타 계산에 안 들어갔어요
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;
        if(Passive(5, 500)) w.longerLaserTime = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
