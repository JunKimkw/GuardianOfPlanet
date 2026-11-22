using System.Collections.Generic;
using UnityEngine;

// 무기 전용 증강과 무기 합체 (설계서 2-4-1, 증강_목록_초안.md 3·4장)
// - 무기 A 나 B 중 하나의 전용 증강 3개를 모두(★1 이상) 모으면 합체 카드(프리즘)가 뽑기에 나와요 (2026-11-20 양쪽 합체).
// - 카드를 고르면 재료가 된 무기의 전용 증강 3개는 사라지고, 합체 무기 전용 증강 3개가 나오기 시작해요. 동작은 항상 A 코드라서 B 로 합체하면 B 자리에 A 가 들어와요.
// - 위력: 합체 직후 A 의 3배, 합체 전용 증강을 다 모으면 5배 (WeaponPower).
// 합체했는지는 카드 증강의 레벨(1)로 저장돼서 따로 저장할 게 없어요.
public partial class AugmentManager
{
    // 원정 중일 때만 증강 레벨 (무기 코드에서 불러요)
    public static int WL(string id)
    {
        var m = Instance;
        return m != null && m.Active ? m.Level(id) : 0;
    }

    // 합체 전용 증강 3개를 모두 ★1 이상 모았는지
    public bool AllFusionAugs(int f)
    {
        foreach(var id in AugmentDefs.Fusions[f].augs) if(Level(id) <= 0) return false;
        return true;
    }

    public bool FusionOwned(int f) => f >= 0 && Level(AugmentDefs.Fusions[f].card) > 0;

    // 무기 A 가 합체한 번호 (없으면 -1)
    public int FusionOf(SideWeapon a)
    {
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++)
            if(AugmentDefs.Fusions[i].a == a && FusionOwned(i)) return i;
        return -1;
    }

    // 원정 중이고 그 합체를 했으면 true (무기 코드에서 불러요)
    public static bool Fused(int f)
    {
        var m = Instance;
        return m != null && m.Active && m.FusionOwned(f);
    }

    // ---- 양쪽 합체 (2026-11-20 사용자 결정): A 나 B 어느 쪽 무기로도 합체할 수 있어요.
    // 고른 쪽 무기(재료)의 전용 증강 3개를 모으면 카드가 나와요. 합체 무기의 동작은 항상 A 코드라서,
    // B 로 합체했는데 A 가 출격 중이 아니면 B 자리에 A 가 들어와요 (IsEquipped 에서 계산, 출격 칸 자체는 안 바꿔요).
    // B 로 합체하려면 A 무기를 가지고(상점에서 열어) 있어야 해요. 레일건·혜성탄처럼 짝이 없으면 A 로만.

    // 그 무기로 합체할 준비가 됐는지: 출격 중이고(합체 교체 전 기준) 전용 증강 3개를 다 모았음
    bool ViaReady(SideWeapon w)
    {
        if(w == SideWeapon.None || !RawEquipped(w)) return false;
        foreach(var d in AugmentDefs.WeaponAugs(w))
            if(Level(d.id) <= 0) return false;
        return true;
    }

    // 이 합체를 어느 무기로 하는지 (A 우선). 없으면 None
    public SideWeapon FusionVia(FusionDef f)
    {
        if(f == null) return SideWeapon.None;
        if(ViaReady(f.a)) return f.a;
        if(f.b != SideWeapon.None && Owned(f.a) && FusionOf(f.b) < 0 && ViaReady(f.b)) return f.b;
        return SideWeapon.None;
    }

    // 카드가 뽑기에 나올 수 있는지: A 가 아직 합체 안 했고, A 나 B 중 하나가 준비됐음
    bool FusionCardAvailable(AugmentDef card)
    {
        var f = AugmentDefs.FusionOfCard(card);
        if(f == null || FusionOf(f.a) >= 0) return false;
        if(Consumed(f.a) || Consumed(f.b)) return false;   // 이미 다른 합체의 재료로 들어간 무기는 또 못 써요
        return FusionVia(f) != SideWeapon.None;
    }

    // 다른 합체의 재료(B)로 들어가서 출격에서 빠진 무기인지
    bool Consumed(SideWeapon w)
    {
        if(w == SideWeapon.None) return false;
        if(fusionSlots.ContainsKey(w)) return true;   // A·B 같이 출격 중에 합체해서 흡수된 B
        foreach(var g in AugmentDefs.Fusions) if(g.b == w && SwappedIn(g)) return true;
        return false;
    }

    // 화면 표시용: 이 출격 칸의 무기가 합체 재료(B)로 들어갔으면 대신 들어온 A
    public SideWeapon ShownWeapon(SideWeapon w)
    {
        if(fusionSlots.TryGetValue(w, out var picked)) return picked;   // 흡수된 B 칸: 새로 고른 무기 (아직 없으면 빈칸)
        foreach(var f in AugmentDefs.Fusions) if(f.b == w && SwappedIn(f)) return f.a;
        return w;
    }

    // B 로 합체해서 A 가 B 자리를 대신하는지
    bool SwappedIn(FusionDef f) => f.b != SideWeapon.None && Level(f.card) > 0 && !RawEquipped(f.a) && RawEquipped(f.b);

    // 뽑기 후보 거르기 (Pick 에서 불러요). false 면 이번 원정에는 안 나와요
    bool WeaponAugAllowed(AugmentDef d)
    {
        if(d.tag != AugTag.Weapon) return true;
        if(d.fusionCard) return FusionCardAvailable(d);
        if(d.ultimate) return FusionOwned(d.fusion) && AllFusionAugs(d.fusion);   // 종결: 합체 전용 3개를 다 모은 뒤
        if(d.fusion >= 0) return FusionOwned(d.fusion);     // 합체 무기 전용: 합체한 뒤에만
        return FusionOf(d.requiredWeapon) < 0;              // 합체한 무기 A 의 전용 증강은 더 안 나와요
    }

    // 합체 카드를 받았을 때 (Apply 에서 불러요)
    void DoFuse(AugmentDef card, bool announce)
    {
        var f = AugmentDefs.FusionOfCard(card);
        if(f == null) return;
        // A·B 둘 다 출격 중이면 B 도 합체 무기 안으로 들어가요 (2026-11-20 사용자 결정. 예전엔 A+B 와 B 가 같이 남았어요)
        bool absorb = f.b != SideWeapon.None && RawEquipped(f.a) && RawEquipped(f.b) && FusionOf(f.b) < 0;   // B 가 이미 다른 합체 무기면 그대로 둬요
        // 재료가 된 무기의 전용 증강 3개가 사라져요 (테스트 창에서 바로 붙이면 출격 중인 쪽으로)
        var via = FusionVia(f);
        if(via == SideWeapon.None) via = !RawEquipped(f.a) && f.b != SideWeapon.None && RawEquipped(f.b) ? f.b : f.a;
        foreach(var d in AugmentDefs.WeaponAugs(via)) {
            levels.Remove(d.id);
            order.Remove(d.id);
        }
        OnAugmentsChanged();
        if(announce) {
            string swap = absorb ? $"\n{AugmentDefs.WeaponName(f.b)}도 합쳐졌어요. 빈 칸에 넣을 새 무기를 골라요"
                        : via == f.b && !RawEquipped(f.a) ? $"\n{AugmentDefs.WeaponName(f.b)} 자리에 {AugmentDefs.WeaponName(f.a)}이(가) 들어와요" : "";
            ui.Toast($"무기 합체! {AugmentDefs.FusionPair(f)}\n= {f.name} (위력 ×{AugmentDefs.FusionPower:0}){swap}", AugmentDefs.GradeColor(AugGrade.Prism), true);
        }
        if(absorb) AbsorbFusionB(f.b, announce);
    }

    // 흡수된 B 칸에 넣을 무기: 가진 무기 중 출격 안 한 것에서 무작위 3개를 보여 주고 하나를 골라요 (사용자 결정)
    // 고를 무기가 없으면 칸이 비어요. 하나뿐이거나 알림 없이 붙일 땐 바로 넣어요
    void AbsorbFusionB(SideWeapon b, bool announce)
    {
        fusionSlots[b] = SideWeapon.None;
        var free = FreeWeaponsForExtra();
        for(int i = free.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }
        if(free.Count > 3) free.RemoveRange(3, free.Count - 3);
        if(free.Count == 0) {
            if(announce) ui.Toast($"{AugmentDefs.WeaponName(b)} 칸에 넣을 무기가 없어요 (상점에서 무기를 더 열어 두세요)", Color.white);
            return;
        }
        if(!announce || free.Count == 1) {
            fusionSlots[b] = free[0];
            if(announce) ui.Toast($"{AugmentDefs.WeaponName(free[0])} 합류!", AugmentDefs.TagColor(AugTag.Treasure));
            ui.RefreshHUD();
            return;
        }
        PauseForUI();
        ui.ShowWeaponChoice($"{AugmentDefs.WeaponName(b)} 칸에 넣을 새 무기", free, w => {
            fusionSlots[b] = w;
            ResumeFromUI();
            ui.RefreshHUD();
        });
    }

    // 흡수된 B 칸이 비어 있을 때 (2026-11-21 점검): 고르는 창이 떠 있는 채로 저장·종료하면 다시 켰을 때 칸이 영영 비었어요.
    // 또 처음에 고를 무기가 없었다가 나중에 생긴 경우에도 여기서 다시 골라요 (AugmentManager.LateUpdate 에서 0.5초마다)
    void ResumeEmptyFusionSlot()
    {
        var empty = SideWeapon.None;
        foreach(var kv in fusionSlots) if(kv.Value == SideWeapon.None) { empty = kv.Key; break; }
        if(empty == SideWeapon.None || FreeWeaponsForExtra().Count == 0) return;
        AbsorbFusionB(empty, true);
    }

    // 저장: "B:새무기;…" (SideWeapon 숫자)
    string SaveFusionSlots()
    {
        var sb = new System.Text.StringBuilder();
        foreach(var kv in fusionSlots) sb.Append((int)kv.Key).Append(':').Append((int)kv.Value).Append(';');
        return sb.ToString();
    }

    void LoadFusionSlots(string s)
    {
        fusionSlots.Clear();
        if(string.IsNullOrEmpty(s)) return;
        foreach(var part in s.Split(';')) {
            var kv = part.Split(':');
            if(kv.Length == 2 && int.TryParse(kv[0], out int b) && int.TryParse(kv[1], out int w))
                fusionSlots[(SideWeapon)b] = (SideWeapon)w;
        }
    }

    // 무기 피해 배율: 합체 위력 + 숫자 없는 증강의 ★2 이상 보너스 (WeaponBase.HitDamage 에서 곱해요)
    public static float WeaponPower(SideWeapon w)
    {
        var m = Instance;
        if(w == SideWeapon.None || m == null || !m.Active) return 1f;
        float p = 1f;
        int f = m.FusionOf(w);
        if(f >= 0) {
            int got = 0;
            foreach(var id in AugmentDefs.Fusions[f].augs) if(m.Level(id) > 0) got++;
            p *= Mathf.Lerp(AugmentDefs.FusionPower, AugmentDefs.FusionPowerFull, got / 3f);
        }
        switch(w) {
            case SideWeapon.DoubleGun: p *= 1f + Q(m.Level("dg_cross")); break;
            case SideWeapon.Lazer: p *= 1f + Q(m.Level("lz_twin")); break;
        }
        return p;
    }

    static float Q(int L) => L > 0 ? AugmentDefs.QualityBonus(L) : 0f;

    // 무기 증강 폭발 (폭발 탄두, 관통 폭발). 연쇄 폭발·용광로 같은 폭발 증강과 이어져요
    public static void WeaponBlast(Vector3 pos, long damage, float radius)
    {
        var m = Instance;
        if(m == null || !m.Active || damage <= 0) return;
        m.explosions.Add(new PendingExplosion { pos = pos, depth = 1, damage = damage, radius = radius, time = Time.time });
    }

    // 무기 코드에서 쓰는 적 찾기 (화면 안, 살아 있는 적)
    public static List<Enemy> EnemiesNear(Vector3 pos, float radius) => EnemiesInRadius(pos, radius);

    public static List<Enemy> NearestInRange(Vector3 pos, int count, ICollection<Enemy> exclude = null, bool bossFirst = false)
    {
        var all = AllEnemies();
        all.RemoveAll(e => !e.IsInRange || (exclude != null && exclude.Contains(e)));
        all.Sort((a, b) => {
            if(bossFirst && a.IsBoss != b.IsBoss) return a.IsBoss ? -1 : 1;
            return (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude);
        });
        if(all.Count > count) all.RemoveRange(count, all.Count - count);
        return all;
    }

    // 화면 안에서 체력이 가장 많은 적 (보스 먼저)
    public static Enemy StrongestInRange()
    {
        Enemy best = null;
        foreach(var e in AllEnemies()) {
            if(!e.IsInRange) continue;
            if(best == null || (e.IsBoss && !best.IsBoss) || (e.IsBoss == best.IsBoss && e.CurrentHealth > best.CurrentHealth)) best = e;
        }
        return best;
    }

    // 무기 효과로 주는 피해 (레이저 종류로 처리: 굴절 등)
    // pen: 방어 관통력. 음수면 증강 효과 기본값(Armor.EffectPen)
    public static void WeaponHit(Enemy e, long damage, bool crit, float critDamage, HitKind kind = HitKind.Weapon, float pen = -1f)
    {
        if(e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) return;
        var prev = CurrentHitKind;
        CurrentHitKind = kind;
        try { e.OnHit(damage, false, crit, critDamage, pen >= 0f ? pen : Armor.EffectPen); }
        finally { CurrentHitKind = prev; }
    }

    // ---- 개발용: 테스트 창의 "무기 고르기"
    public bool DebugPickMode => debugPickMode;
    public bool DebugPicked(SideWeapon w) => debugPickMode && debugPicked.Contains(w);

    // 없는 무기는 Lv.1 로 줘요 (Lv.1 은 저장돼요)
    void DebugEnsureOwned(SideWeapon w)
    {
        var weapon = SideWeaponOf(w);
        if(weapon != null && weapon.UpgradeLevel <= 0) { weapon.UpgradeLevel = 1; weapon.SetDamage(); }
        WeaponShopBase.DebugUnlockAll = true;
    }

    void DebugStartPick()
    {
        if(debugPickMode) return;
        debugPickMode = true;
        debugEquipAll = false;
        debugPicked.Clear();
    }

    public void DebugToggleWeapon(SideWeapon w)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStartPick();
        DebugEnsureOwned(w);
        bool on = !debugPicked.Remove(w);
        if(on) debugPicked.Add(w);
        DebugDropSwaps(w, on);
        ui.RefreshHUD();
#endif
    }

    // 합체 무기 고르기: A 를 출격하고 바로 합체 (A 가 다른 합체 중이면 풀고 바꿔요)
    public void DebugPickFusion(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var fd = AugmentDefs.Fusions[f];
        DebugStartPick();
        DebugEnsureOwned(fd.a);
        debugPicked.Add(fd.a);
        int cur = FusionOf(fd.a);
        if(cur == f) { DebugUnfuse(f); ui.RefreshHUD(); return; }   // 다시 누르면 합체 풀기
        if(cur >= 0) DebugUnfuse(cur);
        DebugDropSwaps(fd.a, true);
        Apply(AugmentDefs.Get(fd.card), true);
#endif
    }

    // 개발용 (2026-11-20 버그: 사냥개 탄을 골랐는데 폭격기가 나감): 테스트 창에서 무기를 켜고 끌 때, 예전에 붙인 다른 합체 때문에
    // "B 자리에 A 가 들어오는" 교체가 일어나 엉뚱한 무기가 나오지 않게 그 합체를 풀어요.
    //  - w 를 켰는데 w 가 다른 합체의 B 이고 그 합체의 A 는 꺼져 있음 → w 대신 A 가 나옴 → 그 합체를 풀어요
    //  - w 를 껐는데 w 가 합체의 A 이고 B 는 켜져 있음 → 끈 A 가 B 자리에 다시 나옴 → 그 합체를 풀어요
    //  흡수된 B(fusionSlots)를 다시 켜면 흡수도 풀어요. B 쪽 합체 버튼(DebugFuse)으로 일부러 만든 교체는 그대로 둬요
    void DebugDropSwaps(SideWeapon w, bool on)
    {
        if(on) fusionSlots.Remove(w);
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++) {
            var g = AugmentDefs.Fusions[i];
            if(!FusionOwned(i) || g.b == SideWeapon.None) continue;
            bool bad = on ? g.b == w && !debugPicked.Contains(g.a) : g.a == w && debugPicked.Contains(g.b);
            if(bad) DebugUnfuse(i);
        }
    }

    // 합체 풀기: 카드와 합체 전용 증강을 지워요
    void DebugUnfuse(int f)
    {
        var fd = AugmentDefs.Fusions[f];
        levels.Remove(fd.card); order.Remove(fd.card);
        foreach(var id in fd.augs) { levels.Remove(id); order.Remove(id); }
        levels.Remove(AugmentDefs.UltIds[f]); order.Remove(AugmentDefs.UltIds[f]);
        fusionSlots.Remove(fd.b);   // 흡수했던 B 를 되돌려요 (새로 고른 무기는 빠져요)
        OnAugmentsChanged();
    }

    public void DebugPickAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStartPick();
        foreach(SideWeapon w in System.Enum.GetValues(typeof(SideWeapon)))
            if(w != SideWeapon.None) { DebugEnsureOwned(w); debugPicked.Add(w); }
        ui.RefreshHUD();
#endif
    }

    // ---- 개발용 연습장 (2026-11-20 사용자 요청): 멈춰 있는 적을 많이 세워 두고 무기를 하나씩 바꿔 가며 봐요
    // 켜는 동안 새 적은 안 나오고(InfiniteStage), 테스트 모드라 적이 죽지 않아요(체력이 다시 차요). 누를 때마다 자리를 새로 섞어요 (작은 적으로)
    public static bool DebugArena;
    public static bool DebugArenaInvincible;   // 연습장 "적 무적" (2026-11-21): 켜면 적이 안 죽고 체력이 다시 차요 (예전 연습장처럼)

    public void DebugArenaStart(int count)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var stage = InfiniteStage.Instance;
        var em = EnemyManager.Instance;
        if(stage == null || em == null || !stage.IsRunning || stage.enemyPrefab == null || stage.enemyPrefab.Length == 0) {
            ui.Toast("[개발] 원정 전투 중에만 연습장을 열 수 있어요", Color.white);
            return;
        }
        DebugArena = true;
        DebugTestMode = true;
        em.removeAllEnemies();
        arenaHome.Clear();
        arenaTarget = count;
        var spots = ArenaSpots();
        int n = Mathf.Min(count, spots.Count);
        for(int i = 0; i < n; i++) SpawnArenaEnemy(spots[i]);
        ui.Toast($"[개발] 연습장: 멈춘 적 {n}마리 (지금 무기로 약 {Tuning.GetInt("arena_hits", 15)}대에 죽고, 죽으면 새로 나와요)", Color.white);
#endif
    }

    // ---- 연습장 적 (2026-11-21 사용자: "지금 데미지로 15대쯤 맞으면 죽는 체력, 죽으면 새로 소환")
    int arenaTarget;
    float arenaTick;

    // 화면을 7 x 13 칸으로 나눈 자리 (우주선 둘레 1.6칸 빼고), 섞어서
    List<Vector3> ArenaSpots()
    {
        var cam = GameManager.instance.mainCamera;
        float w = cam.orthographicSize * cam.aspect, h = cam.orthographicSize;
        Vector3 c = cam.transform.position;
        c.z = 0f;
        Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : c;
        ship.z = 0f;
        var spots = new List<Vector3>();
        for(int gy = 0; gy < 13; gy++) for(int gx = 0; gx < 7; gx++) {
            var p = new Vector3(c.x + Mathf.Lerp(-w + 0.5f, w - 0.5f, gx / 6f), c.y + Mathf.Lerp(-h + 0.8f, h - 0.8f, gy / 12f), 0f);
            if((p - ship).magnitude >= 1.6f) spots.Add(p);
        }
        for(int i = spots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
        return spots;
    }

    // 작은 적 하나: 가장 작은 종류(그래도 크면 arena_enemy_size 칸으로 줄임), 멈춤, 체력 = 지금 무기 1발 × arena_hits, 방어력 0 (몇 대에 죽는지 보이게)
    void SpawnArenaEnemy(Vector3 at, bool exact = false)
    {
        var stage = InfiniteStage.Instance;
        var em = EnemyManager.Instance;
        if(stage == null || em == null || stage.enemyPrefab == null || stage.enemyPrefab.Length == 0) return;
        em.createInfiniteEnemy(SmallestPrefab(stage.enemyPrefab), stage.EnemyScale);
        var e = em.enemyData[em.enemyData.Count - 1];
        e.transform.position = exact ? at : at + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0f);
        float want = Tuning.Get("arena_enemy_size", 0.7f), size = VisualSize(e.gameObject);
        if(size > want) e.ApplyLook(ExpeditionZones.StageTint(stage.CurrentStage), want / size);
        e.Armor = 0;
        e.EnemyHealth = e.CurrentHealth = System.Math.Max(1.0, ArenaRefDamage() * e.NumberScale * Tuning.Get("arena_hits", 15f));
        e.DebugFrozen = true;
        arenaHome[e] = e.transform.position;
    }

    // 연습장 적이 처음 선 자리 (2026-11-22 밤새 측정 점검: 블랙홀·특이점 저격·중력 폭탄이 끌어모으거나 밀어낸 적이
    //  다음 무기 측정까지 그대로 남아서, 뒤 무기가 모인 적을 때리거나(이득) 화면 밖으로 밀린 적을 못 때렸어요(피해 0))
    //  → 측정에서 무기를 바꿀 때마다 이 자리로 되돌려요
    readonly Dictionary<Enemy, Vector3> arenaHome = new Dictionary<Enemy, Vector3>();
    public void DebugArenaResetSpots()
    {
        var gone = new List<Enemy>();
        foreach(var kv in arenaHome) {
            var e = kv.Key;
            if(e == null || !e.gameObject.activeInHierarchy || e._state == Enemy.EnemyState.dead) { gone.Add(e); continue; }
            e.transform.position = kv.Value;
        }
        foreach(var e in gone) arenaHome.Remove(e);
    }

    // 연습장 적을 기억한 자리에 새로 세워요 (2026-11-22 사용자: "측정할 때 이전 무기에 영향 안 가게")
    //  자리만 옮기면 앞 무기가 건 독·느려짐·기절·표식이 그대로 남아서, 적을 지우고 같은 자리에 새 적을 세워요 (보스·단단한 적 방어력도 그대로)
    double measureArenaArmor;
    public void DebugArenaRespawnHome()
    {
        var em = EnemyManager.Instance;
        if(em == null || !DebugArena) return;
        var spots = new List<Vector3>();
        bool boss = false;
        foreach(var kv in arenaHome) {
            var e = kv.Key;
            if(e == null || !e.gameObject.activeInHierarchy || e._state == Enemy.EnemyState.dead) continue;
            if(em.bossData.Contains(e)) boss = true; else spots.Add(kv.Value);
        }
        em.removeAllEnemies();
        arenaHome.Clear();
        foreach(var p in spots) SpawnArenaEnemy(p, true);
        if(boss) DebugArenaBoss();
        if(measureArenaArmor > 0) foreach(var e in em.enemyData) if(e != null) e.Armor = measureArenaArmor;
    }

    // 앞 무기 영향 끊기: 멈춘 적은 새로 세우고, 움직이는 적은 화면의 적을 다 치워요 (스테이지가 다시 내보내요)
    void MeasureIsolate(int mode)
    {
        if(IsMovingMode(mode)) { if(EnemyManager.Instance != null) EnemyManager.Instance.removeAllEnemies(); }
        else DebugArenaRespawnHome();
    }

    // 측정의 미리 돌리기: 새 무기로 바꾼 뒤 measure_isolate 초(앞 무기의 독 장판·블랙홀·종결 효과가 끝날 시간)를 기다렸다가
    //  적을 한 번 더 새로 세우고, 그다음 미리 돌리기(warm)를 해요. 이 동안의 피해는 기록에 안 들어가요
    static float MeasureIsolateSeconds(int mode) => mode == 13 ? 0f : Mathf.Max(0f, Tuning.Get("measure_isolate", 5f));
    System.Collections.IEnumerator MeasureWarm(int mode, float warm)
    {
        float iso = MeasureIsolateSeconds(mode);
        if(iso > 0f) {
            yield return new WaitForSeconds(iso);
            MeasureTry(() => MeasureIsolate(mode), "앞 무기 영향 끊기");
        }
        if(warm > 0f) yield return new WaitForSeconds(warm);
    }

    // 기준 피해: 연습장에서 혼자 싸우는 무기 1발 (합체 위력 포함). 여러 개면 그중 가장 센 것 (기본총 포함)
    double ArenaRefDamage()
    {
        var wm = WeaponManager.instance;
        if(wm == null) return 1;
        var solo = DebugSoloWeapon();
        var sw = solo != SideWeapon.None ? SideWeaponOf(solo) : null;
        if(sw != null) return System.Math.Max(1, sw.HitDamage * ArenaHitScale(FusionOf(solo)));
        long best = wm.mainWeapon != null ? wm.mainWeapon.HitDamage : 1;
        foreach(var w in wm.Weapons) if(w != null && w.gameObject.activeSelf) best = System.Math.Max(best, w.HitDamage);
        return System.Math.Max(1, best);
    }

    // 합체 무기 중 "1발"이 무기 피해(HitDamage)와 크게 다른 것 (2026-11-21 사용자: 위성 레이저가 15대를 맞아도 안 죽어요)
    //  위성 레이저: 위성 1틱 = 360 레이저 1발 × satlaser_tick_mul (세기 1배 기준) / 혜성탄: 머리 1번 = × comet_hit_mul / 별의 낙인: 운석 1발 = ÷ brand_damage_div
//  마이크로 미사일: × micro_damage_mul / 별빛 섬광: 빛살 1번 = 360 레이저 1발 × starlight_hit_mul
    //  나머지 무기·합체는 1발(또는 1틱) ≈ HitDamage 라서 그대로
    static double ArenaHitScale(int fusion)
    {
        switch(fusion) {
            case AugmentDefs.FzSatLaser: return Tuning.Get("satlaser_tick_mul", 0.0072f);
            case AugmentDefs.FzConstellation: return Tuning.Get("comet_hit_mul", 0.4f);
            case AugmentDefs.FzBrand: return 1.0 / Mathf.Max(1f, Tuning.Get("brand_damage_div", 3f));
            case AugmentDefs.FzMicro: return Tuning.Get("micro_damage_mul", 0.3f);
            case AugmentDefs.FzStarlight: return Tuning.Get("starlight_hit_mul", 0.4f);
        }
        return 1.0;
    }

    // 죽은 만큼 빈자리(다른 적에서 0.6칸, 우주선에서 1.6칸 밖)에 새 적 (0.3초마다 확인, AugmentManager.LateUpdate)
    void DebugArenaTick()
    {
        if(!DebugArena || arenaTarget <= 0) return;
        arenaTick -= Time.deltaTime;
        if(arenaTick > 0f) return;
        arenaTick = 0.3f;
        var em = EnemyManager.Instance;
        if(em == null) return;
        var alive = new List<Vector3>();
        foreach(var e in em.enemyData)
            if(e != null && e.DebugFrozen && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy) alive.Add(e.transform.position);
        int missing = arenaTarget - alive.Count;
        if(missing <= 0) return;
        foreach(var p in ArenaSpots()) {
            if(missing <= 0) break;
            bool free = true;
            foreach(var q in alive) if(((Vector2)(q - p)).sqrMagnitude < 0.36f) { free = false; break; }
            if(!free) continue;
            SpawnArenaEnemy(p);
            alive.Add(p);
            missing--;
        }
    }

    // 그림이 가장 작은 적 종류 (스프라이트 크기 × 프리팹 크기)
    static Enemy SmallestPrefab(Enemy[] prefabs)
    {
        Enemy best = null;
        float bestSize = float.MaxValue;
        foreach(var p in prefabs) {
            if(p == null) continue;
            float s = 0f;
            foreach(var sr in p.GetComponentsInChildren<SpriteRenderer>(true)) {
                if(sr.sprite == null || sr.name == "Shadow") continue;
                Vector3 b = sr.sprite.bounds.size, sc = sr.transform.lossyScale;
                s = Mathf.Max(s, Mathf.Max(b.x * Mathf.Abs(sc.x), b.y * Mathf.Abs(sc.y)));
            }
            if(s > 0f && s < bestSize) { bestSize = s; best = p; }
        }
        return best != null ? best : prefabs[0];
    }

    // 화면에 보이는 크기 (그림자 빼고 가장 큰 그림의 가로·세로 중 큰 쪽)
    static float VisualSize(GameObject go)
    {
        float s = 0f;
        foreach(var sr in go.GetComponentsInChildren<SpriteRenderer>()) {
            if(sr.sprite == null || sr.name == "Shadow") continue;
            Vector3 b = sr.bounds.size;
            s = Mathf.Max(s, Mathf.Max(b.x, b.y));
        }
        return s;
    }

    // 보스를 위쪽 가운데에 멈춰 세워요
    public void DebugArenaBoss()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var em = EnemyManager.Instance;
        if(em == null || InfiniteStage.Instance == null || !InfiniteStage.Instance.DebugSpawnBoss()) { ui.Toast("[개발] 원정 전투 중에만 부를 수 있어요", Color.white); return; }
        var b = em.bossData[em.bossData.Count - 1];
        var cam = GameManager.instance.mainCamera;
        b.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y + cam.orthographicSize * 0.55f, 0f);
        b.DebugFrozen = DebugArena;
        if(DebugArena) arenaHome[b] = b.transform.position;
        if(DebugArena) { b.Armor = 0; b.EnemyHealth = b.CurrentHealth = System.Math.Max(1.0, ArenaRefDamage() * b.NumberScale * Tuning.Get("arena_hits", 15f) * Tuning.Get("arena_boss_mul", 10f)); }   // 연습장 보스: 일반 적의 arena_boss_mul 배 (죽으면 다시 안 나와요)
#endif
    }

    public void DebugArenaStop()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugArena = false;
        arenaTarget = 0;
        var em = EnemyManager.Instance;
        if(em != null) {
            foreach(var e in em.enemyData) if(e != null) e.DebugFrozen = false;
            foreach(var e in em.bossData) if(e != null) e.DebugFrozen = false;
        }
        ui.Toast("[개발] 연습장 끔 (적이 다시 움직이고 새로 나와요)", Color.white);
#endif
    }

    // ---- 개발용 측정 모드 (2026-11-22 사용자: "측정 모드를 누르면 무기가 순서대로 바뀌게, 보스랑 멈춘 적도 버튼으로")
    //  상황을 차려 놓고(증강 없이, 적이 안 죽게) 게임 속도 x5, DPS 기록을 켠 뒤 기본 무기 10개를 measure_seconds(게임 30초 = 현실 6초)씩 차례로 혼자 출격시켜요.
    //  무기를 바꿀 때마다 DevTools 가 앞 무기 측정을 Tuning/dps_runs.txt 에 "[상황] 무기" 로 한 줄씩 남겨요 (바꾼 직후 measure_warmup 초는 빼요: 앞 무기 독 장판 등이 섞이지 않게)
    //  0 = 움직이는 적 (1스테이지, 테스트 모드, 적 많이), 1 = 멈춘 적 40 (연습장, 적 무적), 2 = 보스 1 (연습장 적 0 + 멈춘 보스, 적 무적)
    //  3 = 단단한 적 40 (방어력 = 공격력 × measure_armor), 4 = 흩어진 소수 (measure_scatter_count 마리), 5 = 우주선 근처 (둘레에 measure_near_count 마리) (2026-11-22 추가)
    // ---- 개발용 측정 모드 (2026-11-22, 연습장 창 아래 "측정" 버튼). 결과는 Tuning/dps_measure.txt 에 무기·회차마다 한 줄씩
    //  측정 방식 고침 (2026-11-22 사용자: "측정이 완벽하게 된 게 아니네, 4개 다 고쳐줘"):
    //   1) 칸마다 measure_repeats(3)번 재요. 회차마다 연습장 적 자리를 새로 섞어서, 무기마다 같은 3가지 배치를 똑같이 겪어요. 정리할 때 평균을 내요.
    //   2) 무기를 바꾼 뒤 measure_warmup(10)초 + 0~measure_warmup_jitter(5)초를 미리 돌리고 나서 measure_seconds(20)초를 재요.
    //      360 레이저(처음 10초 대기)·블랙홀(8초)·저격총(6초)·폭격기(5초)처럼 시작이 느린 무기가 손해 보지 않고, 회차마다 시작 시점이 달라 주기 운도 평균돼요.
    //   3) 움직이는 적: 더 많이(화면 최대 measure_max_enemies 120) 더 빨리(measure_spawn_mul 10배) 나오고, 증강 단계가 오를수록 적 체력도
    //      measure_hp_aug_0~4 (1·1.5·2·3·4배)로 올라가요. 센 무기가 적이 나오기를 기다리느라 값이 막히지 않게 (실제 게임에서 강해지면 높은 스테이지로 가는 것과 같아요)
    //   4) 속도 비교: 멈춘 적 40 한 배치에서 무기마다 x1 과 measure_speed(x5)로 번갈아 재서, 빠른 속도에서 탄이 적을 뚫고 지나가는지 봐요
    //  상황: 0 = 움직이는 적 (1스테이지, 적이 죽음), 1 = 멈춘 적 40, 2 = 보스 1, 3 = 단단한 적 40 (방어력 = 공격력 × measure_armor),
    //        4 = 흩어진 소수, 5 = 우주선 근처. 1~5 는 연습장(멈춘 적, 적 무적)
    //        (2026-11-22 추가) 11 = 빠른 적 (움직이는 적, 이동 속도 × measure_fast_mul), 12 = 보스 + 졸개 (멈춘 보스 1 + 졸개 measure_minions 마리, 적 무적),
    //        13 = 전투 시작 직후 (움직이는 적, 미리 돌리지 않고 처음 measure_burst_seconds 초만)
    //  버튼: 0~5 = 그 상황, 6 = 여섯 상황 전부, 7 = 증강 5단계 × 여섯 상황, 8 = 빠진 칸만 다시, 9 = 속도 비교, 10 = 밤새 측정

    // 측정 "움직이는 적": 테스트 모드(끝없이 나오고 스테이지 안 넘어감)인데도 적이 진짜 죽어요. 넘친 피해는 DPS 에서 빠져요 (실제 게임처럼)
    //  체력 = 플레이어 공격력 × measure_hits (모든 무기 1발이 거의 공격력이라 약 15대) × 증강 단계 배율, 방어력 0
    public static bool DebugMortal;
    public static bool DebugNoDeath => DebugTestMode && (DebugArena ? DebugArenaInvincible : !DebugMortal);   // 개발용: 적이 안 죽는 상태
    static readonly float[] MeasureHpDefaults = { 1f, 1.5f, 2f, 3f, 4f };
    public static float MeasureHpMul => measureSoloAug != null ? Tuning.Get("measure_hp_solo", 3f) : MeasureFusionStage >= 0
        ? Tuning.Get("measure_hp_fusion_" + MeasureFusionStage, MeasureFusionHpDefaults[Mathf.Clamp(MeasureFusionStage, 0, 2)])
        : Tuning.Get("measure_hp_aug_" + MeasureAugStage, MeasureHpDefaults[Mathf.Clamp(MeasureAugStage, 0, 4)]);
    public static void DebugMortalSetup(Enemy e)
    {
        if(e == null || Player.instance == null) return;
        e.Armor = 0;
        e.EnemyHealth = e.CurrentHealth = System.Math.Max(1.0, (double)Player.instance.FinalAttack_Damage * e.NumberScale * Tuning.Get("measure_hits", 15f) * MeasureHpMul);
        if(MeasureFastEnemies) e.enemyMovingSpeed *= Tuning.Get("measure_fast_mul", 2f);
    }
    public static bool MeasureFastEnemies;   // 측정 "빠른 적"
    static bool IsMovingMode(int mode) => mode == 0 || mode == 11 || mode == 13;
    // 측정 "움직이는 적" 중에는 적이 더 많이, 더 빨리 (InfiniteStage 가 읽어요)
    public static bool MeasuringMoving => DebugMortal && MeasureScene != null;

    public static string MeasureScene;   // 지금 측정 상황 (기록 줄의 [ ] 안). null = 측정 모드 아님
    Coroutine measureCo;
    public bool Measuring => measureCo != null;
    float measurePrevSpeed = 1f;
    int measurePrevAugStage;

    public void DebugMeasure(int mode)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(measureCo != null) { StopCoroutine(measureCo); DevTools.MeasureAbort(); MeasureFinish(); }
        measureCo = StartCoroutine(MeasureRun(mode));
#endif
    }

    public void DebugMeasureStop()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(measureCo != null) { StopCoroutine(measureCo); measureCo = null; }
        DevTools.MeasureAbort();   // 재던 중인 무기는 덜 쟀으니 안 남겨요
        MeasureFinish();
        ui.Toast("[개발] 측정 멈춤 (결과: Tuning/dps_measure.txt)", Color.white);
#endif
    }

    void MeasureFinish()
    {
        MeasureScene = null;
        DebugMortal = false;
        MeasureAugStage = measurePrevAugStage;
        MeasureFusionStage = -1;
        MeasureFastEnemies = false;
        measureSpeedOverride = -1f;
        measureRepeatsOverride = -1;
        measureSoloAug = null;
        measureArenaArmor = 0;
        if(GameManager.instance != null) GameManager.instance.setTimeScale(measurePrevSpeed);
    }

    // 측정할 때 무기마다 넣는 그 무기 전용 증강 3개의 상태 (2026-11-22 사용자: 합체 전에 전용 증강은 거의 무조건 모으니까 단계별로 봐야 해요)
    //  0 = 없음, 1 = 일부 (3개 중 앞 2개만 1레벨), 2 = 3개 모두 1레벨, 3 = 3개 모두 중간 레벨 (최대의 절반, 올림), 4 = 3개 모두 최대
    public static int MeasureAugStage;
    public static readonly string[] MeasureAugNames = { "증강 없음", "증강 일부", "증강 1레벨", "증강 중간", "증강 최대" };

    System.Collections.IEnumerator MeasureRun(int mode)
    {
        var stage = InfiniteStage.Instance;
        if(stage == null || !stage.IsRunning) { ui.Toast("[개발] 원정 전투 중에만 측정할 수 있어요", Color.white); measureCo = null; yield break; }
        if(DevTools.DpsOn) DevTools.ToggleDps();   // 손으로 켠 DPS 기록은 꺼요
        measurePrevSpeed = Time.timeScale;
        measurePrevAugStage = MeasureAugStage;
        DebugClearAugments();
        yield return null;      // 2026-11-22 버그: 플레이어 공격력이 다음 프레임에 다시 계산돼서, 바로 읽으면 증강이 붙은 옛 값이었어요 (방어력이 6배로 나옴)
        yield return null;
        string title = mode == 15 ? "증강 하나씩 (3상황)" : mode == 10 ? "밤새 측정 (속도 비교 · 기본 무기 · 추가 상황 · 합체 무기 · 종결)" : mode == 7 ? "증강 5단계 × 6상황" : mode == 6 ? "6상황 · " + MeasureAugNames[MeasureAugStage] : mode == 8 ? "빈 칸만 다시" : mode == 9 ? "속도 비교" : "한 상황 · " + MeasureAugNames[MeasureAugStage];
        DevTools.MeasureLine($"---- 측정 시작 {System.DateTime.Now:MM-dd HH:mm:ss} · {title} · 회차 {MeasureRepeats} · 미리 {Tuning.Get("measure_warmup", 10f):0.#}초 + 재기 {Tuning.Get("measure_seconds", 20f):0.#}초");
        if(mode == 7) {
            for(int a = 0; a < MeasureAugNames.Length; a++) {
                MeasureAugStage = a;
                for(int m = 0; m <= 5; m++) yield return MeasureSceneRun(m);
            }
        }
        else if(mode == 6) { for(int m = 0; m <= 5; m++) yield return MeasureSceneRun(m); }
        else if(mode == 8) yield return MeasureMissing();
        else if(mode == 9) yield return MeasureSpeedCompare(new[] { 1f, Tuning.Get("measure_speed", 5f) });
        else if(mode == 10) yield return MeasureOvernight();
        else if(mode == 15) yield return MeasureSoloAugs();
        else yield return MeasureSceneRun(mode);
        MeasureFinish();
        ui.Toast($"[개발] 측정 끝 · {title} (결과: Tuning/dps_measure.txt)", Color.white);
        measureCo = null;
    }

    // 2026-11-22 (밤새 측정 점검): 무기 바꾸기·상황 차리기에서 오류가 한 번 나면 측정 전체가 멈춰서 몇 시간이 허탕이 돼요.
    //  그래서 그런 곳은 여기로 감싸서, 오류가 나면 그 칸만 건너뛰고 dps_measure.txt 에 "!!" 줄로 남긴 뒤 계속해요
    bool MeasureTry(System.Action a, string what)
    {
        try { a(); return true; }
        catch(System.Exception ex) {
            Debug.LogError($"[측정] {what} 실패: {ex}");
            DevTools.MeasureLine($"!! {System.DateTime.Now:HH:mm:ss} {what} 실패: {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }

    static int MeasureRepeats => measureRepeatsOverride > 0 ? measureRepeatsOverride : Mathf.Max(1, Tuning.GetInt("measure_repeats", 3));

    static List<SideWeapon> AllSideWeapons()
    {
        var list = new List<SideWeapon>();
        foreach(SideWeapon w in System.Enum.GetValues(typeof(SideWeapon))) if(w != SideWeapon.None) list.Add(w);
        return list;
    }

    // ---- 밤새 측정 (2026-11-22 사용자: "자는 동안 6시간쯤 돌리게, 측정할 수 있는 건 다") — 버튼 mode 10
    //  1) 속도 비교 x1 · overnight_speed(x3) · measure_speed(x5)  (멈춘 적 40 한 배치, 증강 없이)
    //  2) 기본 무기 10개 × 전용 증강 5단계 × 6상황 × overnight_base_repeats(4)회
    //  3) 합체 무기 13개 × 2단계(합체만 / 합체 증강 3개 최대) × 6상황 × overnight_fusion_repeats(3)회 (A 무기 전용 증강은 합체하면 없어지니까 빼요)
    //  4) 종결 13개 × 6상황 × overnight_ult_repeats(2)회: 합체 증강 최대로 미리 돌린 뒤 종결을 한 번 쓰고 measure_ult_seconds(12)초를 재요.
    //     종결 몫 = 이 줄의 피해 − 같은 상황 "합체 증강 최대" 줄의 DPS × 같은 시간 (정리할 때 계산해요)
    //  2~4 는 overnight_speed(x3)로 재요 (x5 는 속도 비교 결과를 보기 전이라 빗나감이 걱정돼서). 미리 돌리기·재는 시간은 measure_* 그대로
    static float measureSpeedOverride = -1f;
    static int measureRepeatsOverride = -1;
    static float MeasureSpeed => measureSpeedOverride > 0f ? measureSpeedOverride : Tuning.Get("measure_speed", 5f);

    // 합체 측정 단계: -1 = 합체 아님, 0 = 합체만, 1 = 합체 증강 최대, 2 = 종결 (합체 증강 최대 + 종결 한 번)
    public static int MeasureFusionStage = -1;
    static readonly string[] MeasureFusionNames = { "합체", "합체 증강 최대", "종결" };
    static readonly float[] MeasureFusionHpDefaults = { 4f, 8f, 8f };   // 움직이는 적 체력 배율 (합체는 A 의 3~5배라서)

    System.Collections.IEnumerator MeasureOvernight()
    {
        // 2026-11-22 사용자: "x1 이 제일 정확하니 x1, 아니면 1.5" → x1.5 로, 6시간 안에 들어가게 증강 3단계(없음·1레벨·최대)·회차를 줄였어요
        float speed = Tuning.Get("overnight_speed", 1.5f);
        // 1) 속도 비교 x1 vs 밤새 속도 (그 속도가 x1 과 같은 값을 내는지)
        yield return MeasureSpeedCompare(new[] { 1f, speed });
        measureSpeedOverride = speed;
        // 2) 기본 무기
        measureRepeatsOverride = Mathf.Max(1, Tuning.GetInt("overnight_base_repeats", 3));
        foreach(int a in new[] { 0, 2, 4 }) {   // 증강 없음 · 1레벨 · 최대 ("일부"·"중간"은 시간 때문에 빼요)
            MeasureAugStage = a;
            for(int m = 0; m <= 5; m++) yield return MeasureSceneRun(m);
        }
        // 2-2) 추가 상황 (2026-11-22 사용자: "6개 상황 말고 더 추가해도 돼"): 빠른 적 · 보스 + 졸개 · 시작 직후. 시간 때문에 증강 없음·최대만
        measureRepeatsOverride = Mathf.Max(1, Tuning.GetInt("overnight_extra_repeats", 2));
        foreach(int a in new[] { 0, 4 }) {
            MeasureAugStage = a;
            foreach(int m in new[] { 11, 12, 13 }) yield return MeasureSceneRun(m);
        }
        MeasureAugStage = 0;
        // 3) 합체 무기
        measureRepeatsOverride = Mathf.Max(1, Tuning.GetInt("overnight_fusion_repeats", 2));
        for(int s = 0; s <= 1; s++) {
            MeasureFusionStage = s;
            for(int m = 0; m <= 5; m++) yield return MeasureFusionSceneRun(m);
        }
        // 4) 종결
        measureRepeatsOverride = Mathf.Max(1, Tuning.GetInt("overnight_ult_repeats", 1));
        MeasureFusionStage = 2;
        for(int m = 0; m <= 5; m++) yield return MeasureFusionSceneRun(m);
        MeasureFusionStage = -1;
    }

    // 합체 무기 한 상황: 회차마다 배치를 새로 섞고 합체 무기 13개를 차례로
    System.Collections.IEnumerator MeasureFusionSceneRun(int mode)
    {
        int reps = MeasureRepeats, n = AugmentDefs.Fusions.Length;
        for(int r = 0; r < reps; r++) {
            string scene = null;
            if(!MeasureTry(() => scene = MeasureSetupScene(mode) + " +" + MeasureFusionNames[MeasureFusionStage], $"상황 차리기 {mode}")) continue;
            yield return null;
            for(int f = 0; f < n; f++)
                yield return MeasureFusionOne(mode, f, scene, $"회차 {r + 1}/{reps}", MeasureSpeed, $"{f + 1}/{n}");
        }
    }

    System.Collections.IEnumerator MeasureFusionOne(int mode, int f, string scene, string note, float speed, string progress)
    {
        string fname = AugmentDefs.Fusions[f].name;
        bool ok = MeasureTry(() => {
            if(IsMovingMode(mode)) {
                InfiniteStage.Instance.JumpToStage(1);
                if(EnemyManager.Instance != null) EnemyManager.Instance.removeAllEnemies();
            } else DebugArenaRespawnHome();   // 앞 무기가 옮기거나 상태를 건 적을 같은 자리에 새로
            DebugClearAugments();
            DebugSoloFusion(f);   // A 를 혼자 출격시키고 합체 (합체하면 A 전용 증강은 없어요)
            if(MeasureFusionStage >= 1)
                foreach(var id in AugmentDefs.Fusions[f].augs) { var d = AugmentDefs.Get(id); if(d != null) DebugAddAugment(d, true); }
            GameManager.instance.setTimeScale(speed);   // 스테이지를 다시 시작하면 저장된 속도로 돌아가서 그 뒤에 맞춰요
        }, $"합체 준비 {scene} {fname}");
        if(!ok) yield break;
        float warm = Tuning.Get("measure_warmup", 10f) + Random.Range(0f, Tuning.Get("measure_warmup_jitter", 5f));
        MeasureScene = scene;
        string name = AugmentDefs.Fusions[f].name;
        DevTools.MeasureBegin(name, $"{note} · x{speed:0.#}", MeasureIsolateSeconds(mode) + warm);
        ui.Toast($"[개발] 측정 [{scene}] {note} · {progress}: {name}", Color.white);
        if(MeasureFusionStage == 2) {   // 종결: 미리 돌린 뒤 한 번 쓰고 그때부터 재요
            yield return MeasureWarm(mode, warm);
            MeasureTry(() => DebugTriggerUltimate(f), $"종결 발동 {scene} {fname}");
            yield return new WaitForSeconds(Mathf.Max(3f, Tuning.Get("measure_ult_seconds", 12f)));
        } else {
            yield return MeasureWarm(mode, warm);
            yield return new WaitForSeconds(Mathf.Max(5f, Tuning.Get("measure_seconds", 20f)));
        }
        DevTools.MeasureEnd();
    }

    // ---- 증강 하나씩 (2026-11-22 사용자: 3상황 2회) — 버튼 mode 15
    //  기본 무기 10개 × 전용 증강마다, 그 증강 하나만 최대로 넣고 움직이는 적 · 멈춘 적 40 · 보스 1 에서 solo_aug_repeats(2)회 재요.
    //  증강 없는 값(밤새 측정)과 나누면 증강 하나가 몇 배를 올리는지 나와요. 속도는 overnight_speed (x1.5)
    //  기록 줄 이름: [상황 +단독 증강이름] 무기
    static AugmentDef measureSoloAug;
    System.Collections.IEnumerator MeasureSoloAugs()
    {
        measureSpeedOverride = Tuning.Get("overnight_speed", 1.5f);
        int reps = Mathf.Max(1, Tuning.GetInt("solo_aug_repeats", 2));
        var jobs = new List<(SideWeapon w, AugmentDef d)>();
        foreach(var w in AllSideWeapons()) foreach(var d in AugmentDefs.WeaponAugs(w)) jobs.Add((w, d));
        MeasureAugStage = 0;
        int total = 3 * reps * jobs.Count, done = 0;
        foreach(int mode in new[] { 0, 1, 2 }) {
            for(int r = 0; r < reps; r++) {
                string scene = null;
                measureSoloAug = null;
                if(!MeasureTry(() => scene = MeasureSetupScene(mode), $"상황 차리기 {mode}")) continue;
                yield return null;
                foreach(var job in jobs) {
                    measureSoloAug = job.d;
                    done++;
                    yield return MeasureWeapon(mode, job.w, scene + " +단독 " + job.d.name, $"회차 {r + 1}/{reps}", MeasureSpeed, $"{done}/{total}");
                }
            }
        }
        measureSoloAug = null;
    }

    // 한 상황: 회차마다 배치를 새로 섞고, 그 배치에서 무기를 차례로
    System.Collections.IEnumerator MeasureSceneRun(int mode)
    {
        int reps = MeasureRepeats;
        var list = AllSideWeapons();
        for(int r = 0; r < reps; r++) {
            string scene = null;
            if(!MeasureTry(() => scene = MeasureSetupScene(mode), $"상황 차리기 {mode}")) continue;
            yield return null;   // 적이 생기고 자리 잡게
            for(int i = 0; i < list.Count; i++)
                yield return MeasureWeapon(mode, list[i], scene, $"회차 {r + 1}/{reps}", MeasureSpeed, $"{i + 1}/{list.Count}");
        }
    }

    // 상황 차리기 (회차마다 새로). 돌려주는 값 = 기록 줄의 [ ] 안 이름
    string MeasureSetupScene(int mode)
    {
        var stage = InfiniteStage.Instance;
        string scene;
        MeasureFastEnemies = mode == 11;
        if(IsMovingMode(mode)) {
            if(DebugArena) DebugArenaStop();
            DebugTestMode = true;          // 끝없이 나오고 스테이지가 안 넘어가요
            DebugMortal = true;            // 그래도 적은 죽어요
            DebugManyEnemies = true;
            stage.JumpToStage(1);
            if(EnemyManager.Instance != null) EnemyManager.Instance.removeAllEnemies();
            scene = mode == 11 ? "빠른 적 (죽음)" : mode == 13 ? "시작 직후 (죽음)" : "움직이는 적 (죽음)";
        } else {
            DebugMortal = false;
            DebugArenaInvincible = true;
            int count = mode == 1 || mode == 3 ? 40 : mode == 4 ? Tuning.GetInt("measure_scatter_count", 7) : mode == 12 ? Tuning.GetInt("measure_minions", 20) : 0;
            DebugArenaStart(count);        // 자리를 새로 섞어요
            if(mode == 2 || mode == 12) DebugArenaBoss();
            if(mode == 3) {   // 단단한 적: 방어력 = 플레이어 공격력 × measure_armor (1이면 평균 무기 1발이 관통력 0일 때 절반만 들어가요)
                double armor = Player.instance != null ? Player.instance.FinalAttack_Damage * (double)Tuning.Get("measure_armor", 1f) : 0;
                foreach(var e in EnemyManager.Instance.enemyData) if(e != null) e.Armor = armor;
            }
            measureArenaArmor = mode == 3 && Player.instance != null ? Player.instance.FinalAttack_Damage * (double)Tuning.Get("measure_armor", 1f) : 0;
            if(mode == 5) {   // 우주선 근처: 우주선 둘레 measure_near_min ~ measure_near_max 칸에 고르게
                Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
                ship.z = 0f;
                int n = Tuning.GetInt("measure_near_count", 12);
                float r0 = Tuning.Get("measure_near_min", 1.8f), r1 = Tuning.Get("measure_near_max", 2.6f);
                for(int k = 0; k < n; k++) {
                    float ang = (360f * k / n + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                    SpawnArenaEnemy(ship + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * Random.Range(r0, r1));
                }
            }
            scene = mode == 1 ? "멈춘 적 40" : mode == 2 ? "보스 1" : mode == 3 ? "단단한 적 40" : mode == 4 ? $"흩어진 {count}" : mode == 12 ? $"보스 + 졸개 {count}" : "우주선 근처";
        }
        if(MeasureAugStage > 0) scene += " +" + MeasureAugNames[MeasureAugStage];
        return scene;
    }

    // 무기 하나 재기: (움직이는 적이면 적을 치우고 처음부터) 증강 넣기 → 혼자 출격 → 미리 돌리기 → 재기 → 한 줄 남기기
    System.Collections.IEnumerator MeasureWeapon(int mode, SideWeapon w, string scene, string note, float speed, string progress)
    {
        bool ok = MeasureTry(() => {
            if(IsMovingMode(mode)) {   // 무기가 바뀌면 적을 다 치우고 처음부터 다시 (모든 무기가 같은 조건)
                InfiniteStage.Instance.JumpToStage(1);
                if(EnemyManager.Instance != null) EnemyManager.Instance.removeAllEnemies();
            } else DebugArenaRespawnHome();   // 앞 무기가 옮기거나 상태를 건 적을 같은 자리에 새로
            // 이 무기 전용 증강만, 지금 단계만큼. "증강 없음"이면 앞 측정에서 남은 증강을 지워요
            //  (2026-11-22 점검 버그: 밤새 측정의 추가 상황은 "증강 최대" 다음에 "증강 없음"을 재서, 앞 무기 증강이 남아 있었어요)
            if(measureSoloAug != null) {   // 증강 하나씩: 그 증강 하나만 최대 레벨로
                DebugClearAugments();
                for(int guard = 0; guard < 20 && Level(measureSoloAug.id) < measureSoloAug.maxLevel; guard++) DebugAddAugment(measureSoloAug, false);
            }
            else if(MeasureAugStage > 0) MeasureGiveAugs(w);
            else if(levels.Count > 0) DebugClearAugments();
            DebugSolo(w);
            GameManager.instance.setTimeScale(speed);   // 스테이지를 다시 시작하면 저장된 속도로 돌아가서 그 뒤에 맞춰요
        }, $"무기 준비 {scene} {AugmentDefs.WeaponName(w)}");
        if(!ok) yield break;
        bool burst = mode == 13;   // 시작 직후: 미리 돌리지 않고 처음 몇 초만 (시작이 느린 무기의 단점)
        float warm = burst ? 0f : Tuning.Get("measure_warmup", 10f) + Random.Range(0f, Tuning.Get("measure_warmup_jitter", 5f));
        float len = burst ? Mathf.Max(3f, Tuning.Get("measure_burst_seconds", 10f)) : Mathf.Max(5f, Tuning.Get("measure_seconds", 20f));
        MeasureScene = scene;
        DevTools.MeasureBegin(AugmentDefs.WeaponName(w), $"{note} · x{speed:0.#}", MeasureIsolateSeconds(mode) + warm);
        ui.Toast($"[개발] 측정 [{scene}] {note} · {progress}: {AugmentDefs.WeaponName(w)}", Color.white);
        yield return MeasureWarm(mode, warm);   // 게임 시간
        yield return new WaitForSeconds(len);
        DevTools.MeasureEnd();
    }

    // 속도 비교: 멈춘 적 40 한 배치(끝까지 같은 자리)에서 무기마다 x1 → x5 를 번갈아 (증강 없이)
    System.Collections.IEnumerator MeasureSpeedCompare(float[] speeds)
    {
        MeasureAugStage = 0;
        if(!MeasureTry(() => MeasureSetupScene(1), "상황 차리기 (속도 비교)")) yield break;
        yield return null;
        var list = AllSideWeapons();
        for(int i = 0; i < list.Count; i++)
            foreach(float sp in speeds)
                yield return MeasureWeapon(1, list[i], $"속도 비교 x{sp:0.#}", "같은 배치", sp, $"{i + 1}/{list.Count}");
    }

    // 지금 단계에 맞게 이 무기 전용 증강을 넣어요 (다른 증강은 지워요)
    void MeasureGiveAugs(SideWeapon w)
    {
        DebugClearAugments();
        var defs = AugmentDefs.WeaponAugs(w);
        for(int k = 0; k < defs.Count; k++) {
            var d = defs[k];
            int want = MeasureAugStage switch {
                1 => k < 2 ? 1 : 0,
                2 => 1,
                3 => (d.maxLevel + 1) / 2,
                4 => d.maxLevel,
                _ => 0,
            };
            for(int guard = 0; guard < 20 && Level(d.id) < want; guard++) DebugAddAugment(d, false);
        }
    }

    // ---- 빈 칸만 다시 (2026-11-22 사용자: "오류로 비어 있는 측정값들만 다시 측정하자")
    //  dps_measure.txt 에서 기록은 있는데 무기 피해가 하나도 없는 줄(기본총만 있거나 아무것도 없음)을 찾아 그 칸을 빈 줄 수만큼 다시 재요.
    //  기본 무기·합체·종결 모두. 움직이는 적 계열은 빼요 (거기서 빈 줄은 오류가 아니라 진짜 결과: 예) 시작 직후 360 레이저)
    //  속도는 overnight_speed (밤새 측정과 같게). 다시 잰 줄은 "다시" 로 표시돼요
    System.Collections.IEnumerator MeasureMissing()
    {
#if UNITY_EDITOR
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Tuning", "dps_measure.txt"));
        if(!System.IO.File.Exists(path)) { ui.Toast("[개발] 측정 기록(dps_measure.txt)이 없어요", Color.white); yield break; }
        var empty = new Dictionary<string, int>();
        var order = new List<string>();
        var rx = new System.Text.RegularExpressions.Regex(@"\| \[([^\]]+)\] ([^|]+?) \| 적");
        var dps = new System.Text.RegularExpressions.Regex(@"\| ([^|]+?) DPS ");
        foreach(var line in System.IO.File.ReadAllLines(path)) {
            var m = rx.Match(line);
            if(!m.Success || m.Groups[1].Value.StartsWith("속도 비교")) continue;
            bool hit = false;
            foreach(System.Text.RegularExpressions.Match d in dps.Matches(line)) if(d.Groups[1].Value.Trim() != "기본총") { hit = true; break; }
            if(hit) continue;
            string key = m.Groups[1].Value + "|" + m.Groups[2].Value.Trim();
            if(!empty.ContainsKey(key)) { empty[key] = 0; order.Add(key); }
            empty[key]++;
        }
        var jobs = new List<(int mode, int aug, int fusion, int fstage, SideWeapon w, int need)>();
        int total = 0;
        foreach(var key in order) {
            int bar = key.LastIndexOf('|');
            string scene = key.Substring(0, bar), weapon = key.Substring(bar + 1);
            string baseScene = scene, suffix = "";
            int plus = scene.LastIndexOf(" +");
            if(plus >= 0) { baseScene = scene.Substring(0, plus); suffix = scene.Substring(plus + 2); }
            int mode = baseScene.StartsWith("움직이는") ? 0 : baseScene.StartsWith("빠른") ? 11 : baseScene.StartsWith("시작") ? 13
                     : baseScene.StartsWith("보스 + 졸개") ? 12 : baseScene.StartsWith("멈춘") ? 1 : baseScene.StartsWith("보스") ? 2
                     : baseScene.StartsWith("단단한") ? 3 : baseScene.StartsWith("흩어진") ? 4 : 5;
            if(IsMovingMode(mode)) continue;
            int fstage = System.Array.IndexOf(MeasureFusionNames, suffix);
            if(fstage >= 0) {
                int f = -1;
                for(int i = 0; i < AugmentDefs.Fusions.Length; i++) if(AugmentDefs.Fusions[i].name == weapon) f = i;
                if(f < 0) continue;
                jobs.Add((mode, 0, f, fstage, SideWeapon.None, empty[key]));
            } else {
                int aug = suffix == "" ? 0 : System.Array.IndexOf(MeasureAugNames, suffix);
                if(aug < 0) continue;
                SideWeapon sw = SideWeapon.None;
                foreach(var w in AllSideWeapons()) if(AugmentDefs.WeaponName(w) == weapon) sw = w;
                if(sw == SideWeapon.None) continue;
                jobs.Add((mode, aug, -1, -1, sw, empty[key]));
            }
            total += empty[key];
        }
        if(jobs.Count == 0) { ui.Toast("[개발] 빈 칸이 없어요", Color.white); yield break; }
        ui.Toast($"[개발] 빈 칸 다시 측정: {total}번", Color.white);
        measureSpeedOverride = Tuning.Get("overnight_speed", 1.5f);
        int done = 0;
        foreach(var job in jobs) {
            MeasureAugStage = job.aug;
            MeasureFusionStage = job.fstage;
            for(int k = 0; k < job.need; k++) {
                string scene = null;
                if(!MeasureTry(() => scene = MeasureSetupScene(job.mode), $"상황 차리기 {job.mode}")) continue;
                yield return null;
                done++;
                if(job.fusion >= 0)
                    yield return MeasureFusionOne(job.mode, job.fusion, scene + " +" + MeasureFusionNames[job.fstage], "다시", MeasureSpeed, $"{done}/{total}");
                else
                    yield return MeasureWeapon(job.mode, job.w, scene, "다시", MeasureSpeed, $"{done}/{total}");
            }
        }
        MeasureFusionStage = -1;
#else
        yield break;
#endif
    }

    // 무기 하나만 출격 (합체돼 있으면 풀어서 기본 무기로)
    public void DebugSolo(SideWeapon w)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStartPick();
        debugPicked.Clear();
        DebugEnsureOwned(w);
        DebugUnfuseAllExcept(-1);   // 다른 합체가 남아 있으면 B 자리 교체로 엉뚱한 무기가 나와요 (2026-11-20: 사냥개 탄인데 폭격기)
        debugPicked.Add(w);
        ui.RefreshHUD();
#endif
    }

    // 합체 무기 하나만 출격
    public void DebugSoloFusion(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var fd = AugmentDefs.Fusions[f];
        DebugStartPick();
        debugPicked.Clear();
        DebugEnsureOwned(fd.a);
        debugPicked.Add(fd.a);
        DebugUnfuseAllExcept(f);
        fusionSlots.Clear();   // 예전에 흡수된 B 칸에 들어온 무기까지 같이 나오지 않게
        if(!FusionOwned(f)) Apply(AugmentDefs.Get(fd.card), true);
        ui.RefreshHUD();
#endif
    }

    // 연습장: 고른 합체(keep) 말고 다른 합체를 모두 풀어요
    void DebugUnfuseAllExcept(int keep)
    {
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++)
            if(i != keep && FusionOwned(i)) DebugUnfuse(i);
    }

    // 연습장에서 지금 혼자 출격 중인 무기 (없으면 None)
    public SideWeapon DebugSoloWeapon()
    {
        if(!debugPickMode || debugPicked.Count != 1) return SideWeapon.None;
        foreach(var w in debugPicked) return w;
        return SideWeapon.None;
    }

    public void DebugPickClear(bool backToLoadout)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugPicked.Clear();
        if(backToLoadout) debugPickMode = false;
        ui.RefreshHUD();
#endif
    }

    public void DebugFuse(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(f < 0 || f >= AugmentDefs.Fusions.Length) return;
        var fd = AugmentDefs.Fusions[f];
        if(FusionOf(fd.a) >= 0) { ui.Toast($"[개발] {AugmentDefs.WeaponName(fd.a)}는 이미 합체했어요", Color.white); return; }
        DebugEnsureOwned(fd.a);   // B 로 합체할 때 A 무기가 있어야 해요
        Apply(AugmentDefs.Get(fd.card), true);
#endif
    }
}

// 궤도 위성 (위성 추가 증강, 독안개 위성). 본체 궤도를 복제해서 만들고, 피해는 본체 궤도 값으로 줘요
public class OrbitalSatellite : MonoBehaviour
{
    // 표시용이에요. 위치는 MoveInCircle, 피해는 본체 Orbital 이 위성 자리마다 판정해요 (Orbital.Update)
}

// 위성 레이저 드론 (2026-11-20 바꿈): 기본 드론과 다른 "위성" 그림. 가장 가까운 적에게 이어지는 레이저를 쏘고,
// 같은 적을 오래 비출수록 피해가 점점 세져요 (빔도 굵어지고 하얗게 달아올라요).
// 목표가 죽거나 화면 밖으로 나가면 가까운 적으로 옮기고, 쌓인 세기는 일부만 남아요.
// 숫자: tuning.txt satlaser_* (예전 "0.4초마다 한 방" 방식은 기본 드론과 따로 노는 느낌이라 바꿨어요)
public class LaserDrone : MonoBehaviour
{
    public int index, count = 1;
    float tickTimer, heat;   // heat: 0~1, 같은 적을 비춘 정도
    Enemy target;
    SpriteRenderer sr;
    LineRenderer beam, core;
    static readonly Color Cool = new Color(0.35f, 0.9f, 1f), Hot = new Color(1f, 0.97f, 0.9f);

    public static LaserDrone Create(Transform parent)
    {
        var go = new GameObject("LaserDrone");
        go.transform.SetParent(parent, false);
        var d = go.AddComponent<LaserDrone>();
        d.sr = go.AddComponent<SpriteRenderer>();
        d.sr.sprite = SatArt.Sprite;
        d.sr.sortingLayerName = "Weapons";   // Default 레이어는 배경(Map)보다 아래라 가려졌어요
        d.sr.sortingOrder = 50;
        d.beam = MakeLine(go.transform, "Beam", 40);
        d.core = MakeLine(go.transform, "Core", 41);
        return d;
    }

    static LineRenderer MakeLine(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingLayerName = RemasterFx.FxLayer;
        lr.sortingOrder = order;
        lr.numCapVertices = 4;
        lr.enabled = false;
        return lr;
    }

    void OnDisable()
    {
        target = null;
        heat = 0f;
        if(beam != null) beam.enabled = false;
        if(core != null) core.enabled = false;
    }

    void Update()
    {
        var lz = Lazer.instance;
        var wm = WeaponManager.instance;
        if(lz == null || wm == null || wm.mainWeapon == null) return;
        // 돌지 않고 우주선 날개에 붙어 있어요 (2026-11-20 사용자 요청): 0번 왼쪽, 1번 오른쪽, 위성 추가로 늘면 바깥·아래로 한 칸씩
        Vector3 ship = wm.mainWeapon.transform.position;
        int side = index % 2 == 0 ? -1 : 1, row = index / 2;
        float wx = Tuning.Get("satlaser_wing_x", 0.55f) + row * Tuning.Get("satlaser_wing_step", 0.3f);
        float wy = Tuning.Get("satlaser_wing_y", -0.1f) - row * 0.15f;
        transform.position = ship + new Vector3(side * wx, wy, 0f);
        float sc = Tuning.Get("satlaser_sat_scale", 0.7f);
        transform.localScale = new Vector3(sc, sc, 1f);

        bool shooting = wm.shootType != WeaponManager.ShootType.DontShoot;
        bool focus = AugmentManager.WL("sat_focus") > 0;
        if(!shooting) { SetBeam(false); heat = 0f; target = null; return; }

        // 목표 유지: 살아 있고 화면 안이면 계속 비춰요
        if(target == null || target._state == Enemy.EnemyState.dead || !target.gameObject.activeInHierarchy || !target.IsInRange) {
            Enemy next = focus ? AugmentManager.StrongestInRange() : ClosestTo(transform.position);
            // 목표가 바뀌면 처음부터 (가늘게, 1배) 다시 쌓아요 (2026-11-20 사용자 결정)
            if(next != target) heat *= Tuning.Get("satlaser_heat_keep", 0f);
            target = next;
        }
        if(target == null) { SetBeam(false); heat = Mathf.Max(0f, heat - Time.deltaTime / Mathf.Max(0.1f, Tuning.Get("satlaser_cool_time", 3f))); return; }   // 적이 없으면 천천히 식어요

        float rampTime = Mathf.Max(0.1f, Tuning.Get("satlaser_ramp_time", 6f));
        heat = Mathf.Min(1f, heat + Time.deltaTime / rampTime);
        float power = Mathf.Lerp(1f, Tuning.Get("satlaser_ramp_max", 4f), heat);

        Vector3 to = target.transform.position - transform.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f);   // 비추는 적 쪽을 봐요
        DrawBeam(target.transform.position);

        tickTimer -= Time.deltaTime;
        if(tickTimer > 0f) return;
        tickTimer = Mathf.Max(0.05f, Tuning.Get("satlaser_tick", 0.1f));
        // 1틱 피해 = 360 레이저 1발 × satlaser_tick_mul × 세기(1 → ramp_max) (× 궤도 일제 사격)
        double mul = Tuning.Get("satlaser_tick_mul", 0.0072f) * power * (focus ? AugmentDefs.SatFocus(AugmentManager.WL("sat_focus")) : 1f);
        AugmentManager.WeaponHit(target, WeaponBase.ClampDamage(lz.HitDamage * mul), Random.Range(1, 101) <= Player.instance.FinalCriticalChance + lz.ExtraCritChance,
                                 Player.instance.FinalCriticalDamage + lz.ExtraCritDamage, HitKind.Laser, Armor.Pen(TraitsData.WeaponType.CircleLaser));
        AugmentManager.WeaponHitEvent(SideWeapon.Lazer);
    }

    void DrawBeam(Vector3 to)
    {
        SetBeam(true);
        Vector3 from = transform.position + transform.up * 0.12f;   // 렌즈 자리
        float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 40f + index);
        Color c = Color.Lerp(Cool, Hot, heat);
        // 처음엔 아주 가늘게 → 달아오를수록 굵게 (2026-11-20 사용자 요청)
        float w = Mathf.Lerp(Tuning.Get("satlaser_width_min", 0.012f), Tuning.Get("satlaser_width_max", 0.3f), heat * heat) * flicker;
        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        beam.startWidth = w * 0.7f;
        beam.endWidth = w;
        beam.startColor = new Color(c.r, c.g, c.b, 0.45f);
        beam.endColor = new Color(c.r, c.g, c.b, 0.6f);
        core.SetPosition(0, from);
        core.SetPosition(1, to);
        core.startWidth = w * 0.25f;
        core.endWidth = w * 0.35f;
        core.startColor = core.endColor = Color.Lerp(Color.white, Hot, heat);
        sr.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.8f), heat);   // 달아오르면 몸체도 살짝 따뜻하게
    }

    void SetBeam(bool on)
    {
        if(beam.enabled != on) beam.enabled = on;
        if(core.enabled != on) core.enabled = on;
        if(!on && sr != null) sr.color = Color.white;
    }

    static Enemy ClosestTo(Vector3 p)
    {
        var list = AugmentManager.NearestInRange(p, 1);
        return list.Count > 0 ? list[0] : null;
    }
}

// 위성 그림 (코드로 그려요): 가운데 몸체 + 양옆 태양전지판 + 위쪽 렌즈. 기본 드론(쿼드콥터)과 다르게 보이게
public static class SatArt
{
    static Sprite sprite;
    public static Sprite Sprite => sprite != null ? sprite : (sprite = Make());

    static Sprite Make()
    {
        const int S = 72;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        Color panel = new Color(0.16f, 0.32f, 0.72f), grid = new Color(0.45f, 0.65f, 1f), frame = new Color(0.75f, 0.8f, 0.88f);
        Color body = new Color(0.86f, 0.89f, 0.93f), bodyEdge = new Color(0.45f, 0.5f, 0.58f), lens = new Color(0.45f, 1f, 1f);
        void Set(int x, int y, Color c) { if(x >= 0 && y >= 0 && x < S && y < S) px[y * S + x] = c; }
        void Rect(int x0, int y0, int x1, int y1, Color c) { for(int y = y0; y <= y1; y++) for(int x = x0; x <= x1; x++) Set(x, y, c); }
        void Disc(float cx, float cy, float r, Color c) {
            for(int y = 0; y < S; y++) for(int x = 0; x < S; x++) {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                if(d <= r + 0.5f) { var o = px[y * S + x]; float k = Mathf.Clamp01(r - d + 0.5f); px[y * S + x] = Color.Lerp(o, c, k); }
            }
        }
        // 태양전지판 (양옆), 격자 무늬
        foreach(int side in new[] { -1, 1 }) {
            int x0 = side < 0 ? 2 : 47, x1 = side < 0 ? 24 : 69;
            Rect(x0, 27, x1, 44, frame);
            Rect(x0 + 1, 28, x1 - 1, 43, panel);
            for(int x = x0 + 1; x < x1; x++) if((x - x0) % 5 == 0) Rect(x, 28, x, 43, grid);
            Rect(x0 + 1, 35, x1 - 1, 36, grid);
        }
        Rect(24, 34, 47, 37, bodyEdge);   // 받침대
        // 몸체
        Disc(36, 35, 11f, bodyEdge);
        Disc(36, 35, 9.5f, body);
        Disc(33, 38, 3f, Color.white);   // 반짝
        // 렌즈 (위쪽 = 쏘는 방향)
        Rect(34, 44, 38, 50, bodyEdge);
        Disc(36, 53, 5f, bodyEdge);
        Disc(36, 53, 3.6f, lens);
        Disc(35, 54, 1.4f, Color.white);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 128f);
    }
}

// 레이저 빔 꾸미기 (굵은 광선, 넓은 빔, 2중 회전).
// 본체 빔(LaserBim)과 판정(본체 BoxCollider2D)의 폭을 바꾸고, 추가 갈래는 본체 아래 자식으로 복제해요.
// 자식 판정은 본체 Rigidbody2D 에 붙어서 충돌이 본체 레이저 스크립트로 전달돼요.
public class BeamRig
{
    readonly Transform root, mainBeam;
    readonly BoxCollider2D box;
    readonly SpriteRenderer mainSprite;
    readonly Vector2 boxSize0;
    readonly Vector3 beamScale0;
    readonly Color color0;
    readonly List<GameObject> extras = new List<GameObject>();

    public BeamRig(Transform root, ShootLazer beam)
    {
        this.root = root;
        box = root.GetComponent<BoxCollider2D>();
        if(box != null) boxSize0 = box.size;
        if(beam != null) {
            mainBeam = beam.transform;
            beamScale0 = mainBeam.localScale;
            mainSprite = beam.lazerRenderer;
            if(mainSprite != null) color0 = mainSprite.color;
        }
    }

    // beams: 전체 갈래 수 (1 = 본체만), widthMul: 폭 배율
    public void Apply(bool shooting, int beams, float widthMul)
    {
        if(box != null) box.size = new Vector2(boxSize0.x * widthMul, boxSize0.y);
        if(mainBeam != null) mainBeam.localScale = new Vector3(beamScale0.x * widthMul, beamScale0.y, beamScale0.z);
        int need = Mathf.Max(0, beams - 1);
        while(extras.Count < need && mainBeam != null) extras.Add(MakeExtra());
        if(mainSprite != null) { mainSprite.color = color0; mainSprite.enabled = true; }
        for(int i = 0; i < extras.Count; i++) {
            var go = extras[i];
            bool on = shooting && i < need;
            if(go.activeSelf != on) go.SetActive(on);
            if(!on) continue;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 360f * (i + 1) / beams);
            var col = go.GetComponent<BoxCollider2D>();
            if(col != null && box != null) { col.size = box.size; col.offset = box.offset; }
            if(go.transform.childCount > 0) {
                var b = go.transform.GetChild(0);
                b.localScale = mainBeam.localScale;
                var sr = b.GetComponent<SpriteRenderer>();
                if(sr != null) { sr.enabled = true; sr.color = color0; }
            }
        }
    }

    public void Hide()
    {
        foreach(var go in extras) if(go != null && go.activeSelf) go.SetActive(false);
    }

    // 켜진 빔(본체 + 추가 갈래)에 몸이 닿은 적으로 무기의 대상 목록을 채워요
    readonly List<BoxCollider2D> extraBoxes = new List<BoxCollider2D>();
    public void CollectTargets(ContactWeapon w)
    {
        extraBoxes.Clear();
        foreach(var go in extras)
            if(go != null && go.activeSelf && go.TryGetComponent(out BoxCollider2D c)) extraBoxes.Add(c);
        w.CollectInBoxes(box, extraBoxes);
    }

    GameObject MakeExtra()
    {
        var go = new GameObject("ExtraBeam");
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        var col = go.AddComponent<BoxCollider2D>();
        if(box != null) { col.size = box.size; col.offset = box.offset; col.sharedMaterial = box.sharedMaterial; }
        var clone = Object.Instantiate(mainBeam.gameObject, go.transform);
        clone.transform.localPosition = mainBeam.localPosition;
        clone.transform.localRotation = Quaternion.identity;
        var sl = clone.GetComponent<ShootLazer>();
        if(sl != null) Object.Destroy(sl);
        go.SetActive(false);
        return go;
    }

}
