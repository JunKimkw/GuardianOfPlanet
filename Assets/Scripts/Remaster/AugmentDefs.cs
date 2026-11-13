using System.Collections.Generic;
using UnityEngine;

// 증강 시스템 데이터 정의 (설계서 15장 MVP 20개)
public enum AugTag { Chain, Curse, Split, Treasure }
public enum AugGrade { Silver = 0, Gold = 1, Prism = 2 }

// 출격 가능한 보조 무기 (기본총 제외)
public enum SideWeapon { None = -1, DoubleGun = 0, Bomber, TrackingGun, Poison, Orbital, Lazer, StrLaser, Sniper, BounceGun }

public class AugmentDef
{
    public string id;
    public string name;
    public AugTag tag;
    public AugGrade grade;
    public int maxLevel;
    public string icon;
    public SideWeapon requiredWeapon = SideWeapon.None;
    public System.Func<int, string> describe;

    public AugmentDef(string id, string name, AugTag tag, AugGrade grade, int maxLevel, string icon, System.Func<int, string> describe, SideWeapon req = SideWeapon.None)
    {
        this.id = id; this.name = name; this.tag = tag; this.grade = grade;
        this.maxLevel = maxLevel; this.icon = icon; this.describe = describe; requiredWeapon = req;
    }
}

public static class AugmentDefs
{
    // 효과 수치 (★ 레벨 L = 1..5). 숫자는 시뮬레이션 전 초안.
    public static float IgniteDamage(int L) => 0.5f + 0.15f * (L - 1);          // 플레이어 공격력 대비
    public static float GunpowderRadius(int L) => 0.15f + 0.05f * (L - 1);      // 폭발 범위 +%
    public static int ChainDepth(int L) => 4 + L;                               // 연쇄 단수
    public static float HammerDamage(int L) => 3f + 1.5f * (L - 1);             // 플레이어 공격력 배수
    public static int CarpetBombs(int L) => Mathf.Min(2 + L, 6);                // 추가 폭탄 수
    public static float SoulPerKill(int L) => 0.0005f * (1 + 0.375f * (L - 1)); // 처치당 피해 +
    public static float BloodDamage(int L) => 0.4f + 0.1f * (L - 1);
    public static float WraithPerBoss(int L) => 0.03f * (1 + 0.25f * (L - 1));
    public static float SlaughterPerKill(int L) => 0.005f * (1 + 0.25f * (L - 1));
    public static float TartarusMult(int L) => 2f + 0.25f * (L - 1);
    public static float MultiShotChance(int L) => 0.15f + 0.05f * (L - 1);
    public static float BigShotSize(int L) => 0.15f + 0.05f * (L - 1);
    public static float StardustShotPer100(int L) => 0.05f * (1 + 0.25f * (L - 1));
    public static float SplitChance(int L) => 0.10f + 0.03f * (L - 1);
    public static int BounceExtra(int L) => 1 + L;
    public static int SupplyStages(int L) => 30 + 10 * (L - 1);
    public static float CollectorBonus(int L) => 0.25f + 0.10f * (L - 1);

    public static readonly List<AugmentDef> All = new List<AugmentDef> {
        // ---- 연쇄 (헤파이스토스)
        new AugmentDef("ignite", "점화탄", AugTag.Chain, AugGrade.Silver, 5, "ignite",
            L => $"적을 처치하면 작은 폭발\n(공격력의 {IgniteDamage(L) * 100:0}% 범위 피해)"),
        new AugmentDef("gunpowder", "화약 연구", AugTag.Chain, AugGrade.Silver, 5, "gunpowder",
            L => $"모든 폭발 범위 +{GunpowderRadius(L) * 100:0}%"),
        new AugmentDef("chain", "연쇄 폭발", AugTag.Chain, AugGrade.Gold, 5, "chain",
            L => $"폭발로 죽은 적도 폭발\n(최대 {ChainDepth(L)}단 연쇄)"),
        new AugmentDef("hammer", "대장장이의 망치", AugTag.Chain, AugGrade.Prism, 3, "hammer",
            L => $"연쇄가 10단에 닿으면 화면 전체 충격파\n(공격력의 {HammerDamage(L) * 100:0}%)"),
        new AugmentDef("carpet", "융단 폭격", AugTag.Chain, AugGrade.Gold, 4, "carpet",
            L => $"[폭격기] 폭탄을 쏠 때 {CarpetBombs(L)}발을 일렬로 추가 투하", SideWeapon.Bomber),

        // ---- 저주 (타르타로스)
        new AugmentDef("soul", "영혼 수확", AugTag.Curse, AugGrade.Silver, 5, "soul",
            L => $"누적: 처치마다 피해 +{SoulPerKill(L) * 100:0.###}%"),
        new AugmentDef("bloodprice", "피의 대가", AugTag.Curse, AugGrade.Silver, 5, "bloodprice",
            L => $"실드 -1, 피해 +{BloodDamage(L) * 100:0}%"),
        new AugmentDef("wraith", "망령", AugTag.Curse, AugGrade.Silver, 5, "wraith",
            L => $"누적: 보스 처치마다 피해 +{WraithPerBoss(L) * 100:0.#}%"),
        new AugmentDef("slaughter", "학살자의 저주", AugTag.Curse, AugGrade.Gold, 5, "slaughter",
            L => $"누적: 처치마다 피해 +{SlaughterPerKill(L) * 100:0.###}%\n대신 처치 1000마다 적 이동속도 +1%"),
        new AugmentDef("tartarus", "타르타로스의 문", AugTag.Curse, AugGrade.Prism, 3, "tartarus",
            L => $"실드가 1칸만 남으면\n모든 누적 효과 ×{TartarusMult(L):0.##}"),

        // ---- 증식 (아스테리아)
        new AugmentDef("multishot", "다중 사격", AugTag.Split, AugGrade.Silver, 5, "multishot",
            L => $"모든 총이 {MultiShotChance(L) * 100:0}% 확률로 1발 추가 발사"),
        new AugmentDef("bigshot", "큰 탄", AugTag.Split, AugGrade.Silver, 5, "bigshot",
            L => $"탄 크기 +{BigShotSize(L) * 100:0}%"),
        new AugmentDef("stardustshot", "별의 먼지탄", AugTag.Split, AugGrade.Gold, 5, "stardustshot",
            L => $"누적: 처치 100마다 탄 크기 +{StardustShotPer100(L) * 100:0.#}%"),
        new AugmentDef("split", "분열", AugTag.Split, AugGrade.Gold, 5, "split",
            L => $"적중 시 {SplitChance(L) * 100:0}% 확률로\n주변 적 2마리에게 파편"),
        new AugmentDef("bouncegrow", "증식 튕김", AugTag.Split, AugGrade.Gold, 5, "bouncegrow",
            L => $"[튕김총] 튕김 +{BounceExtra(L)}회,\n튕길 때마다 피해 +20%", SideWeapon.BounceGun),

        // ---- 보물 (픽시)
        new AugmentDef("extraarms", "추가 무장", AugTag.Treasure, AugGrade.Gold, 1, "extraarms",
            L => "이번 원정에 무기 칸 +1\n(가진 무기 중 하나를 골라 장착)"),
        new AugmentDef("supply", "보급 투하", AugTag.Treasure, AugGrade.Silver, 3, "supply",
            L => $"무작위 무기 하나가\n{SupplyStages(L)}스테이지 동안 함께 싸움"),
        new AugmentDef("collector", "별의 먼지 수집가", AugTag.Treasure, AugGrade.Gold, 5, "collector",
            L => $"이번 원정 환생 보상 +{CollectorBonus(L) * 100:0}%"),
        new AugmentDef("rerollbag", "리롤 주머니", AugTag.Treasure, AugGrade.Silver, 5, "rerollbag",
            L => "이번 원정 리롤 +3회"),
        new AugmentDef("spareshield", "예비 실드", AugTag.Treasure, AugGrade.Silver, 3, "spareshield",
            L => "실드 +1"),
    };

    static Dictionary<string, AugmentDef> byId;
    public static AugmentDef Get(string id)
    {
        if(byId == null) {
            byId = new Dictionary<string, AugmentDef>();
            foreach(var d in All) byId[d.id] = d;
        }
        byId.TryGetValue(id, out var def);
        return def;
    }

    public static string TagName(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "연쇄";
            case AugTag.Curse: return "저주";
            case AugTag.Split: return "증식";
            default: return "보물";
        }
    }

    public static string GodName(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "헤파이스토스";
            case AugTag.Curse: return "타르타로스";
            case AugTag.Split: return "아스테리아";
            default: return "픽시";
        }
    }

    public static Color TagColor(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return new Color(1f, 0.45f, 0.22f);
            case AugTag.Curse: return new Color(0.66f, 0.4f, 0.95f);
            case AugTag.Split: return new Color(0.3f, 0.68f, 1f);
            default: return new Color(1f, 0.8f, 0.3f);
        }
    }

    public static Color GradeColor(AugGrade g)
    {
        switch(g) {
            case AugGrade.Silver: return new Color(0.8f, 0.84f, 0.88f);
            case AugGrade.Gold: return new Color(1f, 0.78f, 0.25f);
            default: return new Color(0.95f, 0.5f, 1f);
        }
    }

    public static string GradeName(AugGrade g)
    {
        switch(g) {
            case AugGrade.Silver: return "실버";
            case AugGrade.Gold: return "골드";
            default: return "프리즘";
        }
    }

    // 계열 시너지 단계 설명 (MVP는 계열당 증강이 5개라 6단계는 아직 도달 불가)
    public static string SynergyText(AugTag t, int tier)
    {
        switch(t) {
            case AugTag.Chain:
                return tier == 2 ? "폭발 범위 +10%" : tier == 4 ? "연쇄 단수 +2" : "모든 처치가 폭발";
            case AugTag.Curse:
                return tier == 2 ? "누적 속도 +20%" : tier == 4 ? "저주의 대가 절반" : "누적 효과 ×1.5";
            case AugTag.Split:
                return tier == 2 ? "탄 크기 +10%" : tier == 4 ? "파편 피해 100%" : "다중 사격 확률 +100%";
        }
        return "";
    }

    public static string WeaponName(SideWeapon w)
    {
        switch(w) {
            case SideWeapon.DoubleGun: return "쌍발총";
            case SideWeapon.Bomber: return "폭격기";
            case SideWeapon.TrackingGun: return "유도탄";
            case SideWeapon.Poison: return "독 비행기";
            case SideWeapon.Orbital: return "궤도";
            case SideWeapon.Lazer: return "360 레이저";
            case SideWeapon.StrLaser: return "직선 레이저";
            case SideWeapon.Sniper: return "저격총";
            case SideWeapon.BounceGun: return "튕김총";
        }
        return "없음";
    }

    public static AugTag? WeaponTag(SideWeapon w)
    {
        switch(w) {
            case SideWeapon.Bomber: case SideWeapon.TrackingGun: return AugTag.Chain;
            case SideWeapon.Orbital: case SideWeapon.Lazer: return AugTag.Curse;
            case SideWeapon.BounceGun: case SideWeapon.DoubleGun: return AugTag.Split;
        }
        return null;
    }
}
