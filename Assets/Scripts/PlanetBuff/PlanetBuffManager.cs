using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanetBuffManager : MonoBehaviour, IDataPersistence
{
    public static PlanetBuffManager instance;

    public GameObject BuyTab;

    PlanetBuff current;

    public PlanetBuff[] Buffs;

    public Slider stageIconSlider;
    public Image[] StageIcons;
    public Image[] StageIconInner;

    [Header("#----Error Tab")]
    public GameObject ErrorTab;

    [Header("#----Exclamation Mark")]
    public GameObject ExclamationMark;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        InvokeRepeating("checkLockCover", 0, 0.5f);
    }

    public void checkLockCover()
    {
        for(int i = 1; i < Buffs.Length; i++) {
            Buffs[i].Cover.SetActive(!(Buffs[i - 1].Level >= 5));
        }

        stageIconSlider.maxValue = 70;
        stageIconSlider.value = 0;
        for(int i = 0; i < Buffs.Length; i++) {
            if(Buffs[i].Level >= 5) {
                stageIconSlider.value += 10;
                StageIcons[i].color = new Color(0.07843138f, 0.682353f, 0.9803922f);
                StageIconInner[i].color = new Color(0.2352941f, 1, 1);
            } else if(Buffs[i].Level >= 0 && Buffs[i].Level < 5){
                StageIcons[i].color = new Color(1, 0.8431373f, 0);
                StageIconInner[i].color = new Color(1, 1, 0.4941176f);
                break;
            } else {
                StageIcons[i].color = new Color(0.282353f, 0.2901961f, 0.3960784f);
                StageIconInner[i].color = new Color(0.4862745f, 0.4980392f, 0.6352941f);
                break;
            }
        }

        ExclamationMark.SetActive(false);
        for(int i = 0; i < Buffs.Length; i++) {
            if(!Buffs[i].Cover.activeSelf && !Buffs[i].maxText.activeSelf) {
                if(GameManager.GetParts() >= Buffs[i].getBuffPriceParts()) {
                    ExclamationMark.SetActive(true);
                    break;
                }
            }
        }
    }

    public void OpenBuyTab(PlanetBuff data)
    {
        if(GameManager.GetParts() >= data.getBuffPriceParts()) {
            BuyTab.SetActive(true);
            current = data;
        } else {
            ErrorTab.SetActive(true);
        }
    }

    public void Upgrade()
    {
        current.Upgrade();

        SoundManager.Instance.Invoke("playBuffSFX", SoundManager.Instance.click.length);
    }

    // 경제 버프 (2026-11-25, PlanetBuff.MaxOf 참고)
    static float V(PlanetBuff.BuffType t) => instance != null ? instance.getValue(t) : 0f;
    public static float OfflineBonus => V(PlanetBuff.BuffType.DamageIncrease);
    public static float OfflineExtraHours => V(PlanetBuff.BuffType.AttackSpeedIncrease);
    public static float DustBonus => V(PlanetBuff.BuffType.CritDamageIncrease);
    public static float OreBonus => V(PlanetBuff.BuffType.StunIncrease);
    public static float StoneBonus => V(PlanetBuff.BuffType.CritChanceIncrease);
    public static float TowerTimeCut => Mathf.Clamp(V(PlanetBuff.BuffType.BulletSpeedIncrease), 0f, 0.9f);
    public static long Ore(long n) => n <= 0 ? n : (long)System.Math.Round(n * (1.0 + OreBonus));
    // 작은 수도 의미 있게: 소수점 몫은 확률로
    public static int Stones(int n) { if(n <= 0) return n; float f = n * (1f + StoneBonus); int k = Mathf.FloorToInt(f); return k + (Random.value < f - k ? 1 : 0); }

    public float getValue(PlanetBuff.BuffType type)
    {
        for(int i = 0; i < Buffs.Length; i++) {
            if(Buffs[i].buffType == type) {
                return Buffs[i].value;
            }
        }
        return 0;
    }

    public void LoadData(GameData data)
    {
        for(int i = 0; i < Buffs.Length; i++) {
            data.buffLevel.TryGetValue(Buffs[i].id, out int value);
            Buffs[i].Level = value;
            Buffs[i].ChangeShopSetting();
        }
    }

    public void SaveData(GameData data)
    {
        for(int i = 0; i < Buffs.Length; i++) {
            if(data.buffLevel.ContainsKey(Buffs[i].id))
                data.buffLevel.Remove(Buffs[i].id);
            data.buffLevel.Add(Buffs[i].id, Buffs[i].Level);
        }
    }
}
