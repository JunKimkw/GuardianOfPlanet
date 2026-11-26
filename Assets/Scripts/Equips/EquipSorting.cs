using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EquipSorting : MonoBehaviour
{
    public Transform parentObject;

    public List<Equips> allItems;
    public Equips[] LegendaryEquips;
    public Equips[] UniqueEquips;
    public Equips[] EpicEquips;
    public Equips[] RareEquips;
    public Equips[] NormalEquips;

    public List<EquipFrame> Have_Items;

    public string resourcePath;

    private void Awake()
    {
        //반지
        allItems.AddRange(Resources.LoadAll<Equips>("Prefab/Equips/" + resourcePath));
        LegendaryEquips = Resources.LoadAll<Equips>("Prefab/Equips/"+ resourcePath  +"/Legendary");
        UniqueEquips = Resources.LoadAll<Equips>("Prefab/Equips/"+resourcePath+"/Unique");
        EpicEquips = Resources.LoadAll<Equips>("Prefab/Equips/"+resourcePath+"/Epic");
        RareEquips = Resources.LoadAll<Equips>("Prefab/Equips/"+resourcePath+"/Rare");
        NormalEquips = Resources.LoadAll<Equips>("Prefab/Equips/"+resourcePath+"/Normal");
    }
    private void Update()
    {
        for(var i = Have_Items.Count - 1; i > -1; i--) {
            if(Have_Items[i] == null)
                Have_Items.RemoveAt(i);
        }
        SortOrder();
    }

    // 가방 정렬 기준 (2026-11-26 사용자 B안: 탭 아래 칩 다섯 개 + ↓↑). 0 등급 · 1 강화 · 2 잠재력 · 3 세트 · 4 최신. 고른 값은 저장돼요
    public static readonly string[] SortNames = { "등급", "강화", "잠재력", "세트", "최신" };
    public static int Mode { get => PlayerPrefs.GetInt("equip_sort_mode", 0); set => PlayerPrefs.SetInt("equip_sort_mode", value); }
    public static bool Ascending { get => PlayerPrefs.GetInt("equip_sort_asc", 0) == 1; set => PlayerPrefs.SetInt("equip_sort_asc", value ? 1 : 0); }

    // 가방 정렬 (2026-11-25): 등급 높은 순 → 같은 장비끼리 → 강화 높은 순 → 무작위 값 좋은 순 (기본 = 등급)
    public void SortOrder()
    {
        var items = Have_Items.Where(x => x != null && x.equipData != null);
        var all = EquipManager.Instance != null ? EquipManager.Instance.All_Equips : null;
        IOrderedEnumerable<EquipFrame> o;
        switch(Mode) {
            case 1:   // 강화 높은 순 → 등급 → 잠재력
                o = items.OrderByDescending(x => x.equipData.Level).ThenByDescending(x => (int)x.equipData.Grade).ThenByDescending(x => x.equipData.Quality01); break;
            case 2:   // 잠재력 좋은 순 → 등급
                o = items.OrderByDescending(x => x.equipData.Quality01).ThenByDescending(x => (int)x.equipData.Grade); break;
            case 3:   // 세트끼리 (세트 없는 장비는 뒤) → 등급 → 강화
                o = items.OrderBy(x => x.equipData.setType == Equips.SetType.None ? 1 : 0).ThenBy(x => (int)x.equipData.setType)
                    .ThenByDescending(x => (int)x.equipData.Grade).ThenByDescending(x => x.equipData.Level); break;
            case 4:   // 최신 (가방에 늦게 들어온 것부터)
                o = items.OrderByDescending(x => all != null ? all.IndexOf(x) : 0); break;
            default:
                o = items.OrderByDescending(x => (int)x.equipData.Grade).ThenBy(x => x.equipData.id)
                    .ThenByDescending(x => x.equipData.Level).ThenByDescending(x => x.equipData.Quality01); break;
        }
        // 마지막 기준 = 장비 종류·고유 번호 → 순서가 늘 하나로 정해져요 (2026-11-26: 같은 값끼리는 이전 순서를 이어받아서
        //  낮은 순(거꾸로)일 때 매 프레임 자리가 뒤바뀌었어요, 세트 정렬에서 잘 보였어요)
        Have_Items = o.ThenBy(x => x.equipData.id).ThenBy(x => x.equipData.uid).ToList();
        if(Ascending) Have_Items.Reverse();   // 낮은 순 = 거꾸로
        for(int i = 0; i < Have_Items.Count; i++) {
            if(Have_Items[i].transform.GetSiblingIndex() != i) Have_Items[i].transform.SetSiblingIndex(i);
        }
    }

    public List<EquipFrame> setListbyGrade(Equips.MaterialClass clas)
    {
        List<EquipFrame> ret = new List<EquipFrame>();
        for(int i = 0; i < Have_Items.Count; i++) {
            if(Have_Items[i].equipData.Grade == clas) {
                ret.Add(Have_Items[i]);
            }
        }
        ret.Sort(SortByscore);
        return ret;
    }
    static int SortByscore(EquipFrame e1, EquipFrame e2)
    {
        if(e1.equipData.id == e2.equipData.id)
            return -e1.equipData.Level.CompareTo(e2.equipData.Level);

        return 0;
    }

    public List<Equips> getAvailableEquip()
    {
        List<Equips> equipList = new List<Equips>();
        foreach(Equips e in allItems) {
            if(e.AmountOfEquip > 0) {
                equipList.Add(e);
            }
        }
        return equipList;
    }

    public void RemoveFromList(Equips equip)
    {
        for(int i = 0; i < Have_Items.Count; i++) {
            if(Have_Items[i] == equip) {
                Have_Items.RemoveAt(i);
            }
        }
    }
}
