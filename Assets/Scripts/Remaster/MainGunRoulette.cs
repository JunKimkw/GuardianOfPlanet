using System.Collections.Generic;
using UnityEngine;

// 기본총 룰렛 (2026-11-25 사용자 결정)
//  무기가 10스테이지에 열리기 전, 1~roulette_until(9)스테이지 중 증강 스테이지(5)가 아닌 스테이지를 깰 때마다 룰렛을 돌려요 (운에 맡겨요).
//  칸 8개: 공격력 · 연사 · 탄 +1 · 관통 +1 · 치명타 · 탄 크기 · 대박 · 꽝(작은 보상). 결과는 그 원정 동안 쌓이고, 환생하면 초기화.
//  기본총 전용 (2026-11-25 사용자: "공격력 +20% 는 후반에 너무 사기 아니야?" → 모든 무기 몫 rl_share 30% → 0). rl_share 를 올리면 공격력·연사·치명타가 그만큼 모든 무기에도 들어가요.
//  숫자는 tuning.txt rl_* (칸 가중치 rl_w_<칸>)
public partial class AugmentManager
{
    // (2026-11-25 사용자: 탄 크기는 필요 없어요 → "size" 칸 뺌. 예전 저장의 size 결과는 불러올 때 버려요)
    public static readonly string[] RouletteIds = { "dmg", "speed", "shot", "pierce", "crit", "jackpot", "dud" };

    static float RlDmg => Tuning.Get("rl_dmg", 0.2f);
    static float RlSpeed => Tuning.Get("rl_speed", 0.12f);
    static float RlCrit => Tuning.Get("rl_crit", 5f);            // 치명타 확률 %p
    static float RlSize => Tuning.Get("rl_size", 0.2f);
    static float RlJackpotDmg => Tuning.Get("rl_jackpot_dmg", 0.4f);
    static float RlJackpotSpeed => Tuning.Get("rl_jackpot_speed", 0.2f);
    static float RlDud => Tuning.Get("rl_dud", 0.03f);
    static float RlShare => Tuning.Get("rl_share", 0f);
    static string Pct(float v) => $"{v * 100f:0.#}%";

    public static string RouletteName(string id)
    {
        switch(id) {
            case "dmg": return $"공격력\n+{Pct(RlDmg)}";
            case "speed": return $"연사\n+{Pct(RlSpeed)}";
            case "shot": return "탄\n+1발";
            case "pierce": return "관통\n+1";
            case "crit": return $"치명타\n+{RlCrit:0.#}%p";
            case "size": return $"탄 크기\n+{Pct(RlSize)}";
            case "jackpot": return $"대박!\n공격력 +{Pct(RlJackpotDmg)}\n연사 +{Pct(RlJackpotSpeed)}";
            case "dud": return $"꽝\n공격력 +{Pct(RlDud)}";
        }
        return id;
    }

    static float RouletteWeight(string id)
    {
        float d = id == "jackpot" ? 6f : id == "dud" ? 10f : id == "shot" ? 10f : id == "pierce" || id == "size" ? 12f : id == "dmg" ? 18f : 16f;
        return Mathf.Max(0f, Tuning.Get("rl_w_" + id, d));
    }

    readonly List<string> rouletteResults = new List<string>();
    int pendingRoulettes;
    bool rouletteShowing;
    public IReadOnlyList<string> RouletteResults => rouletteResults;

    int RlCount(string id) { int n = 0; foreach(var r in rouletteResults) if(r == id) n++; return n; }
    float RlDamageBonus => RlCount("dmg") * RlDmg + RlCount("jackpot") * RlJackpotDmg + RlCount("dud") * RlDud;
    float RlSpeedBonus => RlCount("speed") * RlSpeed + RlCount("jackpot") * RlJackpotSpeed;

    // 피해 배율 (WeaponBase.HitDamage): 기본총은 전부, 다른 무기는 rl_share 만큼
    public static float RouletteDamage(bool mainGun)
    {
        var m = Instance;
        if(m == null || !m.Active || m.rouletteResults.Count == 0) return 1f;
        float b = m.RlDamageBonus;
        return 1f + (mainGun ? b : b * RlShare);
    }
    // 모든 무기 연사 (AttackSpeedFactor 에 곱해요)
    public static float RouletteSpeedAll { get { var m = Instance; return m == null || !m.Active ? 1f : 1f + m.RlSpeedBonus * RlShare; } }
    // 기본총만 더 (전부 - 모든 무기 몫)
    public static float RouletteMainSpeedExtra { get { var m = Instance; if(m == null || !m.Active) return 1f; float b = m.RlSpeedBonus; return (1f + b) / (1f + b * RlShare); } }
    // 치명타 확률 %p: 모든 무기 몫 / 기본총만 더
    public static float RouletteCritAll { get { var m = Instance; return m == null || !m.Active ? 0f : m.RlCount("crit") * RlCrit * RlShare; } }
    public static float RouletteMainCritExtra { get { var m = Instance; return m == null || !m.Active ? 0f : m.RlCount("crit") * RlCrit * (1f - RlShare); } }
    // 기본총에만
    public static int RouletteShots { get { var m = Instance; return m == null || !m.Active ? 0 : Mathf.Min(m.RlCount("shot"), Tuning.GetInt("rl_shot_max", 3)); } }
    public static int RoulettePierce { get { var m = Instance; return m == null || !m.Active ? 0 : m.RlCount("pierce"); } }
    public static float RouletteSize { get { var m = Instance; return m == null || !m.Active ? 1f : 1f + m.RlCount("size") * RlSize; } }

    public static bool IsRouletteStage(int stage) => stage > 0 && stage <= Tuning.GetInt("roulette_until", 9) && !RemasterBalance.IsAugmentStage(stage);
    public static int RouletteStagesBefore(int stage)
    {
        int n = 0;
        for(int s = 1; s < stage; s++) if(IsRouletteStage(s)) n++;
        return n;
    }

    static string SpinRoulette()
    {
        float total = 0f;
        foreach(var id in RouletteIds) total += RouletteWeight(id);
        float x = Random.value * total;
        foreach(var id in RouletteIds) {
            x -= RouletteWeight(id);
            if(x <= 0f) return id;
        }
        return RouletteIds[0];
    }

    // 스테이지를 깼을 때 (OnStageCleared)
    void QueueRouletteIfStage(int clearedStage)
    {
        if(IsRouletteStage(clearedStage)) pendingRoulettes++;
    }

    // 화면이 비었을 때 (LateUpdate 0.5초마다)
    void TryShowRoulette()
    {
        if(pendingRoulettes <= 0 || rouletteShowing || ui.Busy) return;
        if(DebugArena || MeasureScene != null) { pendingRoulettes = 0; return; }
        pendingRoulettes--;
        string id = SpinRoulette();
        rouletteShowing = true;
        PauseForUI();
        ui.ShowRoulette(RouletteIds, id, () => {
            rouletteResults.Add(id);
            rouletteShowing = false;
            ResumeFromUI();
            ui.RefreshHUD();
        });
    }

    // 환생 출발 가속으로 건너뛴 룰렛은 출발할 때 바로 (결과만)
    void GrantSkippedRoulettes(int startStage, List<string> got)
    {
        int n = RouletteStagesBefore(startStage);
        for(int i = 0; i < n; i++) {
            string id = SpinRoulette();
            rouletteResults.Add(id);
            got.Add("기본총 " + RouletteName(id).Replace("\n", " "));
        }
    }

    void ResetRoulette()
    {
        rouletteResults.Clear();
        pendingRoulettes = 0;
    }

    string SaveRoulette() => string.Join(",", rouletteResults);
    void LoadRoulette(string s)
    {
        rouletteResults.Clear();
        if(string.IsNullOrEmpty(s)) return;
        foreach(var id in s.Split(','))
            if(System.Array.IndexOf(RouletteIds, id) >= 0) rouletteResults.Add(id);
    }
}
