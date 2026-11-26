using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 창 B (2026-11-26 사용자 "뽑기는 B", 미리보기 그대로 = UIGen/gacha.json)
// - 예전 뽑기 페이지(Canvas/Tabs Above Buttons/Gacha Panel)가 열리면 화면 전체를 이 창으로 덮어요 (2026-11-27 전체화면, 닫기 = 예전 닫기 버튼과 같게)
// - 뽑는 중(GachaManager.GachaScreen)에는 숨겨서 예전 뽑기 연출·결과가 그대로 보여요
// - 탭: 장비(특별·일반) / 강화석. 1회·10회는 확인 창 없이 바로 (뽑기권 먼저, 없으면 다이아 — 예전 규칙 그대로)
public partial class RemasterUI
{
    GenView gcView;
    GameObject gcPage;
    int gcTab, gcSub;   // gcTab 0 장비 · 1 강화석 / gcSub 0 특별 · 1 일반
    string gcKey;

    void UpdateGachaPage()
    {
        if(gcPage == null) { gcPage = GenFindPage("Gacha Panel", "Tabs Above Buttons"); if(gcPage == null) return; }
        bool open = gcPage.activeInHierarchy;
        if(!open) { if(gcView != null) { GenClose(gcView); gcView = null; } return; }
        if(gcView == null || gcView.layer == null) { BuildGacha(); return; }
        var gm = GachaManager.Instance;
        bool busy = gm != null && gm.GachaScreen != null && gm.GachaScreen.activeInHierarchy;
        if(gcView.layer.gameObject.activeSelf == busy) gcView.layer.gameObject.SetActive(!busy);
        if(!busy) RefreshGacha(false);
    }

    void BuildGacha()
    {
        gcKey = null;
        // 예전 뽑기 페이지는 가운데 고정 크기(1027x1612)라 그 부모(화면 전체)에 띄워요 (2026-11-27 점검: 전체화면이 안 되던 것)
        gcView = GenBuild("gacha", "GachaB", true, (RectTransform)gcPage.transform.parent);
        if(gcView == null) return;
        // 그리는 순서 = 예전 뽑기 페이지 바로 다음 (맨 뒤에 두면 그 뒤의 상점·알림·패키지 창이 이 창 밑에 가려져요, 2026-11-27 점검)
        gcView.layer.SetSiblingIndex(gcPage.transform.GetSiblingIndex() + 1);
        var v = gcView;
        GenHit(v, "close", () => {
            // 예전 닫기 버튼(Gacha Panel/Top_Roof/Button_Close)과 같이: 페이지 끄기 + 아래 메뉴 탭 모두 기본 모양 (Bottom Buttons 의 ButtonGroup.resetAllTabs)
            //  (2026-11-27 점검: 예전엔 TabGroup 을 찾았는데 아래 메뉴엔 없어서, 닫아도 뽑기 탭이 눌린 모양으로 남았어요)
            gcPage.SetActive(false);
            var bb = GameObject.Find("Bottom Buttons");
            var bgp = bb != null ? bb.GetComponent<ButtonGroup>() : null;
            if(bgp != null && bgp.tabButtons != null) bgp.resetAllTabs();
        });
        GenHit(v, "tab0", () => { gcTab = 0; RefreshGacha(true); });
        GenHit(v, "tab1", () => { gcTab = 1; RefreshGacha(true); });
        GenHit(v, "sub0", () => { gcSub = 0; RefreshGacha(true); });
        GenHit(v, "sub1", () => { gcSub = 1; RefreshGacha(true); });
        GenHit(v, "b1", () => GachaBuy(false));
        GenHit(v, "b10", () => GachaBuy(true));
        GenHit(v, "prob", ShowGachaRates);
        RefreshGacha(true);
    }

    GachaData GachaDataOf(GachaData.GachaType type) =>
        FindObjectsByType<GachaData>(FindObjectsInactive.Include).FirstOrDefault(d => d != null && d.gacha == type);

    GachaData.GachaType GachaTypeOf(bool ten) =>
        gcTab == 1 ? (ten ? GachaData.GachaType.UpgradeStonex10 : GachaData.GachaType.UpgradeStone)
        : gcSub == 0 ? (ten ? GachaData.GachaType.Specialx10 : GachaData.GachaType.Special)
        : (ten ? GachaData.GachaType.Normalx10 : GachaData.GachaType.Normal);

    void GachaBuy(bool ten)
    {
        var gm = GachaManager.Instance;
        var d = GachaDataOf(GachaTypeOf(ten));
        if(gm == null || d == null) { Debug.LogWarning("[뽑기] GachaData 없음 " + GachaTypeOf(ten)); return; }
        // 예전 다이아 부족 창은 뽑기 페이지 안이라 이 창에 가려져요 → 먼저 확인해서 메시지로 (뽑기권이 있으면 그걸 써요 = GachaManager.gacha 와 같은 규칙)
        int tk = gcTab == 1 ? gm.StoneGachaTicket : gcSub == 0 ? gm.SpecialGachaTicket : gm.NormalGachaTicket;
        int needTk = ten ? (gcTab == 1 ? 20 : 10) : 1;
        if(tk < needTk && GameManager.GetDiamond() < d.Price) { ShowMessage("다이아가 부족해요", $"다이아 {d.Price:N0}개가 필요해요 (지금 {GameManager.GetDiamond():N0}개)"); return; }
        gm.BuyDirect(d);
        gcKey = null;
    }

    void ShowGachaRates()
    {
        string body = gcTab == 1 ? "강화석 뽑기: 등급마다 강화석을 받아요"
            : gcSub == 0 ? $"일반 {100f - Tuning.Get("gacha_rate_rare", 34.5f) - Tuning.Get("gacha_rate_epic", 6.7f) - Tuning.Get("gacha_rate_unique", 0.78f) - Tuning.Get("gacha_rate_legend", 0.03f) - Tuning.Get("gacha_rate_ancient", 0.0005f):0.####}%\n레어 {Tuning.Get("gacha_rate_rare", 34.5f)}% · 에픽 {Tuning.Get("gacha_rate_epic", 6.7f)}%\n유니크 {Tuning.Get("gacha_rate_unique", 0.78f)}% · 전설 {Tuning.Get("gacha_rate_legend", 0.03f)}%\n고대 {Tuning.Get("gacha_rate_ancient", 0.0005f)}%\n{GachaManager.PityMax}번마다 유니크 이상"
            : $"일반 {100f - Tuning.Get("gacha_n_rate_rare", 30.47f) - Tuning.Get("gacha_n_rate_epic", 1.5f) - Tuning.Get("gacha_n_rate_unique", 0.03f):0.##}%\n레어 {Tuning.Get("gacha_n_rate_rare", 30.47f)}% · 에픽 {Tuning.Get("gacha_n_rate_epic", 1.5f)}%\n유니크 {Tuning.Get("gacha_n_rate_unique", 0.03f)}%\n천장 없음";
        ShowMessage("확률표", body);
    }

    void RefreshGacha(bool force)
    {
        var v = gcView; var gm = GachaManager.Instance;
        if(v == null || v.layer == null || gm == null) return;
        var d1 = GachaDataOf(GachaTypeOf(false)); var d10 = GachaDataOf(GachaTypeOf(true));
        int tickets = gcTab == 1 ? gm.StoneGachaTicket : gcSub == 0 ? gm.SpecialGachaTicket : gm.NormalGachaTicket;
        string key = $"{gcTab}_{gcSub}_{tickets}_{gm.PitySpecial}_{string.Join(",", GachaManager.Recent.Select(x => (x.sprite != null ? x.sprite.name : "") + (int)x.grade))}";
        if(!force && key == gcKey) return;
        gcKey = key;
        // 탭 (켬/끔 두 모양 중 하나)
        for(int k = 0; k < 2; k++) { GenShow(v, "tab" + k + ".on", gcTab == k); GenShow(v, "tab" + k + ".off", gcTab != k); }
        // 특별/일반 탭: 장비 탭에서만 (강화석 탭에서는 둘 다 숨겨요)
        for(int k = 0; k < 2; k++) { GenShow(v, "sub" + k + ".on", gcTab == 0 && gcSub == k); GenShow(v, "sub" + k + ".off", gcTab == 0 && gcSub != k); }
        // 상자
        GenShow(v, "chest.p", gcTab == 0 && gcSub == 0); GenShow(v, "chest.g", gcTab == 0 && gcSub == 1); GenShow(v, "chest.w", gcTab == 1);
        // 확률 한 줄 · 천장 (특별 뽑기만)
        GenText(v, "rate", gcTab == 1 ? "등급마다 강화석을 한 번에" : gcSub == 0
            ? $"전설 {Tuning.Get("gacha_rate_legend", 0.03f)}% · 고대 {Tuning.Get("gacha_rate_ancient", 0.0005f)}%"
            : $"에픽 {Tuning.Get("gacha_n_rate_epic", 1.5f)}% · 유니크 {Tuning.Get("gacha_n_rate_unique", 0.03f)}%");
        bool pity = gcTab == 0 && gcSub == 0;
        GenShow(v, "pity", pity);
        if(pity) {
            int p = Mathf.Min(gm.PitySpecial, GachaManager.PityMax);
            GenText(v, "pity.text", $"{p}/{GachaManager.PityMax}");
            GenFill(v, "pity.fill", p / (float)GachaManager.PityMax);
        }
        // 최근 뽑은 장비 (장비 탭에서만)
        var rec = GachaManager.Recent;
        for(int k = 0; k < 5; k++) {
            bool on = gcTab == 0 && k < rec.Count;
            GenShow(v, "r" + k, on);
            if(on) GenCell(v, "r" + k, rec[k].sprite, rec[k].grade);
        }
        GenShow(v, "recent", gcTab == 0 && rec.Count > 0);
        // 버튼: 1회 · 10회 (강화석은 20회), 가격
        GenText(v, "b1.label", "1회 · ");
        GenText(v, "b10.label", gcTab == 1 ? "20회 · " : "10회 · ");
        GenText(v, "b1.price", d1 != null ? " " + d1.Price : " -");
        GenText(v, "b10.price", d10 != null ? " " + d10.Price : " -");
        // 뽑기권
        GenShow(v, "tk.r", gcTab == 0 && gcSub == 0); GenShow(v, "tk.n", !(gcTab == 0 && gcSub == 0));
        GenText(v, "tk.text", (gcTab == 1 ? "강화석권 " : gcSub == 0 ? "특별권 " : "뽑기권 ") + tickets);
    }

    // 막대 채우기: 내보낼 때 100% 로 잰 칸을 가로로 줄여요 (왼쪽 기준)
    void GenFill(GenView v, string key, float frac)
    {
        if(v == null || !v.objs.TryGetValue(key, out var l)) return;
        foreach(var g in l) {
            if(g == null) continue;
            var rt = (RectTransform)g.transform;
            if(!genFull.TryGetValue(rt, out float full)) genFull[rt] = full = rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(full * Mathf.Clamp01(frac), rt.sizeDelta.y);
        }
    }
    readonly Dictionary<RectTransform, float> genFull = new Dictionary<RectTransform, float>();

    // 장비 칸 바꾸기: 판은 그 등급 칸 그림(mg_cell_*), 안의 그림은 장비
    static readonly string[] GenCellKey = { "normal", "rare", "epic", "unique", "legend", "ancient" };
    void GenCell(GenView v, string key, Sprite sp, Equips.MaterialClass g)
    {
        if(v == null || !v.objs.TryGetValue(key, out var l)) return;
        bool first = true;
        foreach(var o in l) {
            if(o == null) continue;
            var img = o.GetComponent<Image>();
            if(img == null) continue;
            if(o.name == "Box" && first) {
                img.sprite = MapSprite("mg_cell_" + GenCellKey[Mathf.Clamp((int)g, 0, 5)]);
                // mg_cell 그림은 그림자 여백이 없어요 → 그 여백만큼 칸을 줄여서 크기를 맞춰요 (한 번만)
                if(genPad.TryGetValue(o, out float pad) && pad > 0f) {
                    var rt = (RectTransform)o.transform;
                    rt.sizeDelta -= new Vector2(pad * 2f * K, pad * 2f * K);
                    rt.anchoredPosition += new Vector2(pad * K, -pad * K);
                    genPad[o] = 0f;
                }
                first = false;
            }
            else if(o.name == "Icon") img.sprite = sp;
        }
    }
}
