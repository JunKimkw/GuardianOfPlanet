using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using TMPro;
using System.Threading.Tasks;

public class StageManager : MonoBehaviour
{
    public static StageManager instance;

    [Header("#----Map Scroll Rect")]
    public ScrollRect scrollRect;
    public RectTransform contentPanel;
    [Header("#----Parellel Map Scroll Rect")]
    public ScrollRect scrollRect_Parellel;
    public RectTransform contentPanel_Parellel;

    [Header("#-----Planets")]
    public List<Planet> planets;
    [HideInInspector]
    public Planet currentPlanet;
    StageData currentStage;

    // 행성 맵 적 수 (2026-11-25 사용자: 1-1 이 5마리라 쌍발총을 써 보지도 못하고 끝나요)
    //  맵에 정해진 수가 planet_enemy_min(60)보다 적으면 그만큼 늘려요. 늘린 만큼 나오는 간격도 줄여요 (최대 planet_spawn_speedup_max 3배 빠르게)
    public static int EnemyTargetOf(StageData s) => s == null ? 0 : Mathf.Max(s.EnemiesToKill, Tuning.GetInt("planet_enemy_min", 60));
    int EnemyTarget => EnemyTargetOf(currentStage);

    // 행성 난이도 = 원정 몇 스테이지 수준 (2026-11-25 사용자 결정: 원정 곡선에 맞추기)
    //  = planet_stage_start(5) + 행성 번호 × planet_stage_per_planet(12) + 맵 번호 × planet_stage_per_map(1.5)
    //  1번 행성 5~15, 2번 17~27 … 10번 약 113~123. 적·보스 체력·방어력은 원정과 같은 공식 (EnemyManager.PlanetStats)
    public int PlanetEquivStage {
        get {
            if(currentPlanet == null || currentStage == null) return 0;
            int p = Mathf.Max(0, planets.IndexOf(currentPlanet));
            int m = Mathf.Max(0, System.Array.IndexOf(currentPlanet.StagesOfThisPlanet, currentStage));
            return Mathf.Max(1, Mathf.RoundToInt(Tuning.Get("planet_stage_start", 5f) + p * Tuning.Get("planet_stage_per_planet", 12f) + m * Tuning.Get("planet_stage_per_map", 1.5f)));
        }
    }
    public bool PlanetBattle => InBattle;
    public int PlanetMapIndex => currentPlanet == null || currentStage == null ? 0 : Mathf.Max(0, System.Array.IndexOf(currentPlanet.StagesOfThisPlanet, currentStage));

    // 행성 안 뒤쪽 맵일수록 더 단단하게 (2026-11-25 사용자: 증강이 맵마다 이어지니까 뒤쪽 맵은 어렵게, x-1 은 무난하게. 막 고르면 질 수 있게)
    //  체력 × planet_map_hp_growth(1.35) ^ 맵 번호 → x-1 ×1, x-4 ×2.5, x-8 ×8.2. 증강이 쌓여 합체까지 가면 첫 맵보다 약 8~10배 세져요 (계산)
    public float PlanetMapHpMul => Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_map_hp_growth", 1.35f)), PlanetMapIndex);
    float SpawnSpeedup => currentStage == null || currentStage.EnemiesToKill <= 0 ? 1f : Mathf.Clamp(Mathf.Sqrt(EnemyTarget / (float)currentStage.EnemiesToKill), 1f, Mathf.Max(1f, Tuning.Get("planet_spawn_speedup_max", 3f)));

    [Header("#-----UIs")]
    [Space(20)]
    public TextMeshProUGUI RemainedMonster_Text;
    public Image PlanetImage;

    [Header("#-----Result UIS")]
    [Space(20)]
    public GameObject StageFailed;
    public GameObject StageCleared;
    public GameObject GoldText;
    public GameObject DiamondText;
    public GameObject[] stoneObjs;
    public Button nextStageButton;

    public GameObject WatchAdButton;
    public GameObject PopUpAdButton;

    [Header("#-----Messages")]
    [Space(20)]
    public GameObject WarningObject;
    public GameObject PlanetPackageTab;

    [Header("#----Pause Game")]
    public GameObject PauseTab;
    public TextMeshProUGUI pauseTab_CurentStage;

    bool SpawningEnemy;
    bool InBattle;
    bool BossSpawned;
    int KilledEnemy;
    int SpawnedEnemy;

    public bool GameSpeedBought;

    public bool isInStage;
    public bool planetScreen;   // 행성 화면이 떠 있어요 (ToPlanet 순간부터 마을로 갈 때까지). 행성 레벨 바가 골드·남은 적과 같이 뜨게 (2026-11-25)

    //Reward Double Temps
    long rd_Money;
    int rd_Stone;
    int rd_Diamond;

    public void Subscribe(Planet planet)
    {
        if(planets == null)
            planets = new List<Planet>();

        planets.Add(planet);
    }

    private void Awake()
    {
        instance = this;

        for(int i = 14; i < planets.Count; i++) {
            for(int j = 0; j < planets[j].StagesOfThisPlanet.Length; j++) {
                planets[i].StagesOfThisPlanet[j].goldPrice *= 10;
                planets[i].StagesOfThisPlanet[j].DiamondPrice *= 2;
                planets[i].StagesOfThisPlanet[j].stonePriceAmount *= 2;
            }
        }
    }

    void Start()
    {
        planets.Sort(SortByScore);
        if(planets[0].name.Contains("Home")) {
            Planet temp = planets[0];
            planets[0] = planets[1];
            planets[1] = temp;
        }
        currentPlanet = planets[0];
        currentStage = currentPlanet.StagesOfThisPlanet[0];
        SetmapInfo(0);
    }

    void Update()
    {
        //Under stats
        if(InBattle) {
            if(KilledEnemy >= EnemyTarget && !BossSpawned) {
                if(currentStage.bossPrefab.Length != 0) {
                    BossSpawned = true;
                    WarningObject.gameObject.SetActive(true);
                    for(int i = 0; i < currentStage.bossPrefab.Length; i++)
                        EnemyManager.Instance.CreateBoss(currentStage.bossPrefab[i], currentStage.BossScale[i]);
                } else {
                    Cleared();
                }
            }
        }
    }

    public void checkAllPlanetState()
    {
        for(int i = 1; i < planets.Count/2; i++) {
            planets[i].CheckPlanetState(planets[i - 1]);
        }
        for(int i = planets.Count/2 + 1; i < planets.Count; i++) {
            planets[i].CheckPlanetState(planets[i - 1]);
        }

        for(int i = 0; i < planets.Count; i++) {
            planets[i].checkPlanetState();
        }

    }

    public void setMapPosition()
    {
        PlayerPrefs.SetInt("lastAnchoredPosition", (int)contentPanel.anchoredPosition.y);
        PlayerPrefs.SetInt("lastAnchoredPosition2", (int)contentPanel_Parellel.anchoredPosition.y);
    }

    public void getMapPosition()
    {
        contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, PlayerPrefs.GetInt("lastAnchoredPosition"));
        contentPanel_Parellel.anchoredPosition = new Vector2(contentPanel_Parellel.anchoredPosition.x, PlayerPrefs.GetInt("lastAnchoredPosition2"));
    }

    public void StartSpawningEnemy()
    {
        InBattle = true;
        SpawningEnemy = true;
        for(int i = 0; i < currentStage.enemiesPrefab.Length; i++) {
            StartCoroutine(spawnEnemy(currentStage.enemiesPrefab[i], currentStage.SpawnSpeed[i] / SpawnSpeedup, currentStage.enemyScale[i]));
        }
    }

    IEnumerator spawnEnemy(Enemy e, float SpawningTime, float Scale)
    {
        while(SpawningEnemy) {

            // 보스가 나온 뒤엔 일반 적이 덜 나와요 (2026-11-25 사용자: 보스 맵은 계속 나오되 보스 때는 스폰율을 좀 줄여서)
            yield return new WaitForSeconds(SpawningTime * (BossSpawned ? Mathf.Max(1f, Tuning.Get("planet_boss_spawn_slow", 2.5f)) : 1f));
            SpawnedEnemy++;
            EnemyManager.Instance.createEnemy(e, Scale);
            // 보스 없는 맵은 정해진 수만큼만, 보스 맵은 보스를 잡을 때까지 일반 적이 계속 나와요
            //  (2026-11-25 사용자: 보스 맵은 일반 몹이 계속 나오는 게 더 어렵고 재밌어요)
            if(SpawnedEnemy >= EnemyTarget && currentStage.bossPrefab.Length == 0) {
                SpawningEnemy = false;
                StopAllCoroutines();
            }
        }
    }

    public static void EnemyDeadNormal(int exp, long gold)
    {
        if(AugmentManager.Instance != null) AugmentManager.Instance.OnPlanetKill();   // 행성 레벨업 (2026-11-25)
        GameManager.Enemy_Dead_Player_EXP_UP(exp);
        GameManager.Enemy_Dead_Money_UP(gold);
        instance.KilledEnemy++;
        if(instance.KilledEnemy <= instance.EnemyTarget)
            instance.RemainedMonster_Text.text = (instance.EnemyTarget - instance.KilledEnemy).ToString();
        else
            instance.RemainedMonster_Text.text = 0.ToString();
    }
    public static void EnemyDeadBoss(int exp, long gold)
    {
        GameManager.Enemy_Dead_Player_EXP_UP(exp);
        GameManager.Enemy_Dead_Money_UP(gold);
        instance.Cleared();
    }

    public void SetmapInfo(int i)
    {
        currentPlanet.MapSelected(i);
        currentStage = currentPlanet.StagesOfThisPlanet[i];
        PlanetImage.sprite = currentPlanet.PlanetSprite;
    }

    public void Move()
    {
        SetStageBeforeEntering();
    }

    public async void SetStageBeforeEntering()
    {
        // 행성 한 판 시작 (2026-11-25): 원정 상태를 보관하고, 이 행성의 무기를 들고 레벨업 카드로 키워요 (AugmentManager PlanetRun.cs)
        if(AugmentManager.Instance != null) AugmentManager.Instance.BeginPlanetRun(planets.IndexOf(currentPlanet), currentPlanet != null ? System.Array.IndexOf(currentPlanet.StagesOfThisPlanet, currentStage) : 0, EnemyTarget);

        //Sound
        SoundManager.Instance.playMusic(SoundManager.MusicType.AdventureBattleBGM);

        TransformTab.instance.gameObject.SetActive(true);
        TransformTab.instance.startFading(currentPlanet.PlanetName);

        //Load GameSpeed and Auto
        GameManager.instance.loadGameSpeedandAuto();

        //Stop Infinite Stage
        InfiniteStage.Instance.PauseInfiniteStage();

        //Default the values
        KilledEnemy = 0;
        SpawnedEnemy = 0;
        BossSpawned = false;
        WarningObject.SetActive(false);

        await Task.Delay(1500);

        GridManager.instance.ToPlanet(currentPlanet.GridIndex);
        planetScreen = true;
        RemainedMonster_Text.text = (EnemyTarget - KilledEnemy).ToString();
        await Task.Delay(2000);
        StartSpawningEnemy();

        isInStage = true;
    }

    public void moveToNextStage()
    {
        for(int i = 0; i < currentPlanet.StagesOfThisPlanet.Length; i++) {
            if(currentPlanet.StagesOfThisPlanet[i].StageName == currentStage.StageName) {
                currentStage = currentPlanet.StagesOfThisPlanet[i + 1];
                Move();
                break;
            }
        }
    }

    public void MoveToVillage()
    {
        MoveToVillage2();
    }
    public async void MoveToVillage2()
    {
        // 행성 한 판 끝: 원정 상태를 되돌려요 (2026-11-25)
        if(AugmentManager.Instance != null) AugmentManager.Instance.EndPlanetRun();
        //Sound
        SoundManager.Instance.playMainTitleMusic();

        TransformTab.instance.gameObject.SetActive(true);
        TransformTab.instance.startFading("지구");

        //게임 속도 (바탕 속도 = 표시 x1)
        Time.timeScale = GameManager.SpeedBase;

        await Task.Delay(2000);

        for(int i = 0; i < planets.Count; i++) {
            planets[i].checkPlanetState();
        }

        EnemyManager.Instance.removeAllEnemies();
        GridManager.instance.ToVillage();
        InfiniteStage.Instance.ResumeInfiniteStage();
        isInStage = false;
        planetScreen = false;
    }

    public void Pausestage()
    {
        PauseTab.SetActive(true);
        pauseTab_CurentStage.text = currentPlanet.PlanetName + " " + currentStage.StageName;
    }

    public void Cleared()
    {
        if(AugmentManager.Instance != null) {
            AugmentManager.Instance.OnPlanetMapEnd();   // 마지막 처치로 쌓인 레벨업은 안 띄워요 (2026-11-25)
            AugmentManager.Instance.OnPlanetMapCleared();   // 클리어한 맵까지 고른 증강을 기억 (2026-11-25)
            var maps = currentPlanet != null ? currentPlanet.StagesOfThisPlanet : null;
            if(maps != null && maps.Length > 0 && maps[maps.Length - 1] == currentStage) AugmentManager.Instance.OnPlanetLastMapCleared();   // 행성 끝: 모은 증강 지워요
        }
        InBattle = false;
        StageFailed.SetActive(false);
        StageCleared.SetActive(true);
        ResultTabActive(true);

        SoundManager.Instance.musicSource.Stop();
        SoundManager.Instance.playVictory();
    }

    public void Died()
    {
        if(AugmentManager.Instance != null) AugmentManager.Instance.OnPlanetMapEnd();
        InBattle = false;
        StageFailed.SetActive(true);
        StageCleared.SetActive(false);
        ResultTabActive(false);
        SoundManager.Instance.musicSource.Stop();
        SoundManager.Instance.playDefeat();
    }
    public void ResultTabActive(bool a)
    {
        GameManager.instance.saveGameSpeedandAuto(Time.timeScale, WeaponManager.instance.shootType == WeaponManager.ShootType.AutoShoot);
        WeaponManager.instance.shootType = WeaponManager.ShootType.NormalShoot;
        //Next Stage Button
        nextStageButton.interactable = currentPlanet.StagesOfThisPlanet[currentPlanet.StagesOfThisPlanet.Length - 1].StageName != currentStage.StageName;

        //UIS
        EnemyManager.Instance.removeAllEnemies();
        StopAllCoroutines();

        StageCleared.SetActive(a);
        StageFailed.SetActive(!a);

        WatchAdButton.SetActive(true);

        DiamondText.SetActive(false);
        GoldText.SetActive(true);
        for(int i = 0; i < stoneObjs.Length; i++) {
            stoneObjs[i].SetActive(false);
        }

        //Result
        long PriceGold = (long)(currentStage.goldPrice * Mathf.Max(0.3f, 1f - (0.2f * currentStage.ClearedTime)));
        int PriceStone = (int)(currentStage.stonePriceAmount * Mathf.Max(0.3f, 1f - (0.5f * currentStage.ClearedTime)));
        GoldText.GetComponentInChildren<TextMeshProUGUI>().text = GameManager.instance.MoneyStringForInfiniteStage(PriceGold);
        GameManager.SetMoney(PriceGold);
        rd_Money = PriceGold;

        stoneObjs[(int)currentStage.stonePrice].SetActive(true);
        stoneObjs[(int)currentStage.stonePrice].GetComponentInChildren<TextMeshProUGUI>().text = PriceStone.ToString();
        UpgradeStoneManager.instance.addStone(currentStage.stonePrice, PriceStone);
        rd_Stone = PriceStone;

        if(a && !currentStage.StageCleared) {
            DiamondText.gameObject.SetActive(true);
            DiamondText.GetComponentInChildren<TextMeshProUGUI>().text = currentStage.DiamondPrice.ToString();
            GameManager.SetDiamond(currentStage.DiamondPrice);
            rd_Diamond = currentStage.DiamondPrice;
            currentStage.StageCleared = true;
        } else {
            rd_Diamond = 0;
        }

        //ClearedTime ++
        if(a) {
            currentStage.ClearedTime++;
        }


        EnemyManager.Instance.removeAllEnemies();
    }

    public void RewardDoubleAd()
    {
        GoldText.GetComponentInChildren<TextMeshProUGUI>().text = GameManager.instance.MoneyStringForInfiniteStage(rd_Money * 2);
        stoneObjs[(int)currentStage.stonePrice].GetComponentInChildren<TextMeshProUGUI>().text = (rd_Stone * 2).ToString();
        DiamondText.GetComponentInChildren<TextMeshProUGUI>().text = (rd_Diamond * 2).ToString();

        GameManager.SetMoney(rd_Money);
        GameManager.SetDiamond(rd_Diamond);
        UpgradeStoneManager.instance.addStone(currentStage.stonePrice, rd_Stone);
        WatchAdButton.SetActive(false);
        PopUpAdButton.SetActive(false);
    }

    public string FindNextPlanet()
    {
        for(int i = 0; i < currentPlanet.StagesOfThisPlanet.Length; i++) {
            if(currentPlanet.StagesOfThisPlanet[i].StageName == currentStage.StageName) {
                if(i == currentPlanet.StagesOfThisPlanet.Length - 1) {
                    for(int j = 0; j < planets.Count - 1; j++) {
                        if(planets[j].PlanetName == currentPlanet.PlanetName) {
                            return planets[j + 1].PlanetName + " " + planets[j + 1].StagesOfThisPlanet[0].StageName;
                        }
                    }
                } else {
                    return currentPlanet.PlanetName + " " + currentPlanet.StagesOfThisPlanet[i + 1].StageName;
                }
            }
        }
        return "";
    }

    public static int SortByScore(Planet stage1, Planet stage2)
    {
        int stage1Num = int.Parse(Regex.Replace(stage1.name, "[^0-9]", ""));
        int stage2Num = int.Parse(Regex.Replace(stage2.name, "[^0-9]", ""));
        return stage1Num.CompareTo(stage2Num);
    }
}
