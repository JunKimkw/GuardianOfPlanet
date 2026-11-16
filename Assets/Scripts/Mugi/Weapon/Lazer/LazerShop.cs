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

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 100;
        if(Passive(1, 50)) w.ExtraDamagePercent += 0.5f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.01f);
        if(Passive(3, 200)) w.ExtraHit = true;
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;   // 예전엔 레이저 피해 계산에 안 들어갔어요
        if(Passive(5, 500)) w.SpinTwoTimes = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 4f;
    }
}
