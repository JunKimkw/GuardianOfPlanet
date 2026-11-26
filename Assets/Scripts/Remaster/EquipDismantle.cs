using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장비 분해 → 강화석 (2026-11-25 사용자 승인 'D', 설계서 v3 P5)
// - 2026-11-25 가방 이후: 장비 하나를 분해해요 (장착 중인 장비는 안 돼요)
//   강화석 = equip_dismantle_per + 강화 레벨 × equip_dismantle_per_level (그 등급 강화석)
// - 합성(같은 장비 N개 → 다음 등급 + 강화석)이 더 이득이라, 합성할 만큼 모였으면 분해 버튼 대신 합성 버튼이 나와요
// - 버튼은 합성 버튼 자리에 (합성 버튼을 복제). 실수 방지: 한 번 누르면 "한 번 더 누르면 분해", 두 번째에 분해
public static class EquipDismantle
{
    static GameObject button;
    static TMP_Text label;
    static EquipFrame armed;   // 한 번 누른 장비

    public static int StonesFor(EquipFrame f) => f == null || f.equipData == null ? 0 :
        Mathf.Max(0, Tuning.GetInt("equip_dismantle_per", 1) + f.equipData.Level * Tuning.GetInt("equip_dismantle_per_level", 1));

    public static UpgradeStone.TypeOfStone StoneOf(Equips.MaterialClass g)
    {
        switch(g) {
            case Equips.MaterialClass.Rare: return UpgradeStone.TypeOfStone.Rare;
            case Equips.MaterialClass.Epic: return UpgradeStone.TypeOfStone.Epic;
            case Equips.MaterialClass.Unique: return UpgradeStone.TypeOfStone.Unique;
            case Equips.MaterialClass.Legendary: return UpgradeStone.TypeOfStone.Legendary;
            case Equips.MaterialClass.Ancient: return UpgradeStone.TypeOfStone.Ancient;
            default: return UpgradeStone.TypeOfStone.Normal;
        }
    }

    // EquipManager.ChangeEquipsUI 에서: 합성 버튼이 안 보일 때만 분해 버튼
    public static void Refresh(GameObject mergeButton, EquipFrame selected)
    {
        if(mergeButton == null) return;
        if(button == null) {
            button = Object.Instantiate(mergeButton, mergeButton.transform.parent);
            button.name = "DismantleButton (remaster)";
            label = button.GetComponentInChildren<TMP_Text>(true);
            if(label != null) {   // 2026-11-26: 버튼 4개(장착·강화·합성·분해)라 좁아요 → 한 줄, 넘치면 자동으로 작게
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * 0.5f;
            }
            var b = button.GetComponent<Button>();
            if(b != null) { b.onClick = new Button.ButtonClickedEvent(); b.onClick.AddListener(OnClick); }
        }
        if(selected != armed) armed = null;
        // 2026-11-26: 합성 버튼과 같이 보여요 (예전엔 합성 자리에 대신 나왔어요). 버튼 줄이 자동 정렬이라 4개로 나눠져요
        bool show = selected != null && selected.equipData != null && selected.equipData.source != null
                    && !EquipBag.IsEquipped(selected) && Tuning.GetInt("equip_dismantle_on", 1) == 1;
        button.SetActive(show);
        if(show && label != null)
            label.text = armed == selected ? "한 번 더 누르면 분해" : $"분해 (강화석 {StonesFor(selected)})";
    }

    public static void Hide() { armed = null; if(button != null) button.SetActive(false); }

    static void OnClick()
    {
        var em = EquipManager.Instance;
        var sel = em != null ? em.SelectedEquip : null;
        if(sel == null || EquipBag.IsEquipped(sel)) return;
        if(armed != sel) { armed = sel; em.ChangeEquipsUI(); return; }
        int stones = StonesFor(sel);
        var grade = sel.equipData.Grade;
        EquipBag.Remove(sel);
        if(stones > 0 && UpgradeStoneManager.instance != null) UpgradeStoneManager.instance.addStone(StoneOf(grade), stones);
        armed = null;
        em.unSelectAfterMerge();   // 선택 정보창 비우기 (합성 뒤와 같은 처리)
        em.ChangeEquipsUI();
    }
}
