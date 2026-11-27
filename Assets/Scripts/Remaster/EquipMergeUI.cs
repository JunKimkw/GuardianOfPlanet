using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장비 합성 창 A안 '마법진 제단' + 결과 화면 (2026-11-26 사용자: "UI 도 멋지고 임팩트 있게", 미리보기 A 고름 → "추천대로 해 줘")
// - 규칙: 같은 등급 장비 3개(장착 안 한 것) → 같은 등급 무작위 장비 1개 (부위·잠재력 새로). 고대도 돼요
// - 미리보기 tools/ui_preview/merge_preview.js 의 A·결과와 같은 px 좌표 (폭 360 = 1080, 1px = 3단위). 판·칸·버튼 = render_sprites.js 의 mg_*
// - 장비 창 '합 성' 버튼(EquipManager.JinhwaButton)이 이 창을 열어요 (예전 MergeManager 창 대신)
public partial class RemasterUI
{
    static readonly string[] MgKey = { "normal", "rare", "epic", "unique", "legend", "ancient" };
    static readonly string[] MgName = { "일반", "레어", "에픽", "유니크", "전설", "고대" };
    static readonly string[] MgGlow = { "#82a0ab", "#35fbff", "#ff8aff", "#fffe00", "#ff8f9c", "#ff8d00" };   // 등급 빛 색 (EquipFrame Light)
    static readonly Color MgEdge = new Color32(0x10, 0x14, 0x2a, 0xff);

    Equips.MaterialClass mgGrade;
    readonly List<EquipFrame> mgSel = new List<EquipFrame>();
    RectTransform mgLayer;
    float mgScroll;
    bool mgBusy;
    bool mgArmed;   // 강화된 장비가 들어 있을 때 '합성!' 한 번 누름 (한 번 더 누르면 합성)

    public static void OpenEquipMerge(EquipFrame from)
    {
        var ui = FindAnyObjectByType<RemasterUI>();
        if(ui != null) ui.OpenMerge(from);
    }

    void OpenMerge(EquipFrame from)
    {
        if(!EnsureBuilt()) return;
        mgSel.Clear();
        mgScroll = 0f;
        mgGrade = from != null && from.equipData != null ? from.equipData.Grade : Equips.MaterialClass.Normal;
        if(from != null && !EquipBag.IsEquipped(from)) mgSel.Add(from);
        BuildMerge(true);
    }

    // 그 등급의 재료 후보: 장착 안 한 것, 강화 낮은 것 · 잠재력 낮은 것부터 (자동 채우기 순서)
    static List<EquipFrame> MgPool(Equips.MaterialClass g)
    {
        var em = EquipManager.Instance;
        if(em == null || em.All_Equips == null) return new List<EquipFrame>();
        return em.All_Equips.Where(f => f != null && f.equipData != null && f.equipData.Grade == g && !EquipBag.IsEquipped(f))
            .OrderBy(f => f.equipData.Level).ThenBy(f => f.equipData.Quality01).ToList();
    }
    static int MgNeed => Equips.MergeNeed;

    void BuildMerge(bool pop)
    {
        MapFont();
        MgClearLayers();
        var layer = Stretch("EquipMerge", modalRoot);
        layer.SetAsLastSibling();
        mgLayer = layer;
        var dim = Stretch("Dim", layer);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseMerge);
        // 전체화면 (2026-11-27 사용자 "그 창들이 전체화면이 되게" - 합성 창도 같게)
        float W = modalRoot.rect.width > 0 ? modalRoot.rect.width : 1080f, H = modalRoot.rect.height > 0 ? modalRoot.rect.height : 1920f;
        const float mTop = 18f, mBottom = 754f, mWinW = 352f, mWinY = 34f;   // 제목 리본 위 끝 · 창 아래 끝 · 창 폭 · 창 위 끝 (미리보기 px)
        GenSafe(H, out float safeTop, out float safeBot);   // 카메라 구멍·노치 자리는 비워요 (UIGen 과 같게, 2026-11-27)
        float fit = Mathf.Min(W / (mWinW * K), Mathf.Max(H * 0.5f, H - safeTop - safeBot) / ((mBottom - mTop) * K));
        float cdx = (W / 2f) / (K * fit) - mWinW / 2f;   // 닫기를 화면 오른쪽 끝으로 옮길 만큼 (px)
        var panel = Stretch("Panel", layer);
        var pimg = panel.gameObject.AddComponent<Image>();
        pimg.sprite = MapSprite("mg_win"); pimg.type = Image.Type.Sliced; pimg.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, fit);
        float padU = 7f * K * fit, topInset = (mWinY - mTop) * K * fit;
        panel.offsetMin = new Vector2(-padU, -padU);
        panel.offsetMax = new Vector2(padU, -(safeTop + topInset - padU));
        var box = Rect("Box", layer, new Vector2(1080, 780 * K), Vector2.zero);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, mTop * K * fit - safeTop);
        int gi = (int)mgGrade;
        string key = MgKey[gi];
        Color glow = Hex(MgGlow[gi]);
        var pool = MgPool(mgGrade);
        mgSel.RemoveAll(f => f == null || !pool.Contains(f));

        // 판 · 제목 · 닫기
        var tt = GameFont(PxText(box, "장비 합성", 180f, 36f, 20f, Color.white, 1.6f, MgEdge), true, MgEdge);
        float tw = tt.GetPreferredValues(tt.text).x / K;
        Plate(box, "mg_title", 180f - (tw + 56f) / 2f, 18f, tw + 56f, 36f, 0f).transform.SetSiblingIndex(tt.transform.GetSiblingIndex());
        Plate(box, "mg_close", 326f + cdx, 42f, 24f, 24f, 0f);
        GameFont(PxText(box, "×", 338f + cdx, 54f, 15f, Color.white, 1f, MgEdge), true, MgEdge);
        JobHit(box, "Close", 322f + cdx, 38f, 32f, 32f, CloseMerge);

        // 마법진: 바깥·안쪽 고리 + 빛 + 가운데 ? 카드 + 재료 3칸 (위 · 왼쪽 아래 · 오른쪽 아래)
        Plate(box, "mg_ring_l", 72f, 82f, 216f, 216f, 20f).color = glow;
        Plate(box, "mg_ring_s", 106f, 116f, 148f, 148f, 20f).color = glow;
        Plate(box, "mg_altar", 0f, 96f, 360f, 190f, 0f).color = glow;
        var q = Plate(box, "mg_q_" + key, 150f, 160f, 60f, 60f, 30f);
        GameFont(PxText(box, "?", 180f, 190f, 33f, Color.white, 2f, MgEdge), true, MgEdge);
        var slots = new[] { new Vector2(150f, 72f), new Vector2(58f, 232f), new Vector2(244f, 232f) };
        for(int i = 0; i < 3; i++) {
            int ii = i;
            if(i < mgSel.Count) {
                MgCell(box, mgSel[i], slots[i].x, slots[i].y, 58f, false);
                JobHit(box, "Slot" + i, slots[i].x, slots[i].y, 58f, 58f, () => { if(ii < mgSel.Count) { mgSel.RemoveAt(ii); BuildMerge(false); } });
            } else Plate(box, "mg_slot_empty", slots[i].x, slots[i].y, 58f, 58f, 0f);
        }

        // 규칙 한 줄 → 강화된 장비를 넣으면 경고로 바뀌어요 (2026-11-26 사용자)
        var leveled = mgSel.Where(x => x.equipData.Level > 0).ToList();
        if(leveled.Count > 0)
            GameFont(PxText(box, $"강화된 장비가 있어요 (Lv.{leveled.Max(x => x.equipData.Level)}) · 합성하면 강화가 사라져요", 180f, 307f, 11f, Hex("#ff6b6b"), 1f, MgEdge), true, MgEdge);
        else
            GameFont(PxText(box, $"같은 등급 {MgNeed}개 → <color=#ffe14a>같은 등급 무작위 1개</color> · 부위·잠재력 새로", 180f, 307f, 11f, Hex("#e6efff")), false);
        if(leveled.Count == 0) mgArmed = false;

        // 등급 탭 (가진 개수): 등급마다 그 색, 고르지 않은 탭은 어둡게, 고른 탭은 흰 테두리 빛 (2026-11-26 사용자)
        for(int g = 0; g < 6; g++) {
            int gg = g, n = MgPool((Equips.MaterialClass)g).Count;
            float x = 20f + 54f * g;
            if(g == gi) Plate(box, "mg_tab_sel", x, 322f, 50f, 40f, 8f);
            var tab = Plate(box, (g == gi ? "mg_tab_" : "mg_tab_dim_") + MgKey[g], x, 322f, 50f, 40f, 0f);
            var t1 = GameFont(PxText(box, MgName[g], x + 25f, 336.8f, 11f, Color.white, 0.9f, MgEdge), true, MgEdge);
            var t2 = GameFont(PxText(box, $"{n}개", x + 25f, 347.8f, 10f, g == gi ? Color.white : Hex("#cfe2ff")), false);
            if(n == 0 && g != gi) { tab.color = new Color(1, 1, 1, 0.45f); t1.alpha = 0.45f; t2.alpha = 0.45f; }
            JobHit(box, "Tab" + g, x, 322f, 50f, 40f, () => { if(mgGrade != (Equips.MaterialClass)gg) { mgGrade = (Equips.MaterialClass)gg; mgSel.Clear(); mgScroll = 0f; BuildMerge(false); } });
        }

        // 목록 (스크롤): 5칸씩, 고른 장비는 초록 테두리 + 체크
        Plate(box, "mg_panel", 24f, 372f, 312f, 248f, 0f);
        var view = PxBox("View", box, 24f, 372f, 312f, 248f);
        view.gameObject.AddComponent<RectMask2D>();
        var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(1, 1, 1, 0.001f);
        int rows = Mathf.Max(1, Mathf.CeilToInt(pool.Count / 5f));
        var content = PxBox("Content", view, 0f, 0f, 312f, Mathf.Max(248f, 10f + rows * 60f));
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f; sr.viewport = view;
        for(int i = 0; i < pool.Count; i++) {
            var f = pool[i];
            float x = 10f + 60f * (i % 5), y = 10f + 60f * (i / 5);
            bool sel = mgSel.Contains(f);
            MgCell(content, f, x, y, 52f, sel);
            JobHit(content, "Item" + i, x, y, 52f, 52f, () => {
                if(mgSel.Contains(f)) mgSel.Remove(f); else if(mgSel.Count < 3) mgSel.Add(f);
                mgScroll = content.anchoredPosition.y;
                BuildMerge(false);
            });
        }
        if(pool.Count == 0) GameFont(PxText(box, $"{MgName[gi]} 장비가 없어요", 180f, 496f, 12f, Hex("#cfe2ff")), false);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, mgScroll);

        // 버튼: 자동 채우기 · 합성! · 이 등급 모두 합성
        Plate(box, "mg_btn_b", 24f, 634f, 100f, 44f, 3f);
        GameFont(PxText(box, "자동 채우기", 74f, 656f, 14f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Auto", 24f, 634f, 100f, 44f, () => {
            foreach(var f in pool) { if(mgSel.Count >= 3) break; if(f.equipData.Level == 0 && !mgSel.Contains(f)) mgSel.Add(f); }   // 강화 안 한 장비만
            BuildMerge(false);
        });
        bool ready = mgSel.Count >= 3;
        var go = Plate(box, "mg_btn_y", 132f, 634f, 196f, 44f, 3f);
        if(!ready) go.color = new Color(0.55f, 0.55f, 0.6f);
        bool warn = ready && leveled.Count > 0;
        GameFont(PxText(box, warn && mgArmed ? "한 번 더 누르면 합성" : "합성!", 230f, 656f, warn && mgArmed ? 15f : 20f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Go", 132f, 634f, 196f, 44f, () => {
            if(mgSel.Count < 3 || mgBusy) return;
            if(mgSel.Any(x => x.equipData.Level > 0) && !mgArmed) { mgArmed = true; BuildMerge(false); return; }   // 강화된 장비: 두 번 눌러야 해요
            mgArmed = false;
            StartCoroutine(MergeRun(box, slots, false));
        });
        int times = pool.Count(x => x.equipData.Level == 0) / MgNeed;   // 모두 합성 = 강화 안 한 장비만
        var all = Plate(box, "mg_btn_p", 80f, 690f, 200f, 30f, 3f);
        if(times == 0) all.color = new Color(0.55f, 0.55f, 0.6f);
        GameFont(PxText(box, $"{MgName[gi]} 모두 합성 ({times}번)", 180f, 705f, 12f, Color.white, 1f, MgEdge), true, MgEdge);
        JobHit(box, "All", 80f, 690f, 200f, 30f, () => { if(MgPool(mgGrade).Count(x => x.equipData.Level == 0) >= MgNeed && !mgBusy) StartCoroutine(MergeRun(box, slots, true)); });

        box.localScale = new Vector3(fit, fit, 1f);
        if(pop) StartCoroutine(SlideIn(box, fit)); else box.localScale = new Vector3(fit, fit, 1f);   // 아래에서 올라와요 (2026-11-28 사용자: 모든 창)
    }

    // 합성 창·결과 창 지우기: 이름으로 하나만 찾지 않고 모두 (지우는 중인 창이 남아 새 창을 가리지 않게, 2026-11-26 "두 번째부터 장비가 안 보여")
    void MgClearLayers()
    {
        for(int i = modalRoot.childCount - 1; i >= 0; i--) {
            var c = modalRoot.GetChild(i);
            if(c.name == "EquipMerge") { c.name = "EquipMerge (closing)"; c.gameObject.SetActive(false); Destroy(c.gameObject); }
        }
    }

    // 화면에 맞추기 (2026-11-26 사용자: "화면에 안 맞아") — 틀 360×780 px 가 화면 높이·폭 안에 들어가게
    float MgFit(float scale)
    {
        float h = modalRoot.rect.height > 0 ? modalRoot.rect.height : 1920f, w = modalRoot.rect.width > 0 ? modalRoot.rect.width : 1080f;
        return Mathf.Min(1f, (h - 40f) / (780f * K), w / 1080f) * scale;
    }

    // 장비 칸 (게임 가방 칸 모양): 등급 색 판 + 장비 그림 + Lv + (고름 표시)
    void MgCell(Transform parent, EquipFrame f, float x, float y, float s, bool sel)
    {
        int g = Mathf.Clamp((int)f.equipData.Grade, 0, 5);
        if(sel) Plate(parent, "mg_sel", x, y, s, s, 14f * s / 52f);
        Plate(parent, "mg_cell_" + MgKey[g], x, y, s, s, 0f);
        var irt = PxBox("Icon", parent, x + s * 0.14f, y + s * 0.12f, s * 0.72f, s * 0.72f);
        var im = irt.gameObject.AddComponent<Image>();
        im.sprite = f.equipData.Sprite_Equip; im.preserveAspect = true; im.raycastTarget = false;
        var lv = PxText(parent, $"Lv.{f.equipData.Level}", x + s - s * 0.07f, y + s - s * 0.02f - s * 0.12f, s * 0.2f, Color.white, 1f, MgEdge, TextAlignmentOptions.Right);
        GameFont(lv, true, MgEdge);
        lv.characterSpacing = Tuning.Get("merge_lv_spacing", 6f);   // 글자 사이 (2026-11-26 사용자: "Lv.0 글자가 뭉쳐 있어")
        if(sel) PxIcon(parent, "Icon_Check03_l", x + s + 6f - s * 0.19f, y - 6f + s * 0.19f, s * 0.38f);
    }

    void CloseMerge()
    {
        if(mgBusy) return;
        MgClearLayers();
        mgLayer = null;
        var em = EquipManager.Instance;
        if(em != null) em.ChangeEquipsUI();
    }

    // 합성: 재료가 가운데로 빨려 들어가고 → 번쩍 → 결과
    IEnumerator MergeRun(RectTransform box, Vector2[] slots, bool all)
    {
        mgBusy = true;
        var pool = MgPool(mgGrade);
        if(all) pool = pool.Where(x => x.equipData.Level == 0).ToList();   // 모두 합성은 강화 안 한 장비만
        var mats = all ? pool.Take(pool.Count / MgNeed * MgNeed).ToList() : mgSel.Take(MgNeed).ToList();
        if(mats.Count < MgNeed) { mgBusy = false; yield break; }
        // 재료 칸이 가운데 ? 카드로 (0.35초)
        var center = new Vector2(180f * K, -190f * K);
        var parts = new List<(RectTransform rt, Vector2 from)>();
        for(int i = 0; i < 3; i++) {
            var fly = PxAt("Fly" + i, box, slots[i].x + 29f, slots[i].y + 29f, 58f, 58f);   // 가운데 기준 (작아질 때 제자리에서)
            var inner = PxBox("In", fly, 0f, 0f, 58f, 58f);
            MgCell(inner, mats[Mathf.Min(i, mats.Count - 1)], 0f, 0f, 58f, false);
            parts.Add((fly, fly.anchoredPosition));
        }
        float t = 0f;
        while(t < 0.35f) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            foreach(var p in parts) {
                if(p.rt == null) continue;
                p.rt.anchoredPosition = Vector2.Lerp(p.from, center, k);
                p.rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
            }
            yield return null;
        }
        foreach(var p in parts) if(p.rt != null) Destroy(p.rt.gameObject);
        // 번쩍 (흰 화면)
        var flash = Stretch("EquipMergeFlash", modalRoot);
        flash.SetAsLastSibling();
        var fimg = flash.gameObject.AddComponent<Image>();
        fimg.sprite = MapSprite("mg_flash"); fimg.raycastTarget = true;
        t = 0f;
        while(t < 0.12f) { t += Time.unscaledDeltaTime; fimg.color = new Color(1, 1, 1, t / 0.12f); yield return null; }

        // 실제 합성
        var results = new List<EquipFrame>();
        var em = EquipManager.Instance;
        float rlo = Tuning.Get("equip_roll_min", 1f), rhi = Tuning.Get("equip_roll_max", 2f);
        for(int i = 0; i + MgNeed <= mats.Count; i += MgNeed) {
            // 재료 중 가장 낮은 잠재력 → 새 장비는 능력치마다 그보다 낮게 안 나와요 (2026-11-26 사용자)
            float qmin = 1f;
            for(int j = i; j < i + MgNeed; j++) qmin = Mathf.Min(qmin, mats[j].equipData.Quality01);
            var rolls = new float[EquipBag.StatCount];
            for(int k = 0; k < rolls.Length; k++) rolls[k] = Random.Range(Mathf.Lerp(rlo, rhi, qmin), rhi);
            for(int j = i; j < i + MgNeed; j++) EquipBag.Remove(mats[j]);
            var pick = em.getRandomItem_Grade(mgGrade, GachaManager.Instance != null ? GachaManager.Instance.getRandomType() : (Equips.Type)Random.Range(0, 5));
            var r = pick != null ? EquipBag.Create(pick.equipData, null, rolls, 0, null) : null;
            if(r != null) { r.equipData.setFinalOptionStats(); results.Add(r); }
        }
        mgSel.Clear();
        if(em != null) { em.unSelectAfterMerge(); em.ChangeEquipsUI(); }
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        // 가장 좋은 결과 (잠재력 높은 것)를 크게
        Debug.Log($"[합성] {MgName[(int)mgGrade]} {mats.Count}개 → 새 장비 {results.Count}개: {string.Join(", ", results.Select(x => x.equipData.EquipName))}");
        ShowMergeResult(results);
        if(flash != null) flash.SetAsLastSibling();   // 결과 창 위에서 번쩍이 사라져요
        t = 0f;
        while(t < 0.25f && fimg != null) { t += Time.unscaledDeltaTime; fimg.color = new Color(1, 1, 1, 1f - t / 0.25f); yield return null; }
        if(flash != null) Destroy(flash.gameObject);
        mgBusy = false;
    }

    // ---- 결과 화면 (미리보기 '결과'): 빛줄기 · 합성 성공! · 큰 카드 · 등급·부위 띠 · 이름 · 능력치 · 한 번 더 / 확인
    // 2026-11-26 사용자: 결과 UI 조금 작게 (merge_result_scale), 모두 합성이면 새 장비를 모두 보여 줘요
    void ShowMergeResult(List<EquipFrame> results)
    {
        results = results.Where(x => x != null).ToList();
        if(results.Count == 0) { BuildMerge(false); return; }
        var r = results.OrderByDescending(x => x.equipData.Quality01).First();   // 가장 좋은 것 (잠재력)
        bool many = results.Count > 1;
        MgClearLayers();
        var layer = Stretch("EquipMerge", modalRoot);
        layer.SetAsLastSibling();
        mgLayer = layer;
        var dim = Stretch("Dim", layer);
        dim.gameObject.AddComponent<Image>().color = new Color(5f / 255f, 8f / 255f, 25f / 255f, 0.7f);
        var box = Rect("Box", layer, new Vector2(1080, 780 * K), Vector2.zero);
        float fit = MgFit(Tuning.Get("merge_result_scale", 0.8f));
        box.localScale = new Vector3(fit, fit, 1f);
        int gi = Mathf.Clamp((int)r.equipData.Grade, 0, 5);
        Color glow = Hex(MgGlow[gi]);

        var rays = Plate(box, "mg_rays", -120f, 80f, 600f, 600f, 0f);
        rays.color = glow;
        Plate(box, "mg_burst", 60f, 260f, 240f, 240f, 0f).color = Color.Lerp(Color.white, glow, 0.35f);
        GameFont(PxText(box, many ? $"합성 성공! ×{results.Count}" : "합성 성공!", 180f, 170f, 34f, Hex("#ffe14a"), 2.2f, MgEdge), true, MgEdge);
        float by;
        RectTransform popTarget;
        if(!many) {
            var card = PxBox("Card", box, 110f, 270f, 140f, 140f);
            MgCell(card, r, 0f, 0f, 140f, false);
            popTarget = card;
            string part = r.equipData.TypeOfEquip switch {
                Equips.Type.Ring => "반지", Equips.Type.Necklace => "목걸이", Equips.Type.Book => "고서", Equips.Type.Accessory => "장신구", _ => "유물" };
            var bt = GameFont(PxText(box, $"{MgName[gi]} · {part}", 180f, 443.5f, 16f, Color.white, 1.3f, MgEdge), true, MgEdge);
            float bw = bt.GetPreferredValues(bt.text).x / K + 44f;
            Plate(box, "mg_band_" + MgKey[gi], 180f - bw / 2f, 428f, bw, 31f, 0f).transform.SetSiblingIndex(bt.transform.GetSiblingIndex());
            GameFont(PxText(box, r.equipData.EquipName, 180f, 482f, 20f, Color.white, 1.6f, MgEdge), true, MgEdge);
            var o = r.equipData.FinalOption.option;
            var rowsL = new List<(string, string)>();
            if(o.damagePercent >= 5e-4f) rowsL.Add(("데미지", $"+{o.damagePercent * 100f:0.#}%"));
            if(o.AttackSpeed != 0) rowsL.Add(("공격 속도", $"+{o.AttackSpeed * 100f:0.#}%"));
            if(o.CritChance != 0) rowsL.Add(("치명 확률", $"+{o.CritChance:0.#}%"));
            if(o.CritDamage != 0) rowsL.Add(("치명 피해", $"+{o.CritDamage * 100f:0.#}%"));
            if(o.BulletSpeed != 0) rowsL.Add(("방어 관통", $"+{o.BulletSpeed * 100f:0.#}%"));
            if(o.StunPercent != 0) rowsL.Add(("보스 피해", $"+{o.StunPercent:0.#}%"));
            if(o.ExtraMoney != 0) rowsL.Add(("골드", $"+{o.ExtraMoney * 100f:0.#}%"));
            if(o.ExtraEXP != 0) rowsL.Add(("경험치", $"+{o.ExtraEXP * 100f:0.#}%"));
            float sh = 20f + 20.8f * Mathf.Max(1, rowsL.Count);
            Plate(box, "mg_stats", 60f, 506f, 240f, sh, 0f);
            for(int i = 0; i < rowsL.Count; i++) {
                float cy = 526.4f + 20.8f * i;
                GameFont(PxText(box, rowsL[i].Item1, 74f, cy, 13f, Hex("#cfe2ff"), 0f, null, TextAlignmentOptions.Left), false);
                GameFont(PxText(box, rowsL[i].Item2, 286f, cy, 13f, Hex("#7dff9a"), 0f, null, TextAlignmentOptions.Right), false);
            }
            by = Mathf.Max(600f, 506f + sh + 20f);
        } else {
            // 모두 합성: 새 장비를 모두 칸으로 (5칸씩, 잠재력 좋은 순). 25개 넘으면 '+n'
            var bt = GameFont(PxText(box, $"{MgName[gi]} 새 장비 {results.Count}개", 180f, 225f, 16f, Color.white, 1.3f, MgEdge), true, MgEdge);
            float bw = bt.GetPreferredValues(bt.text).x / K + 44f;
            Plate(box, "mg_band_" + MgKey[gi], 180f - bw / 2f, 209.5f, bw, 31f, 0f).transform.SetSiblingIndex(bt.transform.GetSiblingIndex());
            var grid = PxBox("Grid", box, 0f, 0f, 360f, 780f);
            popTarget = grid;
            int show = Mathf.Min(results.Count, 25);
            var list = results.OrderByDescending(x => x.equipData.Quality01).ToList();
            for(int i = 0; i < show; i++) MgCell(grid, list[i], 34f + 60f * (i % 5), 256f + 60f * (i / 5), 52f, false);   // 체크 없이 (2026-11-26 사용자)
            int rowsN = Mathf.CeilToInt(show / 5f);
            if(results.Count > show) GameFont(PxText(box, $"+{results.Count - show}개 더 (가방에 들어갔어요)", 180f, 256f + rowsN * 60f + 6f, 12f, Hex("#e6efff")), false);
            by = 256f + rowsN * 60f + 30f;
        }
        Plate(box, "mg_btn_p2", 55f, by, 120f, 46f, 3f);
        GameFont(PxText(box, "한 번 더", 115f, by + 23f, 16f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Again", 55f, by, 120f, 46f, () => {
            mgSel.Clear();
            foreach(var f in MgPool(mgGrade)) { if(mgSel.Count >= 3) break; mgSel.Add(f); }
            BuildMerge(true);
        });
        Plate(box, "mg_btn_y2", 185f, by, 120f, 46f, 3f);
        GameFont(PxText(box, "확인", 245f, by + 23f, 18f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Ok", 185f, by, 120f, 46f, () => BuildMerge(false));
        StartCoroutine(Twinkle(box, Color.Lerp(Color.white, glow, 0.3f), many));   // 반짝이 (2026-11-26 사용자: 무작위 자리에 계속)
        StartCoroutine(PopIn(popTarget, 1f));
        StartCoroutine(SpinRays(rays.rectTransform));
    }

    // 반짝이: merge_spark_interval 초마다 무작위 자리에 하나 → 0.9초 동안 커졌다 작아지며 돌고 사라져요
    IEnumerator Twinkle(RectTransform box, Color c, bool many)
    {
        float next = 0f;
        while(box != null) {
            next -= Time.unscaledDeltaTime;
            if(next <= 0f) {
                next = Mathf.Max(0.03f, Tuning.Get("merge_spark_interval", 0.12f)) * Random.Range(0.6f, 1.4f);
                float size = Random.Range(12f, 28f);
                float x = Random.Range(24f, 336f - size), y = Random.Range(many ? 250f : 110f, 600f);
                var img = Plate(box, "mg_spark", x, y, size, size, size * 6f / 26f);
                img.color = c;
                StartCoroutine(TwinkleOne(img.rectTransform, Random.Range(0.7f, 1.1f)));
            }
            yield return null;
        }
    }
    IEnumerator TwinkleOne(RectTransform rt, float life)
    {
        float t = 0f, spin = Random.Range(-120f, 120f);
        while(rt != null && t < life) {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            float sc = k < 0.4f ? k / 0.4f : 1f - (k - 0.4f) / 0.6f;
            rt.localScale = Vector3.one * Mathf.Max(0f, sc);
            rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }

    // 빛줄기: 기본은 움직이지 않아요 (2026-11-26 사용자: "뒤에 효과가 자꾸 움직여"). merge_rays_spin > 0 이면 그 속도로 돌아요
    IEnumerator SpinRays(RectTransform rt)
    {
        while(rt != null) {
            float sp = Tuning.Get("merge_rays_spin", 0f);
            if(sp != 0f) rt.Rotate(0f, 0f, -sp * Time.unscaledDeltaTime);
            yield return null;
        }
    }
}
