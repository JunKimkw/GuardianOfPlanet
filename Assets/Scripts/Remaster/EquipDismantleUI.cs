using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 장비 일괄 분해 창 (2026-11-26 사용자 "분해는 추천대로" → 2026-11-28 "직접 고르기만" = UIGen/dispick 하나)
// - 화면은 UIGen/dispick.json (미리보기 previews/dis_list.png 그대로). 여기서는 숫자·체크·버튼·목록만 연결해요
// - 등급 버튼 · 빼기 3개(강화된 장비 · 세트 1개씩 · 장착 중, 저장) · 장비 목록에서 고르기 · 받는 강화석 · '분해하기' 두 번 눌러야 분해
// - 강화석은 장비마다 EquipDismantle.StonesFor (등급에 맞는 강화석)
public partial class RemasterUI
{
    static readonly Equips.MaterialClass[] DsGrades = { Equips.MaterialClass.Normal, Equips.MaterialClass.Rare, Equips.MaterialClass.Epic, Equips.MaterialClass.Unique, Equips.MaterialClass.Legendary, Equips.MaterialClass.Ancient };
    static readonly string[] DsNames = { "일반", "레어", "에픽", "유니크", "전설", "고대" };

    static bool DsEx(int j) => PlayerPrefs.GetInt("dis_ex_" + j, 1) == 1;   // 0 강화된 장비 · 1 세트 1개씩(조각마다 가장 좋은 하나, 자동 분해만) · 2 장착 중 · 4 세트 장비 (빼기)
    // 분해 창 빼기 줄 2칸 (ex0·ex1) → 설정 번호. 장착 중인 장비는 늘 목록에 안 나와요 (2026-11-28 사용자: "장착 중인 건 애초에 안 뜨게, 필터에서 빼 줘"). 가운데 칸 = 세트 장비 모두 (2026-11-28 사용자: "세트 1개씩 말고 다른 필터로" → 유니크 이상은 위 등급 버튼과 겹쳐서 → 세트 장비)
    static readonly int[] DpExKey = { 0, 4 };

    public static void OpenDismantle()
    {
        var ui = FindAnyObjectByType<RemasterUI>();
        if(ui != null) ui.ShowDismantle(true);
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

    // 분해 창 = 직접 고르기 하나 (2026-11-28 사용자: "등급으로는 없애 줘, 직접 고르기만 있으면 돼, 위 버튼 두 개도 없애 줘")
    void ShowDismantle(bool pop) => ShowDismantlePick(pop);

    // ---- 직접 고르기 (2026-11-28 사용자 "B" → 탭 없이 이 화면 하나) — UIGen/dispick (ui_proposals disPick, 미리보기 previews/dis_list.png)
    //  가방 장비(빼기 설정에 걸린 것 빼고)를 5칸씩 보여 주고, 칸을 누르면 고르기/풀기. 고른 칸 = 빨간 테두리 + 체크, 안 고른 칸 = 흐리게(0.45)
    //  등급 버튼 = 그 등급을 모두 고르기 (이미 모두 골랐으면 모두 풀기), 고른 등급은 노란 버튼. 분해하기는 두 번 눌러야 해요
    GenView dpView;
    readonly HashSet<EquipFrame> dpSel = new HashSet<EquipFrame>();
    float dpScroll;
    bool dpArmed;

    static List<EquipFrame> DpPool()
    {
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return new List<EquipFrame>();
        // 빼기 3개는 [등급으로] 탭과 같은 설정 (2026-11-28 사용자: "A 의 빼기 기능도 B 에 넣어 줘") — 켠 것은 목록에 안 나와요
        return em.All_Equips.Where(f => f != null && f.equipData != null
                && !(DsEx(0) && f.equipData.Level > 0) && !(DsEx(4) && f.equipData.setType != Equips.SetType.None) && !EquipBag.IsEquipped(f))
            .OrderBy(f => (int)f.equipData.Grade).ThenBy(f => f.equipData.Level).ThenBy(f => f.equipData.Quality01).ToList();
    }

    void ShowDismantlePick(bool pop)
    {
        dpArmed = false;
        var v = GenBuild("dispick", "Dismantle", pop);   // 올라오기 없이 바로 (UIGen 에서 dispick 은 예외)
        if(v == null) return;
        dpView = v;
        GenUseOrigClose(v);
        GenHit(v, "close", () => { GenClose(v); dpView = null; dpSel.Clear(); if(EquipManager.Instance != null) EquipManager.Instance.ChangeEquipsUI(); });
        GenHit(v, "auto", () => ShowAutoDismantle(true));
        for(int g = 0; g < 6; g++) {
            int gg = g;
            GenHit(v, "q" + g, () => {
                var list = DpPool().Where(f => (int)f.equipData.Grade == gg).ToList();
                if(list.Count == 0) return;
                if(list.All(f => dpSel.Contains(f))) foreach(var f in list) dpSel.Remove(f); else foreach(var f in list) dpSel.Add(f);
                dpArmed = false; RefreshDismantlePick(true);
            });
        }
        for(int j = 0; j < DpExKey.Length; j++) { int jj = DpExKey[j]; GenHit(v, "ex" + j, () => { PlayerPrefs.SetInt("dis_ex_" + jj, DsEx(jj) ? 0 : 1); dpArmed = false; RefreshDismantlePick(true); }, 2f); }
        GenHit(v, "clear", () => { dpSel.Clear(); dpArmed = false; RefreshDismantlePick(true); });
        GenHit(v, "go", DismantlePickGo);
        RefreshDismantlePick(true);
    }

    RectTransform dpContent;
    void RefreshDismantlePick(bool rebuild)
    {
        var v = dpView;
        if(v == null || v.layer == null) return;
        var pool = DpPool();
        dpSel.RemoveWhere(f => f == null || !pool.Contains(f));
        // 등급 버튼: 그 등급이 있고 모두 골랐으면 노랑
        for(int g = 0; g < 6; g++) {
            var list = pool.Where(f => (int)f.equipData.Grade == g).ToList();
            bool all = list.Count > 0 && list.All(f => dpSel.Contains(f));
            GenShow(v, "q" + g + ".on", all); GenShow(v, "q" + g + ".off", !all);
            GenAlpha(v, "q" + g, list.Count > 0 ? 1f : 0.45f);
        }
        for(int j = 0; j < DpExKey.Length; j++) GenShow(v, "ex" + j + ".check", DsEx(DpExKey[j]));
        int stones = dpSel.Sum(f => EquipDismantle.StonesFor(f));
        GenText(v, "count", $"고른 {dpSel.Count}개");
        GenText(v, "total", $"×{stones}");
        GenText(v, "go", dpSel.Count == 0 ? "장비를 골라요" : dpArmed ? "한 번 더 누르면 분해" : "분해하기");
        if(!rebuild || !v.rects.TryGetValue("list", out var r)) return;
        // 목록 (스크롤): 칸 52 · 사이 8 · 안쪽 여백 10 (미리보기와 같게)
        if(dpContent != null) { dpScroll = dpContent.anchoredPosition.y; Destroy(dpContent.parent.gameObject); }
        var view = PxBox("PickView", v.box, r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        int rows = Mathf.Max(1, Mathf.CeilToInt(pool.Count / 5f));
        var content = PxBox("Content", view, 0f, 0f, r.width - 4f, Mathf.Max(r.height - 4f, 8f + rows * 60f + 8f));
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f; sr.viewport = view;
        dpContent = content;
        for(int i = 0; i < pool.Count; i++) {
            var f = pool[i];
            float x = 8f + 60f * (i % 5), y = 8f + 60f * (i / 5);
            bool sel = dpSel.Contains(f);
            var holder = PxBox("Item" + i, content, x, y, 52f, 52f);
            if(!sel) holder.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
            if(sel) Plate(holder, "ds_sel", 0f, 0f, 52f, 52f, 4f);
            MgCell(holder, f, 0f, 0f, 52f, false);
            if(sel) PxIcon(holder, "Icon_Check03_l", 47f, 4f, 20f);
            JobHit(holder, "Hit", 0f, 0f, 52f, 52f, () => {
                if(!dpSel.Remove(f)) dpSel.Add(f);
                dpArmed = false;
                RefreshDismantlePick(true);
            });
        }
        if(pool.Count == 0) GameFont(PxText(view, "분해할 장비가 없어요", (r.width - 4f) / 2f, (r.height - 4f) / 2f, 12f, Hex("#cfe2ff")), false);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, dpScroll);
    }

    void DismantlePickGo()
    {
        if(dpSel.Count == 0) return;
        if(!dpArmed) { dpArmed = true; RefreshDismantlePick(false); return; }   // 두 번 눌러야 (실수 막기)
        dpArmed = false;
        var stones = new Dictionary<UpgradeStone.TypeOfStone, int>();
        int count = 0;
        foreach(var f in dpSel.ToList()) {
            if(f == null || f.equipData == null || EquipBag.IsEquipped(f)) continue;   // 장착 중은 분해 안 해요
            var st = EquipDismantle.StoneOf(f.equipData.Grade);
            stones[st] = (stones.TryGetValue(st, out int n) ? n : 0) + EquipDismantle.StonesFor(f);
            EquipBag.Remove(f);
            count++;
        }
        dpSel.Clear();
        if(UpgradeStoneManager.instance != null) foreach(var kv in stones) if(kv.Value > 0) UpgradeStoneManager.instance.addStone(kv.Key, kv.Value);
        var em = EquipManager.Instance;
        if(em != null) { em.unSelectAfterMerge(); em.ChangeEquipsUI(); }
        Debug.Log($"[분해 직접] {count}개 → 강화석 {stones.Values.Sum()}개");
        dpScroll = 0f;
        RefreshDismantlePick(true);
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
        // 작은 창 (2026-11-28 사용자: "이렇게 크게 말고 그냥 작은 창 띄워서") — 분해 창 위에 반투명 바탕 + 가운데 작은 판 (UIGen/autodis = ui_proposals autodisPop)
        adView = GenBuild("autodis", "AutoDismantle", pop, null, new Color(0f, 0f, 0f, 0.6f));
        if(adView == null) return;
        var v = adView;
        GenUseOrigClose(v);
        GenHit(v, "close", () => { GenClose(v); adView = null; });
        var adDim = v.layer.Find("Dim");   // 바깥(어두운 곳)을 눌러도 닫혀요
        if(adDim != null) adDim.gameObject.AddComponent<Button>().onClick.AddListener(() => { GenClose(v); adView = null; });
        for(int g = 0; g < 5; g++) { int gg = g; GenHit(v, "a" + g + ".off", () => { PlayerPrefs.SetInt("auto_dis_" + gg, AdOn(gg) ? 0 : 1); RefreshAutoDismantle(); }, 4f); }
        RefreshAutoDismantle();
    }

    void RefreshAutoDismantle()
    {
        var v = adView;
        if(v == null || v.layer == null) return;
        GenText(v, "bag", $"가방 {EquipBag.Count} / {EquipBag.Size}");
        GenFill(v, "bag.fill", EquipBag.Size > 0 ? EquipBag.Count / (float)EquipBag.Size : 0f, true);
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
