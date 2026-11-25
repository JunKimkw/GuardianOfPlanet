using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class Equips : MonoBehaviour
{
    [SerializeField] public string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    public string EquipName;

    [Header("#-----장비 옵션")]
    [Space(7)]
    public EquipsOption baseOption;
    [HideInInspector]
    public EquipsOption extraOption;
    [HideInInspector]
    public EquipsOption FinalOption;
    // 새 기본 능력치 (2026-11-25 장비 다시 맞추기). 프리팹 baseOption 은 그대로 두고 EquipBalance 가 계산해요
    [System.NonSerialized] options balancedBase;
    public options BaseStats { get { if(balancedBase == null) { balancedBase = EquipBalance.Of(this); ApplyRolls(balancedBase); } return balancedBase; } }

    // 가방 (2026-11-25, Remaster/EquipBag.cs): 가진 장비 하나하나는 이 프리팹의 복사본이에요
    [System.NonSerialized] public Equips source;    // 복사본이면 원본 프리팹 (도감 칸은 null)
    [System.NonSerialized] public string uid;       // 장비 하나의 번호
    [System.NonSerialized] public float[] rolls;    // 능력치마다 무작위 ×1.0~×2.0 (없으면 ×1.5 = 평균)
    public float RollOf(int k) => rolls != null && k < rolls.Length ? rolls[k] : Tuning.Get("equip_roll_mid", 1.5f);
    // 평균이 등급 사다리와 같게 ÷ 1.5
    void ApplyRolls(options o)
    {
        float mid = Mathf.Max(0.01f, Tuning.Get("equip_roll_mid", 1.5f));
        o.damage = (int)(o.damage * RollOf(0) / mid); o.damagePercent *= RollOf(1) / mid; o.AttackSpeed *= RollOf(2) / mid;
        o.BulletSpeed *= RollOf(3) / mid; o.CritChance *= RollOf(4) / mid; o.CritDamage *= RollOf(5) / mid;
        o.StunPercent *= RollOf(6) / mid; o.ExtraEXP *= RollOf(7) / mid; o.ExtraMoney *= RollOf(8) / mid;
    }
    // 무작위 값이 얼마나 좋은가 0 (모두 ×1.0) ~ 1 (모두 ×2.0). 있는 능력치만
    public float Quality01 {
        get {
            var b = BaseStats; float s = 0; int n = 0;
            float[] v = { b.damage, b.damagePercent, b.AttackSpeed, b.BulletSpeed, b.CritChance, b.CritDamage, b.StunPercent, b.ExtraEXP, b.ExtraMoney };
            float lo = Tuning.Get("equip_roll_min", 1f), hi = Tuning.Get("equip_roll_max", 2f);
            for(int k = 0; k < v.Length; k++) if(v[k] != 0) { s += Mathf.InverseLerp(lo, hi, RollOf(k)); n++; }
            return n > 0 ? s / n : 0.5f;
        }
    }

    public enum MaterialClass {Ancient = 5, Legendary = 4, Unique = 3, Epic = 2, Rare = 1, Normal = 0}
    [Header("장비 등급")]
    [Space(7)]
    public MaterialClass Grade;
    public enum Type { Ring, Necklace, Book, Accessory, Relics }
    [Header("장비 종류")]
    [Space(7)]
    public Type TypeOfEquip;

    public enum SetType {None, Zeus_Set, Hades_Set, Beginner_Set, Atena_Set, Demeter_Set, Hydra_Set, Jungle_Set, Wood_Set, Warrior_Set, Hunter_Set, Magician_Set, Sea_Set, Evil_Set, Devil_Set, Dark_Set, Ice_Set, Honor_Set, Blood_Set, Iron_Set, ME_Set, Argos_Set, Gaea_Set, Pan_Set, Gold_Set, Ares_Set, Poseidon_Set, Hercules_Set, Cerberus_Set, Hestia_Set, Aphrodite_Set, Hephaistus_Set, Artemis_Set}
    [Header("장비 세트 효과")]
    [Space(7)]
    public SetType setType;

    public int Level;

    [Header("Item Amount")]
    [Space(10)]
    public int AmountOfEquip;
    public int RequiredAmountForMerge;

    [Header("UIs")]
    public Sprite Sprite_Equip;

    [Header("#-----Upgrade Index")]
    public List<string> UpgradeIndex;

    [HideInInspector]
    public string gradeText;

    public int getUpgradeXP()
    {
        int SP = 0;

        if(Grade == MaterialClass.Normal) {
            switch(Level) {
                case 0: SP = 15; break;
                case 1: SP = 30; break;
                case 2: SP = 50; break;
                case 3: SP = 75; break;
                case 4: SP = 100; break;
                case 5: SP = 125; break;
                case 6: SP = 150; break;
                case 7: SP = 175; break;
                case 8: SP = 200; break;
                case 9: SP = 250; break;
                case 10: SP = 400; break;
                case 11: SP = 450; break;
                case 12: SP = 500; break;
                case 13: SP = 550; break;
                case 14: SP = 600; break;
                case 15: SP = 650; break;
                case 16: SP = 700; break;
                case 17: SP = 800; break;
                case 18: SP = 900; break;
                case 19: SP = 1000; break;
            }
        } else if(Grade == MaterialClass.Rare) {
            switch(Level) {
                case 0: SP = 50; break;
                case 1: SP = 80; break;
                case 2: SP = 110; break;
                case 3: SP = 150; break;
                case 4: SP = 250; break;
                case 5: SP = 350; break;
                case 6: SP = 450; break;
                case 7: SP = 600; break;
                case 8: SP = 800; break;
                case 9: SP = 1000; break;
                case 10: SP = 1300; break;
                case 11: SP = 1600; break;
                case 12: SP = 1900; break;
                case 13: SP = 2200; break;
                case 14: SP = 2500; break;
                case 15: SP = 2800; break;
                case 16: SP = 3000; break;
                case 17: SP = 3500; break;
                case 18: SP = 4000; break;
                case 19: SP = 5000; break;
            }
        } else if(Grade == MaterialClass.Epic) {
            switch(Level) {
                case 0: SP = 150; break;
                case 1: SP = 300; break;
                case 2: SP = 450; break;
                case 3: SP = 600; break;
                case 4: SP = 750; break;
                case 5: SP = 900; break;
                case 6: SP = 1000; break;
                case 7: SP = 1200; break;
                case 8: SP = 1400; break;
                case 9: SP = 1600; break;
                case 10: SP = 3000; break;
                case 11: SP = 3700; break;
                case 12: SP = 4400; break;
                case 13: SP = 5100; break;
                case 14: SP = 5800; break;
                case 15: SP = 6500; break;
                case 16: SP = 7500; break;
                case 17: SP = 8500; break;
                case 18: SP = 10000; break;
                case 19: SP = 15000; break;
            }
        } else if(Grade == MaterialClass.Unique) {
            switch(Level) {
                case 0: SP = 300; break;
                case 1: SP = 600; break;
                case 2: SP = 900; break;
                case 3: SP = 1200; break;
                case 4: SP = 1500; break;
                case 5: SP = 1800; break;
                case 6: SP = 2100; break;
                case 7: SP = 2400; break;
                case 8: SP = 2800; break;
                case 9: SP = 3200; break;
                case 10: SP = 5000; break;
                case 11: SP = 6000; break;
                case 12: SP = 7000; break;
                case 13: SP = 8000; break;
                case 14: SP = 9000; break;
                case 15: SP = 11000; break;
                case 16: SP = 13000; break;
                case 17: SP = 15000; break;
                case 18: SP = 30000; break;
                case 19: SP = 50000; break;
            }
        } else if(Grade == MaterialClass.Legendary) {
            switch(Level) {
                case 0: SP = 1000; break;
                case 1: SP = 1500; break;
                case 2: SP = 2000; break;
                case 3: SP = 2500; break;
                case 4: SP = 3000; break;
                case 5: SP = 3750; break;
                case 6: SP = 4500; break;
                case 7: SP = 5000; break;
                case 8: SP = 5500; break;
                case 9: SP = 6000; break;
                case 10: SP = 7500; break;
                case 11: SP = 9000; break;
                case 12: SP = 11000; break;
                case 13: SP = 13000; break;
                case 14: SP = 15000; break;
                case 15: SP = 17500; break;
                case 16: SP = 20000; break;
                case 17: SP = 25000; break;
                case 18: SP = 50000; break;
                case 19: SP = 100000; break;
            }
        } else if(Grade == MaterialClass.Ancient) {
            switch(Level) {
                case 0: SP = 3000; break;
                case 1: SP = 5000; break;
                case 2: SP = 7000; break;
                case 3: SP = 9000; break;
                case 4: SP = 11000; break;
                case 5: SP = 13000; break;
                case 6: SP = 15000; break;
                case 7: SP = 17000; break;
                case 8: SP = 19000; break;
                case 9: SP = 20000; break;
                case 10: SP = 25000; break;
                case 11: SP = 30000; break;
                case 12: SP = 35000; break;
                case 13: SP = 40000; break;
                case 14: SP = 50000; break;
                case 15: SP = 60000; break;
                case 16: SP = 70000; break;
                case 17: SP = 100000; break;
                case 18: SP = 150000; break;
                case 19: SP = 300000; break;
            }
        }

        return SP;
    }

    public void setEquipInfo()
    {
        switch(Grade) {
            case MaterialClass.Normal:
                gradeText = "일반";
                RequiredAmountForMerge = 30;
                break;
            case MaterialClass.Rare:
                gradeText = "레어";
                RequiredAmountForMerge = 20;
                break;
            case MaterialClass.Epic:
                gradeText = "에픽";
                RequiredAmountForMerge = 15;
                break;
            case MaterialClass.Unique:
                gradeText = "유니크";
                RequiredAmountForMerge = 10;
                break;
            case MaterialClass.Legendary:
                gradeText = "전설";
                RequiredAmountForMerge = 5;
                break;
            case MaterialClass.Ancient:
                gradeText = "고대";
                RequiredAmountForMerge = 0;
                break;
        }
    }


    public string GetEquipType()
    {
        switch(TypeOfEquip) {
            case Type.Ring:
                return "반지";
            case Type.Necklace:
                return "목걸이";
            case Type.Accessory:
                return "장신구";
            case Type.Relics:
                return "펜던트";
            case Type.Book:
                return "고서";
        }
        return null;
    }

    public void setFinalOptionStats()
    {
        //데미지
        FinalOption.option.damage = BaseStats.damage + extraOption.option.damage;
        //데미지 퍼센트
        FinalOption.option.damagePercent = BaseStats.damagePercent + extraOption.option.damagePercent;
        //공격속도
        FinalOption.option.AttackSpeed = BaseStats.AttackSpeed + extraOption.option.AttackSpeed;
        //총알속도
        FinalOption.option.BulletSpeed = BaseStats.BulletSpeed + extraOption.option.BulletSpeed;
        //치명타 확률
        FinalOption.option.CritChance = BaseStats.CritChance + extraOption.option.CritChance;
        //치명타 데미지
        FinalOption.option.CritDamage = BaseStats.CritDamage + extraOption.option.CritDamage;
        //스턴확률
        FinalOption.option.StunPercent = BaseStats.StunPercent + extraOption.option.StunPercent;
        //추가 %경험치
        FinalOption.option.ExtraEXP = BaseStats.ExtraEXP + extraOption.option.ExtraEXP;
        //추가 %골드
        FinalOption.option.ExtraMoney = BaseStats.ExtraMoney + extraOption.option.ExtraMoney;
    }

    public string plusOrminus(float n)
    {
        if(n >= 0) return "+" + n.ToString("0.##");
        else return n.ToString();
    }
    public string plusOrminus(int n)
    {
        if(n >= 0) return "+" + n;
        else return n.ToString();
    }

    public void EquipEarned(int n)
    {
        setEquipInfo();
        AmountOfEquip += n;
    }

    public List<int> getUpgradeIndex()
    {
        List<int> _i = new List<int>();

        string upgradeIndexString = UpgradeIndex[Level - 1];

        foreach(char digitChar in upgradeIndexString) {
            int digit = int.Parse(digitChar.ToString());
            _i.Add(digit);
        }

        return _i;
    }


    public void removeLevelUpStats()
    {
        UpgradeIndex[Level-1] = "";
        Level--;
        setExtraOptionStats();
    }

    public void LevelUpStats(int index)
    {
        UpgradeIndex[Level] += index;
    }

    public void setExtraOptionStats()
    {
        extraOption.option.damage = 0;
        extraOption.option.damagePercent = 0;
        extraOption.option.AttackSpeed = 0;
        extraOption.option.BulletSpeed = 0;
        extraOption.option.CritChance = 0;
        extraOption.option.CritDamage = 0;
        extraOption.option.StunPercent = 0;
        extraOption.option.ExtraMoney = 0;
        extraOption.option.ExtraEXP = 0;

        int count = 0;
        if(BaseStats.damage != 0) count++;
        if(BaseStats.damagePercent != 0) count++;
        if(BaseStats.AttackSpeed != 0) count++;
        if(BaseStats.BulletSpeed != 0) count++;
        if(BaseStats.CritChance != 0) count++;
        if(BaseStats.CritDamage != 0) count++;
        if(BaseStats.StunPercent != 0) count++;
        if(BaseStats.ExtraEXP != 0) count++;
        if(BaseStats.ExtraMoney != 0) count++;

        int number = 0;

        for(int i = 0; i <= Level; i++) {
            if(UpgradeIndex[i] != "" && UpgradeIndex[i] != null) {
                number = int.Parse(UpgradeIndex[i]);

                for(int j = count - 1; j >= 0; j--) {

                    int indexCount = 0;
                    if(BaseStats.damage != 0) {
                        if(indexCount == j) {
                            extraOption.option.damage += (int)((BaseStats.damage + extraOption.option.damage) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.Damage, this, i)[number % 10]);                                            
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.damagePercent != 0) {
                        if(indexCount == j) {
                            extraOption.option.damagePercent += (BaseStats.damagePercent + extraOption.option.damagePercent) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.DamagePercent, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.AttackSpeed != 0) {
                        if(indexCount == j) {
                            extraOption.option.AttackSpeed += (BaseStats.AttackSpeed + extraOption.option.AttackSpeed) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.AttackSpeed, this, i)[number % 10];
                            
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.BulletSpeed != 0) {
                        if(indexCount == j) {
                            extraOption.option.BulletSpeed += (BaseStats.BulletSpeed + extraOption.option.BulletSpeed) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.BulletSpeed, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.CritChance != 0) {
                        if(indexCount == j) {
                            extraOption.option.CritChance += (BaseStats.CritChance + extraOption.option.CritChance) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.CritChance, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.CritDamage != 0) {
                        if(indexCount == j) {
                            extraOption.option.CritDamage += (BaseStats.CritDamage + extraOption.option.CritDamage) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.CritDamage, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.StunPercent != 0) {
                        if(indexCount == j) {
                            extraOption.option.StunPercent += (BaseStats.StunPercent + extraOption.option.StunPercent) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.StunPercent, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.ExtraMoney != 0) {
                        if(indexCount == j) {
                            extraOption.option.ExtraMoney += (BaseStats.ExtraMoney + extraOption.option.ExtraMoney) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.ExtraMoney, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }
                    if(BaseStats.ExtraEXP != 0) {
                        if(indexCount == j) {

                            extraOption.option.ExtraEXP += (BaseStats.ExtraEXP + extraOption.option.ExtraEXP) * EquipManager.Instance.upgradeManager.getStatsPrefab(Upgrade_StatsPrefab.StatsType.ExtraEXP, this, i)[number % 10];
                            number /= 10;
                        }
                        indexCount++;
                    }    
                }
            }
        }
    }
}
