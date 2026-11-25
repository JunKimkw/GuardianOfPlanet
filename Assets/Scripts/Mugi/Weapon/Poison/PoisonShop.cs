using UnityEngine;

public class PoisonShop : WeaponShopBase
{
    public static PoisonShop instance;
    [HideInInspector] public PoisonPlane weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 3;
    protected override string StatLabel => "독가스 데미지의";
    protected override string DamageLabel => "초당 데미지";
    protected override string Intro => "독가스 발사 후 특정 구역 안\n몬스터에게 중독효과 부여";

    void Awake() => instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_poison, tools/passive_balance.js)
    protected override string PassiveKey => "poison";
    protected override float PassiveKDefault => 0.33f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "독탄 1개 더 (3 → 4)", "레벨마다 피해 +{per}", "독 피해 간격 절반", "독 구역 30% 커져요", "독도 치명타가 터져요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) w.extraBullet = true;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.extraHit = true;
        if(Passive(4, 300)) w.extraSizeOfPoisonArea = true;
        if(Passive(5, 500)) w.Poison_canCrit = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
