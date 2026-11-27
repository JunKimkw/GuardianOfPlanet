using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 무기 강화 창 + 전직 (2026-11-26 사용자: job4 A안 "아래에서 올라오는 판" + job5_2 "가로 선택지 두 줄" → 색은 job6 A안 "장비 창과 같은 파랑", 높이는 예전 강화 창 그대로)
// 미리보기 previews/job6_A.png (tools/ui_preview/job_preview6.js) 와 같은 그림(Resources/PlanetMap/wjA_*, wj_btn_*, wj_close)·같은 px 좌표(크롬으로 잰 값)
// - 옛날 '강화'(Upgrade Ship) 또는 '특성'(Traits) 탭이 열리면 그 탭을 숨기고 이 창을 띄워요 (씬은 안 건드려요). job_ui_on = 0 이면 옛날 화면
// - 무기 줄마다: 레벨업(골드) · 전직 Lv↑(광석) 같은 크기 버튼 + 아래 전직 칸 6개 (고름 = 그림 · 고를 수 있음 = 빛나는 ? · 잠김)
// - 칸을 누르거나, 레벨업으로 새 칸이 열리면 고르기 창 (가로로 긴 선택지 두 줄 → '○○로 정하기')
public partial class RemasterUI
{
    GameObject traitsTab, upgradeTab, hiddenTab, traitButton;
    RectTransform wjRoot, wjFrame, wjSheet, wjList, wjPop;
    bool wjWasOpen;
    float wjRefresh, wjScroll;
    TraitsData.WeaponType wjPopWeapon;
    int wjPopSlot = -1, wjPopSel;

    // 개조 아이콘 = 직접 그린 아이콘 (2026-11-26 사용자: "잘 그렸다 이거 사용하자")
    //  tools/ui_preview/icon_draw.js 로 Layer Lab 그림체(바깥 외곽선·셀 음영)를 따라 그려서 Resources/RemasterUI/Draw_* 로 넣었어요
    //  흰 그림 아이콘·Skill_Icon_Pack 은 안 써요 (사용자)
    static readonly string[,] JobIcons = {
        { "Draw_rapid", "Draw_heavy" }, { "Draw_augpick", "Draw_reroll" },
        { "Draw_crit", "Draw_steady" }, { "Draw_combo", "Draw_first" },
        { "Draw_gold", "Draw_exp" }, { "Draw_pierce", "Draw_critdmg" } };
    // 기본총이 아닌 무기의 '시작 증강' 칸은 둘 다 그 무기 증강을 들고 시작 → 로켓 (둘째는 카드)
    static string JobIcon(TraitsData.WeaponType w, int slot, int choice)
    {
        if(slot == 1 && w != TraitsData.WeaponType.MainWeapon) return choice == 0 ? "Draw_start" : "Draw_augpick";
        return JobIcons[slot, choice];
    }

    // RemasterUI.Update 에서
    void UpdateWeaponJob()
    {
        if(traitsTab == null) {
            var tm = TraitsManager.instance;
            if(tm == null || tm.TraitsData == null || tm.TraitsData.Count == 0 || tm.TraitsData[0] == null) return;
            for(var t = tm.TraitsData[0].transform; t != null; t = t.parent)
                if(t.name == "Traits") { traitsTab = t.gameObject; break; }
            if(traitsTab == null) return;
            var up = traitsTab.transform.parent != null ? traitsTab.transform.parent.Find("Upgrade Ship") : null;
            if(up != null) upgradeTab = up.gameObject;
        }
        bool on = Tuning.Get("job_ui_on", 0f) > 0f;   // 0 = 원래 강화 창 + 전직 줄 (2026-11-26 사용자), 1 = 새 파란 창
        // 아래 메뉴의 특성 버튼은 숨겨요 (강화 창 하나에서 전직까지, 2026-11-26 사용자). 메뉴가 자동 정렬이라 자리가 알아서 맞춰져요
        if(traitButton == null) { var bb = GameObject.Find("Bottom Buttons"); var t = bb != null ? bb.transform.Find("Trait Shop") : null; if(t != null) traitButton = t.gameObject; }
        if(traitButton != null && traitButton.activeSelf) traitButton.SetActive(false);   // 특성은 전직으로 바뀌어서 두 방식 모두 숨겨요
        UpdateOldUpgradeJob();   // 원래 강화 창에 전직 줄 (WeaponJobOldUI.cs)
        GameObject tab = traitsTab.activeInHierarchy ? traitsTab : upgradeTab != null && upgradeTab.activeInHierarchy ? upgradeTab : null;
        bool open = on && tab != null;
        if(open && !wjWasOpen) OpenWeaponWindow(tab);
        if(!open && wjWasOpen) HideWeaponWindow();
        wjWasOpen = open;
        if(!open) return;
        wjRefresh -= Time.unscaledDeltaTime;
        if(wjRefresh <= 0f) { wjRefresh = 1f; if(wjPopSlot < 0) BuildWeaponRows(); }   // 골드·광석이 바뀌니까 1초마다
    }

    void OpenWeaponWindow(GameObject tab)
    {
        if(!EnsureBuilt()) return;
        hiddenTab = tab;
        var cg = tab.GetComponent<CanvasGroup>();
        if(cg == null) cg = tab.AddComponent<CanvasGroup>();   // ?? 는 Unity 오브젝트의 "가짜 null" 을 못 걸러요
        cg.alpha = 0f; cg.blocksRaycasts = false;   // 옛날 탭은 보이지 않게 (열린 상태는 그대로 → 메뉴 버튼이 평소처럼 닫아요)
        if(wjRoot == null) BuildWeaponShell(tab.transform.parent != null && tab.transform.parent.parent != null ? tab.transform.parent.parent : tab.transform);
        wjRoot.SetAsLastSibling();
        wjRoot.gameObject.SetActive(true);
        FitWeaponSheet(tab);   // 예전 창과 같은 높이 (열 때마다, 화면 크기가 바뀌어도)
        ClosePick();
        BuildWeaponRows();
    }

    void HideWeaponWindow()
    {
        if(hiddenTab != null) { var cg = hiddenTab.GetComponent<CanvasGroup>(); if(cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; } }
        hiddenTab = null;
        if(wjRoot != null) wjRoot.gameObject.SetActive(false);
    }

    void CloseWeaponWindow()
    {
        var tab = hiddenTab;
        HideWeaponWindow();
        wjWasOpen = false;
        if(tab != null) tab.SetActive(false);
    }

    // 가진 무기 (기본총은 늘)
    static List<TraitsData.WeaponType> JobWeapons()
    {
        var list = new List<TraitsData.WeaponType>();
        for(int i = 0; i < WeaponJob.WeaponCount; i++) {
            var w = (TraitsData.WeaponType)i;
            if(w == TraitsData.WeaponType.MainWeapon || WeaponJob.WeaponLevel(w) > 0) list.Add(w);
        }
        return list;
    }

    // ── 틀 (2026-11-26 job6 A안: 장비 창과 같은 파랑. 판 높이·위치는 예전 강화 창 그대로 → FitWeaponSheet)
    float wjSheetH = 500f;
    RectTransform wjViewport;

    void BuildWeaponShell(Transform canvas)
    {
        MapFont();
        wjRoot = Stretch("RemasterWeaponWindow", canvas);
        var fit = wjRoot.gameObject.AddComponent<RemasterWidthFit>();
        wjFrame = Rect("Frame", wjRoot, new Vector2(1080, 2340), Vector2.zero);
        fit.target = wjFrame;
        fit.Apply();
        // 뒤 게임 화면을 살짝 어둡게 (rgba(0,0,0,.25)), 누르면 닫기
        var dim = Stretch("Dim", wjRoot);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.25f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseWeaponWindow);
        dim.SetAsFirstSibling();
        // 고르기 창 자리 (화면 가운데 기준, 미리보기 780 높이 그대로)
        wjPop = Rect("Pick", wjFrame, new Vector2(1080, 2340), Vector2.zero);
        wjPop.gameObject.SetActive(false);
    }

    // 판 자리 (2026-11-26 사용자: "예시랑 완전 똑같이" → 미리보기 job6 A안 그대로)
    //  아래 = 아래 메뉴(Bottom Buttons) 바로 위 8 · 위 = 예전 강화 창 맨 위 (높이 일정)
    float PreviewY(Vector3 world) => (wjFrame.rect.height * 0.5f - wjFrame.InverseTransformPoint(world).y) / K;   // 틀 위에서 아래로 px
    void FitWeaponSheet(GameObject tab)
    {
        float bottom = 780f - 68f, maxTop = 780f - 68f - 500f;
        var c = new Vector3[4];
        var bb = GameObject.Find("Bottom Buttons");
        if(bb != null && bb.transform is RectTransform brt) { brt.GetWorldCorners(c); float y = PreviewY(c[1]); if(y > 300f && y < 2000f) bottom = y - 8f; }
        Transform panel = upgradeTab != null ? upgradeTab.transform.Find("Upgrade") : null;
        if(panel == null && tab != null) panel = tab.transform.Find("Upgrade");
        if(panel is RectTransform prt) { prt.GetWorldCorners(c); float y = PreviewY(c[1]); if(y > 0f && y < bottom - 150f) maxTop = y; }
        float h = bottom - maxTop;   // 무기 수와 상관없이 일정 (2026-11-26 사용자)
        float top = bottom - h;
        if(wjSheet != null) Destroy(wjSheet.gameObject);
        wjSheetH = h;
        wjSheet = PxBox("Sheet", wjFrame, 8f, top, 344f, h);
        var sheetImg = Plate(wjSheet, "wjA_sheet", 0f, 0f, 344f, h, 7f);
        sheetImg.raycastTarget = true;   // 판 안을 눌러도 닫히지 않게
        Plate(wjSheet, "wjA_band", 3f, 3f, 338f, 46f, 0f);
        WText(wjSheet, "무기 강화", 21f, 26f, 22f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        WText(wjSheet, "빛나는 ? = 개조 고르기", 127f, 27f, 12f, Hex("#cfe4ff"), 0f, null, TextAlignmentOptions.Left);
        Plate(wjSheet, "wj_close", 302f, -16f, 40f, 40f, 5f);
        JobHit(wjSheet, "Close", 302f, -16f, 40f, 40f, CloseWeaponWindow);
        // 무기 목록 (위아래로 넘겨요): 판 안 49 ~ 아래 테두리
        wjViewport = PxBox("List", wjSheet, 3f, 49f, 338f, Mathf.Max(60f, h - 52f));
        wjViewport.gameObject.AddComponent<RectMask2D>();
        wjViewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        wjList = PxBox("Content", wjViewport, 0f, 0f, 338f, h - 52f);
        var sr = wjViewport.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.viewport = wjViewport; sr.content = wjList;
        sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f;
        wjPop.SetAsLastSibling();
    }

    // 창 글씨 크기 (2026-11-26 사용자: 너무 커요 → 정보·장비 창 글씨와 비슷하게). 위치는 그대로, 크기·외곽선만
    static float TextScale => Mathf.Clamp(Tuning.Get("wj_text_scale", 0.75f), 0.4f, 1.5f);
    TextMeshProUGUI WText(Transform parent, string text, float cx, float cy, float size, Color color, float ow = 0f, Color? outline = null, TextAlignmentOptions align = TextAlignmentOptions.Center, float boxW = 0f)
        => PxText(parent, text, cx, cy, size * TextScale, color, ow * TextScale, outline, align, boxW);

    void JobHit(Transform parent, string name, float x, float y, float w, float h, UnityEngine.Events.UnityAction act)
    {
        var hit = PxBox(name, parent, x, y, w, h);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        hit.gameObject.AddComponent<Button>().onClick.AddListener(act);
    }
    static void ClearKids(Transform t) { for(int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }

    // ── 무기 줄 (판 안 x 11, 위 61 + 112 × i, 322 × 106) — 미리보기 job6 A안 wrowT 를 잰 값
    void BuildWeaponRows()
    {
        if(wjList == null) return;
        wjScroll = wjList.anchoredPosition.y;
        ClearKids(wjList);
        var list = JobWeapons();
        var wm = WeaponManager.instance;
        Color ol = Hex("#0b1f6a");
        for(int i = 0; i < list.Count; i++) {
            var w = list[i];
            float ry = 12f + i * 112f;   // 보는 칸이 판 안 49 부터라 12 를 더해 61
            var row = PxBox("Row", wjList, 8f, ry, 322f, 106f);
            Plate(row, "wjA_row", 0f, 0f, 322f, 106f, 0f);
            Plate(row, "wjA_box", 12f, 11f, 44f, 44f, 0f);
            PxIcon(row, "ItemIcon_Battle", 34f, 33f, 36f);
            int wlv = WeaponJob.WeaponLevel(w), jl = WeaponJob.Level(w);
            WText(row, WeaponJob.WeaponName(w), 64f, 17.5f, 16f, Color.white, 1f, ol, TextAlignmentOptions.Left);
            WText(row, $"Lv.{wlv}", 64f, 36f, 13f, Hex("#ffe14a"), 1f, ol, TextAlignmentOptions.Left);
            WText(row, $"개조 Lv.{jl}", 64f, 51.5f, 11.5f, Hex("#e4ccff"), 1f, ol, TextAlignmentOptions.Left);

            // 레벨업 (골드)
            var wb = WeaponJob.WeaponOf(w);
            int si = wm != null && wb != null ? wm.Weapons.IndexOf(wb) : -1;
            var shop = si >= 0 && si < wm.Shops.Count ? wm.Shops[si] : null;
            long price = shop != null ? shop.getTotalMoney(wlv, Mathf.Max(1, shop.purchaseSize)) : 0;
            Plate(row, "wj_btn_y", 136f, 10f, 84f, 46f, 5f);
            WText(row, "레벨업", 178.5f, 26.5f, 14f, Color.white, 1f, Hex("#6a2e00"));
            PxIcon(row, "ItemIcon_Money_Coin", 157.5f, 40.5f, 11f);
            WText(row, GameManager.MoneyString(price), 166f, 42f, 10f, Color.white, 1f, Hex("#6a2e00"), TextAlignmentOptions.Left);
            var ww = w;
            JobHit(row, "LevelUp", 136f, 10f, 84f, 46f, () => {
                if(shop == null) return;
                int before = WeaponJob.OpenUnpicked(ww);
                shop.LevelUP();
                BuildWeaponRows();
                if(WeaponJob.OpenUnpicked(ww) > before) OpenPick(ww, FirstUnpicked(ww), true);   // 새 칸이 열리면 바로 고르기
            });
            // 전직 Lv↑ (광석)
            long cost = WeaponJob.LevelUpCost(w);
            Plate(row, "wj_btn_p", 226f, 10f, 84f, 46f, 5f);
            WText(row, "개조 Lv↑", 267.5f, 27.5f, 14f, Color.white, 1f, Hex("#3a0d66"));
            WText(row, cost < 0 ? (jl >= WeaponJob.MaxLevel ? "MAX" : $"무기 Lv.{WeaponJob.OpenWeaponLv}") : "광석 " + GameManager.MoneyString(cost), 268f, 42f, 10f, Color.white, 1f, Hex("#3a0d66"));
            JobHit(row, "JobUp", 226f, 10f, 84f, 46f, () => {
                if(!WeaponJob.TryLevelUp(ww)) Toast(WeaponJob.LevelUpCost(ww) < 0 ? "아직 올릴 수 없어요" : "광석이 모자라요", new Color(1f, 0.6f, 0.5f));
                BuildWeaponRows();
            });

            // 전직 칸 6개 (12 + 52 × k, 66, 46 × 32)
            for(int k = 0; k < WeaponJob.Slots; k++) {
                int kk = k;
                float cx = 12f + k * 52f;
                bool open = WeaponJob.SlotOpen(w, k);
                int p = WeaponJob.Pick(w, k);
                if(!open) { Plate(row, "wjA_chip_lock", cx, 66f, 46f, 32f, 0f); var l = PxIcon(row, "Icon_Lock03", cx + 23f, 82f, 16f); l.color = new Color(1, 1, 1, 0.5f); }
                else if(p >= 0) { Plate(row, "wjA_chip_on", cx, 66f, 46f, 32f, 0f); PxIcon(row, JobIcon(w, k, p), cx + 23f, 82f, 24f); }
                else { Plate(row, "wjA_chip_q", cx, 66f, 46f, 32f, 10f); WText(row, "?", cx + 23f, 82f, 17f, Color.white, 1f, Hex("#2a0a50")); }
                JobHit(row, "Slot" + k, cx, 66f, 46f, 32f, () => {
                    if(!WeaponJob.SlotOpen(ww, kk)) { Toast($"개조 Lv.{WeaponJob.SlotLv(kk)} 에 열려요", new Color(1f, 0.85f, 0.5f)); return; }
                    OpenPick(ww, kk, false);
                });
            }
        }
        float viewH = Mathf.Max(60f, wjSheetH - 52f), contentH = Mathf.Max(viewH, 12f + list.Count * 112f);
        wjList.sizeDelta = new Vector2(338f * K, contentH * K);
        wjList.anchoredPosition = new Vector2(0f, Mathf.Clamp(wjScroll, 0f, Mathf.Max(0f, (contentH - viewH) * K)));
    }

    void ShowOreShort(long need, string what)
    {
        // 메시지 창: 광석 배지 + 모은 정도 막대 (MessageBox.cs)
        ShowMessage("광석이 모자라요", $"{what} 광석이 더 필요해요", icon: "OreGem", have: GameManager.GetOre(), need: need, hint: "광석은 광산에서 캐요");
    }

    static int FirstUnpicked(TraitsData.WeaponType w)
    {
        for(int s = 0; s < WeaponJob.Slots; s++) if(WeaponJob.SlotOpen(w, s) && WeaponJob.Pick(w, s) < 0) return s;
        return 0;
    }

    // ── 고르기 창 (미리보기 job6 A안 popT: 창 14,176 · 332 × 400) ──
    void OpenPick(TraitsData.WeaponType w, int slot, bool fresh)
    {
        wjPopWeapon = w; wjPopSlot = slot;
        int cur = WeaponJob.Pick(w, slot);
        wjPopSel = cur >= 0 ? cur : 0;
        BuildPick(fresh);
    }
    void ClosePick()
    {
        wjPopSlot = -1;
        if(wjPop != null) { ClearKids(wjPop); wjPop.gameObject.SetActive(false); }
        AfterPickClosedOld();
    }

    void BuildPick(bool fresh)
    {
        if(wjPopSlot < 0 || !EnsureBuilt()) return;
        // 고르기 창은 리마스터 UI 맨 위 레이어에 (2026-11-26: 원래 강화 창 위에서 어둡게가 다 안 덮이고 바깥을 눌러도 안 닫혔어요)
        if(wjPop == null || wjPop.parent != modalRoot) wjPop = Stretch("WeaponPick", modalRoot);
        ClearKids(wjPop);
        wjPop.gameObject.SetActive(true);
        wjPop.SetAsLastSibling();
        var w = wjPopWeapon; int s = wjPopSlot;
        int cur = WeaponJob.Pick(w, s), jl = WeaponJob.Level(w);
        // 어둡게 (rgba(0,0,0,.6)), 누르면 닫기
        // 어둡게 (rgba(0,0,0,.6)) — 화면 전체 (2026-11-26: 틀 안에만 있으면 원래 강화 창 위에서 다 안 덮여서 바깥을 눌러도 안 닫혔어요)
        var pdim = Stretch("Dim", wjPop);
        pdim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        pdim.gameObject.AddComponent<Button>().onClick.AddListener(() => { ClosePick(); BuildWeaponRows(); });
        var box = Rect("Box", wjPop, new Vector2(1080, 780 * K), Vector2.zero);
        box.localScale = Vector3.one * Mathf.Clamp(Tuning.Get("wj_pick_scale", 0.85f), 0.5f, 1.2f);   // (사용자: 창을 조금 더 작게)
        Plate(box, "wjA_win", 14f, 176f, 332f, 400f, 7f).raycastTarget = true;
        Plate(box, "wjA_headicon", 146f, 145f, 68f, 68f, 18f);
        PxIcon(box, "ItemIcon_Battle", 180f, 179f, 50f);
        // 글씨는 원래 게임 한글 글꼴 (2026-11-26 사용자: "여기 글씨들도 바꿔 줘")
        GameFont(WText(box, $"{WeaponJob.WeaponName(w)} 개조!", 180f, 234f, 22f, Color.white, 2f, Navy), true);
        string sub = cur < 0 ? $"개조 Lv.{WeaponJob.SlotLv(s)} 에 새 칸이 열렸어요 · 하나를 골라요"
                             : $"개조 Lv.{WeaponJob.SlotLv(s)} {WeaponJob.SlotName[s]} · 바꾸기 광석 {GameManager.MoneyString(WeaponJob.SwapCost(w))}";
        GameFont(WText(box, sub, 180f, 258.5f, 12.5f, Hex("#e6efff")), false);
        for(int c = 0; c < 2; c++) {
            int cc = c;
            float oy = c == 0 ? 281f : 395f;
            bool on = wjPopSel == c;
            Plate(box, on ? "wjA_opt_on" : "wjA_opt", 31f, oy, 298f, 104f, on ? 14f : 0f);
            Plate(box, on ? "wjA_optbox_on" : "wjA_optbox", 43f, oy + 18f, 66f, 66f, 0f);
            PxIcon(box, JobIcon(w, s, c), 76f, oy + 51f, 54f);
            string name = WeaponJob.ChoiceName(w, s, c);   // ✔ 는 미원체에 없어서 네모로 나와요 (고른 줄은 노란색으로 보여요)
            string amt = WeaponJob.Amount(w, s, c, jl);
            // 이름 줄은 늘 흰 글씨 + 테두리 (고른 노란 카드 = 진한 갈색 테두리 · 안 고른 파란 카드 = 원래 검은 테두리)
            // (2026-11-26 사용자: 노란 카드의 진한 글씨는 답답해 보여요)
            if(on) GameFont(WText(box, $"{name} <size={18 * K * TextScale}><color=#ffe14a>{amt}</color></size>", 123f, oy + 25.5f, 20f, Color.white, 1f, Hex("#6a3000"), TextAlignmentOptions.Left, 200f), true, Hex("#6a3000"));
            else GameFont(WText(box, $"{name} <size={18 * K * TextScale}><color=#ffe14a>{amt}</color></size>", 123f, oy + 25.5f, 20f, Color.white, 1f, Hex("#0b1f6a"), TextAlignmentOptions.Left, 200f), true);
            GameFont(WText(box, WeaponJob.Sentence(w, s, c), 123f, oy + 51f, 15f, on ? Hex("#4a3000") : Hex("#e6efff"), 0f, null, TextAlignmentOptions.Left, 200f), false);
            GameFont(WText(box, WeaponJob.PerLevelLine(w, s, c), 123f, oy + 84f, 13f, on ? Hex("#6a5020") : Hex("#b8cff5"), 0f, null, TextAlignmentOptions.Left, 200f), false);
            JobHit(box, "Opt" + c, 31f, oy, 298f, 104f, () => { wjPopSel = cc; BuildPick(fresh); });
        }
        string pick = WeaponJob.ChoiceName(w, s, wjPopSel);
        Plate(box, "wj_btn_y206", 77f, 513f, 206f, 50f, 5f);
        BtnText(box, $"{pick}{WeaponJob.Ro(pick)} 정하기", 180f, 538.5f, 19f * TextScale, edge: OnYellow);
        // 닫기 ✕ (창 오른쪽 위, 강화 창과 같은 그림)
        Plate(box, "wj_close", 314f, 160f, 40f, 40f, 5f);
        JobHit(box, "Close", 314f, 160f, 40f, 40f, () => { ClosePick(); BuildWeaponRows(); });
        JobHit(box, "Ok", 77f, 513f, 206f, 50f, () => {
            if(!WeaponJob.TryPick(w, s, wjPopSel)) { ShowOreShort(WeaponJob.SwapCost(w), "이 칸을 바꾸려면"); return; }
            ClosePick();
            BuildWeaponRows();
            int next = FirstUnpicked(w);   // 아직 안 고른 칸이 더 있으면 이어서
            if(WeaponJob.SlotOpen(w, next) && WeaponJob.Pick(w, next) < 0) OpenPick(w, next, true);
        });
    }
}
