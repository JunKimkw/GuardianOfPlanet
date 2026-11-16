using System;
using System.Collections.Generic;
using UnityEngine;

// 리마스터 핵심: 원정(무한 스테이지) 증강 / 축복 / 리롤 / 출격 무기 / 환생 / 기존 유저 보상
// 씬을 건드리지 않도록 게임 시작 시 코드로 생성돼요(Boot).
public class AugmentManager : MonoBehaviour, IDataPersistence
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
    public float StarPowerMultiplier => Mathf.Pow(RemasterBalance.StarPowerPerLevel, tree.Length > StarPowerNode ? tree[StarPowerNode] : 0);

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
    SideWeapon supplyWeapon = SideWeapon.None;
    int supplyUntil;
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
    AugmentDef shownAug;
    AugGrade shownGrade;

    // ---- 폭발 처리
    struct PendingExplosion { public Vector3 pos; public int depth; public long damage; public float radius; public float time; }
    readonly List<PendingExplosion> explosions = new List<PendingExplosion>();
    int effectDepth;            // 폭발/파편으로 준 피해 처리 중이면 >0
    bool inSplit;
    float hammerReadyTime;

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
    public SideWeapon SupplyWeapon => supplyWeapon;
    public int SupplyUntil => supplyUntil;
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
            if(!loaded || InfiniteStage.Instance == null || !InfiniteStage.Instance.IsRunning)
                return false;
            if(StageManager.instance != null && StageManager.instance.isInStage)
                return false;
            return true;
        }
    }

    public int TagCount(AugTag tag)
    {
        int n = 0;
        foreach(var id in order) {
            var d = AugmentDefs.Get(id);
            if(d != null && d.tag == tag) n++;
        }
        return n;
    }

    public bool Synergy(AugTag tag, int tier) => TagCount(tag) >= tier;

    float CurseGain => Synergy(AugTag.Curse, 2) ? 1.2f : 1f;

    public float CurseStackTotal {
        get {
            // 처치 수가 많아질수록 덜 쌓이고(EffectiveKills), 합계는 +300%가 상한
            float k = RemasterBalance.EffectiveKills(expKills) * CurseGain;
            float s = 0f;
            if(Level("soul") > 0) s += AugmentDefs.SoulPerKill(Level("soul")) * k;
            if(Level("slaughter") > 0) s += AugmentDefs.SlaughterPerKill(Level("slaughter")) * k;
            if(Level("wraith") > 0) s += AugmentDefs.WraithPerBoss(Level("wraith")) * expBossKills * CurseGain;
            s = Mathf.Min(s, RemasterBalance.CurseCap);
            if(Synergy(AugTag.Curse, 6)) s *= 1.5f;
            int tl = Level("tartarus");
            if(tl > 0 && InfiniteStage.Instance != null && InfiniteStage.Instance.playerHealth == 1)
                s *= AugmentDefs.TartarusMult(tl);
            return s;
        }
    }

    public float DamageMultiplier {
        get {
            if(!EffectsActive) return 1f;
            float blood = Level("bloodprice") > 0 ? AugmentDefs.BloodDamage(Level("bloodprice")) : 0f;
            return (1f + blood) * (1f + CurseStackTotal) * StarPowerMultiplier;
        }
    }

    public float BulletScaleMultiplier {
        get {
            if(!EffectsActive) return 1f;
            float s = 1f + stardustSizeStack;
            if(Level("bigshot") > 0) s += AugmentDefs.BigShotSize(Level("bigshot"));
            if(Synergy(AugTag.Split, 2)) s += 0.1f;
            return Mathf.Min(s, 3f);
        }
    }

    public float EnemySpeedMultiplier {
        get {
            if(!EffectsActive || Level("slaughter") <= 0) return 1f;
            float per = Synergy(AugTag.Curse, 4) ? 0.005f : 0.01f;
            return 1f + Mathf.Min(0.5f, (expKills / 1000) * per);
        }
    }

    public int ShieldBonus {
        get {
            if(!loaded) return 0;
            int b = Level("spareshield") + T(5);
            if(Level("bloodprice") > 0) b -= 1;
            return b;
        }
    }

    public int CarpetBombCount => EffectsActive && Level("carpet") > 0 && IsEquipped(SideWeapon.Bomber) ? AugmentDefs.CarpetBombs(Level("carpet")) : 0;
    public int BounceExtraCount => EffectsActive && Level("bouncegrow") > 0 && IsEquipped(SideWeapon.BounceGun) ? AugmentDefs.BounceExtra(Level("bouncegrow")) : 0;

    public static bool RollMultiShot()
    {
        var m = Instance;
        if(m == null || !m.EffectsActive) return false;
        int l = m.Level("multishot");
        float chance = l > 0 ? AugmentDefs.MultiShotChance(l) : 0f;
        if(m.Synergy(AugTag.Split, 6)) chance += 1f;
        return chance > 0 && UnityEngine.Random.value < chance;
    }

    float ExplosionRadius {
        get {
            float r = 1.3f;
            if(Level("gunpowder") > 0) r *= 1f + AugmentDefs.GunpowderRadius(Level("gunpowder"));
            if(Synergy(AugTag.Chain, 2)) r *= 1.1f;
            return r;
        }
    }

    int MaxChainDepth {
        get {
            int l = Level("chain");
            if(l <= 0) return 1;
            return AugmentDefs.ChainDepth(l) + (Synergy(AugTag.Chain, 4) ? 2 : 0);
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

    public bool IsEquipped(SideWeapon w)
    {
        if(w == SideWeapon.None) return false;
        return w == loadoutLeft || w == loadoutRight || w == extraWeapon || w == supplyWeapon;
    }

    public List<SideWeapon> OwnedWeapons()
    {
        var list = new List<SideWeapon>();
        foreach(SideWeapon w in Enum.GetValues(typeof(SideWeapon)))
            if(Owned(w)) list.Add(w);
        list.Sort((a, b) => WeaponLevel(b).CompareTo(WeaponLevel(a)));
        return list;
    }

    // 빈 칸은 가진 무기로 자동으로 채워요(새로 산 무기가 바로 보이게).
    void EnsureLoadout()
    {
        if(!Owned(loadoutLeft)) loadoutLeft = SideWeapon.None;
        if(!Owned(loadoutRight)) loadoutRight = SideWeapon.None;
        if(loadoutLeft == loadoutRight) loadoutRight = SideWeapon.None;
        if(loadoutLeft != SideWeapon.None && loadoutRight != SideWeapon.None)
            return;
        foreach(var w in OwnedWeapons()) {
            if(w == loadoutLeft || w == loadoutRight || w == extraWeapon || w == supplyWeapon) continue;
            if(loadoutLeft == SideWeapon.None) loadoutLeft = w;
            else if(loadoutRight == SideWeapon.None) loadoutRight = w;
            if(loadoutLeft != SideWeapon.None && loadoutRight != SideWeapon.None) break;
        }
        loadoutSet = true;
    }

    public void SetLoadout(SideWeapon left, SideWeapon right)
    {
        loadoutLeft = left;
        loadoutRight = right == left ? SideWeapon.None : right;
        loadoutSet = true;
    }

    float loadoutCheckTimer;
    void LateUpdate()
    {
        if(!loaded || WeaponManager.instance == null)
            return;
        loadoutCheckTimer -= Time.unscaledDeltaTime;
        if(loadoutCheckTimer <= 0f) {
            loadoutCheckTimer = 0.5f;
            EnsureLoadout();
        }
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
    static int BlessingPlanetIndex(AugTag t)
    {
        // 정렬된 행성 목록 기준: 4 헤파이스토스, 8 타르타로스, 10 아스테리아
        switch(t) {
            case AugTag.Chain: return 3;
            case AugTag.Curse: return 7;
            case AugTag.Split: return 9;
        }
        return -1;
    }

    public static string BlessingPlanetName(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "헤파이스토스(4번째)";
            case AugTag.Curse: return "타르타로스(8번째)";
            case AugTag.Split: return "아스테리아(10번째)";
        }
        return "";
    }

    public bool BlessingUnlocked(AugTag t)
    {
        if(debugAllBlessings) return true;
        int idx = BlessingPlanetIndex(t);
        var sm = StageManager.instance;
        if(sm == null || sm.planets == null || idx < 0 || idx >= sm.planets.Count) return false;
        return sm.planets[idx].PlanetCleared;
    }

    public void SetBlessing(int tagIndex) => blessing = tagIndex;
    #endregion

    #region 일시정지 (팝업용)
    public void PauseForUI()
    {
        if(pauseCount == 0) {
            pausedScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
            Time.timeScale = 0.0000001f;
        }
        pauseCount++;
    }

    public void ResumeFromUI()
    {
        if(pauseCount <= 0) return;
        pauseCount--;
        if(pauseCount == 0)
            Time.timeScale = pausedScale;
    }
    #endregion

    #region 스테이지 / 처치 / 적중
    public void OnStageCleared(int clearedStage)
    {
        if(!loaded) return;
        if(clearedStage + 1 > bestStage) bestStage = clearedStage + 1;
        if(supplyWeapon != SideWeapon.None && clearedStage >= supplyUntil) {
            ui.Toast($"보급 무기 {AugmentDefs.WeaponName(supplyWeapon)}가 돌아갔어요", AugmentDefs.TagColor(AugTag.Treasure));
            supplyWeapon = SideWeapon.None;
        }
        if(RemasterBalance.IsAugmentStage(clearedStage))
            QueuePull(1);
        ui.RefreshHUD();
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if(!EffectsActive || enemy == null) return;
        bool isBoss = enemy._typeofEnemy == Enemy.typeofEnemy.InfiniteStageBoss;
        if(enemy._typeofEnemy != Enemy.typeofEnemy.InfiniteStage && !isBoss) return;

        expKills++;
        if(isBoss) {
            expBossKills++;
        }
        if(Level("stardustshot") > 0 && expKills % 100 == 0)
            stardustSizeStack = Mathf.Min(2f, stardustSizeStack + AugmentDefs.StardustShotPer100(Level("stardustshot")));

        // 폭발: 총으로 잡으면 점화탄, 폭발로 잡으면 연쇄 폭발이 있을 때만 이어짐
        bool igniteOn = Level("ignite") > 0 || Synergy(AugTag.Chain, 6);
        if(!igniteOn) return;
        int depth = effectDepth;
        if(depth > 0 && depth >= MaxChainDepth) return;
        float mult = Level("ignite") > 0 ? AugmentDefs.IgniteDamage(Level("ignite")) : 0.5f;
        long dmg = (long)Mathf.Max(1, Player.instance.FinalAttack_Damage * mult);
        explosions.Add(new PendingExplosion {
            pos = enemy.transform.position,
            depth = depth + 1,
            damage = dmg,
            radius = ExplosionRadius,
            time = Time.time + (depth == 0 ? 0f : 0.08f)
        });

        if(depth + 1 >= 10 && Level("hammer") > 0 && Time.time >= hammerReadyTime) {
            hammerReadyTime = Time.time + 3f;
            Shockwave();
        }
    }

    public void OnEnemyHit(Enemy enemy, long damage)
    {
        if(effectDepth > 0 || inSplit || !EffectsActive) return;
        int l = Level("split");
        if(l <= 0 || UnityEngine.Random.value >= AugmentDefs.SplitChance(l)) return;
        long shard = Synergy(AugTag.Split, 4) ? damage : damage / 2;
        if(shard <= 0) return;
        var targets = NearestEnemies(enemy.transform.position, 3f, 2, enemy);
        inSplit = true;
        try {
            foreach(var t in targets) {
                RemasterFx.Line(enemy.transform.position, t.transform.position, AugmentDefs.TagColor(AugTag.Split));
                t.OnHit(shard, false, false, 0);
            }
        } finally {
            inSplit = false;
        }
    }

    void Update()
    {
        if(explosions.Count > 0) {
            if(!EffectsActive) {
                explosions.Clear();
            } else {
                for(int i = 0; i < explosions.Count; i++) {
                    if(explosions[i].time > Time.time) continue;
                    var e = explosions[i];
                    explosions.RemoveAt(i);
                    i--;
                    Explode(e);
                    if(explosions.Count > 200) { explosions.Clear(); break; }
                }
            }
        }

        if(loaded) {
            // 기존 유저 보상 후: 원정이 시작되면 1스테이지로 확실히 다시 시작(스폰 정보까지 초기화)
            if(pendingMigrationJump && InfiniteStage.Instance != null && InfiniteStage.Instance.IsRunning) {
                pendingMigrationJump = false;
                InfiniteStage.Instance.JumpToStage(1);
            }
            TryShowSetup();
        }

        DebugKeys();
    }

    void Explode(PendingExplosion e)
    {
        RemasterFx.Burst(e.pos, e.radius, AugmentDefs.TagColor(AugTag.Chain));
        var targets = EnemiesInRadius(e.pos, e.radius);
        int prev = effectDepth;
        effectDepth = e.depth;
        try {
            foreach(var t in targets)
                t.OnHit(e.damage, false, false, 0);
        } finally {
            effectDepth = prev;
        }
    }

    void Shockwave()
    {
        long dmg = (long)Mathf.Max(1, Player.instance.FinalAttack_Damage * AugmentDefs.HammerDamage(Level("hammer")));
        RemasterFx.Shockwave(WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero);
        var targets = EnemiesInRadius(Vector3.zero, 999f);
        int prev = effectDepth;
        effectDepth = 99; // 충격파로 죽은 적은 다시 폭발하지 않음
        try {
            foreach(var t in targets)
                t.OnHit(dmg, false, false, 0);
        } finally {
            effectDepth = prev;
        }
        ui.Toast("대장장이의 망치!", AugmentDefs.TagColor(AugTag.Chain));
    }

    static List<Enemy> EnemiesInRadius(Vector3 pos, float radius)
    {
        var result = new List<Enemy>();
        var em = EnemyManager.Instance;
        if(em == null) return result;
        float r2 = radius * radius;
        foreach(var list in new[] { em.enemyData, em.bossData }) {
            foreach(var e in list) {
                if(e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) continue;
                if((e.transform.position - pos).sqrMagnitude <= r2) result.Add(e);
            }
        }
        return result;
    }

    static List<Enemy> NearestEnemies(Vector3 pos, float radius, int count, Enemy exclude)
    {
        var all = EnemiesInRadius(pos, radius);
        all.Remove(exclude);
        all.Sort((a, b) => (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
        if(all.Count > count) all.RemoveRange(count, all.Count - count);
        return all;
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
        var def = Roll(out AugGrade grade, -1);
        if(def == null) {
            // 모든 증강이 최대 레벨이면 별의 먼지로 대신 지급
            pendingPulls = 0;
            stardust += 5;
            ui.Toast("모든 증강이 최대 레벨이에요! 별의 먼지 +5", AugmentDefs.TagColor(AugTag.Treasure));
            return;
        }
        pendingPulls--;
        pullShowing = true;
        shownAug = def;
        shownGrade = grade;
        PauseForUI();
        ui.ShowPull(def, Level(def.id) + 1, RerollLabel(), AcceptShown, RequestReroll);
    }

    void AcceptShown()
    {
        var def = shownAug;
        shownAug = null;
        pullShowing = false;
        ResumeFromUI();
        if(def != null) Apply(def, true);
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
        var def = Pick(treasure, grade);
        if(def == null) def = Pick(treasure, null);
        if(def == null) def = Pick(!treasure, grade);
        if(def == null) def = Pick(!treasure, null);
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
            if(d.requiredWeapon != SideWeapon.None && !IsEquipped(d.requiredWeapon)) continue;
            if(d.id == "extraarms" && FreeWeaponsForExtra().Count == 0) continue;
            if(d.id == "supply" && (supplyWeapon != SideWeapon.None || FreeWeaponsForExtra().Count == 0)) continue;
            float w = 1f;
            if(blessing >= 0 && (int)d.tag == blessing) w = 2f;
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

    List<SideWeapon> FreeWeaponsForExtra()
    {
        var list = new List<SideWeapon>();
        foreach(var w in OwnedWeapons())
            if(!IsEquipped(w)) list.Add(w);
        return list;
    }

    void Apply(AugmentDef def, bool announce)
    {
        int before = Level(def.id);
        var tagTierBefore = SynergyTier(def.tag);
        levels[def.id] = before + 1;
        if(before == 0) order.Add(def.id);

        switch(def.id) {
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
            case "supply":
                var pool = FreeWeaponsForExtra();
                if(pool.Count > 0) {
                    supplyWeapon = pool[UnityEngine.Random.Range(0, pool.Count)];
                    supplyUntil = CurrentStage + AugmentDefs.SupplyStages(Level("supply"));
                    if(announce) ui.Toast($"보급 투하: {AugmentDefs.WeaponName(supplyWeapon)} 합류!", AugmentDefs.TagColor(AugTag.Treasure));
                }
                break;
        }

        int tierAfter = SynergyTier(def.tag);
        if(announce && def.tag != AugTag.Treasure && tierAfter > tagTierBefore)
            ui.Toast($"시너지 완성! {AugmentDefs.TagName(def.tag)} {tierAfter}단계: {AugmentDefs.SynergyText(def.tag, tierAfter)}", AugmentDefs.TagColor(def.tag), true);
        ui.RefreshHUD();
    }

    int SynergyTier(AugTag t)
    {
        int c = TagCount(t);
        return c >= 6 ? 6 : c >= 4 ? 4 : c >= 2 ? 2 : 0;
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

    public string RerollLabel()
    {
        RefreshRerollDay();
        int free = FreeRerollsPerDay - freeRerollsUsed;
        if(free > 0) return $"다시 뽑기 (무료 {free})";
        if(rerollBag > 0) return $"다시 뽑기 (주머니 {rerollBag})";
        if(adRerollsUsed < AdRerollsPerDay) return $"광고 보고 다시 뽑기 ({AdRerollsPerDay - adRerollsUsed})";
        return $"다시 뽑기 (다이아 {DiamondRerollCost})";
    }

    void RequestReroll()
    {
        RefreshRerollDay();
        if(FreeRerollsPerDay - freeRerollsUsed > 0) {
            freeRerollsUsed++;
            DoReroll();
        } else if(rerollBag > 0) {
            rerollBag--;
            DoReroll();
        } else if(adRerollsUsed < AdRerollsPerDay && AdmobManager.Instance != null) {
            AdmobManager.Instance.ShowRewardedAdGeneric(() => {
                adRerollsUsed++;
                DoReroll();
            });
        } else if(GameManager.GetDiamond() >= DiamondRerollCost) {
            GameManager.SetDiamond(-DiamondRerollCost);
            DoReroll();
        } else {
            ui.Toast("다이아가 부족해요", Color.white);
        }
    }

    void DoReroll()
    {
        if(shownAug == null) return;
        var def = Roll(out AugGrade grade, -1);
        if(def == null) return;
        shownAug = def;
        shownGrade = grade;
        ui.ShowPull(def, Level(def.id) + 1, RerollLabel(), AcceptShown, RequestReroll);
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
            return g;
        }
    }

    public int TreeCost(int node) => node == StarPowerNode ? RemasterBalance.StarPowerCost(tree[node]) : TreeBaseCost[node] * (tree[node] + 1);

    public bool UpgradeTree(int node)
    {
        if(!TreeUnlocked(node)) return false;
        if(tree[node] >= TreeMax[node]) return false;
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
            case 1: return l == 0 ? "원정 시작 시 최고 기록의 일부를 건너뛰어요" : $"최고 기록의 {l * 10}%부터 출발 (건너뛴 만큼 증강 지급)";
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
        supplyWeapon = SideWeapon.None;
        supplyUntil = 0;
        explosions.Clear();
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
            stardustSizeStack = Level("stardustshot") > 0 ? Mathf.Min(2f, (expKills / 100) * AugmentDefs.StardustShotPer100(Level("stardustshot"))) : 0f;
            blessing = data.expBlessing;
            rerollBag = data.expRerollBag;
            pity = data.expPity;
            extraWeapon = (SideWeapon)data.expExtraWeapon;
            supplyWeapon = (SideWeapon)data.expSupplyWeapon;
            supplyUntil = data.expSupplyUntil;
            loadoutSet = data.expLoadoutSet;
            loadoutLeft = (SideWeapon)data.expLoadoutLeft;
            loadoutRight = (SideWeapon)data.expLoadoutRight;
            pendingSetup = data.expPendingSetup;
            freeRerollDate = data.freeRerollDate ?? "";
            freeRerollsUsed = data.freeRerollsUsed;
            adRerollsUsed = data.adRerollsUsed;
            stardust = data.stardust;
            tree = new int[TreeNames.Length];
            if(data.rebirthTree != null)
                for(int i = 0; i < tree.Length && i < data.rebirthTree.Count; i++) tree[i] = Mathf.Clamp(data.rebirthTree[i], 0, TreeMax[i]);
            bestStage = Mathf.Max(1, data.bestExpeditionStage);
            rebirthCount = data.rebirthCount;
            // 환생 직후 출발 전에 저장된 경우: 원정 준비 화면과 출발 보너스를 다시 이어서 진행
            pendingRebirthStart = data.expPendingStartStage > 0;
            rebirthStartStage = Mathf.Max(1, data.expPendingStartStage);
            if(pendingRebirthStart) pendingSetup = true;
        }
        clearedGates.Clear();
        if(data.clearedGates != null) foreach(var g in data.clearedGates) clearedGates.Add(g);
        else MigrateGatesFromPlanets();
        if(!loadoutSet) EnsureLoadout();
        loaded = true;
        ui.RefreshHUD();
    }

    public void SaveData(GameData data)
    {
        if(!loaded) return;
        data.expeditionVersion = 1;
        data.augIds = new List<string>(order);
        data.augLevels = new List<int>();
        foreach(var id in order) data.augLevels.Add(Level(id));
        data.expKills = expKills;
        data.expBossKills = expBossKills;
        data.expSoulStack = soulStack;
        data.expSlaughterStack = slaughterStack;
        data.expWraithStack = wraithStack;
        data.expBlessing = blessing;
        data.expRerollBag = rerollBag;
        data.expPity = pity;
        data.expExtraWeapon = (int)extraWeapon;
        data.expSupplyWeapon = (int)supplyWeapon;
        data.expSupplyUntil = supplyUntil;
        data.expLoadoutSet = loadoutSet;
        data.expLoadoutLeft = (int)loadoutLeft;
        data.expLoadoutRight = (int)loadoutRight;
        data.expPendingSetup = pendingSetup || migrationGift > 0;
        data.freeRerollDate = freeRerollDate;
        data.freeRerollsUsed = freeRerollsUsed;
        data.adRerollsUsed = adRerollsUsed;
        data.stardust = stardust;
        data.rebirthTree = new List<int>(tree);
        data.bestExpeditionStage = bestStage;
        data.rebirthCount = rebirthCount;
        data.expPendingStartStage = pendingRebirthStart ? rebirthStartStage : 0;
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
#endif
    }
    #endregion
}
