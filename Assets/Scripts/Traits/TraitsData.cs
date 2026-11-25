using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TraitsData : MonoBehaviour
{
    [SerializeField] public string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }
    public enum WeaponType { MainWeapon, DoubleGun, TrackingMissile, SniperGun, Bomber, BounceGun, Poison, CircleLaser, StrLaser, CodeA, Meteor };   // Meteor: 2026-11-21 (특성은 아직 없어요)
    public WeaponType Weapon_Type;
    public enum TraitsType {AttackSpeed, BulletSpeed, DamagePercent}
    public TraitsType Trait_Type;
    public int Trait_Level;

    public float TraitIncreaseAmount;
    public string traitShopName;


    [Header("UI")]
    public TextMeshProUGUI TraitsDataText;
    public TextMeshProUGUI TraitsLevelText;
    public TextMeshProUGUI PriceText;

    public Button priceButton;

    public long[] totalPrice = { 1000, 5000, 10000, 50000, 100000, 500000, 1000000, 5000000, 10000000, 50000000, 100000000, 500000000, 1000000000, 5000000000, 10000000000, 50000000000, 100000000000, 500000000000, 1000000000000, 5000000000000 };

    private void Start()
    {
        SetTraitData();
    }
    public void Upgrade()
    {
        TraitsManager.instance.Upgrade(this);
    }

    private void Update()
    {
        if(Trait_Level < 20) {
            if(GameManager.GetOre() >= totalPrice[Trait_Level])
                priceButton.GetComponent<Image>().color = new Color(0.5299332f, 0.9056604f, 0.3545746f);
            else
                priceButton.GetComponent<Image>().color = Color.white;
        } else {
            priceButton.GetComponent<Image>().color = Color.white;
        }
    }

    public void SetTraitData()
    {
        if(Trait_Level >= 20) {
            TraitsDataText.text = traitShopName.Replace("@", string.Format("{0:0.###}%", Amount * Trait_Level * 100));
            PriceText.text = "MAX";
            TraitsLevelText.text = "MAX";
            priceButton.interactable = false;
        } else {
            TraitsDataText.text = traitShopName.Replace("@", string.Format("{0:0.###}%", Amount * Trait_Level * 100));
            PriceText.text = GameManager.MoneyString(totalPrice[Trait_Level]);
            TraitsLevelText.text = "Lv." + Trait_Level;
            priceButton.interactable = true;
        }
    }

    // 특성 피해% 크기 (2026-11-25 사용자 승인 '3': 무기마다 +400% 는 너무 커요. 성장 예산표에서 trait_dmg_scale 로 줄여요. 나중에 무기 전직으로 바뀌어요)
    float Amount => TraitIncreaseAmount * (Trait_Type == TraitsType.DamagePercent ? Tuning.Get("trait_dmg_scale", 1f) : 1f);

    public float getTraitStat()
    {
        return Trait_Level * Amount;
    }
}
