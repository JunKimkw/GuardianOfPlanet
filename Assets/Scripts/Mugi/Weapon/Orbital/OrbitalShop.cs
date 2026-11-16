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

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 150;
        if(Passive(1, 50)) w.extraRange = true;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.0075f);
        if(Passive(3, 200)) w.ExtraStunPercent = 15f;   // 예전 0.15 는 % 단위라 사실상 +0.15%였어요
        if(Passive(4, 300)) w.extraSpeed = true;
        if(Passive(5, 500) && !w.hasScaled) {
            w.transform.localScale *= 2f;
            w.hasScaled = true;
        }
        if(Passive(6, 750)) w.ExtraDamagePercent += 4f;
    }
}
