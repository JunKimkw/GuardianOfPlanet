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

    public static double EnemyHp(int stage) => HpBase * (double)Curve(stage, HpPow);
    public static double EnemyGold(int stage) => GoldBase * (double)Curve(stage, GoldPow);
    public static double EnemyExp(int stage) => ExpBase * (double)Curve(stage, ExpPow);
    public static double BossHp(int stage) => EnemyHp(stage) * (stage % 50 == 0 ? GateMul : BossMul);

    // 스테이지당 적 수: 6 + 스테이지/5, 최대 60 (v3: 초반 스테이지는 금방 끝나고, 갈수록 길어져요)
    public static int EnemyCount(int stage) => Mathf.Min(6 + stage / 5, 60);

    // 스폰 간격(초, 적 종류 2개가 번갈아 나오므로 실제 간격은 이 값의 약 절반 이하)
    public static float SpawnSpeed(int stage) => Mathf.Max(1.365f, 3.64f - 0.0182f * stage);

    // 스테이지 클리어 보너스 골드 = 그 스테이지 처치 골드의 25%
    public static long StageClearGold(int stage) => (long)(EnemyCount(stage) * EnemyGold(stage) * 0.25);

    // ---- 저주 누적: 처치 수가 많아질수록 덜 쌓이게 (처치 100 → 100, 1000 → 501, 10000 → 2512)
    public static float EffectiveKills(long kills) => kills <= 0 ? 0f : 100f * Mathf.Pow(kills / 100f, 0.7f);
    public const float CurseCap = 3f;       // 누적 피해 상한 +300% (시너지·타르타로스 배율은 그 위에)

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
    public static bool IsAugmentStage(int stage) =>
        stage > 0 && (stage <= 50 ? stage % 5 == 0 : stage <= 200 ? stage % 10 == 0 : stage % 25 == 0);

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
}
