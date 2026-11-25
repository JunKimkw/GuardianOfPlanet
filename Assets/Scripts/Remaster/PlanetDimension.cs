using System.Collections.Generic;
using UnityEngine;
using Rule = ExpeditionZones.Rule;

// 행성 차원 (2026-11-25 사용자: "평행 우주를 좀 난이도 있게, 새 차원 하나는 진짜 개어렵게. 대신 보상은 확실하게")
//  0 일반  : 1~14번 행성
//  1 평행  : 15~28번 행성 (1~14번의 두 번째 바퀴). 적 체력 × planet_parallel_hp(30) × 성장 1.32^행성, 행성마다 규칙 1개(질주·강철·무리·광란 차례로),
//            보스는 관문 보스 패턴 2개. 행성을 처음 다 깨면 다이아 + 별의 먼지 보너스
//  2 심연  : 평행 행성을 다 깨면 그 행성마다 열려요. 적 체력 × planet_abyss_hp(1만) × 성장 1.25^행성, 맵마다 규칙 2개, 정예 적, 보스 패턴 3개(각성)
//            보상(골드·강화석·첫 클리어 다이아) × planet_abyss_reward(5). 마지막 맵까지 깨면 '심연 정복': 그 행성 무기 피해 +20% 영구
//            (자유 선택 행성은 모든 무기 +5%) → AugmentManager.AbyssWeaponMul
public static class PlanetDimension
{
    public const int Normal = 0, Parallel = 1, Abyss = 2;
    static readonly Rule[] Order = { Rule.Swift, Rule.Tough, Rule.Swarm, Rule.Frenzy };

    public static int LoopStart => Tuning.GetInt("planet_loop_start", 14);
    public static int Of(int planet, bool abyss) => abyss ? Abyss : (LoopStart > 0 && planet >= LoopStart ? Parallel : Normal);

    public static string Name(int dim) => dim == Abyss ? "심연" : dim == Parallel ? "평행 우주" : "일반 우주";
    public static string Prefix(int dim) => dim == Abyss ? "심연 " : dim == Parallel ? "평행 " : "";
    public static Color Accent(int dim) => dim == Abyss ? new Color(1f, 0.23f, 0.31f) : dim == Parallel ? new Color(0.76f, 0.49f, 1f) : new Color(0.21f, 0.78f, 1f);

    // 규칙: 평행은 행성마다 1개, 심연은 맵마다 2개 (맵이 바뀌면 조합이 바뀌어요)
    public static List<Rule> Rules(int planet, int map, int dim)
    {
        var list = new List<Rule>();
        int k = Mathf.Max(0, planet - LoopStart);
        if(dim == Parallel) list.Add(Order[k % Order.Length]);
        else if(dim == Abyss) {
            list.Add(Order[(k + map) % Order.Length]);
            list.Add(Order[(k + map + 1) % Order.Length]);
        }
        return list;
    }

    // 적 체력 배율 = 기본 × 성장 ^ (평행 행성 번호 0~13) (2026-11-25 사용자: "장기적으로 플레이하게. 며칠이 아니라")
    //  계산 tools/planet_dim_balance.js (예전 원정 시뮬레이터 40회, 하루 30분 × 2번 + 오프라인):
    //  평행 ×30 · 1.32 → 15번 약 2주, 28번 약 2달 반 / 심연 ×1만 · 1.25 → 15번 약 2달 반, 17번 약 5달, 20번부터 반년 넘게
    public static float HpMul(int dim, int planet)
    {
        int k = Mathf.Max(0, planet - LoopStart);
        if(dim == Abyss) return Tuning.Get("planet_abyss_hp", 10000f) * Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_abyss_hp_growth", 1.25f)), k);
        if(dim == Parallel) return Tuning.Get("planet_parallel_hp", 30f) * Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_parallel_hp_growth", 1.32f)), k);
        return 1f;
    }
    // 화면에 보이는 글자: ×30, ×1.2만
    public static string HpText(int dim, int planet)
    {
        float v = HpMul(dim, planet);
        return v >= 10000f ? $"×{v / 10000f:0.#}만" : $"×{v:0.#}";
    }
    public static float RewardMul(int dim) => dim == Abyss ? Tuning.Get("planet_abyss_reward", 5f) : 1f;

    // 규칙 효과 (원정 차원 규칙과 같은 숫자)
    public static float RuleSpeed(List<Rule> r) => r.Contains(Rule.Swift) ? 1.3f : 1f;
    public static float RuleHp(List<Rule> r) => r.Contains(Rule.Tough) ? 1.5f : 1f;
    public static float RuleGold(List<Rule> r) => r.Contains(Rule.Tough) ? 1.3f : 1f;
    public static float RuleCount(List<Rule> r) => r.Contains(Rule.Swarm) ? 1.5f : 1f;
    public static float RuleSpawn(List<Rule> r) => r.Contains(Rule.Frenzy) ? 0.75f : 1f;

    public static string RuleShort(Rule r)
    {
        switch(r) {
            case Rule.Swift: return "질주";
            case Rule.Tough: return "강철";
            case Rule.Swarm: return "무리";
            default: return "광란";
        }
    }
    public static string RulesText(List<Rule> r, string sep = "\n")
    {
        var parts = new List<string>();
        foreach(var x in r) parts.Add(ExpeditionZones.RuleName(x));
        return string.Join(sep, parts);
    }
    public static string RulesShort(List<Rule> r)
    {
        var parts = new List<string>();
        foreach(var x in r) parts.Add(RuleShort(x));
        return string.Join(" + ", parts);
    }
}
