using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 장비 세트 효과 (2026-11-25 사용자 승인 'B': 예전엔 화면에만 있고 효과가 없었어요)
// - 같은 세트 장비를 2·3·4·5개 끼면 효과가 커져요. 세트 크기 = 그 세트 장비가 들어가는 부위 수 (같은 부위 둘이면 하나로 세요)
// - 효과 크기 = 그 세트 평균 등급의 '장비 1개 몫' × (2개 0.5 · 3개 1 · 4개 1.5 · 5개 2) × equip_set_scale
//   (장비 1개 몫 = EquipBalance 의 (등급 평균 배율 − 1) / 5 와 같은 단위) → 5개 세트를 다 끼면 장비 2개만큼 더 강해져요
// - 세트마다 주제 능력치 하나 (아래 표)
// 2026-11-26 사용자 "세트효과 밸런스도 맞춰 주라" (시뮬레이터 tools/set_balance.js 와 같은 계산):
//   공격력 세트는 그대로. 그 세트 평균 등급의 '보통 5칸'(LOAD)에 공격력 세트를 더했을 때 오르는 배율 R 을 기준으로
//   공속·치명 확률·치명 피해 세트는 같은 보통 5칸에서 같은 R 이 되는 값, 보스 피해는 (R − 1) × 1.5 (보스에게만이라)
//   방어 관통·골드·경험치는 '그 등급 장비 1개 평균 값 × 단계 몫'. → 예전처럼 상한에 걸려 2개·3개 효과가 같아지지 않아요
public static class EquipSets
{
    public enum Theme { Damage, AttackSpeed, Crit, CritDamage, Boss, Pen, Gold, Exp }

    static readonly Dictionary<Equips.SetType, Theme> Themes = new Dictionary<Equips.SetType, Theme> {
        { Equips.SetType.Beginner_Set, Theme.Damage }, { Equips.SetType.Wood_Set, Theme.Gold }, { Equips.SetType.Jungle_Set, Theme.Exp },
        { Equips.SetType.Iron_Set, Theme.Pen }, { Equips.SetType.Warrior_Set, Theme.Damage }, { Equips.SetType.Hunter_Set, Theme.Boss },
        { Equips.SetType.Magician_Set, Theme.CritDamage }, { Equips.SetType.Sea_Set, Theme.AttackSpeed }, { Equips.SetType.Blood_Set, Theme.Crit },
        { Equips.SetType.Dark_Set, Theme.Boss }, { Equips.SetType.Ice_Set, Theme.AttackSpeed }, { Equips.SetType.Gold_Set, Theme.Gold },
        { Equips.SetType.Devil_Set, Theme.CritDamage }, { Equips.SetType.Evil_Set, Theme.Pen }, { Equips.SetType.Honor_Set, Theme.Damage },
        { Equips.SetType.ME_Set, Theme.Crit }, { Equips.SetType.Aphrodite_Set, Theme.Crit }, { Equips.SetType.Artemis_Set, Theme.AttackSpeed },
        { Equips.SetType.Hercules_Set, Theme.Boss }, { Equips.SetType.Hestia_Set, Theme.CritDamage }, { Equips.SetType.Pan_Set, Theme.Exp },
        { Equips.SetType.Cerberus_Set, Theme.Pen }, { Equips.SetType.Argos_Set, Theme.Crit }, { Equips.SetType.Atena_Set, Theme.Damage },
        { Equips.SetType.Gaea_Set, Theme.Gold }, { Equips.SetType.Hydra_Set, Theme.CritDamage }, { Equips.SetType.Ares_Set, Theme.Boss },
        { Equips.SetType.Poseidon_Set, Theme.AttackSpeed }, { Equips.SetType.Hephaistus_Set, Theme.Pen }, { Equips.SetType.Hades_Set, Theme.Damage },
        { Equips.SetType.Zeus_Set, Theme.Boss }, { Equips.SetType.Demeter_Set, Theme.Exp },
    };
    static readonly float[] AVG = { 1.2f, 1.5f, 2f, 3f, 4.5f, 7f };   // EquipBalance 와 같은 등급 평균 배율

    // 세트 정보 (프리팹에서 한 번): 부위 수, 평균 등급
    struct Info { public int size; public float grade; }
    static Dictionary<Equips.SetType, Info> infos;
    static void Build()
    {
        infos = new Dictionary<Equips.SetType, Info>();
        var slots = new Dictionary<Equips.SetType, HashSet<int>>();
        var gsum = new Dictionary<Equips.SetType, float>();
        var gcnt = new Dictionary<Equips.SetType, int>();
        foreach(var e in Resources.LoadAll<Equips>("Prefab/Equips")) {
            if(e == null || e.setType == Equips.SetType.None) continue;
            if(!slots.ContainsKey(e.setType)) { slots[e.setType] = new HashSet<int>(); gsum[e.setType] = 0; gcnt[e.setType] = 0; }
            slots[e.setType].Add((int)e.TypeOfEquip);
            gsum[e.setType] += (int)e.Grade; gcnt[e.setType]++;
        }
        foreach(var k in slots.Keys) infos[k] = new Info { size = slots[k].Count, grade = gsum[k] / Mathf.Max(1, gcnt[k]) };
    }
    static Info InfoOf(Equips.SetType t) { if(infos == null) Build(); return infos.TryGetValue(t, out var i) ? i : new Info { size = 0, grade = 0 }; }
    public static int SizeOf(Equips.SetType t) => InfoOf(t).size;

    static float Share(int n) => n >= 5 ? 2f : n == 4 ? 1.5f : n == 3 ? 1f : n == 2 ? 0.5f : 0f;
    // 평균 등급 사이는 직선으로
    static float UnitOf(float grade)
    {
        int lo = Mathf.Clamp(Mathf.FloorToInt(grade), 0, 5), hi = Mathf.Min(5, lo + 1);
        float avg = Mathf.Lerp(AVG[lo], AVG[hi], grade - lo);
        return (avg - 1f) / 5f;
    }
    public static float Power(Equips.SetType t, int n) => UnitOf(InfoOf(t).grade) * Share(Mathf.Min(n, InfoOf(t).size)) * Tuning.Get("equip_set_scale", 1f);

    // 몫 p → 그 세트 능력치 값 (EquipingManager 단위)
    // 등급별 보통 5칸 (중간 품질 · 잠재력 평균): 공격력%, 공속, 치명 확률(0~1, 기본 10% 포함), 치명 피해 — set_balance.js 의 LOAD
    static readonly float[,] LOAD = {
        { 0.0611f, 0.0833f, 0.133f, 0.0667f }, { 0.1184f, 0.202f, 0.163f, 0.1004f }, { 0.3017f, 0.1612f, 0.3009f, 0.4072f },
        { 0.7073f, 0.2923f, 0.1847f, 1.0472f }, { 1.0826f, 0.2311f, 0.1972f, 3.1193f }, { 2.7247f, 0.1431f, 0.185f, 3.3882f } };
    static readonly float[] PEN_ONE = { 0.03f, 0.05f, 0.08f, 0.12f, 0.16f, 0.2f };      // EquipBalance 장비 1개 최대 (× 0.8 = 평균)
    static readonly float[] GOLD_ONE = { 0.05f, 0.1f, 0.2f, 0.35f, 0.5f, 0.8f };
    static float Lerp6(float[] a, float gr) { int lo = Mathf.Clamp(Mathf.FloorToInt(gr), 0, 5), hi = Mathf.Min(5, lo + 1); return Mathf.Lerp(a[lo], a[hi], gr - lo); }
    static float Load(int k, float gr) { int lo = Mathf.Clamp(Mathf.FloorToInt(gr), 0, 5), hi = Mathf.Min(5, lo + 1); return Mathf.Lerp(LOAD[lo, k], LOAD[hi, k], gr - lo); }

    // n 개 낀 효과 (EquipingManager 단위: 공격력·공속·관통·골드·경험치 0.1 = 10%, 치명 확률·보스 피해 = %, 치명 피해 1 = 100%)
    public static float ValueOf(Equips.SetType t, int n)
    {
        var info = InfoOf(t);
        float gr = info.grade, s = Share(Mathf.Min(n, info.size)) * Tuning.Get("equip_set_scale", 1f);
        if(s <= 0f) return 0f;
        float D = Load(0, gr), A = Load(1, gr), C = Load(2, gr), CD = Load(3, gr);
        float dmg = UnitOf(gr) * s;
        float R = (1f + D + dmg) / (1f + D);   // 공격력 세트가 보통 5칸을 몇 배로 만드나
        switch(ThemeOf(t)) {
            case Theme.Damage: return dmg;
            case Theme.AttackSpeed: return Mathf.Min(Tuning.Get("player_as_cap", 0.75f) - A, (1f - A) * (1f - 1f / R));   // 1/(1−공속) 이 R 배
            case Theme.Crit: return (R - 1f) * (1f + C * (1f + CD)) / (1f + CD) * 100f;
            case Theme.CritDamage: return (R - 1f) * (1f + C * (1f + CD)) / C;
            case Theme.Boss: return 150f * (R - 1f);
            case Theme.Pen: return Lerp6(PEN_ONE, gr) * 0.8f * s;
            default: return Lerp6(GOLD_ONE, gr) * 0.8f * s;   // 골드·경험치
        }
    }

    public static Theme ThemeOf(Equips.SetType t) => Themes.TryGetValue(t, out var th) ? th : Theme.Damage;

    // 낀 장비에서 세트별 개수
    static readonly Dictionary<Equips.SetType, int> counts = new Dictionary<Equips.SetType, int>();
    static readonly HashSet<(Equips.SetType, int)> worn = new HashSet<(Equips.SetType, int)>();   // 낀 (세트, 부위)
    public static bool IsWorn(Equips.SetType t, int part) => worn.Contains((t, part));
    public static int EquippedCount(Equips.SetType t) => counts.TryGetValue(t, out int n) ? n : 0;

    // EquipingManager.setEquipDamage 끝에서 불러요
    public static void Apply(EquipingManager m, params EquipingData[] slots)
    {
        counts.Clear(); worn.Clear();
        var seen = new HashSet<int>();
        foreach(var s in slots) {
            if(s == null || s.equipment == null || s.equipment.equipData == null) continue;
            var e = s.equipment.equipData;
            if(e.setType == Equips.SetType.None || !seen.Add((int)e.TypeOfEquip)) continue;
            counts[e.setType] = EquippedCount(e.setType) + 1;
            worn.Add((e.setType, (int)e.TypeOfEquip));
        }
        foreach(var kv in counts) {
            if(kv.Value < 2) continue;
            float v = ValueOf(kv.Key, kv.Value);
            switch(ThemeOf(kv.Key)) {
                case Theme.Damage: m.Attack_DamagePercent_Equip += v; break;
                case Theme.AttackSpeed: m.Attack_Speed_Equip += v; break;
                case Theme.Crit: m.Critical_Chance_Equip += v; break;
                case Theme.CritDamage: m.Critical_Damage_Equip += v; break;
                case Theme.Boss: m.StunPower_Equip += v; break;      // 보스 피해 칸 (EquipBalance.BossMul)
                case Theme.Pen: m.Bullet_Speed_Equip += v; break;    // 방어 관통 칸 (EquipBalance.Pen)
                case Theme.Gold: m.ExtraGold_Equip += v; break;
                case Theme.Exp: m.ExtraEXP_Equip += v; break;
            }
        }
    }

    static string Name(Theme th) => th switch {
        Theme.Damage => "공격력", Theme.AttackSpeed => "공격 속도", Theme.Crit => "치명 확률", Theme.CritDamage => "치명 피해",
        Theme.Boss => "보스 피해", Theme.Pen => "방어 관통", Theme.Gold => "골드", _ => "경험치" };
    static string Fmt(Theme th, float v) => th == Theme.Crit || th == Theme.Boss ? $"+{v:0.#}%" : $"+{v * 100f:0.#}%";

    // 장비 창 한 줄: "하데스 세트 2/4 · 2개 공격력 +12% · 4개 +36%"
    public static string Summary(Equips e)
    {
        if(e == null || e.setType == Equips.SetType.None) return "";
        int size = SizeOf(e.setType);
        if(size < 2) return "";
        var th = ThemeOf(e.setType);
        string name = EquipManager.Instance != null ? EquipManager.Instance.setSetNameInKorean(e.setType) : e.setType.ToString();
        int now = EquippedCount(e.setType);
        var parts = new List<string>();
        for(int n = 2; n <= size; n++) {
            string seg = $"{n}개 {Fmt(th, ValueOf(e.setType, n))}";
            parts.Add(n == now ? $"<color=#ffd23f>{seg}</color>" : seg);
        }
        return $"{name} {now}/{size} · {Name(th)} " + string.Join(" · ", parts);
    }

    // ---- 장비 창 위 '지금 낀 세트' 띠 (2026-11-26 사용자 B안, 그리기는 EquipSetBarUI.cs)
    // 예전 이름판 아래 세트 줄은 없앴어요 (EquipManager 가 부르는 두 함수는 이제 아무것도 안 해요)
    public static void ShowUnder(TextMeshProUGUI nameText, Equips e) { }
    public static void Hide() { }

    // 가장 많이 낀 세트 (같으면 효과가 큰 쪽). 없으면 false
    public static bool Best(out Equips.SetType t, out int now)
    {
        t = Equips.SetType.None; now = 0; float bp = -1f;
        foreach(var kv in counts) {
            if(SizeOf(kv.Key) < 2) continue;
            float p = Power(kv.Key, Mathf.Max(2, kv.Value));
            if(kv.Value > now || (kv.Value == now && p > bp)) { t = kv.Key; now = kv.Value; bp = p; }
        }
        return now > 0;
    }
    // 세트 창 부위 칸: 부위 순서대로 (부위 번호, 그 세트 장비 그림)
    // owned = 그 부위의 이 세트 장비를 하나라도 가방에 가졌나 (2026-11-26 사용자: 안 가진 장비는 검게)
    public static List<(int part, Sprite sprite, bool owned, Equips.MaterialClass grade)> Pieces(Equips.SetType t)
    {
        var map = new SortedDictionary<int, (Sprite sp, bool owned, Equips.MaterialClass g)>();
        foreach(var e in Resources.LoadAll<Equips>("Prefab/Equips")) {
            if(e == null || e.setType != t) continue;
            int p = (int)e.TypeOfEquip;
            bool has = EquipBag.CountOf(e) > 0;
            if(!map.TryGetValue(p, out var v)) map[p] = (e.Sprite_Equip, has, e.Grade);
            else if(has && !v.owned) map[p] = (e.Sprite_Equip, true, e.Grade);   // 가진 쪽 그림으로
        }
        var list = new List<(int, Sprite, bool, Equips.MaterialClass)>();
        foreach(var kv in map) list.Add((kv.Key, kv.Value.sp, kv.Value.owned, kv.Value.g));
        return list;
    }
    // 가방에 있는 그 세트·부위 장비 중 안 낀 것 하나 (강화 높은 것, 잠재력 좋은 것부터) — 세트 창에서 눌러 찾기
    public static EquipFrame OwnedPiece(Equips.SetType t, int part)
    {
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return null;
        EquipFrame best = null;
        foreach(var f in em.All_Equips) {
            if(f == null || f.equipData == null || f.equipData.setType != t || (int)f.equipData.TypeOfEquip != part || EquipBag.IsEquipped(f)) continue;
            if(best == null || f.equipData.Level > best.equipData.Level || (f.equipData.Level == best.equipData.Level && f.equipData.Quality01 > best.equipData.Quality01)) best = f;
        }
        return best;
    }
    public static string ThemeName(Equips.SetType t) => Name(ThemeOf(t));
    public static string SetName(Equips.SetType t) => EquipManager.Instance != null ? EquipManager.Instance.setSetNameInKorean(t) : t.ToString();
    // n 개 효과: "공격력 +53.8%"
    public static string TierText(Equips.SetType t, int n) { var th = ThemeOf(t); return $"{Name(th)} {Fmt(th, ValueOf(t, n))}"; }
    // 숫자만: "+107.5%"
    public static string TierValue(Equips.SetType t, int n) { var th = ThemeOf(t); return Fmt(th, ValueOf(t, n)); }
}
