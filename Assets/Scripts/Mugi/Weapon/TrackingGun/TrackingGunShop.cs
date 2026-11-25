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

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_track, tools/passive_balance.js)
    protected override string PassiveKey => "track";
    protected override float PassiveKDefault => 0.67f;
    protected override string[] PassiveDescs => new[] { "미사일 속도 50% 증가", "피해 +{d50}", "레벨마다 피해 +{per}", "미사일 속도 50% 더 증가", "치명타 피해 +75%", "장전 시간 40% 감소", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        w.missileSpeedScale = 0;
        if(Passive(0, 10)) w.missileSpeedScale += 0.5f;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.missileSpeedScale += 0.5f;
        if(Passive(4, 300)) w.ExtraCritDamage = 0.75f;
        if(Passive(5, 500)) w.ExtraAttackSpeedScale = 0.4f;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
