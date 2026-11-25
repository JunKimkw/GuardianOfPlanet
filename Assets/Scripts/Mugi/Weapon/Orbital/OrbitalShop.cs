using UnityEngine;

public class OrbitalShop : WeaponShopBase
{
    public static OrbitalShop Instance;
    [HideInInspector] public Orbital weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 7;
    protected override string StatLabel => "드론 데미지의";
    protected override string Intro => "우주선 주변을 돌며 몬스터를\n공격하는 설치물";

    void Awake() => Instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_drone, tools/passive_balance.js)
    protected override string PassiveKey => "drone";
    protected override float PassiveKDefault => 0.65f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "도는 반경 20% 증가", "레벨마다 피해 +{per}", "기절 확률 +15%", "공격 속도 30% 증가", "드론 크기 2배", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) w.extraRange = true;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraStunPercent = 15f;   // 예전 0.15 는 % 단위라 사실상 +0.15%였어요
        if(Passive(4, 300)) w.extraSpeed = true;
        if(Passive(5, 500) && !w.hasScaled) {
            w.transform.localScale *= 2f;
            w.hasScaled = true;
        }
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
