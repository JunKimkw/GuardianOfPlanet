using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 모든 창이 아래에서 올라와요 (2026-11-28 사용자: "지금부터 모든 창은 뽑기처럼 아래에서 올라오는 애니메이션 넣어 줘")
// - 새 창(합성·분해·자동 분해·세트·던전 입장·패시브 설명·OpenModal 창들): PopIn 대신 SlideIn
// - 장면의 원래 창(장비·도감·버프·랭킹·설정·상점·업적·출석·일일 뽑기·패키지·타워·정보·던전): 켜지는 순간을 보고 아래에서 올려요
// - 작은 메시지 창·토스트·결과 카드는 그대로 (창이 아니에요). 시간은 page_slide_time (기본 0.25초), page_slide 0 이면 꺼요
public partial class RemasterUI
{
    // 아래에서 올라오기: 지금 자리에서 부모 높이만큼 아래 → 제자리 (끝이 부드럽게 멈춰요). fit = 크기 (PopIn 과 같은 뜻)
    IEnumerator SlideIn(RectTransform rt, float fit = 1f)
    {
        if(rt == null) yield break;
        rt.localScale = new Vector3(fit, fit, 1f);
        if(Tuning.Get("page_slide", 1f) <= 0f) yield break;
        var par = rt.parent as RectTransform;
        float h = par != null && par.rect.height > 0f ? par.rect.height : 2048f;
        Vector2 home = rt.anchoredPosition, from = home - new Vector2(0f, h);
        float dur = Mathf.Max(0.05f, Tuning.Get("page_slide_time", 0.25f)), t = 0f;
        rt.anchoredPosition = from;
        while(rt != null && t < dur) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur), e = 1f - Mathf.Pow(1f - k, 3f);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, home, e);
            yield return null;
        }
        if(rt != null) rt.anchoredPosition = home;
    }

    // ---- 원래 창들: 켜지는 순간 아래에서
    static readonly (string name, string parent)[] SlidePages = {
        ("Equipment", "Tabs Above Buttons"), ("Achievement", "Tabs Above Buttons"), ("Collection", "Tabs Above Buttons"),
        ("Attendance Tab", "Tabs Above Buttons"), ("Daily Gacha", "Tabs Above Buttons"), ("Settings", "Tabs Above Buttons"),
        ("Tower", "Tabs Above Buttons"), ("Shop", "Tabs Above Buttons"), ("Package", "Tabs Above Buttons"),
        ("Buffs", "Tabs Above Buttons"), ("LeaderBoards", "Tabs Above Buttons"), ("Upgrade Ship", "Tabs"),   // 정보 창(InfoSheetUI)·던전(DungeonSheetUI)은 새 판이 스스로 올라와요
    };
    readonly Dictionary<string, GameObject> slidePage = new Dictionary<string, GameObject>();
    readonly Dictionary<GameObject, bool> slideWasOn = new Dictionary<GameObject, bool>();
    readonly Dictionary<GameObject, Vector2> slideHome = new Dictionary<GameObject, Vector2>();
    readonly Dictionary<GameObject, Coroutine> slideRun = new Dictionary<GameObject, Coroutine>();

    // 강화 창을 장비 창과 같은 색으로 (2026-11-28 사용자: "강화 창도 장비랑 똑같은 창으로") — 판 남보라(69,78,156) → 장비 창 파랑 #123a8c, 지붕 → 조금 밝은 파랑
    static void RecolorUpgrade(GameObject page)
    {
        if(Tuning.Get("upgrade_recolor", 1f) <= 0f) return;
        var bg = page.transform.Find("Upgrade"); var top = page.transform.Find("Upgrade/Top");
        var bi = bg != null ? bg.GetComponent<UnityEngine.UI.Image>() : null; var ti = top != null ? top.GetComponent<UnityEngine.UI.Image>() : null;
        if(bi != null) bi.color = Hex("#123a8c");
        if(ti != null) ti.color = Hex("#1c4bb0");
        // 아래 메뉴와 붙어 있지 않게: 판 위 끝은 그대로, 아래 끝을 아래 메뉴(180) 위 24 로 (장비·정보·뽑기 창처럼 자기 아래 테두리가 보여요)
        var rt = bg as RectTransform;
        // 판을 줄이지 않고 통째로 올려요 (아래쪽에 붙은 무기 목록이 +1·+10·+100 을 덮지 않게, 2026-11-28 사용자) + 위로 upgrade_extra_height 만큼 더 크게
        if(rt != null) {
            float lift = 180f + Tuning.Get("upgrade_bottom_gap", 24f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y + lift);
            rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y + lift + Tuning.Get("upgrade_extra_height", -120f));
        }
        // 무기 목록 칸: 원래는 판 가운데 고정 높이라 창이 커지면 아래가 비었어요 → 판에 맞춰 늘려요 (위 = +1·+10·+100 아래, 아래 = 판 아래 테두리 위)
        var sr = page.transform.Find("Upgrade/ScrollRect") as RectTransform;
        if(sr != null) {
            sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = new Vector2(0f, Tuning.Get("upgrade_list_bottom", 44f));
            sr.offsetMax = new Vector2(0f, -Tuning.Get("upgrade_list_top", 262f));
            // 스크롤 기준 칸: 장면에선 판 전체(Upgrade)로 잡혀 있어 맨 위로 올리면 목록 위쪽이 +1·+10·+100 뒤로 숨었어요
            //  (원래는 위 여백 278 로 가렸어요) → 목록 칸(viewport)으로 (2026-11-28 사용자: 맨 위까지 스크롤이 안 돼요)
            var scroll = sr.GetComponent<ScrollRect>();
            var vp = sr.Find("viewport") as RectTransform;
            if(scroll != null) {
                scroll.viewport = vp != null ? vp : sr;
                // ScrollRect 는 기준 칸을 m_ViewRect 에 한 번 기억해 두고 viewport 를 바꿔도 안 지워요 → 같이 비워서 새 칸을 다시 읽게
                var f = typeof(ScrollRect).GetField("m_ViewRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if(f != null) f.SetValue(scroll, null);
                scroll.StopMovement();
                var ct = scroll.content;
                if(ct != null) ct.anchoredPosition = new Vector2(ct.anchoredPosition.x, 0f);   // 맨 위에서 시작
            }
        }
        // 목록 끝 여백: 원래 아래 210 (아래 메뉴에 가려지던 때 몫) → 30, 위 → 20 (2026-11-28 사용자: 끝까지 내리면 여백이 너무 커요)
        var content = page.transform.Find("Upgrade/ScrollRect/viewport/Contents");
        var vl = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
        if(vl != null) {
            vl.padding.top = Mathf.RoundToInt(Tuning.Get("upgrade_list_pad_top", 20f));
            vl.padding.bottom = Mathf.RoundToInt(Tuning.Get("upgrade_list_pad_bottom", 50f));
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)content);
        }
    }

    void UpdatePageSlides()
    {
        if(Tuning.Get("page_slide", 1f) <= 0f) return;
        foreach(var (name, parent) in SlidePages) {
            string key = parent + "/" + name;
            if(!slidePage.TryGetValue(key, out var go) || go == null) {
                go = GenFindPage(name, parent);
                if(go == null) continue;
                slidePage[key] = go;
                if(name == "Upgrade Ship") RecolorUpgrade(go);
                slideWasOn[go] = go.activeInHierarchy;   // 처음 찾았을 때 이미 켜져 있으면 올리지 않아요
                slideHome[go] = ((RectTransform)go.transform).anchoredPosition;
                continue;
            }
            bool on = go.activeInHierarchy;
            slideWasOn.TryGetValue(go, out bool was);
            if(on && !was) {
                var rt = (RectTransform)go.transform;
                if(slideRun.TryGetValue(go, out var c) && c != null) StopCoroutine(c);
                rt.anchoredPosition = slideHome[go];   // 지난번에 중간에 닫혔어도 제자리에서 시작
                slideRun[go] = StartCoroutine(SlideIn(rt, rt.localScale.x));
            } else if(!on && was) {
                if(slideRun.TryGetValue(go, out var c) && c != null) StopCoroutine(c);
                ((RectTransform)go.transform).anchoredPosition = slideHome[go];
            }
            slideWasOn[go] = on;
        }
    }
}
