using UnityEngine;

public class LazerShop : WeaponShopBase
{
    public static LazerShop Instance;
    [HideInInspector] public Lazer weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 6;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "360도 레이저로 적을 공격(관통)";

    void Awake() => Instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_laser, tools/passive_balance.js)
    protected override string PassiveKey => "laser";
    protected override float PassiveKDefault => 0.32f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "피해 +{d50}", "레벨마다 피해 +{per}", "타격 간격 절반", "치명타 피해 +100%", "두 바퀴 돌아요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraHit = true;
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;   // 예전엔 레이저 피해 계산에 안 들어갔어요
        if(Passive(5, 500)) w.SpinTwoTimes = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
