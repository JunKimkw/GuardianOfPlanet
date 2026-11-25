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

    // 가방 정렬 (2026-11-25): 등급 높은 순 → 같은 장비끼리 → 강화 높은 순 → 무작위 값 좋은 순
    public void SortOrder()
    {
        Have_Items = Have_Items.Where(x => x != null && x.equipData != null)
            .OrderByDescending(x => (int)x.equipData.Grade).ThenBy(x => x.equipData.id)
            .ThenByDescending(x => x.equipData.Level).ThenByDescending(x => x.equipData.Quality01).ToList();
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
