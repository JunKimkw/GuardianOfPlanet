using System.Collections.Generic;
using UnityEngine;

// 증강 데이터 정의 (설계서 v3 2-4-2, 증강_목록_초안.md)
// 번호는 저장 데이터(축복)에 쓰여서 앞의 넷은 순서를 바꾸지 말고 뒤에만 추가해요.
public enum AugTag { Chain, Curse, Split, Treasure, Crit, Plague, Thunder, Frost, Gravity, Guard, Weapon }
public enum AugGrade { Silver = 0, Gold = 1, Prism = 2 }

// 출격 가능한 보조 무기 (기본총 제외)
public enum SideWeapon { None = -1, DoubleGun = 0, Bomber, TrackingGun, Poison, Orbital, Lazer, StrLaser, Sniper, BounceGun, Meteor }   // Meteor: 2026-11-21 추가 (저장은 숫자라 맨 뒤에만 붙여요)

public class AugmentDef
{
    public string id;
    public string name;
    public AugTag tag;
    public AugGrade grade;
    public int maxLevel;
    public string icon;
    public SideWeapon requiredWeapon = SideWeapon.None;
    public int fusion = -1;           // 합체 번호 (AugmentDefs.Fusions). 카드 또는 합체 무기 전용 증강
    public bool fusionCard;           // 합체 카드 (고르면 무기 A + B 합체)
    public bool ultimate;             // 합체 무기 종결 증강 (합체 전용 3개를 다 모으면 나와요)
    public System.Func<int, string> describe;

    public AugmentDef(string id, string name, AugTag tag, AugGrade grade, int maxLevel, string icon, System.Func<int, string> describe, SideWeapon req = SideWeapon.None)
    {
        this.id = id; this.name = name; this.tag = tag; this.grade = grade;
        this.maxLevel = maxLevel; this.icon = icon; this.describe = describe; requiredWeapon = req;
    }
}

// 무기 합체 (설계서 2-4-1): 무기 A 전용 증강 3개를 모두 모으면 짝 무기 B 카드가 나와요
public class FusionDef
{
    public SideWeapon a, b;
    public string name, card, effect;
    public string[] augs;      // 합체 무기 전용 증강 3개
    public FusionDef(SideWeapon a, SideWeapon b, string name, string card, string effect, params string[] augs)
    {
        this.a = a; this.b = b; this.name = name; this.card = card; this.effect = effect; this.augs = augs;
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
    public static float CollectorBonus(int L) => Lin(L, 0.25f, 0.10f);
    public static float HarvestChance(int L) => Lin(L, 0.10f, 0.05f);
    // 무기 전용 (무기마다 3개, 설계서 2-4-1)
    public static float QualityBonus(int L) => 0.15f * (L - 1);              // 효과가 숫자 없는 증강: ★2부터 그 무기 피해 +15%씩
    public static float DgRapid(int L) => Lin(L, 0.15f, 0.05f);              // 쌍발총 공격속도
    public static float DgAccel(int L) => Lin(L, 0.30f, 0.10f);              // 쌍발총 탄속
    public static float BmBig(int L) => Lin(L, 0.25f, 0.08f);                // 폭격기 폭발 범위
    public static float BmSecond(int L) => Lin(L, 0.40f, 0.15f);             // 2차 폭발 피해
    public static int MtRain(int L) => 1 + (L - 1) / 2;                      // 운석 유성우: 운석 +
    public static float MtBig(int L) => Lin(L, 0.25f, 0.08f);                // 운석 범위 +
    public static float MtBurn(int L) => Lin(L, 0.15f, 0.05f);               // 불타는 구덩이: 0.5초마다 운석 피해의 이 비율
    // 운석 합체 (2026-11-21)
    public static int BdMore(int L) => 1 + (L - 1) / 2;                      // 별의 낙인: 낙인 운석 +
    public static float BdAim(int L) => Lin(L, 0.5f, 0.2f);                  // 별의 낙인: 치명 피해 +
    public static int BdChain(int L) => L;                                   // 별의 낙인 처형 낙인 (R ★3): 옮겨 가는 횟수
    public static float ImWell(int L) => Lin(L, 0.08f, 0.03f);               // 대충돌: 블랙홀 범위 + (2026-11-21 사용자: 30~70% → 15~35% → 최대 20%: 8·11·14·17·20%)
    public static int ImMore(int L) => 1 + (L - 1) / 2;                      // 대충돌 소행성대: 블랙홀마다 운석 + ("중력 지속" 대신, 2026-11-21)
    public static float ImEnergy(int L) => Lin(L, 0.8f, 0.4f);               // 대충돌 충돌 에너지 (R ★3): 큰 운석 피해 + 80·120·160% (2026-11-21, 연쇄 충돌 대신)
    public static int TkMulti(int L) => 1 + (L - 1) / 2;                     // 미사일 추가
    public static float TkHoming(int L) => Lin(L, 0.50f, 0.15f);             // 선회 속도
    public static float TkWarhead(int L) => Lin(L, 0.30f, 0.10f);            // 명중 폭발 피해
    public static float PsLong(int L) => Lin(L, 2f, 0.5f);                   // 독 지대 지속 (초)
    public static float PsToxic(int L) => Lin(L, 0.50f, 0.15f);              // 독 지대 피해
    public static float ObFast(int L) => Lin(L, 0.30f, 0.10f);               // 드론 공격속도 (속사)
    public static float ObShock(int L) => Lin(L, 15f, 5f);                   // (예전 충격탄, 지금은 안 써요)
    public static float LzLong(int L) => Lin(L, 2f, 0.5f);                   // 360 레이저 지속 (초)
    // 레이저 폭 (2026-11-20: +30~70% 는 빔이 원래 가늘어서 눈에 안 띄었어요 → 배율 3 = ★1 +90%, ★5 +210%. tuning.txt beam_width_mul)
    public static float BeamWidth(int L) => Lin(L, 0.30f, 0.10f) * Tuning.Get("beam_width_mul", 3f);
    // 블랙홀 (2026-11-20, 예전 직선 레이저 자리)
    public static float BhBig(int L) => Lin(L, 0.20f, 0.10f);                // 반경 +
    public static float BhLong(int L) => Lin(L, 0.5f, 0.25f);                // 열려 있는 시간 + (초)
    public static float BhCollapse(int L) => Lin(L, 0.5f, 0.25f);            // 붕괴 폭발 피해 +
    // 블랙홀 합체 (2026-11-20)
    public static float SgCharge(int L) => Lin(L, 0.4f, 0.4f);               // 특이점 저격: 블랙홀 간격 - (초). ★5 = 2초 (2026-11-20: 3초는 너무 많다고 해서)
    public static float SgMark(int L) => Lin(L, 0.05f, 0.025f);              // 특이점 저격: 모인 적 1마리당 피해 + (추가)
    public static float SgEcho(int L) => Lin(L, 0.2f, 0.1f);                 // 특이점 저격: 저격 뒤 작은 블랙홀이 다시 열릴 확률
    public static float GvBig(int L) => Lin(L, 0.25f, 0.1f);                 // 중력 폭탄: 폭발 범위 +
    public static int GvChain(int L) => 2 + (L - 1) / 2;                     // 중력 폭탄: 둘레 작은 폭발 수
    public static float GvMass(int L) => Lin(L, 0.04f, 0.02f);               // 중력 폭탄: 모인 적 1마리당 피해 + (추가)
    public static float SnFast(int L) => Lin(L, 0.20f, 0.05f);               // 저격총 장전 감소
    public static float SnHead(int L) => Lin(L, 1.0f, 0.3f);                 // 저격총 치명 피해
    public static float BnAccel(int L) => Lin(L, 0.20f, 0.05f);              // 튈 때마다 탄속
    public static float BounceGrowDamage(int L) => Lin(L, 0.20f, 0.05f);     // 튈 때마다 피해
    public static int BounceExtra(int L) => 1 + L;                           // 튕김 강화
    public static float CmTail(int L) => Lin(L, 0.25f, 0.125f);              // 혜성탄 긴 꼬리: 꼬리 길이·피해 +
    public static int CmBounce(int L) => 1 + L / 2;                          // 혜성탄 궤도 연장: 튕김 + (1·2·2·3·3)
    public static float CmImpact(int L) => Lin(L, 0.4f, 0.2f);               // 혜성탄 혜성 충돌 (R ★3): 1발 × 이 값 × (1 + 0.1 × 튕긴 수)

    // 합체 무기 전용
    public const float FusionPower = 3f, FusionPowerFull = 5f;               // 합체 직후 A의 3배, 전용 증강 3개를 다 모으면 5배
    public static float GbDouble(int L) => Lin(L, 1.0f, 0.2f);
    public static int GbMissile(int L) => 2 + (L - 1) / 2;
    public static int GbFleet(int L) => 4 + L;
    public static int SatMore(int L) => 2 + (L - 1) / 2;
    public static float SatLong(int L) => Lin(L, 3f, 1f);
    public static float SatFocus(int L) => Lin(L, 1.5f, 0.5f);
    public static float RgCharge(int L) => Lin(L, 0.25f, 0.05f);
    public static float RgBlast(int L) => Lin(L, 0.30f, 0.10f);
    public static int RgMegaEvery(int L) => 5 - L;
    public static float PbArea(int L) => Lin(L, 0.30f, 0.10f);
    public static float PbToxic(int L) => Lin(L, 1.0f, 0.3f);
    public static int PbSpread(int L) => L;
    public static int SdMore(int L) => 1 + (L - 1) / 2;
    public static float SdAccel(int L) => Lin(L, 0.30f, 0.10f);
    public static float SdEndless(int L) => Lin(L, 0.15f, 0.10f);
    public static int HdBounce(int L) => 2 + (L - 1) / 2;
    public static float HdBoss(int L) => Lin(L, 0.30f, 0.10f);
    public static float HdPack(int L) => Lin(L, 0.20f, 0.10f);
    public static float TsTrail(int L) => Lin(L, 0.50f, 0.15f);
    public static int TsMore(int L) => 1 + (L - 1) / 2;
    public static float TsRing(int L) => Lin(L, 2.5f, 0.5f);
    public static int PrMore(int L) => 2 + (L - 1) / 2;
    public static float PrSpin(int L) => Lin(L, 0.30f, 0.10f);
    public static float PrStormEvery(int L) => Lin(L, 20f, -4f);

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
        A("horizon", "중력 우물", AugTag.Gravity, G, L => $"{HorizonInterval(L):0}초마다 블랙홀이\n적을 한곳에 모아요"),
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
        A("collector", "별의 먼지 수집가", AugTag.Treasure, G, L => $"이번 원정 환생 보상 +{P(CollectorBonus(L))}"),
        A("rerollbag", "리롤 주머니", AugTag.Treasure, S, L => "이번 원정 리롤 +3회"),
        A("spareshield", "예비 실드", AugTag.Treasure, S, L => "실드 +1", 3),
        A("harvest", "풍년", AugTag.Treasure, G, L => $"증강을 받을 때 {P(HarvestChance(L))} 확률로\n하나 더 뽑아요", 3),

        // ---- 무기 전용 (무기마다 3개. 원정 준비에서 고른 무기 것만 나와요. 3개를 다 모으면 합체 카드)
        A("dg_rapid", "쌍둥이 연사", AugTag.Weapon, S, L => $"[쌍발총] 공격속도 +{P(DgRapid(L))}", 0, SideWeapon.DoubleGun),
        A("dg_accel", "탄 가속", AugTag.Weapon, S, L => $"[쌍발총] 탄속 +{P(DgAccel(L))}", 0, SideWeapon.DoubleGun),
        A("dg_cross", "교차 사격", AugTag.Weapon, G, L => $"[쌍발총] 두 총구가 서로 다른 적을 조준{Q(L)}", 3, SideWeapon.DoubleGun),
        A("bm_big", "대형 폭탄", AugTag.Weapon, S, L => $"[폭격기] 폭발 범위 +{P(BmBig(L))}", 0, SideWeapon.Bomber),
        A("bm_second", "2차 폭발", AugTag.Weapon, S, L => $"[폭격기] 0.5초 뒤 한 번 더 터져요\n(피해 {P(BmSecond(L))})", 0, SideWeapon.Bomber),
        A("carpet", "연속 투하", AugTag.Weapon, G, L => $"[폭격기] 폭탄을 쏠 때 {CarpetBombs(L)}발을 일렬로 추가 투하", 4, SideWeapon.Bomber),
        A("tk_multi", "다탄두", AugTag.Weapon, S, L => $"[유도탄] 미사일 +{TkMulti(L)}발", 0, SideWeapon.TrackingGun),
        A("tk_homing", "추적 강화", AugTag.Weapon, S, L => $"[유도탄] 선회 속도 +{P(TkHoming(L))}", 0, SideWeapon.TrackingGun),
        A("tk_warhead", "폭발 탄두", AugTag.Weapon, G, L => $"[유도탄] 명중하면 작은 폭발\n(미사일 피해의 {P(TkWarhead(L))})", 0, SideWeapon.TrackingGun),
        A("ps_spread", "살포 확장", AugTag.Weapon, S, L => $"[독 비행기] 독 발사 방향 +{L}", 3, SideWeapon.Poison),
        A("ps_long", "오래가는 독", AugTag.Weapon, S, L => $"[독 비행기] 독 지대 지속 +{PsLong(L):0.#}초", 0, SideWeapon.Poison),
        A("ps_toxic", "맹독 지대", AugTag.Weapon, G, L => $"[독 비행기] 독 지대 피해 +{P(PsToxic(L))}", 0, SideWeapon.Poison),
        A("ob_extra", "드론 추가", AugTag.Weapon, S, L => $"[드론] 드론 +{L}대", 3, SideWeapon.Orbital),
        A("ob_fast", "속사", AugTag.Weapon, S, L => $"[드론] 공격속도 +{P(ObFast(L))}", 0, SideWeapon.Orbital),
        A("ob_multi", "추가 발사", AugTag.Weapon, G, L => $"[드론] 쏠 때 탄 +{L}발이 부채꼴로 더 나가요\n(추가 탄 피해 70%)", 3, SideWeapon.Orbital),
        A("lz_long", "지속 연장", AugTag.Weapon, S, L => $"[360 레이저] 쏘는 시간 +{LzLong(L):0.#}초", 0, SideWeapon.Lazer),
        A("lz_thick", "굵은 광선", AugTag.Weapon, S, L => $"[360 레이저] 광선 폭 +{P(BeamWidth(L))}", 0, SideWeapon.Lazer),
        A("lz_twin", "2중 회전", AugTag.Weapon, G, L => $"[360 레이저] 반대 방향 광선 추가{Q(L)}", 3, SideWeapon.Lazer),
        A("bh_big", "거대 중력", AugTag.Weapon, S, L => $"[블랙홀] 빨아들이는 반경 +{P(BhBig(L))}", 0, SideWeapon.StrLaser),
        A("bh_long", "중력 유지", AugTag.Weapon, S, L => $"[블랙홀] 열려 있는 시간 +{BhLong(L):0.##}초", 0, SideWeapon.StrLaser),
        A("bh_collapse", "초신성 붕괴", AugTag.Weapon, G, L => $"[블랙홀] 닫힐 때 폭발 피해 +{P(BhCollapse(L))}, 범위 +25%", 0, SideWeapon.StrLaser),
        A("sn_fast", "속사 장전", AugTag.Weapon, S, L => $"[저격총] 장전 시간 -{P(SnFast(L))}", 0, SideWeapon.Sniper),
        A("sn_ap", "철갑탄", AugTag.Weapon, S, L => $"[저격총] 관통 +{L}", 3, SideWeapon.Sniper),
        A("sn_head", "헤드샷", AugTag.Weapon, G, L => $"[저격총] 치명 피해 +{P(SnHead(L))}", 0, SideWeapon.Sniper),
        A("bn_more", "튕김 강화", AugTag.Weapon, S, L => $"[튕김총] 튕김 +{BounceExtra(L)}회", 0, SideWeapon.BounceGun),
        A("bn_accel", "탄성 가속", AugTag.Weapon, S, L => $"[튕김총] 튈 때마다 탄속 +{P(BnAccel(L))}", 0, SideWeapon.BounceGun),
        A("mt_rain", "유성우", AugTag.Weapon, S, L => $"[운석] 한 번에 떨어지는 운석 +{MtRain(L)}", 0, SideWeapon.Meteor),
        A("mt_big", "거대 운석", AugTag.Weapon, S, L => $"[운석] 떨어지는 범위 +{P(MtBig(L))}", 0, SideWeapon.Meteor),
        A("mt_burn", "불타는 구덩이", AugTag.Weapon, G, L => $"[운석] 떨어진 자리가 2초 동안 불타요\n(0.5초마다 운석 피해의 {P(MtBurn(L))})", 0, SideWeapon.Meteor),
        A("bouncegrow", "증식 튕김", AugTag.Weapon, G, L => $"[튕김총] 튈 때마다 피해 +{P(BounceGrowDamage(L))}\n(튈수록 약해지지 않아요)", 0, SideWeapon.BounceGun),

        // ---- 합체 카드 (프리즘). 합체 무기 전용 증강은 아래 FusionAugs 에서 만들어요
    };

    static string Q(int L) => L > 1 ? $"\n그 무기 피해 +{P(QualityBonus(L))}" : "";

    // 합체 8종 (A + B). 같은 A 가 둘이면 (360 레이저) 하나를 고르면 다른 하나는 그 원정에서 안 나와요
    public static readonly FusionDef[] Fusions = {
        new FusionDef(SideWeapon.Bomber, SideWeapon.TrackingGun, "유도 폭격", "fz_guided", "폭탄이 적을 쫓아가서 더 크게 터져요", "gb_double", "gb_missile", "gb_fleet"),
        new FusionDef(SideWeapon.Lazer, SideWeapon.Orbital, "위성 레이저", "fz_satlaser", "우주선 양쪽 날개의 위성 2대가 가장 가까운 적을 레이저로 계속 비춰요. 같은 적을 오래 비출수록 세져요 (최대 4배)", "sat_more", "sat_long", "sat_focus"),
        new FusionDef(SideWeapon.Sniper, SideWeapon.None, "레일건", "fz_railgun", "탄 대신 화면 끝까지 꿰뚫는 빔을 쏴요", "rg_charge", "rg_blast", "rg_mega"),
        new FusionDef(SideWeapon.Poison, SideWeapon.Bomber, "역병 폭탄", "fz_plague", "독탄이 적을 조준해서 날아가 폭발하고, 독 지대가 1.5배 커지고 적을 30% 느리게 해요", "pb_area", "pb_toxic", "pb_spread"),
        new FusionDef(SideWeapon.DoubleGun, SideWeapon.BounceGun, "분열 쌍발", "fz_splitdbl", "탄이 적을 맞히면 주변 적 2마리에게 갈라져요 (1발당 피해 70%)", "sd_more", "sd_accel", "sd_endless"),
        new FusionDef(SideWeapon.TrackingGun, SideWeapon.BounceGun, "사냥개 탄", "fz_hound", "미사일이 적을 맞히고 다음 적을 쫓아가요 (2번). 물 때마다 그 미사일 피해·속도 +15%", "hd_bounce", "hd_boss", "hd_pack"),
        new FusionDef(SideWeapon.Orbital, SideWeapon.Poison, "독안개 드론", "fz_toxsat", "드론 탄에 맞은 적이 30% 느려지고 독에 걸리며, 주변 적에게 독이 퍼져요", "ts_trail", "ts_more", "ts_ring"),
        new FusionDef(SideWeapon.Lazer, SideWeapon.None, "프리즘 빔", "fz_prism", "360 레이저가 3갈래 무지개 빔이 돼요", "pr_more", "pr_spin", "pr_storm"),
        new FusionDef(SideWeapon.StrLaser, SideWeapon.Sniper, "특이점 저격", "fz_singularity", "블랙홀이 닫히는 순간 저격총이 한가운데를 꿰뚫어요. 모인 적은 모두 치명타, 모인 적이 많을수록 세져요", "sg_charge", "sg_mark", "sg_echo"),
        new FusionDef(SideWeapon.StrLaser, SideWeapon.Bomber, "중력 폭탄", "fz_gravbomb", "블랙홀이 닫히는 순간 그 자리에 거대 폭탄이 떨어져요. 모인 적이 많을수록 크게 터져요", "gv_big", "gv_chain", "gv_mass"),
        // 2026-11-20 사용자 결정: 튕김총 진화 "혜성탄" (내부 이름은 constellation, 종결 "운명의 별자리"). 동작은 CometShot.cs
        new FusionDef(SideWeapon.BounceGun, SideWeapon.None, "혜성탄", "fz_constellation", "혜성 1발이 갈라지지 않고 적 사이를 6번 튕겨요. 뒤따르는 꼬리에 닿은 적도 피해를 받고, 튕길수록 꼬리가 길어져요", "cm_tail", "cm_bounce", "cm_impact"),
        // 2026-11-21 사용자 결정: 운석 합체 2개. 동작은 MeteorGun.cs
        new FusionDef(SideWeapon.Meteor, SideWeapon.Sniper, "별의 낙인", "fz_brand", "저격총이 강한 적 3마리에 낙인을 찍고, 낙인 적마다 운석 3개가 따라 떨어져요. 피할 수 없고 폭발은 모두 치명타, 보스는 추가 피해", "bd_more", "bd_aim", "bd_chain"),
        new FusionDef(SideWeapon.Meteor, SideWeapon.StrLaser, "대충돌", "fz_impact", "운석은 계속 떨어지고, 5초마다 블랙홀이 적이 몰린 곳을 끌어모은 뒤 그 자리에 큰 운석이 떨어져요", "im_well", "im_more", "im_energy"),
    };
    public const int FzGuided = 0, FzSatLaser = 1, FzRailgun = 2, FzPlague = 3, FzSplitDbl = 4, FzHound = 5, FzToxSat = 6, FzPrism = 7, FzSingularity = 8, FzGravBomb = 9,
                     FzConstellation = 10, FzBrand = 11, FzImpact = 12;

    static AugmentDef F(int fusion, string id, string name, AugGrade grade, System.Func<int, string> d)
    {
        var f = Fusions[fusion];
        var def = A(id, name, AugTag.Weapon, grade, L => $"[{f.name}] " + d(L), 0, f.a);
        def.fusion = fusion;
        return def;
    }

    static List<AugmentDef> FusionAugs()
    {
        var list = new List<AugmentDef>();
        for(int i = 0; i < Fusions.Length; i++) {
            var f = Fusions[i];
            var card = A(f.card, f.name, AugTag.Weapon, R, L => FusionCardText(f), 1, f.a);
            card.fusion = i;
            card.fusionCard = true;
            list.Add(card);
        }
        list.Add(F(FzGuided, "gb_double", "이중 폭발", G, L => $"폭탄이 0.5초 뒤 더 크게 한 번 더 터져요\n(피해 {P(GbDouble(L))})"));
        list.Add(F(FzGuided, "gb_missile", "미사일 추가", G, L => $"다른 적을 쫓는 폭탄 +{GbMissile(L)}발"));
        list.Add(F(FzGuided, "gb_fleet", "폭격 편대", R, L => $"쏠 때마다 폭탄 {GbFleet(L)}발이\n서로 다른 적에게 동시에 날아가요"));
        list.Add(F(FzSatLaser, "sat_more", "위성 추가", G, L => $"레이저 드론 +{SatMore(L)}대"));
        list.Add(F(FzSatLaser, "sat_long", "레이저 연장", G, L => $"360 레이저 쏘는 시간 +{SatLong(L):0.#}초"));
        list.Add(F(FzSatLaser, "sat_focus", "궤도 일제 사격", R, L => $"모든 드론이 가장 강한 적 하나를 노려요\n(드론 피해 ×{SatFocus(L):0.#})"));
        list.Add(F(FzRailgun, "rg_charge", "충전 가속", G, L => $"장전 시간 -{P(RgCharge(L))}"));
        list.Add(F(FzRailgun, "rg_blast", "관통 폭발", G, L => $"빔에 뚫린 적마다 작은 폭발\n(빔 피해의 {P(RgBlast(L))})"));
        list.Add(F(FzRailgun, "rg_mega", "초전자포", R, L => $"{RgMegaEvery(L)}번째 발사마다 4배 굵기,\n3배 피해의 거대 빔"));
        list.Add(F(FzPlague, "pb_area", "독 장판 확대", G, L => $"독 지대 크기 +{P(PbArea(L))}"));
        list.Add(F(FzPlague, "pb_toxic", "맹독 폭발", G, L => $"독탄 폭발 피해 +{P(PbToxic(L))},\n맞은 적은 독 3중첩"));
        list.Add(F(FzPlague, "pb_spread", "역병 확산", R, L => $"독 지대가 사라질 때 가까운 적 자리에\n작은 독 지대 {PbSpread(L)}개가 이어져요"));
        list.Add(F(FzSplitDbl, "sd_more", "분열 추가", G, L => $"갈라진 탄이 한 번 더 갈라져요 (+{SdMore(L)}세대)"));
        list.Add(F(FzSplitDbl, "sd_accel", "튕김 가속", G, L => $"갈라진 탄 탄속 +{P(SdAccel(L))} (쌍발총 탄 기준),\n갈라진 탄 피해 70% → 85%"));
        list.Add(F(FzSplitDbl, "sd_endless", "무한 분열", R, L => $"{P(SdEndless(L))} 확률로 탄이 맞혀도 사라지지 않아요"));
        list.Add(F(FzHound, "hd_bounce", "추적 튕김", G, L => $"다음 적을 쫓는 횟수 +{HdBounce(L)}"));
        list.Add(F(FzHound, "hd_boss", "사냥 본능", G, L => $"보스를 먼저 쫓고, 보스 피해 +{P(HdBoss(L))}"));
        list.Add(F(FzHound, "hd_pack", "무리 사냥", R, L => $"튈 때 {P(HdPack(L))} 확률로 새 미사일이 생겨요"));
        list.Add(F(FzToxSat, "ts_trail", "독안개 확대", G, L => $"독이 퍼지는 범위 +{P(TsTrail(L))}"));
        list.Add(F(FzToxSat, "ts_more", "드론 증원", G, L => $"드론 +{TsMore(L)}대"));
        list.Add(F(FzToxSat, "ts_ring", "역병의 고리", R, L => $"독이 반경 {TsRing(L):0.#}칸까지 3중첩으로 퍼져요"));
        list.Add(F(FzPrism, "pr_more", "갈래 추가", G, L => $"빔 +{PrMore(L)}갈래"));
        list.Add(F(FzPrism, "pr_spin", "회전 가속", G, L => $"회전 속도 +{P(PrSpin(L))}"));
        list.Add(F(FzSingularity, "sg_charge", "가속 특이점", G, L => $"블랙홀이 {SgCharge(L):0.#}초 더 자주 열려요"));
        list.Add(F(FzSingularity, "sg_mark", "약점 표시", G, L => $"모인 적 1마리당 저격 피해 +{P(SgMark(L))} 더"));
        list.Add(F(FzSingularity, "sg_echo", "여진", R, L => $"저격 뒤 {P(SgEcho(L))} 확률로 작은 블랙홀이 다시 열려요"));
        list.Add(F(FzGravBomb, "gv_big", "거대 폭탄", G, L => $"폭발 범위 +{P(GvBig(L))}"));
        list.Add(F(FzGravBomb, "gv_chain", "고리 폭발", G, L => $"폭발 뒤 둘레에 작은 폭발 {GvChain(L)}개"));
        list.Add(F(FzGravBomb, "gv_mass", "임계 질량", R, L => $"모인 적 1마리당 폭발 피해 +{P(GvMass(L))} 더"));
        list.Add(F(FzPrism, "pr_storm", "무지개 폭풍", R, L => $"{PrStormEvery(L):0}초마다 3초 동안 빔 12갈래"));
        list.Add(F(FzConstellation, "cm_tail", "긴 꼬리", G, L => $"꼬리 길이·꼬리 피해 +{P(CmTail(L))}"));
        list.Add(F(FzConstellation, "cm_bounce", "궤도 연장", G, L => $"튕김 +{CmBounce(L)}번"));
        list.Add(F(FzConstellation, "cm_impact", "혜성 충돌", R, L => $"마지막 튕김 뒤 혜성이 떨어져 크게 터져요\n(1발 피해의 {P(CmImpact(L))}, 튕긴 적 1마리당 +10%)"));
        list.Add(F(FzBrand, "bd_more", "연속 낙인", G, L => $"낙인을 찍는(인식하는) 적 +{BdMore(L)}"));
        list.Add(F(FzBrand, "bd_aim", "약점 조준", G, L => $"낙인 운석 치명 피해 +{P(BdAim(L))}"));
        list.Add(F(FzBrand, "bd_chain", "처형 낙인", R, L => $"낙인 운석에 적이 죽으면 가까운 적에게\n낙인이 옮겨 운석이 또 떨어져요 (최대 {BdChain(L)}번)"));
        list.Add(F(FzImpact, "im_well", "강한 중력", G, L => $"블랙홀 범위 +{P(ImWell(L))}"));
        list.Add(F(FzImpact, "im_more", "소행성대", G, L => $"블랙홀마다 떨어지는 운석 +{ImMore(L)}"));
        list.Add(F(FzImpact, "im_energy", "충돌 에너지", R, L => $"블랙홀 자리에 떨어지는 큰 운석 피해 +{P(ImEnergy(L))}"));
        // 종결 증강 (합체마다 1개, 프리즘 ★1). 게이지가 차면(약 30초) 자동 발동, 그 무기가 맞힐 때 1% 확률로 바로 발동
        for(int i = 0; i < Fusions.Length; i++) {
            int f = i;
            var u = F(f, UltIds[f], UltNames[f], R, L => UltDescs[f] + "\n(게이지가 차면 자동 발동 · 맞힐 때 1% 확률로 바로)");
            u.maxLevel = 1;
            u.ultimate = true;
            list.Add(u);
        }
        return list;
    }

    // 종결 증강 (Fusions 순서와 같아요)
    public static readonly string[] UltIds = { "ult_guided", "ult_satlaser", "ult_railgun", "ult_plague", "ult_splitdbl", "ult_hound", "ult_toxsat", "ult_prism", "ult_singularity", "ult_gravbomb", "ult_constellation", "ult_brand", "ult_impact" };
    public static readonly string[] UltNames = { "유도탄 세례", "궤도 포격", "천공", "역병의 비", "별똥별 폭풍", "사냥개 무리", "독의 그물", "무지개 심판", "사건의 지평선", "중력 대폭격", "운명의 별자리", "천벌", "소행성 충돌" };
    static readonly string[] UltDescs = {
        "유도탄 수십 발이 사방으로 퍼졌다가 휘어져\n화면의 적 하나하나에게 꽂혀요",
        "위성 6대가 화면 끝까지 닿는 레이저로 화면을 쓸고\n한 점에 모아 하얗게 대폭발",
        "레일건 4연사 뒤 우주선에서 사방으로 초전자포를 쏘고\n꿰뚫린 적 중 체력 30% 이하는 처형해요 (모두 치명타)",
        "독비가 쏟아져 모든 적이 독에 걸리고 느려져요\n독에 걸린 채 죽은 적은 터지며 독을 옮겨요",
        "유성 12개가 쏟아지며 터질 때마다 조각으로 갈라지고\n마지막 거대 유성이 사방으로 터져요",
        "붉은 사냥개 혼 8마리가 적을 차례로 물어뜯다가\n가장 강한 적에게 일제히 덮쳐 한 방에 터뜨려요",
        "독 줄기가 적에서 적으로 번지며 화면을 그물처럼 잇고\n그물이 두 번 맥동하며 모두에게 피해",
        "빛이 모였다가 7색 빔으로 갈라져 화면을 한 바퀴 쓸고\n하얀 충격파로 마무리해요",
        "화면 한가운데 거대 블랙홀이 모든 적을 빨아들이고\n초거대 저격이 꿰뚫어요 (모두 치명타)",
        "화면의 모든 적을 한가운데로 끌어모은 뒤\n거대 폭탄이 연달아 터져요",
        "혜성 12개가 날아가며 황도 12궁을 그리고\n화면 전체가 한 번에 펑 터져요",
        "화면의 모든 적에게 낙인이 찍히고\n운석이 하나하나에게 쏟아져요 (모두 치명타, 마지막은 보스에게 거대 운석)",
        "블랙홀이 화면의 모든 적을 한곳에 끌어모은 뒤\n거대한 충돌로 한꺼번에 터뜨려요",
    };

    static AugmentDefs() => All.AddRange(FusionAugs());

    public static FusionDef FusionOfCard(AugmentDef d) => d != null && d.fusion >= 0 ? Fusions[d.fusion] : null;

    // 무기 A 의 전용 증강 3개 (합체 증강 제외)
    public static List<AugmentDef> WeaponAugs(SideWeapon w) => All.FindAll(d => d.tag == AugTag.Weapon && d.fusion < 0 && d.requiredWeapon == w);

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

    // 합체 재료 표시: 짝 무기가 없으면 "저격총 진화" (레일건·프리즘 빔, 2026-11-20)
    public static string FusionPair(FusionDef f) => f.b == SideWeapon.None ? WeaponName(f.a) + " 진화" : WeaponName(f.a) + " + " + WeaponName(f.b);

    // 합체 카드 설명: 지금 어느 무기로 합체하는지에 따라 달라요 (양쪽 합체, 2026-11-20)
    static string FusionCardText(FusionDef f)
    {
        var m = AugmentManager.Instance;
        var via = m != null ? m.FusionVia(f) : SideWeapon.None;
        if(via == SideWeapon.None) via = f.a;
        string text = $"[합체] {FusionPair(f)}\n{f.effect}\n{WeaponName(f.a)} 위력 ×{FusionPower:0} ({WeaponName(via)} 전용 증강 3개는 사라져요)";
        if(via == f.b && m != null && !m.IsEquipped(f.a)) text += $"\n{WeaponName(f.b)} 자리에 {WeaponName(f.a)}이(가) 들어와요";
        return text;
    }

    public static string WeaponName(SideWeapon w)
    {
        switch(w) {
            case SideWeapon.DoubleGun: return "쌍발총";
            case SideWeapon.Bomber: return "폭격기";
            case SideWeapon.TrackingGun: return "유도탄";
            case SideWeapon.Poison: return "독 비행기";
            case SideWeapon.Orbital: return "드론";
            case SideWeapon.Lazer: return "360 레이저";
            case SideWeapon.StrLaser: return "블랙홀";   // 2026-11-20: 직선 레이저 자리
            case SideWeapon.Sniper: return "저격총";
            case SideWeapon.BounceGun: return "튕김총";
            case SideWeapon.Meteor: return "운석";
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
            case SideWeapon.Meteor: return AugTag.Gravity;   // 하늘에서 떨어지니까 중력
        }
        return null;
    }
}
