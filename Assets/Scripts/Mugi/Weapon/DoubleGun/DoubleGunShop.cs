using UnityEngine;

public class DoubleGunShop : WeaponShopBase
{
    public static DoubleGunShop Instance;
    [HideInInspector] public DoubleGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 0;
    protected override string StatLabel => "우주선 데미지의";
    protected override string Intro => "두개의 총으로 몬스터를 공격한다";

    void Awake() => Instance = this;

    // 패시브 (2026-11-26 밸런스: 피해 % 는 모든 무기가 같은 길 × passive_k_double, tools/passive_balance.js)
    protected override string PassiveKey => "double";
    protected override float PassiveKDefault => 0.34f;
    protected override string[] PassiveDescs => new[] { "장전 시간 25% 감소", "피해 +{d50}", "레벨마다 피해 +{per}", "장전 시간 30% 더 감소", "총알 속도 75% 증가", "쏠 때마다 한 번 더 쏴요", "피해 +{d750}" };
    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamage = 0;
        float d = 0f;
        w.ExtraAttackSpeedScale = 0;
        if(Passive(0, 10)) w.ExtraAttackSpeedScale += 0.25f;
        if(Passive(1, 50)) d += PvLv50;
        if(Passive(2, 100)) d += PerLevelFrom100(lv, PvPerLv);
        if(Passive(3, 200)) w.ExtraAttackSpeedScale += 0.3f;
        if(Passive(4, 300)) w.ExtraBulletSpeedScale = 0.75f;
        if(Passive(5, 500)) w.doubleAttack = true;
        if(Passive(6, 750)) d += PvLv750;
        w.ExtraDamagePercent = d * PK;
    }
}
