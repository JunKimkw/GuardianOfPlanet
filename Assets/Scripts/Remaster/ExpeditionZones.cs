using System.Collections.Generic;
using UnityEngine;

// 원정 구간·신의 관문·차원 (설계서 v3 2-1, 2-1-1, 2-2)
// - 50스테이지 = 1구간, 행성 20개 = 20구간 (1~1000스테이지)
// - 구간의 끝(50의 배수) = 신의 관문
// - 1000스테이지 이후는 "차원 2, 3…"으로 20구간을 다시 돌아요. 새 그림 없이 색조·이름·규칙·보스 각성으로 달라 보여요.
// - 지금 그림은 행성 14개 분량이라, 15~20번째 행성은 새 그림(P7)이 나올 때까지 기존 행성 그림 + 색조로 임시 표시해요.
public static class ExpeditionZones
{
    public const int ZoneLength = 50;
    public const int ZoneCount = 20;
    public const int ArtZoneCount = 14;                          // 지금 그림이 있는 행성 수 (P7 에서 20으로)
    public const int DimensionLength = ZoneLength * ZoneCount;   // 1000
    public const int EnemiesPerZone = 4;                         // 적 프리팹: 행성마다 4종
    public const int BossesPerZone = 2;                          // 보스 프리팹: 행성마다 2종 (중간 보스, 관문 보스)

    public static int Dimension(int stage) => (Mathf.Max(1, stage) - 1) / DimensionLength + 1;
    public static int Zone(int stage) => ((Mathf.Max(1, stage) - 1) % DimensionLength) / ZoneLength;   // 0~19

    // 그림을 빌려 쓸 행성 번호 (15~20번째 행성은 임시로 1~6번째 그림)
    public static int ArtZone(int zone) => zone % ArtZoneCount;

    // 임시 행성 구분용 색 (그림이 생기면 필요 없어요)
    static readonly float[] PlaceholderHues = { 0.6f, 0.95f, 0.15f, 0.45f, 0.75f, 0.28f };
    public static Color PlaceholderTint(int zone) =>
        zone < ArtZoneCount ? Color.white : Color.Lerp(Color.white, Color.HSVToRGB(PlaceholderHues[(zone - ArtZoneCount) % PlaceholderHues.Length], 0.6f, 1f), 0.45f);

    // 적·배경에 입힐 최종 색 = 임시 행성 색 × 차원 색
    public static Color StageTint(int stage) => PlaceholderTint(Zone(stage)) * DimensionTint(Dimension(stage));
    public static int StageInZone(int stage) => (Mathf.Max(1, stage) - 1) % ZoneLength + 1;            // 1~50
    public static bool IsGate(int stage) => stage > 0 && stage % ZoneLength == 0;
    public static int GateNumber(int stage) => stage / ZoneLength;                                    // 1, 2, 3 …

    public static string PlanetName(int zone)
    {
        if(zone >= ArtZoneCount) return $"미지의 행성 {zone + 1}";   // 15~20번째: 이름은 P7 에서
        var sm = StageManager.instance;
        if(sm != null && sm.planets != null && zone < sm.planets.Count && sm.planets[zone] != null)
            return sm.planets[zone].PlanetName;
        return $"행성 {zone + 1}";
    }

    // ---- 차원
    static readonly string[] Prefixes = { "", "심연의", "황혼의", "혹한의", "작열의", "공허의", "폭풍의", "망각의" };
    static readonly float[] Hues = { 0f, 0.78f, 0.04f, 0.55f, 0.1f, 0.68f, 0.33f, 0.9f };

    public static string DimensionPrefix(int dim)
    {
        if(dim <= 1) return "";
        int i = (dim - 1) % (Prefixes.Length - 1) + 1;
        int loop = (dim - 2) / (Prefixes.Length - 1);
        return loop > 0 ? $"{Prefixes[i]}{loop + 1}" : Prefixes[i];
    }

    // 차원 1은 원래 색, 2부터는 차원마다 다른 색조를 섞어요
    public static Color DimensionTint(int dim)
    {
        if(dim <= 1) return Color.white;
        float h = Hues[(dim - 1) % (Hues.Length - 1) + 1];
        return Color.Lerp(Color.white, Color.HSVToRGB(h, 0.75f, 1f), 0.55f);
    }

    // ---- 차원 규칙 (차원 2부터 1개, 차원 4부터 2개)
    public enum Rule { Swift, Tough, Swarm, Frenzy }
    static readonly Rule[] RuleOrder = { Rule.Swift, Rule.Tough, Rule.Swarm, Rule.Frenzy };

    public static List<Rule> Rules(int stage)
    {
        var list = new List<Rule>();
        int dim = Dimension(stage);
        if(dim >= 2) list.Add(RuleOrder[(dim - 2) % RuleOrder.Length]);
        if(dim >= 4) {
            var r2 = RuleOrder[(dim + 1) % RuleOrder.Length];
            if(!list.Contains(r2)) list.Add(r2);
        }
        return list;
    }

    public static string RuleName(Rule r)
    {
        switch(r) {
            case Rule.Swift: return "질주: 적 이동속도 +30%";
            case Rule.Tough: return "강철: 적 체력 +50%, 골드 +30%";
            case Rule.Swarm: return "무리: 적 수 +50%";
            default: return "광란: 적이 더 빨리 나와요";
        }
    }

    public static float SpeedMul(int stage) => Rules(stage).Contains(Rule.Swift) ? 1.3f : 1f;
    public static float HpMul(int stage) => Rules(stage).Contains(Rule.Tough) ? 1.5f : 1f;
    public static float GoldMul(int stage) => Rules(stage).Contains(Rule.Tough) ? 1.3f : 1f;
    public static float CountMul(int stage) => Rules(stage).Contains(Rule.Swarm) ? 1.5f : 1f;
    public static float SpawnMul(int stage) => Rules(stage).Contains(Rule.Frenzy) ? 0.75f : 1f;

    // 관문 보스 각성 단계 (차원 2 = ★1)
    public static int Awakening(int stage) => Dimension(stage) - 1;

    // ---- 화면 표시
    public static string ZoneLabel(int stage)
    {
        int dim = Dimension(stage);
        string planet = PlanetName(Zone(stage));
        string head = dim >= 2 ? $"차원 {dim} · {DimensionPrefix(dim)} {planet}" : planet;
        return $"{head} 구간 {StageInZone(stage)}/{ZoneLength}";
    }

    // ---- 배경 색조 (원래 색을 기억해 두고 곱해요)
    static readonly Dictionary<Object, Color> originalColors = new Dictionary<Object, Color>();

    public static void TintBackground(GameObject grid, Color tint)
    {
        if(grid == null) return;
        foreach(var tm in grid.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true)) {
            if(!originalColors.TryGetValue(tm, out var c)) originalColors[tm] = c = tm.color;
            tm.color = c * tint;
        }
        foreach(var sr in grid.GetComponentsInChildren<SpriteRenderer>(true)) {
            if(!originalColors.TryGetValue(sr, out var c)) originalColors[sr] = c = sr.color;
            sr.color = c * tint;
        }
    }
}
