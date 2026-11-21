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
    // B 로 합체하려면 A 무기를 가지고(상점에서 열어) 있어야 해요. 레일건·프리즘 빔처럼 짝이 없으면 A 로만.

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
    // 켜는 동안 새 적은 안 나오고(InfiniteStage), 테스트 모드라 적이 죽지 않아요(체력이 다시 차요). 같은 개수면 늘 같은 자리
    public static bool DebugArena;

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
        var cam = GameManager.instance.mainCamera;
        float w = cam.orthographicSize * cam.aspect, h = cam.orthographicSize;
        Vector3 c = cam.transform.position;
        c.z = 0f;
        Vector3 ship = WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : c;
        ship.z = 0f;
        // 화면을 7 x 13 칸으로 나눠 우주선 둘레(1.6칸)를 뺀 자리 중에서 고르게 (섞는 순서 고정)
        var spots = new List<Vector3>();
        for(int gy = 0; gy < 13; gy++) for(int gx = 0; gx < 7; gx++) {
            var p = new Vector3(c.x + Mathf.Lerp(-w + 0.5f, w - 0.5f, gx / 6f), c.y + Mathf.Lerp(-h + 0.8f, h - 0.8f, gy / 12f), 0f);
            if((p - ship).magnitude >= 1.6f) spots.Add(p);
        }
        var rng = new System.Random(1);
        for(int i = spots.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
        var prefab = stage.enemyPrefab[Mathf.Clamp(stage.CurrentEnemyIndex.Count > 0 ? stage.CurrentEnemyIndex[0] : 0, 0, stage.enemyPrefab.Length - 1)];
        int n = Mathf.Min(count, spots.Count);
        for(int i = 0; i < n; i++) {
            em.createInfiniteEnemy(prefab, stage.EnemyScale);
            var e = em.enemyData[em.enemyData.Count - 1];
            e.transform.position = spots[i];
            e.DebugFrozen = true;
        }
        ui.Toast($"[개발] 연습장: 멈춘 적 {n}마리", Color.white);
#endif
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
#endif
    }

    public void DebugArenaStop()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugArena = false;
        var em = EnemyManager.Instance;
        if(em != null) {
            foreach(var e in em.enemyData) if(e != null) e.DebugFrozen = false;
            foreach(var e in em.bossData) if(e != null) e.DebugFrozen = false;
        }
        ui.Toast("[개발] 연습장 끔 (적이 다시 움직이고 새로 나와요)", Color.white);
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

// 레이저 빔 꾸미기 (굵은 광선, 넓은 빔, 2중 회전, 프리즘 빔).
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

    // beams: 전체 갈래 수 (1 = 본체만), widthMul: 폭 배율, rainbow: 프리즘 빔 색
    public void Apply(bool shooting, int beams, float widthMul, bool rainbow)
    {
        if(box != null) box.size = new Vector2(boxSize0.x * widthMul, boxSize0.y);
        if(mainBeam != null) mainBeam.localScale = new Vector3(beamScale0.x * widthMul, beamScale0.y, beamScale0.z);
        int need = Mathf.Max(0, beams - 1);
        while(extras.Count < need && mainBeam != null) extras.Add(MakeExtra());
        float t = Time.time * 0.6f;
        if(mainSprite != null) mainSprite.color = rainbow ? Rainbow(t) : color0;
        if(mainSprite != null) mainSprite.enabled = !rainbow;   // 프리즘 빔은 아래 PrismBeams 가 그려요
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
                if(sr != null) { sr.enabled = !rainbow; sr.color = rainbow ? Rainbow(t + (i + 1f) / beams) : color0; }
            }
        }
        PrismBeams(shooting && rainbow, beams, widthMul, t);
    }

    public void Hide()
    {
        foreach(var go in extras) if(go != null && go.activeSelf) go.SetActive(false);
        PrismBeams(false, 0, 1f, 0f);
    }

    // ---- 프리즘 빔 연출 (2026-11-21 사용자 요청: "빔처럼 나오는 임팩트")
    // 쏘기 시작하면 우주선에서 번쩍 + 무지개 고리, 빔이 0.18초 동안 화면 끝까지 쭉 뻗어 나가요.
    // 빔 = 넓은 무지개 빛무리 + 하얀 심, 살짝 일렁이고, 끝에서 무지개 불꽃이 튀어요. 판정(충돌 상자)은 그대로예요
    static Material prismMat;
    readonly List<LineRenderer> prismLines = new List<LineRenderer>();   // [빛무리, 심] 짝
    bool prismOn;
    float prismStart, prismSparkAt;

    void PrismBeams(bool on, int beams, float widthMul, float t)
    {
        if(!on) {
            if(prismOn) foreach(var lr in prismLines) if(lr != null) lr.enabled = false;
            prismOn = false;
            return;
        }
        if(!prismOn) {   // 막 쏘기 시작
            prismOn = true;
            prismStart = Time.time;
            Vector3 c = root.position;
            RemasterFx.Burst(c, Tuning.Get("prism_flash", 0.7f), Color.white);
            RemasterFx.Ring(c, Tuning.Get("prism_flash", 0.7f) * 1.8f, Rainbow(t), 0.3f);
        }
        while(prismLines.Count < beams * 2) prismLines.Add(MakePrismLine(prismLines.Count % 2 == 1));
        float grow = Mathf.Clamp01((Time.time - prismStart) / Mathf.Max(0.01f, Tuning.Get("prism_shoot_time", 0.18f)));
        grow = 1f - (1f - grow) * (1f - grow);
        float from = 0.3f, to = box != null ? box.offset.y + box.size.y / 2f : 12.7f;
        float flick = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
        float glowW = Tuning.Get("prism_glow_width", 0.5f) * widthMul * flick, coreW = Tuning.Get("prism_core_width", 0.14f) * widthMul * flick;
        bool spark = Time.time >= prismSparkAt;
        if(spark) prismSparkAt = Time.time + 0.1f;
        for(int i = 0; i < prismLines.Count / 2; i++) {
            var glow = prismLines[i * 2];
            var core = prismLines[i * 2 + 1];
            bool use = i < beams;
            glow.enabled = core.enabled = use;
            if(!use) continue;
            Vector3 dir = root.rotation * Quaternion.Euler(0f, 0f, 360f * i / beams) * Vector3.up;
            Vector3 a = root.position + dir * from, b = root.position + dir * Mathf.Lerp(from, to, grow);
            a.z = b.z = 0f;
            var col = Rainbow(t + (float)i / beams);
            glow.SetPosition(0, a); glow.SetPosition(1, b);
            core.SetPosition(0, a); core.SetPosition(1, b);
            glow.startWidth = glowW * 0.6f; glow.endWidth = glowW;
            core.startWidth = coreW * 0.7f; core.endWidth = coreW;
            glow.startColor = new Color(col.r, col.g, col.b, 0.75f);
            glow.endColor = new Color(col.r, col.g, col.b, 0.35f);
            if(spark && grow >= 1f) RemasterFx.Burst(b, 0.22f * widthMul, col);   // 빔 끝 불꽃 (화면 밖이면 안 보여요)
        }
    }

    LineRenderer MakePrismLine(bool core)
    {
        var go = new GameObject(core ? "PrismCore" : "PrismGlow");
        go.transform.SetParent(root, false);
        var lr = go.AddComponent<LineRenderer>();
        if(prismMat == null) prismMat = new Material(Shader.Find("Sprites/Default"));
        lr.sharedMaterial = prismMat;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 4;
        lr.sortingLayerName = RemasterFx.FxLayer;
        lr.sortingOrder = core ? 481 : 480;
        if(core) lr.startColor = lr.endColor = new Color(1f, 1f, 1f, 0.95f);
        lr.enabled = false;
        return lr;
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

    static Color Rainbow(float t) => Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.55f, 1f);
}
