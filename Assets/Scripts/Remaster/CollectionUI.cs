using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 도감 창 A (2026-11-26 사용자 "도감은 A", 미리보기 그대로 = UIGen/collection)
// - 예전 도감 페이지(Canvas/Tabs Above Buttons/Collection)가 열리면 그 위를 덮어요
// - 카드 = 미리보기 카드(일반 c0 · 보스 c8)를 틀로 몬스터 수만큼 (스크롤). 못 만난 몬스터 = 그림자 + '???'
// - '받기' = 아직 안 받은 도감 다이아 모두 (예전 표 20·40·75·100·150·300). 예전 보상 알림(Reward Tab)은 가려지니 메시지 창으로 대신 보여 줘요
//   (2026-11-27) 예전의 '0.5초마다 하나씩 저절로 받기'는 꺼요 (CollectionManager, collection_auto_claim 0) → 카드의 다이아 표시 + '받기'로 한 번에
public partial class RemasterUI
{
    GenView clView;
    GameObject clPage;
    int clTab;   // 0 전체 · 1 일반 · 2 보스
    List<GenRow> clRows;
    List<CollectionData> clList;
    string clKey;
    static readonly int[] ClDia = { 20, 40, 75, 100, 150, 300 };

    void UpdateCollectionPage()
    {
        var cm = CollectionManager.Instance;
        if(clPage == null) { clPage = GenFindPage("Collection", "Tabs Above Buttons"); if(clPage == null) return; }
        if(!clPage.activeInHierarchy) { if(clView != null) { GenClose(clView); clView = null; } return; }
        // 예전 보상 알림 → 새 메시지 창
        if(cm != null && cm.DiamondReward_Tab != null && cm.DiamondReward_Tab.activeSelf) {
            string amt = cm.DiamondReward_Text != null ? cm.DiamondReward_Text.text : "";
            cm.DiamondReward_Tab.SetActive(false);
            ShowMessage("도감 보상", $"다이아 {amt} 받았어요");
        }
        if(clView == null || clView.layer == null) BuildCollection(true);
        else RefreshCollection(false);
    }

    void BuildCollection(bool pop)
    {
        var cm = CollectionManager.Instance;
        clKey = null;
        clView = GenBuild("collection", "CollectionA", pop, (RectTransform)clPage.transform);
        if(clView == null || cm == null) return;
        var v = clView;
        GenHit(v, "close", () => GenPageBack(clPage));
        for(int k = 0; k < 3; k++) { int kk = k; GenHit(v, "tab" + k + ".off", () => { if(clTab == kk) return; clTab = kk; BuildCollection(false); }); }
        GenHit(v, "claim", ClaimCollection);
        for(int k = 0; k < 3; k++) { GenShow(v, "tab" + k + ".on", clTab == k); GenShow(v, "tab" + k + ".off", clTab != k); }
        // 카드 목록
        clList = cm.CollectionDatas.Where(d => d != null && (clTab == 0 || (clTab == 1 ? d._typeofEnemy == CollectionData.typeofEnemy.normal : d._typeofEnemy == CollectionData.typeofEnemy.boss))).ToList();
        var r0 = v.rects["c0"]; var r1 = v.rects["c1"]; var r4 = v.rects["c4"];
        clRows = GenRepeat(v, new[] { "c0", "c8" }, i => clList[i]._typeofEnemy == CollectionData.typeofEnemy.boss ? 1 : 0, clList.Count, 4,
            r1.x - r0.x, r4.y - r0.y, new UnityEngine.Rect(r0.x - 6f, r0.y, 340f, GenBottomPx(v) - 14f - r0.y), "c");
        RefreshCollection(true);
    }

    void RefreshCollection(bool force)
    {
        var v = clView; var cm = CollectionManager.Instance;
        if(v == null || v.layer == null || cm == null || clRows == null) return;
        int found = cm.CollectionDatas.Count(d => d != null && d.grade != CollectionData.Grade.Unknown);
        int total = cm.CollectionDatas.Count(d => d != null);
        int pending = cm.CollectionDatas.Where(d => d != null && d.EarnDiamond != null).Sum(d => d.EarnDiamond.Count(x => x == 1));
        string key = $"{found}_{pending}_{clTab}_{cm.CollectionDatas.Sum(d => d != null ? (int)d.grade : 0)}";
        if(!force && key == clKey) return;
        clKey = key;
        GenText(v, "cnt", $"모은 몬스터 {found} / {total}");
        GenFill(v, "bar", total > 0 ? found / (float)total : 0f);
        GenAlpha(v, "claim", pending > 0 ? 1f : 0.6f);
        for(int i = 0; i < clRows.Count && i < clList.Count; i++) {
            var r = clRows[i]; var d = clList[i];
            bool f = d.grade != CollectionData.Grade.Unknown;
            var im = r.Img(".img");
            if(im != null) { im.sprite = d.Collection_MonsterImage != null ? d.Collection_MonsterImage.sprite : im.sprite; im.color = f ? Color.white : new Color(0f, 0f, 0f, 0.6f); }
            r.Show(".lbl", f); r.Show(".unk", !f);
            if(f) ClLabel(r, d);
            r.Show(".gem", d.EarnDiamond != null && d.EarnDiamond.Any(x => x == 1));
        }
    }

    // 카드 이름 = 나오는 곳 (예전 도감 칸의 글). 카드 폭보다 길면 글자를 줄여요
    void ClLabel(GenRow r, CollectionData d)
    {
        if(!r.texts.TryGetValue(".lbl", out var t) || t == null) return;
        string s = d.Collection_Stage != null ? d.Collection_Stage.text : "";
        if(string.IsNullOrEmpty(s)) s = d._typeofEnemy == CollectionData.typeofEnemy.boss ? "보스" : "몬스터";
        if(t.text == s) return;
        t.text = s;
        float max = 70f * K, pw = t.GetPreferredValues(s, 99999f, 99999f).x;
        if(pw > max) t.fontSize *= max / pw;
        var rt = t.rectTransform; rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, Mathf.Min(pw, max) + 8f), rt.sizeDelta.y);
    }

    void ClaimCollection()
    {
        var cm = CollectionManager.Instance;
        if(cm == null) return;
        int sum = 0;
        foreach(var d in cm.CollectionDatas) {
            if(d == null || d.EarnDiamond == null) continue;
            for(int j = 0; j < d.EarnDiamond.Length; j++) if(d.EarnDiamond[j] == 1) { sum += ClDia[Mathf.Min(j, ClDia.Length - 1)]; d.EarnDiamond[j] = 2; }
        }
        if(sum > 0) { GameManager.SetDiamond(sum); ShowMessage("도감 보상", $"다이아 {sum}개 받았어요"); }
        else ShowMessage("도감 보상", "받을 보상이 없어요\n몬스터를 잡아 등급을 올리면 다이아를 받아요");
        RefreshCollection(true);
    }

    // 예전 페이지의 뒤로/닫기 버튼을 눌러요 (없으면 페이지만 꺼요)
    //  (2026-11-27 점검) 페이지에 가장 가까운 버튼을 골라요: 버프 페이지는 안쪽 구매 창의 닫기가 먼저 잡혔어요
    //  우리 창의 버튼(Hit_…)은 빼요 — 잡히면 자기 자신을 다시 불러 멈추지 않아요
    void GenPageBack(GameObject page)
    {
        if(page == null) return;
        Button best = null; int bestDepth = int.MaxValue;
        foreach(var b in page.GetComponentsInChildren<Button>(true)) {
            if(b == null || b.name.StartsWith("Hit_")) continue;
            string n = b.name.ToLowerInvariant();
            if(!(n.Contains("back") || n.Contains("close") || n.Contains("exit"))) continue;
            int depth = 0;
            for(var t = b.transform; t != null && t != page.transform; t = t.parent) depth++;
            if(depth < bestDepth) { best = b; bestDepth = depth; }
        }
        if(best != null) best.onClick.Invoke();
        if(page.activeSelf) page.SetActive(false);
    }
}
