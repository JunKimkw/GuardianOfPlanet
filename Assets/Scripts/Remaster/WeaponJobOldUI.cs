using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 원래 '우주선 강화' 창에 무기 전직 붙이기 (2026-11-26 사용자: "직접 만든 원래 강화 창이 좋아요. 거기에 지금 기능을 넣어 줘")
// - job_ui_on = 0 일 때 (기본). 창·줄·+1/+10/+100 은 원래 그대로, 무기 줄마다 아래에 '전직 줄'을 붙여요
// - 무기 줄 아래쪽(패시브 배지 줄)을 새 바닥으로: 윗줄 패시브 알약 7개 · 아랫줄 전직 칸 6개 + 보라 '전직 Lv↑' 버튼 (사용자: 이 부분은 네 입맛대로)
// - 칸 누르기 / 레벨업으로 새 칸이 열리면 고르기 창 (WeaponJobUI.cs 의 BuildPick 을 같이 써요)
public partial class RemasterUI
{
    class JobStrip
    {
        public RectTransform root;
        public WeaponShopBase shop;
        public TMP_Text label, price;
        public Image oreIcon;
        public Image[] pills = new Image[7];
        public TMP_Text[] pillTexts = new TMP_Text[7];
        public RectTransform[] chipRoots = new RectTransform[WeaponJob.Slots];
    }
    readonly Dictionary<WeaponShopBase, JobStrip> jobStrips = new Dictionary<WeaponShopBase, JobStrip>();
    readonly int[] jobLastUnpicked = new int[WeaponJob.WeaponCount];
    bool jobOldWasOpen;
    float jobOldTimer;
    bool jobPickOldMode;


    void UpdateOldUpgradeJob()
    {
        bool open = upgradeTab != null && upgradeTab.activeInHierarchy && Tuning.Get("job_ui_on", 0f) <= 0f;
        var wm = WeaponManager.instance;
        if(!open || wm == null) { jobOldWasOpen = false; return; }
        for(int i = 0; i < wm.Shops.Count && i < wm.Weapons.Count; i++) {
            var shop = wm.Shops[i];
            if(shop == null || wm.Weapons[i] == null || jobStrips.ContainsKey(shop)) continue;
            var st = MakeJobStrip(shop, wm.Weapons[i].JobType);
            jobStrips[shop] = st;
        }
        // 새로 열린 칸 → 바로 고르기 (창을 연 순간 이미 있던 칸은 빛나는 칸으로만)
        for(int w = 0; w < WeaponJob.WeaponCount; w++) {
            int n = WeaponJob.OpenUnpicked((TraitsData.WeaponType)w);
            if(jobOldWasOpen && wjPopSlot < 0 && n > jobLastUnpicked[w]) OpenPickOld((TraitsData.WeaponType)w, FirstUnpicked((TraitsData.WeaponType)w));
            jobLastUnpicked[w] = n;
        }
        jobOldWasOpen = true;
        jobOldTimer -= Time.unscaledDeltaTime;
        if(jobOldTimer <= 0f) {
            jobOldTimer = 0.3f;
            for(int i = 0; i < wm.Shops.Count && i < wm.Weapons.Count; i++)
                if(wm.Shops[i] != null && jobStrips.TryGetValue(wm.Shops[i], out var st) && st.root != null) RefreshJobStrip(st, wm.Weapons[i].JobType, wm.Shops[i]);
        }
    }

    // 원래 줄의 아래쪽(패시브 배지 줄)을 숨기고 새 '바닥'을 붙여요 (한 번만) — 미리보기 previews/job8.png (job_preview8.js)
    //  줄 1019 단위 = 미리보기 340px (1px = 3단위). 위쪽 78px 는 원래 그대로, 바닥 = 왼쪽 6 · 위 78 · 328 × 92, 줄 높이 178px
    JobStrip MakeJobStrip(WeaponShopBase shop, TraitsData.WeaponType w)
    {
        var st = new JobStrip();
        var row = shop.transform as RectTransform;
        float oldH = row.sizeDelta.y;
        var passive = row.Find("GUI - Passive");
        float passiveH = passive is RectTransform prt0 ? prt0.sizeDelta.y : 139.7f;
        float newH = 202f * K, add = newH - oldH;   // 9차 A안: 바닥 116 (작은 제목 두 줄)
        foreach(Transform c in row) {   // 위쪽 것들은 위 끝과의 거리를 그대로 (가운데 기준이라 반만큼 위로). 줄 전체를 덮는 잠금 덮개는 그대로
            var rt = c as RectTransform;
            if(rt == null || (rt.anchorMin.y <= 0f && rt.anchorMax.y >= 1f)) continue;
            rt.anchoredPosition += new Vector2(0f, add * 0.5f);
        }
        if(passive != null) passive.gameObject.SetActive(false);   // 패시브 배지 줄은 새 바닥의 알약으로
        row.sizeDelta = new Vector2(row.sizeDelta.x, newH);
        var le = row.GetComponent<LayoutElement>();
        if(le != null) { le.preferredHeight = newH; le.minHeight = Mathf.Max(le.minHeight, newH); }

        var floor = PxBox("JobFloor (remaster)", row, 6f, 78f, 328f, 116f);
        st.root = floor;
        Plate(floor, "jo_floor", 0f, 0f, 328f, 116f, 0f);
        // 작은 제목 + 한 줄 설명 (2026-11-26 사용자 9차 A안: 처음 보는 사람도 알게)
        JobCaption(floor, 12f, "패시브", "무기 레벨이 오르면 자동으로 열려요");
        JobCaption(floor, 62f, "개조", "칸을 눌러 무기를 내 식대로 골라요");
        // 윗줄: 패시브 알약 7개 (4 + 46 × i, 6, 40 × 24). 누르면 그 패시브 설명
        for(int i = 0; i < PassiveLevels.Length; i++) {
            int ii = i;
            float x = 4f + i * 46f;
            st.pills[i] = Plate(floor, "jo_pill_off", x, 22f, 40f, 24f, 0f);
            st.pillTexts[i] = PxText(floor, $"Lv.{PassiveLevels[i]}", x + 20f, 34f, 10.5f, Hex("#8a97b8"));
            var wp = w;
            JobHit(floor, "Pill" + i, x, 22f, 40f, 24f, () => ShowPassiveInfo(shop, wp, ii, true));
        }
        // 아랫줄: 전직 칸 6개 (4 + 38 × k, 44, 34 × 34) + 보라 버튼 (240, 42, 84 × 40)
        var ww = w;
        for(int k = 0; k < WeaponJob.Slots; k++) {
            int kk = k;
            float x = 4f + k * 38f;
            st.chipRoots[k] = PxBox("Chip" + k, floor, 0f, 0f, 1f, 1f);
            JobHit(floor, "Slot" + k, x, 72f, 34f, 34f, () => {
                if(!WeaponJob.SlotOpen(ww, kk)) { Toast($"무기 Lv.{WeaponJob.SlotLevel[kk]} 에 열려요", new Color(1f, 0.85f, 0.5f)); return; }
                OpenPickOld(ww, kk);
            });
        }
        Plate(floor, "jo_btn", 240f, 70f, 84f, 40f, 3f);
        st.label = BtnText(floor, "", 282f, 83f, 10f, edge: OnPurple);
        st.price = BtnText(floor, "", 288f, 98f, 12.5f, edge: OnPurple);
        st.oreIcon = PxIcon(floor, "OreGem", 260f, 98f, 13f);   // 광석 = 게임 화면 위 회색 보석 (GradeIcon_Gem_Off). "Ore" 그림은 강화석이에요
        JobHit(floor, "JobUp", 240f, 70f, 84f, 40f, () => {
            if(!WeaponJob.TryLevelUp(ww)) {
                long need = WeaponJob.LevelUpCost(ww);
                if(need >= 0) ShowOreShort(need, "개조 레벨을 올리려면");
                else Toast(WeaponJob.Level(ww) >= WeaponJob.MaxLevel ? "개조 레벨이 최대예요" : $"무기 Lv.{WeaponJob.SlotLevel[0]} 에 개조가 열려요", new Color(1f, 0.85f, 0.5f));
            }
            if(jobStrips.TryGetValue(shop, out var s2)) RefreshJobStrip(s2, ww, shop);
        });
        st.shop = shop;
        RefreshJobStrip(st, w, shop);
        LayoutRebuilder.MarkLayoutForRebuild(row.parent as RectTransform);
        return st;
    }

    static readonly int[] PassiveLevels = { 10, 50, 100, 200, 300, 500, 750 };

    // ---- 패시브 설명 창 (2026-11-26 사용자 B안: previews/passive_tip.png = tools/ui_preview/passive_preview.js)
    // 가운데 창에 그 무기 패시브 7개 목록, 누른 것은 노란 줄. 좌표 = measure_preview.js 로 잰 미리보기 px (폰 600 높이 → 화면 가운데로 dy)
    // 줄을 누르면 그 줄을 강조해요. 바깥·확인을 누르면 닫혀요. 설명 글은 WeaponShopBase.PassiveText (지금 숫자)
    void ShowPassiveInfo(WeaponShopBase shop, TraitsData.WeaponType w, int sel, bool pop)
    {
        if(shop == null || !EnsureBuilt()) return;
        MapFont();
        var old = modalRoot.Find("PassiveInfo");
        if(old != null) Destroy(old.gameObject);
        var layer = Stretch("PassiveInfo", modalRoot);
        layer.SetAsLastSibling();
        var dim = Stretch("Dim", layer);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(() => { if(layer != null) Destroy(layer.gameObject); });
        var box = Rect("Box", layer, new Vector2(1080, 780 * K), Vector2.zero);
        const float dy = 76f;   // 미리보기 창 가운데(96 + 436/2 = 314) → 780 틀 가운데(390)
        int lv = shop.Weapon != null ? shop.Weapon.UpgradeLevel : 0;

        Plate(box, "pt_win", 20f, 96f + dy, 320f, 436f, 7f).raycastTarget = true;   // 판을 눌러도 닫히지 않게
        Plate(box, "pt_badge", 150f, 69f + dy, 60f, 60f, 0f);
        // 배지 안 = 그 무기 그림 (무기 줄의 GUI - Image/WeaponImage). 없으면 칼 아이콘
        var wi = shop.transform.Find("GUI - Image/WeaponImage");
        var wImg = wi != null ? wi.GetComponent<Image>() : null;
        if(wImg != null && wImg.sprite != null) {
            var irt = PxAt("WeaponIcon", box, 180f, 99f + dy, 40f, 40f);
            var im = irt.gameObject.AddComponent<Image>();
            im.sprite = wImg.sprite; im.preserveAspect = true; im.raycastTarget = false;
        } else PxIcon(box, "ItemIcon_Battle", 180f, 99f + dy, 40f);
        GameFont(PxText(box, $"{WeaponJob.WeaponName(w)} 패시브", 180f, 145f + dy, 15f, Color.white, 1f, Navy), true);
        GameFont(PxText(box, $"무기 레벨이 오르면 자동으로 열려요 · 지금 Lv.{lv}", 180f, 169.5f + dy, 10.5f, Hex("#e6efff")), false);

        for(int i = 0; i < PassiveLevels.Length; i++) {
            int ii = i, need = PassiveLevels[i];
            bool on = lv >= need, s = i == sel;
            float y = 185f + 40f * i + dy;
            Plate(box, s ? "pt_row_sel" : on ? "pt_row_on" : "pt_row_off", 37f, y, 286f, 36f, 0f);
            Plate(box, on ? "pt_lv_on" : "pt_lv_off", 45f, y + 7f, 46f, 22f, 0f);
            var lt = PxText(box, $"Lv.{need}", 68f, y + 18f, 9.5f, on ? Color.white : Hex("#8a97b8"));
            if(on) StyleBtnText(lt, 9.5f, OnGreen);
            // 글: 고른 줄 = 진한 갈색, 열린 줄 = 흰 글씨 + 검은 테두리, 잠긴 줄 = 흐린 파랑
            bool edge = on && !s;
            var tt = PxText(box, shop.PassiveText(i), 99f, y + 17.5f, 11.5f, s ? Hex("#3a2000") : on ? Color.white : Hex("#9fb0d0"), edge ? 1f : 0f, Navy, TextAlignmentOptions.Left);
            GameFont(tt, edge);
            // 아이콘 = 게임에서 쓰는 체크(Icon_Check03_l)·자물쇠(Icon_Lock03) (2026-11-26 사용자)
            PxIcon(box, on ? "Icon_Check03_l" : "Icon_Lock03", 303f, y + 18f, 20f);
            JobHit(box, "Row" + i, 37f, y, 286f, 36f, () => ShowPassiveInfo(shop, w, ii, false));
        }
        // 확인 (미리보기는 창 안쪽 기준이라 3px 오른쪽으로 치우쳐 있었어요 → 가운데로)
        Plate(box, "pt_ok", 110f, 469f + dy, 140f, 42f, 4f);
        GameFont(PxText(box, "확인", 180f, 490f + dy, 15f, Color.white, 1f, Navy), true);
        JobHit(box, "Ok", 110f, 469f + dy, 140f, 42f, () => { if(layer != null) Destroy(layer.gameObject); });
        if(pop) StartCoroutine(PopIn(box, 1f));
    }

    // 바닥 안 작은 제목: "패시브" (11, 외곽선) + 설명 (9.5, 연한 하늘). cy = 글자 가운데
    void JobCaption(Transform floor, float cy, string title, string sub)
    {
        // 원래 게임 한글 글꼴: 제목 = 검은 테두리, 설명 = 테두리 없이
        var tt = GameFont(PxText(floor, title, 6f, cy, 11f, Color.white, 1f, Navy, TextAlignmentOptions.Left), true);
        float tw = tt.GetPreferredValues(title).x / K;
        GameFont(PxText(floor, sub, 6f + tw + 5f, cy + 0.5f, 9.5f, Hex("#dfe8ff"), 0f, null, TextAlignmentOptions.Left), false);
    }

    void RefreshJobStrip(JobStrip st, TraitsData.WeaponType w, WeaponShopBase shop = null)
    {
        if(shop == null) shop = st.shop;   // ?? 는 Unity 오브젝트에 쓰지 않아요
        int jl = WeaponJob.Level(w), wlv = WeaponJob.WeaponLevel(w);
        // 패시브: 원래 배지의 덮개가 꺼졌으면 열린 것 (없으면 무기 레벨로)
        for(int i = 0; i < PassiveLevels.Length; i++) {
            bool on = wlv >= PassiveLevels[i];
            if(shop != null && shop.passiveObjects != null && i < shop.passiveObjects.Count && shop.passiveObjects[i] != null && shop.passiveObjects[i].PassiveCover != null)
                on = !shop.passiveObjects[i].PassiveCover.activeSelf;
            if(st.pills[i] != null) st.pills[i].sprite = MapSprite(on ? "jo_pill_on" : "jo_pill_off");
            if(st.pillTexts[i] != null) {
                st.pillTexts[i].color = on ? Color.white : Hex("#8a97b8");
                // 열린 알약 = 베이글 흰 글씨 + 진한 초록 테두리·그림자, 잠긴 알약 = 주아 회색
                if(on) StyleBtnText(st.pillTexts[i], 10.5f, OnGreen);
                else { st.pillTexts[i].font = MapFont(); st.pillTexts[i].fontSize = 10.5f * K; st.pillTexts[i].fontSharedMaterial = TextMat(Navy, 0f, false, 0f); st.pillTexts[i].color = Hex("#8a97b8"); }
            }
        }
        // 전직 칸: 그 칸 자리를 지우고 다시 그려요 (고름 = 노랑+그림, 고를 수 있음 = 보라+?, 잠김 = 어두움+자물쇠)
        for(int k = 0; k < WeaponJob.Slots; k++) {
            var root = st.chipRoots[k];
            if(root == null) continue;
            ClearKids(root);
            float x = 4f + k * 38f;
            bool open = WeaponJob.SlotOpen(w, k);
            int p = WeaponJob.Pick(w, k);
            // root 는 바닥 왼쪽 위 1×1 → 바닥 좌표 그대로 쓰려고 부모를 바닥으로
            var floor = st.root;
            if(!open) { Plate(root, "jo_chip_off", x, 72f, 34f, 34f, 0f); var l = PxIcon(root, "Icon_Lock03", x + 17f, 89f, 14f); l.color = new Color(1, 1, 1, 0.5f); }
            else if(p >= 0) { Plate(root, "jo_chip_on", x, 72f, 34f, 34f, 0f); PxIcon(root, JobIcon(w, k, p), x + 17f, 89f, 30f); }
            else { Plate(root, "jo_chip_q", x, 72f, 34f, 34f, 8f); PxText(root, "?", x + 17f, 89f, 15f, Color.white, 1f, Hex("#2a0a50")); }
        }
        long cost = WeaponJob.LevelUpCost(w);
        // 화살표 ↑ 는 원래 글꼴에 없어서 뺐어요 (2026-11-26 사용자)
        if(st.label != null) { st.label.text = $"개조 Lv.{jl}"; StyleBtnText(st.label, 10f, OnPurple); }
        string pr = cost < 0 ? (jl >= WeaponJob.MaxLevel ? "MAX" : "무기 Lv.50") : GameManager.MoneyString(cost);
        if(st.price != null) {
            st.price.text = pr;
            StyleBtnText(st.price, 12.5f, OnPurple);
            float tw = st.price.GetPreferredValues(pr).x / K;   // 베이글 글꼴 실제 폭
            bool icon = cost >= 0;
            float total = (icon ? 13f + 3f : 0f) + tw;
            float left = 282f - total * 0.5f;
            st.price.rectTransform.anchoredPosition = new Vector2((left + (icon ? 16f : 0f) + tw * 0.5f) * K, st.price.rectTransform.anchoredPosition.y);
            if(st.oreIcon != null) { st.oreIcon.enabled = icon; st.oreIcon.rectTransform.anchoredPosition = new Vector2((left + 6.5f) * K, st.oreIcon.rectTransform.anchoredPosition.y); }
        }
    }

    // 고르기 창 (WeaponJobUI 의 BuildPick). 원래 창 모드에선 판·어둡게는 빼고 고르기 창만
    void OpenPickOld(TraitsData.WeaponType w, int slot)
    {
        if(!EnsureBuilt()) return;
        jobPickOldMode = true;
        OpenPick(w, slot, true);   // 고르기 창은 맨 위 레이어 (Modals)
    }
    // 고르기 창을 닫을 때 (원래 창 모드면 틀도 같이 꺼요)
    void AfterPickClosedOld()
    {
        if(!jobPickOldMode || wjPopSlot >= 0) return;
        jobPickOldMode = false;
        var wm = WeaponManager.instance;
        if(wm != null) for(int i = 0; i < wm.Shops.Count && i < wm.Weapons.Count; i++)
            if(wm.Shops[i] != null && jobStrips.TryGetValue(wm.Shops[i], out var st) && st.root != null) RefreshJobStrip(st, wm.Weapons[i].JobType, wm.Shops[i]);
    }
}
