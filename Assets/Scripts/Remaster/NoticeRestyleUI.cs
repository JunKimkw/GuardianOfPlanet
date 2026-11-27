using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 게임 전체 '안내' 창 B안 (2026-11-28 사용자 "B 좋다" + "광고 보기랑 다이아 밑에 여백을 좀 줄여 줘" = previews/notice.png, tools/ui_preview/notice_preview.js 의 B)
// - 원래 Popup04_TopBar (판 Popup01_Basic_White_Bg + 파란 지붕 + 하늘색 버튼) 가 켜지는 순간 한 번 바꿔 입혀요 (게임 곳곳 30여 곳, 제목 '안내'·'경고')
//   판 = 파란 둥근 판(nt_win) · 지붕은 숨기고 제목은 위 끝 보라 리본(mg_title) · 글 = 밝은 글씨 + 어두운 칸(nt_box) · 버튼 = 광고(동영상 그림)는 보라, 나머지는 노랑(nt_btn_*)
//   판 아래 끝은 가장 아래 버튼 + notice_bottom_pad(16px) 로 줄여요 (버튼 밑 여백)
// - 던전 입장권 추가 창은 글을 짧게 + 입장권 그림 + "오늘 N / 3 번 받았어요"
// - notice_restyle 0 이면 원래 모양
public partial class RemasterUI
{
    readonly List<RectTransform> ntAll = new List<RectTransform>();
    readonly HashSet<RectTransform> ntDone = new HashSet<RectTransform>();
    bool ntScanned;
    TMP_Text ntDungeonHint;

    void UpdateNoticeRestyle()
    {
        if(Tuning.Get("notice_restyle", 1f) <= 0f) return;
        if(!ntScanned) {
            ntScanned = true;
            foreach(var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if(t != null && t.name == "Popup04_TopBar") ntAll.Add(t);
        }
        for(int i = 0; i < ntAll.Count; i++) {
            var p = ntAll[i];
            if(p == null || !p.gameObject.activeInHierarchy || ntDone.Contains(p)) continue;
            ntDone.Add(p);
            NtRestyle(p);
        }
        // 던전 입장권: 오늘 받은 수 (창이 떠 있을 때)
        if(ntDungeonHint != null && ntDungeonHint.gameObject.activeInHierarchy && DungeonManager.instance != null)
            ntDungeonHint.text = $"오늘 {DungeonManager.instance.ExtraTicketsToday} / 3 번 받았어요";
    }

    static readonly Vector3[] corners0 = new Vector3[4];
    static string NtTitle(RectTransform p)
    {
        var t = p.Find("Top/Text_Title");
        var tm = t != null ? t.GetComponent<TMP_Text>() : null;
        return tm != null ? tm.text.Trim() : "";
    }

    void NtRestyle(RectTransform p)
    {
        var bgT = p.Find("Bg") as RectTransform;
        var bg = bgT != null ? bgT.GetComponent<Image>() : null;
        string title = NtTitle(p);
        // 판이 기본 판이고 제목이 안내·경고인 창만 (약관 창처럼 모양이 다른 창은 그대로)
        if(bg == null || bg.sprite == null || bg.sprite.name != "Popup01_Basic_White_Bg") return;
        if(title != "안내" && title != "경고") return;
        MapFont();
        bool dungeonTicket = p.parent != null && p.parent.name == "AddDungeonTicket" && p.parent.parent != null && p.parent.parent.name == "DungeonTab"
                             && p.Find("Ads") != null && p.Find("Diamond") != null;

        // 판
        bg.sprite = MapSprite("nt_win"); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f; bg.color = Color.white;
        // 지붕 그림은 숨기고 제목만 리본으로
        var top = p.Find("Top");
        if(top != null) foreach(var im in top.GetComponentsInChildren<Image>(true)) im.enabled = false;
        var titleT = top != null ? top.Find("Text_Title") as RectTransform : null;
        var titleTm = titleT != null ? titleT.GetComponent<TMP_Text>() : null;
        if(dungeonTicket && titleTm != null) titleTm.text = "입장권 추가";
        if(titleTm != null) {
            // 리본: 판 위 끝 가운데 (판 위로 반쯤 걸쳐요). 폭 = 글 + 양옆 26px, 높이 36px
            // 제목 글씨 크기 (2026-11-28 사용자: '입장권 추가' 글씨 좀 줄여 줘) notice_title_scale(0.78)배 — 먼저 글꼴을 입히고 그 폭으로 리본을 맞춰요
            float titlePx = titleTm.fontSize / K;
            StyleBtnText(titleTm, titlePx * Mathf.Clamp(Tuning.Get("notice_title_scale", 0.78f), 0.4f, 1.2f), Hex("#10142a"));
            float tw = titleTm.GetPreferredValues(titleTm.text).x;
            var rib = new GameObject("Ribbon (remaster)", typeof(RectTransform)).GetComponent<RectTransform>();
            rib.SetParent(bgT, false);
            rib.anchorMin = rib.anchorMax = new Vector2(0.5f, 1f); rib.pivot = new Vector2(0.5f, 0.5f);
            float scale = Mathf.Max(0.5f, titlePx / 19f);   // 리본 높이는 원래 제목 크기 기준 그대로
            rib.sizeDelta = new Vector2(tw + 52f * K * scale, 36f * K * scale);
            rib.anchoredPosition = new Vector2(0f, -8f * K);
            var ri = rib.gameObject.AddComponent<Image>(); ri.sprite = MapSprite("mg_title"); ri.type = Image.Type.Sliced; ri.pixelsPerUnitMultiplier = 1f / scale; ri.raycastTarget = false;
            titleT.SetParent(rib, true);
            titleT.anchorMin = titleT.anchorMax = new Vector2(0.5f, 0.5f); titleT.pivot = new Vector2(0.5f, 0.5f);
            titleT.anchoredPosition = Vector2.zero;
            titleTm.alignment = TextAlignmentOptions.Center;
        }
        // 글: 밝은 글씨 + 어두운 칸
        foreach(Transform c in p) {
            var tm = c.GetComponent<TMP_Text>();
            if(tm == null) continue;
            if(dungeonTicket) tm.text = "광고를 보거나 다이아로\n던전 입장권 1장을 받아요";
            tm.color = Hex("#e6efff");
            GameFont(tm, false);
            var crt = (RectTransform)c;
            // 칸 = 글 가운데에 글 크기 + 여백 (입장권 창은 왼쪽에 입장권 그림 자리)
            var box = new GameObject("TextBox (remaster)", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(p, false);
            box.SetSiblingIndex(c.GetSiblingIndex());
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            var pref = tm.GetPreferredValues(tm.text, crt.rect.width, 9999f);
            float tw = Mathf.Min(crt.rect.width, pref.x), th = pref.y;
            float padX = 14f * K, padY = 10f * K, iconW = dungeonTicket ? (42f + 12f) * K : 0f;
            Vector3 mid = p.InverseTransformPoint(crt.TransformPoint(crt.rect.center));
            box.localPosition = new Vector3(mid.x, mid.y, 0f);
            box.sizeDelta = new Vector2(tw + padX * 2f + iconW, th + padY * 2f);
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = MapSprite("nt_box"); bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 1f; bi.raycastTarget = false;
            if(dungeonTicket) {
                var ic = new GameObject("Ticket", typeof(RectTransform)).GetComponent<RectTransform>();
                ic.SetParent(box, false);
                ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f); ic.pivot = new Vector2(0f, 0.5f);
                ic.sizeDelta = new Vector2(42f * K, 42f * K); ic.anchoredPosition = new Vector2(12f * K, 0f);
                var ii = ic.gameObject.AddComponent<Image>(); ii.sprite = Skin("ItemIcon_Ticket_Gold"); ii.preserveAspect = true; ii.raycastTarget = false;
                crt.anchoredPosition += new Vector2(iconW / 2f, 0f);   // 글은 그림 옆으로
                // 오늘 받은 수 (칸 아래)
                var hint = PxText(p, $"오늘 {(DungeonManager.instance != null ? DungeonManager.instance.ExtraTicketsToday : 0)} / 3 번 받았어요", 0f, 0f, 11f, Hex("#cfe4ff"));   // 처음부터 글이 있어야 버튼 자리를 계산해요
                GameFont(hint, false);
                var hrt = hint.rectTransform;
                hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f); hrt.pivot = new Vector2(0.5f, 1f);
                hrt.localPosition = box.localPosition - new Vector3(0f, box.sizeDelta.y / 2f + 8f * K, 0f);
                // 그림 있는 칸(입장권 그림 + 글) 과 아래 줄을 살짝 위로 (2026-11-28 사용자) notice_ticket_lift(6px)
                float lift = Tuning.Get("notice_ticket_lift", 6f) * K;
                box.localPosition += new Vector3(0f, lift, 0f); crt.anchoredPosition += new Vector2(0f, lift); hrt.localPosition += new Vector3(0f, lift, 0f);
                hint.transform.SetSiblingIndex(crt.GetSiblingIndex() + 1);   // 안쪽 팝업(다이아·광고 확인)보다 뒤에 그려지게 (2026-11-28: 위에 비쳐 보였어요)
                ntDungeonHint = hint;
            }
            break;   // 글 칸은 첫 글 하나만 (두 줄 창은 위 글만 칸, 아래 글은 밝게만)
        }
        foreach(Transform c in p) { var tm = c.GetComponent<TMP_Text>(); if(tm != null && tm.color != Hex("#e6efff")) { tm.color = Hex("#e6efff"); GameFont(tm, false); } }
        // 버튼: 광고(동영상 그림) = 보라, 나머지 = 노랑. 글은 흰 글씨 + 진한 테두리
        var btns = new List<RectTransform>();
        foreach(Transform c in p) {
            if(c.name == "Button_Close" || c == bgT || c == top) continue;
            var btn = c.GetComponent<Button>(); var im = c.GetComponent<Image>();
            if(btn == null || im == null) continue;
            bool ad = c.GetComponentsInChildren<Image>(true).Any(x => x.sprite != null && x.sprite.name.Contains("Video")) || c.name == "Ads";
            im.sprite = MapSprite(ad ? "nt_btn_p" : "nt_btn_y"); im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 1f; im.color = Color.white;
            // 그림에 그림자 여백 4px 이 들어 있어서 버튼이 원래보다 작아 보였어요 (2026-11-28 사용자: 원래대로 키워 줘) → 그만큼 양옆·위아래로 넓혀요 (가운데는 그대로)
            ((RectTransform)c).sizeDelta += new Vector2(4f * K * 2f, 4f * K * 2f);
            foreach(var tm in c.GetComponentsInChildren<TMP_Text>(true)) StyleBtnText(tm, tm.fontSize / K, Hex("#0b1030"));
            btns.Add((RectTransform)c);
        }
        // 글(칸·안내 줄·두 번째 글)이 버튼과 겹치면 버튼을 그 아래로 내려요 (2026-11-28 사용자: 안내 창 문구 위치가 이상해요 — 글 칸이 버튼에 걸렸어요)
        var cs = new Vector3[4];
        float contentBottom = float.MaxValue, btnTop = float.MinValue;
        foreach(Transform c in p) {
            if(c == bgT || c == top || c.name == "Button_Close" || btns.Contains((RectTransform)c) || !c.gameObject.activeSelf) continue;
            var ctm = c.GetComponent<TMP_Text>();
            if(ctm != null) {   // 글은 글 상자가 아니라 실제 글자 아래 끝 (상자가 버튼까지 내려와 있는 창이 있어요)
                if(string.IsNullOrEmpty(ctm.text)) continue;
                ctm.ForceMeshUpdate();
                var bnd = ctm.textBounds;
                contentBottom = Mathf.Min(contentBottom, p.InverseTransformPoint(c.TransformPoint(new Vector3(bnd.center.x, bnd.min.y, 0f))).y);
            } else if(c.name == "TextBox (remaster)") {
                ((RectTransform)c).GetWorldCorners(cs);
                foreach(var v in cs) contentBottom = Mathf.Min(contentBottom, p.InverseTransformPoint(v).y);
            }
        }
        foreach(var b in btns) { b.GetWorldCorners(cs); foreach(var v in cs) btnTop = Mathf.Max(btnTop, p.InverseTransformPoint(v).y); }
        float gap = Tuning.Get("notice_text_gap", 10f) * K;
        if(btns.Count > 0 && contentBottom < float.MaxValue && btnTop > contentBottom - gap) {
            float drop = btnTop - (contentBottom - gap);
            foreach(var b in btns) b.anchoredPosition -= new Vector2(0f, drop);
        }
        // 판 아래 끝 = 가장 아래 버튼 + notice_bottom_pad (버튼 밑 여백). 버튼을 내렸으면 판도 늘어나요
        float lowest = float.MaxValue;
        foreach(var b in btns) { b.GetWorldCorners(cs); foreach(var v in cs) lowest = Mathf.Min(lowest, bgT.InverseTransformPoint(v).y); }
        if(lowest < float.MaxValue) {
            float bottomNow = -bgT.rect.height * bgT.pivot.y;   // 판 아래 끝 (판 안 좌표)
            float want = lowest - Tuning.Get("notice_bottom_pad", 16f) * K - 7f * K;   // 그림 그림자 여백 7px 포함
            float cut = want - bottomNow;
            bgT.sizeDelta -= new Vector2(0f, cut);   // 위 끝은 제자리, 아래만
            bgT.anchoredPosition += new Vector2(0f, cut * bgT.pivot.y);
        }
    }

}
