using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipManager : MonoBehaviour, IDataPersistence
{
    public static EquipManager Instance;

    public EquipUpgradeManager upgradeManager;

    public EquipFrame equipFrame_pfb;

    [Header("아이템 등급별")]
    public EquipSorting Sorting_Ring;
    public EquipSorting Sorting_Necklace;
    public EquipSorting Sorting_Book;
    public EquipSorting Sorting_Accessory;
    public EquipSorting Sorting_Relics;
    [HideInInspector]
    public List<EquipFrame> All_Equips;

    [HideInInspector]
    public EquipFrame SelectedEquip;
    [HideInInspector]
    public UpgradeStone SelectedStone;

    [Header("정보창")]
    [Space(5)]
    public Image Background;
    public Image Background_BG;
    public Image Border;
    public Image Light;
    public Image Glow;
    public Image TypeBorder;
    public Image Type;
    public Image EquipImage;
    public TextMeshProUGUI LevelText;
    public TextMeshProUGUI GradeText;
    public List<GameObject> equipStats_obj;
    public TextMeshProUGUI EquipNameText;

    //버튼
    public GameObject EquipButton;
    public GameObject unEquipButton;
    public GameObject UpgradeButton;
    public GameObject JinhwaButton;
    bool mergeButtonHooked;

    [Header("star prefab")]
    public Transform StarParent;
    public Image Star;
    public List<Image> star_List;

    [Header("Set")]
    public SetScript prefab_Set;
    public Transform SetParent;

    [HideInInspector]
    public List<SetScript> setDatas;

    [Header("#----1세트2세트")]
    public int CurrentSet;
    public List<EquipSet> Equip_Sets;
    public TabGroup tabGroup;
    public List<TabButton> Buttons;

    public Sprite[] EquipType_Sprites;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Background.gameObject.SetActive(false);
        Background_BG.gameObject.SetActive(false);
        Glow.gameObject.SetActive(false);
        Border.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
        TypeBorder.gameObject.SetActive(false);
        EquipImage.gameObject.SetActive(false);
        GradeText.gameObject.SetActive(false);
        LevelText.gameObject.SetActive(false);
        EquipNameText.gameObject.SetActive(false);
        EquipSets.Hide();
        Type.gameObject.SetActive(false);
        foreach(GameObject g in equipStats_obj) {
            g.SetActive(false);
        }

        createAllItems();
        CreateStar();
    }

    private void Update()
    {
#if UNITY_EDITOR
        // 개발용 x: 모든 장비 종류를 하나씩 (2026-11-25 가방: 예전엔 출시 빌드에서도 켜져 있었어요)
        // (2026-11-26 사용자: 장비가 없으면 확인을 못 해요 → 가진 장비 말고 도감(Catalog)에서 하나씩, 가방 칸 수는 안 따져요)
        if(Input.GetKeyDown("x")) {
            int n = 0;
            foreach(EquipFrame e in EquipBag.Catalog.ToArray()) if(e != null && EquipBag.Grant(e) != null) n++;
            Debug.Log($"[개발] 장비 {n}개 받음 (종류마다 하나, 무작위 잠재력)");
        }
#endif

        ChangeEquipsUI();
        TightenGrids();
    }

    // 장비 칸 격자 (2026-11-26 사용자: 장비 크기를 좀 줄이고 장비 사이 빈칸을 줄여 줘)
    // 장비 탭 격자(RingTab·NecklaceTab·AccessoryTab·RelicsTab·BookTab: 칸 185×192, 사이 25×75, 5줄)
    //  → 격자 전체를 equip_grid_scale 배로 줄이고 한 줄 equip_grid_cols 개, 사이 equip_grid_gap_x × equip_grid_gap_y
    //  세로 사이 75 는 칸 밖으로 튀어나온 잠재력 바 자리였는데 바를 숨겨서 줄여요. 칸 안 그림은 크기가 고정이라 칸 대신 격자를 줄여요
    //  tuning 은 장비 창이 열려 있으면 바로 먹어요
    List<GridLayoutGroup> equipGrids;
    void TightenGrids()
    {
        if(equipGrids == null) {
            equipGrids = new List<GridLayoutGroup>();
            foreach(var g in FindObjectsByType<GridLayoutGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if(g != null && g.name.EndsWith("Tab") && Mathf.Abs(g.cellSize.x - 185f) < 1f && Mathf.Abs(g.cellSize.y - 192f) < 1f) equipGrids.Add(g);
        }
        float s = Tuning.Get("equip_grid_scale", 0.85f);
        int cols = Mathf.Max(1, Tuning.GetInt("equip_grid_cols", 6));
        var gap = new Vector2(Tuning.Get("equip_grid_gap_x", 20f), Tuning.Get("equip_grid_gap_y", 22f));
        var sc = new Vector3(s, s, 1f);
        foreach(var g in equipGrids) {
            if(g == null) continue;
            if(g.constraintCount != cols) g.constraintCount = cols;
            if(g.spacing != gap) g.spacing = gap;
            if(g.transform.localScale != sc) g.transform.localScale = sc;
        }
    }

    //Create Methods
    public void createAllItems()
    {
        //반지
        for(int i = 0; i < Sorting_Ring.allItems.Count; i++) {
            CreateCatalog(Sorting_Ring.allItems[i]);
        }

        //목걸이
        for(int i = 0; i < Sorting_Necklace.allItems.Count; i++) {
            CreateCatalog(Sorting_Necklace.allItems[i]);
        }

        //책
        for(int i = 0; i < Sorting_Book.allItems.Count; i++) {
            CreateCatalog(Sorting_Book.allItems[i]);
        }

        //장신구
        for(int i = 0; i < Sorting_Accessory.allItems.Count; i++) {
            CreateCatalog(Sorting_Accessory.allItems[i]);
        }

        //장신구
        for(int i = 0; i < Sorting_Relics.allItems.Count; i++) {
            CreateCatalog(Sorting_Relics.allItems[i]);
        }
    }
    // 도감 칸 (뽑기·합성 미리보기용, 가방엔 안 나와요) — 2026-11-25 가방
    public EquipFrame CreateCatalog(Equips equip)
    {
        EquipFrame e = Instantiate(equipFrame_pfb, EquipBag.Holder);
        e.equipData = equip;
        equip.setEquipInfo();
        int t = equip.TypeOfEquip == Equips.Type.Ring ? 0 : equip.TypeOfEquip == Equips.Type.Necklace ? 1 : equip.TypeOfEquip == Equips.Type.Relics ? 2 : equip.TypeOfEquip == Equips.Type.Accessory ? 3 : 4;
        e.setEquipFrameUI(equip, EquipType_Sprites[t]);
        e.name = "[도감] " + equip.EquipName + equip.Grade;
        EquipBag.Catalog.Add(e);
        return e;
    }

    // 가방 칸 하나 (장비 하나) — EquipBag.Create 가 불러요
    public EquipFrame CreateEquip(Equips equip)
    {
        EquipFrame e = Instantiate(equipFrame_pfb);
        e.equipData = equip;
        e.equipData.setEquipInfo();
        if(equip.TypeOfEquip == Equips.Type.Ring) {
            e.setEquipFrameUI(equip, EquipType_Sprites[0]);
            Sorting_Ring.Have_Items.Add(e);
            e.transform.SetParent(Sorting_Ring.parentObject);
        } else if(equip.TypeOfEquip == Equips.Type.Necklace) {
            e.setEquipFrameUI(equip, EquipType_Sprites[1]);
            Sorting_Necklace.Have_Items.Add(e);
            e.transform.SetParent(Sorting_Necklace.parentObject);
        } else if(equip.TypeOfEquip == Equips.Type.Book) {
            e.setEquipFrameUI(equip, EquipType_Sprites[4]);
            Sorting_Book.Have_Items.Add(e);
            e.transform.SetParent(Sorting_Book.parentObject);
        } else if(equip.TypeOfEquip == Equips.Type.Accessory) {
            e.setEquipFrameUI(equip, EquipType_Sprites[3]);
            Sorting_Accessory.Have_Items.Add(e);
            e.transform.SetParent(Sorting_Accessory.parentObject);
        } else if(equip.TypeOfEquip == Equips.Type.Relics) {
            e.setEquipFrameUI(equip, EquipType_Sprites[2]);
            Sorting_Relics.Have_Items.Add(e);
            e.transform.SetParent(Sorting_Relics.parentObject);
        }
        All_Equips.Add(e);
        e.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
        e.name = e.equipData.EquipName + e.equipData.Grade;
        return e;
    }

    public void setEquipAvailablilty()
    {
        //반지
        for(int i = 0; i < Sorting_Ring.Have_Items.Count; i++) {
            Sorting_Ring.Have_Items[i].gameObject.SetActive(Sorting_Ring.Have_Items[i].equipData.AmountOfEquip > 0);
        }

        //목걸이
        for(int i = 0; i < Sorting_Necklace.Have_Items.Count; i++) {
            Sorting_Necklace.Have_Items[i].gameObject.SetActive(Sorting_Necklace.Have_Items[i].equipData.AmountOfEquip > 0);
        }

        //유물
        for(int i = 0; i < Sorting_Relics.Have_Items.Count; i++) {
            Sorting_Relics.Have_Items[i].gameObject.SetActive(Sorting_Relics.Have_Items[i].equipData.AmountOfEquip > 0);
        }

        //장신구
        for(int i = 0; i < Sorting_Accessory.Have_Items.Count; i++) {
            Sorting_Accessory.Have_Items[i].gameObject.SetActive(Sorting_Accessory.Have_Items[i].equipData.AmountOfEquip > 0);
        }

        //책
        for(int i = 0; i < Sorting_Book.Have_Items.Count; i++) {
            Sorting_Book.Have_Items[i].gameObject.SetActive(Sorting_Book.Have_Items[i].equipData.AmountOfEquip > 0);
        }
    }

    public void CreateStar()
    {
        for(int i = 0; i < 10; i++) {
            Image _star = Instantiate(Star, StarParent);
            star_List.Add(_star);
            star_List[i].color = new Color(0, 0, 0, 0.6f);
        }
    }
    /*
    public void CreateSetInformation()
    {
        for(int i = 1; i < System.Enum.GetValues(typeof(Equips.SetType)).Length; i++) {
            SetScript _s = Instantiate(prefab_Set, SetParent);
            setDatas.Add(_s);
        }
    }
    */
    public void ReorderSets()
    {
        setDatas.Sort((a, b) => {
            int ancientComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Ancient).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Ancient));

            if(ancientComparison != 0) {
                return ancientComparison;
            }

            int legendaryComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Legendary).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Legendary));

            if(legendaryComparison != 0) {
                return legendaryComparison;
            }

            int uniqueComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Unique).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Unique));

            if(uniqueComparison != 0) {
                return uniqueComparison;
            }

            int epicComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Epic).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Epic));

            if(epicComparison != 0) {
                return epicComparison;
            }

            int rareComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Rare).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Rare));

            if(rareComparison != 0) {
                return rareComparison;
            }

            int normalComparison = a.getNumberOfGradeEquip(Equips.MaterialClass.Normal).CompareTo(b.getNumberOfGradeEquip(Equips.MaterialClass.Normal));

            return normalComparison;
        });

        for(int i = 0; i < setDatas.Count; i++) {
            setDatas[i].gameObject.transform.SetSiblingIndex(i);
        }
    }
    public void ChangeStarSetting(int n)
    {
        for(int i = 0; i < 10 + n; i++) {
            if(i < 10) {
                star_List[i].color = new Color(0, 0, 0, 0.6f);
            } else if(i >= 10 && i < 20) {
                star_List[i - 10].color = new Color(1, 1, 1, 1f);
            } else {
                star_List[i - 20].color = new Color(1, 0, 0.085289f);
            }
        }
    }

    //Equip Related
    public void UpgradeEquip(Equips equip, int n)
    {
        for(int i = 0; i < All_Equips.Count; i++) {
            if(equip.id == All_Equips[i].equipData.id) {
                All_Equips[i].equipData.EquipEarned(n);
            }
        }

        ChangeEquipsUI();
    }
    public void ChangeEquipsUI()
    {
        //UI
        if(SelectedEquip != null) {
            unEquipButton.SetActive(SelectedEquip == EquipingManager.Instance.Ring.equipment || SelectedEquip == EquipingManager.Instance.Necklace.equipment || SelectedEquip == EquipingManager.Instance.Book.equipment || SelectedEquip == EquipingManager.Instance.Accessory.equipment || SelectedEquip == EquipingManager.Instance.Relics.equipment);
            EquipButton.SetActive(!(SelectedEquip == EquipingManager.Instance.Ring.equipment || SelectedEquip == EquipingManager.Instance.Necklace.equipment || SelectedEquip == EquipingManager.Instance.Book.equipment || SelectedEquip == EquipingManager.Instance.Accessory.equipment || SelectedEquip == EquipingManager.Instance.Relics.equipment));
            UpgradeButton.SetActive(SelectedEquip.equipData.Level < 20);
            JinhwaButton.SetActive(EquipBag.MergeCount(SelectedEquip.equipData) >= SelectedEquip.equipData.RequiredAmountForMerge);   // 고대도 같은 등급 합성 가능 (2026-11-26)
            { var jl = JinhwaButton.GetComponentInChildren<TMPro.TMP_Text>(true); if(jl != null && jl.text != "합 성") jl.text = "합 성"; }   // 진화 → 같은 등급 합성 (2026-11-26)
            if(!mergeButtonHooked) {   // 예전 합성 창(MergeManager) 대신 새 합성 창 (Remaster/EquipMergeUI.cs, 2026-11-26 A안)
                var b = JinhwaButton.GetComponent<UnityEngine.UI.Button>();
                if(b != null) { b.onClick = new UnityEngine.UI.Button.ButtonClickedEvent(); b.onClick.AddListener(() => { SoundManager.Instance.playClickSFX(); RemasterUI.OpenEquipMerge(SelectedEquip); }); }
                mergeButtonHooked = true;
            }
            EquipDismantle.Refresh(JinhwaButton, SelectedEquip);   // 분해 (2026-11-25)
            ChangeStarSetting(SelectedEquip.equipData.Level);
            setEquipInfoGUI();
        }

        //SetButton
        tabGroup.SelectTabbyIndex(Buttons[CurrentSet]);

        foreach(EquipFrame e in All_Equips) {
            e.setGUI();
        }

        setEquipStatsUI();
        setEquipAvailablilty();
    }

    public void setEquipStatsUI()
    {
        foreach(GameObject g in equipStats_obj) {
            g.SetActive(false);
        }

        if(SelectedEquip != null) {
            if(SelectedEquip.equipData.FinalOption.option.damage != 0) {
                equipStats_obj[0].SetActive(true);
                equipStats_obj[0].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = SelectedEquip.equipData.FinalOption.option.damage.ToString("#.##");
            }
            if(SelectedEquip.equipData.FinalOption.option.damagePercent != 0) {
                equipStats_obj[1].SetActive(true);
                equipStats_obj[1].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.damagePercent * 100).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.CritChance != 0) {
                equipStats_obj[2].SetActive(true);
                equipStats_obj[2].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.CritChance).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.CritDamage != 0) {
                equipStats_obj[3].SetActive(true);
                equipStats_obj[3].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.CritDamage * 100).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.AttackSpeed != 0) {
                equipStats_obj[4].SetActive(true);
                equipStats_obj[4].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.AttackSpeed * 100).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.BulletSpeed != 0) {
                equipStats_obj[5].SetActive(true);
                equipStats_obj[5].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.BulletSpeed * 100).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.StunPercent != 0) {
                equipStats_obj[6].SetActive(true);
                equipStats_obj[6].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.StunPercent).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.ExtraMoney != 0) {
                equipStats_obj[7].SetActive(true);
                equipStats_obj[7].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.ExtraMoney * 100).ToString("#.##") + "%";
            }
            if(SelectedEquip.equipData.FinalOption.option.ExtraEXP != 0) {
                equipStats_obj[8].SetActive(true);
                equipStats_obj[8].transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = (SelectedEquip.equipData.FinalOption.option.ExtraEXP * 100).ToString("#.##") + "%";
            }
        }
    }

    public void unSelectAfterMerge()
    {
        SelectedEquip = null;
        Background.gameObject.SetActive(false);
        Background_BG.gameObject.SetActive(false);
        Glow.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
        TypeBorder.gameObject.SetActive(false);
        EquipImage.gameObject.SetActive(false);
        GradeText.gameObject.SetActive(false);
        LevelText.gameObject.SetActive(false);
        EquipNameText.gameObject.SetActive(false);
        EquipSets.Hide();
        Type.gameObject.SetActive(false);
        Border.gameObject.SetActive(false);
        EquipButton.SetActive(false);
        unEquipButton.SetActive(false);
        UpgradeButton.SetActive(false);
        JinhwaButton.SetActive(false);
        EquipDismantle.Hide();
    }

    public void EquipClicked(EquipFrame data)
    {
        SelectedEquip = data;

        Background.gameObject.SetActive(true);
        Background_BG.gameObject.SetActive(true);
        Glow.gameObject.SetActive(true);
        Light.gameObject.SetActive(true);
        TypeBorder.gameObject.SetActive(true);
        EquipImage.gameObject.SetActive(true);
        GradeText.gameObject.SetActive(true);
        LevelText.gameObject.SetActive(true);
        EquipNameText.gameObject.SetActive(true);
        Type.gameObject.SetActive(true);
        Border.gameObject.SetActive(true);

        Background.color = data.Background.color;
        Background_BG.color = data.Background_BG.color;
        Glow.color = data.Glow.color;
        Light.color = data.Light.color;
        TypeBorder.color = data.TypeBorder.color;
        Type.sprite = data.Type_Image.sprite;
        LevelText.text = "Lv." + data.equipData.Level;
        GradeText.text = data.equipData.gradeText;
        GradeText.color = data.Background_BG.color;
        EquipNameText.text = data.equipData.EquipName;
        EquipSets.ShowUnder(EquipNameText, data.equipData);   // 세트 한 줄 (2026-11-25)
        EquipImage.sprite = data.equipData.Sprite_Equip;
        setEquipStatsUI();

        ChangeEquipsUI();
    }

    public void setEquipInfoGUI()
    {
        Background.color = SelectedEquip.Background.color;
        Background_BG.color = SelectedEquip.Background_BG.color;
        Glow.color = SelectedEquip.Glow.color;
        Light.color = SelectedEquip.Light.color;
        TypeBorder.color = SelectedEquip.TypeBorder.color;
        Type.sprite = SelectedEquip.Type_Image.sprite;
        LevelText.text = "Lv." + SelectedEquip.equipData.Level;
        GradeText.text = SelectedEquip.equipData.gradeText;
        GradeText.color = SelectedEquip.Background_BG.color;
        EquipNameText.text = SelectedEquip.equipData.EquipName;
        EquipImage.sprite = SelectedEquip.equipData.Sprite_Equip;
    }

    public void deSelectOthers(EquipFrame eq)
    {
        foreach(EquipFrame e in All_Equips) {
            if(e != eq) {
                e.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
                e.Border.color = Color.black;
            }
        }

        ChangeEquipsUI();
    }
    public void EquipItem()
    {
        EquipingManager.Instance.SetEquipingItem(SelectedEquip);
        for(int i = 0; i < All_Equips.Count; i++) {
            if(All_Equips[i].equipData.TypeOfEquip == SelectedEquip.equipData.TypeOfEquip) {
                if(All_Equips[i] == SelectedEquip) {
                    All_Equips[i].isEquiped = true;
                } else {
                    All_Equips[i].isEquiped = false;
                }
            }
        }

        ChangeEquipsUI();
    }
    public void UnequipItem()
    {
        EquipingManager.Instance.UnequipItem(SelectedEquip.equipData.TypeOfEquip);
        for(int i = 0; i < All_Equips.Count; i++) {
            All_Equips[i].isEquiped = false;
        }

        ChangeEquipsUI();
    }
    public void UnequipAllItem()
    {
        EquipingManager.Instance.UnequipItem(Equips.Type.Ring);
        EquipingManager.Instance.UnequipItem(Equips.Type.Accessory);
        EquipingManager.Instance.UnequipItem(Equips.Type.Book);
        EquipingManager.Instance.UnequipItem(Equips.Type.Relics);
        EquipingManager.Instance.UnequipItem(Equips.Type.Necklace);
        for(int i = 0; i < All_Equips.Count; i++) {
            All_Equips[i].isEquiped = false;
        }

        ChangeEquipsUI();
    }

    //Add Methods
    public void addMergeEquip(EquipFrame data)
    {
        EquipBag.Grant(data);
    }

    public int getEquipNumber(EquipFrame data)
    {
        return EquipBag.CountOf(data.equipData);
    }

    //Get Methods
    public EquipFrame getRandomItem_Grade(Equips.MaterialClass materialClass, Equips.Type EquipType)
    {
        List<EquipFrame> RandomList = new List<EquipFrame>();
        for(int i = 0; i < EquipBag.Catalog.Count; i++) {   // 도감에서 골라요 (2026-11-25 가방)
            if(EquipBag.Catalog[i].equipData.Grade == materialClass && EquipBag.Catalog[i].equipData.TypeOfEquip == EquipType) {
                RandomList.Add(EquipBag.Catalog[i]);
            }
        }
        int randomIndex = Random.Range(0, RandomList.Count);

        return RandomList[randomIndex];
    }
    public int getEquipNumberByType(Equips.Type data)
    {
        int sum = 0;
        for(int i = 0; i < All_Equips.Count; i++) {
            if(All_Equips[i].equipData.TypeOfEquip == data && All_Equips.FindIndex(x => x.equipData.id == All_Equips[i].equipData.id) == i)   // 같은 장비 여러 개는 한 번만 (2026-11-25 가방)
                sum++;
        }
        return sum;
    }

    //Set Methods
    public void setSetTabInterace()
    {
        /*
        for(int i = 1; i < System.Enum.GetValues(typeof(Equips.SetType)).Length; i++) {
            datas.Clear();
            equipState.Clear();
            for(int j = 0; j < All_Equips.Count; j++) {
                if((Equips.SetType)i == All_Equips[j].setType) {
                    datas.Add(All_Equips[j]);

                    name = All_Equips[j].setNameInKorean;

                    if(All_Equips[j].isEquiped) {
                        equipState.Add(3);
                    } else if(All_Equips[j].AmountOfEquip > 0) {
                        equipState.Add(2);
                    } else {
                        equipState.Add(1);
                    }
                }
            }
            setDatas[i-1].setInterface(setSetNameInKorean((Equips.SetType)i), datas, equipState);
        }

        ReorderSets();
        */
    }
    public string setSetNameInKorean(Equips.SetType type)
    {
        switch(type) {
            case Equips.SetType.Aphrodite_Set:
                return "아프로디테 세트";
            case Equips.SetType.Ares_Set:
                return "아레스 세트";
            case Equips.SetType.Argos_Set:
                return "아르고스 세트";
            case Equips.SetType.Artemis_Set:
                return "아르테미스 세트";
            case Equips.SetType.Atena_Set:
                return "아테나 세트";
            case Equips.SetType.Beginner_Set:
                return "초보자 세트";
            case Equips.SetType.Blood_Set:
                return "혈의 세트";
            case Equips.SetType.Cerberus_Set:
                return "케르베로스 세트";
            case Equips.SetType.Dark_Set:
                return "어둠 세트";
            case Equips.SetType.Demeter_Set:
                return "****** 세트";
            case Equips.SetType.Devil_Set:
                return "악마 세트";
            case Equips.SetType.Evil_Set:
                return "악의 세트";
            case Equips.SetType.Gaea_Set:
                return "가이아 세트";
            case Equips.SetType.Gold_Set:
                return "황금 세트";
            case Equips.SetType.Hades_Set:
                return "하데스 세트";
            case Equips.SetType.Hephaistus_Set:
                return "헤파이스토스 세트";
            case Equips.SetType.Hercules_Set:
                return "헤라클레스 세트";
            case Equips.SetType.Hestia_Set:
                return "헤스티아 세트";
            case Equips.SetType.Honor_Set:
                return "정복자 세트";
            case Equips.SetType.Hunter_Set:
                return "사냥꾼 세트";
            case Equips.SetType.Hydra_Set:
                return "히드라 세트";
            case Equips.SetType.Ice_Set:
                return "서리 세트";
            case Equips.SetType.Iron_Set:
                return "철 세트";
            case Equips.SetType.Jungle_Set:
                return "정글 세트";
            case Equips.SetType.Magician_Set:
                return "마법사 세트";
            case Equips.SetType.ME_Set:
                return "ME 세트";
            case Equips.SetType.Pan_Set:
                return "판 세트";
            case Equips.SetType.Poseidon_Set:
                return "포세이돈 세트";
            case Equips.SetType.Sea_Set:
                return "바다 세트";
            case Equips.SetType.Warrior_Set:
                return "전사 세트";
            case Equips.SetType.Wood_Set:
                return "나무 세트";
            case Equips.SetType.Zeus_Set:
                return "제우스 세트";
        }
        return "";
    }
    public void setSetEffect(Equips.SetType type, EquipsOption option)
    {
        switch(type) {
            case Equips.SetType.Aphrodite_Set:
                option.option.CritChance = 15;
                option.option.damage = 30;
                break;
            case Equips.SetType.Ares_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Argos_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Artemis_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Atena_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Beginner_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Blood_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Cerberus_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Dark_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Demeter_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Devil_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Evil_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Gaea_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Gold_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hades_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hephaistus_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hercules_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hestia_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Honor_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hunter_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Hydra_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Ice_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Iron_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Jungle_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Magician_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.ME_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Pan_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Poseidon_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Sea_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Warrior_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Wood_Set:
                option.option.CritChance = 15;
                break;
            case Equips.SetType.Zeus_Set:
                option.option.CritChance = 15;
                break;
        }
    }

    public void unSetEquipSet(int index)
    {
        Equip_Sets[CurrentSet].equipSet[index] = null;
    }
    public void setEquipSet(int index, EquipFrame data)
    {
        Equip_Sets[CurrentSet].equipSet[index] = data;
    }

    public void setEquipSetItem(int index)
    {
        CurrentSet = index;
        for(int i = 0; i < 5; i++) {
            if(Equip_Sets[index].equipSet[i] != null) {
                EquipingManager.Instance.SetEquipingItem(Equip_Sets[index].equipSet[i]);
            } else {
                if(i == 0)
                    EquipingManager.Instance.UnequipItem(Equips.Type.Ring);
                else if(i == 1)
                    EquipingManager.Instance.UnequipItem(Equips.Type.Necklace);
                else if(i == 2)
                    EquipingManager.Instance.UnequipItem(Equips.Type.Relics);
                else if(i == 3)
                    EquipingManager.Instance.UnequipItem(Equips.Type.Accessory);
                else if(i == 4)
                    EquipingManager.Instance.UnequipItem(Equips.Type.Book);
            }
        }

        ChangeEquipsUI();
    }

    public void addEquipByID(string id)
    {
        // 2026-11-25 가방: 도감에서 찾아 장비 하나 넣기
        var c = EquipBag.Catalog.Find(x => x.equipData.id == id);
        if(c != null) EquipBag.Grant(c);
    }

    //Data Save
    public void LoadData(GameData data)
    {
        // 장비 가방 (2026-11-25): 장비 하나씩. 예전 저장은 종류별 개수에서 만들어요
        EquipBag.Load(data);
        for(int i = 0; i < Equip_Sets.Count; i++) {
            for(int j = 0; j < Equip_Sets[i].equipSet.Count; j++) {
                data.Equip_SetData.TryGetValue("setofthedatas" + i + j, out string EquipID);

                if(EquipID != "null") {
                    Equip_Sets[i].equipSet[j] = EquipBag.Find(EquipID);   // 새 저장은 장비 번호(uid), 예전 저장은 종류 id
                }
            }
        }

        CurrentSet = data.currentSetIndex;

        setEquipAvailablilty();
        setEquipSetItem(CurrentSet);
    }
    public void SaveData(GameData data)
    {
        // 장비 가방 (2026-11-25). 예전 종류별 기록(Equip_Amount 등)은 건드리지 않아요 (equipItems 가 있으면 안 읽어요)
        EquipBag.Save(data);
        //Set 
        for(int i = 0; i < Equip_Sets.Count; i++) {
            for(int j = 0; j < Equip_Sets[i].equipSet.Count; j++) {
                if(data.Equip_SetData.ContainsKey("setofthedatas" + i + j))
                    data.Equip_SetData.Remove("setofthedatas" + i + j);

                if(Equip_Sets[i].equipSet[j] != null) {
                    data.Equip_SetData.Add("setofthedatas" + i + j, Equip_Sets[i].equipSet[j].equipData.uid ?? Equip_Sets[i].equipSet[j].equipData.id);
                } else {
                    data.Equip_SetData.Add("setofthedatas" + i + j, "null");
                }
            }
        }

        data.currentSetIndex = CurrentSet;
    }
}

[System.Serializable]
public class EquipSet
{
    public List<EquipFrame> equipSet;
}
