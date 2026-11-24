using UnityEngine;

// 원정 밸런스 공식 (한 곳에 모아 둠). 숫자는 sim/sim.js 시뮬레이터 기준안 v3 (2026-11-16).
// 목표: 초반은 짧고 빠르게(첫 판 약 17분) → 환생마다 약 1.2배씩 길어져서 10회 약 1.3시간, 20회 약 5시간(4~5일)
// 적 체력은 다항식 곡선: 영구 전투력이 2배가 되면 벽이 약 +19% 뒤로 밀려요 (2^(1/4)).
public static class RemasterBalance
{
    // ---- 적
    const float HpBase = 3f, HpPow = 4f;
    const float GoldBase = 1.5f, GoldPow = 3f;   // v2: 적 수가 2배라 마리당 절반
    const float ExpBase = 0.5f, ExpPow = 2.5f;
    const float CurveScale = 10f;           // (1 + 스테이지/10)^n
    public const float BossMul = 8f;        // 10스테이지마다 보스
    public const float GateMul = 15f;       // 50스테이지마다 신의 관문 보스

    static float Curve(int stage, float pow) => Mathf.Pow(1f + Mathf.Max(1, stage) / CurveScale, pow);

    public static double EnemyHp(int stage) => HpBase * (double)Curve(stage, HpPow) * EarlyHpMul(stage);
    // 초반 쉽게 (2026-11-25 사용자: "1~10스테이지는 그냥 몬스터 잡는 건 쉽게, 12마리도 오래 걸려"):
    //  1스테이지 체력 × early_hp_mul(0.35) → early_easy_until(10)스테이지 다음에 × 1. 보스·방어력·오프라인도 같이 (모두 EnemyHp 를 써요). 측정 중엔 그대로
    public static double EarlyHpMul(int stage)
    {
        int until = Tuning.GetInt("early_easy_until", 10);
        if(until <= 0 || stage > until || AugmentManager.MeasureScene != null) return 1.0;
        return Mathf.Lerp(Mathf.Clamp(Tuning.Get("early_hp_mul", 0.35f), 0.01f, 1f), 1f, (Mathf.Max(1, stage) - 1) / (float)until);
    }
    public static double EnemyGold(int stage) => GoldBase * (double)Curve(stage, GoldPow);
    public static double EnemyExp(int stage) => ExpBase * (double)Curve(stage, ExpPow);
    public static double BossHp(int stage) => EnemyHp(stage) * (stage % 50 == 0 ? GateMul : BossMul);

    // 스테이지당 적 수: 6 + 스테이지/5, 최대 60 (v3: 초반 스테이지는 금방 끝나고, 갈수록 길어져요)
    public static int EnemyCountBase(int stage) => Mathf.Min(6 + stage / 5, 60);
    // (2026-11-25 사용자: "1스테이지에서 한 마리씩 나와서 보이기도 전에 죽고 느려") 초반은 적을 더 많고 약하게:
    //  1스테이지 × early_count_mul(2) → early_fade_stage(40)스테이지에서 × 1. 1마리 체력·골드·경험치는 그만큼 나눠서 스테이지 전체 합은 그대로
    public static float EarlyCountMul(int stage)
    {
        if(AugmentManager.MeasureScene != null) return 1f;   // 측정은 예전과 같게
        float fade = Mathf.Max(2f, Tuning.Get("early_fade_stage", 40f));
        return Mathf.Lerp(Mathf.Max(1f, Tuning.Get("early_count_mul", 2f)), 1f, (Mathf.Max(1, stage) - 1) / (fade - 1f));
    }
    public static int EnemyCount(int stage) => Mathf.Max(EnemyCountBase(stage), Mathf.CeilToInt(EnemyCountBase(stage) * EarlyCountMul(stage)));
    // 1마리 체력·골드·경험치 배율 (적 수가 늘어난 만큼 나눠요)
    public static double EnemyUnitMul(int stage) => EnemyCountBase(stage) / (double)EnemyCount(stage);

    // 스폰 간격(초, 적 종류 2개가 번갈아 나오므로 실제 간격은 이 값의 약 절반 이하)
    public static float SpawnSpeed(int stage) => Mathf.Max(1.365f, 3.64f - 0.0182f * stage);

    // 무리로 나오기 (2026-11-25 사용자): 한 번에 spawn_group_min~max(2~4)마리가 뭉쳐서, 간격은 × spawn_group_interval_mul(2)
    //  → 나오는 속도는 평균 무리 수 ÷ 간격 배율 = 3 ÷ 2 = 1.5배. 측정 중엔 예전처럼 1마리씩
    public static int SpawnGroupMin => AugmentManager.MeasureScene != null ? 1 : Mathf.Max(1, Tuning.GetInt("spawn_group_min", 2));
    public static int SpawnGroupMax => AugmentManager.MeasureScene != null ? 1 : Mathf.Max(SpawnGroupMin, Tuning.GetInt("spawn_group_max", 4));
    public static float SpawnGroupIntervalMul => AugmentManager.MeasureScene != null ? 1f : Mathf.Max(0.1f, Tuning.Get("spawn_group_interval_mul", 2f));
    public static float SpawnRateMul => (SpawnGroupMin + SpawnGroupMax) * 0.5f / SpawnGroupIntervalMul;
    // 무리 사이 간격 배율: 초반은 적 수가 늘어난 만큼 더 자주 나와요 (안 그러면 적이 2배라 스테이지가 오히려 길어져요)
    public static float SpawnIntervalMul(int stage) => SpawnGroupIntervalMul / EarlyCountMul(stage);
    // 오프라인 계산용: 예전 1마리씩보다 몇 배 빨리 나오는지
    public static float SpawnRate(int stage) => SpawnRateMul * EarlyCountMul(stage);

    // 스테이지 클리어 보너스 골드 = 그 스테이지 처치 골드의 25%
    public static long StageClearGold(int stage) => (long)(EnemyCountBase(stage) * EnemyGold(stage) * 0.25);

    // ---- 저주 누적: 처치 수가 많아질수록 덜 쌓이게 (처치 100 → 100, 1000 → 501, 10000 → 2512)
    public static float EffectiveKills(long kills) => kills <= 0 ? 0f : 100f * Mathf.Pow(kills / 100f, 0.7f);
    // 저주 누적 상한 (2026-11-25 사용자: "무한 스테이지라 환생만 안 하면 상한까지 가는 게 어렵지 않아, 사기 같아")
    //  예전: +300%, 저주 세트 8이면 상한 없음 → 이제 늘 상한: curse_cap(+100%), 세트 8이면 curse_cap_set8(+150%).
    //  세트 6(×1.5)·심연·타르타로스를 곱한 뒤에도 curse_total_cap(+250%)을 넘지 않아요
    public static float CurseCap => Tuning.Get("curse_cap", 1f);
    public static float CurseCapSet8 => Tuning.Get("curse_cap_set8", 1.5f);
    public static float CurseTotalCap => Tuning.Get("curse_total_cap", 2.5f);

    // 출발 가속으로 건너뛴 스테이지의 처치 수 (누적 증강이 손해 보지 않게)
    public static long KillsBefore(int stage)
    {
        long k = 0;
        for(int s = 1; s < stage; s++) k += EnemyCount(s);
        return k;
    }

    // ---- 환생 트리 "별의 가호"
    public const float StarPowerPerLevel = 1.25f;
    public static int StarPowerCost(int level) => Mathf.FloorToInt(4f * Mathf.Pow(1.3f, level));

    // ---- 증강 뽑는 스테이지: 1~50은 5마다, 51~200은 10마다, 201부터 25마다 (판이 길어져도 넘치지 않게)
    //  (2026-11-25 사용자: "초반에 몬스터 잡는 거 기다리는 거 말고는 아무것도 없어") 1~aug_every_stage_until(10)은 매 스테이지
    public static bool IsAugmentStage(int stage) =>
        stage > 0 && (stage <= Tuning.GetInt("aug_every_stage_until", 0) || (stage <= 50 ? stage % 5 == 0 : stage <= 200 ? stage % 10 == 0 : stage % 25 == 0));

    public static int AugmentStagesBefore(int stage)
    {
        int n = 0;
        for(int s = 1; s < stage; s++) if(IsAugmentStage(s)) n++;
        return n;
    }

    // ---- 환생
    public const int FirstRebirthStage = 40;     // 첫 환생 가능 스테이지 (첫 벽 평균 65, 운이 나빠도 50)
    public const float StardustMul = 2f;          // 별의 먼지 = 2 × (스테이지/10)^1.5

    // ---- 오프라인 진행 (C안): 다음 신의 관문 앞까지, 최대 10시간
    public const float OfflineCapHours = 10f;
    public const float EnemyTravelSeconds = 7f;  // 적이 화면 끝에서 우주선까지 (플레이 테스트로 보정 예정)
    public static bool IsGateStage(int stage) => stage % 50 == 0;

    // ---- 구간의 힘 (2026-11-17): 깊은 스테이지일수록 적 체력과 내 피해에 같은 배율을 곱해요.
    // 양쪽이 같으니 밸런스는 그대로, 숫자만 커져요. 골드에는 곱하지 않아요.
    // 80스테이지(첫 벽 근처)까지는 배율 없음 → 첫 판은 한 발 몇천. 그 뒤로 완만하게: 200 ×약 400, 450 ×약 2천만, 2000 ×약 10^21, 10만 ×약 10^184
    public const double NumberScaleK = 0.6;
    public const int NumberScaleStart = 80;
    public static double NumberScale(int stage) =>
        System.Math.Pow(10, NumberScaleK * System.Math.Max(0, System.Math.Sqrt(Mathf.Max(1, stage)) - System.Math.Sqrt(NumberScaleStart)));
}

// 적 방어력 (2026-11-20 사용자 결정)
// - 받는 피해 = 피해 × 피해 / (피해 + 방어력 × (1 - 관통력)). 피해가 방어력과 같으면 절반, 훨씬 크면 거의 그대로, 작으면 크게 깎여요.
//   그래서 한 방이 센 무기(저격총·폭격기)는 단단한 적에게 강하고, 작은 피해를 자주 주는 무기는 관통력으로 버텨요.
// - 방어력 = 그 스테이지 일반 적 기본 체력 × 비율. 비율은 1스테이지 armor_ratio_start → armor_ramp_stage 에서 armor_ratio_end 까지 늘어요.
//   보스는 × armor_boss_mul. 스테이지 난이도를 체력 대신 방어력으로도 조절할 수 있어요.
// - 관통력(0~1): 무기마다 pen_* + 플레이어 관통력(장비·증강이 나중에 더해요). 1 이면 방어 무시.
public static class Armor
{
    public static float Ratio(int stage)
    {
        float ramp = Mathf.Max(2f, Tuning.Get("armor_ramp_stage", 100f));
        float t = Mathf.Clamp01((Mathf.Max(1, stage) - 1) / (ramp - 1));
        return Mathf.Lerp(Tuning.Get("armor_ratio_start", 0f), Tuning.Get("armor_ratio_end", 0.05f), t);
    }

    // 체력과 같은 단위가 아니라 "구간의 힘" 배율을 곱하기 전 피해 단위예요
    public static double ForStage(int stage, bool boss) =>
        RemasterBalance.EnemyHp(stage) * ExpeditionZones.HpMul(stage) * Ratio(stage) * (boss ? Tuning.Get("armor_boss_mul", 3f) : 1f);

    // 장비·증강 관통력이 들어올 자리 (지금은 0)
    public static float PlayerPen => 0f;

    public static float Pen(TraitsData.WeaponType w)
    {
        float p;
        switch(w) {
            case TraitsData.WeaponType.MainWeapon: p = Tuning.Get("pen_main", 0.5f); break;
            case TraitsData.WeaponType.DoubleGun: p = Tuning.Get("pen_double", 0.3f); break;
            case TraitsData.WeaponType.TrackingMissile: p = Tuning.Get("pen_track", 0.2f); break;
            case TraitsData.WeaponType.SniperGun: p = Tuning.Get("pen_sniper", 0f); break;
            case TraitsData.WeaponType.Bomber: p = Tuning.Get("pen_bomber", 0f); break;
            case TraitsData.WeaponType.BounceGun: p = Tuning.Get("pen_bounce", 0.2f); break;
            case TraitsData.WeaponType.Poison: p = Tuning.Get("pen_poison", 0.7f); break;
            case TraitsData.WeaponType.CircleLaser: p = Tuning.Get("pen_laser", 0.5f); break;
            case TraitsData.WeaponType.StrLaser: p = Tuning.Get("pen_blackhole", 0.5f); break;
            case TraitsData.WeaponType.CodeA: p = Tuning.Get("pen_drone", 0.3f); break;
            case TraitsData.WeaponType.Meteor: p = Tuning.Get("pen_meteor", 0.2f); break;
            default: p = 0f; break;
        }
        return Mathf.Clamp01(p + PlayerPen);
    }

    // 증강 효과(분열 파편·전염 독·연쇄 등)와 종결 증강
    public static float EffectPen => Mathf.Clamp01(Tuning.Get("pen_effect", 0.5f) + PlayerPen);
    public static float UltPen => Mathf.Clamp01(Tuning.Get("pen_ult", 1f) + PlayerPen);

    public static double Apply(double damage, double armor, float pen)
    {
        double a = armor * (1.0 - pen);
        if(a <= 0 || damage <= 0) return damage;
        return damage * damage / (damage + a);
    }
}
