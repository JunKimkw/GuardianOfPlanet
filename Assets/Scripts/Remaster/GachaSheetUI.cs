using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 뽑기 창 3차 (2026-11-27 사용자 "A 좋다" = previews/gacha3.png, tools/ui_preview/gacha2_preview.js 의 C)
// - 원래 뽑기 창(Gacha Panel)에 있던 그림(판·지붕·리본·닫기·카드·회전 빛·막대·상자 탭·상자·다이아·뽑기권)을 그대로 가져와
//   밑에서 올라오는 판으로 다시 놓아요. 좌표 = 미리보기의 게임 단위 그대로 (화면 1080 × 2048 기준, 화면 아래에 붙여요)
// - 원래 창은 투명하게(CanvasGroup) 두고 그 자리에 이 판. 뽑는 중(GachaScreen)엔 숨겨서 예전 연출 그대로
// - 탭: 0 일반 · 1 스페셜 · 2 강화석. 1회·10회(강화석 20회)는 확인 창 없이 바로 (뽑기권 먼저, 없으면 다이아 — 예전 규칙)
// - gacha_sheet 0 이면 꺼요 (원래 창 그대로)
public partial class RemasterUI
{
    const float GsT = 622f, GsH = 2048f, GsBot = 180f;   // 판 위 끝 · 기준 화면 높이 · 아래 메뉴 높이 (2026-11-27 사용자: 창이 너무 높아 → 낮게, 화면 아래 6~7할)
    GameObject gsPage;
    RectTransform gsLayer, gsRoot;
    CanvasGroup gsPageCg;
    int gsTab = 1;
    string gsKey;
    readonly Dictionary<string, Sprite> gsSpr = new Dictionary<string, Sprite>();
    static readonly string[] GsTabName = { "Normal", "Special", "Stone" };

    void UpdateGachaSheet()
    {
        if(Tuning.Get("gacha_sheet", 1f) <= 0f) return;
        if(gsPage == null) { gsPage = GenFindPage("Gacha Panel", "Tabs Above Buttons"); if(gsPage == null) return; }
        if(!gsPage.activeInHierarchy) {
            if(gsLayer != null) { Destroy(gsLayer.gameObject); gsLayer = null; gsRoot = null; gsKey = null; }
            return;
        }
        if(gsPageCg == null) { gsPageCg = gsPage.GetComponent<CanvasGroup>(); if(gsPageCg == null) gsPageCg = gsPage.AddComponent<CanvasGroup>(); }
        if(gsPageCg.alpha != 0f) { gsPageCg.alpha = 0f; gsPageCg.blocksRaycasts = false; }   // 원래 창은 안 보이게 (그림만 빌려 써요)
        var gm = GachaManager.Instance;
        bool busy = GachaFxBusy || (gm != null && gm.GachaScreen != null && gm.GachaScreen.activeInHierarchy);
        if(gsLayer == null) GsMakeLayer();
        if(gsLayer.gameObject.activeSelf == busy) gsLayer.gameObject.SetActive(!busy);
        if(!busy) GsRefresh();
    }

    // 원래 창의 그림 (경로 = Gacha Panel 아래)
    Sprite GsS(string p)
    {
        if(gsSpr.TryGetValue(p, out var s)) return s;
        var t = gsPage.transform.Find(p);
        var im = t != null ? t.GetComponent<Image>() : null;
        s = im != null ? im.sprite : null;
        if(s == null) Debug.LogWarning("[뽑기 창] 그림 없음: " + p);
        gsSpr[p] = s;
        return s;
    }
    string GsBtnPath(int tab, string sub) => $"Tabs/{GsTabName[tab]} Gacha/Gacha Button/Gacha Buttons/Gacha {GsTabName[tab]} x1/{sub}";

    void GsMakeLayer()
    {
        var host = (RectTransform)gsPage.transform.parent;
        gsLayer = Stretch("GachaSheet", host);
        gsLayer.SetSiblingIndex(gsPage.transform.GetSiblingIndex() + 1);   // 예전 뽑기 페이지 바로 다음 (뒤의 상점·알림 창이 가려지지 않게)
        var dim = Stretch("Dim", gsLayer);
        dim.offsetMin = new Vector2(0f, GsBot);   // 아래 메뉴는 덮지 않아요
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(GsClose);
        gsRoot = Rect("Sheet", gsLayer, new Vector2(1080f, GsH - GsT), Vector2.zero);
        gsRoot.anchorMin = gsRoot.anchorMax = new Vector2(0.5f, 0f);
        gsRoot.pivot = new Vector2(0.5f, 0f);
        // 화면이 낮으면(태블릿 등) 판 전체가 들어오게 줄여요
        float hc = host.rect.height > 0f ? host.rect.height : GsH, wc = host.rect.width > 0f ? host.rect.width : 1080f;
        float s = Mathf.Min(1f, (hc - 20f) / (GsH - GsT), wc / 1080f);
        gsRoot.localScale = new Vector3(s, s, 1f);
        gsKey = null;
        StartCoroutine(GsSlideIn(gsRoot, (GsH - GsT) * s));
    }
    IEnumerator GsSlideIn(RectTransform rt, float dist)
    {
        float t = 0f;
        while(rt != null && t < 0.22f) {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.22f), 3f);
            rt.anchoredPosition = new Vector2(0f, -dist * (1f - k));
            yield return null;
        }
        if(rt != null) rt.anchoredPosition = Vector2.zero;
    }

    void GsClose()
    {
        // 예전 닫기 버튼과 같이: 페이지 끄기 + 아래 메뉴 탭 모두 기본 모양
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        if(gsPage != null) gsPage.SetActive(false);
        var bb = GameObject.Find("Bottom Buttons");
        var bgp = bb != null ? bb.GetComponent<ButtonGroup>() : null;
        if(bgp != null && bgp.tabButtons != null) bgp.resetAllTabs();
    }

    int GsTickets(GachaManager gm) => gsTab == 0 ? gm.NormalGachaTicket : gsTab == 1 ? gm.SpecialGachaTicket : gm.StoneGachaTicket;
    GachaData.GachaType GsType(bool ten) => gsTab == 0 ? (ten ? GachaData.GachaType.Normalx10 : GachaData.GachaType.Normal)
        : gsTab == 1 ? (ten ? GachaData.GachaType.Specialx10 : GachaData.GachaType.Special)
        : (ten ? GachaData.GachaType.UpgradeStonex10 : GachaData.GachaType.UpgradeStone);
    int GsNeedTk(bool ten) => ten ? (gsTab == 2 ? 20 : 10) : 1;

    void GsBuy(bool ten)
    {
        var gm = GachaManager.Instance;
        var d = GachaDataOf(GsType(ten));
        if(gm == null || d == null) { Debug.LogWarning("[뽑기] GachaData 없음 " + GsType(ten)); return; }
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        // 원래 다이아 부족 창은 투명한 원래 창 안이라 안 보여요 → 먼저 확인해서 메시지로
        if(GsTickets(gm) < GsNeedTk(ten) && GameManager.GetDiamond() < d.Price) { ShowMessage("다이아가 부족해요", $"다이아 {d.Price:N0}개가 필요해요 (지금 {GameManager.GetDiamond():N0}개)"); return; }
        gm.BuyDirect(d);
        gsKey = null;
    }

    // 등급별 확률 (높은 등급부터). 강화석은 GachaManager.StoneGacha 의 값
    (string g, float p)[] GsRates()
    {
        if(gsTab == 1) {
            float r = Tuning.Get("gacha_rate_rare", 34.5f), e = Tuning.Get("gacha_rate_epic", 6.7f), u = Tuning.Get("gacha_rate_unique", 0.78f), l = Tuning.Get("gacha_rate_legend", 0.03f), a = Tuning.Get("gacha_rate_ancient", 0.0005f);
            return new[] { ("ancient", a), ("legend", l), ("unique", u), ("epic", e), ("rare", r), ("normal", 100f - r - e - u - l - a) };
        }
        if(gsTab == 0) {
            float r = Tuning.Get("gacha_n_rate_rare", 30.47f), e = Tuning.Get("gacha_n_rate_epic", 1.5f), u = Tuning.Get("gacha_n_rate_unique", 0.03f);
            return new[] { ("unique", u), ("epic", e), ("rare", r), ("normal", 100f - r - e - u) };
        }
        return new[] { ("ancient", 0.1f), ("legend", 1.9f), ("unique", 8f), ("epic", 13f), ("rare", 52f), ("normal", 25f) };
    }
    static readonly Dictionary<string, (string name, string col)> GsGrade = new Dictionary<string, (string, string)> {
        { "normal", ("일반", "#cfe0ea") }, { "rare", ("레어", "#7fd8ff") }, { "epic", ("에픽", "#e3b6ff") },
        { "unique", ("유니크", "#ffe14a") }, { "legend", ("전설", "#ff8a8a") }, { "ancient", ("고대", "#ffb35c") } };
    static string GsPct(float p) => p >= 10f ? p.ToString("0.##") + "%" : p >= 0.01f ? p.ToString("0.###") + "%" : p.ToString("0.####") + "%";

    void GsRefresh()
    {
        var gm = GachaManager.Instance;
        if(gm == null || gsRoot == null) return;
        string stones = "";
        if(gsTab == 2 && UpgradeStoneManager.instance != null)
            foreach(UpgradeStone.TypeOfStone ty in System.Enum.GetValues(typeof(UpgradeStone.TypeOfStone))) { var st = UpgradeStoneManager.instance.getStone(ty); stones += (st != null ? st.StoneAmount : 0) + ","; }
        string key = $"{gsTab}_{EquipBag.Count}_{EquipBag.Size}_{Tuning.Get("gacha_stone_ticket_angle", 30f)}_{GsTickets(gm)}_{gm.PitySpecial}_{gm.PityStone}_{stones}_{string.Join(",", GachaManager.Recent.Select(x => (x.sprite != null ? x.sprite.name : "") + (int)x.grade))}";
        if(key == gsKey) return;
        gsKey = key;
        for(int i = gsRoot.childCount - 1; i >= 0; i--) Destroy(gsRoot.GetChild(i).gameObject);
        GsBuildContent(gm);
    }

    // ---- 게임 단위 그리기 도구 (미리보기 gacha2_preview.js 와 같은 뜻: x·y = 화면 1080×2048 의 왼쪽 위 기준)
    RectTransform GsRect(float x, float y, float w, float h)
    {
        var rt = Rect("E", gsRoot, new Vector2(w, h), Vector2.zero);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -(y - GsT));
        return rt;
    }
    // 판 (9-slice) — 원래 그림 × 색 (미리보기 box)
    Image GsBox(Sprite s, float x, float y, float w, float h, Color c)
    {
        var im = GsRect(x, y, w, h).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        if(s != null && s.border.sqrMagnitude > 0f) { im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 1f; }
        return im;
    }
    // 그림 (늘려서) — 미리보기 pic
    Image GsPic(Sprite s, float x, float y, float w, float h, Color c, bool keepAspect = false)
    {
        var im = GsRect(x, y, w, h).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = s != null ? c : Color.clear; im.raycastTarget = false; im.preserveAspect = keepAspect;
        return im;
    }
    // 글자: (x, y) = 가운데 줄. align 0 가운데 · 1 왼쪽 끝 · 2 오른쪽 끝
    TextMeshProUGUI GsTx(string s, float x, float y, float size, Color c, int align = 0)
    {
        float w = 1000f, h = size * 1.4f;
        var rt = GsRect(align == 0 ? x - w / 2f : align == 1 ? x : x - w, y - h / 2f, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = MapFont(); t.text = s; t.fontSize = size; t.richText = true;
        t.alignment = align == 0 ? TextAlignmentOptions.Midline : align == 1 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
        GameFont(t, true);
        t.color = c;
        return t;
    }
    void GsHit(float x, float y, float w, float h, UnityEngine.Events.UnityAction act)
    {
        var rt = GsRect(x, y, w, h);
        rt.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        rt.gameObject.AddComponent<Button>().onClick.AddListener(act);
    }
    static Color GsC(string hex, float a = 1f) => Hex(hex, a);

    void GsBuildContent(GachaManager gm)
    {
        Color W = Color.white;
        const float T = GsT;
        int tk = GsTickets(gm);
        // 판 · 빛 · 지붕 · 리본 제목 · 닫기
        float sh = GsH - GsBot - 24f - T;   // 아래 메뉴 위 24 에서 끝나요 (2026-11-27 사용자: 아래 메뉴 버튼을 가려요)
        GsBox(GsS("Background"), 0f, T, 1080f, sh, GsC("#0796f8")).raycastTarget = true;   // 판을 눌러도 닫히지 않게
        GsBox(GsS("Glow"), 6f, T + 40f, 1068f, sh - 46f, new Color(98f / 255f, 85f / 255f, 1f, 0.9f));
        GsBox(GsS("Top_Roof"), 6f, T + 4f, 1068f, 150f, GsC("#0e7ff2"));
        GsBox(GsS("Top_Roof/Title_Ribbon03"), 260f, T + 26f, 560f, 102f, W);
        GsTx("장비 뽑기", 540f, T + 70f, 50f, W);
        GsPic(GsS("Top_Roof/Button_Close"), 950f, T + 30f, 94f, 96f, W);
        GsPic(GsS("Top_Roof/Button_Close/Icon"), 975f, T + 52f, 44f, 44f, W);
        GsHit(950f, T + 30f, 94f, 96f, GsClose);
        // 지붕 왼쪽: 지금 탭의 뽑기권 수 (2026-11-27 사용자: 아래 '뽑기권 10장' 줄이 이상해 → 위 칩으로)
        var taper = GsS("Tabs/Normal Gacha/Gacha Button/Title_Tapered");
        //  (2026-11-27 사용자: 티켓 부분 새로 맞춰 줘) 둥근 어두운 판 + [뽑기권 그림 | 숫자] 한 덩어리를 판 가운데에. 판 폭 = 내용 + 양옆 22
        var chip = GsBox(GsS("Bottom/Normal Gacha"), 40f, T + 47f, 200f, 62f, new Color(6f / 255f, 20f / 255f, 39f / 255f, 0.6f));
        float gw = GsIconText(GsTicket(), tk.ToString("N0") + "장", 0f, T + 78f, 36f, 36f, W, -1f, true);
        float chipW = Mathf.Max(150f, gw + 44f);
        chip.rectTransform.sizeDelta = new Vector2(chipW, 62f);
        GsIconText(GsTicket(), tk.ToString("N0") + "장", 40f + chipW / 2f, T + 78f, 36f, 36f, W, -1f, false, GsTicketColor());

        // 상자 탭 3개 (아이콘이 탭 위로 30 튀어나와요)
        float tw = 316f, tg = 26f, ty = T + 190f, th = 110f;
        string[] tabLabel = { "일반 뽑기", "스페셜 뽑기", "강화석 뽑기" };
        for(int k = 0; k < 3; k++) {
            int kk = k; bool on = k == gsTab;
            float x = 40f + k * (tw + tg);
            GsBox(GsS("Bottom/Normal Gacha"), x, ty, tw, th, on ? GsC("#ffc21a") : GsC("#3181ff"));
            GsBox(GsS("Bottom/Normal Gacha/Light"), x + 5f, ty + 6f, tw - 10f, 30f, new Color(1f, 1f, 1f, 0.3f));
            GsPic(GsS(GsBtnPath(k, "Icon_GemPack_1")), x + tw / 2f - 52f, ty - 30f, 104f, 98f, W);
            GsTx(tabLabel[k], x + tw / 2f, ty + th - 28f, 34f, on ? W : GsC("#e6f2ff"));
            GsHit(x, ty - 30f, tw, th + 30f, () => { if(gsTab == kk) return; gsTab = kk; if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); gsKey = null; });
        }

        // 대표 카드: 왼쪽 장비 무리 / 오른쪽 제목 + 확률 표 / 아래 천장
        float cy = ty + th + 24f, ch = 460f;
        string ui = $"Tabs/{GsTabName[gsTab]} Gacha/Gacha UI/UI";
        GsBox(GsS("Tabs/Normal Gacha/Gacha UI/UI"), 40f, cy, 1000f, ch, GsC("#509be8"));
        GsPic(GsS("Tabs/Normal Gacha/Gacha UI/UI/Glow"), 40f, cy, 1000f, ch, new Color(1f, 1f, 1f, 0.45f));
        GsShowcase(ui, 76f, cy + 40f, 1.02f);
        float rx = 500f, rw = 510f;
        GsBox(GsS("Tabs/Normal Gacha/Gacha UI/Title"), rx, cy + 20f, rw, 86f, GsC("#178bff"));
        GsTx(gsTab == 0 ? "일반 장비 뽑기" : gsTab == 1 ? "스페셜 장비 뽑기" : "강화석 뽑기", rx + rw / 2f, cy + 63f, 38f, W);
        var rates = GsRates();
        float cw = (rw - 14f) / 2f;
        for(int k = 0; k < rates.Length; k++) {
            float x = rx + (k % 2) * (cw + 14f), y = cy + 122f + (k / 2) * 56f;
            GsBox(taper, x, y, cw, 48f, new Color(6f / 255f, 20f / 255f, 39f / 255f, 0.4f));
            // (등급 색 네모는 뺐어요 — 2026-11-27 사용자. 등급은 글자 색으로 알아요)
            GsTx(GsGrade[rates[k].g].name, x + 16f, y + 24f, 27f, GsC(GsGrade[rates[k].g].col), 1);
            GsTx(GsPct(rates[k].p), x + cw - 12f, y + 24f, 27f, W, 2);
        }
        float by = cy + 330f;
        if(gsTab == 0) {
            // 가방 칸 막대 (2026-11-27 사용자): 천장 막대와 같은 자리·모양. 90% 넘으면 빨갛게 (가득 차면 뽑기가 막혀요)
            int bc = EquipBag.Count, bs = Mathf.Max(1, EquipBag.Size);
            bool nearFull = bc >= bs * 0.9f;
            GsTx("가방", 90f, by + 28f, 32f, GsC("#ffe14a"), 1);
            GsBox(GsS("Tabs/Normal Gacha/Gacha UI/Slider_Level/Bg"), 180f, by, 800f, 56f, W);
            if(bc > 0) GsBox(GsS("Tabs/Normal Gacha/Gacha UI/Slider_Level/Bg/FillMask/FillArea/Fill"), 184f, by + 5f, Mathf.Max(40f, 792f * Mathf.Clamp01(bc / (float)bs)), 46f, nearFull ? new Color(1f, 0.42f, 0.38f) : W);
            GsTx($"{bc} / {bs}", 580f, by + 29f, 34f, W);
            GsTx(nearFull ? "가방이 거의 찼어요 · 가득 차면 뽑을 수 없어요" : "가방이 가득 차면 뽑을 수 없어요", 580f, by + 84f, 27f, nearFull ? GsC("#ffb0a8") : GsC("#e6f4ff"));
        } else {
            int pmax = gsTab == 1 ? GachaManager.PityMax : 200;
            int p = Mathf.Clamp(gsTab == 1 ? gm.PitySpecial : gm.PityStone, 0, pmax);
            GsTx("천장", 90f, by + 28f, 32f, GsC("#ffe14a"), 1);
            GsBox(GsS("Tabs/Normal Gacha/Gacha UI/Slider_Level/Bg"), 180f, by, 800f, 56f, W);
            if(p > 0) GsBox(GsS("Tabs/Normal Gacha/Gacha UI/Slider_Level/Bg/FillMask/FillArea/Fill"), 184f, by + 5f, Mathf.Max(40f, 792f * p / pmax), 46f, W);
            GsTx($"{p} / {pmax}", 580f, by + 29f, 34f, W);
            GsTx(gsTab == 1 ? $"{pmax}번마다 유니크 이상 확정" : $"{pmax}번마다 고대 강화석 확정", 580f, by + 84f, 27f, GsC("#e6f4ff"));
        }

        // 최근 뽑은 장비 (장비 탭) / 가진 강화석 (강화석 탭)
        float ry = cy + ch + 24f, cs = 136f, cyc = ry + 40f;
        if(gsTab == 2) {
            GsTx("가진 강화석", 60f, ry + 16f, 30f, GsC("#e6f4ff"), 1);
            string[] g6 = { "ancient", "legend", "unique", "epic", "rare", "normal" };
            UpgradeStone.TypeOfStone[] t6 = { UpgradeStone.TypeOfStone.Ancient, UpgradeStone.TypeOfStone.Legendary, UpgradeStone.TypeOfStone.Unique, UpgradeStone.TypeOfStone.Epic, UpgradeStone.TypeOfStone.Rare, UpgradeStone.TypeOfStone.Normal };
            float gap = (1000f - cs * 6f) / 5f;
            for(int k = 0; k < 6; k++) {
                float x = 40f + k * (cs + gap);
                var st = UpgradeStoneManager.instance != null ? UpgradeStoneManager.instance.getStone(t6[k]) : null;
                GsPic(MapSprite("mg_cell_" + g6[k]), x, cyc, cs, cs, W);
                GsTx(GsGrade[g6[k]].name, x + cs / 2f, cyc + 44f, 28f, W);
                GsTx((st != null ? st.StoneAmount : 0).ToString("N0"), x + cs / 2f, cyc + 94f, 34f, W);
            }
        } else {
            GsTx("최근 뽑은 장비", 60f, ry + 16f, 30f, GsC("#e6f4ff"), 1);
            GsTx("좋은 것부터", 1020f, ry + 16f, 26f, GsC("#bfe0ff"), 2);
            var rec = GachaManager.Recent;
            float cg = (1000f - cs * 5f) / 4f;
            for(int k = 0; k < 5; k++) {
                float x = 40f + k * (cs + cg);
                if(k < rec.Count) {
                    GsPic(MapSprite("mg_cell_" + GenCellKey[Mathf.Clamp((int)rec[k].grade, 0, 5)]), x, cyc, cs, cs, W);
                    GsPic(rec[k].sprite, x + cs * 0.14f, cyc + cs * 0.12f, cs * 0.72f, cs * 0.72f, W, true);
                } else GsPic(MapSprite("mg_cell_normal"), x, cyc, cs, cs, new Color(1f, 1f, 1f, 0.3f));
            }
        }

        // 사는 버튼 2개
        float yb = cyc + cs + 24f, bw = (1000f - 24f) / 2f;
        GsBuyBtn(40f, yb, bw, 150f, false, tk);
        GsBuyBtn(40f + bw + 24f, yb, bw, 150f, true, tk);
    }

    // 대표 장비 무리: 큰 회전 빛 + 장비 5개(앞 4개는 작은 회전 빛 위). 장비 그림은 원래 창 그 탭의 것
    void GsShowcase(string ui, float x, float y, float s)
    {
        var rot = GsS("Tabs/Normal Gacha/Gacha UI/UI/Items/Equip Image_01");
        Color rc = GsC("#fff4c4");
        GsPic(rot, x + 26f * s, y, 300f * s, 300f * s, rc);
        var items = new List<Sprite>();
        var it = gsPage.transform.Find(ui + "/Items");
        if(it != null) foreach(Transform c in it) if(c.name == "Equips") { var e = c.Find("Equip Image"); var im = e != null ? e.GetComponent<Image>() : null; if(im != null) items.Add(im.sprite); }
        float[,] off = { { 216, 10 }, { -26, 10 }, { -11, 137 }, { 184, 137 }, { 85, 86 } };
        for(int i = 0; i < 5 && i < items.Count; i++) {
            float dx = off[i, 0], dy = off[i, 1];
            if(i < 4) GsPic(rot, x + dx * s, y + dy * s, 150f * s, 150f * s, rc);
            GsPic(items[i], x + (dx + 10f) * s, y + (dy + 10f) * s, 130f * s, 130f * s, Color.white, true);
        }
    }

    // 사는 버튼: 왼쪽 상자 칸 | 오른쪽 칸 가운데에 '1회' 와 값 한 줄 (뽑기권이 넉넉하면 뽑기권 그림 + 장 수, 아니면 다이아 + 값)
    //  (2026-11-27 사용자: '1장' 배치가 이상해 → 그림을 글자 높이에 맞추고, 그림 + 글을 한 덩어리로 가운데)
    void GsBuyBtn(float x, float y, float w, float h, bool ten, int tickets)
    {
        var d = GachaDataOf(GsType(ten));
        bool gold = ten;
        float ib = h - 28f, rx = x + 14f + ib + (w - 14f - ib) / 2f;
        string bp = GsBtnPath(0, "");
        GsBox(GsS(bp + "Bg"), x, y, w, h, gold ? GsC("#ffb21a") : GsC("#509be8"));
        GsBox(GsS(bp + "InnerBg"), x + 14f, y + 14f, ib, ib, gold ? GsC("#ffe27a") : GsC("#6ed7fe"));
        GsBox(GsS(bp + "Light"), x + 4f, y + 4f, w - 8f, h - 8f, new Color(1f, 1f, 1f, 0.47f));
        GsPic(GsS(GsBtnPath(gsTab, "Icon_GemPack_1")), x + 14f + ib * 0.1f, y + 14f + ib * 0.12f, ib * 0.8f, ib * 0.76f, Color.white);
        GsTx(ten ? (gsTab == 2 ? "20회" : "10회") : "1회", rx, y + h * 0.32f, 46f, Color.white);
        int need = GsNeedTk(ten);
        bool useTk = tickets >= need;
        string price = useTk ? need.ToString("N0") + "장" : (d != null ? d.Price.ToString("N0") : "-");   // 뽑기권이면 "1장" (사용자)
        GsIconText(useTk ? GsTicket() : GsS(bp + "Group_Price/Icon"), price, rx, y + h * 0.7f, 40f, 42f, Color.white, -1f, false, useTk ? GsTicketColor() : Color.white);
        GsHit(x, y, w, h, () => GsBuy(ten));
    }

    // [그림 | 글] 한 덩어리: 그림 높이 ih(폭은 그림 비율대로), 글은 실제로 그려진 글자 범위로 재서 그림과 세로 가운데를 맞춰요
    //  cx = 덩어리 가운데 x, cy = 가운데 줄. measureOnly 면 그리지 않고 폭만 (판 크기 정하기용). 돌려주는 값 = 덩어리 폭
    // 뽑기권 그림: 원래 그 탭의 뽑기권 (일반 은색 · 스페셜 금색 · 강화석 빨간 별 표 ItemIcon_Ticket_Red)
    //  강화석 표는 대각선 + 여백이 커서 작아 보여요 → 32도 눕혀 가로로, 다른 표와 같은 폭(높이 × 2)으로 (2026-11-27 사용자: previews/stone_ticket2.png 의 A2)
    Sprite GsTicket() => GsS(GsBtnPath(gsTab, "Group_Price/Icon_01"));
    Color GsTicketColor() => Color.white;
    float GsIconText(Sprite ic, string s, float cx, float cy, float ih, float size, Color c, float gap = -1f, bool measureOnly = false, Color? icColor = null)
    {
        if(gap < 0f) gap = ih * 0.25f;
        bool lay = gsTab == 2 && ic != null && ic == GsTicket();   // 강화석 표: 눕혀 그려요
        float iw = lay ? ih * 2f : ic != null && ic.rect.height > 0f ? ih * ic.rect.width / ic.rect.height : ih;
        var t = GsTx(s, 0f, cy, size, c, 1);
        t.ForceMeshUpdate();
        var tb = t.textBounds;
        float tw = tb.size.x, total = iw + gap + tw;
        if(measureOnly) { Destroy(t.gameObject); return total; }
        float gx = cx - total / 2f;
        var rt = t.rectTransform;
        float dy = tb.center.y + rt.rect.height / 2f;   // 글자 가운데 - 칸 가운데 (칸 기준점 = 왼쪽 위)
        rt.anchoredPosition = new Vector2(gx + iw + gap - tb.min.x, rt.anchoredPosition.y - dy);
        if(lay) {
            float sq = ih * 256f / 159f;   // 그림 칸 (여백 포함 정사각형): 눕힌 표 높이 = 칸의 159/256 (잰 값) → 다른 표와 같은 높이 ih
            var lrt = GsPic(ic, gx + iw / 2f - sq / 2f, cy - sq / 2f, sq, sq, icColor ?? Color.white).rectTransform;
            lrt.pivot = new Vector2(0.5f, 0.5f);   // 가운데를 축으로 돌려요 (기준점을 옮긴 만큼 자리도)
            lrt.anchoredPosition += new Vector2(sq / 2f, -sq / 2f);
            lrt.localRotation = Quaternion.Euler(0f, 0f, -Tuning.Get("gacha_stone_ticket_angle", 30f));   // 시계 방향 각도. 원래 그림의 표가 30도 기울어 있어요(잰 값) → 30 = 바닥과 평행 (사용자: 다른 표처럼 평행하게)
        } else GsPic(ic, gx, cy - ih / 2f, iw, ih, icColor ?? Color.white);
        return total;
    }

    void GsRatesMsg()
    {
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        var r = GsRates();
        var lines = new List<string>();
        for(int i = 0; i < r.Length; i += 2) lines.Add(string.Join(" · ", r.Skip(i).Take(2).Select(x => GsGrade[x.g].name + " " + GsPct(x.p))));
        lines.Add(gsTab == 0 ? "천장 없음" : gsTab == 1 ? $"{GachaManager.PityMax}번마다 유니크 이상 확정" : "200번마다 고대 강화석 확정");
        ShowMessage("확률표", string.Join("\n", lines));
    }
}
