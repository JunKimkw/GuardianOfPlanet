using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 장비 일괄 분해 창 (2026-11-26 사용자 "분해는 추천대로" = ui_proposals 분해 A + 자동 분해 버튼)
// - 화면은 UIGen/dismantle.json (미리보기 그대로). 여기서는 숫자·체크·버튼만 연결해요
// - 등급마다 체크(저장) · 빼기 3개(강화된 장비 · 세트 1개씩 · 장착 중, 저장) · 받는 강화석 합계 · '분해하기' 두 번 눌러야 분해
// - 강화석은 장비마다 EquipDismantle.StonesFor (등급에 맞는 강화석)
public partial class RemasterUI
{
    static readonly Equips.MaterialClass[] DsGrades = { Equips.MaterialClass.Normal, Equips.MaterialClass.Rare, Equips.MaterialClass.Epic, Equips.MaterialClass.Unique, Equips.MaterialClass.Legendary, Equips.MaterialClass.Ancient };
    static readonly string[] DsNames = { "일반", "레어", "에픽", "유니크", "전설", "고대" };
    GenView dsView;
    bool dsArmed;

    static bool DsOn(int g) => PlayerPrefs.GetInt("dis_grade_" + g, g < 2 ? 1 : 0) == 1;
    static bool DsEx(int j) => PlayerPrefs.GetInt("dis_ex_" + j, 1) == 1;   // 0 강화된 장비 · 1 세트 1개씩(조각마다 가장 좋은 하나) · 2 장착 중 (빼기)

    public static void OpenDismantle()
    {
        var ui = FindAnyObjectByType<RemasterUI>();
        if(ui != null) ui.ShowDismantle(true);
    }

    // 분해할 장비 (등급 g, 빼기 옵션 적용). keep = 지킬 세트 조각 (DsKeepSet)
    static List<EquipFrame> DsTargets(int g, HashSet<EquipFrame> keep)
    {
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return new List<EquipFrame>();
        return em.All_Equips.Where(f => f != null && f.equipData != null && f.equipData.Grade == DsGrades[g]
            && !(DsEx(0) && f.equipData.Level > 0)
            && !(DsEx(1) && keep.Contains(f))
            && !(DsEx(2) && EquipBag.IsEquipped(f))).ToList();
    }

    // '세트 1개씩' 빼기 (2026-11-27 점검): 세트 장비가 일반의 절반·레어의 3/4 이라, 세트 장비를 모두 빼면 일괄·자동 분해가 거의 안 됐어요
    //  → 세트·부위마다 가장 좋은 하나(낀 것 > 강화 높은 것 > 등급 > 잠재력)만 지키고, 같은 조각이 또 있으면 그건 분해해요
    //  (아직 없던 세트 조각은 하나뿐이라 늘 지켜져요)
    static HashSet<EquipFrame> DsKeepSet()
    {
        var keep = new HashSet<EquipFrame>();
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return keep;
        var best = new Dictionary<(Equips.SetType, Equips.Type), EquipFrame>();
        foreach(var f in em.All_Equips) {
            if(f == null || f.equipData == null || f.equipData.setType == Equips.SetType.None) continue;
            var k = (f.equipData.setType, f.equipData.TypeOfEquip);
            if(!best.TryGetValue(k, out var b) || DsBetter(f, b)) best[k] = f;
        }
        foreach(var f in best.Values) keep.Add(f);
        return keep;
    }
    static bool DsBetter(EquipFrame a, EquipFrame b)
    {
        bool ea = EquipBag.IsEquipped(a), eb = EquipBag.IsEquipped(b);
        if(ea != eb) return ea;
        if(a.equipData.Level != b.equipData.Level) return a.equipData.Level > b.equipData.Level;
        if(a.equipData.Grade != b.equipData.Grade) return a.equipData.Grade > b.equipData.Grade;
        return a.equipData.Quality01 > b.equipData.Quality01;
    }

    void ShowDismantle(bool pop)
    {
        dsArmed = false;
        dsView = GenBuild("dismantle", "Dismantle", pop);
        if(dsView == null) return;
        var v = dsView;
        GenHit(v, "close", () => { GenClose(v); dsView = null; if(EquipManager.Instance != null) EquipManager.Instance.ChangeEquipsUI(); });
        GenHit(v, "auto", () => ShowAutoDismantle(true));
        for(int g = 0; g < 6; g++) { int gg = g; GenHit(v, "g" + g, () => { PlayerPrefs.SetInt("dis_grade_" + gg, DsOn(gg) ? 0 : 1); dsArmed = false; RefreshDismantle(); }); }
        for(int j = 0; j < 3; j++) { int jj = j; GenHit(v, "ex" + j, () => { PlayerPrefs.SetInt("dis_ex_" + jj, DsEx(jj) ? 0 : 1); dsArmed = false; RefreshDismantle(); }, 2f); }
        GenHit(v, "go", DismantleGo);
        RefreshDismantle();
    }

    void RefreshDismantle()
    {
        var v = dsView;
        if(v == null || v.layer == null) return;
        int total = 0, count = 0;
        var keep = DsKeepSet();
        GenText(v, "ex1", "세트 1개씩");   // 빼기: 세트 조각마다 하나는 남겨요 (DsKeepSet)
        for(int g = 0; g < 6; g++) {
            var list = DsTargets(g, keep);
            int stones = list.Sum(f => EquipDismantle.StonesFor(f));
            GenText(v, "g" + g + ".name", $"{DsNames[g]} {list.Count}개");
            GenText(v, "g" + g + ".stone", $"강화석 ×{stones}");
            bool on = DsOn(g);
            GenShow(v, "g" + g + ".check", on);
            GenAlpha(v, "g" + g, list.Count > 0 ? 1f : 0.45f);   // 없는 등급은 흐리게 (미리보기와 같은 0.45)
            if(on) { total += stones; count += list.Count; }
        }
        for(int j = 0; j < 3; j++) GenShow(v, "ex" + j + ".check", DsEx(j));
        GenText(v, "total", $"×{total}");
        GenText(v, "count", $"{count}개 분해");
        GenText(v, "go", count == 0 ? "분해할 장비 없음" : dsArmed ? "한 번 더 누르면 분해" : "분해하기");
    }

    void DismantleGo()
    {
        int count = 0;
        var keep = DsKeepSet();
        for(int g = 0; g < 6; g++) if(DsOn(g)) count += DsTargets(g, keep).Count;
        if(count == 0) return;
        if(!dsArmed) { dsArmed = true; RefreshDismantle(); return; }   // 두 번 눌러야 (실수 막기)
        dsArmed = false;
        var stones = new Dictionary<UpgradeStone.TypeOfStone, int>();
        for(int g = 0; g < 6; g++) {
            if(!DsOn(g)) continue;
            foreach(var f in DsTargets(g, keep)) {
                var st = EquipDismantle.StoneOf(f.equipData.Grade);
                stones[st] = (stones.TryGetValue(st, out int n) ? n : 0) + EquipDismantle.StonesFor(f);
                EquipBag.Remove(f);
            }
        }
        if(UpgradeStoneManager.instance != null) foreach(var kv in stones) if(kv.Value > 0) UpgradeStoneManager.instance.addStone(kv.Key, kv.Value);
        var em = EquipManager.Instance;
        if(em != null) { em.unSelectAfterMerge(); em.ChangeEquipsUI(); }
        Debug.Log($"[분해] {count}개 → 강화석 {stones.Values.Sum()}개");
        RefreshDismantle();
    }

    // ---- 자동 분해 (C 화면 = UIGen/autodis): 등급마다 켜면, 뽑기로 들어온 그 등급 장비를 뽑기 화면이 닫힌 뒤 분해해요
    //      가방이 꽉 차서 뽑기가 막히면, 켠 등급부터(잠재력 낮은 것부터) 분해해서 자리를 만들어요. 기본은 모두 꺼짐
    GenView adView;
    static bool AdOn(int g) => PlayerPrefs.GetInt("auto_dis_" + g, 0) == 1;
    static readonly List<EquipFrame> AdPending = new List<EquipFrame>();

    // 뽑기에서 불러요 (GachaManager.NormalGacha / SpecialGacha)
    public static void QueueAutoDismantle(IEnumerable<EquipFrame> got)
    {
        foreach(var f in got) if(f != null && f.equipData != null && (int)f.equipData.Grade < 5 && AdOn((int)f.equipData.Grade)) AdPending.Add(f);
    }
    // 가방이 꽉 찼을 때 자리 만들기 (need 칸). 만들었으면 true
    public static bool AutoMakeRoom(int need)
    {
        if(EquipBag.HasRoom(need)) return true;
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return false;
        var keep = DsKeepSet();
        var cand = em.All_Equips.Where(f => f != null && f.equipData != null && (int)f.equipData.Grade < 5 && AdOn((int)f.equipData.Grade) && AdOk(f, keep))
            .OrderBy(f => (int)f.equipData.Grade).ThenBy(f => f.equipData.Quality01).ToList();
        int i = 0;
        while(!EquipBag.HasRoom(need) && i < cand.Count) AdBreak(cand[i++]);
        if(i > 0) { em.unSelectAfterMerge(); em.ChangeEquipsUI(); }
        return EquipBag.HasRoom(need);
    }
    // 자동으로 분해해도 되는 장비 (일괄 분해의 '세트 1개씩' 설정을 같이 따라요. 강화된 것·낀 것은 늘 빼요)
    static bool AdOk(EquipFrame f, HashSet<EquipFrame> keep) => f.equipData.Level == 0 && !EquipBag.IsEquipped(f) && !(DsEx(1) && keep.Contains(f));
    static void AdBreak(EquipFrame f)
    {
        int st = EquipDismantle.StonesFor(f);
        var type = EquipDismantle.StoneOf(f.equipData.Grade);
        EquipBag.Remove(f);
        if(st > 0 && UpgradeStoneManager.instance != null) UpgradeStoneManager.instance.addStone(type, st);
        string day = System.DateTime.Now.ToString("yyyyMMdd");
        if(PlayerPrefs.GetString("auto_dis_day", "") != day) { PlayerPrefs.SetString("auto_dis_day", day); PlayerPrefs.SetInt("auto_dis_cnt", 0); PlayerPrefs.SetInt("auto_dis_st", 0); }
        PlayerPrefs.SetInt("auto_dis_cnt", PlayerPrefs.GetInt("auto_dis_cnt", 0) + 1);
        PlayerPrefs.SetInt("auto_dis_st", PlayerPrefs.GetInt("auto_dis_st", 0) + st);
    }
    // 매 프레임: 뽑기 화면이 닫혀 있으면 쌓인 장비를 분해
    void UpdateAutoDismantle()
    {
        if(AdPending.Count == 0) return;
        var gm = GachaManager.Instance;
        if(GachaFxBusy || (gm != null && gm.GachaScreen != null && gm.GachaScreen.activeInHierarchy)) return;   // 뽑기 연출이 끝난 뒤
        int n = 0;
        var keep = DsKeepSet();
        foreach(var f in AdPending.ToList()) if(f != null && f.equipData != null && AdOk(f, keep)) { AdBreak(f); n++; }
        AdPending.Clear();
        if(n > 0) { var em = EquipManager.Instance; if(em != null) { em.unSelectAfterMerge(); em.ChangeEquipsUI(); } Debug.Log($"[자동 분해] {n}개"); }
        if(adView != null) RefreshAutoDismantle();
    }

    void ShowAutoDismantle(bool pop)
    {
        adView = GenBuild("autodis", "AutoDismantle", pop);
        if(adView == null) return;
        var v = adView;
        GenHit(v, "close", () => { GenClose(v); adView = null; });
        for(int g = 0; g < 5; g++) { int gg = g; GenHit(v, "a" + g + ".off", () => { PlayerPrefs.SetInt("auto_dis_" + gg, AdOn(gg) ? 0 : 1); RefreshAutoDismantle(); }, 4f); }
        GenHit(v, "go", () => { GenClose(v); adView = null; ShowDismantle(true); });
        RefreshAutoDismantle();
    }

    void RefreshAutoDismantle()
    {
        var v = adView;
        if(v == null || v.layer == null) return;
        GenText(v, "bag", $"가방 {EquipBag.Count} / {EquipBag.Size}");
        GenFill(v, "bag.fill", EquipBag.Size > 0 ? EquipBag.Count / (float)EquipBag.Size : 0f);
        for(int g = 0; g < 5; g++) { bool on = AdOn(g); GenShow(v, "a" + g + ".on", on); GenShow(v, "a" + g + ".off", !on); }
        bool today = PlayerPrefs.GetString("auto_dis_day", "") == System.DateTime.Now.ToString("yyyyMMdd");
        GenText(v, "today.cnt", (today ? PlayerPrefs.GetInt("auto_dis_cnt", 0) : 0) + "개");
        GenText(v, "today.st", "×" + (today ? PlayerPrefs.GetInt("auto_dis_st", 0) : 0));
    }
}

// [개발] 가방 비우기 (B 키, 에디터·개발 빌드): 낀 장비 말고 모두 지워요. 묻는 창에서 '비우기'를 눌러야 해요 (되돌릴 수 없어요)
public partial class RemasterUI
{
    public void DebugEmptyBag()
    {
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return;
        var list = em.All_Equips.Where(f => f != null && f.equipData != null && !EquipBag.IsEquipped(f)).ToList();
        if(list.Count == 0) { Toast("[개발] 가방에 지울 장비가 없어요", Color.white); return; }
        ShowMessage("[개발] 가방 비우기", $"낀 장비 말고 {list.Count}개를 모두 지워요\n되돌릴 수 없어요", "비우기", () => {
            foreach(var f in list) EquipBag.Remove(f);
            em.unSelectAfterMerge(); em.ChangeEquipsUI();
            Toast($"[개발] 장비 {list.Count}개를 지웠어요", Color.white);
        }, cancel: "취소");
    }
}
