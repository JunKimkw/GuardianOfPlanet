using System.Collections.Generic;
using UnityEngine;

// 행성 한 판 (2026-11-25 사용자 결정, 설계: 작업 폴더 행성모드_설계.md)
//  - 행성 = 그 행성의 무기를 써 보는 짧은 한 판 (탕탕 특공대처럼 적을 잡으면 레벨업 → 카드).
//  - 들어갈 때 원정 상태(증강·누적·무기 칸·룰렛)를 planetSnap 에 보관하고 비운 뒤, 이 행성의 무기를 왼쪽 칸에 넣어요.
//    나올 때(MoveToVillage) 그대로 되돌려요. 행성 중에 저장하면 원정 상태는 보관한 것을 저장해요 (SaveData).
//  - 아직 못 산 무기는 이 판 동안만 Lv.1 로 빌려요 (저장은 0, SavedWeaponLevel).
//  - 레벨업: 첫 간격 = 맵 적 수 × planet_lv_first_ratio(3%, 최소 planet_lv_first_min 10마리), 다음 간격은 × planet_lv_growth(1.25)
//    (2026-11-25 사용자: 증강이 너무 자주 떠서 뭔지 모르겠어 → 60마리 맵 9번 → 4번, 긴 맵 15번 → 10번)
//  - planet_second_weapon_level(3) 레벨업에는 증강 대신 두 번째 무기 카드: 이미 깬 행성의 무기 (1번 행성은 없음)
public partial class AugmentManager
{
    GameData planetSnap;            // null 이 아니면 행성 한 판 중
    int planetIndex = -1;
    int planetLevel;
    int planetKills;
    float planetGap;                // 지금 레벨업 간격 (마리)
    float planetNextAt;             // 이 처치 수가 되면 레벨업
    int planetPendingLevels;
    readonly HashSet<WeaponBase> planetBorrowed = new HashSet<WeaponBase>();

    public bool InPlanetRun => planetSnap != null;
    // 다음 레벨까지 진행 (0~1, 레벨 바)
    public float PlanetLevelProgress => planetGap <= 0f ? 0f : Mathf.Clamp01(1f - (planetNextAt - planetKills) / planetGap);
    public int PlanetLevel => planetLevel;

    // 무기 저장 레벨: 행성에서 빌린 Lv.1 은 0 (WeaponManager.SaveData)
    public static int SavedWeaponLevel(WeaponBase w)
    {
        var m = Instance;
        return m != null && m.planetBorrowed.Contains(w) ? 0 : w.UpgradeLevel;
    }

    // 행성 i 의 무기 (상점의 해금 행성 번호로 찾아요)
    static SideWeapon PlanetWeapon(int planet)
    {
        var wm = WeaponManager.instance;
        if(wm == null || planet < 0) return SideWeapon.None;
        for(int i = 0; i < wm.Weapons.Count && i < wm.Shops.Count; i++)
            if(wm.Shops[i] != null && wm.Shops[i].PlanetIndex == planet) return wm.Weapons[i].Side;
        return SideWeapon.None;
    }

    void BorrowWeapon(SideWeapon w)
    {
        var wb = SideWeaponOf(w);
        if(wb == null || wb.UpgradeLevel > 0) return;
        wb.UpgradeLevel = 1;
        wb.SetDamage();
        planetBorrowed.Add(wb);
    }

    void ReturnBorrowed()
    {
        foreach(var wb in planetBorrowed) {
            if(wb == null) continue;
            wb.UpgradeLevel = 0;
            wb.SetDamage();
        }
        planetBorrowed.Clear();
    }

    // 행성 한 판 시작. map: 그 행성 안의 맵 번호 (0 = x-1)
    public void BeginPlanetRun(int planet, int map, int enemiesToKill)
    {
        if(!loaded) return;
        // 같은 행성의 다음 맵·다시 하기는 이어 가요 (2026-11-25 사용자: 맵마다 초기화하지 말고 유지 → 2-8쯤엔 합체까지)
        if(planetSnap != null && planet == planetIndex) { ContinuePlanetMap(map, enemiesToKill); return; }
        if(planetSnap != null) EndPlanetRun();
        planetSnap = new GameData();
        SaveExpeditionFields(planetSnap);

        // 원정 상태를 비워요 (planetSnap 에 있어요)
        expBossKills = 0;
        soulStack = slaughterStack = wraithStack = stardustSizeStack = 0;
        extraWeapon = SideWeapon.None;
        ResetRoulette();
        pendingPulls = 0;
        pendingRebirthStart = false;
        pendingSetup = false;
        loadoutSet = true;
        planetIndex = planet;
        planetFinished = false;
        ContinuePlanetMap(map, enemiesToKill);
    }

    // ---- 행성마다 따로, 맵마다 저장 지점 (2026-11-25 사용자)
    //  - 마을에 다녀와도·게임을 꺼도 이어져요 (GameData.planetRuns)
    //  - 클리어한 맵까지 고른 것만 남아요 (실패·나가기·다시 하기는 버려요)
    //  - 맵 k 는 언제나 '맵 k-1 을 깼을 때 상태'에서 시작해요. 깬 맵을 반복해서 증강을 쌓는 것을 막아요
    //    (사용자: 2-6 을 깨야 하는데 2-5 만 계속 깨서 증강을 계속 먹으면 안 돼요). 다시 하며 더 좋은 조합을 노리는 건 돼요
    //  - 그 행성의 마지막 맵을 깨면 그 행성 저장을 다 지워요
    readonly Dictionary<int, PlanetRunSave> planetRuns = new Dictionary<int, PlanetRunSave>();   // 열쇠 = 행성 × 1000 + 맵
    bool planetFinished;
    int planetMap;
    static int RunKey(int planet, int map) => planet * 1000 + map;

    PlanetRunSave CapturePlanetRun()
    {
        var tmp = new GameData();
        SaveStacks(tmp);
        var s = new PlanetRunSave { planet = planetIndex, map = planetMap, level = planetLevel, kills = expKills, left = (int)loadoutLeft, right = (int)loadoutRight, fusionSlots = SaveFusionSlots() };
        s.augIds = new List<string>(order);
        foreach(var id in order) s.augLevels.Add(Level(id));
        s.stackKeys = tmp.expStackKeys ?? new List<string>();
        s.stackVals = tmp.expStackVals ?? new List<float>();
        return s;
    }

    void RestorePlanetRun(PlanetRunSave s)
    {
        levels.Clear();
        order.Clear();
        for(int i = 0; s.augIds != null && i < s.augIds.Count && i < s.augLevels.Count; i++) {
            if(AugmentDefs.Get(s.augIds[i]) == null) continue;
            levels[s.augIds[i]] = s.augLevels[i];
            order.Add(s.augIds[i]);
        }
        expKills = s.kills;
        var tmp = new GameData { expStackKeys = s.stackKeys, expStackVals = s.stackVals };
        LoadStacks(tmp);
        OnAugmentsChanged();
        LoadFusionSlots(s.fusionSlots);
        loadoutLeft = s.left >= 0 ? (SideWeapon)s.left : PlanetWeapon(planetIndex);
        loadoutRight = s.right >= 0 ? (SideWeapon)s.right : SideWeapon.None;
        BorrowWeapon(loadoutLeft);
        BorrowWeapon(loadoutRight);
        planetLevel = s.level;
    }

    // 처음 상태: 이 행성의 무기 하나, 증강 없음, Lv.0
    void FreshPlanetState()
    {
        levels.Clear();
        order.Clear();
        expKills = 0;
        fusionSlots.Clear();
        ResetEffectState();
        OnAugmentsChanged();
        loadoutLeft = PlanetWeapon(planetIndex);
        BorrowWeapon(loadoutLeft);
        loadoutRight = SideWeapon.None;
        planetLevel = 0;
    }

    // 맵 map 을 시작할 때의 상태 = 맵 map-1 을 깼을 때 저장 (없으면 처음)
    void LoadMapStart(int map)
    {
        ResetEffectState();
        if(map > 0 && planetRuns.TryGetValue(RunKey(planetIndex, map - 1), out var prev)) RestorePlanetRun(prev);
        else FreshPlanetState();
    }

    // 증강 초기화 (2026-11-25 사용자, 증강 목록 화면의 버튼): 이 행성의 저장을 모두 지우고 지금 맵을 처음 상태로
    public void ResetPlanetRun()
    {
        if(planetSnap == null) return;
        ForgetPlanetRun(planetIndex);
        FreshPlanetState();
        planetPendingLevels = 0;
        ui.Toast("이 행성의 증강을 초기화했어요", Color.white, true);
        ui.RefreshHUD();
    }

    // 행성 창에서 보기·지우기 (RemasterUI.ShowPlanetRunList): 가장 멀리 깬 맵의 저장
    public PlanetRunSave PlanetRunOf(int planet)
    {
        PlanetRunSave best = null;
        foreach(var kv in planetRuns) if(kv.Value.planet == planet && (best == null || kv.Value.map > best.map)) best = kv.Value;
        return best;
    }
    public void ForgetPlanetRun(int planet)
    {
        var del = new List<int>();
        foreach(var kv in planetRuns) if(kv.Value.planet == planet) del.Add(kv.Key);
        foreach(var k in del) planetRuns.Remove(k);
    }

    // 맵 클리어 (StageManager.Cleared): 이 맵을 깼을 때 상태를 저장 (다음 맵의 출발점)
    public void OnPlanetMapCleared()
    {
        if(planetSnap == null) return;
        planetRuns[RunKey(planetIndex, planetMap)] = CapturePlanetRun();
    }

    // 그 행성의 마지막 맵을 깼어요 (StageManager.Cleared): 다음에 들어오면 처음부터
    public void OnPlanetLastMapCleared()
    {
        if(planetSnap == null) return;
        planetFinished = true;
        ForgetPlanetRun(planetIndex);
    }

    void SavePlanetRuns(GameData data)
    {
        data.planetRuns = new List<PlanetRunSave>(planetRuns.Values);
    }

    void LoadPlanetRuns(GameData data)
    {
        planetRuns.Clear();
        if(data.planetRuns == null) return;
        foreach(var s in data.planetRuns) if(s != null && s.map >= 0) planetRuns[RunKey(s.planet, s.map)] = s;   // 맵 번호가 없는 예전 저장(하루 쓴 것)은 버려요
    }

    // 맵 시작 (처음 들어올 때·다음 맵·다시 하기): 그 맵의 출발 상태로, 레벨업 간격은 이 맵 길이로
    void ContinuePlanetMap(int map, int enemiesToKill)
    {
        planetMap = Mathf.Max(0, map);
        LoadMapStart(planetMap);
        planetKills = 0;
        planetPendingLevels = 0;
        planetMapEnded = false;
        planetGap = Mathf.Max(Tuning.Get("planet_lv_first_min", 10f), enemiesToKill * Tuning.Get("planet_lv_first_ratio", 0.03f));
        planetNextAt = planetGap;
        ui.RefreshHUD();
    }

    public void EndPlanetRun()
    {
        if(planetSnap == null) return;
        if(planetFinished) ForgetPlanetRun(planetIndex);
        ReturnBorrowed();
        ResetEffectState();
        var snap = planetSnap;
        planetSnap = null;
        LoadExpeditionFields(snap);
        pendingPulls = 0;
        planetIndex = -1;
        ui.RefreshHUD();
    }

    // 행성 적 처치 (StageManager.EnemyDeadNormal)
    public void OnPlanetKill()
    {
        if(planetSnap == null) return;
        planetKills++;
        while(planetKills >= planetNextAt) {
            planetPendingLevels++;
            planetGap *= Mathf.Max(1f, Tuning.Get("planet_lv_growth", 1.25f));
            planetNextAt += planetGap;
        }
    }

    // 맵이 끝나면(클리어·실패) 남은 레벨업은 버려요 (2026-11-25 사용자: 마지막 적 처치가 증강 선택과 겹치면 증강 선택은 안 뜨게)
    //  StageManager.Cleared/Died 는 Update 에서 불려서, 같은 처치로 쌓인 레벨업이 LateUpdate 에서 뜨기 전에 지워져요
    bool planetMapEnded;
    public void OnPlanetMapEnd()
    {
        if(planetSnap == null) return;
        planetMapEnded = true;
        planetPendingLevels = 0;
        if(!pullShowing) pendingPulls = 0;
    }

    // LateUpdate (화면이 비었을 때): 쌓인 레벨업을 하나씩
    void TryPlanetLevelUp()
    {
        if(planetMapEnded) { planetPendingLevels = 0; return; }
        if(planetPendingLevels <= 0 || ui.Busy || pullShowing) return;
        planetPendingLevels--;
        planetLevel++;
        ui.Toast($"레벨 업! Lv.{planetLevel}", new Color(0.6f, 1f, 0.7f), true);
        // (2026-11-25) 예전엔 레벨 3에 두 번째 무기 카드를 무조건 줬어요 → 이제 레벨업 카드에 '추가 무기'가 확률로 섞여요 (TryPlanetSecondWeapon 은 안 써요)
        QueuePull(1);
    }

    // 두 번째 무기 카드: 이미 깬 행성의 무기 (이 행성 무기 빼고). 없으면 false → 그냥 증강
    bool TryPlanetSecondWeapon()
    {
        var wm = WeaponManager.instance;
        if(wm == null || loadoutRight != SideWeapon.None) return false;
        var pool = new List<SideWeapon>();
        for(int i = 0; i < wm.Weapons.Count && i < wm.Shops.Count; i++) {
            var shop = wm.Shops[i];
            if(shop == null || shop.PlanetIndex < 0 || shop.PlanetIndex == planetIndex || !shop.IsUnlocked) continue;
            var s = wm.Weapons[i].Side;
            if(s != SideWeapon.None && s != loadoutLeft) pool.Add(s);
        }
        if(pool.Count == 0) return false;
        for(int i = pool.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        int n = Mathf.Max(1, Tuning.GetInt("weapon_choice_count", 3));
        if(pool.Count > n) pool.RemoveRange(n, pool.Count - n);
        PauseForUI();
        ui.ShowWeaponChoice("두 번째 무기!", pool, w => {
            if(planetSnap != null && w != SideWeapon.None) {
                BorrowWeapon(w);
                loadoutRight = w;
            }
            ResumeFromUI();
            ui.RefreshHUD();
        });
        return true;
    }

    // 행성 카드 (2026-11-25 사용자: "행성에선 무기 전용 증강만, 그것도 항상은 아니고 능력치 카드랑 확률로 섞여서")
    //  계열 증강(번개·독·냉기 …)은 세트를 모아야 의미가 있는데 행성 한 판은 레벨업이 4~10번뿐이라 빼요.
    //  카드 한 장마다 planet_weapon_card_chance(60%)로 무기 증강(무기 전용·합체 카드·합체 증강), 나머지는 능력치 카드(화력·속사·약점)
    bool planetWantWeapon = true, planetWantExtra;
    bool PlanetCardFits(AugmentDef d)
    {
        if(d.grantWeapon != SideWeapon.None) return planetWantExtra && ExtraWeaponOk(d.grantWeapon);
        if(planetWantExtra) return false;
        bool weaponCard = d.tag == AugTag.Weapon && !d.planetOnly && d.id != "extraarms";
        return planetWantWeapon ? weaponCard : d.planetOnly;
    }

    // 추가 무기로 나올 수 있는 무기: 이미 깬 행성의 무기 (이 행성 무기·지금 든 무기 빼고), 두 번째 칸이 비었을 때만
    bool ExtraWeaponOk(SideWeapon w)
    {
        if(loadoutRight != SideWeapon.None || w == loadoutLeft) return false;
        var wm = WeaponManager.instance;
        var shop = wm != null ? wm.ShopOf(SideWeaponOf(w)) : null;
        return shop != null && shop.PlanetIndex >= 0 && shop.PlanetIndex != planetIndex && shop.IsUnlocked;
    }
}
