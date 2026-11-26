using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 던전 창 A + 입장 설명 창 (2026-11-26 사용자 "던전 A, 입장 누르면 그 던전 설명이 들어간 창", 미리보기 그대로 = UIGen/dungeon · dungeon_pop)
// - 예전 던전 페이지(Canvas2/Dungeon/DungeonTab)가 열리면 그 위를 덮어요. 닫기 = 예전 뒤로 버튼 그대로
// - 줄 = 던전 (권장 DPS 낮은 순). 그림 = 그 던전의 보스 그림(예전 창 그대로). '입장' → 설명 창 → 입장 (예전과 같이 EnterDungeon + 던전 창 닫기)
// - 소탕: 한 번 깬 던전은 입장권 1장으로 바로 보상 (깬 기록 = PlayerPrefs "dungeon_clear_이름", DungeonManager.dungeonClear 에서 남겨요)
public partial class RemasterUI
{
    GenView dnView, dnPop;
    GameObject dnPage;
    string dnKey;
    // 설명 (두 줄씩) — 던전 이름으로 찾아요 (2026-11-27 점검: 목록 순서로 찾아서 다른 던전의 설명이 나왔어요)
    //  글은 실제 데이터에 맞췄어요: 사바나 적 체력 800~2,500 · 망령의 집 빠른 적(속도 0.55)과 단단한 적(7만), 보스 70만
    //  · 숲 가장 느린 적(0.15)이 가장 단단(50만) · 아폴로 적이 0.4~1초마다 · 메이즈 적 체력 1억 넘음, 기절 저항 50 (다른 던전 15~30)
    static readonly Dictionary<string, string[]> DnDesc = new Dictionary<string, string[]> {
        { "사바나 평원", new[] { "넓은 평원에서 약한 적들이 몰려와요.", "처음 들어가 보기 좋은 던전이에요." } },
        { "망령의 집", new[] { "빠른 망령과 단단한 망령이 섞여 와요.", "보스는 일반 망령보다 10배 이상 단단해요." } },
        { "그린우드 숲", new[] { "덩굴 숲에서 단단한 적들이 몰려와요.", "가장 느린 적이 가장 단단해요." } },
        { "아폴로의 불타는 산", new[] { "불타는 산에서 적들이 쉴 새 없이 쏟아져요.", "범위 공격 무기로 한꺼번에 쓸어요." } },
        { "다크 메이즈 던전", new[] { "어둠 속 미로에 가장 강한 적들이 숨어 있어요.", "기절이 잘 안 걸려요. 보스 피해를 올려요." } },
    };
    static string[] DnDescOf(DungeonData d) =>
        DnDesc.TryGetValue(DnName(d), out var s) ? s : new[] { $"권장 전투력 {KNum(d.RequiredDPS)}의 던전이에요.", "보스까지 쓰러뜨리면 보상을 받아요." };
    // 던전 그림 = 예전 던전 창에서 그 던전 줄의 보스 그림(BossImage) (2026-11-27 점검: 미리보기 그림은 던전과 상관없는 그림이었어요). 없으면 미리보기 그림 그대로
    static Sprite DnBossSprite(DungeonData d)
    {
        if(d == null) return null;
        foreach(var im in d.GetComponentsInChildren<Image>(true)) if(im != null && im.name == "BossImage" && im.sprite != null) return im.sprite;
        return null;
    }

    List<DungeonData> DnList()
    {
        var dm = DungeonManager.instance;
        if(dm == null || dm.DungeonDatas == null) return new List<DungeonData>();
        return dm.DungeonDatas.Where(d => d != null).OrderBy(d => d.RequiredDPS).ToList();
    }
    static string DnName(DungeonData d)
    {
        var toks = System.Text.RegularExpressions.Regex.Split((d.DungeonName ?? "").Trim(), @"\s+");
        if(toks.Length >= 3 && toks.All(t => t.Length == 1)) return string.Concat(toks.Take(toks.Length - 1)) + " " + toks.Last();   // "망 령 의  집" → "망령의 집"
        return string.Join(" ", toks);
    }
    // 큰 수를 한국어로 (5천 · 8만 · 50만 · 1천만 · 3억 · 2조)
    static string KNum(double v)
    {
        string T(double x) => x >= 100 ? x.ToString("0") : x.ToString("0.#");
        if(v >= 1e12) return T(v / 1e12) + "조";
        if(v >= 1e8) return T(v / 1e8) + "억";
        if(v >= 1e7) return T(v / 1e7) + "천만";
        if(v >= 1e4) return T(v / 1e4) + "만";
        if(v >= 1e3) return T(v / 1e3) + "천";
        return v.ToString("0");
    }

    void UpdateDungeonPage()
    {
        if(dnPage == null) {
            var dm = DungeonManager.instance;
            if(dm == null || dm.DungeonDatas == null || dm.DungeonDatas.Count == 0 || dm.DungeonDatas[0] == null) return;
            for(var t = dm.DungeonDatas[0].transform; t != null; t = t.parent) if(t.name == "DungeonTab") { dnPage = t.gameObject; break; }
            if(dnPage == null) return;
        }
        if(!dnPage.activeInHierarchy) {
            if(dnView != null) { GenClose(dnView); dnView = null; }
            if(dnPop != null) { GenClose(dnPop); dnPop = null; }
            return;
        }
        if(dnView == null || dnView.layer == null) BuildDungeon();
        else RefreshDungeon(false);
    }

    void BuildDungeon()
    {
        dnKey = null;
        dnView = GenBuild("dungeon", "DungeonA", true, (RectTransform)dnPage.transform);
        if(dnView == null) return;
        var v = dnView;
        GenHit(v, "close", DungeonBack);
        var list = DnList();
        for(int j = 0; j < 5; j++) {
            int jj = j;
            if(j < list.Count) GenHit(v, "d" + j + ".go", () => ShowDungeonPop(jj), 4f);
        }
        RefreshDungeon(true);
    }

    void DungeonBack()
    {
        // 예전 뒤로 버튼 그대로 (창 닫기 + 기본 음악)
        var back = dnPage != null ? dnPage.transform.Find("Button_Back") : null;
        var b = back != null ? back.GetComponent<Button>() : null;
        if(b != null) b.onClick.Invoke(); else if(dnPage != null) dnPage.SetActive(false);
    }

    void RefreshDungeon(bool force)
    {
        var v = dnView; var dm = DungeonManager.instance;
        if(v == null || v.layer == null || dm == null) return;
        string key = dm.DungeonTicket.ToString();
        if(!force && key == dnKey) return;
        dnKey = key;
        GenText(v, "tk", $"입장권 {dm.DungeonTicket} / 3");
        var list = DnList();
        for(int j = 0; j < 5; j++) {
            bool on = j < list.Count;
            GenShow(v, "d" + j, on);
            if(!on) continue;
            var d = list[j];
            GenText(v, "d" + j + ".name", DnName(d));
            GenText(v, "d" + j + ".stars", d.DungeonDifficulty);
            GenText(v, "d" + j + ".dps", "권장 전투력 " + KNum(d.RequiredDPS));
            var bs = DnBossSprite(d);
            if(bs != null && v.objs.TryGetValue("d" + j + ".img", out var il))
                foreach(var o in il) { var im = o != null ? o.GetComponent<Image>() : null; if(im != null && im.sprite != bs) im.sprite = bs; }
            GenShow(v, "d" + j + ".lock", false);
            GenShow(v, "d" + j + ".go", true);
        }
        if(dnPop != null && dnPop.layer != null) RefreshDungeonPop();
    }

    int dnSel;
    void ShowDungeonPop(int j)
    {
        var list = DnList();
        if(j >= list.Count) return;
        dnSel = j;
        dnPop = GenBuild("dungeon_pop", "DungeonPop", true, (RectTransform)dnPage.transform, new Color(0f, 0f, 0f, 0.6f));
        if(dnPop == null) return;
        var v = dnPop;
        GenHit(v, "close", () => { GenClose(v); dnPop = null; });
        GenHit(v, "go", DungeonEnter);
        GenHit(v, "sweep", DungeonSweep);
        // 그림 = 목록 그 줄의 그림
        if(dnView != null && dnView.objs.TryGetValue("d" + j + ".img", out var src) && v.objs.TryGetValue("img", out var dst)) {
            var si = src.Select(o => o != null ? o.GetComponent<Image>() : null).FirstOrDefault(x => x != null);
            foreach(var o in dst) { var di = o != null ? o.GetComponent<Image>() : null; if(di != null && si != null) di.sprite = si.sprite; }
        }
        RefreshDungeonPop();
    }

    void RefreshDungeonPop()
    {
        var v = dnPop; var dm = DungeonManager.instance;
        var list = DnList();
        if(v == null || v.layer == null || dm == null || dnSel >= list.Count) return;
        var d = list[dnSel];
        GenRibbon(v, "title", DnName(d));
        GenText(v, "stars", d.DungeonDifficulty);
        var desc = DnDescOf(d);
        GenTextFit(v, "desc", desc[0], 280f, 0); GenTextFit(v, "desc", desc[1], 280f, 1);   // 창 안 폭(40~320) 넘으면 줄여요
        GenText(v, "req", KNum(d.RequiredDPS));
        GenText(v, "my", KNum(Player.instance != null ? Player.instance.TotalDamagePower : 0));
        GenText(v, "cnt", d.getTotalEnemy() + "마리");
        GenText(v, "hp", d.playerhealth.ToString());
        GenText(v, "rw0", GameManager.MoneyString(d.PriceGold));
        GenText(v, "rw1", "×" + d.PriceStoneAmount);
        GenText(v, "rw2", "×" + GameManager.MoneyString(d.PriceEXP));
        GenText(v, "tk", $"1장 사용 ({dm.DungeonTicket}/3)");
    }

    void DungeonEnter()
    {
        var dm = DungeonManager.instance; var list = DnList();
        if(dm == null || dnSel >= list.Count) return;
        if(dm.DungeonTicket <= 0) { ShowMessage("입장권이 없어요", "내일 다시 3장이 생겨요"); return; }
        var d = list[dnSel];
        if(dnPop != null) { GenClose(dnPop); dnPop = null; }
        dm.EnterDungeon(d);          // 예전 입장 버튼과 같은 순서: 입장 → 던전 창 닫기
        if(dnPage != null) dnPage.SetActive(false);
    }

    void DungeonSweep()
    {
        var dm = DungeonManager.instance; var list = DnList();
        if(dm == null || dnSel >= list.Count) return;
        var d = list[dnSel];
        if(PlayerPrefs.GetInt("dungeon_clear_" + d.DungeonName, 0) == 0) { ShowMessage("소탕", "한 번 깬 던전만 소탕할 수 있어요"); return; }
        if(dm.DungeonTicket <= 0) { ShowMessage("입장권이 없어요", "내일 다시 3장이 생겨요"); return; }
        dm.DungeonTicket--;
        dm.setDungeonTicketText();
        GameManager.SetMoney(d.PriceGold);
        GameManager.Enemy_Dead_Player_EXP_UP(d.PriceEXP);
        if(UpgradeStoneManager.instance != null) UpgradeStoneManager.instance.addStone(d.DungeonPriceStone, d.PriceStoneAmount);
        ShowMessage("소탕 완료", $"골드 {GameManager.MoneyString(d.PriceGold)} · 강화석 ×{d.PriceStoneAmount} · 경험치 {GameManager.MoneyString(d.PriceEXP)}");
        RefreshDungeon(true);
    }

    // 리본 제목 바꾸기: 글이 길어지면 리본(9-slice mg_title, 미리보기 .ribbon 과 같은 CSS)을 글 폭 + 양옆 30px 로
    void GenRibbon(GenView v, string key, string s)
    {
        if(v == null || !v.texts.TryGetValue(key, out var tl) || tl.Count == 0) return;
        var t = tl[0];
        t.text = s;
        t.alignment = TextAlignmentOptions.Midline;
        float tw = t.GetPreferredValues(s, 99999f, 99999f).x;
        var trt = t.rectTransform;
        trt.sizeDelta = new Vector2(Mathf.Max(trt.sizeDelta.x, tw + 8f), trt.sizeDelta.y);
        if(!v.objs.TryGetValue(key, out var ol)) return;
        var box = ol.FirstOrDefault(o => o != null && o.name == "Box");
        if(box == null) return;
        var img = box.GetComponent<Image>();
        var rt = (RectTransform)box.transform;
        genPad.TryGetValue(box, out float pad);
        float cx = rt.anchoredPosition.x + rt.sizeDelta.x / 2f;
        float h = rt.sizeDelta.y - pad * 2f * K;
        float top = rt.anchoredPosition.y - pad * K;
        float w = tw + 60f * K;
        img.sprite = MapSprite("mg_title"); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(cx - w / 2f, top);
        genPad[box] = 0f;
    }
}
