using UnityEngine;

public class strLazerShop : WeaponShopBase
{
    public static strLazerShop instance;
    [HideInInspector] public strLazer weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 8;
    protected override string StatLabel => "레이저 데미지의";
    protected override string Intro => "레이저로 적을 공격(관통)";

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 50;
        if(Passive(1, 50)) w.ExtraDamagePercent += 0.3f;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.005f);
        if(Passive(3, 200)) w.ExtraCritChance = 15f;  // 예전엔 레이저 치명타 계산에 안 들어갔어요
        if(Passive(4, 300)) w.ExtraCritDamage = 1f;
        if(Passive(5, 500)) w.longerLaserTime = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 2f;
    }
}
