using System.Collections.Generic;
using UnityEngine;

// 장비의 힘 다시 맞추기 (2026-11-25 사용자 결정, 장비_설계서.md 1장)
// 시뮬레이터 tools/equip_balance.js 와 같은 계산이에요. 프리팹 숫자(baseOption)는 그대로 두고 여기서 새 능력치를 만들어요.
//  1. 장비마다 '원래 힘' = 공격력% + 깡공/5000 + 공속 a/(1-a) + 치명 확률/100 + 치명 피해 × 0.3
//  2. 같은 등급·같은 부위 안에서 원래 힘 순서로 품질 q (0 = 가장 나쁨 ~ 1 = 가장 좋음)
//  3. 5칸 목표 배율 M(q): q 0 → 이전 등급 평균, 0.5 → 그 등급 평균, 1 → 다음 등급 평균 × 0.95
//  4. 장비 1개의 몫 p = (M − 1) / 5 × 보정 k(q) 를 원래 능력치 비율대로 나눠요. 깡공은 공격력%로 합쳐요
//  5. 장비 공격력%는 최종 피해에 곱해요 (WeaponBase.HitDamage × DamageMul) → 언제 끼든 효과가 같아요
// 강화: 단계마다 오르는 비율 r 을 (1 + r)^α − 1 로 줄여서, 20강(중간 운)에 능력치가 equip_upgrade_max 배가 되게 해요
public static class EquipBalance
{
    static readonly float[] AVG = { 1.2f, 1.5f, 2f, 3f, 4.5f, 7f };
    const float NEXT_OF_TOP = 10f;
    const float CD_W = 0.3f, FLAT_W = 1f / 5000f;
    static readonly float[] AS_CAP = { 0.03f, 0.05f, 0.07f, 0.09f, 0.11f, 0.13f };     // 장비 하나 공속 최대 (넘는 몫은 공격력%로)
    static readonly float[] GOLD_CAP = { 0.05f, 0.1f, 0.2f, 0.35f, 0.5f, 0.8f };      // 골드·경험치% 최대 (원래 있던 장비만)
    // 탄속 → 방어 관통, 기절 → 보스 피해 (2026-11-25 사용자 승인 '4·C'). 원래 그 능력치가 있던 장비만, 등급별 최대 × (0.6 + 0.4 × 품질)
    static readonly float[] PEN_CAP = { 0.03f, 0.05f, 0.08f, 0.12f, 0.16f, 0.2f };    // 방어 관통 (0.1 = 10%, BulletSpeed 칸에 저장)
    static readonly float[] BOSS_CAP = { 5f, 8f, 12f, 18f, 25f, 35f };                // 보스 피해 % (StunPercent 칸에 저장, 1 = 1%)
    // 보정 k: 등급마다 q = 0 · 0.5 · 1 에서 5칸 배율이 목표가 되게 시뮬레이터로 찾은 값 (node tools/equip_balance.js 마지막 줄)
    static readonly float[,] KQ = {
        { 0.8321f, 0.928f, 1.0551f }, { 0.7271f, 0.8044f, 0.9317f }, { 0.7444f, 0.7551f, 0.8662f },
        { 0.6974f, 0.6695f, 0.7545f }, { 0.6692f, 0.7117f, 0.7905f }, { 0.6037f, 0.6302f, 0.8117f } };

    static float Lo(int g) => g == 0 ? 1.1f : AVG[g - 1];
    static float Hi(int g) => 0.95f * (g == 5 ? NEXT_OF_TOP : AVG[g + 1]);
    public static float TargetMul(int g, float q) => q < 0.5f ? Lo(g) + (AVG[g] - Lo(g)) * q / 0.5f : AVG[g] + (Hi(g) - AVG[g]) * (q - 0.5f) / 0.5f;
    static float K(int g, float q) => q < 0.5f ? KQ[g, 0] + (KQ[g, 1] - KQ[g, 0]) * q / 0.5f : KQ[g, 1] + (KQ[g, 2] - KQ[g, 1]) * (q - 0.5f) / 0.5f;

    // 장비 공격력% → 최종 피해 배율 (낀 장비 5칸 합)
    public static double DamageMul => EquipingManager.Instance != null ? 1.0 + EquipingManager.Instance.Attack_DamagePercent_Equip : 1.0;

    // 방어 관통 합 (0~0.9): 장비 + 능력치 '방어 관통'(옛 탄속 칸, 레벨당 탄속 값 × stat_pen_mul)
    public static float Pen {
        get {
            float p = EquipingManager.Instance != null ? EquipingManager.Instance.Bullet_Speed_Equip : 0f;
            if(StatsManager.instance != null) p += StatsManager.instance.getStatAmount(StatsData.TypeofStat.Attack_Bullet_Speed) * Tuning.Get("stat_pen_mul", 0.2f);
            return Mathf.Clamp(p, 0f, 0.9f);
        }
    }
    // 보스 피해 배율: 장비 + 능력치 '보스 피해'(옛 기절 칸, 레벨당 기절 값 × stat_boss_mul) — % 단위
    public static double BossMul {
        get {
            float b = EquipingManager.Instance != null ? EquipingManager.Instance.StunPower_Equip : 0f;
            if(StatsManager.instance != null) b += StatsManager.instance.getStatAmount(StatsData.TypeofStat.StunPercent) * Tuning.Get("stat_boss_mul", 1.5f);
            return 1.0 + Mathf.Max(0f, b) / 100.0;
        }
    }

    // 화면 글자 바꾸기 (씬 파일은 그대로, 시작할 때 한 번)
    static readonly Dictionary<string, string> Relabels = new Dictionary<string, string> {
        { "총알속도(%)", "방어 관통(%)" }, { "스턴 확률(%)", "보스 피해(%)" },
        { "총알속도", "방어 관통" }, { "스턴 확률", "보스 피해" } };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RelabelScene()
    {
        foreach(var t in Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()) {
            if(t == null || !t.gameObject.scene.IsValid()) continue;   // 씬에 있는 것만 (프리팹 원본은 건드리지 않아요)
            if(t.text != null && Relabels.TryGetValue(t.text.Trim(), out var nt)) t.text = nt;
        }
    }

    // ── 품질 q (같은 등급·부위 안 순위) ──
    static Dictionary<string, float> quality;
    static string KeyOf(Equips e) => string.IsNullOrEmpty(e.id) ? e.EquipName : e.id;

    struct Eq { public float d, a, c, cd; public float Sum => d + a + c + cd; }
    static Eq EqOf(options o) => new Eq {
        d = o.damagePercent + o.damage * FLAT_W,
        a = o.AttackSpeed > 0 ? o.AttackSpeed / Mathf.Max(0.05f, 1 - o.AttackSpeed) : 0,
        c = o.CritChance / 100f,
        cd = o.CritDamage * CD_W };

    static void BuildQuality()
    {
        quality = new Dictionary<string, float>();
        var all = Resources.LoadAll<Equips>("Prefab/Equips");
        var groups = new Dictionary<int, List<Equips>>();
        foreach(var e in all) {
            if(e == null || e.baseOption == null || e.baseOption.option == null) continue;
            int key = (int)e.Grade * 10 + (int)e.TypeOfEquip;
            if(!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Equips>();
            list.Add(e);
        }
        foreach(var list in groups.Values) {
            list.Sort((x, y) => EqOf(x.baseOption.option).Sum.CompareTo(EqOf(y.baseOption.option).Sum));
            for(int k = 0; k < list.Count; k++)
                quality[KeyOf(list[k])] = list.Count > 1 ? k / (float)(list.Count - 1) : 0.5f;
        }
    }

    public static float QualityOf(Equips e)
    {
        if(quality == null) BuildQuality();
        return quality.TryGetValue(KeyOf(e), out float q) ? q : 0.5f;
    }

    // 강화 전 새 능력치 (탄속·기절은 원래 값 그대로)
    public static options Of(Equips e)
    {
        var src = e.baseOption.option;
        int g = Mathf.Clamp((int)e.Grade, 0, 5);
        float q = QualityOf(e);
        float p = (TargetMul(g, q) - 1f) / 5f * K(g, q) * Tuning.Get("equip_power_scale", 1f);
        var eq = EqOf(src);
        float sum = eq.Sum;
        float sd = sum > 0 ? eq.d / sum : 1f, sa = sum > 0 ? eq.a / sum : 0f, sc = sum > 0 ? eq.c / sum : 0f, scd = sum > 0 ? eq.cd / sum : 0f;
        float aspd = Mathf.Min(p * sa, AS_CAP[g]);
        float util = GOLD_CAP[g] * (0.6f + 0.4f * q);
        return new options {
            damage = 0,
            damagePercent = p * (sd + sa) - aspd,
            AttackSpeed = aspd,
            BulletSpeed = src.BulletSpeed > 0 ? PEN_CAP[g] * (0.6f + 0.4f * q) * Tuning.Get("equip_pen_scale", 1f) : 0,
            CritChance = p * sc * 100f,
            CritDamage = p * scd / CD_W,
            StunPercent = src.StunPercent > 0 ? BOSS_CAP[g] * (0.6f + 0.4f * q) * Tuning.Get("equip_boss_scale", 1f) : 0,
            ExtraEXP = src.ExtraEXP > 0 ? util : 0,
            ExtraMoney = src.ExtraMoney > 0 ? util : 0,
        };
    }

    // ── 강화 ──
    static readonly Dictionary<int, float> alphaCache = new Dictionary<int, float>();
    // raw(level) = 원래 강화표 (EquipUpgradeManager). 20강을 중간 운으로 했을 때 능력치 배율이 목표가 되는 α
    public static float UpgradeAlpha(Upgrade_StatsPrefab.StatsType type, Equips.MaterialClass grade, System.Func<int, List<float>> raw)
    {
        bool util = type == Upgrade_StatsPrefab.StatsType.ExtraMoney || type == Upgrade_StatsPrefab.StatsType.ExtraEXP;
        float target = util ? Tuning.Get("equip_upgrade_max_gold", 1.3f) : Tuning.Get("equip_upgrade_max", 1.3f);
        int key = (int)type * 100 + (int)grade * 10 + (util ? 1 : 0);
        float a;
#if !UNITY_EDITOR
        if(alphaCache.TryGetValue(key, out a)) return a;   // 에디터는 tuning.txt 가 바뀔 수 있어서 매번 계산
#endif
        double lnG = 0;
        for(int i = 0; i < 20; i++) {
            var t = raw(i);
            if(t == null || t.Count == 0) continue;
            lnG += System.Math.Log(1 + t[t.Count / 2]);
        }
        a = lnG > 0 ? (float)(System.Math.Log(Mathf.Max(1f, target)) / lnG) : 1f;
        alphaCache[key] = a;
        return a;
    }

    public static List<float> Compress(List<float> rawRates, float alpha)
    {
        var o = new List<float>(rawRates.Count);
        foreach(float r in rawRates) o.Add((float)(System.Math.Pow(1 + r, alpha) - 1));
        return o;
    }
}
