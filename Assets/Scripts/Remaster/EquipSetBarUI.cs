using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장비 창 맨 위 장착 칸 줄 아래 '지금 낀 세트' 띠 (2026-11-26 사용자: 세트 효과 자리 A·B·C 중 "B가 훨씬 좋다")
// - 미리보기 tools/ui_preview/set_preview.js 의 B 와 같은 모양. 판·점 그림은 render_sprites.js 의 es_* (크롬으로 찍음)
// - 장착 칸 판(Group_Slots)을 아래로 EsGrow 만큼 늘리고, 그 아래 장비 정보·버튼·가방은 그만큼 내려요
// - 가장 많이 낀 세트 하나를 보여 줘요: 이름 · 점(낀 개수/세트 크기) · 지금 효과(노랑) · 다음 단계 (오른쪽)
public partial class RemasterUI
{
    const float EsGrow = 107f;   // 늘리는 높이 (게임 단위) = 띠 위 여백 7 + 띠 75 + 아래 여백 25
    const float EsW = 324f, EsH = 25f, EsPad = 6.9f, EsGap = 5.75f;   // 띠 크기·안쪽 여백·사이 (미리보기 px, 1px = 3단위)
    RectTransform esMiddle, esBar, esContent;
    string esKey;

    void UpdateEquipSetBar()
    {
        if(esMiddle == null && !SetupEquipSetBar()) return;
        if(!esMiddle.gameObject.activeInHierarchy) return;
        string key;
        if(EquipSets.Best(out var st, out int now)) key = $"{st}_{now}_{Tuning.Get("equip_set_scale", 1f):0.###}";
        else key = "none";
        if(key == esKey && esContent != null) return;
        esKey = key;
        BuildEquipSetBar();
    }

    // 장비 창 배치 바꾸기 (한 번)
    bool SetupEquipSetBar()
    {
        var em = EquipManager.Instance;
        if(em == null || em.EquipNameText == null) return false;
        var plate = em.EquipNameText.transform.parent;
        var info = plate != null ? plate.parent as RectTransform : null;
        var mid = info != null ? info.parent as RectTransform : null;
        if(mid == null || mid.name != "Middle") return false;
        var slots = mid.Find("Group_Slots") as RectTransform;
        if(slots == null) return false;
        esMiddle = mid;
        float g = EsGrow, oldH = slots.sizeDelta.y;
        // 가운데 기준인 Middle 을 아래로 늘리면 안쪽 칸들이 g/2 내려가요 → 장착 칸·프리셋은 제자리, 정보·버튼은 g 만큼 아래
        mid.sizeDelta += new Vector2(0f, g);
        mid.anchoredPosition -= new Vector2(0f, g / 2f);
        slots.sizeDelta += new Vector2(0f, g);   // 위 끝은 그대로, 아래로 g 늘어나요
        var lay = slots.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if(lay != null) lay.padding.bottom += Mathf.RoundToInt(g);   // 칸은 원래 높이 안에서 가운데
        var preset = mid.Find("Equip Set") as RectTransform;
        if(preset != null) preset.anchoredPosition += new Vector2(0f, g / 2f);
        info.anchoredPosition -= new Vector2(0f, g / 2f);
        var btns = mid.Find("Buttons") as RectTransform;
        if(btns != null) btns.anchoredPosition -= new Vector2(0f, g / 2f);
        var bag = mid.parent != null ? mid.parent.Find("Equips") as RectTransform : null;
        if(bag != null) bag.offsetMax -= new Vector2(0f, g);   // 가방 위 끝을 g 내려요
        // 띠 (장착 칸 판 안, 원래 아래 끝에서 7 아래)
        esBar = Rect("SetBar (remaster)", slots, new Vector2(EsW * K, EsH * K), Vector2.zero);
        esBar.anchorMin = esBar.anchorMax = new Vector2(0f, 1f);
        esBar.pivot = new Vector2(0f, 1f);
        esBar.anchoredPosition = new Vector2((slots.sizeDelta.x - EsW * K) / 2f, -(oldH + 7f));
        esBar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Plate(esBar, "es_bar", 0f, 0f, EsW, EsH, 0f);
        // 띠를 누르면 세트 창 (2026-11-26 사용자: 미리보기 A 의 창)
        JobHit(esBar, "Hit", 0f, 0f, EsW, EsH, () => {
            if(!EquipSets.Best(out var t, out _)) return;
            if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
            ShowSetInfo(t);
        });
        return true;
    }

    void BuildEquipSetBar()
    {
        if(esContent != null) Destroy(esContent.gameObject);
        esContent = PxBox("Content", esBar, 0f, 0f, EsW, EsH);
        float cy = EsH / 2f;
        Color dim = Hex("#c9b8ff"), yellow = Hex("#ffe14a"), edge = Hex("#10142a");
        if(!EquipSets.Best(out var st, out int now)) {
            var t = PxText(esContent, "세트 장비를 2개 이상 끼면 세트 효과가 켜져요", EsW / 2f, cy, 9f, dim);
            GameFont(t, false);
            return;
        }
        int size = EquipSets.SizeOf(st);
        float x = EsPad;
        x = EsSeg(EquipSets.SetName(st), x, cy, 10.9f, Color.white, edge, true) + EsGap;
        // 점: 미리보기 14px(+여백 1.5×2) → 8px, 한 칸 9.8px
        for(int k = 0; k < size; k++) Plate(esContent, k < now ? "es_pip_on" : "es_pip_off", x + 0.85f + k * 9.8f, cy - 4f, 8f, 8f, 0f);
        x += size * 9.8f + EsGap;
        if(now >= 2) EsSeg(EquipSets.TierText(st, Mathf.Min(now, size)), x, cy, 10.35f, yellow, edge, true);
        else EsSeg($"2개부터 {EquipSets.TierText(st, 2)}", x, cy, 9f, dim, edge, false);
        if(now >= 2 && now < size) EsSeg($"다음 {now + 1}개 {EquipSets.TierValue(st, now + 1)}", EsW - EsPad, cy, 8.05f, dim, edge, false, true);
        else if(now >= size) EsSeg("세트 완성", EsW - EsPad, cy, 8.05f, yellow, edge, false, true);
    }

    // 글자 하나 놓기 (왼쪽 끝 x, right 면 오른쪽 끝 x). 게임 글꼴로 바꾼 뒤 실제 폭으로 다시 맞춰요. 돌려주는 값 = 오른쪽 끝 x
    float EsSeg(string s, float x, float cy, float size, Color c, Color edge, bool outline, bool right = false)
    {
        var t = PxText(esContent, s, x, cy, size, c, outline ? 1f : 0f, edge, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        if(outline) GameFont(t, true, edge); else GameFont(t, false);
        t.color = c;
        float w = t.GetPreferredValues(s, 99999f, 99999f).x / K;
        var rt = t.rectTransform;
        rt.sizeDelta = new Vector2((w + 2f) * K, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2((right ? x - w / 2f : x + w / 2f) * K, rt.anchoredPosition.y);
        return x + w;
    }

    // ---- 세트 창 (set_preview.js 의 A 창, 스크린샷 px × 0.575 = 미리보기 px). 틀 360 × 780, 가운데 (180, 390)
    void ShowSetInfo(Equips.SetType st)
    {
        if(!EnsureBuilt()) return;
        MapFont();
        var old = modalRoot.Find("SetInfo");
        if(old != null) Destroy(old.gameObject);
        var layer = Stretch("SetInfo", modalRoot);
        layer.SetAsLastSibling();
        var dim = Stretch("Dim", layer);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(() => { if(layer != null) Destroy(layer.gameObject); });
        var box = Rect("Box", layer, new Vector2(1080, 780 * K), Vector2.zero);

        int size = EquipSets.SizeOf(st), now = EquipSets.EquippedCount(st);
        int rows = Mathf.Max(1, size - 1);
        const float wx = 42f, ww = 276f, rowStep = 29.9f;
        float wh = 241.5f + (rows - 3) * rowStep;
        float wy = 390f - wh / 2f;
        Plate(box, "es_win", wx, wy, ww, wh, 4f).raycastTarget = true;   // 판을 눌러도 닫히지 않게
        // 위 보라 '세트' 칩
        Plate(box, "es_chip", 180f - 34.5f, wy - 19.55f, 69f, 32.2f, 3f);
        GameFont(PxText(box, "세트", 180f, wy - 3.45f, 13.8f, Color.white, 1f, Hex("#10142a")), true, Hex("#10142a"));
        // 제목·설명
        GameFont(PxText(box, EquipSets.SetName(st), 180f, wy + 29.2f, 16.1f, Color.white, 1f, Navy), true);
        GameFont(PxText(box, $"같은 세트를 많이 낄수록 {EsIGa(EquipSets.ThemeName(st))} 올라요 · 지금 {now}/{size}", 180f, wy + 46.9f, 9.2f, Hex("#e6efff")), false);
        // 부위 칸: 낀 부위 = 노란 테, 안 낀 부위 = 어둡게
        var pieces = EquipSets.Pieces(st);
        const float pb = 47.15f, pg = 8.05f;
        float px0 = 180f - (pieces.Count * pb + (pieces.Count - 1) * pg) / 2f, py = wy + 59.8f;
        for(int i = 0; i < pieces.Count; i++) {
            bool on = EquipSets.IsWorn(st, pieces[i].part);
            float x = px0 + i * (pb + pg);
            Plate(box, on ? "es_slot_on" : "es_slot_off", x, py, pb, pb, 0f);
            // 안쪽 = 그 장비 등급 색 (가방 칸과 같은 색, 2026-11-26 사용자). 안 낀 부위는 조금 어둡게
            var inner = Plate(box, "es_slot_grade", x + 3.45f, py + 3.45f, 40.25f, 40.25f, 0f);
            Color gc = EquipFrame.GradeColor(pieces[i].grade);
            inner.color = on ? gc : Color.Lerp(gc, Color.black, 0.45f);
            var irt = PxAt("Piece", box, x + pb / 2f, py + pb / 2f, 34f, 34f);
            var im = irt.gameObject.AddComponent<Image>();
            im.sprite = pieces[i].sprite; im.preserveAspect = true; im.raycastTarget = false;
            // 안 가진 장비 = 검은 그림자, 가졌지만 안 낀 장비 = 어둡게
            if(!pieces[i].owned) im.color = Color.black;
            else if(!on) im.color = new Color(0.4f, 0.4f, 0.4f);
            // 가졌지만 안 낀 부위: 누르면 창을 닫고 가방의 그 장비로 가서 두 번 콩콩 (2026-11-26 사용자)
            if(pieces[i].owned && !on) {
                int part = pieces[i].part;
                JobHit(box, "Find" + i, x, py, pb, pb, () => {
                    if(layer != null) Destroy(layer.gameObject);
                    var target = EquipSets.OwnedPiece(st, part);
                    if(target != null) StartCoroutine(GoToEquip(target));
                });
            }
        }
        // 단계 줄: 지금 단계 = 노란 줄, 켜진 단계 = 밝은 줄, 아직 = 어두운 줄 + 자물쇠
        for(int k = 0; k < rows; k++) {
            int n = k + 2;
            bool on = now >= n, sel = n == Mathf.Min(now, size);
            float y = wy + 118.45f + k * rowStep, x = wx + 13.8f;
            Plate(box, sel ? "es_row_sel" : on ? "es_row_on" : "es_row_off", x, y, 248.4f, 25.3f, 0f);
            Plate(box, on ? "es_n_on" : "es_n_off", x + 4.6f, y + 4f, 35.65f, 14.95f, 0f);
            var nt = PxText(box, $"{n}개", x + 4.6f + 17.8f, y + 11.5f, 8.6f, on ? Color.white : Hex("#8a97b8"));
            if(on) StyleBtnText(nt, 8.6f, OnGreen); else GameFont(nt, false);
            bool edge = on && !sel;
            var tt = PxText(box, EquipSets.TierText(st, n), x + 48.3f, y + 12.65f, 10.9f, sel ? Hex("#3a2000") : on ? Color.white : Hex("#9fb0d0"), edge ? 1f : 0f, Navy, TextAlignmentOptions.Left);
            GameFont(tt, edge);
            var ic = PxIcon(box, on ? "Icon_Check03_l" : "Icon_Lock03", x + 248.4f - 6.9f - 7.5f, y + 12.1f, 14.95f);
            if(!on) ic.color = new Color(1f, 1f, 1f, 0.7f);
        }
        // 확인
        float oy = wy + 209.3f + (rows - 3) * rowStep;
        Plate(box, "es_ok", 134f, oy, 92f, 25.3f, 3f);
        GameFont(PxText(box, "확인", 180f, oy + 12.65f, 11.5f, Color.white, 1f, Navy), true);
        JobHit(box, "Ok", 134f, oy, 92f, 25.3f, () => { if(layer != null) Destroy(layer.gameObject); });
        StartCoroutine(PopIn(box, 1f));
    }
    // 받침 있으면 '이', 없으면 '가' (공격력이 · 공격 속도가)
    static string EsIGa(string w)
    {
        if(string.IsNullOrEmpty(w)) return w;
        char c = w[w.Length - 1];
        bool jong = c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
        return w + (jong ? "이" : "가");
    }

    // ---- 가방에서 장비 찾아 주기: 그 부위 탭으로 바꾸고 → 보이게 스크롤 → 두 번 점프
    IEnumerator GoToEquip(EquipFrame f)
    {
        if(f == null) yield break;
        // 1. 탭: 이 장비가 들어 있는 칸 묶음을 objectsToSwap 으로 가진 TabGroup 을 찾아 그 탭을 골라요
        foreach(var tg in FindObjectsByType<TabGroup>(FindObjectsInactive.Include)) {
            if(tg == null || tg.objectsToSwap == null || tg.tabButtons == null) continue;
            int idx = -1;
            for(int i = 0; i < tg.objectsToSwap.Count && idx < 0; i++)
                if(tg.objectsToSwap[i] != null && f.transform.IsChildOf(tg.objectsToSwap[i].transform)) idx = i;
            if(idx < 0) continue;
            foreach(var b in tg.tabButtons) if(b != null && b.transform.GetSiblingIndex() == idx) { tg.SelectTabbyIndex(b); break; }
            break;
        }
        yield return null;   // 탭이 켜지고 칸 배치가 끝나게
        Canvas.ForceUpdateCanvases();
        // 2. 스크롤: 장비가 보이는 칸 가운데쯤 오게 (0.3초)
        var sr = f.GetComponentInParent<ScrollRect>();
        if(sr != null && sr.content != null) {
            var content = sr.content;
            var view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            float itemY = -content.InverseTransformPoint(f.transform.position).y;          // 내용 맨 위에서 장비까지
            float maxY = Mathf.Max(0f, content.rect.height - view.rect.height);
            float goal = Mathf.Clamp(itemY - view.rect.height * 0.5f, 0f, maxY);
            float from = content.anchoredPosition.y, t = 0f;
            sr.velocity = Vector2.zero;
            while(t < 0.3f && content != null) {
                t += Time.unscaledDeltaTime;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Lerp(from, goal, Mathf.SmoothStep(0f, 1f, t / 0.3f)));
                yield return null;
            }
        }
        // 3. 두 번 점프 (칸 배치는 그대로 두고, 장비 칸 안의 그림들을 위로 들었다 내려요)
        if(f == null) yield break;
        var kids = new List<RectTransform>();
        var basePos = new List<Vector2>();
        foreach(Transform c in f.transform) if(c is RectTransform rt) { kids.Add(rt); basePos.Add(rt.anchoredPosition); }
        float h = Tuning.Get("equip_find_jump", 40f);
        for(int j = 0; j < 2; j++) {
            float t = 0f, dur = 0.32f, hh = j == 0 ? h : h * 0.6f;
            while(t < dur && f != null) {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float y = 4f * k * (1f - k) * hh;   // 포물선
                for(int i = 0; i < kids.Count; i++) if(kids[i] != null) kids[i].anchoredPosition = basePos[i] + new Vector2(0f, y);
                yield return null;
            }
        }
        for(int i = 0; i < kids.Count; i++) if(kids[i] != null) kids[i].anchoredPosition = basePos[i];
    }

    // ---- 가방 정렬 칩 줄 B안 (2026-11-26 사용자 "추천대로 B"): 부위 탭 바로 아래, 칩 5개 + ↓↑. sort_preview.js 의 B
    //   스크린샷 px → 게임 단위 ×1.709 (1080 / 632), 미리보기 px = 단위 / 3
    const float SoRowH = 85f;                      // 끼우는 줄 높이 (단위) = 스크린샷 50px
    const float SoChipW = 54.7f, SoChipH = 19.4f, SoGap = 3.4f, SoArrowW = 22.8f;   // 미리보기 px
    RectTransform soRow;
    string soKey;
    // 가방을 크게 (2026-11-26 사용자: "장비가 너무 안 보여, 장비가 메인이 돼야") — 위 장비 정보 영역을 위 기준으로 줄이고 가방을 그만큼 올려요
    RectTransform soBag;
    float soMidH, soBagTop, soMidTop;
    RectTransform soPanel;
    readonly System.Collections.Generic.List<RectTransform> soViews = new System.Collections.Generic.List<RectTransform>();

    void UpdateEquipSortBar()
    {
        if(soRow == null && !SetupEquipSortBar()) return;
        if(!soRow.gameObject.activeInHierarchy) return;
        ApplyBagLayout();
        string key = EquipSorting.Mode + "_" + EquipSorting.Ascending;
        if(key == soKey) return;
        soKey = key;
        BuildEquipSortBar();
    }

    // 매 프레임 (tuning 바로 반영): 위 영역 크기 equip_top_scale, 가방 칸 위 여백 equip_bag_top_inset (원래 105)
    void ApplyBagLayout()
    {
        float sc = Mathf.Clamp(Tuning.Get("equip_top_scale", 0.85f), 0.5f, 1f);
        var v3 = new Vector3(sc, sc, 1f);
        if(esMiddle.localScale != v3) esMiddle.localScale = v3;
        float bagTop = soBagTop + (1f - sc) * soMidH;
        if(Mathf.Abs(soBag.offsetMax.y - bagTop) > 0.01f) soBag.offsetMax = new Vector2(soBag.offsetMax.x, bagTop);
        // 위 판을 '창 맨 위 ~ 가방 위 끝' 사이 세로 가운데로 (2026-11-26 사용자: "좀 더 올려서 가운데로") + equip_top_lift 만큼 더 위로
        float gapBelow = (soMidTop - sc * soMidH) - bagTop;          // 판 아래 끝 ~ 가방 위 끝
        float d = Tuning.Get("equip_top_center", 1f) > 0f ? (-soMidTop - gapBelow) / 2f : 0f;
        d += Tuning.Get("equip_top_lift", 0f);
        float midY = soMidTop + d;
        if(Mathf.Abs(esMiddle.anchoredPosition.y - midY) > 0.01f) esMiddle.anchoredPosition = new Vector2(esMiddle.anchoredPosition.x, midY);
        // 판 양옆 넓히기 (위 영역 바깥으로 equip_top_panel_wide 씩, 단위)
        if(soPanel != null) {
            float wx = Tuning.Get("equip_top_panel_wide", 36f);
            var om = new Vector2(wx, 0f);
            if(soPanel.offsetMax != om) { soPanel.offsetMax = om; soPanel.offsetMin = -om; }
        }
        float inset = Tuning.Get("equip_bag_top_inset", 12f);
        foreach(var v in soViews) if(v != null && Mathf.Abs(v.offsetMax.y + inset) > 0.01f) v.offsetMax = new Vector2(v.offsetMax.x, -inset);
    }

    bool SetupEquipSortBar()
    {
        if(esMiddle == null) return false;   // 장비 창을 찾은 뒤 (SetupEquipSetBar)
        var bag = esMiddle.parent != null ? esMiddle.parent.Find("Equips") as RectTransform : null;
        var menu = bag != null ? bag.Find("Menu") as RectTransform : null;
        var tabs = bag != null ? bag.Find("Tabs") as RectTransform : null;
        if(bag == null || menu == null || tabs == null) return false;
        tabs.offsetMax -= new Vector2(0f, SoRowH);   // 칸 묶음 위 끝을 한 줄만큼 내려요
        // 위 영역(Middle)은 위 끝 기준으로 줄일 수 있게 pivot 을 위로 (자리는 그대로)
        float top = esMiddle.anchoredPosition.y + esMiddle.rect.height * (1f - esMiddle.pivot.y);
        esMiddle.pivot = new Vector2(esMiddle.pivot.x, 1f);
        esMiddle.anchoredPosition = new Vector2(esMiddle.anchoredPosition.x, top);
        soMidTop = top;
        // 버튼 줄 여백 (2026-11-26 사용자: "버튼들이 끝에 딱 붙어 있어 답답해"): 판 아래를 늘려 버튼 밑 여백, 버튼 줄 양옆·사이 여백
        float padB = Tuning.Get("equip_btn_pad_bottom", 30f);
        esMiddle.sizeDelta += new Vector2(0f, padB);   // pivot 이 위라서 아래로만 늘어요
        bag.offsetMax -= new Vector2(0f, padB);         // 가방도 그만큼 아래로 (판과 겹치지 않게)
        var btnRow = esMiddle.Find("Buttons");
        var hl = btnRow != null ? btnRow.GetComponent<HorizontalLayoutGroup>() : null;
        if(hl != null) {
            int side = Mathf.RoundToInt(Tuning.Get("equip_btn_pad_side", 32f));
            hl.padding.left = side; hl.padding.right = side;
            hl.spacing = Tuning.Get("equip_btn_gap", 16f);
        }
        soMidH = esMiddle.rect.height;
        // 배경 나누기 (2026-11-26 사용자: 한 색보다 나눈 게 좋아 보여 → 추천): 위 영역 = 테두리 있는 둥근 판, 바깥 바탕·맨 위 띠 = 진한 파랑
        // 판은 위 영역과 따로 (양옆으로 더 넓게, 2026-11-26 사용자: "창을 좀 더 키워 봐 오른쪽 왼쪽으로") — 위 영역 자체 배경은 투명
        var midImg = esMiddle.GetComponent<Image>();
        if(midImg != null) midImg.color = new Color(1f, 1f, 1f, 0f);
        soPanel = Rect("TopPanel (remaster)", esMiddle, Vector2.zero, Vector2.zero);
        soPanel.anchorMin = Vector2.zero; soPanel.anchorMax = Vector2.one; soPanel.pivot = new Vector2(0.5f, 0.5f);
        soPanel.SetAsFirstSibling();   // 맨 뒤
        var pimg = soPanel.gameObject.AddComponent<Image>();
        pimg.sprite = MapSprite("eq_top_panel");
        pimg.type = Image.Type.Sliced; pimg.pixelsPerUnitMultiplier = 1f;
        pimg.raycastTarget = false;
        Color back = Hex("#123a8c");
        var winImg = bag.parent != null ? bag.parent.GetComponent<Image>() : null;
        if(winImg != null) winImg.color = back;
        var topbar = bag.parent != null ? bag.parent.Find("Topbar") : null;
        var tbImg = topbar != null ? topbar.GetComponent<Image>() : null;
        if(tbImg != null) tbImg.color = back;
        // 맨 위 띠 오른쪽 '일괄 분해' (2026-11-26: 분해 창 A 를 여는 곳)
        if(topbar != null) {
            float th = ((RectTransform)topbar).rect.height / K;
            Plate(topbar, "mg_btn_p", 360f - 12f - 92f, (th - 30f) / 2f, 92f, 30f, 3f);
            GameFont(PxText(topbar, "일괄 분해", 360f - 12f - 46f, th / 2f, 12f, Color.white, 1f, MgEdge), true, MgEdge);
            JobHit(topbar, "OpenDismantle", 360f - 12f - 92f, (th - 30f) / 2f, 92f, 30f, () => { if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); ShowDismantle(true); });
        }
        soBag = bag; soBagTop = bag.offsetMax.y;
        foreach(Transform t in tabs) { var v = t.Find("Viewport") as RectTransform; if(v != null) soViews.Add(v); }
        soRow = Rect("SortBar (remaster)", bag, Vector2.zero, Vector2.zero);
        soRow.anchorMin = new Vector2(0.5f, 1f); soRow.anchorMax = new Vector2(0.5f, 1f); soRow.pivot = new Vector2(0.5f, 1f);
        float w = 5f * SoChipW + SoArrowW + 5f * SoGap;
        soRow.sizeDelta = new Vector2(w * K, SoRowH);
        soRow.anchoredPosition = new Vector2(0f, -(menu.rect.height + 4f * 1.709f));   // 탭 줄 아래 4px
        return true;
    }

    void BuildEquipSortBar()
    {
        for(int i = soRow.childCount - 1; i >= 0; i--) Destroy(soRow.GetChild(i).gameObject);
        int mode = EquipSorting.Mode;
        for(int i = 0; i < 5; i++) {
            int ii = i;
            float x = i * (SoChipW + SoGap);
            bool on = i == mode;
            Plate(soRow, on ? "so_chip_on" : "so_chip_off", x, 0f, SoChipW, SoChipH, on ? 2f : 0f);
            var t = PxText(soRow, EquipSorting.SortNames[i], x + SoChipW / 2f, SoChipH / 2f, 9.1f, on ? Color.white : Hex("#cfe2ff"), on ? 1f : 0f, MgEdge);
            GameFont(t, on, MgEdge);
            JobHit(soRow, "Sort" + i, x, 0f, SoChipW, SoChipH, () => {
                if(EquipSorting.Mode == ii) return;
                EquipSorting.Mode = ii;
                if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
            });
        }
        float ax = 5f * (SoChipW + SoGap);
        Plate(soRow, EquipSorting.Ascending ? "so_arrow_up" : "so_arrow_dn", ax, 0f, SoArrowW, SoChipH, 2f);
        JobHit(soRow, "SortDir", ax, 0f, SoArrowW, SoChipH, () => {
            EquipSorting.Ascending = !EquipSorting.Ascending;
            if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        });
    }
}
