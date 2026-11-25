using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanetBuff : MonoBehaviour
{
    [SerializeField] public string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    public enum BuffType { GoldIncrease, EXPIncrease, DamageIncrease, CritChanceIncrease, StunIncrease, AttackSpeedIncrease, BulletSpeedIncrease, CritDamageIncrease };
    public BuffType buffType;

    [Header("#---UI")]
    public Button BuyButton;
    public TextMeshProUGUI BuffInfo;
    public TextMeshProUGUI priceText;
    public GameObject priceImage;
    public GameObject maxText;
    public GameObject Cover;
    public GameObject[] stars;

    public int Level;
    public float MaxValue;

    [HideInInspector]
    public float value;

    public void Upgrade()
    {
        GameManager.SetParts(-getBuffPriceParts());
        if(Level < 10) {
            Level++;
            ChangeShopSetting();
        }
    }

    public long getBuffPriceParts()
    {
        switch(Level) {
            case 0:
                return 1;
            case 1:
                return 3;
            case 2:
                return 5;
            case 3:
                return 10;
            case 4:
                return 20;
            case 5:
                return 50;
            case 6:
                return 75;
            case 7:
                return 100;
            case 8:
                return 300;
            case 9:
                return 500;
            default:
                return 0;
        }
    }

    // 행성 버프 = 경제·편의만 (2026-11-25 사용자 승인 '2'. 설계서 v3 '신의 버프'). 저장 순서 때문에 이름은 그대로, 뜻만 바꿨어요
    //  골드·경험치: 그대로 (씬 MaxValue)
    //  옛 '총 데미지'   → 오프라인 보상 +%         옛 '공격 속도' → 오프라인 최대 시간 +시간
    //  옛 '치명타 데미지' → 환생 별의 먼지 +%      옛 '스턴'     → 광석 획득 +%
    //  옛 '치명타 확률' → 강화석 획득 +%          옛 '총알 속도' → 탐험 시간 단축 %
    public float MaxOf()
    {
        switch(buffType) {
            case BuffType.DamageIncrease: return Tuning.Get("planet_buff_offline_max", 1f);
            case BuffType.AttackSpeedIncrease: return Tuning.Get("planet_buff_offline_hours_max", 6f);
            case BuffType.CritDamageIncrease: return Tuning.Get("planet_buff_dust_max", 0.5f);
            case BuffType.StunIncrease: return Tuning.Get("planet_buff_ore_max", 1f);
            case BuffType.CritChanceIncrease: return Tuning.Get("planet_buff_stone_max", 0.5f);
            case BuffType.BulletSpeedIncrease: return Tuning.Get("planet_buff_tower_cut_max", 0.3f);
            default: return MaxValue;
        }
    }

    public string Describe()
    {
        string v = (value * 100).ToString("0.#");
        switch(buffType) {
            case BuffType.GoldIncrease: return "획득 골드 " + v + "% 증가";
            case BuffType.EXPIncrease: return "획득 경험치 " + v + "% 증가";
            case BuffType.DamageIncrease: return "오프라인 보상 " + v + "% 증가";
            case BuffType.AttackSpeedIncrease: return "오프라인 최대 시간 +" + value.ToString("0.#") + "시간";
            case BuffType.CritDamageIncrease: return "환생 별의 먼지 " + v + "% 증가";
            case BuffType.StunIncrease: return "광석 획득 " + v + "% 증가";
            case BuffType.CritChanceIncrease: return "강화석 획득 " + v + "% 증가";
            case BuffType.BulletSpeedIncrease: return "탐험 시간 " + v + "% 단축";
        }
        return "";
    }

    public void ChangeShopSetting()
    {
        value = MaxOf() / 10.0f * Level;
        BuffInfo.text = Describe();
        bool max = Level >= 10;
        priceText.gameObject.SetActive(!max);
        priceImage.SetActive(!max);
        maxText.SetActive(max);
        if(!max) priceText.text = getBuffPriceParts().ToString();
        BuyButton.interactable = !max;


        //Star
        for(int i = 0; i < stars.Length; i++) {
            stars[i].SetActive(false);
            if(i < Level) {
                stars[i].SetActive(true);
            }
        }

    }
}
