using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfiniteStage : MonoBehaviour, IDataPersistence
{
    public static InfiniteStage Instance;
    public Enemy[] enemyPrefab;
    public Enemy[] bossPrefab;

    [Header("UI")]
    [Space(5)]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI HealthText;
    public TextMeshProUGUI RemainedMonsterText;

    [Header("#GameObject")]
    public GameObject WarningObject;

    [Header("#---Current Stage Info")]
    public List<int> CurrentEnemyIndex;
    int CurrentBossIndex;

    [Header("#----Stage Failed Tabs")]
    public GameObject ResultTab_Failed;
    public TextMeshProUGUI currentStageText_Failed;
    public Slider timeSlider_Failed;

    [Header("#----Result")]
    public GameObject ResultTab_Cleared;
    public Slider timeSlider_Cleared;

    //Prize
    public GameObject GoldReward_gameObject;
    public GameObject DiamondReward_gameObject;
    public GameObject NormalChestReward_gameObject;
    public GameObject SpecialChestReward_gameObject;
    public GameObject StoneChestReward_gameObject;
    public GameObject NormalStoneReward_gameObject;
    public GameObject RareStoneReward_gameObject;
    public GameObject EpicStoneReward_gameObject;
    public GameObject UniqueStoneReward_gameObject;
    public GameObject LegendaryStoneReward_gameObject;
    public GameObject AncientStoneReward_gameObject;

    [HideInInspector]
    public int playerHealth;

    public int CurrentStage;

    bool SpawningEnemy;
    bool InBattle;
    bool BossSpawned;
    int KilledEnemy;
    int SpawnedEnemy;

    public float EnemyScale;
    float SpawnSpeed;

    //Saved Killed Enemy and Health
    public int tempEnemy;
    public int tempHealth;

    private void Awake()
    {
        Instance = this;
    }

    // 리마스터: 원정(무한 스테이지)이 지금 진행 중인지
    public bool IsRunning => InBattle;

    int StartingShield()
    {
        int bonus = AugmentManager.Instance != null ? AugmentManager.Instance.ShieldBonus : 0;
        return Mathf.Max(1, 3 + bonus);
    }

    // 리마스터: 원정을 특정 스테이지에서 다시 시작 (환생, 기존 유저 보상 후 재시작)
    public void JumpToStage(int stage)
    {
        bool wasRunning = InBattle;
        PauseInfiniteStage();
        ResultTab_Cleared.SetActive(false);
        ResultTab_Failed.SetActive(false);
        WarningObject.SetActive(false);
        CurrentStage = Mathf.Max(1, stage);
        tempEnemy = 0;
        tempHealth = StartingShield();
        ApplyZoneVisuals();
        stageText.text = CurrentStage.ToString();
        if(wasRunning)
            SetStageBeforeEntering();
    }

    // 리마스터: 실드 수가 바뀌는 증강을 얻었을 때 현재 실드에도 바로 반영
    public void AddShieldNow(int delta)
    {
        playerHealth = Mathf.Max(1, playerHealth + delta);
        HealthText.text = playerHealth.ToString();
    }

    private void Update()
    {
        if(InBattle && !AugmentManager.DebugTestMode) {   // 테스트 모드: 스테이지가 넘어가지 않아요
            if(KilledEnemy >= setEnemyNumber() && !BossSpawned) {
                if(CurrentStage % 10 == 0) {
                    BossSpawned = true;
                    WarningObject.gameObject.SetActive(true);
                    EnemyManager.Instance.CreateInfiniteBoss(bossPrefab[CurrentBossIndex], KilledEnemy * 0.005f);
                } else {
                    CurrentStage++;
                    StartCoroutine(nextStage());
                }
            }
        }
    }

    // 개발용 (테스트 창 "보스 소환"): 지금 스테이지의 보스를 하나 더 불러요. 체력은 스테이지를 다 잡았을 때 나오는 보스와 같아요.
    // 테스트 모드가 꺼져 있으면 이 보스를 잡을 때 다음 스테이지로 넘어가요
    public bool DebugSpawnBoss()
    {
        if(!InBattle || bossPrefab == null || bossPrefab.Length == 0) return false;
        var prefab = bossPrefab[Mathf.Clamp(CurrentBossIndex, 0, bossPrefab.Length - 1)];
        EnemyManager.Instance.CreateInfiniteBoss(prefab, setEnemyNumber() * 0.005f);
        return true;
    }

    public void StartSpawningEnemy()
    {
        InBattle = true;
        SpawningEnemy = true;
        for(int i = 0; i < CurrentEnemyIndex.Count; i++) {
            StartCoroutine(spawnEnemy(enemyPrefab[CurrentEnemyIndex[i]]));
        }
    }


    IEnumerator spawnEnemy(Enemy e)
    {
        while(SpawningEnemy) {
            // 처치가 늘수록 조금씩 빨라지지만 최소 30% 간격은 지켜요 (예전엔 0초·음수가 되어 막판에 한꺼번에 쏟아졌어요)
            float speedUp = 0.1f * (KilledEnemy / Mathf.Max(1f, setEnemyNumber() / 8f));
            yield return new WaitForSeconds(SpawnSpeed * Mathf.Max(0.3f, 1f - speedUp) / AugmentManager.DebugSpawnMul);
            // 테스트 모드: 끝없이 나와요 (화면에 너무 많으면 잠깐 쉬어요)
            if(AugmentManager.DebugTestMode && EnemyManager.Instance.enemyData.Count >= Tuning.GetInt("test_max_enemies", 80)) continue;
            SpawnedEnemy++;
            EnemyManager.Instance.createInfiniteEnemy(e, EnemyScale);
            if(SpawnedEnemy >= setEnemyNumber() && !AugmentManager.DebugTestMode) {
                SpawningEnemy = false;
                StopAllCoroutines();
            }
        }
    }


    public static void EnemyDeadNormal(int exp, long gold)
    {
        GameManager.Enemy_Dead_Player_EXP_UP(exp);
        GameManager.Enemy_Dead_Money_UP(gold);
        Instance.KilledEnemy++;
        if(Instance.KilledEnemy <= Instance.setEnemyNumber())
            Instance.RemainedMonsterText.text = (Instance.setEnemyNumber() - Instance.KilledEnemy).ToString();
    }
    public static void EnemyDeadBoss(int exp, long gold)
    {
        // 신의 관문 보스를 이겼으면 첫 돌파 보상
        if(ExpeditionZones.IsGate(Instance.CurrentStage) && AugmentManager.Instance != null)
            AugmentManager.Instance.OnGateCleared(Instance.CurrentStage);
        Instance.WarningObject.gameObject.SetActive(false);
        GameManager.Enemy_Dead_Player_EXP_UP(exp);
        GameManager.Enemy_Dead_Money_UP(gold);
        Instance.CurrentStage++;
        Instance.StartCoroutine(Instance.nextStage());
    }

    // 실드 감소 (증강: 아이기스·무적이 막고, 방벽이 버티고, 반격·복수 등이 터져요)
    void LoseShield(int amount)
    {
        var aug = AugmentManager.Instance;
        if(aug != null) amount = aug.FilterShieldLoss(amount);
        if(amount <= 0) return;
        playerHealth -= amount;
        if(playerHealth <= 0 && aug != null && aug.TrySurviveLethal()) playerHealth = 1;
        if(aug != null) aug.OnShieldLost(amount);
    }

    public void enemyHit(Enemy data)
    {
        if(AugmentManager.DebugTestMode) return;   // 테스트 모드: 우주선에 닿은 적은 그냥 사라져요 (실드·처치 수 그대로)
        if(data._typeofEnemy == Enemy.typeofEnemy.InfiniteStageBoss) {
            // 보스가 우주선에 닿으면 실드 -2, 살아 있으면 보스가 처음부터 다시 나와요
            // (예전엔 보스 도착을 처리하지 않아서 스테이지가 멈췄어요)
            LoseShield(2);
            BossSpawned = false;
            WarningObject.gameObject.SetActive(false);
        } else {
            KilledEnemy++;
            LoseShield(1);
            if(KilledEnemy <= setEnemyNumber())
                RemainedMonsterText.text = (setEnemyNumber() - KilledEnemy).ToString();
        }
        HealthText.text = playerHealth.ToString();
        if(playerHealth <= 0) {
            SpawningEnemy = false;
            InBattle = false;
            EnemyManager.Instance.removeAllEnemies();
            StopAllCoroutines();

            //Auto Restart
            StartCoroutine(restartStage());
        }
    }

    public void SetStageBeforeEntering()
    {
        //Sound
        SoundManager.Instance.playMusic(SoundManager.MusicType.MainTitle);
        GameManager.instance.loadGameSpeedandAuto();

        KilledEnemy = 0;
        SpawnedEnemy = 0;
        BossSpawned = false;
        playerHealth = StartingShield();
        setEnemyIndex();
        setSpawnSpeed();
        setEnemyScale();
        StartSpawningEnemy();
        GridManager.instance.ToVillage();
        ApplyZoneVisuals();
        HealthText.text = playerHealth.ToString();
        RemainedMonsterText.text = (setEnemyNumber() - KilledEnemy).ToString();
        stageText.text = CurrentStage.ToString();
    }

    public IEnumerator nextStage()
    {
        SoundManager.Instance.playVictory();

        ResultTab_Cleared.SetActive(true);

        //SetPrize
        setPrize();

        //리마스터: 스테이지 클리어 → 증강 뽑기 등
        if(AugmentManager.Instance != null)
            AugmentManager.Instance.OnStageCleared(CurrentStage - 1);

        //Achievement
        AchievementManager.instance.InfiniteStageDaily++;
        AchievementManager.instance.InfiniteStageWeekly++;

        //LeaderBoard Update
        LeaderBoardManager.Instance.AddInfiniteStageScore();

        stageText.text = CurrentStage.ToString();

        //Default the values
        EnemyManager.Instance.removeAllEnemies();
        KilledEnemy = 0;
        SpawnedEnemy = 0;
        BossSpawned = false;

        //SetIndex
        setEnemyIndex();
        setSpawnSpeed();
        setEnemyScale();
        ApplyZoneVisuals();
        RemainedMonsterText.text = (setEnemyNumber() - KilledEnemy).ToString();
        AnnounceZone();
        timeSlider_Cleared.maxValue = 1.5f;
        timeSlider_Cleared.value = 1.5f;

        while(timeSlider_Cleared.value > 0) {
            timeSlider_Cleared.value -= Time.unscaledDeltaTime;
            yield return null;
        }
        ResultTab_Cleared.SetActive(false);

        yield return new WaitForSecondsRealtime(1.5f);
        StartSpawningEnemy();
    }

    public void setPrize()
    {
        //Gold
        GoldReward_gameObject.SetActive(true);
        GoldReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = GameManager.instance.MoneyStringForInfiniteStage(getPrizeMoney());
        GameManager.SetMoney(getPrizeMoney());

        //Diamond
        if((CurrentStage - 1) % 5 == 0) {
            DiamondReward_gameObject.SetActive(true);
            DiamondReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = 50.ToString();
            GameManager.SetDiamond(50);
        } else {
            DiamondReward_gameObject.SetActive(false);
        }

        NormalStoneReward_gameObject.SetActive(false);
        RareStoneReward_gameObject.SetActive(false);
        EpicStoneReward_gameObject.SetActive(false);
        UniqueStoneReward_gameObject.SetActive(false);
        LegendaryStoneReward_gameObject.SetActive(false);
        AncientStoneReward_gameObject.SetActive(false);
        float ran = 0;
        //Stone
        if((CurrentStage - 1) % 4 == 0) {
            ran = UnityEngine.Random.Range(0f, 100f);
            if(ran <= 0.3f) {
                AncientStoneReward_gameObject.SetActive(true);
                AncientStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "1";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Ancient, 1);
            } else if(ran <= 1.5f) {
                LegendaryStoneReward_gameObject.SetActive(true);
                LegendaryStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "2";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Legendary, 2);
            } else if(ran <= 5) {
                UniqueStoneReward_gameObject.SetActive(true);
                UniqueStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "5";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Unique, 5);
            } else if(ran <= 15) {
                EpicStoneReward_gameObject.SetActive(true);
                EpicStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "10";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Epic, 10);
            } else if(ran <= 35) {
                RareStoneReward_gameObject.SetActive(true);
                RareStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "30";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Rare, 30);
            } else if(ran <= 100) {
                NormalStoneReward_gameObject.SetActive(true);
                NormalStoneReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "60";
                UpgradeStoneManager.instance.addStone(UpgradeStone.TypeOfStone.Normal, 60);
            }
        }
        //Ticket
        if((CurrentStage - 1) % 10 == 0) {
            if((CurrentStage - 1) % 20 == 0) {
                SpecialChestReward_gameObject.SetActive(true);
                NormalChestReward_gameObject.SetActive(false);
                SpecialChestReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "x1";
                GachaManager.Instance.SpecialGachaTicket++;
            } else {
                SpecialChestReward_gameObject.SetActive(false);
                NormalChestReward_gameObject.SetActive(true);
                NormalChestReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "x1";
                GachaManager.Instance.NormalGachaTicket++;
            }
        } else {
            SpecialChestReward_gameObject.SetActive(false);
            NormalChestReward_gameObject.SetActive(false);
        }

        //Stone
        if((CurrentStage - 1) % 5 == 0) {
            int ranNum = UnityEngine.Random.Range(1, 5);
            GachaManager.Instance.addTicket(GachaData.GachaType.UpgradeStone, ranNum);
            StoneChestReward_gameObject.SetActive(true);
            StoneChestReward_gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "x1";
        } else {
            StoneChestReward_gameObject.SetActive(false);
        }
    }

    public long getPrizeMoney()
    {
        // 리마스터 원정: 클리어한 스테이지 처치 골드의 25%
        return System.Math.Max(1, RemasterBalance.StageClearGold(Mathf.Max(1, CurrentStage - 1)));
    }

    IEnumerator restartStage()
    {
        SoundManager.Instance.playDefeat();

        ResultTab_Failed.SetActive(true);
        currentStageText_Failed.text = "스테이지 " + CurrentStage;

        timeSlider_Failed.maxValue = 3;
        timeSlider_Failed.value = 3;

        while(timeSlider_Failed.value > 0) {
            timeSlider_Failed.value -= Time.unscaledDeltaTime;
            yield return null;
        }
        ResultTab_Failed.SetActive(false);
        SetStageBeforeEntering();
    }

    public int setEnemyNumber()
    {
        // 리마스터 원정: 10 + 스테이지/5, 최대 30마리 (예전: 10 + 스테이지, 끝없이 늘어남)
        return Mathf.CeilToInt(RemasterBalance.EnemyCount(CurrentStage) * ExpeditionZones.CountMul(CurrentStage) * AugmentManager.DebugSpawnMul);
    }

    public void setEnemyScale()
    {
        // 구간 안에서 조금씩 빨라지고(최대 +2%) 스턴 저항이 올라가요 (예전 120스테이지 톱니 대신 구간 기준)
        EnemyScale = (ExpeditionZones.StageInZone(CurrentStage) - 1) * 0.02f;
    }

    // 구간(행성)마다 적 4종: 앞 25스테이지는 1·2번, 뒤 25스테이지는 3·4번. 보스는 10스테이지마다 중간 보스, 관문은 관문 보스
    public void setEnemyIndex()
    {
        int zone = ExpeditionZones.ArtZone(ExpeditionZones.Zone(CurrentStage));
        int half = ExpeditionZones.StageInZone(CurrentStage) > ExpeditionZones.ZoneLength / 2 ? 2 : 0;
        int first = zone * ExpeditionZones.EnemiesPerZone + half;
        CurrentEnemyIndex[0] = Mathf.Clamp(first, 0, enemyPrefab.Length - 1);
        CurrentEnemyIndex[1] = Mathf.Clamp(first + 1, 0, enemyPrefab.Length - 1);

        int boss = zone * ExpeditionZones.BossesPerZone + (ExpeditionZones.IsGate(CurrentStage) ? 1 : 0);
        CurrentBossIndex = Mathf.Clamp(boss, 0, bossPrefab.Length - 1);
    }

    // 구간 배경 + 차원 색조
    void ApplyZoneVisuals()
    {
        var gm = GridManager.instance;
        if(gm == null || gm.grids == null || gm.grids.Length == 0) return;
        int zone = Mathf.Min(ExpeditionZones.ArtZone(ExpeditionZones.Zone(CurrentStage)), gm.grids.Length - 1);
        gm.ActiveGrid(zone);
        ExpeditionZones.TintBackground(gm.grids[zone], ExpeditionZones.StageTint(CurrentStage));
    }

    // 새 구간·새 차원에 들어오면 알려 줘요
    void AnnounceZone()
    {
        if(AugmentManager.Instance == null || AugmentManager.Instance.UI == null) return;
        if(ExpeditionZones.StageInZone(CurrentStage) != 1) return;
        var ui = AugmentManager.Instance.UI;
        ui.Toast(ExpeditionZones.ZoneLabel(CurrentStage), new Color(0.75f, 0.9f, 1f), true);
        if(ExpeditionZones.Zone(CurrentStage) == 0 && ExpeditionZones.Dimension(CurrentStage) >= 2)
            foreach(var r in ExpeditionZones.Rules(CurrentStage))
                ui.Toast(ExpeditionZones.RuleName(r), new Color(1f, 0.6f, 0.5f));
    }

    public void setSpawnSpeed()
    {
        SpawnSpeed = RemasterBalance.SpawnSpeed(CurrentStage) * ExpeditionZones.SpawnMul(CurrentStage);
    }

    public void ResumeInfiniteStage()
    {
        SoundManager.Instance.playMusic(SoundManager.MusicType.MainTitle);
        GameManager.instance.loadGameSpeedandAuto();

        ResultTab_Cleared.SetActive(false);
        ResultTab_Failed.SetActive(false);


        KilledEnemy = tempEnemy;
        SpawnedEnemy = KilledEnemy;
        playerHealth = tempHealth;
        BossSpawned = false;
        setEnemyIndex();
        setSpawnSpeed();
        setEnemyScale();
        StartSpawningEnemy();

        HealthText.text = playerHealth.ToString();
        RemainedMonsterText.text = (setEnemyNumber() - KilledEnemy).ToString();
        stageText.text = CurrentStage.ToString();
    }

    public void PauseInfiniteStage()
    {
        tempEnemy = KilledEnemy;
        tempHealth = playerHealth;
        EnemyManager.Instance.removeAllEnemies();
        StopAllCoroutines();
        InBattle = false;
    }

    public void LoadData(GameData data)
    {
        CurrentStage = data.StageIndex;
    }

    public void SaveData(GameData data)
    {
        data.StageIndex = CurrentStage;
    }
}
