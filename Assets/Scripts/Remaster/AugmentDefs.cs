using System.Collections.Generic;
using UnityEngine;

// 증강 데이터 정의 (설계서 v3 2-4-2, 증강_목록_초안.md)
// 번호는 저장 데이터(축복)에 쓰여서 앞의 넷은 순서를 바꾸지 말고 뒤에만 추가해요.
public enum AugTag { Chain, Curse, Split, Treasure, Crit, Plague, Thunder, Frost, Gravity, Guard, Weapon }
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

// 조합 계열: 두 메인 계열을 각각 2개 이상(2단계는 4개 이상) 모으면 켜지는 약한 보너스
public class ComboDef
{
    public string god, name;
    public AugTag a, b;
    public string tier1, tier2;
    public ComboDef(string god, string name, AugTag a, AugTag b, string tier1, string tier2)
    {
        this.god = god; this.name = name; this.a = a; this.b = b; this.tier1 = tier1; this.tier2 = tier2;
    }
}

public static class AugmentDefs
{
    // 메인 계열 9개 (보물·무기 전용 제외)
    public static readonly AugTag[] MainTags = {
        AugTag.Chain, AugTag.Curse, AugTag.Split, AugTag.Crit, AugTag.Plague,
        AugTag.Thunder, AugTag.Frost, AugTag.Gravity, AugTag.Guard
    };
    public static bool IsMain(AugTag t) => t != AugTag.Treasure && t != AugTag.Weapon;

    static float Lin(int L, float a, float b) => a + b * (L - 1);

    // ---- 효과 수치 (★ 레벨 L). 숫자는 시뮬레이션·테스트 전 초안
    // 연쇄
    public static float IgniteDamage(int L) => Lin(L, 0.5f, 0.15f);          // 플레이어 공격력 대비
    public static float GunpowderRadius(int L) => Lin(L, 0.15f, 0.05f);
    public static float EmberTick(int L) => Lin(L, 0.15f, 0.05f);            // 폭발 피해 대비 (0.5초마다 4번)
    public static int ChainDepth(int L) => 4 + L;
    public static float FurnacePer(int L) => Lin(L, 0.002f, 0.0005f);         // 폭발 1회당 폭발 피해 +
    public const float FurnaceCap = 2f;
    public static float ShrapnelDamage(int L) => Lin(L, 0.3f, 0.1f);
    public static float HeatwaveDamage(int L) => Lin(L, 0.2f, 0.1f);
    public static float HammerDamage(int L) => Lin(L, 3f, 1.5f);
    public static int CarpetBombs(int L) => Mathf.Min(2 + L, 6);
    // 저주
    public static float SoulPerKill(int L) => 0.001f * (1 + 0.375f * (L - 1));
    public static float BloodDamage(int L) => Lin(L, 0.4f, 0.1f);
    public static float ChainsSlow(int L) => Lin(L, 0.10f, 0.025f);
    public static float WraithPerBoss(int L) => 0.03f * (1 + 0.25f * (L - 1));
    public static float SlaughterPerKill(int L) => 0.002f * (1 + 0.25f * (L - 1));
    public static float AbyssPerCurse(int L) => Lin(L, 0.06f, 0.02f);
    public static int DeathmarkEvery(int L) => 50 - 5 * (L - 1);
    public static float TartarusMult(int L) => Lin(L, 2f, 0.25f);
    // 증식
    public static float MultiShotChance(int L) => Lin(L, 0.15f, 0.05f);
    public static float BigShotSize(int L) => Lin(L, 0.15f, 0.05f);
    public static float TwinChance(int L) => Lin(L, 0.10f, 0.03f);
    public static int RicochetCount(int L) => 1 + (L - 1) / 2;
    public static float StardustShotPer100(int L) => 0.05f * (1 + 0.25f * (L - 1));
    public static float SplitChance(int L) => Lin(L, 0.10f, 0.03f);
    public static float RefractChance(int L) => Lin(L, 0.25f, 0.10f);
    public static int StarswarmGenerations(int L) => 1 + L;
    // 치명
    public static float AimChance(int L) => Lin(L, 8f, 2f);                   // %p
    public static float VitalDamage(int L) => Lin(L, 0.3f, 0.1f);
    public static float ExecuteLine(int L) => Lin(L, 0.10f, 0.01f);
    public static float HuntChance(int L) => Lin(L, 15f, 5f);
    public static float StreakPer(int L) => Lin(L, 0.10f, 0.025f);
    public const int StreakMax = 10;
    public static float OvercritRate(int L) => Lin(L, 1f, 0.25f);
    public static float RevengeSeconds(int L) => Lin(L, 10f, 2f);
    public static int MoonhuntEvery(int L) => 1000 - 200 * (L - 1);
    // 전염
    public static float InfectSeconds(int L) => Lin(L, 2f, 0.5f);
    public static float PoisonTickBase(int L) => L > 0 ? Lin(L, 0.10f, 0.02f) : 0.10f;   // 0.5초마다 중첩 1당 공격력 대비
    public static float VenomBonus(int L) => Lin(L, 0.3f, 0.1f);
    public static float CloudSize(int L) => Lin(L, 0.25f, 0.05f);
    public static float CorrodeBonus(int L) => Lin(L, 0.10f, 0.03f);
    public static int ContagionTargets(int L) => 3 + (L - 1) / 2;
    public static float MutatePer(int L) => Lin(L, 0.005f, 0.001f);
    public const float MutateCap = 3f;
    public static int ToxburstStacks(int L) => 10 - (L - 1);
    public static float NemesisBoss(int L) => Lin(L, 0.3f, 0.1f);
    // 번개
    public static float StaticChance(int L) => Lin(L, 0.10f, 0.03f);
    public static int ConductJumps(int L) => 1 + (L - 1) / 2;
    public static float OverloadBonus(int L) => Lin(L, 0.3f, 0.1f);
    public static float RodBonus(int L) => Lin(L, 0.10f, 0.05f);
    public static float ChainlightPer(int L) => Lin(L, 0.15f, 0.05f);
    public static int CapacitorEvery(int L) => 100 - 10 * (L - 1);
    public static float StormInterval(int L) => Lin(L, 10f, -1f);
    public static int JudgmentJumps(int L) => 2 + L;
    // 냉기
    public static float FrostSlow(int L) => Lin(L, 0.20f, 0.05f);
    public static float ChillSeconds(int L) => Lin(L, 1f, 0.25f);
    public static float FreezeChance(int L) => Lin(L, 0.05f, 0.015f);
    public static float ShatterBonus(int L) => Lin(L, 0.5f, 0.15f);
    public static float SnowflakeRadius(int L) => Lin(L, 2f, 0.3f);
    public static float PermafrostPer(int L) => 0.01f * (1 + 0.25f * (L - 1));
    public static float GlacierDamage(int L) => Lin(L, 0.6f, 0.15f);
    public static float NorthwindInterval(int L) => Lin(L, 20f, -3f);
    // 중력
    public static float PullRadius(int L) => Lin(L, 1.5f, 0.25f);
    public static float FieldSlow(int L) => Lin(L, 0.15f, 0.05f);
    public static float TimeSlow(int L) => Lin(L, 0.15f, 0.05f);
    public static float MassPer(int L) => Lin(L, 0.02f, 0.005f);
    public static float HorizonInterval(int L) => Lin(L, 10f, -1f);
    public static float HasteSeconds(int L) => Lin(L, 3f, 0.5f);
    public static float CollapseDamage(int L) => Lin(L, 1.5f, 0.5f);
    public static float ChronoSeconds(int L) => Lin(L, 5f, 1f);
    public const int ChronoUses = 3;
    // 수호
    public static float CounterDamage(int L) => Lin(L, 3f, 1f);
    public static float PolishPer(int L) => Lin(L, 0.05f, 0.01f);
    public static int RegenEvery(int L) => 30 - 4 * (L - 1);
    public static int AegisBlocks(int L) => 1 + (L - 1) / 2;
    public static float UnyieldBonus(int L) => Lin(L, 0.5f, 0.15f);
    public static float OathPer(int L) => Lin(L, 0.01f, 0.0025f);
    public const float OathCap = 2f;
    // 보물
    public static int SupplyStages(int L) => 30 + 10 * (L - 1);
    public static float CollectorBonus(int L) => Lin(L, 0.25f, 0.10f);
    public static float HarvestChance(int L) => Lin(L, 0.10f, 0.05f);
    // 무기 전용
    public static int BounceExtra(int L) => 1 + L;

    static string P(float v) => $"{v * 100:0.#}%";

    static AugmentDef A(string id, string name, AugTag tag, AugGrade grade, System.Func<int, string> d, int max = 0, SideWeapon req = SideWeapon.None)
        => new AugmentDef(id, name, tag, grade, max > 0 ? max : grade == AugGrade.Prism ? 3 : 5, id, d, req);

    const AugGrade S = AugGrade.Silver, G = AugGrade.Gold, R = AugGrade.Prism;

    public static readonly List<AugmentDef> All = new List<AugmentDef> {
        // ---- 연쇄 (헤파이스토스)
        A("ignite", "점화탄", AugTag.Chain, S, L => $"적을 처치하면 작은 폭발\n(공격력의 {P(IgniteDamage(L))} 범위 피해)"),
        A("gunpowder", "화약 연구", AugTag.Chain, S, L => $"모든 폭발 범위 +{P(GunpowderRadius(L))}"),
        A("embers", "잔불", AugTag.Chain, S, L => $"폭발 자리에 2초 동안 불이 남아요\n(0.5초마다 폭발 피해의 {P(EmberTick(L))})"),
        A("chain", "연쇄 폭발", AugTag.Chain, G, L => $"폭발로 죽은 적도 폭발\n(최대 {ChainDepth(L)}단 연쇄)"),
        A("furnace", "용광로", AugTag.Chain, G, L => $"누적: 폭발 1번마다 폭발 피해 +{FurnacePer(L) * 100:0.##}%\n(최대 +{P(FurnaceCap)})"),
        A("shrapnel", "파편", AugTag.Chain, G, L => $"폭발할 때 파편 3개가 주변 적에게 튐\n(폭발 피해의 {P(ShrapnelDamage(L))})"),
        A("heatwave", "열폭풍", AugTag.Chain, G, L => $"폭발이 주변 적을 밀어내며\n폭발 피해의 {P(HeatwaveDamage(L))}를 더 줘요"),
        A("hammer", "대장장이의 망치", AugTag.Chain, R, L => $"연쇄가 10단에 닿으면 화면 전체 충격파\n(공격력의 {P(HammerDamage(L))})"),

        // ---- 저주 (타르타로스)
        A("soul", "영혼 수확", AugTag.Curse, S, L => $"누적: 이번 원정에서 처치할수록 피해 증가\n(처음 100마리 동안 +{SoulPerKill(L) * 10000:0.#}%, 갈수록 천천히)"),
        A("bloodprice", "피의 대가", AugTag.Curse, S, L => $"실드 -1, 피해 +{P(BloodDamage(L))}"),
        A("chains", "무거운 사슬", AugTag.Curse, S, L => $"적 이동속도 -{P(ChainsSlow(L))}\n대신 내 공격속도 -5%"),
        A("wraith", "망령", AugTag.Curse, S, L => $"누적: 보스 처치마다 피해 +{P(WraithPerBoss(L))}"),
        A("slaughter", "학살자의 저주", AugTag.Curse, G, L => $"누적: 처치할수록 피해 크게 증가\n(처음 100마리 동안 +{SlaughterPerKill(L) * 10000:0.#}%, 갈수록 천천히)\n대신 처치 1000마다 적 이동속도 +1%"),
        A("abyss", "심연의 계약", AugTag.Curse, G, L => $"가진 저주 증강 1개마다\n모든 누적 효과 +{P(AbyssPerCurse(L))}"),
        A("deathmark", "죽음의 표식", AugTag.Curse, G, L => $"처치 {DeathmarkEvery(L)}마리마다 적 하나 즉사\n(보스는 최대 체력의 10%)"),
        A("tartarus", "타르타로스의 문", AugTag.Curse, R, L => $"실드가 1칸만 남으면\n모든 누적 효과 ×{TartarusMult(L):0.##}"),

        // ---- 증식 (아스테리아)
        A("multishot", "다중 사격", AugTag.Split, S, L => $"모든 총이 {P(MultiShotChance(L))} 확률로 1발 추가 발사"),
        A("bigshot", "큰 탄", AugTag.Split, S, L => $"탄 크기 +{P(BigShotSize(L))}"),
        A("twin", "쌍둥이탄", AugTag.Split, S, L => $"{P(TwinChance(L))} 확률로 탄이 한 발 더 나가요\n(다중 사격과 따로 굴려요)"),
        A("ricochet", "반사 파편", AugTag.Split, S, L => $"탄이 화면 끝에 닿으면 {RicochetCount(L)}번 튕겨요"),
        A("stardustshot", "별의 먼지탄", AugTag.Split, G, L => $"누적: 처치 100마다 탄 크기 +{P(StardustShotPer100(L))}"),
        A("split", "분열", AugTag.Split, G, L => $"적중 시 {P(SplitChance(L))} 확률로\n주변 적 2마리에게 파편"),
        A("refract", "굴절", AugTag.Split, G, L => $"레이저가 적을 맞히면 {P(RefractChance(L))} 확률로\n옆 적 2마리에게 꺾여 들어가요 (피해 50%)"),
        A("starswarm", "아스테리아의 별무리", AugTag.Split, R, L => $"파편이 다시 갈라져요\n(최대 {StarswarmGenerations(L)}세대)"),

        // ---- 치명 (아르테미스)
        A("aim", "정조준", AugTag.Crit, S, L => $"치명타 확률 +{AimChance(L):0.#}%p"),
        A("vital", "급소", AugTag.Crit, S, L => $"치명타 피해 +{P(VitalDamage(L))}"),
        A("execute", "처형", AugTag.Crit, S, L => $"체력이 {P(ExecuteLine(L))} 이하로 남은 일반 적은 즉사"),
        A("hunt", "사냥감 표시", AugTag.Crit, S, L => $"보스에게 치명타 확률 +{HuntChance(L):0.#}%p"),
        A("streak", "연속 치명", AugTag.Crit, G, L => $"치명타가 이어질수록 치명 피해 +{P(StreakPer(L))}\n(최대 {StreakMax}번, 치명타가 아니면 처음부터)"),
        A("overcrit", "초과 치명", AugTag.Crit, G, L => $"치명타 확률이 100%를 넘은 만큼\n치명 피해로 바꿔요 (×{OvercritRate(L):0.##})"),
        A("revenge", "복수", AugTag.Crit, G, L => $"실드를 잃으면 {RevengeSeconds(L):0.#}초 동안\n치명타 확률 100%"),
        A("moonhunt", "달의 사냥", AugTag.Crit, R, L => $"누적: 치명타 {MoonhuntEvery(L):N0}번마다\n화면 전체에 치명타 3배 피해"),

        // ---- 전염 (네메시스)
        A("infect", "감염탄", AugTag.Plague, S, L => $"모든 탄이 {InfectSeconds(L):0.#}초 독을 묻혀요\n(0.5초마다 중첩 1당 공격력의 {P(PoisonTickBase(L))})"),
        A("venom", "맹독", AugTag.Plague, S, L => $"독 피해 +{P(VenomBonus(L))}\n(독 비행기 독 지대 포함)"),
        A("cloud", "독구름 확장", AugTag.Plague, S, L => $"독 지대 크기 +{P(CloudSize(L))}"),
        A("corrode", "부식", AugTag.Plague, S, L => $"독에 걸린 적은 받는 피해 +{P(CorrodeBonus(L))}"),
        A("contagion", "전염", AugTag.Plague, G, L => $"독에 걸린 적이 죽으면\n주변 {ContagionTargets(L)}마리에게 독이 옮아요"),
        A("mutate", "변이", AugTag.Plague, G, L => $"누적: 독이 옮을 때마다 독 피해 +{MutatePer(L) * 100:0.##}%\n(최대 +{P(MutateCap)})"),
        A("toxburst", "독 폭발", AugTag.Plague, G, L => $"독이 {ToxburstStacks(L)}중첩 쌓이면 터지며\n주변 적에게 피해와 독"),
        A("nemesis", "네메시스의 역병", AugTag.Plague, R, L => $"화면의 일반 적이 모두 독에 걸리면\n3초 뒤 한꺼번에 소멸\n(보스는 최대 체력의 {P(NemesisBoss(L))})"),

        // ---- 번개 (제우스)
        A("static", "정전기탄", AugTag.Thunder, S, L => $"적중 시 {P(StaticChance(L))} 확률로\n번개가 옆 적에게 2번 튐 (공격력의 50%)"),
        A("conduct", "전도", AugTag.Thunder, S, L => $"번개가 튀는 횟수 +{ConductJumps(L)}"),
        A("overload", "과부하", AugTag.Thunder, S, L => $"번개 피해 +{P(OverloadBonus(L))}"),
        A("rod", "피뢰침", AugTag.Thunder, S, L => $"번개가 보스를 먼저 노리고\n보스에게 번개 피해 +{P(RodBonus(L))}"),
        A("chainlight", "연쇄 번개", AugTag.Thunder, G, L => $"번개가 튈 때마다 피해 +{P(ChainlightPer(L))}"),
        A("capacitor", "축전", AugTag.Thunder, G, L => $"누적: 번개 {CapacitorEvery(L)}번마다 낙뢰 1번\n(공격력의 300% 범위 피해)"),
        A("storm", "뇌운", AugTag.Thunder, G, L => $"{StormInterval(L):0}초마다 무작위 적 5마리에 낙뢰"),
        A("judgment", "제우스의 심판", AugTag.Thunder, R, L => $"낙뢰를 맞은 적이 번개를\n{JudgmentJumps(L)}번 다시 튕겨요"),

        // ---- 냉기 (보레아스)
        A("frostshot", "서리탄", AugTag.Frost, S, L => $"적중 시 2초 동안 이동속도 -{P(FrostSlow(L))}"),
        A("chill", "한기", AugTag.Frost, S, L => $"둔화 지속 +{ChillSeconds(L):0.##}초"),
        A("freeze", "빙결", AugTag.Frost, S, L => $"느려진 적을 맞히면 {P(FreezeChance(L))} 확률로\n2초 얼어붙어요 (보스 제외)"),
        A("shatter", "얼음 깨기", AugTag.Frost, S, L => $"얼어 있는 적에게 피해 +{P(ShatterBonus(L))}"),
        A("snowflake", "눈송이", AugTag.Frost, G, L => $"처치 시 반경 {SnowflakeRadius(L):0.#} 안의 적을 둔화"),
        A("permafrost", "영구동토", AugTag.Frost, G, L => $"누적: 빙결 50번마다 빙결 확률 +{PermafrostPer(L) * 100:0.##}%p\n(최대 +15%p)"),
        A("glacier", "빙하 폭발", AugTag.Frost, G, L => $"얼었던 적이 깨지거나 얼어서 죽으면\n주변에 공격력의 {P(GlacierDamage(L))} 폭발"),
        A("northwind", "북풍", AugTag.Frost, R, L => $"{NorthwindInterval(L):0}초마다 화면 전체 3초 빙결\n(보스는 1초)"),

        // ---- 중력 (크로노스)
        A("pull", "끌어당김", AugTag.Gravity, S, L => $"적중 시 반경 {PullRadius(L):0.##} 안의 적을\n맞은 적 쪽으로 끌어당겨 뭉치게 해요"),
        A("field", "중력장", AugTag.Gravity, S, L => $"우주선 가까이 온 적 이동속도 -{P(FieldSlow(L))}"),
        A("timeslow", "시간 지연", AugTag.Gravity, S, L => $"보스 이동속도 -{P(TimeSlow(L))}"),
        A("mass", "질량 증폭", AugTag.Gravity, S, L => $"화면의 적 1마리마다 피해 +{P(MassPer(L))}\n(최대 30마리)"),
        A("horizon", "사건의 지평선", AugTag.Gravity, G, L => $"{HorizonInterval(L):0}초마다 블랙홀이\n적을 한곳에 모아요"),
        A("haste", "시간 가속", AugTag.Gravity, G, L => $"10초마다 {HasteSeconds(L):0.#}초 동안 공격속도 +50%"),
        A("collapse", "중력 붕괴", AugTag.Gravity, G, L => $"블랙홀이 사라질 때 큰 폭발\n(공격력의 {P(CollapseDamage(L))})"),
        A("chronostop", "크로노스의 정지", AugTag.Gravity, R, L => $"실드가 깎이면 {ChronoSeconds(L):0}초 동안 적의 시간이 멈춰요\n(원정마다 {ChronoUses}번)"),

        // ---- 수호 (아테나)
        A("armor", "강철 장갑", AugTag.Guard, S, L => $"실드 +{L}, 공격속도 -{3 * L}%"),
        A("counter", "반격", AugTag.Guard, S, L => $"실드가 깎이면 우주선 주변 큰 폭발\n(공격력의 {P(CounterDamage(L))})"),
        A("polish", "방패 연마", AugTag.Guard, S, L => $"남은 실드 1칸마다 피해 +{P(PolishPer(L))}"),
        A("regen", "재생", AugTag.Guard, S, L => $"{RegenEvery(L)}스테이지마다 실드 1칸 회복"),
        A("aegis", "아이기스", AugTag.Guard, G, L => $"스테이지마다 처음 {AegisBlocks(L)}번의 실드 피해를 막아요"),
        A("unyield", "불굴", AugTag.Guard, G, L => $"실드가 1칸이면 피해 +{P(UnyieldBonus(L))}"),
        A("oath", "수호의 맹세", AugTag.Guard, G, L => $"누적: 실드를 잃지 않고 넘긴 스테이지마다\n피해 +{OathPer(L) * 100:0.##}% (최대 +{P(OathCap)})"),
        A("barrier", "아테나의 방벽", AugTag.Guard, R, L => $"실드가 0이 되면 1칸으로 버티고\n화면의 일반 적을 정리 (원정마다 {L}번)"),

        // ---- 보물 (픽시)
        A("extraarms", "추가 무장", AugTag.Treasure, G, L => "이번 원정에 무기 칸 +1\n(가진 무기 중 하나를 골라 장착)", 1),
        A("supply", "보급 투하", AugTag.Treasure, S, L => $"무작위 무기 하나가\n{SupplyStages(L)}스테이지 동안 함께 싸움", 3),
        A("collector", "별의 먼지 수집가", AugTag.Treasure, G, L => $"이번 원정 환생 보상 +{P(CollectorBonus(L))}"),
        A("rerollbag", "리롤 주머니", AugTag.Treasure, S, L => "이번 원정 리롤 +3회"),
        A("spareshield", "예비 실드", AugTag.Treasure, S, L => "실드 +1", 3),
        A("harvest", "풍년", AugTag.Treasure, G, L => $"증강을 받을 때 {P(HarvestChance(L))} 확률로\n하나 더 뽑아요", 3),

        // ---- 무기 전용 (3단계에서 무기마다 3개로 늘려요)
        A("carpet", "융단 폭격", AugTag.Weapon, G, L => $"[폭격기] 폭탄을 쏠 때 {CarpetBombs(L)}발을 일렬로 추가 투하", 4, SideWeapon.Bomber),
        A("bouncegrow", "증식 튕김", AugTag.Weapon, G, L => $"[튕김총] 튕김 +{BounceExtra(L)}회,\n튕길 때마다 피해 +20%", 5, SideWeapon.BounceGun),
    };

    // ---- 조합 계열 9개 (설계서 2-4-3). 메인 계열마다 정확히 두 조합에 들어가요
    public static readonly ComboDef[] Combos = {
        new ComboDef("브론테스", "천둥망치", AugTag.Chain, AugTag.Thunder, "폭발이 15% 확률로 번개를 튕겨요", "폭발이 30% 확률로 번개를 튕겨요"),
        new ComboDef("타나토스", "사신", AugTag.Crit, AugTag.Curse, "치명타로 처치하면 처치 2번으로 세요", "치명타로 처치하면 처치 3번으로 세요"),
        new ComboDef("히프노스", "깊은 잠", AugTag.Plague, AugTag.Frost, "독에 걸린 적 이동속도 -10%", "독에 걸린 적 이동속도 -20%"),
        new ComboDef("이리스", "무지개", AugTag.Thunder, AugTag.Split, "파편이 15% 확률로 번개를 튕겨요", "파편이 30% 확률로 번개를 튕겨요"),
        new ComboDef("포보스", "공포", AugTag.Gravity, AugTag.Chain, "폭발이 주변 적을 가운데로 끌어당겨요", "끌어당김 + 폭발 피해 +20%"),
        new ComboDef("에리스", "불화", AugTag.Curse, AugTag.Plague, "독에 걸린 적을 처치하면 처치 1번 더", "독에 걸린 적을 처치하면 처치 2번 더"),
        new ComboDef("니케", "승리", AugTag.Split, AugTag.Crit, "파편도 치명타가 터져요 (확률 절반)", "파편도 치명타가 터져요 (확률 그대로)"),
        new ComboDef("키오네", "눈보라", AugTag.Frost, AugTag.Guard, "실드가 깎이면 화면 전체 3초 둔화", "실드가 깎이면 화면 전체 1.5초 빙결"),
        new ComboDef("모로스", "운명", AugTag.Guard, AugTag.Gravity, "실드가 깎이면 블랙홀이 바로 생겨요", "실드가 깎이면 큰 블랙홀이 바로 생겨요"),
    };
    public const int ComboThunderHammer = 0, ComboReaper = 1, ComboDeepSleep = 2, ComboRainbow = 3, ComboFear = 4,
                     ComboDiscord = 5, ComboVictory = 6, ComboBlizzard = 7, ComboFate = 8;

    static Dictionary<string, AugmentDef> byId;
    public static AugmentDef Get(string id)
    {
        if(id == null) return null;
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
            case AugTag.Crit: return "치명";
            case AugTag.Plague: return "전염";
            case AugTag.Thunder: return "번개";
            case AugTag.Frost: return "냉기";
            case AugTag.Gravity: return "중력";
            case AugTag.Guard: return "수호";
            case AugTag.Weapon: return "무기 전용";
            default: return "보물";
        }
    }

    public static string GodName(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "헤파이스토스";
            case AugTag.Curse: return "타르타로스";
            case AugTag.Split: return "아스테리아";
            case AugTag.Crit: return "아르테미스";
            case AugTag.Plague: return "네메시스";
            case AugTag.Thunder: return "제우스";
            case AugTag.Frost: return "보레아스";
            case AugTag.Gravity: return "크로노스";
            case AugTag.Guard: return "아테나";
            case AugTag.Weapon: return "무기";
            default: return "픽시";
        }
    }

    public static Color TagColor(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return new Color(1f, 0.45f, 0.22f);
            case AugTag.Curse: return new Color(0.66f, 0.4f, 0.95f);
            case AugTag.Split: return new Color(0.3f, 0.68f, 1f);
            case AugTag.Crit: return new Color(1f, 0.33f, 0.45f);
            case AugTag.Plague: return new Color(0.45f, 0.85f, 0.3f);
            case AugTag.Thunder: return new Color(1f, 0.92f, 0.3f);
            case AugTag.Frost: return new Color(0.55f, 0.92f, 1f);
            case AugTag.Gravity: return new Color(0.45f, 0.35f, 0.85f);
            case AugTag.Guard: return new Color(0.85f, 0.85f, 0.7f);
            case AugTag.Weapon: return new Color(0.6f, 0.75f, 0.9f);
            default: return new Color(1f, 0.8f, 0.3f);
        }
    }

    // 계열 아이콘 (그림이 없는 계열은 글자로 대신해요)
    public static string TagIcon(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "bless_chain";
            case AugTag.Curse: return "bless_curse";
            case AugTag.Split: return "bless_split";
        }
        return "tag_" + t.ToString().ToLowerInvariant();
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

    public static readonly int[] SetTiers = { 2, 4, 6, 8 };

    // 계열 세트 효과 (2/4/6/8)
    public static string SynergyText(AugTag t, int tier)
    {
        int i = tier >= 8 ? 3 : tier >= 6 ? 2 : tier >= 4 ? 1 : 0;
        string[] s;
        switch(t) {
            case AugTag.Chain: s = new[] { "폭발 범위 +10%", "연쇄 단수 +2", "모든 처치가 폭발", "폭발마다 작은 폭발이 3번 더" }; break;
            case AugTag.Curse: s = new[] { "누적 속도 +20%", "저주의 대가 절반", "누적 효과 ×1.5", "누적 상한(+300%)이 사라짐" }; break;
            case AugTag.Split: s = new[] { "탄 크기 +10%", "파편 피해 100%", "모든 총 발사 수 +1", "모든 적중이 2갈래로 분열" }; break;
            case AugTag.Crit: s = new[] { "치명타 확률 +5%p", "치명타 피해 +50%", "치명타가 뒤의 적도 꿰뚫음", "모든 공격이 치명타" }; break;
            case AugTag.Plague: s = new[] { "독 지속 +1초", "전염 대상 +2", "보스에게도 전염", "지속 피해 2배 + 처치하면 무조건 전염" }; break;
            case AugTag.Thunder: s = new[] { "번개 튐 +1", "번개에 맞으면 잠깐 기절", "처치 20마다 낙뢰", "모든 적중이 번개를 띰" }; break;
            case AugTag.Frost: s = new[] { "적 이동속도 -10%", "얼어 있는 적 피해 2배", "얼었다 깨지면 폭발", "보스도 얼어붙음 + 빙결 확률 2배" }; break;
            case AugTag.Gravity: s = new[] { "적중 시 적을 끌어당김", "보스 이동속도 -15%", "블랙홀 주기 절반 (없으면 20초마다)", "블랙홀 가운데의 일반 적 즉시 처치" }; break;
            case AugTag.Guard: s = new[] { "실드 +1", "실드가 깎이면 반격 폭발", "남은 실드 1칸마다 피해 +4%", "스테이지마다 1번, 실드가 깎이면 10초 무적" }; break;
            default: return "";
        }
        return s[i];
    }

    // 계열 축복이 열리는 신의 관문 번호 (관문 = 스테이지 50의 배수)
    public static int BlessingGate(AugTag t)
    {
        switch(t) {
            case AugTag.Guard: return 2;
            case AugTag.Crit: return 3;
            case AugTag.Chain: return 4;
            case AugTag.Plague: return 6;
            case AugTag.Curse: return 8;
            case AugTag.Split: return 10;
            case AugTag.Thunder: return 12;
            case AugTag.Frost: return 14;
            case AugTag.Gravity: return 16;
        }
        return -1;
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

    // 무기 카드 색 (잘 맞는 계열)
    public static AugTag? WeaponTag(SideWeapon w)
    {
        switch(w) {
            case SideWeapon.Bomber: case SideWeapon.TrackingGun: return AugTag.Chain;
            case SideWeapon.Lazer: return AugTag.Curse;
            case SideWeapon.BounceGun: case SideWeapon.DoubleGun: return AugTag.Split;
            case SideWeapon.Sniper: return AugTag.Crit;
            case SideWeapon.Poison: return AugTag.Plague;
            case SideWeapon.StrLaser: return AugTag.Thunder;
            case SideWeapon.Orbital: return AugTag.Guard;
        }
        return null;
    }
}
