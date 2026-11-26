using System;
using System.Collections.Generic;
using UnityEngine;

// 리마스터 핵심: 원정(무한 스테이지) 증강 / 축복 / 리롤 / 출격 무기 / 환생 / 기존 유저 보상
// 씬을 건드리지 않도록 게임 시작 시 코드로 생성돼요(Boot).
public partial class AugmentManager : MonoBehaviour, IDataPersistence
{
    public static AugmentManager Instance { get; private set; }

    public const int PullInterval = 5;
    public const int FreeRerollsPerDay = 3;
    public const int AdRerollsPerDay = 5;
    public const int DiamondRerollCost = 30;
    const float TreasureShare = 0.15f;
    const int PityLimit = 8;

    // 환생 트리: 0 행운, 1 출발 가속, 2 첫 선물
    // 환생 트리 (설계서 3-2): 4갈래 11칸. 번호는 저장 데이터 순서라 바꾸지 말고 뒤에만 추가해요.
    public static readonly string[] TreeNames = { "행운", "출발 가속", "첫 선물", "별의 가호", "리롤 주머니", "예비 실드", "관문의 선물", "먼지 수집", "가벼운 환생", "천장 단축", "보스 사냥꾼" };
    public static readonly int[] TreeMax = { 10, 5, 3, 99, 3, 2, 1, 5, 4, 2, 5 };
    static readonly int[] TreeBaseCost = { 5, 20, 40, 0, 30, 60, 80, 25, 50, 60, 15 };
    // 갈래: 0 증강, 1 출발, 2 원정, 3 보상
    public static readonly int[] TreeBranch = { 0, 1, 1, 3, 0, 2, 2, 3, 3, 0, 2 };
    public static readonly string[] BranchNames = { "증강", "출발", "원정", "보상" };
    // 먼저 찍어야 하는 칸 (-1 = 없음)
    public static readonly int[] TreeRequires = { -1, -1, 1, -1, 0, -1, 5, 3, 7, 4, 6 };
    public const int StarPowerNode = 3;
    int T(int node) => node < tree.Length ? tree[node] : 0;
    public bool TreeUnlocked(int node) => TreeRequires[node] < 0 || T(TreeRequires[node]) > 0;
    public float BossDamageMul => 1f + 0.1f * T(10);
    // 별의 가호: 원정 피해 ×1.15 / 레벨 (환생할수록 확실히 강해지는 축)
    // 최대 레벨을 넘게 찍어 둔 예전 저장은 효과만 최대까지 (레벨은 그대로 둬요)
    public float StarPowerMultiplier => Mathf.Pow(RemasterBalance.StarPowerPerLevel, Mathf.Min(RemasterBalance.StarPowerMax, tree.Length > StarPowerNode ? tree[StarPowerNode] : 0));
    public static int TreeMaxOf(int node) => node == StarPowerNode ? Mathf.Min(TreeMax[node], RemasterBalance.StarPowerMax) : TreeMax[node];

    RemasterUI ui;
    bool loaded;
    bool pendingMigrationJump;
    long migrationGift;
    bool pendingRebirthStart;
    int rebirthStartStage;
    bool debugAllBlessings;

    // ---- 원정 상태 (저장)
    readonly Dictionary<string, int> levels = new Dictionary<string, int>();
    readonly List<string> order = new List<string>();
    long expKills;
    int expBossKills;
    float soulStack, slaughterStack, wraithStack, stardustSizeStack;
    int blessing = -1;
    int rerollBag;
    int pity;
    SideWeapon extraWeapon = SideWeapon.None;
    // 합체로 흡수된 B 무기 → 그 칸에 새로 고른 무기 (None = 아직 안 고름 / 고를 무기 없음). 원정마다 초기화 (WeaponAugments.cs AbsorbFusionB)
    readonly Dictionary<SideWeapon, SideWeapon> fusionSlots = new Dictionary<SideWeapon, SideWeapon>();
    bool loadoutSet;
    SideWeapon loadoutLeft = SideWeapon.None, loadoutRight = SideWeapon.None;
    bool pendingSetup;
    string freeRerollDate = "";
    int freeRerollsUsed, adRerollsUsed;

    // ---- 영구 상태 (저장)
    long stardust;
    int[] tree = new int[TreeNames.Length];
    int bestStage;
    int rebirthCount;

    // ---- 뽑기 진행
    int pendingPulls;
    bool pullShowing;
    readonly List<AugmentDef> shownAugs = new List<AugmentDef>();   // 고를 수 있는 카드 (2026-11-25: 3장 중 하나)
    readonly List<bool> shownRerolled = new List<bool>();          // 카드 자리마다 리롤은 한 번만 (2026-11-25 사용자)


    // ---- 화면 일시정지 (UI용)
    int pauseCount;
    float pausedScale = 1f;

    #region 생성 / 등록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(Instance != null)
            return;
        if(FindAnyObjectByType<DataPersistenceManager>() == null)
            return; // 게임 씬이 아니면 만들지 않음
        var go = new GameObject("RemasterSystems");
        go.AddComponent<AugmentManager>();
    }

    void Awake()
    {
        if(Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ui = gameObject.AddComponent<RemasterUI>();
        gameObject.AddComponent<OfflineProgress>();
        DataPersistenceManager.Register(this);
    }
    #endregion

    #region 조회용 값
    public bool Loaded => loaded;
    public long Stardust => stardust;
    public int BestStage => bestStage;
    public int RebirthCount => rebirthCount;
    public int Blessing => blessing;
    public SideWeapon LoadoutLeft => loadoutLeft;
    public SideWeapon LoadoutRight => loadoutRight;
    public SideWeapon ExtraWeapon => extraWeapon;
    public long ExpeditionKills => expKills;

    // 오프라인 진행으로 넘은 스테이지의 처치·보스 수 (누적 증강에 반영)
    public void AddOfflineKills(long kills, int bosses)
    {
        expKills += kills;
        expBossKills += bosses;
    }

    public bool IsLoaded => loaded;

    // ---- 신의 관문 (처음 이긴 관문 번호 = 스테이지/50)
    readonly HashSet<int> clearedGates = new HashSet<int>();
    public bool IsGateCleared(int gateNumber) => clearedGates.Contains(gateNumber);
    public static long GateDiamondReward(int gateNumber) => 100 + 20L * (gateNumber - 1);

    public void OnGateCleared(int stage)
    {
        int gate = ExpeditionZones.GateNumber(stage);
        if(T(6) > 0) QueuePull(1);   // 환생 트리 "관문의 선물": 관문을 이길 때마다 증강 1개
        string god = ExpeditionZones.PlanetName(ExpeditionZones.Zone(stage)) + "의 신";
        if(!clearedGates.Add(gate)) {
            ui.Toast($"{god} 처치!", new Color(1f, 0.85f, 0.4f));
            return;
        }
        long dia = GateDiamondReward(gate);
        GameManager.SetDiamond((int)Mathf.Min(int.MaxValue, dia));
        ui.Toast($"신의 관문 첫 돌파! {god} 처치\n다이아 +{dia}", new Color(1f, 0.85f, 0.4f), true);
    }

    // 예전 세이브: 깬 행성은 그 구간의 관문도 깬 것으로 (설계서 2-2)
    void MigrateGatesFromPlanets()
    {
        var sm = StageManager.instance;
        if(sm == null || sm.planets == null) return;
        for(int z = 0; z < ExpeditionZones.ArtZoneCount && z < sm.planets.Count; z++)   // 예전 행성 14개만
            if(sm.planets[z] != null && sm.planets[z].PlanetCleared) clearedGates.Add(z + 1);
    }
    public RemasterUI UI => ui;
    public IReadOnlyList<string> OwnedOrder => order;
    public int TreeLevel(int node) => tree[node];

    public int Level(string id) => levels.TryGetValue(id, out int l) ? l : 0;

    int CurrentStage => InfiniteStage.Instance != null ? InfiniteStage.Instance.CurrentStage : 1;

    public bool EffectsActive {
        get {
            if(!loaded) return false;
            if(StageManager.instance != null && StageManager.instance.isInStage)
                return planetSnap != null;   // 행성: 한 판(PlanetRun.cs) 중에만 증강이 켜져요 (2026-11-25)
            if(InfiniteStage.Instance == null || !InfiniteStage.Instance.IsRunning)
                return false;
            return true;
        }
    }

    #endregion

    #region 무기 / 출격
    WeaponBase SideWeaponOf(SideWeapon w) => WeaponManager.instance != null ? WeaponManager.instance.GetSideWeapon(w) : null;

    public int WeaponLevel(SideWeapon w)
    {
        var weapon = SideWeaponOf(w);
        return weapon != null ? weapon.UpgradeLevel : 0;
    }

    GameObject WeaponObject(SideWeapon w)
    {
        var weapon = SideWeaponOf(w);
        return weapon != null ? weapon.gameObject : null;
    }

    public bool Owned(SideWeapon w) => w != SideWeapon.None && WeaponLevel(w) > 0;

    // 출격 중인지 (합체 교체 포함): B 무기로 합체했고 A 가 출격 칸에 없으면, B 대신 A 가 출격해요 (WeaponAugments.cs 양쪽 합체)
    public bool IsEquipped(SideWeapon w)
    {
        if(w == SideWeapon.None) return false;
        foreach(var f in AugmentDefs.Fusions) {
            if(!SwappedIn(f)) continue;
            if(f.b == w) return false;   // 합체 재료로 들어간 B
            if(f.a == w) return Owned(w);   // B 자리에 들어온 A
        }
        return RawEquipped(w);
    }

    // 출격 칸 기준 (합체 교체 전)
    bool RawEquipped(SideWeapon w)
    {
        if(w == SideWeapon.None) return false;
        if(fusionSlots.ContainsKey(w)) return false;            // 합체로 흡수된 B
        if(fusionSlots.ContainsValue(w)) return Owned(w);       // 그 칸에 새로 고른 무기
        if(debugPickMode) return Owned(w) && debugPicked.Contains(w);   // 개발용: 테스트 창에서 고른 무기만
        if(debugEquipAll && Owned(w)) return true;   // 개발용: 가진 무기 전부 출격
        return w == loadoutLeft || w == loadoutRight || w == extraWeapon;
    }

    public List<SideWeapon> OwnedWeapons()
    {
        var list = new List<SideWeapon>();
        foreach(SideWeapon w in Enum.GetValues(typeof(SideWeapon)))
            if(Owned(w)) list.Add(w);
        list.Sort((a, b) => WeaponLevel(b).CompareTo(WeaponLevel(a)));
        return list;
    }

    // 출격 칸 정리: 안 가진 무기·같은 무기는 빼요.
    //  (2026-11-25 사용자: 기본총 하나로 출발 → 정한 스테이지에 무기) 예전엔 빈 칸을 가진 무기로 자동으로 채웠는데,
    //  이제 빈 칸은 TryWeaponSlotPick 에서 스테이지가 되면 카드로 골라요
    void EnsureLoadout()
    {
        if(planetSnap != null) return;   // 행성 한 판: 행성 무기는 원정에서 못 쓰는 무기여도 그대로 (2026-11-25 버그: 쌍발총이 바로 빠졌어요)
        if(!ExpeditionUsable(loadoutLeft)) loadoutLeft = SideWeapon.None;     // 행성을 안 깬 무기는 원정에서 빼요 (2026-11-25)
        if(!ExpeditionUsable(loadoutRight)) loadoutRight = SideWeapon.None;
        if(loadoutLeft == loadoutRight) loadoutRight = SideWeapon.None;
        loadoutSet = true;
    }

    // 행성 능력치 카드 (WeaponBase.HitDamage · RollCrit)
    public static float PlanetCardDamage { get { var m = Instance; return m == null || m.planetSnap == null ? 1f : 1f + AugmentDefs.PlPower(m.Level("pl_power")); } }
    public static float PlanetCardCrit { get { var m = Instance; return m == null || m.planetSnap == null ? 0f : AugmentDefs.PlCrit(m.Level("pl_crit")); } }

    // ---- 무기 칸 열기 (2026-11-25 사용자 결정)
    //  기본총 하나로 출발 → weapon_slot1_stage(10)·weapon_slot2_stage(25)스테이지가 되면 가진 무기 중 무작위 weapon_choice_count(3)장에서 하나를 골라요.
    //  환생 출발 가속으로 그 뒤 스테이지에서 출발하면 출발하자마자 칸 수만큼 차례로 골라요
    public static int WeaponSlotStage(int i) => i == 0 ? Tuning.GetInt("weapon_slot1_stage", 10) : Tuning.GetInt("weapon_slot2_stage", 25);
    public int OpenWeaponSlots => (CurrentStage >= WeaponSlotStage(0) ? 1 : 0) + (CurrentStage >= WeaponSlotStage(1) ? 1 : 0);
    bool weaponPickShowing;
    string weaponPickWhy;   // 칸이 열렸는데 못 띄운 까닭 (바뀔 때만 콘솔에 남겨요, 2026-11-25 "10스테이지에서 무기 카드가 안 떠요" 추적용)
    void WeaponPickSkip(string why)
    {
        if(why == weaponPickWhy) return;
        weaponPickWhy = why;
        if(why != null) Debug.Log($"[무기 칸] {CurrentStage}스테이지: 무기 카드를 못 띄웠어요 — {why} (왼쪽 {loadoutLeft}, 오른쪽 {loadoutRight}, 추가 {extraWeapon})");
    }
    void TryWeaponSlotPick()
    {
        // (2026-11-25) 룰렛은 뜨는데 무기 카드는 안 떠서, 룰렛에 없는 조건(시작 화면·개발용 무기 고르기·전부 출격)을 뺐어요.
        //  이 함수는 LateUpdate 에서 EffectsActive(원정 전투 중, 스테이지 모드 아님)일 때만 불려요
        if(weaponPickShowing || ui.Busy) return;
        if(DebugArena || MeasureScene != null) return;   // 연습장·측정 중엔 안 물어봐요
        int filled = (loadoutLeft != SideWeapon.None ? 1 : 0) + (loadoutRight != SideWeapon.None ? 1 : 0);
        int open = OpenWeaponSlots;
        if(filled >= open) {
            // 10스테이지에 들어왔는데 이미 칸이 다 차 있으면 (예전 규칙으로 시작한 원정) 한 번 남겨요
            if(CurrentStage == WeaponSlotStage(0) && filled > 0) WeaponPickSkip("10스테이지 전에 이미 무기가 장착돼 있어요 (End 로 새 원정을 시작했나요?)");
            return;
        }
        var pool = FreeWeaponsForExtra();   // 가진 무기 중 출격·합체 재료가 아닌 것
        if(pool.Count == 0) { WeaponPickSkip($"고를 무기가 없어요 (산 무기 {OwnedWeapons().Count}개, 그중 행성을 깬 무기만 원정에서 써요)"); return; }
        WeaponPickSkip(null);
        for(int i = pool.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        int n = Mathf.Max(1, Tuning.GetInt("weapon_choice_count", 3));
        if(pool.Count > n) pool.RemoveRange(n, pool.Count - n);
        weaponPickShowing = true;
        PauseForUI();
        ui.ShowWeaponChoice($"새 무기! ({WeaponSlotStage(filled)}스테이지)", pool, w => {
            if(w != SideWeapon.None) {
                if(loadoutLeft == SideWeapon.None) loadoutLeft = w;
                else if(loadoutRight == SideWeapon.None) loadoutRight = w;
            }
            loadoutSet = true;
            weaponPickShowing = false;
            ResumeFromUI();
            ui.RefreshHUD();
        });
    }

    public void SetLoadout(SideWeapon left, SideWeapon right)
    {
        loadoutLeft = left;
        loadoutRight = right == left ? SideWeapon.None : right;
        loadoutSet = true;
    }

    // 화면 줌 (2026-11-25 사용자: "줌 아웃, 적이 이동하는 거리가 짧아 보여서"): 씬의 카메라 크기 7 → camera_size.
    //  적이 나오는 자리·화면 밖 판정은 모두 카메라 크기를 따라가서 같이 넓어져요
    void ApplyCameraSize()
    {
        var gm = GameManager.instance;
        var cam = gm != null ? gm.mainCamera : null;
        if(cam == null || !cam.orthographic) return;
        float want = Mathf.Clamp(Tuning.Get("camera_size", 9.5f), 4f, 20f);
        if(!Mathf.Approximately(cam.orthographicSize, want)) cam.orthographicSize = want;
    }

    float loadoutCheckTimer;
    void LateUpdate()
    {
        ApplyCameraSize();
        if(!loaded || WeaponManager.instance == null)
            return;
        loadoutCheckTimer -= Time.unscaledDeltaTime;
        if(loadoutCheckTimer <= 0f) {
            loadoutCheckTimer = 0.5f;
            EnsureLoadout();
            if(EffectsActive && pauseCount == 0) ApplyJobStart();   // 전직 '시작 증강' (무기가 판에 들어오면)
            if(EffectsActive && pauseCount == 0 && !pullShowing && planetSnap != null) TryPlanetLevelUp();   // 행성 한 판 (PlanetRun.cs)
            else if(EffectsActive && pauseCount == 0 && !pullShowing) {
                ResumeEmptyFusionSlot();   // 흡수된 B 칸이 비어 있으면 다시 고르게
                TryWeaponSlotPick();       // 무기 칸이 열렸으면 고르게 (증강 뽑기보다 먼저 나오면 새 무기 증강도 그 뽑기에 나와요)
                TryShowRoulette();         // 기본총 룰렛
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugArenaTick();   // 연습장: 죽은 적 다시 채우기
#endif
        foreach(SideWeapon w in Enum.GetValues(typeof(SideWeapon))) {
            if(w == SideWeapon.None) continue;
            var go = WeaponObject(w);
            if(go == null) continue;
            bool active = Owned(w) && IsEquipped(w);
            if(go.activeSelf != active)
                go.SetActive(active);
        }
    }
    #endregion

    #region 축복
    // 계열마다 정해진 신의 관문을 처음 이기면 그 신의 축복이 열려요 (예전 행성 클리어는 관문으로 옮겨져 있어요)
    public static string BlessingPlanetName(AugTag t)
    {
        int gate = AugmentDefs.BlessingGate(t);
        return gate > 0 ? $"{gate}번째 관문 ({ExpeditionZones.PlanetName(gate - 1)})" : "";
    }

    public bool BlessingUnlocked(AugTag t)
    {
        if(debugAllBlessings) return true;
        int gate = AugmentDefs.BlessingGate(t);
        return gate > 0 && IsGateCleared(gate);
    }

    public void SetBlessing(int tagIndex) => blessing = tagIndex;
    #endregion

    #region 일시정지 (팝업용)
    public bool UIPaused => pauseCount > 0;
    public float PausedScale { get => pausedScale; set => pausedScale = value; }

    public void PauseForUI()
    {
        if(pauseCount == 0) {
            pausedScale = Time.timeScale > 0.01f ? Time.timeScale : GameManager.SpeedBase;
            Time.timeScale = 0.0000001f;
        }
        pauseCount++;
    }

    // 창을 한꺼번에 닫을 때: 멈춤을 전부 풀어요
    public void ResumeAllUI()
    {
        if(pauseCount <= 0) return;
        pauseCount = 0;
        Time.timeScale = pausedScale;
    }

    public void ResumeFromUI()
    {
        if(pauseCount <= 0) return;
        pauseCount--;
        if(pauseCount == 0)
            Time.timeScale = pausedScale;
    }
    #endregion


    #region 뽑기
    public void QueuePull(int n)
    {
        pendingPulls += n;
        if(!pullShowing) ShowNextPull();
    }

    void ShowNextPull()
    {
        if(pendingPulls <= 0) return;
        var choices = RollChoices(null);
        if(choices.Count == 0) {
            // 모든 증강이 최대 레벨이면 별의 먼지로 대신 지급
            pendingPulls = 0;
            stardust += 5;
            ui.Toast("모든 증강이 최대 레벨이에요! 별의 먼지 +5", AugmentDefs.TagColor(AugTag.Treasure));
            return;
        }
        pendingPulls--;
        pullShowing = true;
        shownAugs.Clear();
        shownAugs.AddRange(choices);
        shownRerolled.Clear();
        foreach(var _ in choices) shownRerolled.Add(false);
        PauseForUI();
        ShowChoices();
    }

    void ShowChoices()
    {
        var lv = new List<int>();
        foreach(var d in shownAugs) lv.Add(Level(d.id) + 1);
        ui.ShowPull(new List<AugmentDef>(shownAugs), lv, new List<bool>(shownRerolled), RerollLabelShort(), AcceptShown, RequestReroll);
    }

    // 서로 다른 증강 aug_choices(3)장. 등급은 장마다 따로 굴려요. 금 보장(pity)은 한 번 뽑기에 한 번만 세요
    List<AugmentDef> RollChoices(List<string> exclude)
    {
        var list = new List<AugmentDef>();
        rollExcludes.Clear();
        if(exclude != null) foreach(var id in exclude) rollExcludes.Add(id);
        int pityBefore = pity;
        bool goldOrBetter = false;
        int n = Mathf.Max(1, Tuning.GetInt("aug_choices", 3));
        for(int i = 0; i < n; i++) {
            pity = pityBefore;
            var d = Roll(out AugGrade g, -1);
            if(d == null) break;
            list.Add(d);
            rollExcludes.Add(d.id);
            if(g >= AugGrade.Gold) goldOrBetter = true;
        }
        pity = list.Count == 0 ? pityBefore : goldOrBetter ? 0 : pityBefore + 1;
        rollExcludes.Clear();
        if(list.Count == 0 && exclude != null && exclude.Count > 0) return RollChoices(null);   // 뺄 게 다라서 하나도 없으면 빼지 않고
        return list;
    }

    void AcceptShown(int index)
    {
        var def = index >= 0 && index < shownAugs.Count ? shownAugs[index] : null;
        shownAugs.Clear();
        pullShowing = false;
        ResumeFromUI();
        if(def != null) {
            Apply(def, true);
            // 보물 "풍년": 확률로 한 번 더
            int hv = Level("harvest");
            if(hv > 0 && def.id != "harvest" && UnityEngine.Random.value < AugmentDefs.HarvestChance(hv)) {
                pendingPulls++;
                ui.Toast("풍년! 증강을 하나 더 뽑아요", AugmentDefs.TagColor(AugTag.Treasure), true);
            }
        }
        if(pendingPulls > 0) ShowNextPull();
        ui.RefreshHUD();
    }

    // minGrade: -1 이면 일반 뽑기, 0~2 이면 그 등급 이상
    AugmentDef Roll(out AugGrade grade, int minGrade)
    {
        float luck = tree[0];
        float prism = 0.03f + luck * 0.004f;
        float gold = 0.22f + luck * 0.02f;
        float r = UnityEngine.Random.value;
        grade = r < prism ? AugGrade.Prism : r < prism + gold ? AugGrade.Gold : AugGrade.Silver;
        if(minGrade < 0) {
            if(grade == AugGrade.Silver && pity >= PityLimit - 2 * T(9)) grade = AugGrade.Gold;
            pity = grade == AugGrade.Silver ? pity + 1 : 0;
        } else if((int)grade < minGrade) {
            grade = (AugGrade)minGrade;
        }

        bool treasure = UnityEngine.Random.value < TreasureShare;
        if(planetSnap != null) {   // 행성: 추가 무기 카드(두 번째 칸이 비었을 때 25%) → 무기 증강 60% · 능력치 카드 40%
            treasure = false;
            // 받을 무기가 없으면 안 나와요. 자유 선택 행성에서 아직 무기가 없으면 카드 3장 모두 무기 카드 (첫 레벨업)
            //  (2026-11-25 사용자: 처음 레벨업은 한두 마리만 잡아도 바로, 새 무기를 얻게) 그 행성의 첫 레벨업(Lv.1)도 카드 3장 모두 새 무기
            bool firstLevel = planetLevel <= 1 && Tuning.Get("planet_first_weapon_card", 1f) > 0f;
            planetWantExtra = AnyExtraWeapon() && (loadoutLeft == SideWeapon.None || firstLevel || UnityEngine.Random.value < Tuning.Get("planet_extra_weapon_chance", 0.25f));
            planetWantWeapon = UnityEngine.Random.value < Tuning.Get("planet_weapon_card_chance", 0.6f);
        }
        var def = Pick(treasure, grade);
        if(def == null) def = Pick(treasure, null);
        if(def == null) def = Pick(!treasure, grade);
        if(def == null) def = Pick(!treasure, null);
        if(def == null && planetSnap != null && planetWantExtra) { planetWantExtra = false; def = Pick(false, grade) ?? Pick(false, null); }   // 추가 무기를 줄 게 없으면 보통 카드
        if(def == null && planetSnap != null) { planetWantWeapon = !planetWantWeapon; def = Pick(false, grade) ?? Pick(false, null); }   // 한쪽이 다 떨어졌으면 다른 쪽
        if(def != null) grade = def.grade;
        return def;
    }

    AugmentDef Pick(bool treasure, AugGrade? grade)
    {
        var cands = new List<AugmentDef>();
        var weights = new List<float>();
        float total = 0;
        foreach(var d in AugmentDefs.All) {
            if((d.tag == AugTag.Treasure) != treasure) continue;
            if(grade.HasValue && d.grade != grade.Value) continue;
            if(Level(d.id) >= d.maxLevel) continue;
            if(rollExcludes.Contains(d.id)) continue;   // 같은 뽑기의 다른 카드 · 리롤 전에 본 카드는 빼요 (2026-11-25 사용자)
            if(planetSnap == null ? d.planetOnly : !PlanetCardFits(d)) continue;   // 행성: 무기 증강 또는 능력치 카드만 (PlanetRun.cs)
            if(!AugPrereqMet(d)) continue;   // 키울 효과가 아직 없으면 안 나와요 (예: 번개가 없는데 '과부하')
            if(d.requiredWeapon != SideWeapon.None && !d.fusionCard && !IsEquipped(d.requiredWeapon)) continue;   // 합체 카드는 B 로도 나와요 (FusionCardAvailable)
            if(!WeaponAugAllowed(d)) continue;   // 무기 합체 조건 (WeaponAugments.cs)
            if(d.id == "extraarms" && FreeWeaponsForExtra().Count == 0) continue;
            float w = 1f;
            if(blessing >= 0 && (int)d.tag == blessing) w = 2f;
            if(planetSnap != null && d.fusionCard) w *= Tuning.Get("planet_fusion_weight", 1f);   // 행성: 합체 카드가 열리면 잘 나오게
            cands.Add(d); weights.Add(w); total += w;
        }
        if(cands.Count == 0) return null;
        float x = UnityEngine.Random.value * total;
        for(int i = 0; i < cands.Count; i++) {
            x -= weights[i];
            if(x <= 0) return cands[i];
        }
        return cands[cands.Count - 1];
    }

    // 원정에서 쓸 수 있는 무기: 산 무기 + 그 무기의 행성을 클리어 (2026-11-25 사용자 결정)
    public bool ExpeditionUsable(SideWeapon w)
    {
        if(!Owned(w)) return false;
        var wm = WeaponManager.instance;
        var shop = wm != null ? wm.ShopOf(SideWeaponOf(w)) : null;
        return shop == null || shop.ExpeditionUnlocked;
    }

    // ---- 앞선 증강 (2026-11-25 사용자: "번개가 있지도 않은데 '과부하'(번개 피해 +30%)가 떠요")
    //  효과를 키우기만 하는 증강은 그 효과를 만드는 증강(또는 무기)이 있어야 나와요.
    //  번개: 정전기탄·뇌운 / 폭발: 점화탄·폭발 탄두류 / 독: 감염탄·독 비행기 / 둔화·빙결: 서리탄·눈송이 → 빙결 / 블랙홀: 중력 우물 / 파편: 분열 / 레이저: 레이저 무기
    bool HasLightning => Level("static") > 0 || Level("storm") > 0 || Synergy(AugTag.Thunder, 8);
    bool HasExplosion => Level("ignite") > 0 || Synergy(AugTag.Chain, 6) || Level("tk_warhead") > 0 || Level("rg_blast") > 0 || Level("lk_blast") > 0;
    bool HasPoison => Level("infect") > 0 || IsEquipped(SideWeapon.Poison) || Fused(AugmentDefs.FzPlague) || Fused(AugmentDefs.FzToxSat);
    bool HasSlow => Level("frostshot") > 0 || Level("snowflake") > 0;
    bool HasFreeze => Level("freeze") > 0 || Level("northwind") > 0;
    bool HasLaser => IsEquipped(SideWeapon.Lazer) || IsEquipped(SideWeapon.Orbital) || IsEquipped(SideWeapon.StrLaser);
    // 탄 크기가 폭발 범위로 이어지는 무기 (폭격기 폭탄·운석). 다른 무기는 탄 크기가 거의 의미 없어요 (자동 조준이라 원래 다 맞아요)
    bool HasBlastWeapon => IsEquipped(SideWeapon.Bomber) || IsEquipped(SideWeapon.Meteor)
        || Fused(AugmentDefs.FzGuided) || Fused(AugmentDefs.FzGravBomb) || Fused(AugmentDefs.FzBrand) || Fused(AugmentDefs.FzImpact);

    bool AugPrereqMet(AugmentDef d)
    {
        switch(d.id) {
            // 번개
            case "conduct": case "overload": case "rod": case "chainlight": case "capacitor": return HasLightning;
            case "judgment": return Level("storm") > 0 || Level("capacitor") > 0;   // 낙뢰가 있어야
            // 폭발
            case "gunpowder": return HasExplosion || IsEquipped(SideWeapon.Bomber);   // 폭격기 폭탄 범위에도 들어가요
            case "embers": case "chain": case "furnace": case "shrapnel": case "heatwave": return HasExplosion;
            case "hammer": return Level("chain") > 0;
            // 독
            case "venom": case "corrode": case "contagion": case "toxburst": case "nemesis": return HasPoison;
            case "cloud": return IsEquipped(SideWeapon.Poison) || Fused(AugmentDefs.FzPlague);   // 독 지대는 독 비행기에서만
            case "mutate": return Level("contagion") > 0;   // 독이 옮아야 쌓여요
            // 냉기
            case "chill": case "freeze": return HasSlow;
            case "shatter": case "permafrost": case "glacier": return HasFreeze;
            // 중력
            case "collapse": return Level("horizon") > 0;
            // 증식
            case "starswarm": return Level("split") > 0;
            case "refract": return HasLaser;
            case "bigshot": case "stardustshot": return HasBlastWeapon;   // (2026-11-25 사용자: 탄 크기가 큰 의미가 있나 → 폭격기·운석이 있을 때만)
            // 저주: 다른 저주 증강이 있어야
            case "abyss": return TagCount(AugTag.Curse) > 0;
        }
        return true;
    }

    List<SideWeapon> FreeWeaponsForExtra()
    {
        var list = new List<SideWeapon>();
        if(planetSnap != null) return list;   // 행성 한 판: 무기는 행성 무기 + 두 번째 칸 카드로만 (2026-11-25)
        foreach(var w in OwnedWeapons())
            if(ExpeditionUsable(w) && !IsEquipped(w) && !RawEquipped(w) && !fusionSlots.ContainsKey(w)) list.Add(w);   // 합체 재료로 들어간 무기(교체·흡수)는 빼요
        return list;
    }

    // ── 무기 전직 '시작 증강' (2026-11-26, WeaponJob) ──
    //  무기가 이번 판에 들어오면 고른 전용 증강을 그 레벨까지 (이미 더 높으면 그대로). 기본총은 시작 뽑기 +N / 리롤 +2N (판이 비어 있을 때만)
    readonly HashSet<int> jobGranted = new HashSet<int>();
    void ApplyJobStart()
    {
        for(int i = 0; i < WeaponJob.WeaponCount; i++) {
            if(jobGranted.Contains(i)) continue;
            var w = (TraitsData.WeaponType)i;
            int pick = WeaponJob.Active(w, 1);
            if(pick < 0) continue;
            int n = WeaponJob.StartCount(WeaponJob.Level(w));
            if(w == TraitsData.WeaponType.MainWeapon) {
                jobGranted.Add(i);
                if(order.Count > 0) continue;   // 이어 하는 판(행성 다음 맵 등)엔 다시 안 줘요
                if(pick == 0) QueuePull(n); else rerollBag += n * 2;
                continue;
            }
            var side = AugmentDefs.SideOf(w);
            if(!IsEquipped(side)) continue;
            jobGranted.Add(i);
            var def = AugmentDefs.Get(WeaponJob.StartAugId(w, pick));
            if(def == null) continue;
            while(Level(def.id) < Mathf.Min(n, def.maxLevel)) Apply(def, false);
        }
    }

    void Apply(AugmentDef def, bool announce)
    {
        int before = Level(def.id);
        var tagTierBefore = SynergyTier(def.tag);
        var comboBefore = new int[AugmentDefs.Combos.Length];
        for(int i = 0; i < comboBefore.Length; i++) comboBefore[i] = ComboTier(i);
        levels[def.id] = before + 1;
        if(before == 0) order.Add(def.id);
        OnAugmentsChanged();

        if(def.fusionCard) DoFuse(def, announce);
        if(def.grantWeapon != SideWeapon.None && planetSnap != null) {   // 행성 추가 무기 카드 (두 번째 칸 또는 합체로 빈 칸)
            GiveExtraWeapon(def.grantWeapon, announce);
            levels.Remove(def.id);   // 같은 무기 카드가 나중에 또 나올 수 있게 (예: 합체 재료로 들어간 뒤에는 ExtraWeaponOk 가 막아요)
            order.Remove(def.id);
            OnAugmentsChanged();
        }

        switch(def.id) {
            case "armor":
                if(InfiniteStage.Instance != null) InfiniteStage.Instance.AddShieldNow(1);
                break;
            case "aegis":
                aegisLeft = AugmentDefs.AegisBlocks(Level("aegis"));
                break;
            case "bloodprice":
                if(before == 0 && InfiniteStage.Instance != null) InfiniteStage.Instance.AddShieldNow(-1);
                break;
            case "spareshield":
                if(InfiniteStage.Instance != null) InfiniteStage.Instance.AddShieldNow(1);
                break;
            case "rerollbag":
                rerollBag += 3;
                break;
            case "extraarms":
                var free = FreeWeaponsForExtra();
                if(free.Count == 1 || !announce) {
                    if(free.Count > 0) extraWeapon = free[0];
                } else if(free.Count > 1) {
                    PauseForUI();
                    ui.ShowWeaponChoice("추가 무장: 무기 선택", free, w => {
                        extraWeapon = w;
                        ResumeFromUI();
                        ui.RefreshHUD();
                    });
                }
                break;
        }

        int tierAfter = SynergyTier(def.tag);
        // 수호 세트 2: 실드 +1 (지금 실드에도 바로)
        if(def.tag == AugTag.Guard && tagTierBefore < 2 && tierAfter >= 2 && InfiniteStage.Instance != null)
            InfiniteStage.Instance.AddShieldNow(1);
        if(announce && AugmentDefs.IsMain(def.tag) && tierAfter > tagTierBefore)
            ui.Toast($"세트 완성! {AugmentDefs.TagName(def.tag)} {tierAfter}단계: {AugmentDefs.SynergyText(def.tag, tierAfter)}", AugmentDefs.TagColor(def.tag), true);
        if(announce) {
            for(int i = 0; i < comboBefore.Length; i++) {
                int t = ComboTier(i);
                if(t <= comboBefore[i]) continue;
                var c = AugmentDefs.Combos[i];
                ui.Toast($"조합 계열! {c.god} · {c.name}{(t == 2 ? " 2단계" : "")}\n{(t == 2 ? c.tier2 : c.tier1)}", Color.Lerp(AugmentDefs.TagColor(c.a), AugmentDefs.TagColor(c.b), 0.5f), true);
            }
        }
        ui.RefreshHUD();
    }

    void RefreshRerollDay()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if(freeRerollDate != today) {
            freeRerollDate = today;
            freeRerollsUsed = 0;
            adRerollsUsed = 0;
        }
    }

    public string RerollLabelShort()
    {
        RefreshRerollDay();
        int free = FreeRerollsPerDay - freeRerollsUsed;
        if(free > 0) return $"리롤 무료 {free}번";
        if(rerollBag > 0) return $"리롤 주머니 {rerollBag}번";
        if(adRerollsUsed < AdRerollsPerDay) return $"리롤 광고 {AdRerollsPerDay - adRerollsUsed}번";
        return $"리롤 다이아 {DiamondRerollCost}개";
    }

    public string RerollLabel()
    {
        RefreshRerollDay();
        int free = FreeRerollsPerDay - freeRerollsUsed;
        if(free > 0) return $"다시 뽑기 (무료 {free})";
        if(rerollBag > 0) return $"다시 뽑기 (주머니 {rerollBag})";
        if(adRerollsUsed < AdRerollsPerDay) return $"광고 보고 다시 뽑기 ({AdRerollsPerDay - adRerollsUsed})";
        return $"다시 뽑기 (다이아 {DiamondRerollCost})";
    }

    // 카드 한 장 다시 뽑기 (2026-11-25 사용자: "A 리롤, B 리롤, C 리롤처럼 카드마다 오른쪽 버튼")
    void RequestReroll(int index)
    {
        if(index < 0 || index >= shownRerolled.Count || shownRerolled[index]) return;   // 이 자리는 이미 리롤했어요
        RefreshRerollDay();
        if(FreeRerollsPerDay - freeRerollsUsed > 0) {
            freeRerollsUsed++;
            DoReroll(index);
        } else if(rerollBag > 0) {
            rerollBag--;
            DoReroll(index);
        } else if(adRerollsUsed < AdRerollsPerDay && AdmobManager.Instance != null) {
            AdmobManager.Instance.ShowRewardedAdGeneric(() => {
                adRerollsUsed++;
                DoReroll(index);
            });
        } else if(GameManager.GetDiamond() >= DiamondRerollCost) {
            GameManager.SetDiamond(-DiamondRerollCost);
            DoReroll(index);
        } else {
            ui.Toast("다이아가 부족해요", Color.white);
        }
    }

    readonly HashSet<string> rollExcludes = new HashSet<string>();
    // 그 카드만 새로 (화면의 다른 카드·방금 그 카드와 겹치지 않게, 2026-11-25 사용자: 같은 증강이 연달아 안 나오게)
    void DoReroll(int index)
    {
        if(index < 0 || index >= shownAugs.Count) return;
        rollExcludes.Clear();
        foreach(var d in shownAugs) rollExcludes.Add(d.id);
        var def = Roll(out AugGrade _, -1);
        rollExcludes.Clear();
        if(def == null) { ui.Toast("다른 증강이 더 없어요", Color.white); ShowChoices(); return; }
        shownAugs[index] = def;
        shownRerolled[index] = true;
        ShowChoices();
    }
    #endregion

    #region 원정 시작 / 환생
    public int RebirthThreshold => rebirthCount == 0 ? RemasterBalance.FirstRebirthStage : Mathf.Max(40, Mathf.FloorToInt(bestStage * (0.5f - 0.05f * T(8))));
    public bool CanRebirth => loaded && CurrentStage >= RebirthThreshold;

    public static long StardustFor(int stage)
    {
        if(stage < 10) return 0;
        return (long)Mathf.Floor(RemasterBalance.StardustMul * Mathf.Pow(stage / 10f, 1.5f));
    }

    public long RebirthGain {
        get {
            long g = StardustFor(CurrentStage);
            int c = Level("collector");
            if(c > 0) g = (long)(g * (1f + AugmentDefs.CollectorBonus(c)));
            if(T(7) > 0) g = (long)(g * (1f + 0.1f * T(7)));   // 환생 트리 "먼지 수집"
            g = (long)(g * (1.0 + PlanetBuffManager.DustBonus));   // 행성 버프 '별의 먼지' (2026-11-25)
            return g;
        }
    }

    public int TreeCost(int node) => node == StarPowerNode ? RemasterBalance.StarPowerCost(tree[node]) : TreeBaseCost[node] * (tree[node] + 1);

    public bool UpgradeTree(int node)
    {
        if(!TreeUnlocked(node)) return false;
        if(tree[node] >= TreeMaxOf(node)) return false;
        int cost = TreeCost(node);
        if(stardust < cost) return false;
        stardust -= cost;
        tree[node]++;
        ui.RefreshHUD();
        return true;
    }

    public string TreeDesc(int node)
    {
        int l = tree[node];
        switch(node) {
            case 0: return $"골드 확률 +{l * 2}%p, 프리즘 확률 +{l * 0.4f:0.#}%p";
            case 1: return l == 0 ? "원정 시작 시 최고 기록의 일부 건너뛰기" : $"최고 기록의 {l * 10}%부터 출발 (건너뛴 만큼 증강 지급)";
            case 2: return l == 0 ? "원정 시작 시 증강 1개를 들고 출발" : $"{(l == 1 ? "실버" : l == 2 ? "골드" : "프리즘")} 이상 증강 1개를 들고 출발";
            case 3: return $"원정 피해 ×{StarPowerMultiplier:0.##} (레벨마다 ×{RemasterBalance.StarPowerPerLevel:0.##})";
            case 4: return $"원정마다 리롤 +{Mathf.Max(1, l)}회로 출발";
            case 5: return $"원정 시작 실드 +{Mathf.Max(1, l)}";
            case 6: return "신의 관문을 이길 때마다 증강 1개 추가";
            case 7: return $"환생할 때 별의 먼지 +{Mathf.Max(1, l) * 10}%";
            case 8: return $"환생 조건: 최고 기록의 {(int)((0.5f - 0.05f * Mathf.Max(1, l)) * 100)}%만 가면 환생 가능";
            case 9: return $"실버 연속 {PityLimit - 2 * Mathf.Max(1, l)}번이면 다음은 골드 이상 (기본 {PityLimit}번)";
            case 10: return $"보스·관문 보스에게 주는 피해 +{Mathf.Max(1, l) * 10}%";
        }
        return "";
    }

    void ResetExpeditionState()
    {
        levels.Clear();
        order.Clear();
        expKills = 0;
        expBossKills = 0;
        soulStack = slaughterStack = wraithStack = stardustSizeStack = 0;
        rerollBag = T(4);   // 환생 트리 "리롤 주머니"
        pity = 0;
        extraWeapon = SideWeapon.None;
        fusionSlots.Clear();
        loadoutLeft = loadoutRight = SideWeapon.None;   // 기본총 하나로 출발 (2026-11-25), 무기는 정한 스테이지에 골라요
        ResetRoulette();
        ResetEffectState();
        jobGranted.Clear();   // 전직 시작 증강 (새 원정)
        OnAugmentsChanged();
    }

    public void DoRebirth()
    {
        if(!CanRebirth) return;
        long gain = RebirthGain;
        stardust += gain;
        rebirthCount++;
        if(CurrentStage > bestStage) bestStage = CurrentStage;
        ResetExpeditionState();

        int start = 1;
        if(tree[1] > 0) {
            start = Mathf.FloorToInt(bestStage * 0.1f * tree[1] / PullInterval) * PullInterval + 1;
            start = Mathf.Max(1, start);
        }
        rebirthStartStage = start;
        pendingRebirthStart = true;
        pendingSetup = true;
        if(InfiniteStage.Instance != null) InfiniteStage.Instance.JumpToStage(1);
        ui.Toast($"환생! 별의 먼지 +{gain:N0}", AugmentDefs.TagColor(AugTag.Treasure), true);
        TryShowSetup();
    }

    void TryShowSetup()
    {
        if(!pendingSetup || ui.Busy) return;
        if(InfiniteStage.Instance == null || !InfiniteStage.Instance.IsRunning) return;
        if(StartScreenScript.Instance != null && StartScreenScript.Instance.gameObject.activeInHierarchy) return;
        if(StageManager.instance != null && StageManager.instance.isInStage) return;

        if(migrationGift > 0) {
            long gift = migrationGift;
            migrationGift = 0;
            PauseForUI();
            ui.ShowNotice("무한 스테이지가 '원정'으로 바뀌었어요",
                $"5스테이지마다 증강을 뽑아 강해지고,\n벽에 막히면 환생해서 더 강하게 출발해요.\n\n지금까지의 기록 (최고 {bestStage:N0}스테이지)에 대한\n보상으로 별의 먼지 {gift:N0}개를 드려요.\n\n원정은 1스테이지부터 새로 시작해요.",
                () => { ResumeFromUI(); });
            return;
        }

        pendingSetup = false;
        EnsureLoadout();
        PauseForUI();
        ui.ShowSetup(() => {
            ResumeFromUI();
            StartExpeditionAfterSetup();
        });
    }

    void StartExpeditionAfterSetup()
    {
        var got = new List<string>();
        if(pendingRebirthStart) {
            pendingRebirthStart = false;
            if(tree[2] > 0) {
                var def = Roll(out _, tree[2] - 1);
                if(def != null) { Apply(def, false); got.Add(def.name); }
            }
            GrantSkippedRoulettes(rebirthStartStage, got);   // 건너뛴 기본총 룰렛
            int skippedPulls = RemasterBalance.AugmentStagesBefore(rebirthStartStage);
            for(int i = 0; i < skippedPulls; i++) {
                var def = Roll(out _, -1);
                if(def == null) break;
                Apply(def, false);
                got.Add(def.name);
            }
            if(InfiniteStage.Instance != null && rebirthStartStage > 1) {
                // 건너뛴 스테이지의 처치·보스 수를 인정 (누적 증강이 손해 보지 않게)
                expKills += RemasterBalance.KillsBefore(rebirthStartStage);
                expBossKills += (rebirthStartStage - 1) / 10;
                InfiniteStage.Instance.JumpToStage(rebirthStartStage);
            }
        }
        if(got.Count > 0) {
            PauseForUI();
            ui.ShowNotice("출발 보너스", $"증강 {got.Count}개를 얻었어요!\n\n{string.Join(", ", got)}", () => ResumeFromUI());
        }
        ui.RefreshHUD();
    }
    #endregion

    #region 저장
    public void LoadData(GameData data)
    {
        WeaponJob.Load(data);   // 무기 전직 (2026-11-26). 특성(TraitsManager)보다 늦게 불러와요 → 예전 특성 광석 돌려주기
        if(data.expeditionVersion < 1) {
            // 예전 세이브(또는 새 게임): 원정 상태를 깨끗하게 초기화
            ResetExpeditionState();
            blessing = -1;
            loadoutSet = false;
            loadoutLeft = loadoutRight = SideWeapon.None;
            freeRerollDate = "";
            freeRerollsUsed = adRerollsUsed = 0;
            stardust = 0;
            tree = new int[TreeNames.Length];
            rebirthCount = 0;
            int oldStage = data.StageIndex;
            bestStage = Mathf.Max(1, oldStage);
            pendingSetup = false;
            migrationGift = 0;
            if(oldStage > 1) {
                migrationGift = StardustFor(oldStage);
                stardust = migrationGift;
                pendingSetup = true;
                pendingMigrationJump = true;
                data.StageIndex = 1;
                if(InfiniteStage.Instance != null) InfiniteStage.Instance.CurrentStage = 1;
            }
            data.expeditionVersion = 1;
        data.clearedGates = new List<int>(clearedGates);
        } else {
            LoadExpeditionFields(data);
            freeRerollDate = data.freeRerollDate ?? "";
            freeRerollsUsed = data.freeRerollsUsed;
            adRerollsUsed = data.adRerollsUsed;
            stardust = data.stardust;
            tree = new int[TreeNames.Length];
            if(data.rebirthTree != null)
                for(int i = 0; i < tree.Length && i < data.rebirthTree.Count; i++) tree[i] = Mathf.Clamp(data.rebirthTree[i], 0, TreeMax[i]);
            bestStage = Mathf.Max(1, data.bestExpeditionStage);
            rebirthCount = data.rebirthCount;
        }
        LoadPlanetRuns(data);   // 행성마다 모은 증강 (PlanetRun.cs)
        LoadDimensionData(data);   // 심연 클리어·정복, 평행 첫 완주 (PlanetRun.cs)
        clearedGates.Clear();
        if(data.clearedGates != null) foreach(var g in data.clearedGates) clearedGates.Add(g);
        else MigrateGatesFromPlanets();
        if(!loadoutSet) EnsureLoadout();
        loaded = true;
        ui.RefreshHUD();
    }

    public void SaveData(GameData data)
    {
        WeaponJob.Save(data);
        if(!loaded) return;
        data.expeditionVersion = 1;
        if(planetSnap != null) CopyExpeditionFields(planetSnap, data);   // 행성 한 판 중: 원정 상태는 들어갈 때 보관한 그대로 (PlanetRun.cs)
        else SaveExpeditionFields(data);
        data.freeRerollDate = freeRerollDate;
        data.freeRerollsUsed = freeRerollsUsed;
        data.adRerollsUsed = adRerollsUsed;
        data.stardust = stardust;
        data.rebirthTree = new List<int>(tree);
        data.bestExpeditionStage = bestStage;
        data.rebirthCount = rebirthCount;
        SavePlanetRuns(data);   // 행성마다 모은 증강 (PlanetRun.cs)
    }

    // ---- 원정 상태 (증강·누적·무기 칸·룰렛·환생 출발 대기). 행성 한 판은 이걸 보관했다가 나올 때 되돌려요 (2026-11-25)
    void SaveExpeditionFields(GameData data)
    {
        data.augIds = new List<string>(order);
        data.augLevels = new List<int>();
        foreach(var id in order) data.augLevels.Add(Level(id));
        data.expKills = expKills;
        data.expBossKills = expBossKills;
        data.expSoulStack = soulStack;
        data.expSlaughterStack = slaughterStack;
        data.expWraithStack = wraithStack;
        SaveStacks(data);
        data.expBlessing = blessing;
        data.expRerollBag = rerollBag;
        data.expPity = pity;
        data.expExtraWeapon = (int)extraWeapon;
        data.expFusionSlots = SaveFusionSlots();
        data.expRoulette = SaveRoulette();
        data.expLoadoutSet = loadoutSet;
        data.expLoadoutLeft = (int)loadoutLeft;
        data.expLoadoutRight = (int)loadoutRight;
        data.expPendingSetup = pendingSetup || migrationGift > 0;
        data.expPendingStartStage = pendingRebirthStart ? rebirthStartStage : 0;
    }

    static void CopyExpeditionFields(GameData from, GameData to)
    {
        to.augIds = from.augIds != null ? new List<string>(from.augIds) : new List<string>();
        to.augLevels = from.augLevels != null ? new List<int>(from.augLevels) : new List<int>();
        to.expKills = from.expKills;
        to.expBossKills = from.expBossKills;
        to.expSoulStack = from.expSoulStack;
        to.expSlaughterStack = from.expSlaughterStack;
        to.expWraithStack = from.expWraithStack;
        to.expStackKeys = from.expStackKeys != null ? new List<string>(from.expStackKeys) : new List<string>();
        to.expStackVals = from.expStackVals != null ? new List<float>(from.expStackVals) : new List<float>();
        to.expBlessing = from.expBlessing;
        to.expRerollBag = from.expRerollBag;
        to.expPity = from.expPity;
        to.expExtraWeapon = from.expExtraWeapon;
        to.expFusionSlots = from.expFusionSlots;
        to.expRoulette = from.expRoulette;
        to.expLoadoutSet = from.expLoadoutSet;
        to.expLoadoutLeft = from.expLoadoutLeft;
        to.expLoadoutRight = from.expLoadoutRight;
        to.expPendingSetup = from.expPendingSetup;
        to.expPendingStartStage = from.expPendingStartStage;
    }

    void LoadExpeditionFields(GameData data)
    {
        levels.Clear();
        order.Clear();
        if(data.augIds != null && data.augLevels != null) {
            for(int i = 0; i < data.augIds.Count && i < data.augLevels.Count; i++) {
                if(AugmentDefs.Get(data.augIds[i]) == null) continue;
                levels[data.augIds[i]] = data.augLevels[i];
                order.Add(data.augIds[i]);
            }
        }
        expKills = data.expKills;
        expBossKills = data.expBossKills;
        soulStack = data.expSoulStack;
        slaughterStack = data.expSlaughterStack;
        wraithStack = data.expWraithStack;
        LoadStacks(data);
        OnAugmentsChanged();
        stardustSizeStack = Level("stardustshot") > 0 ? Mathf.Min(2f, (expKills / 100) * AugmentDefs.StardustShotPer100(Level("stardustshot"))) : 0f;
        blessing = data.expBlessing;
        rerollBag = data.expRerollBag;
        pity = data.expPity;
        extraWeapon = (SideWeapon)data.expExtraWeapon;
        LoadFusionSlots(data.expFusionSlots);
        LoadRoulette(data.expRoulette);
        loadoutSet = data.expLoadoutSet;
        loadoutLeft = (SideWeapon)data.expLoadoutLeft;
        loadoutRight = (SideWeapon)data.expLoadoutRight;
        pendingSetup = data.expPendingSetup;
        // 환생 직후 출발 전에 저장된 경우: 원정 준비 화면과 출발 보너스를 다시 이어서 진행
        pendingRebirthStart = data.expPendingStartStage > 0;
        rebirthStartStage = Mathf.Max(1, data.expPendingStartStage);
        if(pendingRebirthStart) pendingSetup = true;
    }
    #endregion

    #region 개발용 키 (에디터 / 개발 빌드 전용)
    void DebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(!loaded) return;
        if(Input.GetKeyDown(KeyCode.F1)) QueuePull(1);
        if(Input.GetKeyDown(KeyCode.F2) && InfiniteStage.Instance != null) {
            InfiniteStage.Instance.JumpToStage(InfiniteStage.Instance.CurrentStage + 20);
            ui.Toast($"[개발] {InfiniteStage.Instance.CurrentStage}스테이지로 이동", Color.white);
        }
        if(Input.GetKeyDown(KeyCode.F3)) {
            debugAllBlessings = !debugAllBlessings;
            ui.Toast("[개발] 모든 축복 해금: " + (debugAllBlessings ? "켜짐" : "꺼짐"), Color.white);
        }
        if(Input.GetKeyDown(KeyCode.F4)) { stardust += 100; ui.Toast("[개발] 별의 먼지 +100", Color.white); ui.RefreshHUD(); }
        if(Input.GetKeyDown(KeyCode.F5)) { pendingSetup = true; ui.Toast("[개발] 원정 준비 화면", Color.white); }
        if(Input.GetKeyDown(KeyCode.F6)) ui.ShowRebirth();
        // F7: 게임 속도 x1 → x2 → x4 → x5 → x10 → x20 → x1 (표시 배속, 실제는 × 1.5)
        if(Input.GetKeyDown(KeyCode.F7)) ui.Toast($"[개발] 게임 속도 x{DevTools.CycleSpeed()}", Color.white);
        // F12: 스크린샷 → 작업 폴더 previews/ (Claude 가 직접 볼 수 있어요)
        if(Input.GetKeyDown(KeyCode.F12)) ui.Toast($"[개발] 스크린샷 저장: {DevTools.Screenshot()}", Color.white);
        // P: 연습장 (멈춘 적 + 무기 하나씩 바꾸기). F11 은 에디터에서 안 먹어서 바꿨어요
        if(Input.GetKeyDown(KeyCode.P)) ui.ShowDebugArena();
        // U: 지금 합체 무기의 종결 바로 발동
        if(Input.GetKeyDown(KeyCode.U)) DebugTriggerCurrentUlt();
        // B: 가방 비우기 (2026-11-27 사용자 "가방 비워 줘") — 묻는 창에서 '비우기'를 눌러야 지워요. 낀 장비는 남겨요
        if(Input.GetKeyDown(KeyCode.B)) ui.DebugEmptyBag();
        // G = 전설 · H = 고대 뽑기 연출 시험 (Shift = 1회, 아니면 10회). 실제로 뽑지 않아요 (2026-11-27 사용자 "레전더리랑 고대도 테스트하게")
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if(Input.GetKeyDown(KeyCode.G)) ui.DebugGachaFx(4, shift ? 1 : 10);
        if(Input.GetKeyDown(KeyCode.H)) ui.DebugGachaFx(5, shift ? 1 : 10);
        if(Input.GetKeyDown(KeyCode.J)) ui.DebugGachaFx(3, shift ? 1 : 10);   // J = 유니크 (금빛 강림까지는 전설·고대와 같아요)
        DevTools.Tick();
        // F8: 1000스테이지 앞으로 (차원 확인용)
        if(Input.GetKeyDown(KeyCode.F8) && InfiniteStage.Instance != null) {
            InfiniteStage.Instance.JumpToStage(InfiniteStage.Instance.CurrentStage + 1000);
            ui.Toast($"[개발] {InfiniteStage.Instance.CurrentStage}스테이지로 이동", Color.white);
        }
        // Home: 1스테이지로 (2026-11-22 사용자: 높은 스테이지 적은 너무 빠르고 세고 커서 DPS 재기 어려워요). F2(+20)로 다시 올라갈 수 있어요
        if(Input.GetKeyDown(KeyCode.Home) && InfiniteStage.Instance != null) {
            InfiniteStage.Instance.JumpToStage(1);
            ui.Toast("[개발] 1스테이지로 이동", Color.white);
        }
        // F9: 골드 많이 (무기 강화 확인용)
        if(Input.GetKeyDown(KeyCode.F9)) {
            GameManager.Enemy_Dead_Money_UP(1000000000L);
            ui.Toast("[개발] 골드 +10억", Color.white);
        }
        // Insert: 부품(우주석) +100 (행성 버프 확인용, 2026-11-25)
        if(Input.GetKeyDown(KeyCode.Insert)) {
            GameManager.SetParts(100);
            ui.Toast("[개발] 부품 +100", Color.white);
        }
        // ` 키(숫자 1 왼쪽): 증강 테스트 창. F10은 녹화 프로그램 단축키와 겹쳐서 바꿨어요. 화면의 [테스트] 버튼도 같아요
        if(Input.GetKeyDown(KeyCode.BackQuote)) ui.ShowDebugAugments();
        // End: 실제 한 판 (테스트 설정을 다 끄고 1스테이지부터 원정 준비 화면)
        if(Input.GetKeyDown(KeyCode.End)) DebugRealRun();
#endif
    }

    // ---- 개발용: 증강 테스트 창에서 불러요 (에디터 / 개발 빌드에서만 동작)
    public void DebugAddAugment(AugmentDef def, bool toMax)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(def == null) return;
        if(def.fusionCard) { DebugFuse(def.fusion); return; }   // 합체 카드: 조건 없이 바로 합체
        do {
            if(Level(def.id) >= def.maxLevel) break;
            Apply(def, true);
        } while(toMax);
#endif
    }

    bool debugEquipAll;
    bool debugPickMode;
    readonly HashSet<SideWeapon> debugPicked = new HashSet<SideWeapon>();

    // 개발용 (에디터에서만, 처음부터 켜져 있어요)
    // 테스트 모드: 적이 죽지 않고(체력이 다시 차요), 우주선에 닿으면 사라져요. 실드 안 깎임, 스테이지 멈춤, 적이 끝없이 나와요
    // 적 3배: 나오는 속도 3배
#if UNITY_EDITOR
    public static bool DebugTestMode = true;
    public static bool DebugManyEnemies = true;
#else
    public static bool DebugTestMode = false;
    public static bool DebugManyEnemies = false;
#endif
    public void DebugSpawnBoss()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool ok = InfiniteStage.Instance != null && InfiniteStage.Instance.DebugSpawnBoss();
        ui.Toast(ok ? "[개발] 보스 소환" : "[개발] 원정 전투 중에만 부를 수 있어요", Color.white);
#endif
    }

    // 실제 한 판 (2026-11-25 사용자: "이 설정들을 한번에"): 에디터 테스트 설정을 다 끄고, 원정을 1스테이지부터 새로 (원정 준비 화면부터)
    //  테스트 모드·적 3배·연습장·적 무적·무기 고르기·전부 출격·게임 속도를 끄고, 증강을 비워요. 환생 횟수·별의 먼지·환생 트리는 그대로
    public void DebugRealRun()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(InfiniteStage.Instance == null || !InfiniteStage.Instance.IsRunning) { ui.Toast("[개발] 원정 전투 중에만 쓸 수 있어요", Color.white); return; }
        if(DebugArena) DebugArenaStop();
        DebugTestMode = false;
        DebugManyEnemies = false;
        DebugMortal = false;
        DebugArenaInvincible = false;
        debugEquipAll = false;
        DebugPickClear(true);
        GameManager.SetDisplaySpeed(1f);   // 표시 x1 (실제 바탕 1.5)
        int shieldDiff = ShieldBonus;
        ResetExpeditionState();
        shieldDiff -= ShieldBonus;
        if(shieldDiff != 0) InfiniteStage.Instance.AddShieldNow(-shieldDiff);   // 증강 실드 보너스만큼 빼요 (모두 지우기와 같아요)
        if(EnemyManager.Instance != null) EnemyManager.Instance.removeAllEnemies();
        InfiniteStage.Instance.JumpToStage(1);
        pendingSetup = true;
        TryShowSetup();
        ui.Toast("[개발] 실제 한 판: 테스트 설정을 다 끄고 1스테이지부터 (게임 속도 x1)", Color.white);
        ui.RefreshHUD();
#endif
    }

    public void DebugToggleTestMode()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugTestMode = !DebugTestMode;
        if(InfiniteStage.Instance != null) InfiniteStage.Instance.JumpToStage(InfiniteStage.Instance.CurrentStage);   // 생성을 다시 시작
        ui.Toast("[개발] 테스트 모드: " + (DebugTestMode ? "켜짐 (적이 안 죽고 끝없이 나와요)" : "꺼짐 (스테이지 다시 시작)"), Color.white);
#endif
    }
    public static float DebugSpawnMul => MeasuringMoving ? Tuning.Get("measure_spawn_mul", 10f) : DebugManyEnemies ? Tuning.Get("test_spawn_mul", 3f) : 1f;
    // 테스트 모드에서 화면에 있을 수 있는 최대 적 수 (측정 "움직이는 적" 중엔 더 많이, 2026-11-22)
    public static int DebugMaxEnemies => MeasuringMoving ? Tuning.GetInt("measure_max_enemies", 120) : Tuning.GetInt("test_max_enemies", 80);
    public bool DebugEquipAll => debugEquipAll;

    // 무기 상점 잠금을 풀고, 아직 없는 무기는 Lv.1 로 줘요 (Lv.1은 저장돼요)
    public void DebugUnlockAllWeapons()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        WeaponShopBase.DebugUnlockAll = true;
        int given = 0;
        if(WeaponManager.instance != null)
            foreach(var w in WeaponManager.instance.Weapons)
                if(w.UpgradeLevel <= 0) { w.UpgradeLevel = 1; w.SetDamage(); given++; }
        foreach(var s in FindObjectsByType<WeaponShopBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if(s.LockCover != null) s.LockCover.SetActive(false);
        ui.Toast($"[개발] 무기 전부 열기 (새로 받은 무기 {given}개)", Color.white);
        ui.RefreshHUD();
#endif
    }

    public void DebugToggleEquipAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugEquipAll = !debugEquipAll;
        ui.Toast("[개발] 가진 무기 전부 출격: " + (debugEquipAll ? "켜짐" : "꺼짐"), Color.white);
        ui.RefreshHUD();
#endif
    }

    public void DebugClearAugments()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int shieldDiff = ShieldBonus;
        levels.Clear();
        order.Clear();
        ResetEffectState();
        OnAugmentsChanged();
        extraWeapon = SideWeapon.None;
        fusionSlots.Clear();
        ResetRoulette();   // 기본총 룰렛도 (2026-11-25)
        shieldDiff -= ShieldBonus;
        if(shieldDiff != 0 && InfiniteStage.Instance != null) InfiniteStage.Instance.AddShieldNow(-shieldDiff);
        ui.Toast("[개발] 증강을 모두 지웠어요", Color.white);
        ui.RefreshHUD();
#endif
    }
    #endregion
}
