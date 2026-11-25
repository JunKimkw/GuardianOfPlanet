using UnityEngine;

public class BounceGunShop : WeaponShopBase
{
    public static BounceGunShop Instance;
    [HideInInspector] public BounceGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 4;
    protected override string StatLabel => "추적 우주선 데미지의";
    protected override string Intro => "적을 추적하고 튕기는 총알을\n발사하는 우주선";

    void Awake() => Instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_bounce, tools/passive_balance.js)
    protected override string PassiveKey => "bounce";
    protected override float PassiveKDefault => 0.41f;
    protected override string[] PassiveDescs => new[] { "피해 +{d10}", "총알 1개 더", "레벨마다 피해 +{per}", "장전 시간 25% 감소", "튕기는 횟수 +1", "튕겨도 약해지지 않아요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        if(Passive(0, 10)) d += PvLv10;
        if(Passive(1, 50)) w.ExtraBullet = true;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraAttackSpeedScale = 0.25f;
        if(Passive(4, 300)) w.ExtraBounce = true;
        if(Passive(5, 500)) w.NoDamageReduce = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
