using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 게임 전체에서 쓰는 메시지 창 (2026-11-26 사용자: "에러 메시지 창을 따로 만들자, 게임 전체에 다 쓸 수 있는 걸로")
// 모양 = 미리보기 previews/msg.png 의 A안(위에 아이콘 배지) + B안의 모은 정도 막대 (사용자: "A 의 UI 에 B 의 바")
// - ShowMessage("제목", "내용")                                        : 글만
// - ShowMessage("광석이 모자라요", "…", icon: "OreGem", have: 30, need: 1250, hint: "광석은 광산에서 캐요") : 배지 + 막대 + 작은 안내
// - 리마스터 UI 맨 위 레이어(Modals, 정렬 20)라 원래 UI·다른 창 위에서도 화면 전체가 어두워져요. 바깥·확인을 누르면 닫혀요
public partial class RemasterUI
{
    public void ShowMessage(string title, string body, string button = "확인", Action onOk = null,
                            string icon = null, long have = -1, long need = -1, string hint = null)
    {
        if(!EnsureBuilt()) return;
        MapFont();
        var layer = Stretch("Message", modalRoot);
        layer.SetAsLastSibling();
        var dim = Stretch("Dim", layer);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        Action close = () => { if(layer != null) Destroy(layer.gameObject); onOk?.Invoke(); };
        dim.gameObject.AddComponent<Button>().onClick.AddListener(() => close());

        // 미리보기 360 × 780 틀을 화면 가운데에. 판 = 왼쪽 40 · 폭 280, 높이는 들어가는 것에 맞춰요
        var box = Rect("Box", layer, new Vector2(1080, 780 * K), Vector2.zero);
        bool badge = !string.IsNullOrEmpty(icon), bar = need > 0 && have >= 0, small = !string.IsNullOrEmpty(hint);
        int lines = string.IsNullOrEmpty(body) ? 0 : Mathf.Clamp(body.Split('\n').Length + Mathf.FloorToInt(MeasureText(body, 12.5f) / 250f), 1, 6);
        // 세로 칸: 제목 38~62 · 본문 줄마다 20 · 막대 30 · 안내 22 · 버튼 44 + 아래 24
        float cy = 38f;
        float titleY = cy + 12f; cy += 26f;
        float bodyY = cy + lines * 10f; cy += lines * 20f + (lines > 0 ? 6f : 0f);
        float barY = cy; if(bar) cy += 34f;
        float hintY = cy + 8f; if(small) cy += 22f;
        float okY = cy + 8f; cy += 8f + 44f + 20f;
        float h = cy, y = 390f - h * 0.5f, x = 40f;

        Plate(box, "wjA_sheet", x, y, 280f, h, 7f).raycastTarget = true;   // 판을 눌러도 닫히지 않게
        if(badge) {   // 위에 아이콘 배지 (판 위로 34 나와요)
            Plate(box, "msg_badge", 148f, y - 34f, 64f, 64f, 18f);
            PxIcon(box, icon, 180f, y - 2f, 44f);
        }
        BtnText(box, title, 180f, y + titleY, 19f);
        if(lines > 0) {
            var t = PxText(box, body, 180f, y + bodyY, 12.5f, Hex("#e6efff"), 0f, null, TextAlignmentOptions.Center, 250f);
            GameFont(t, false);   // 원래 게임 한글 글꼴
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(250f * K, lines * 20f * K);
        }
        if(bar) {   // 모은 정도 막대 (왼쪽 24 · 폭 232 · 높이 26): 채움 + 가운데 "아이콘 30 / 1,250"
            float bx = x + 24f;
            Plate(box, "msg_bar", bx, y + barY, 232f, 26f, 0f);
            float ratio = Mathf.Clamp01(need > 0 ? (float)((double)have / need) : 0f);
            if(ratio > 0f) Plate(box, "msg_bar_fill", bx + 2f, y + barY + 2f, Mathf.Max(22f, 228f * ratio), 22f, 0f);
            string label = $"{GameManager.MoneyString(have)} / {GameManager.MoneyString(need)}";
            float iw = badge ? 16f + 4f : 0f;
            var lt = BtnText(box, label, 180f, y + barY + 13f, 12f);
            float tw = lt.GetPreferredValues(label).x / K;   // 실제로 쓴 글꼴의 폭
            float left = 180f - (iw + tw) * 0.5f;
            lt.rectTransform.anchoredPosition = new Vector2((left + iw + tw * 0.5f) * K, lt.rectTransform.anchoredPosition.y);
            if(badge) PxIcon(box, icon, left + 8f, y + barY + 13f, 16f);
        }
        if(small) GameFont(PxText(box, hint, 180f, y + hintY, 11f, Hex("#cfe4ff")), false);
        Plate(box, "msg_btn", 110f, y + okY, 140f, 44f, 5f);
        BtnText(box, button, 180f, y + okY + 22f, 17f, edge: OnYellow);
        var hit = PxBox("Ok", box, 110f, y + okY, 140f, 44f);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        hit.gameObject.AddComponent<Button>().onClick.AddListener(() => close());
        StartCoroutine(PopIn(box, 1f));
    }
}
