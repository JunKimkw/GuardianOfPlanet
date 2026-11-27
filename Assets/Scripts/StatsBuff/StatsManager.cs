using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsManager : MonoBehaviour, IDataPersistence
{
    public static StatsManager instance;
    public List<StatsData> statsDatas;
    public TextMeshProUGUI statPointText;
    int[] requiredStats_per_level = { 1, 2, 4, 8, 10, 15, 20, 25, 30, 35, 50, 65, 85, 95, 110, 130, 150, 200, 250, 300, 350, 400, 450, 500, 1000, 2000, 3000, 4000, 5000, 6000, 10000 };
    public int statPoint;
    public int UsedPoint;
    public GameObject Reset_Error;
    public GameObject LackPoint_Error;

    public Button ResetButton;

    [Header("#----Stats Pop Up")]
    [Space(6)]
    public GameObject StatsUpgrade_Popup;
    public TextMeshProUGUI Text_StatsPopup;

    public GameObject ExclamationMark;

    StatsData selectedStatsData;

    private void Start()
    {
        instance = this;

        InvokeRepeating("setStatsPointUI", 0, 0.05f);
        InvokeRepeating("checkExclamationMark", 0, 1f);
    }

    public void checkExclamationMark()
    {
        ExclamationMark.SetActive(false);
        for(int i = 0; i < statsDatas.Count; i ++)
        {
            if(statPoint >= requiredStats_per_level[statsDatas[i]._statsLevel])
                ExclamationMark.SetActive(true);
        }
    }

    public void UpgradeStats(StatsData data)
    {
        if(statPoint >= requiredStats_per_level[data._statsLevel]) {
            selectedStatsData = data;
            Text_StatsPopup.text = "보유 중인 스탯: " + statPoint + "\n" + "필요 스탯: " + requiredStats_per_level[data._statsLevel];
            StatsUpgrade_Popup.SetActive(true);
        } else
            LackPoint_Error.SetActive(true);
    }

    public void popUP_UpgradeStats()
    {
        statPoint -= requiredStats_per_level[selectedStatsData._statsLevel];
        UsedPoint += requiredStats_per_level[selectedStatsData._statsLevel];
        selectedStatsData.UpgradeStat();
        setStatsPointUI();
    }

    // 새 정보 창 (2026-11-28, Remaster/InfoSheetUI.cs): 다음 레벨에 필요한 포인트 (-1 = 최대) · 확인 창 없이 바로 올리기
    public StatsData DataOf(StatsData.TypeofStat t) => statsDatas.Find(x => x != null && x._typeOfStat == t);
    public int NextCost(StatsData.TypeofStat t)
    {
        var d = DataOf(t);
        if(d == null || d._statsLevel >= requiredStats_per_level.Length) return -1;
        return requiredStats_per_level[d._statsLevel];
    }
    public bool UpgradeDirect(StatsData.TypeofStat t)
    {
        var d = DataOf(t);
        int c = NextCost(t);
        if(d == null || c < 0 || statPoint < c) return false;
        statPoint -= c;
        UsedPoint += c;
        d.UpgradeStat();
        setStatsPointUI();
        return true;
    }

    public float getStatAmount(StatsData.TypeofStat data)
    {
        for(int i = 0; i < statsDatas.Count; i++) {
            if(statsDatas[i]._typeOfStat == data)
                return statsDatas[i].getStat();
        }
        return 0;
    }

    public int getStatLevel(StatsData.TypeofStat data)
    {
        for(int i = 0; i < statsDatas.Count; i++) {
            if(statsDatas[i]._typeOfStat == data)
                return statsDatas[i]._statsLevel;
        }
        return 0;
    }

    public void Reset()
    {
        if(GameManager.GetMoney() >= 100000000) {
            GameManager.SetMoney(-100000000);
            statPoint += UsedPoint;
            UsedPoint = 0;
            for(int i = 0; i < statsDatas.Count; i++) {
                statsDatas[i]._statsLevel = 0;            
            }
            setStatsPointUI();
        } else {
            Reset_Error.gameObject.SetActive(true);
        }
    }

    public void setStatsPointUI()
    {
        statPointText.text = statPoint.ToString();

        foreach(StatsData s in statsDatas) {
            s.setStatUI();
        }

        ResetButton.interactable = UsedPoint > 0;
    }

    public void LoadData(GameData data)
    {
        for(int i = 0; i < statsDatas.Count; i++) {
            data.Stats_Level.TryGetValue(statsDatas[i].id, out int value);
            statsDatas[i]._statsLevel = value;
        }

        this.statPoint = data.StatsPoint;
        this.UsedPoint = data.UsedPoint;
    }

    public void SaveData(GameData data)
    {
        for(int i = 0; i < statsDatas.Count; i++) {
            if(data.Stats_Level.ContainsKey(statsDatas[i].id)) {
                data.Stats_Level.Remove(statsDatas[i].id);
            }
            data.Stats_Level.Add(statsDatas[i].id, statsDatas[i]._statsLevel);

        }


        data.StatsPoint = this.statPoint;
        data.UsedPoint = this.UsedPoint;
    }
}
