using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장비 분해 → 강화석 (2026-11-25 사용자 승인 'D', 설계서 v3 P5)
// - 같은 장비가 2개 이상이면 1개만 남기고 나머지를 그 등급 강화석으로 바꿔요 (개당 equip_dismantle_per 개)
// - 합성(같은 장비 N개 → 다음 등급 + 강화석)보다 손해라서, 합성할 만큼 모였으면 분해 버튼 대신 합성 버튼이 나와요
// - 버튼은 합성 버튼 자리에 (합성 버튼을 복제). 실수 방지: 한 번 누르면 "한 번 더 누르면 분해", 두 번째에 분해
public static class EquipDismantle
{
    static GameObject button;
    static TMP_Text label;
    static EquipFrame armed;   // 한 번 누른 장비

    static int Spare(EquipFrame f) => f == null || f.equipData == null ? 0 : Mathf.Max(0, f.equipData.AmountOfEquip - 1);
    public static int StonesFor(EquipFrame f) => Spare(f) * Mathf.Max(0, Tuning.GetInt("equip_dismantle_per", 1));

    static UpgradeStone.TypeOfStone StoneOf(Equips.MaterialClass g)
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
            var b = button.GetComponent<Button>();
            if(b != null) { b.onClick = new Button.ButtonClickedEvent(); b.onClick.AddListener(OnClick); }
        }
        if(selected != armed) armed = null;
        bool show = selected != null && !mergeButton.activeSelf && Spare(selected) > 0 && Tuning.GetInt("equip_dismantle_on", 1) == 1;
        button.SetActive(show);
        if(show && label != null)
            label.text = armed == selected ? "한 번 더 누르면 분해" : $"분해 ({Spare(selected)}개 → 강화석 {StonesFor(selected)})";
    }

    public static void Hide() { armed = null; if(button != null) button.SetActive(false); }

    static void OnClick()
    {
        var em = EquipManager.Instance;
        var sel = em != null ? em.SelectedEquip : null;
        if(sel == null || Spare(sel) <= 0) return;
        if(armed != sel) { armed = sel; em.ChangeEquipsUI(); return; }
        int stones = StonesFor(sel);
        sel.equipData.AmountOfEquip = 1;
        if(stones > 0 && UpgradeStoneManager.instance != null) UpgradeStoneManager.instance.addStone(StoneOf(sel.equipData.Grade), stones);
        armed = null;
        em.ChangeEquipsUI();
    }
}
