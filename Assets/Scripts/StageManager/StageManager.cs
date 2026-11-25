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
    //  (2026-11-25 사용자: 적 수를 맵마다 늘려서 무기가 세지는 만큼 쓸어 담는 맛) 최소 수 = planet_enemy_min(60) × planet_enemy_growth(1.25) ^ 맵 번호
    //  → x-1 60, x-4 117, x-8 286
    //  (2026-11-25 차원) dim: -1 이면 지금 상태로 (그 행성이 지금 행성이고 심연으로 들어가면 심연). 규칙 '무리'면 × 1.5 (최대 수 뒤에 곱해요)
    public static int EnemyTargetOf(Planet p, StageData s, int dim = -1)
    {
        if(s == null) return 0;
        int m = p != null ? Mathf.Max(0, System.Array.IndexOf(p.StagesOfThisPlanet, s)) : 0;
        int pi = p != null && instance != null ? Mathf.Max(0, instance.planets.IndexOf(p)) : 0;
        if(dim < 0) dim = PlanetDimension.Of(pi, instance != null && instance.AbyssMode && p == instance.currentPlanet);
        // (2026-11-25 모든 행성 같은 공식) 60 × (1 + 행성 번호 × planet_enemy_per_planet 0.1) × 1.25 ^ 맵 번호, 최대 planet_enemy_cap(600)
        //  예전 맵 데이터 수(5~2,000마리, 들쭉날쭉)는 안 써요. 쓰려면 planet_use_data_count = 1
        float want = Tuning.Get("planet_enemy_min", 60f) * (1f + pi * Tuning.Get("planet_enemy_per_planet", 0.1f)) * Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_enemy_growth", 1.25f)), m);
        want = Mathf.Min(want, Tuning.Get("planet_enemy_cap", 600f));
        if(Tuning.Get("planet_use_data_count", 0f) > 0f) want = Mathf.Max(s.EnemiesToKill, want);
        want *= PlanetDimension.RuleCount(PlanetDimension.Rules(pi, m, dim));
        return NiceCount(want);
    }

    // 깔끔한 숫자로 반올림 (2026-11-25 사용자: 185 → 200, 230 → 250, 285 → 300, 1050 → 1000)
    //  앞 두 자리를 0 이나 5 로: 100 미만은 5 단위, 100~999 는 50 단위, 1000~9999 는 500 단위 …
    //  → 60 · 75 · 95 · 100 · 150 · 200 · 250 · 300
    static int NiceCount(float n)
    {
        if(n <= 0f) return 0;
        int digits = Mathf.FloorToInt(Mathf.Log10(n)) + 1;
        float step = 5f * Mathf.Pow(10f, Mathf.Max(0, digits - 2));
        return Mathf.Max(5, Mathf.RoundToInt(n / step) * (int)step);
    }
    int EnemyTarget => EnemyTargetOf(currentPlanet, currentStage);
    public int PlanetEnemyTarget => EnemyTarget;

    // 적 종류 하나가 나오는 간격 (2026-11-25): 맵 하나를 planet_map_seconds(45초) + 맵 번호 × planet_map_seconds_step(6초) 동안 내보내게
    //  (예전 맵 데이터 간격은 맵마다 0.5~8초로 들쭉날쭉). 대군 웨이브가 맵 적 수의 일부를 가져가서 실제로는 조금 더 빨리 끝나요
    float PlanetSpawnInterval(int i)
    {
        if(Tuning.Get("planet_use_data_count", 0f) > 0f) return currentStage.SpawnSpeed[i] / SpawnSpeedup;
        int types = Mathf.Max(1, currentStage.enemiesPrefab.Length);
        float seconds = MapSecondsOf(PlanetMapIndex, CurrentDim);
        return Mathf.Max(0.05f, seconds * types / Mathf.Max(1, EnemyTarget) * PlanetDimension.RuleSpawn(CurrentRules));   // 규칙 '광란': 더 빨리
    }

    // ---- 행성 첫 맵들은 쉽게 (2026-11-25 사용자: "3-1 을 하는데 애들이 한 방에 죽는데 너무 몰려와서 잡을 수가 없어. 일반 차원 x-1·x-2 는 좀 쉽게")
    //  첫 맵은 레벨 0에 행성 무기 하나라 화력이 적어요 → 적 수는 그대로(레벨업 횟수 그대로), 더 천천히 나오고 대군은 덜
    //  일반 차원만: x-1 나오는 시간 × planet_easy_time_1(1.6), 대군 planet_easy_horde_1(0)번 / x-2 × planet_easy_time_2(1.3), 대군 planet_easy_horde_2(1)번
    public static float MapSecondsOf(int m, int dim)
    {
        float sec = Tuning.Get("planet_map_seconds", 45f) + m * Tuning.Get("planet_map_seconds_step", 6f);
        if(dim == PlanetDimension.Normal) {
            if(m == 0) sec *= Mathf.Max(1f, Tuning.Get("planet_easy_time_1", 1.6f));
            else if(m == 1) sec *= Mathf.Max(1f, Tuning.Get("planet_easy_time_2", 1.3f));
        }
        return sec;
    }
    public static int HordeCountOf(int m, int dim)
    {
        int n = Mathf.Max(0, Tuning.GetInt("planet_horde_count", 2));
        if(dim == PlanetDimension.Normal) {
            if(m == 0) n = Mathf.Min(n, Mathf.Max(0, Tuning.GetInt("planet_easy_horde_1", 0)));
            else if(m == 1) n = Mathf.Min(n, Mathf.Max(0, Tuning.GetInt("planet_easy_horde_2", 1)));
        }
        return n;
    }

    // ---- 차원 (2026-11-25, PlanetDimension.cs): 평행 = 15~28번 행성, 심연 = 다 깬 평행 행성에서 고르면 (행성 지도 화면이 정해 줘요)
    public bool AbyssMode;
    public int PlanetIndex => currentPlanet == null ? -1 : planets.IndexOf(currentPlanet);
    public int CurrentDim => PlanetDimension.Of(Mathf.Max(0, PlanetIndex), AbyssMode);
    public List<ExpeditionZones.Rule> CurrentRules => PlanetDimension.Rules(Mathf.Max(0, PlanetIndex), PlanetMapIndex, CurrentDim);
    public StageData CurrentStageData => currentStage;
    public static bool AllCleared(Planet p)
    {
        if(p == null) return false;
        foreach(var s in p.StagesOfThisPlanet) if(!s.StageCleared) return false;
        return true;
    }

    // 관문 보스 패턴 '소환' (행성): 이 맵의 일반 적을 n 마리 (GateBossAbilities)
    public void PlanetSummon(int n)
    {
        if(!InBattle || currentStage == null || currentStage.enemiesPrefab.Length == 0) return;
        for(int i = 0; i < n; i++) {
            int k = Random.Range(0, currentStage.enemiesPrefab.Length);
            EnemyManager.Instance.createEnemy(currentStage.enemiesPrefab[k], currentStage.enemyScale[k]);
        }
    }

    // 행성 난이도 = 원정 몇 스테이지 수준 (2026-11-25 사용자 결정: 원정 곡선에 맞추기)
    //  = planet_stage_start(5) + 행성 번호 × planet_stage_per_planet(12) + 맵 번호 × planet_stage_per_map(1.5)
    //  1번 행성 5~15, 2번 17~27 … 10번 약 113~123. 적·보스 체력·방어력은 원정과 같은 공식 (EnemyManager.PlanetStats)
    public int PlanetEquivStage {
        get {
            if(currentPlanet == null || currentStage == null) return 0;
            int p = Mathf.Max(0, planets.IndexOf(currentPlanet));
            int m = Mathf.Max(0, System.Array.IndexOf(currentPlanet.StagesOfThisPlanet, currentStage));
            return EquivStageOf(p, m);
        }
    }
    public static int EquivStageOf(int p, int m) => Mathf.Max(1, Mathf.RoundToInt(Tuning.Get("planet_stage_start", 5f) + p * Tuning.Get("planet_stage_per_planet", 12f) + m * Tuning.Get("planet_stage_per_map", 1.5f)));

    // 골드를 셀 스테이지 (2026-11-25): 평행·심연은 적 체력이 몇십~몇만 배라 깰 즈음엔 원정이 훨씬 깊어요 → 체력이 같은 원정 스테이지로 세요
    //  적 체력 ∝ (1 + S/10)^4 이라 체력 × k 인 스테이지 = 10 × ((1 + S/10) × k^(1/4) − 1). 일반 행성은 그대로
    public static int GoldStageOf(int p, int m, int dim)
    {
        int s = EquivStageOf(p, m);
        float k = PlanetDimension.HpMul(dim, p) * PlanetDimension.RuleHp(PlanetDimension.Rules(p, m, dim));
        if(k <= 1f) return s;
        return Mathf.Max(s, Mathf.RoundToInt(10f * ((1f + s / 10f) * Mathf.Pow(k, 0.25f) - 1f)));
    }

    // 맵 보상 (결과 창과 행성 지도 미리보기가 같이 써요, 2026-11-25 차원)
    //  골드 = max(맵 데이터, 원정 그 난이도 스테이지 클리어 골드 × planet_clear_gold_stages(6)) × 차원 배율(심연 5) × 규칙 '강철' 1.3
    //  강화석 = 맵 데이터 × 차원 배율, 첫 클리어 다이아 = 맵 데이터 × 차원 배율. 반복하면 골드 −20%·강화석 −50%씩 (최소 30%)
    public void RewardOf(int p, int m, int dim, out long gold, out int stone, out int dia, out bool first)
    {
        gold = 0; stone = 0; dia = 0; first = false;
        if(p < 0 || p >= planets.Count) return;
        var maps = planets[p].StagesOfThisPlanet;
        if(m < 0 || m >= maps.Length) return;
        var s = maps[m];
        var aug = AugmentManager.Instance;
        bool abyss = dim == PlanetDimension.Abyss && aug != null;
        int clears = abyss ? aug.AbyssClearCount(p, m) : s.ClearedTime;
        float rmul = PlanetDimension.RewardMul(dim);
        long baseGold = System.Math.Max(s.goldPrice, (long)(RemasterBalance.StageClearGold(GoldStageOf(p, m, dim)) * Tuning.Get("planet_clear_gold_stages", 30f)));
        // (2026-11-25 사용자: 깰 때마다 같은 골드를 계속 주고 실패 골드를 줄이자. 반복도 노가다라) 골드는 반복해도 그대로 (planet_gold_repeat_decay 0, 예전 0.2).
        //  강화석은 장비 성장 재료라 반복할수록 줄어드는 걸 그대로 둬요
        gold = (long)(baseGold * rmul * PlanetDimension.RuleGold(PlanetDimension.Rules(p, m, dim)) * Mathf.Max(0.3f, 1f - (Tuning.Get("planet_gold_repeat_decay", 0f) * clears)));
        stone = (int)(s.stonePriceAmount * rmul * Mathf.Max(0.3f, 1f - (0.5f * clears)));
        dia = Mathf.RoundToInt(s.DiamondPrice * rmul);
        first = abyss ? !aug.AbyssMapCleared(p, m) : !s.StageCleared;
    }
    public bool PlanetBattle => InBattle;
    public int PlanetMapIndex => currentPlanet == null || currentStage == null ? 0 : Mathf.Max(0, System.Array.IndexOf(currentPlanet.StagesOfThisPlanet, currentStage));

    // 행성 안 뒤쪽 맵일수록 더 단단하게 (2026-11-25 사용자: 증강이 맵마다 이어지니까 뒤쪽 맵은 어렵게, x-1 은 무난하게. 막 고르면 질 수 있게)
    //  체력 × planet_map_hp_growth(1.35) ^ 맵 번호 → x-1 ×1, x-4 ×2.5, x-8 ×8.2. 증강이 쌓여 합체까지 가면 첫 맵보다 약 8~10배 세져요 (계산)
    //  (2026-11-25 사용자: 적이 많아지는 만큼 1마리는 덜 단단하게) 1.35 → 1.12. 맵 하나의 전체 체력은 1.25 × 1.12 = 1.4배씩 늘어요
    // 앞쪽 행성은 쉽게 (2026-11-25 사용자: 행성 2 는 초반이니 쉽게 깨게) = planet_early_hp_base(0.35) + 행성 번호 × planet_early_hp_step(0.15), 최대 1
    //  → 1번 ×0.35, 2번 ×0.5, 3번 ×0.65, 4번 ×0.8, 5번 ×0.95, 6번부터 ×1
    public float PlanetEarlyHpMul => Mathf.Min(1f, Tuning.Get("planet_early_hp_base", 1f) + Mathf.Max(0, planets.IndexOf(currentPlanet)) * Tuning.Get("planet_early_hp_step", 0.15f));
    public float PlanetMapHpMul => Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_map_hp_growth", 1f)), PlanetMapIndex);
    //  나오는 간격: (늘린 수 / 원래 수) ^ planet_spawn_speedup_pow(0.8) 배 빠르게, 최대 planet_spawn_speedup_max(6)배 (맵이 너무 길어지지 않게)
    float SpawnSpeedup => currentStage == null || currentStage.EnemiesToKill <= 0 ? 1f : Mathf.Clamp(Mathf.Pow(EnemyTarget / (float)currentStage.EnemiesToKill, Tuning.Get("planet_spawn_speedup_pow", 0.8f)), 1f, Mathf.Max(1f, Tuning.Get("planet_spawn_speedup_max", 4f)));

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
            for(int j = 0; j < planets[i].StagesOfThisPlanet.Length; j++) {   // (2026-11-25 버그) 예전엔 planets[j] 였어요
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
            if(SpawningEnemy || currentStage.bossPrefab.Length != 0) TryHorde();
            TickHorde();
            // (2026-11-25 사용자: 2-6 을 못 깨요) 적이 다 나와서 더 안 나오는데 화면에 적이 없고 처치 수만 모자라면 (처치로 안 센 죽음)
            //  2초 기다렸다가 콘솔에 남기고 다 잡은 걸로 쳐요
            if(!SpawningEnemy && !BossSpawned && KilledEnemy < EnemyTarget && EnemyManager.Instance != null && EnemyManager.Instance.enemyData.Count == 0) {
                stuckTimer += Time.deltaTime;
                if(stuckTimer > 2f) {
                    Debug.Log($"[행성] {currentPlanet?.PlanetName} {currentStage?.StageName}: 적이 더 안 나오는데 처치 {KilledEnemy}/{EnemyTarget} (나온 적 {SpawnedEnemy}) → 다 잡은 걸로 쳐요");
                    KilledEnemy = EnemyTarget;
                    stuckTimer = 0f;
                }
            } else stuckTimer = 0f;
        }
    }
    float stuckTimer;

    // 대군 웨이브 (2026-11-25 사용자: 한 번에 쓸어 담는 맛) 맵 진행(나온 적)이 1/3·2/3 (planet_horde_count 2) 이 되면
    //  "대군 출현!" 과 함께 위나 아래에서 맵 적 수 × planet_horde_share(20%) (10~60마리)가 한꺼번에. 맵 적 수 안에서 나와요 (보스 맵은 계속)
    //  진행률은 일반 적만으로 세요
    int hordeDone, hordeReserved;
    int HordeSize => Mathf.Clamp(Mathf.RoundToInt(EnemyTarget * Tuning.Get("planet_horde_share", 0.2f)), Tuning.GetInt("planet_horde_min", 10), Tuning.GetInt("planet_horde_max", 60));
    void TryHorde()
    {
        int count = HordeCountOf(PlanetMapIndex, CurrentDim);   // 첫 맵들은 대군이 덜 (2026-11-25)
        if(hordeDone >= count || currentStage == null || currentStage.enemiesPrefab.Length == 0) return;
        // 진행률은 일반 적만으로 세요 (2026-11-25 버그: 대군 몫을 미리 떼어 두면 60마리 맵에선 두 번째 대군이 바로 이어 나왔어요)
        int normalTotal = Mathf.Max(1, EnemyTarget - count * HordeSize);
        if(SpawnedEnemy - hordeReserved < normalTotal * (hordeDone + 1) / (count + 1)) return;
        hordeDone++;
        int n = HordeSize;
        if(currentStage.bossPrefab.Length == 0) n = Mathf.Min(n, EnemyTarget - SpawnedEnemy);
        if(n <= 0) return;
        hordeReserved += n;
        SpawnedEnemy += n;   // 맵 적 수에서 미리 떼어 둬요 (경고 동안 일반 적이 그 몫까지 채우지 않게)
        if(SpawnedEnemy >= EnemyTarget && currentStage.bossPrefab.Length == 0) SpawningEnemy = false;
        StartCoroutine(HordeRoutine(n));
    }

    // (2026-11-25 도파민) 위나 아래에서만 (세로 화면이라 멀리서 몰려오는 게 보여요), 경고 planet_horde_delay(1초) 뒤에.
    //  대군을 다 잡으면 "대군 섬멸!" + 레벨 경험치 보너스 (AugmentManager.OnPlanetHordeCleared)
    readonly List<Enemy> hordeEnemies = new List<Enemy>();
    bool hordeWatching;
    IEnumerator HordeRoutine(int n)
    {
        int side = Random.value < 0.5f ? 2 : 4;   // 2 = 위, 4 = 아래 (EnemyManager.RandomLocation)
        var ui = AugmentManager.Instance != null ? AugmentManager.Instance.UI : null;
        if(ui != null) ui.Toast(side == 2 ? "위쪽에서 대군 출현!" : "아래쪽에서 대군 출현!", new Color(1f, 0.45f, 0.35f), true);
        yield return new WaitForSeconds(Mathf.Max(0f, Tuning.Get("planet_horde_delay", 1f)));
        if(!InBattle) yield break;
        var em = EnemyManager.Instance;
        em.BeginSpawnGroup(Tuning.Get("planet_horde_spread", 2.5f), side);
        for(int i = 0; i < n; i++) {
            int k = i % currentStage.enemiesPrefab.Length;
            em.createEnemy(currentStage.enemiesPrefab[k], currentStage.enemyScale[k]);
            if(em.enemyData.Count > 0) hordeEnemies.Add(em.enemyData[em.enemyData.Count - 1]);
        }
        em.EndSpawnGroup();
        hordeWatching = hordeEnemies.Count > 0;
    }

    void TickHorde()
    {
        if(!hordeWatching) return;
        foreach(var e in hordeEnemies)
            if(e != null && e.gameObject.activeInHierarchy && e._state != Enemy.EnemyState.dead) return;   // 아직 남았어요
        hordeWatching = false;
        hordeEnemies.Clear();
        if(!InBattle) return;
        var aug = AugmentManager.Instance;
        if(aug != null && aug.UI != null) aug.UI.Toast("대군 섬멸!", new Color(1f, 0.85f, 0.35f), true);
        if(aug != null) aug.OnPlanetHordeCleared();
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
        battleStartTime = Time.time;
        InBattle = true;
        SpawningEnemy = true;
        for(int i = 0; i < currentStage.enemiesPrefab.Length; i++) {
            StartCoroutine(spawnEnemy(currentStage.enemiesPrefab[i], PlanetSpawnInterval(i), currentStage.enemyScale[i]));
        }
    }

    IEnumerator spawnEnemy(Enemy e, float SpawningTime, float Scale)
    {
        while(SpawningEnemy) {

            // 보스가 나온 뒤엔 일반 적이 덜 나와요 (2026-11-25 사용자: 보스 맵은 계속 나오되 보스 때는 스폰율을 좀 줄여서)
            yield return new WaitForSeconds(SpawningTime * (BossSpawned ? Mathf.Max(1f, Tuning.Get("planet_boss_spawn_slow", 2.5f)) : 1f));
            if(!SpawningEnemy) yield break;   // (2026-11-25 버그) 기다리는 동안 다른 종류·대군이 다 채웠으면 그만 (예전엔 목표보다 몇 마리 더 나왔어요)
            if(SpawnedEnemy >= EnemyTarget && currentStage.bossPrefab.Length == 0) { SpawningEnemy = false; yield break; }
            SpawnedEnemy++;
            EnemyManager.Instance.createEnemy(e, Scale);
            // 보스 없는 맵은 정해진 수만큼만, 보스 맵은 보스를 잡을 때까지 일반 적이 계속 나와요
            //  (2026-11-25 사용자: 보스 맵은 일반 몹이 계속 나오는 게 더 어렵고 재밌어요)
            if(SpawnedEnemy >= EnemyTarget && currentStage.bossPrefab.Length == 0) {
                SpawningEnemy = false;   // (2026-11-25 버그) 예전엔 StopAllCoroutines() 라 대군 웨이브 코루틴까지 멈췄어요. 다른 종류는 다음 차례에 스스로 멈춰요
                yield break;
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
        // (2026-11-25 3-1 원인) 수동 조준이면 기본총이 적이 아니라 손가락(마우스) 쪽으로 쏴서 폭격기만 잡았어요 → 행성은 자동 조준으로 시작 (planet_force_auto 1)
        if(Tuning.Get("planet_force_auto", 1f) > 0f) WeaponManager.instance.shootType = WeaponManager.ShootType.AutoShoot;

        //Stop Infinite Stage
        InfiniteStage.Instance.PauseInfiniteStage();

        // 남아 있던 적을 치워요 (2026-11-25: 전투 전에 남은 적이 닿아 지는 일)
        if(EnemyManager.Instance != null) {
            int left = EnemyManager.Instance.enemyData.Count + EnemyManager.Instance.bossData.Count;
            if(left > 0) Debug.Log($"[행성] 들어갈 때 남아 있던 적 {left}마리를 치웠어요");
            EnemyManager.Instance.removeAllEnemies();
        }

        //Default the values
        KilledEnemy = 0;
        hordeDone = 0;
        hordeReserved = 0;
        hordeEnemies.Clear();
        hordeWatching = false;
        SpawnedEnemy = 0;
        BossSpawned = false;
        WarningObject.SetActive(false);

        await Task.Delay(1500);

        GridManager.instance.ToPlanet(currentPlanet.GridIndex);
        planetScreen = true;
        RemainedMonster_Text.text = (EnemyTarget - KilledEnemy).ToString();
        await Task.Delay(2000);
        StartSpawningEnemy();
        // 차원 알림 (2026-11-25): "평행 우주 · 질주: 적 이동속도 +30%"
        if(CurrentDim != PlanetDimension.Normal && AugmentManager.Instance != null && AugmentManager.Instance.UI != null)
            AugmentManager.Instance.UI.Toast($"{PlanetDimension.Name(CurrentDim)}\n{PlanetDimension.RulesText(CurrentRules)}", PlanetDimension.Accent(CurrentDim), true);

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

    // 실패 진단 (2026-11-25 사용자: 3-1 을 못 깨요 — 시뮬레이션으론 쉬운데 실제론 11~17마리만 잡혀서 원인 찾기)
    public void DiedBy(Enemy who)
    {
        if(who != null) {
            int alive = 0, inRange = 0;
            if(EnemyManager.Instance != null) foreach(var e in EnemyManager.Instance.enemyData) if(e != null && e.gameObject.activeInHierarchy) { alive++; if(e.IsInRange) inRange++; }
            var ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
            float trip = Time.time - who.spawnTime;
            Debug.Log($"[행성 진단] 전투 {Time.time - battleStartTime:0.0}초째, 화면의 적 {alive}마리(사거리 안 {inRange}), 닿은 적: 종류 {who._typeofEnemy}, 나온 자리 {who.spawnPos} → 우주선 {ship}, {trip:0.0}초 걸림 (속도 {who.enemyMovingSpeed:0.00}, 평균 {Vector3.Distance(who.spawnPos, ship) / Mathf.Max(0.01f, trip):0.00}칸/초), 게임 속도 {Time.timeScale:0.##}");
        }
        Died();
    }
    float battleStartTime;

    public void Died()
    {
        Debug.Log($"[행성] {currentPlanet?.PlanetName} {currentStage?.StageName}: 실패 — 적이 우주선에 닿았어요 (처치 {KilledEnemy}/{EnemyTarget}, 나온 적 {SpawnedEnemy}, 보스 {(BossSpawned ? "나옴" : "아직")}, 행성 Lv.{(AugmentManager.Instance != null ? AugmentManager.Instance.PlanetLevel : 0)})");
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
        // (2026-11-25 사용자: 차원 보상은 확실하게)
        //  골드 = max(맵 데이터, 원정 그 난이도 스테이지 클리어 골드 × planet_clear_gold_stages(6)) × 차원 배율 × 규칙 '강철' 1.3
        //  심연은 골드·강화석·첫 클리어 다이아 × planet_abyss_reward(5). 반복하면 줄어드는 건 같아요 (심연은 따로 세요)
        int dim = CurrentDim, pIdx = Mathf.Max(0, PlanetIndex), mIdx = PlanetMapIndex;
        var aug = AugmentManager.Instance;
        bool abyss = dim == PlanetDimension.Abyss && aug != null;
        RewardOf(pIdx, mIdx, dim, out long PriceGold, out int PriceStone, out int dia, out bool firstClear);
        if(!a) {   // 실패는 planet_fail_reward(30%)만 (골드가 원정 기준으로 커져서, 2026-11-25)
            float f = Mathf.Clamp01(Tuning.Get("planet_fail_reward", 0.1f));   // (2026-11-25 사용자) 30% → 10%
            PriceGold = (long)(PriceGold * f);
            PriceStone = (int)(PriceStone * f);
        }
        bool wasFull = AllCleared(currentPlanet);
        GoldText.GetComponentInChildren<TextMeshProUGUI>().text = GameManager.instance.MoneyStringForInfiniteStage(PriceGold);
        GameManager.SetMoney(PriceGold);
        rd_Money = PriceGold;

        stoneObjs[(int)currentStage.stonePrice].SetActive(true);
        stoneObjs[(int)currentStage.stonePrice].GetComponentInChildren<TextMeshProUGUI>().text = PriceStone.ToString();
        UpgradeStoneManager.instance.addStone(currentStage.stonePrice, PriceStone);
        rd_Stone = PriceStone;

        if(a && firstClear) {
            DiamondText.gameObject.SetActive(true);
            DiamondText.GetComponentInChildren<TextMeshProUGUI>().text = dia.ToString();
            GameManager.SetDiamond(dia);
            rd_Diamond = dia;
            if(!abyss) currentStage.StageCleared = true;
        } else {
            rd_Diamond = 0;
        }

        //ClearedTime ++
        if(a) {
            var maps = currentPlanet.StagesOfThisPlanet;
            bool last = maps.Length > 0 && maps[maps.Length - 1] == currentStage;
            if(abyss) aug.MarkAbyssClear(pIdx, mIdx, last);   // 심연 클리어는 따로 저장, 마지막 맵이면 정복
            else currentStage.ClearedTime++;
            // 평행 행성을 처음 다 깼어요 → 보너스 (다이아 planet_parallel_bonus_dia + 별의 먼지 = 그 난이도 환생 몫 × planet_parallel_bonus_dust)
            if(!abyss && dim == PlanetDimension.Parallel && !wasFull && AllCleared(currentPlanet) && aug != null) aug.GiveParallelBonus(pIdx, PlanetEquivStage);
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
