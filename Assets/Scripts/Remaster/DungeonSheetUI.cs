using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 던전 지도 (2026-11-28 사용자 "던전도 행성 스테이지처럼 이쁘게" → "이대로" = previews/dungeon_map.png, tools/ui_preview/dungeon_map_preview.js)
// - 원래 던전 창(Canvas2/Dungeon/DungeonTab)은 켜 둔 채 안의 그림을 모두 숨기고 그 위에 지도를 얹어요. 원래 '적 정보'·'입장권 추가'·다이아 부족 창은 그대로 (지도보다 위)
// - 행성 지도(PlanetMapUI)와 같은 도우미·같은 px 좌표 (360 × 780, 1px = 3단위): 보라 우주 배경 · 항로(빛 + 점선 + 지나온 금색 길) · 차원문 5개(보스) · 이름판 · 별 + 권장
//   위 = 재화 + 닫기, 입장권 줄(+ = 원래 입장권 추가) · 아래 = 추천 던전 바 · 던전을 누르면 밑에서 올라오는 판 (설명 · 권장/내 전투력 · 적 · 보상 · 적 정보 · 소탕 · 입장)
// - 추천 = 내 전투력으로 깰 수 있는 가장 높은 던전 (없으면 첫 던전). 부족한 던전은 어둡게 + 자물쇠 (그래도 눌러서 들어가 볼 수 있어요)
// - dungeon_sheet 0 이면 원래 창
public partial class RemasterUI
{
    GameObject dsPage;
    RectTransform dsLayer, dsFrame, dsMap, dsTop, dsQuick, dsSheetLayer;
    string dsKey;
    float dsNext;
    int dsSheetSel = -1;
    ScrollRect dsScroll;
    bool dsScrolled;
    const float DsMapH = 650f;   // 지도 내용 높이 (px): 맨 아래 던전 이름·별까지
    readonly List<CanvasGroup> dsHidden = new List<CanvasGroup>();
    static readonly string[] DsPopups = { "MapInfo", "AddDungeonTicket", "Popup_LackDiamond" };
    // 지도 위 차원문 자리 (미리보기 px, 아래에서 위로) · 던전 색
    static readonly Vector2[] DsPos = { new Vector2(104, 545), new Vector2(256, 438), new Vector2(100, 330), new Vector2(258, 225), new Vector2(134, 145) };
    static readonly string[] DsCol = { "#5bd66a", "#b36bff", "#3ee0b0", "#ff7a3d", "#ff3b6a" };

    void UpdateDungeonSheet()
    {
        bool on = Tuning.Get("dungeon_sheet", 1f) > 0f;
        if(dsPage == null) {
            var dm = DungeonManager.instance;
            if(dm == null || dm.DungeonDatas == null || dm.DungeonDatas.Count == 0 || dm.DungeonDatas[0] == null) return;
            for(var t = dm.DungeonDatas[0].transform; t != null; t = t.parent) if(t.name == "DungeonTab") { dsPage = t.gameObject; break; }
            if(dsPage == null) return;
        }
        if(!on || !dsPage.activeInHierarchy) {
            if(dsLayer != null) { Destroy(dsLayer.gameObject); dsLayer = null; dsKey = null; dsSheetSel = -1; }
            if(!on) foreach(var cg in dsHidden) if(cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
            return;
        }
        if(dsLayer == null) DsMakeLayer();
        DsSafe();
        CrispTexts(dsLayer);
        if(Time.unscaledTime < dsNext) return;
        dsNext = Time.unscaledTime + 0.5f;
        var dm0 = DungeonManager.instance;
        DsBuildTop();   // 재화·입장권 줄은 0.5초마다 (작아요)
        string key = dm0.DungeonTicket + "_" + DsMyPower().ToString("0");   // 지도·판은 입장권·전투력이 바뀔 때만
        if(key != dsKey) { dsKey = key; DsBuildMap(); if(dsSheetSel >= 0) DsBuildSheet(dsSheetSel, false); }
    }

    static double DsMyPower() => Player.instance != null ? Player.instance.TotalDamagePower : 0;
    static bool DsCleared(DungeonData d) => PlayerPrefs.GetInt("dungeon_clear_" + d.DungeonName, 0) == 1;
    int DsRecommended(List<DungeonData> list)
    {
        double my = DsMyPower(); int r = 0;
        for(int i = 0; i < list.Count; i++) if(my >= list[i].RequiredDPS) r = i;
        return r;
    }

    void DsMakeLayer()
    {
        var host = (RectTransform)dsPage.transform;
        // 원래 그림은 모두 숨겨요 (팝업 3개만 남겨요)
        foreach(Transform t in host) {
            if(DsPopups.Contains(t.name) || t.name == "DungeonMap") continue;
            var cg = t.GetComponent<CanvasGroup>(); if(cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false;
            if(!dsHidden.Contains(cg)) dsHidden.Add(cg);
        }
        if(softCircle == null) { softCircle = MakeHalo(128); softLine = MakeSoftLine(); dotSprite = MakeDot(32); smallRound = MakeRounded(32, 9); }
        MapFont();
        dsLayer = Stretch("DungeonMap", host);
        int idx = host.childCount - 1;
        foreach(var n in DsPopups) { var t = host.Find(n); if(t != null) idx = Mathf.Min(idx, t.GetSiblingIndex()); }
        dsLayer.SetSiblingIndex(idx);
        // 가로 1080 기준 틀 (행성 지도와 같게)
        var fit = dsLayer.gameObject.AddComponent<RemasterWidthFit>();
        dsFrame = Rect("Frame", dsLayer, new Vector2(1080, 2340), Vector2.zero);
        fit.target = dsFrame; fit.Apply();
        // 배경 (화면을 덮게) — 아래 옛 화면을 못 누르게
        var bgRt = Stretch("Bg", dsFrame);
        var bgBlock = bgRt.gameObject.AddComponent<Image>(); bgBlock.color = Hex("#0b0418"); bgBlock.raycastTarget = true;
        var art = Rect("BgArt", bgRt, new Vector2(1080, 2340), Vector2.zero);
        var bgi = art.gameObject.AddComponent<Image>(); bgi.sprite = MapSprite("dg_bg"); bgi.raycastTarget = false;
        var arf = art.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 1080f / 2340f;
        // 지도 (미리보기 360 × 780 그대로, 위 기준) · 위 · 아래 바 · 판 자리
        // 지도는 스크롤 (2026-11-28 사용자: 밑으로 스크롤이 안 돼요 — 화면이 미리보기 780px 보다 짧으면 아래 던전이 바에 가렸어요)
        var vp = Stretch("Viewport", dsFrame);
        vp.offsetMin = new Vector2(0, (16f + 60f + 6f) * K);   // 아래 바 위까지
        vp.gameObject.AddComponent<RectMask2D>();
        vp.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        dsScroll = vp.gameObject.AddComponent<ScrollRect>();
        dsScroll.horizontal = false; dsScroll.vertical = true; dsScroll.movementType = ScrollRect.MovementType.Elastic; dsScroll.scrollSensitivity = 40f;
        dsScroll.viewport = vp;
        dsMap = Rect("Map", vp, new Vector2(1080, DsMapH * K), Vector2.zero);
        dsMap.anchorMin = dsMap.anchorMax = new Vector2(0.5f, 1f); dsMap.pivot = new Vector2(0.5f, 1f); dsMap.anchoredPosition = Vector2.zero;
        dsScroll.content = dsMap;
        dsScrolled = false;
        dsTop = Rect("Top", dsFrame, new Vector2(1080, 100 * K), Vector2.zero);
        dsTop.anchorMin = dsTop.anchorMax = new Vector2(0.5f, 1f); dsTop.pivot = new Vector2(0.5f, 1f); dsTop.anchoredPosition = Vector2.zero;
        dsQuick = Rect("Quick", dsFrame, new Vector2(1080, 60 * K), Vector2.zero);
        dsQuick.anchorMin = dsQuick.anchorMax = new Vector2(0.5f, 0f); dsQuick.pivot = new Vector2(0.5f, 0f); dsQuick.anchoredPosition = new Vector2(0, 16f * K);
        dsSheetLayer = Stretch("Sheet", dsFrame);
        dsSheetLayer.gameObject.SetActive(false);
        dsKey = null; dsSheetSel = -1;
        StartCoroutine(SlideIn(dsFrame, dsFrame.localScale.x));   // 아래에서 올라와요 (모든 창)
    }

    // 안전 영역 (2026-11-28 사용자: 아이폰에서 위가 잘려요) — 위 줄·지도는 노치 아래로, 아래 바·판은 홈 막대 위로. 배경은 화면 끝까지
    float dsSafeBot;
    void DsSafe()
    {
        if(dsFrame == null || dsTop == null || dsScroll == null) return;
        GenSafe(dsFrame.sizeDelta.y, out float t, out float b);
        dsSafeBot = b;
        dsTop.anchoredPosition = new Vector2(0f, -t);
        var vp = (RectTransform)dsScroll.viewport;
        float gap = Tuning.Get("quick_bar_gap", -2f);   // 아래 바와 화면 아래 사이 (2026-11-28 사용자: 조금 더 낮춰도 돼요 16 → 4, 더 밑으로 → -2: 바 그림자만 화면 밖)
        vp.offsetMin = new Vector2(0f, (gap + 60f + 6f) * K + b);
        vp.offsetMax = new Vector2(0f, -t);
        if(dsQuick != null) dsQuick.anchoredPosition = new Vector2(0f, gap * K + b);
    }

    void DsClose()
    {
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        var back = dsPage != null ? dsPage.transform.Find("Button_Back") : null;   // 원래 뒤로 버튼 그대로 (창 닫기 + 기본 음악)
        var b = back != null ? back.GetComponent<Button>() : null;
        if(b != null) b.onClick.Invoke(); else if(dsPage != null) dsPage.SetActive(false);
    }

    void DsHitBox(Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction act)
    {
        var hit = PxBox("Hit", parent, x, y, w, h);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        hit.gameObject.AddComponent<Button>().onClick.AddListener(() => { if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); act(); });
    }
    static Vector2 Bez(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t) { float u = 1 - t; return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b; }
    // 차원문: 빛 + 흰 차원문 그림(던전 색을 곱해요) + 보스. (cx, cy) = 가운데
    void DsPortal(Transform parent, DungeonData d, int i, float cx, float cy, float size, bool dim)
    {
        Color c = Hex(DsCol[Mathf.Clamp(i, 0, 4)]);
        Halo(parent, cx, cy, size * 1.7f, new Color(c.r, c.g, c.b, dim ? 0.2f : 0.47f));
        float ps = size * 180f / 140f;   // 그림 여백 20 포함
        var p = PxAt("Portal", parent, cx, cy, ps, ps).gameObject.AddComponent<Image>();
        p.sprite = MapSprite("dg_portal"); p.color = dim ? new Color(c.r, c.g, c.b, 0.45f) : c; p.raycastTarget = false;
        var b = PxBox("Boss", parent, cx - size / 2f + size * 0.12f, cy - size / 2f + size * 0.04f, size * 0.76f, size * 0.84f).gameObject.AddComponent<Image>();
        b.sprite = DnBossSprite(d); b.preserveAspect = true; b.raycastTarget = false;
        b.color = b.sprite == null ? Color.clear : dim ? new Color(0.3f, 0.28f, 0.33f) : Color.white;
    }
    string DsStars(DungeonData d)
    {
        int n = Mathf.Clamp((d.DungeonDifficulty ?? "").Count(ch => ch == '★'), 1, 10);
        return $"<color=#ffd23f>{new string('★', n)}</color>" + (n < 5 ? $"<color=#5a6585>{new string('★', 5 - n)}</color>" : "");
    }

    // ---- 지도
    void DsBuildMap()
    {
        var list = DnList();
        var dm = DungeonManager.instance;
        int rec = DsRecommended(list);
        double my = DsMyPower();
        DsBuildRoute(list, rec, my);
        DsBuildQuick(list, rec);
        CrispTexts(dsMap, true); CrispTexts(dsQuick, true);
        if(!dsScrolled && dsScroll != null) { Canvas.ForceUpdateCanvases(); dsScroll.verticalNormalizedPosition = 0f; dsScrolled = true; }   // 처음엔 맨 아래(쉬운 던전)부터
    }

    // 위: 재화 + 닫기 + 입장권 줄
    void DsBuildTop()
    {
        var dm = DungeonManager.instance;
        Clear(dsTop);
        ResBar(dsTop);
        Plate(dsTop, "btn_close", 316f, 9f, 34f, 34f, 7f);
        DsHitBox(dsTop, 310f, 4f, 46f, 44f, DsClose);
        Plate(dsTop, "dg_ticket", 12f, 50f, 336f, 44f, 4f);
        PxIcon(dsTop, "ItemIcon_Ticket_Gold", 12f + 3f + 8f + 15f, 72f, 30f);
        BtnText(dsTop, $"입장권 {dm.DungeonTicket} / 3", 12f + 3f + 8f + 30f + 6f, 72f, 17f, TextAlignmentOptions.Left, 0f, Navy);   // (2026-11-28 사용자: 입장권 글꼴) 버튼과 같은 글꼴
        // 오른쪽 (2026-11-28 사용자: 이 부분 UI 바꿔 줘): 다시 채워지는 시간 칩 + 노랑 둥근 + (안내 창 버튼과 같은 그림)
        //  입장권은 자정(기기 시간)에 3장으로 채워져요 (DungeonManager.SaveData) → 3장 미만이면 "05:12:33 뒤 3장", 가득이면 "가득"
        float px = 12f + 336f - 3f - 6f - 34f;
        bool full = dm.DungeonTicket >= 3;
        var left = System.DateTime.Today.AddDays(1) - System.DateTime.Now;
        string chip = full ? "가득" : $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00} 뒤 3장";
        float cw = MeasureText(chip, 12f) + 20f;
        Plate(dsTop, full ? "chip_yellow" : "chip_dark", px - 6f - cw, 60f, cw, 24f, 1f);
        PxText(dsTop, chip, px - 6f - cw / 2f, 72f, 12f, full ? Hex("#3a2000") : Hex("#e2d6ff"));
        Plate(dsTop, "nt_btn_y", px, 57f, 34f, 30f, 4f);
        BtnText(dsTop, "+", px + 17f, 71.5f, 20f, TextAlignmentOptions.Center, 0f, Hex("#6a2e00"));
        DsHitBox(dsTop, 12f + 336f - 50f, 50f, 50f, 44f, DsAddTicket);
        CrispTexts(dsTop, true);
    }

    void DsBuildRoute(List<DungeonData> list, int rec, double my)
    {
        // 항로: 빛(보라 30%) + 점선 + 지나온 금색 길 (추천 던전까지)
        Clear(dsMap);
        var route = PxBox("Route", dsMap, 0, 0, 360, 780);
        Color acc = Hex("#b36bff"), gold = Hex("#ffd23f");
        int n = Mathf.Min(list.Count, 5);
        var tail = new[] { new Vector2(258, 225), new Vector2(258, 180), new Vector2(150, 185), new Vector2(134, 145) };
        for(int k = 0; k + 1 < n; k++) {
            Vector2 a = DsPos[k], b = DsPos[k + 1];
            Vector2 c1 = a + new Vector2(0, -60f), c2 = b + new Vector2(0, 60f);
            if(k == 3) { c1 = tail[1]; c2 = tail[2]; }
            var pts = new List<Vector2>();
            for(int s = 0; s <= 40; s++) pts.Add(Bez(a, c1, c2, b, s / 40f));
            Stroke(route, pts, 22f, new Color(acc.r, acc.g, acc.b, 0.3f), softLine);
            Dots(route, pts, 3f, 11f, new Color(1, 1, 1, 0.4f));
            if(k < rec) { Stroke(route, pts, 16f, new Color(gold.r, gold.g, gold.b, 0.5f), softLine); Stroke(route, pts, 5f, gold, smallRound); }
        }
        // 차원문
        for(int i = 0; i < n; i++) {
            var d = list[i]; int ii = i;
            var c = DsPos[i];
            bool cur = i == rec, weak = my < d.RequiredDPS, done = DsCleared(d);
            float size = cur ? 104f : i == 4 ? 66f : 80f;
            var node = PxBox("Dungeon" + i, dsMap, c.x, c.y, 0f, 0f);
            if(cur) {
                var ring = PxAt("Ring", node, 0, 0, size + 30f + 3f, size + 30f + 3f).gameObject.AddComponent<Image>();
                ring.sprite = Ring(size + 30f, 3f); ring.color = new Color(1f, 230 / 255f, 120 / 255f, 0.8f); ring.raycastTarget = false;
            }
            DsPortal(node, d, i, 0f, 0f, size, weak);
            if(done && !weak) PxIcon(node, "Icon_Check03_l", size / 2f + 6f - 13f, -size / 2f - 8f + 13f, 26f, 2f);
            if(weak) PxIcon(node, "Icon_Lock03", 0f, 0f, 28f);
            if(cur) {
                float side = c.x < 180f ? -1f : 1f;   // 1 = 왼쪽에 (미리보기). 차원문이 왼쪽이면 오른쪽에 (2026-11-28: 왼쪽 화면 밖으로 잘렸어요)
                var ship = PxAt("Ship", node, side * (-size / 2f - 50f + 22f), -size / 2f + 6f + 22f, 44f, 44f);
                var si = ship.gameObject.AddComponent<Image>(); si.sprite = Skin("Ship_LVL_1"); si.preserveAspect = true; si.raycastTarget = false;
                ship.localRotation = Quaternion.Euler(0, 0, 28f * side);
                float chipW = 1.5f + 9f + MeasureText("추천", 13f) + 9f + 1.5f;
                Chip(node, "chip_yellow", null, "추천", side > 0 ? -size / 2f - 82f : size / 2f + 82f - chipW, -size / 2f + 54f, Hex("#3a2000"));
            }
            float y = size / 2f - 6f;
            y += NamePlate(node, weak ? "plate_dim" : "dg_plate" + Mathf.Clamp(i, 0, 4), DnName(d), 0f, y, cur ? 17f : 15f, weak ? Hex("#9aa0b8") : Color.white);
            // 별 + 권장 칩
            string st = DsStars(d), req = "필요 " + KNum(d.RequiredDPS);   // 이 전투력이 돼야 열려요 (2026-11-28 사용자: 안 열리면 권장이 아니잖아 → 필요)
            int nStar = Mathf.Max(5, Mathf.Clamp((d.DungeonDifficulty ?? "").Count(ch => ch == '★'), 1, 10));
            float sw = nStar * 12f * 0.95f, cw = MeasureText(req, 11f) + 18f, tot = sw + 4f + cw, x0 = -tot / 2f;
            PxText(node, st, x0, y + 4f + 10f, 12f, Color.white, 0f, null, TextAlignmentOptions.Left);
            Plate(node, "chip_dark", x0 + sw + 4f, y + 4f, cw, 20f, 1f);
            PxText(node, req, x0 + sw + 4f + cw / 2f, y + 14f, 11f, weak ? Hex("#ff9a9a") : Color.white);
            DsHitBox(node, -size / 2f, -size / 2f, size, size + 60f, () => DsBuildSheet(ii, true));   // 잠긴 던전은 DsBuildSheet 에서 막아요
        }
    }

    // 아래 바: 추천 던전 · 내 전투력 · 입장 (미리보기 336 × 60)
    void DsBuildQuick(List<DungeonData> list, int rec)
    {
        Clear(dsQuick);
        if(rec >= list.Count) return;
        var d = list[rec];
        var q = PxBox("Bar", dsQuick, 12f, 0f, 336f, 60f);
        q.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        Plate(q, "dg_quick", 0f, 0f, 336f, 60f, 7f);
        float x = 3f + 10f;
        DsPortal(q, d, rec, x + 23f, 30f, 46f, false);
        x += 46f + 10f;
        PxText(q, $"추천 던전 · 내 전투력 {KNum(DsMyPower())}", x, 18f, 12f, Hex("#c9b8ff"), 0f, null, TextAlignmentOptions.Left);
        string name = DnName(d);
        float avail = 336f - 3f - 8f - 104f - 8f - x;
        PxText(q, name, x, 37f, 19f * Mathf.Min(1f, avail / Mathf.Max(1f, MeasureText(name, 19f))), Color.white, 2f, Navy, TextAlignmentOptions.Left);
        QuickButton(q, "btn_yellow_m", "ItemIcon_Gear_Sword", "입장", 336f - 3f - 8f - 104f, 104f, Hex("#6a2e00"), () => DsBuildSheet(rec, true));
    }

    // + = 원래 '입장권 추가' 창 (다이아 100 또는 광고, 하루 3번)
    void DsAddTicket()
    {
        var list = DnList();
        var b = list.Count > 0 ? list[0].DungeonTicektBuyButton : null;
        if(b == null) return;
        if(!b.interactable) { ShowMessage("입장권 추가", "오늘은 더 받을 수 없어요 (하루 3번)"); return; }
        b.onClick.Invoke();
    }

    // ---- 던전 판 (밑에서 올라와요, 미리보기 ② = 행성 판과 같은 틀 360 × 548)
    void DsCloseSheet()
    {
        if(dsSheetLayer == null) return;
        Clear(dsSheetLayer);
        dsSheetLayer.gameObject.SetActive(false);
        dsSheetSel = -1;
    }

    void DsBuildSheet(int j, bool slide)
    {
        var list = DnList();
        if(j < 0 || j >= list.Count) return;
        dsSheetSel = j;
        var d = list[j]; var dm = DungeonManager.instance;
        double my = DsMyPower(); bool ok = my >= d.RequiredDPS;
        // 잠긴 던전 (내 전투력 < 권장) 은 안 열려요 (2026-11-28 사용자: 잠겨 있는데 열 수 있어요)
        if(!ok) { Toast($"전투력 {KNum(d.RequiredDPS)} 이 되면 열려요", new Color(1f, 0.85f, 0.5f)); if(dsSheetSel == j) DsCloseSheet(); return; }
        Clear(dsSheetLayer);
        dsSheetLayer.gameObject.SetActive(true);
        var dimImg = Stretch("Dim", dsSheetLayer).gameObject.AddComponent<Image>();
        dimImg.color = new Color(6 / 255f, 2 / 255f, 20 / 255f, 0.62f);
        var dimBtn = dimImg.gameObject.AddComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.onClick.AddListener(DsCloseSheet);
        var holder = Rect("Holder", dsSheetLayer, new Vector2(1080, 548 * K), Vector2.zero);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f); holder.pivot = new Vector2(0.5f, 0f); holder.anchoredPosition = new Vector2(0f, dsSafeBot);
        float room = dsFrame.sizeDelta.y - 40f;
        // 행성 판처럼 작게 (2026-11-28 사용자: 너무 올라갔어요, 내려 줘) — 아래 끝은 그대로, dungeon_sheet_scale(0.88)배 → 위 끝이 내려와요
        float hs = Mathf.Min(Mathf.Clamp(Tuning.Get("dungeon_sheet_scale", 0.88f), 0.5f, 1f), room / ((548f + 72f) * K));
        holder.localScale = new Vector3(hs, hs, 1f);
        var sheet = PxBox("Panel", holder, 0f, 0f, 360f, 548f);
        var sp = sheet.gameObject.AddComponent<Image>(); sp.sprite = MapSprite("dg_sheet"); sp.raycastTarget = true;
        var s = sheet.transform;

        // 차원문 (left 10, top -70, 140) · 이름 (left 164, top 16, 28px) · 칩 (top 58)
        DsPortal(s, d, j, 10f + 70f, -70f + 70f, 140f, false);
        string title = DnName(d);
        float room2 = 360f - 14f - 40f - 8f - 164f;
        float ts = 28f * Mathf.Min(1f, room2 / Mathf.Max(1f, MeasureText(title, 28f)));
        PxText(s, title, 164f, 16f + 17f, ts, Color.white, 3f * ts / 28f, Navy, TextAlignmentOptions.Left);
        float cx = 164f;
        string st = DsStars(d);
        int nStar = Mathf.Max(5, Mathf.Clamp((d.DungeonDifficulty ?? "").Count(ch => ch == '★'), 1, 10));
        float sw = nStar * 13f * 0.95f + 18f;
        Plate(s, "chip_dark", cx, 58f, sw, 24f, 1f);
        PxText(s, st, cx + 9f, 58f + 12f, 13f, Color.white, 0f, null, TextAlignmentOptions.Left);
        cx += sw + 5f;
        Chip(s, "chip_dark", null, $"몬스터 {d.getTotalEnemy()}", cx, 58f);
        Plate(s, "btn_close", 360f - 14f - 40f, -18f, 40f, 40f, 7f);
        DsHitBox(s, 360f - 14f - 40f, -18f, 40f, 40f, DsCloseSheet);
        // 설명 두 줄 (top 96, 13px, 줄 사이 19.5)
        var desc = DnDescOf(d);
        PxText(s, desc[0], 16f, 96f + 10f, 13f, Hex("#d9d0ff"), 0f, null, TextAlignmentOptions.Left);
        PxText(s, desc[1], 16f, 96f + 29.5f, 13f, Hex("#d9d0ff"), 0f, null, TextAlignmentOptions.Left);
        // 정보 판 (left 16, top 150, 328 × 186)
        float py = 150f;
        Plate(s, "dg_info", 16f, py, 328f, 186f, 0f);
        PxText(s, "필요 전투력", 30f, py + 20f, 12f, Hex("#c9b8ff"), 0f, null, TextAlignmentOptions.Left);
        PxText(s, KNum(d.RequiredDPS), 30f, py + 42f, 22f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        PxText(s, ok ? "충분해요" : "부족해요", 180f, py + 32f, 15f, ok ? Hex("#7dff9a") : Hex("#ff8a8a"), 1f, Navy);
        PxText(s, "내 전투력", 330f, py + 20f, 12f, Hex("#c9b8ff"), 0f, null, TextAlignmentOptions.Right);
        PxText(s, KNum(my), 330f, py + 42f, 22f, ok ? Hex("#7dff9a") : Hex("#ff8a8a"), 2f, Navy, TextAlignmentOptions.Right);
        var line1 = PxBox("Line", s, 30f, py + 64f, 300f, 2f).gameObject.AddComponent<Image>(); line1.color = new Color(1, 1, 1, 0.08f); line1.raycastTarget = false;
        int bosses = d.DungeonEnimes != null ? d.DungeonEnimes.Count(e => e != null && e._typeofEnemy == Enemy.typeofEnemy.DungeonBoss) : 0;
        Kv(s, "ItemIcon_Battle", 22f, "출현 적 ", d.getTotalEnemy().ToString(), 30f, py + 86f);
        Kv(s, "ItemIcon_Skull", 22f, "보스 ", $"{Mathf.Max(1, bosses)}마리", 180f, py + 86f);
        Kv(s, "Icon_Shield", 20f, "내 체력 ", d.playerhealth.ToString(), 30f, py + 114f);
        Kv(s, "ItemIcon_Energy_Blue", 20f, "웨이브 ", $"{(d.spawnAmount != null ? d.spawnAmount.Length : 0)}번", 180f, py + 114f);
        var line2 = PxBox("Line", s, 30f, py + 136f, 300f, 2f).gameObject.AddComponent<Image>(); line2.color = new Color(1, 1, 1, 0.08f); line2.raycastTarget = false;
        PxText(s, "보상", 30f, py + 160f, 13f, Hex("#c9b8ff"), 0f, null, TextAlignmentOptions.Left);
        float rx = 30f + MeasureText("보상", 13f) + 12f;
        rx = Reward(s, "ItemIcon_Money_Coin", 22f, GameManager.MoneyString(d.PriceGold), rx, py + 160f, null) + 12f;
        rx = Reward(s, null, 20f, d.PriceStoneAmount.ToString(), rx, py + 160f, StoneSprite(d.DungeonPriceStone != null ? d.DungeonPriceStone.stoneGrade : UpgradeStone.TypeOfStone.Normal)) + 12f;
        Reward(s, "ItemIcon_Star_Gold", 20f, GameManager.MoneyString(d.PriceEXP), rx, py + 160f, null);
        // 버튼: 적 정보 · 소탕 (top 384, 160 × 44) · 입장 (top 446, 328 × 58)
        DsBtn(s, "dg_btn_b", "ItemIcon_Skull", "적 정보", 16f, 384f, 160f, 44f, 17f, Hex("#0b1f6a"), () => ShowDungeonEnemies(d, j));   // 새 적 정보 창 (EnemyInfoUI.cs)
        bool cleared = DsCleared(d);
        DsBtn(s, "dg_btn_p", "ItemIcon_Trophy_Gold", "소탕", 184f, 384f, 160f, 44f, 17f, Hex("#3a0d66"), () => { dnPage = dsPage; dnSel = j; DungeonSweep(); dsKey = null; }).color = cleared ? Color.white : new Color(0.6f, 0.6f, 0.68f);
        Plate(s, "btn_yellow_l", 16f, 446f, 328f, 58f, 7f);
        float gw = 30f + 8f + MeasureText("입장!", 28f) + 6f + MeasureText("(입장권 1)", 15f), gx = 180f - gw / 2f;
        PxIcon(s, "ItemIcon_Gear_Sword", gx + 15f, 446f + 29f, 30f);
        PxText(s, "입장!", gx + 38f, 446f + 29f, 28f, Color.white, 2f, Hex("#6a2e00"), TextAlignmentOptions.Left);
        PxText(s, "(입장권 1)", gx + 38f + MeasureText("입장!", 28f) + 6f, 446f + 31f, 15f, Color.white, 2f, Hex("#6a2e00"), TextAlignmentOptions.Left);
        DsHitBox(s, 16f, 446f, 328f, 58f, () => { dnPage = dsPage; dnSel = j; DsCloseSheet(); DungeonEnter(); });   // 예전 입장과 같은 순서 (DungeonUI.DungeonEnter)
        CrispTexts(dsSheetLayer, true);
        if(slide) StartCoroutine(SlideIn(holder, hs));
    }

    Image DsBtn(Transform s, string sprite, string icon, string text, float x, float y, float w, float h, float size, Color ol, UnityEngine.Events.UnityAction act)
    {
        var img = Plate(s, sprite, x, y, w, h, 7f);
        float tw = 22f + 6f + MeasureText(text, size), cx = x + w / 2f - tw / 2f;
        PxIcon(s, icon, cx + 11f, y + h / 2f, 22f);
        PxText(s, text, cx + 28f, y + h / 2f, size, Color.white, 2f, ol, TextAlignmentOptions.Left);
        DsHitBox(s, x, y, w, h, act);
        return img;
    }
}
