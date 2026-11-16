using System;
using UnityEngine;

// 오프라인 진행 (설계서 v3 9장, C안 2026-11-16)
// - 꺼져 있던 시간(최대 10시간) 동안 원정을 자동으로 진행한 것으로 계산해요. 실제 전투는 돌리지 않아요.
// - 다음 신의 관문(50의 배수) 앞, 또는 적이 새기 시작하는 벽에서 멈춰요.
// - 넘은 스테이지의 골드·경험치를 주고, 증강 선택권은 대기열에 쌓아서 들어왔을 때 직접 골라요.
// - 계산에 쓰는 "내 초당 피해"는 실제 원정 전투에서 잰 값이에요(적이 있을 때만, 넘친 피해 제외).
public class OfflineProgress : MonoBehaviour
{
    const string KeyLastSeen = "Offline_LastSeenUtc";
    const string KeyDps = "Offline_Dps";
    const float MinAwaySeconds = 60f;      // 1분 안쪽 자리 비움은 무시
    const float BossDpsShare = 0.6f;       // 보스 한 마리에는 전체 피해의 일부만 들어간다고 봐요
    const float StampInterval = 15f;

    public static OfflineProgress Instance { get; private set; }

    // ---- 초당 피해 측정
    static double windowDamage;
    static float windowTime;
    double dpsEstimate;
    float stampTimer;
    bool checkPending = true;   // 켜졌을 때 / 돌아왔을 때 한 번 계산

    void Awake()
    {
        Instance = this;
        windowDamage = 0;
        windowTime = 0;
        double.TryParse(PlayerPrefs.GetString(KeyDps, "0"), out dpsEstimate);
    }

    // Enemy.OnHit 에서 불러요 (실제로 깎인 체력만)
    public static void RecordDamage(double dealt)
    {
        if(dealt > 0) windowDamage += dealt;
    }

    static bool ExpeditionRunning =>
        InfiniteStage.Instance != null && InfiniteStage.Instance.IsRunning &&
        AugmentManager.Instance != null && AugmentManager.Instance.IsLoaded;

    void Update()
    {
        if(!ExpeditionRunning) return;

        // 적이 필드에 있을 때만 시간 측정 (결과 화면·대기 시간은 빼요)
        var em = EnemyManager.Instance;
        if(em != null && Time.timeScale > 0.01f && em.enemyData.Count + em.bossData.Count > 0)
            windowTime += Time.deltaTime;
        if(windowTime >= 10f) {
            double w = windowDamage / windowTime;
            dpsEstimate = dpsEstimate <= 0 ? w : dpsEstimate * 0.8 + w * 0.2;
            windowDamage = 0;
            windowTime = 0;
            PlayerPrefs.SetString(KeyDps, dpsEstimate.ToString("R"));
        }

        if(checkPending) {
            checkPending = false;
            Apply();
        }

        // 갑자기 꺼져도 마지막 시각이 남게 주기적으로 기록
        stampTimer += Time.unscaledDeltaTime;
        if(stampTimer >= StampInterval) {
            stampTimer = 0;
            Stamp();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if(paused) Stamp();
        else checkPending = true;
    }

    void OnApplicationQuit() => Stamp();

    static void Stamp()
    {
        if(!ExpeditionRunning) return;
        PlayerPrefs.SetString(KeyLastSeen, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    void Apply()
    {
        if(!long.TryParse(PlayerPrefs.GetString(KeyLastSeen, ""), out long ticks)) { Stamp(); return; }
        double away = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
        Stamp();
        if(away < MinAwaySeconds || dpsEstimate <= 0) return;

        var result = Simulate(InfiniteStage.Instance.CurrentStage, Math.Min(away, RemasterBalance.OfflineCapHours * 3600.0), dpsEstimate, InfiniteStage.Instance.playerHealth);
        if(result.cleared <= 0) {
            AugmentManager.Instance.UI.Toast($"자리를 비운 동안 {StopReason(result)}", new Color(0.8f, 0.85f, 1f));
            return;
        }

        GameManager.Enemy_Dead_Money_UP(result.gold);
        Player.instance.PlayerEXPUP(result.exp);
        AugmentManager.Instance.AddOfflineKills(result.kills, result.bosses);

        int from = InfiniteStage.Instance.CurrentStage;
        int to = from + result.cleared;
        // 넘은 스테이지마다 클리어 처리 (증강 선택권은 대기열로)
        for(int s = from; s < to; s++) AugmentManager.Instance.OnStageCleared(s);
        InfiniteStage.Instance.JumpToStage(to);

        // 토스트는 두 줄까지라 나눠서 보여 줘요
        var ui = AugmentManager.Instance.UI;
        ui.Toast($"자리를 비운 동안 {from} → {to}스테이지!\n골드 +{GameManager.MoneyString(result.gold)} · 경험치 +{GameManager.MoneyString(result.exp)}",
            new Color(1f, 0.85f, 0.4f), true);
        string reason = StopReason(result);
        if(reason.Length > 0) ui.Toast(reason, new Color(0.8f, 0.85f, 1f));
    }

    static string StopReason(Result r) =>
        r.stop == Stop.Gate ? $"{r.stopStage}스테이지 신의 관문은 직접 도전하세요!"
        : r.stop == Stop.Wall ? "더 강해져야 다음 스테이지를 넘을 수 있어요"
        : "";

    public enum Stop { Time, Gate, Wall }

    public struct Result
    {
        public int cleared;
        public long gold, exp, kills;
        public int bosses;
        public Stop stop;
        public int stopStage;
    }

    // 지금 스테이지부터 시간이 허락하는 만큼 진행. 시뮬레이터(sim.js)와 같은 규칙이에요.
    public static Result Simulate(int startStage, double seconds, double dps, int shield)
    {
        var r = new Result { stop = Stop.Time };
        float goldMul = 1f + Player.GetStatsByType(Upgrade_StatsPrefab.StatsType.ExtraMoney);
        float expMul = 1f + Player.GetStatsByType(Upgrade_StatsPrefab.StatsType.ExtraEXP);
        long goldFlat = EquipingManager.Instance != null ? (long)EquipingManager.Instance.ExtraGoldByNumber_Equip : 0;
        long expFlat = EquipingManager.Instance != null ? (long)EquipingManager.Instance.ExtraEXPByNumber_Equip : 0;
        int leakLimit = Mathf.Max(1, shield);

        for(int s = startStage; ; s++) {
            if(RemasterBalance.IsGateStage(s)) { r.stop = Stop.Gate; r.stopStage = s; break; }

            int n = RemasterBalance.EnemyCount(s);
            double hp = RemasterBalance.EnemyHp(s);
            double spawn = n * RemasterBalance.SpawnSpeed(s) / 2.0;   // 적 종류 2개가 번갈아 나와요
            double canKill = dps / hp * (spawn + RemasterBalance.EnemyTravelSeconds);
            if(n - canKill >= leakLimit) { r.stop = Stop.Wall; r.stopStage = s; break; }

            double t = Math.Max(spawn * 0.6, n * hp / dps);
            bool boss = s % 10 == 0;
            if(boss) {
                double bt = RemasterBalance.BossHp(s) / (dps * BossDpsShare);
                if(bt > 30) { r.stop = Stop.Wall; r.stopStage = s; break; }
                t += bt;
            }
            if(t > seconds) break;
            seconds -= t;

            double g = n * (RemasterBalance.EnemyGold(s) + goldFlat) * goldMul + RemasterBalance.StageClearGold(s);
            double x = n * (RemasterBalance.EnemyExp(s) + expFlat) * expMul;
            if(boss) {
                g += (RemasterBalance.EnemyGold(s) * 20 + goldFlat) * goldMul;
                x += (RemasterBalance.EnemyExp(s) * 20 + expFlat) * expMul;
                r.bosses++;
            }
            r.gold = (long)Math.Min(long.MaxValue / 2, r.gold + g);
            r.exp = (long)Math.Min(long.MaxValue / 2, r.exp + x);
            r.kills += n + (boss ? 1 : 0);
            r.cleared++;
        }
        return r;
    }
}
