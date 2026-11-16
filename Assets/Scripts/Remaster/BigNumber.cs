using System;
using UnityEngine;

// 우주 단위 숫자 표시 (2026-11-17 결정). 데미지·적 체력용이에요. 골드는 예전 표시(GameManager.MoneyString)를 그대로 써요.
// - 10만 미만: 그대로 (예: 45,000)
// - 10만 이상: 유효숫자 3자리 + 단위 (예: 350Sk, 1.25Cm, 12.5Gx)
// - 단위는 1000배마다: Sk Cm Mn Pl St Nv Nb Cl Gx Qs Vd Cs Uv Mv Dm Et If (17단계)
//   그 다음은 접두어 H(Hyper) U(Ultra) O(Omega) A(Apex) X(Xeno)를 붙여 다시 돌아요 → 총 102단계(약 10^306)
public static class BigNumber
{
    public static readonly string[] Units = { "Sk", "Cm", "Mn", "Pl", "St", "Nv", "Nb", "Cl", "Gx", "Qs", "Vd", "Cs", "Uv", "Mv", "Dm", "Et", "If" };
    public static readonly string[] Prefixes = { "", "H", "U", "O", "A", "X" };
    public const double PlainBelow = 100000;
    public static int TierCount => Units.Length * Prefixes.Length;   // 102

    // 단위 단계 (0 = 단위 없음, 1 = Sk, 2 = Cm …)
    public static int Tier(double v)
    {
        v = Math.Abs(v);
        if(v < PlainBelow || double.IsNaN(v)) return 0;
        if(double.IsInfinity(v)) return TierCount + 1;
        return Math.Min(TierCount + 1, (int)Math.Floor(Math.Log10(v) / 3));
    }

    public static string UnitName(int tier)
    {
        if(tier <= 0) return "";
        int i = tier - 1;
        return Prefixes[i / Units.Length] + Units[i % Units.Length];
    }

    public static string Format(double v)
    {
        if(double.IsNaN(v)) return "0";
        if(v < 0) return "-" + Format(-v);
        if(v < PlainBelow) return Math.Floor(v).ToString("#,##0");

        int tier = Tier(v);
        if(tier > TierCount) return "MAX";

        double m = v / Math.Pow(10, tier * 3);
        // 반올림하다 1000이 되면 다음 단위로
        if(Math.Round(m, m < 10 ? 2 : m < 100 ? 1 : 0) >= 1000) {
            tier++;
            if(tier > TierCount) return "MAX";
            m /= 1000;
        }
        string num = m < 10 ? m.ToString("0.##") : m < 100 ? m.ToString("0.#") : m.ToString("0");
        return num + UnitName(tier);
    }

    // 단위가 오를수록 바뀌는 색: 하양 → 초록(Pl) → 파랑(Gx) → 보라(Uv) → 금색(If), 접두어 바퀴마다 더 진하게
    public static Color TierColor(double v)
    {
        int tier = Tier(v);
        if(tier <= 0) return Color.white;
        int baseTier = (tier - 1) % Units.Length + 1;   // 1~17
        int loop = (tier - 1) / Units.Length;           // 0~5
        Color c;
        if(baseTier >= 17) c = new Color(1f, 0.84f, 0.25f);        // If: 금색
        else if(baseTier >= 13) c = new Color(0.78f, 0.5f, 1f);    // Uv~Et: 보라
        else if(baseTier >= 9) c = new Color(0.4f, 0.7f, 1f);      // Gx~Cs: 파랑
        else if(baseTier >= 4) c = new Color(0.45f, 1f, 0.55f);    // Pl~Cl: 초록
        else c = Color.white;                                       // Sk~Mn
        if(loop > 0) {
            Color loopTint = loop == 1 ? new Color(1f, 0.35f, 0.3f) : loop == 2 ? new Color(0.4f, 0.95f, 1f) : Color.HSVToRGB((Time.time * 0.5f) % 1f, 0.6f, 1f);
            c = Color.Lerp(c, loopTint, 0.45f);
        }
        return c;
    }
}
