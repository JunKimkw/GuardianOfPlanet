using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 장비 세트 효과 (2026-11-25 사용자 승인 'B': 예전엔 화면에만 있고 효과가 없었어요)
// - 같은 세트 장비를 2·3·4·5개 끼면 효과가 커져요. 세트 크기 = 그 세트 장비가 들어가는 부위 수 (같은 부위 둘이면 하나로 세요)
// - 효과 크기 = 그 세트 평균 등급의 '장비 1개 몫' × (2개 0.5 · 3개 1 · 4개 1.5 · 5개 2) × equip_set_scale
//   (장비 1개 몫 = EquipBalance 의 (등급 평균 배율 − 1) / 5 와 같은 단위) → 5개 세트를 다 끼면 장비 2개만큼 더 강해져요
// - 세트마다 주제 능력치 하나 (아래 표). 보스 피해·방어 관통·골드·경험치도 같은 몫을 그 능력치로 바꿔요
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
    static float StatOf(Theme th, float p)
    {
        switch(th) {
            case Theme.Damage: return p;                                   // 공격력% (0.1 = 10%)
            case Theme.AttackSpeed: return Mathf.Min(p / (1f + p), 0.15f); // 공속 (넘는 몫은 버려요)
            case Theme.Crit: return p * 100f;                             // 치명 확률 %p
            case Theme.CritDamage: return p / 0.3f;                        // 치명 피해 (1 = 100%)
            case Theme.Boss: return p * 150f;                              // 보스 피해 % (보스에게만이라 1.5배)
            case Theme.Pen: return Mathf.Min(p * 0.5f, 0.25f);             // 방어 관통
            default: return p;                                             // 골드·경험치 %
        }
    }

    public static Theme ThemeOf(Equips.SetType t) => Themes.TryGetValue(t, out var th) ? th : Theme.Damage;

    // 낀 장비에서 세트별 개수
    static readonly Dictionary<Equips.SetType, int> counts = new Dictionary<Equips.SetType, int>();
    public static int EquippedCount(Equips.SetType t) => counts.TryGetValue(t, out int n) ? n : 0;

    // EquipingManager.setEquipDamage 끝에서 불러요
    public static void Apply(EquipingManager m, params EquipingData[] slots)
    {
        counts.Clear();
        var seen = new HashSet<int>();
        foreach(var s in slots) {
            if(s == null || s.equipment == null || s.equipment.equipData == null) continue;
            var e = s.equipment.equipData;
            if(e.setType == Equips.SetType.None || !seen.Add((int)e.TypeOfEquip)) continue;
            counts[e.setType] = EquippedCount(e.setType) + 1;
        }
        foreach(var kv in counts) {
            if(kv.Value < 2) continue;
            float v = StatOf(ThemeOf(kv.Key), Power(kv.Key, kv.Value));
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
            string seg = $"{n}개 {Fmt(th, StatOf(th, Power(e.setType, n)))}";
            parts.Add(n == now ? $"<color=#ffd23f>{seg}</color>" : seg);
        }
        return $"{name} {now}/{size} · {Name(th)} " + string.Join(" · ", parts);
    }

    // 장비 창 이름 아래에 세트 줄 (이름 글자를 복제해서 작게)
    static TextMeshProUGUI line;
    public static void ShowUnder(TextMeshProUGUI nameText, Equips e)
    {
        if(nameText == null) return;
        if(line == null) {
            line = Object.Instantiate(nameText, nameText.transform.parent);
            line.name = "SetLine (remaster)";
            var rt = line.rectTransform;
            rt.anchoredPosition = nameText.rectTransform.anchoredPosition - new Vector2(0f, nameText.rectTransform.rect.height * 0.85f);
            rt.sizeDelta = new Vector2(Mathf.Max(nameText.rectTransform.rect.width, 600f), rt.sizeDelta.y);
            line.enableAutoSizing = true;
            line.fontSizeMax = nameText.fontSize * 0.55f;
            line.fontSizeMin = 10f;
            line.alignment = TextAlignmentOptions.Center;
            line.richText = true;
            line.raycastTarget = false;
        }
        string s = Summary(e);
        line.text = s;
        line.gameObject.SetActive(nameText.gameObject.activeSelf && s.Length > 0);
    }
    public static void Hide() { if(line != null) line.gameObject.SetActive(false); }
}
