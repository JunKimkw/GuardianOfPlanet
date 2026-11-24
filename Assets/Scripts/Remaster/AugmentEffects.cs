using System.Collections.Generic;
using UnityEngine;

// 지금 처리 중인 피해의 출처. 레이저·독 지대가 피해를 줄 때 잠깐 바꿔요.
public enum HitKind { Weapon, Laser, Poison, PoisonArea }

// 증강 전투 효과: 9계열 × 8개 + 세트(2/4/6/8) + 조합 계열 9개 (증강_목록_초안.md)
// 뽑기·저장·환생은 AugmentManager.cs, 여기는 싸움에서 일어나는 일만 있어요.
public partial class AugmentManager
{
    public static HitKind CurrentHitKind = HitKind.Weapon;
    static bool IsPoisonHit => CurrentHitKind == HitKind.Poison || CurrentHitKind == HitKind.PoisonArea;

    #region 상태
    // 폭발
    struct PendingExplosion { public Vector3 pos; public int depth; public long damage; public float radius; public float time; public bool mini; public SideWeapon src; }   // src: 합체 보스 배율을 받는 무기 (2026-11-25)
    readonly List<PendingExplosion> explosions = new List<PendingExplosion>();
    struct FireZone { public Vector3 pos; public float radius; public long damage; public int ticks; public float next; }
    readonly List<FireZone> fires = new List<FireZone>();
    class BlackHole { public Vector3 pos; public float radius, left, tick; public bool big; }
    readonly List<BlackHole> blackHoles = new List<BlackHole>();

    int effectDepth;            // 폭발·번개 등 효과로 준 피해 처리 중이면 >0 (연쇄 단수)
    int splitGen;               // 파편 세대 (0 = 파편 아님)
    float hammerReadyTime;

    // 원정 동안 쌓이는 값 (저장): 용광로, 변이, 맹세, 치명 횟수, 번개 횟수, 빙결 횟수, 표식 카운터, 1회용 사용 수
    readonly Dictionary<string, float> stacks = new Dictionary<string, float>();
    float Stack(string k) => stacks.TryGetValue(k, out var v) ? v : 0f;
    void AddStack(string k, float v) => stacks[k] = Stack(k) + v;

    // 저장하지 않는 짧은 상태
    int critStreak;
    bool lastHitCrit;
    int aegisLeft;
    bool shieldLostThisStage;
    bool invulnUsedThisStage;
    float invulnLeft, revengeLeft;
    float stormTimer, horizonTimer, northwindTimer, hasteClock, nemesisCheck, nemesisAt = -1f, nemesisCooldown;
    float lightningBudget = 40f;
    int enemyCount;
    float enemyCountTimer;

    void ResetEffectState()
    {
        explosions.Clear();
        fires.Clear();
        blackHoles.Clear();
        stacks.Clear();
        critStreak = 0;
        aegisLeft = 0;
        shieldLostThisStage = invulnUsedThisStage = false;
        invulnLeft = revengeLeft = 0;
        stormTimer = horizonTimer = northwindTimer = hasteClock = nemesisCheck = nemesisCooldown = 0;
        nemesisAt = -1f;
        ResetUltimates();
    }

    void LoadStacks(GameData data)
    {
        stacks.Clear();
        if(data.expStackKeys == null || data.expStackVals == null) return;
        for(int i = 0; i < data.expStackKeys.Count && i < data.expStackVals.Count; i++)
            stacks[data.expStackKeys[i]] = data.expStackVals[i];
    }

    void SaveStacks(GameData data)
    {
        data.expStackKeys = new List<string>(stacks.Keys);
        data.expStackVals = new List<float>();
        foreach(var k in data.expStackKeys) data.expStackVals.Add(stacks[k]);
    }

    // 계열 개수 (증강이 바뀔 때만 다시 세요)
    readonly int[] tagCounts = new int[16];
    void OnAugmentsChanged()
    {
        System.Array.Clear(tagCounts, 0, tagCounts.Length);
        foreach(var id in order) {
            var d = AugmentDefs.Get(id);
            if(d != null) tagCounts[(int)d.tag]++;
        }
    }

    public int TagCount(AugTag tag) => tagCounts[(int)tag];
    public bool Synergy(AugTag tag, int tier) => TagCount(tag) >= tier;
    public int SynergyTier(AugTag t)
    {
        if(!AugmentDefs.IsMain(t)) return 0;
        int c = TagCount(t);
        return c >= 8 ? 8 : c >= 6 ? 6 : c >= 4 ? 4 : c >= 2 ? 2 : 0;
    }

    // 조합 계열 단계: 두 계열 모두 2개 이상 = 1, 모두 4개 이상 = 2
    public int ComboTier(int i)
    {
        var c = AugmentDefs.Combos[i];
        int a = TagCount(c.a), b = TagCount(c.b);
        return a >= 4 && b >= 4 ? 2 : a >= 2 && b >= 2 ? 1 : 0;
    }

    int L(string id) => Level(id);

    // EffectsActive 를 적마다 매 프레임 계산하지 않게 프레임 단위로 기억
    int activeFrame = -1;
    bool activeCached;
    bool Active {
        get {
            if(activeFrame != Time.frameCount) { activeFrame = Time.frameCount; activeCached = EffectsActive; }
            return activeCached;
        }
    }
    #endregion

    #region 능력치 (플레이어·무기·적이 읽어요)
    int Shield => InfiniteStage.Instance != null ? InfiniteStage.Instance.playerHealth : 0;
    float PlayerAttack => Player.instance != null ? Player.instance.FinalAttack_Damage : 10f;

    float CurseGain => Synergy(AugTag.Curse, 2) ? 1.2f : 1f;

    public float CurseStackTotal {
        get {
            // 처치 수가 많아질수록 덜 쌓이고(EffectiveKills), 합계는 +300%가 상한 (세트 8이면 상한 없음)
            float k = RemasterBalance.EffectiveKills(expKills) * CurseGain;
            float s = 0f;
            if(L("soul") > 0) s += AugmentDefs.SoulPerKill(L("soul")) * k;
            if(L("slaughter") > 0) s += AugmentDefs.SlaughterPerKill(L("slaughter")) * k;
            if(L("wraith") > 0) s += AugmentDefs.WraithPerBoss(L("wraith")) * expBossKills * CurseGain;
            if(!Synergy(AugTag.Curse, 8)) s = Mathf.Min(s, RemasterBalance.CurseCap);
            if(Synergy(AugTag.Curse, 6)) s *= 1.5f;
            if(L("abyss") > 0) s *= 1f + AugmentDefs.AbyssPerCurse(L("abyss")) * TagCount(AugTag.Curse);
            int tl = L("tartarus");
            if(tl > 0 && Shield == 1) s *= AugmentDefs.TartarusMult(tl);
            return s;
        }
    }

    // 플레이어 공격력에 곱해지는 배율 (Player.calculateValues)
    public float DamageMultiplier {
        get {
            if(!Active) return 1f;
            float m = (1f + (L("bloodprice") > 0 ? AugmentDefs.BloodDamage(L("bloodprice")) : 0f)) * (1f + CurseStackTotal) * StarPowerMultiplier;
            int sh = Shield;
            float perShield = (L("polish") > 0 ? AugmentDefs.PolishPer(L("polish")) : 0f) + (Synergy(AugTag.Guard, 6) ? 0.04f : 0f);
            if(perShield > 0) m *= 1f + perShield * Mathf.Max(0, sh);
            if(L("unyield") > 0 && sh == 1) m *= 1f + AugmentDefs.UnyieldBonus(L("unyield"));
            if(L("oath") > 0) m *= 1f + Mathf.Min(AugmentDefs.OathCap, Stack("oath"));
            if(L("mass") > 0) m *= 1f + AugmentDefs.MassPer(L("mass")) * Mathf.Min(30, enemyCount);
            return m;
        }
    }

    public float BulletScaleMultiplier {
        get {
            if(!Active) return 1f;
            float s = 1f + stardustSizeStack;
            if(L("bigshot") > 0) s += AugmentDefs.BigShotSize(L("bigshot"));
            if(Synergy(AugTag.Split, 2)) s += 0.1f;
            return Mathf.Min(s, 3f);
        }
    }

    // 생성할 때 한 번 곱하는 적 속도 (학살자의 저주)
    public float EnemySpeedMultiplier {
        get {
            if(!Active || L("slaughter") <= 0) return 1f;
            float per = Synergy(AugTag.Curse, 4) ? 0.005f : 0.01f;
            return 1f + Mathf.Min(0.5f, (expKills / 1000) * per);
        }
    }

    // 매 프레임 적 이동에 곱하는 배율 (둔화·중력장·시간 지연)
    public float MoveFactor(Enemy e)
    {
        if(!Active) return 1f;
        float f = 1f;
        if(L("chains") > 0) f *= 1f - AugmentDefs.ChainsSlow(L("chains"));
        if(Synergy(AugTag.Frost, 2)) f *= 0.9f;
        if(e.SlowLeft > 0) f *= 1f - e.SlowAmount;
        if(e.IsBoss) {
            if(L("timeslow") > 0) f *= 1f - AugmentDefs.TimeSlow(L("timeslow"));
            if(Synergy(AugTag.Gravity, 4)) f *= 0.85f;
        } else if(L("field") > 0 && e.distance < 3f) {
            f *= 1f - AugmentDefs.FieldSlow(L("field"));
        }
        int sleep = ComboTier(AugmentDefs.ComboDeepSleep);
        if(sleep > 0 && e.IsPoisoned) f *= sleep == 2 ? 0.8f : 0.9f;
        return f;
    }

    public int ShieldBonus {
        get {
            if(!loaded) return 0;
            int b = L("spareshield") + T(5) + L("armor");
            if(L("bloodprice") > 0) b -= 1;
            if(Synergy(AugTag.Guard, 2)) b += 1;
            return b;
        }
    }

    // 장전 시간을 나누는 값 (클수록 빨리 쏴요)
    public static float AttackSpeedFactor {
        get {
            var m = Instance;
            if(m == null || !m.Active) return 1f;
            float f = 1f;
            if(m.L("chains") > 0) f *= m.Synergy(AugTag.Curse, 4) ? 0.975f : 0.95f;
            if(m.L("armor") > 0) f *= 1f - 0.03f * m.L("armor");
            if(m.L("haste") > 0 && m.hasteClock % 10f < AugmentDefs.HasteSeconds(m.L("haste"))) f *= 1.5f;
            return Mathf.Max(0.3f, f);
        }
    }

    // 한 번 쏠 때 더 나가는 탄 수 (다중 사격, 쌍둥이탄, 증식 세트 6)
    public static int ExtraShots()
    {
        var m = Instance;
        if(m == null || !m.Active) return 0;
        int n = 0;
        if(m.L("multishot") > 0 && Random.value < AugmentDefs.MultiShotChance(m.L("multishot"))) n++;
        if(m.L("twin") > 0 && Random.value < AugmentDefs.TwinChance(m.L("twin"))) n++;
        if(m.Synergy(AugTag.Split, 6)) n++;
        return n;
    }

    // 반사 파편: 탄이 화면 끝에서 튕기는 횟수
    public static int RicochetCount {
        get {
            var m = Instance;
            return m != null && m.Active && m.L("ricochet") > 0 ? AugmentDefs.RicochetCount(m.L("ricochet")) : 0;
        }
    }

    public static float PoisonAreaScale {
        get {
            var m = Instance;
            return m != null && m.Active && m.L("cloud") > 0 ? 1f + AugmentDefs.CloudSize(m.L("cloud")) : 1f;
        }
    }

    float PoisonDamageMul {
        get {
            float m = 1f;
            if(L("venom") > 0) m += AugmentDefs.VenomBonus(L("venom"));
            if(L("mutate") > 0) m += Mathf.Min(AugmentDefs.MutateCap, Stack("mutate"));
            if(Synergy(AugTag.Plague, 8)) m *= 2f;
            return m;
        }
    }

    float PoisonSeconds => (L("infect") > 0 ? AugmentDefs.InfectSeconds(L("infect")) : 2f) + (Synergy(AugTag.Plague, 2) ? 1f : 0f);

    public int CarpetBombCount => Active && L("carpet") > 0 && IsEquipped(SideWeapon.Bomber) ? AugmentDefs.CarpetBombs(L("carpet")) : 0;
    public int BounceExtraCount => Active && L("bn_more") > 0 && IsEquipped(SideWeapon.BounceGun) ? AugmentDefs.BounceExtra(L("bn_more")) : 0;

    // 폭격기 폭탄에도 같은 폭발 범위 증강(화약 연구, 연쇄 세트 2)을 곱해요
    public static float BombRadiusMultiplier {
        get {
            var m = Instance;
            return m != null && m.Active ? m.ExplosionRadius / 1.3f : 1f;
        }
    }

    float ExplosionRadius {
        get {
            float r = 1.3f;
            if(L("gunpowder") > 0) r *= 1f + AugmentDefs.GunpowderRadius(L("gunpowder"));
            if(Synergy(AugTag.Chain, 2)) r *= 1.1f;
            return r;
        }
    }

    int MaxChainDepth {
        get {
            int l = L("chain");
            if(l <= 0) return 1;
            return AugmentDefs.ChainDepth(l) + (Synergy(AugTag.Chain, 4) ? 2 : 0);
        }
    }

    float ExplosionDamageMul {
        get {
            float m = 1f + Mathf.Min(AugmentDefs.FurnaceCap, Stack("furnace"));
            if(ComboTier(AugmentDefs.ComboFear) == 2) m *= 1.2f;
            return m;
        }
    }

    float CritChanceBonus(bool boss)
    {
        float b = 0;
        if(L("aim") > 0) b += AugmentDefs.AimChance(L("aim"));
        if(boss && L("hunt") > 0) b += AugmentDefs.HuntChance(L("hunt"));
        if(Synergy(AugTag.Crit, 2)) b += 5f;
        return b;
    }

    float CritDamageBonus(bool boss)
    {
        float b = 0;
        if(L("vital") > 0) b += AugmentDefs.VitalDamage(L("vital"));
        if(Synergy(AugTag.Crit, 4)) b += 0.5f;
        if(L("streak") > 0) b += AugmentDefs.StreakPer(L("streak")) * Mathf.Min(critStreak, AugmentDefs.StreakMax);
        if(L("overcrit") > 0) {
            float total = Player.instance.FinalCriticalChance + CritChanceBonus(boss);
            if(total > 100f) b += (total - 100f) / 100f * AugmentDefs.OvercritRate(L("overcrit"));
        }
        return b;
    }

    bool AllCrit => Synergy(AugTag.Crit, 8) || revengeLeft > 0;
    #endregion

    #region 적중 (Enemy.OnHit 이 불러요)
    // 피해를 깎기 전: 치명타 다시 굴리기, 받는 피해 배율. 반환값 = 피해 배율
    public double BeforeHit(Enemy e, ref bool crit, ref float critDamage)
    {
        if(!Active) return 1;
        bool boss = e.IsBoss;
        bool direct = effectDepth == 0 && splitGen == 0 && !IsPoisonHit;
        if(direct) {
            if(!crit) {
                if(AllCrit) crit = true;
                else {
                    float baseC = Player.instance.FinalCriticalChance;
                    float bonus = CritChanceBonus(boss);
                    // 처음 굴림에서 안 나온 경우만 다시 굴려서, 합계가 (기본 + 보너스)%가 되게
                    if(bonus > 0 && baseC < 100f && Random.value < bonus / (100f - baseC)) crit = true;
                }
            }
            if(crit) {
                critDamage += CritDamageBonus(boss);
                critStreak++;
                if(L("moonhunt") > 0) {
                    AddStack("crits", 1);
                    if(Stack("crits") >= AugmentDefs.MoonhuntEvery(L("moonhunt"))) { stacks["crits"] = 0; MoonStrike(); }
                }
            } else critStreak = 0;
        } else if(splitGen > 0 && !crit) {
            // 조합 "승리": 파편도 치명타
            int v = ComboTier(AugmentDefs.ComboVictory);
            if(v > 0) {
                float chance = Player.instance.FinalCriticalChance + CritChanceBonus(boss);
                if(v == 1) chance *= 0.5f;
                if(Random.Range(0f, 100f) < chance) { crit = true; critDamage += CritDamageBonus(boss); }
            }
        }

        double mul = 1;
        if(IsPoisonHit) mul *= PoisonDamageMul;
        if(e.IsPoisoned && L("corrode") > 0) mul *= 1 + AugmentDefs.CorrodeBonus(L("corrode"));
        if(e.Frozen) {
            if(L("shatter") > 0) mul *= 1 + AugmentDefs.ShatterBonus(L("shatter"));
            if(Synergy(AugTag.Frost, 4)) mul *= 2;
        }
        return mul;
    }

    // 처형: 맞고 남을 체력이 기준 이하인 일반 적은 바로 죽어요
    public bool ShouldExecute(Enemy e, double healthLeft)
    {
        if(!Active || e.IsBoss || L("execute") <= 0 || e.EnemyHealth <= 0) return false;
        return healthLeft / e.EnemyHealth <= AugmentDefs.ExecuteLine(L("execute"));
    }

    // 피해를 준 뒤 (적이 아직 살아 있을 수도, 이번 타격으로 죽을 수도 있어요)
    public void OnEnemyHit(Enemy enemy, long damage, bool crit)
    {
        if(!Active) return;
        bool direct = effectDepth == 0 && splitGen == 0 && !IsPoisonHit;
        lastHitCrit = crit;

        // 독 비행기 독 지대: 때릴 때마다 독 1중첩 (2026-11-18: 예전엔 전염 증강이 있어야 했어요.
        // 독 무기 기본 특성 "독 중첩 1당 피해 +2%"가 독 비행기 혼자서도 켜지게)
        if(CurrentHitKind == HitKind.PoisonArea)
            enemy.AddPoison(1, PoisonSeconds);

        if(direct) {
            if(L("infect") > 0) enemy.AddPoison(1, PoisonSeconds);

            // 냉기
            if(L("frostshot") > 0) {
                float secs = 2f + (L("chill") > 0 ? AugmentDefs.ChillSeconds(L("chill")) : 0f);
                enemy.AddSlow(AugmentDefs.FrostSlow(L("frostshot")), secs);
            }
            if(L("freeze") > 0 && enemy.SlowLeft > 0 && (!enemy.IsBoss || Synergy(AugTag.Frost, 8))) {
                float chance = AugmentDefs.FreezeChance(L("freeze"));
                if(L("permafrost") > 0) chance += Mathf.Min(0.15f, Mathf.Floor(Stack("freezes") / 50f) * AugmentDefs.PermafrostPer(L("permafrost")));
                if(Synergy(AugTag.Frost, 8)) chance *= 2f;
                if(Random.value < chance) {
                    enemy.Freeze(enemy.IsBoss ? 1f : 2f);
                    AddStack("freezes", 1);
                }
            }

            // 번개
            float lc = Synergy(AugTag.Thunder, 8) ? 1f : L("static") > 0 ? AugmentDefs.StaticChance(L("static")) : 0f;
            if(lc > 0 && Random.value < lc) Lightning(enemy, LightningJumps, (long)(PlayerAttack * 0.5f));

            // 중력: 끌어당김
            if(L("pull") > 0 || Synergy(AugTag.Gravity, 2))
                PullToward(enemy.transform.position, L("pull") > 0 ? AugmentDefs.PullRadius(L("pull")) : 1.5f, 0.12f, enemy);

            // 치명 세트 6: 치명타가 뒤의 적도 꿰뚫음
            if(crit && Synergy(AugTag.Crit, 6)) {
                var behind = NearestEnemies(enemy.transform.position, 2.5f, 1, enemy);
                foreach(var t in behind) {
                    RemasterFx.Line(enemy.transform.position, t.transform.position, AugmentDefs.TagColor(AugTag.Crit));
                    HitEffect(t, damage / 2, 99);
                }
            }

            // 굴절: 레이저가 옆 적 2마리에게 꺾여 들어감
            if(CurrentHitKind == HitKind.Laser && L("refract") > 0 && Random.value < AugmentDefs.RefractChance(L("refract"))) {
                foreach(var t in NearestEnemies(enemy.transform.position, 3.5f, 2, enemy)) {
                    RemasterFx.Line(enemy.transform.position, t.transform.position, AugmentDefs.TagColor(AugTag.Split));
                    HitEffect(t, damage / 2, 99);
                }
            }
        }

        // 분열 (별무리가 있으면 파편도 다시 갈라져요)
        int maxGen = L("starswarm") > 0 ? AugmentDefs.StarswarmGenerations(L("starswarm")) : 1;
        if((direct || (splitGen > 0 && splitGen < maxGen)) && effectDepth == 0) {
            float sc = Synergy(AugTag.Split, 8) ? 1f : L("split") > 0 ? AugmentDefs.SplitChance(L("split")) : 0f;
            if(sc > 0 && Random.value < sc) Split(enemy, damage);
        }
    }

    void Split(Enemy enemy, long damage)
    {
        long shard = Synergy(AugTag.Split, 4) ? damage : damage / 2;
        if(shard <= 0) return;
        var targets = NearestEnemies(enemy.transform.position, 3f, 2, enemy);
        int rainbow = ComboTier(AugmentDefs.ComboRainbow);
        splitGen++;
        try {
            foreach(var t in targets) {
                RemasterFx.Line(enemy.transform.position, t.transform.position, AugmentDefs.TagColor(AugTag.Split));
                t.OnHit(shard, false, false, 0, Armor.EffectPen);
                if(rainbow > 0 && Random.value < (rainbow == 2 ? 0.3f : 0.15f)) Lightning(t, LightningJumps, (long)(PlayerAttack * 0.5f));
            }
        } finally {
            splitGen--;
        }
    }

    // 독 1틱 (Enemy 가 0.5초마다 불러요)
    public void PoisonTick(Enemy e)
    {
        if(!Active || e.PoisonStacks <= 0) return;
        long dmg = (long)Mathf.Max(1, PlayerAttack * AugmentDefs.PoisonTickBase(L("infect")) * e.PoisonStacks);
        var prev = CurrentHitKind;
        CurrentHitKind = HitKind.Poison;
        try { e.OnHit(dmg, false, false, 0, Armor.EffectPen); }
        finally { CurrentHitKind = prev; }

        // 독 폭발
        if(L("toxburst") > 0 && e.PoisonStacks >= AugmentDefs.ToxburstStacks(L("toxburst")) && e.gameObject.activeInHierarchy && e._state != Enemy.EnemyState.dead) {
            Vector3 p = e.transform.position;
            e.PoisonStacks = 1;
            RemasterFx.Burst(p, 1.5f, AugmentDefs.TagColor(AugTag.Plague));
            foreach(var t in EnemiesInRadius(p, 1.5f)) {
                if(t == e) continue;
                t.AddPoison(2, PoisonSeconds);
                HitEffect(t, dmg * 4, 99, HitKind.Poison);
            }
        }
    }

    // 얼었던 적이 녹을 때 (Enemy 가 불러요)
    public void OnThaw(Enemy e)
    {
        if(!Active) return;
        FrostBurst(e.transform.position);
    }

    void FrostBurst(Vector3 pos)
    {
        float mult = L("glacier") > 0 ? AugmentDefs.GlacierDamage(L("glacier")) : Synergy(AugTag.Frost, 6) ? 0.5f : 0f;
        if(mult <= 0) return;
        RemasterFx.Burst(pos, 1.5f, AugmentDefs.TagColor(AugTag.Frost));
        long dmg = (long)Mathf.Max(1, PlayerAttack * mult);
        foreach(var t in EnemiesInRadius(pos, 1.5f)) HitEffect(t, dmg, 99);
    }
    #endregion

    #region 처치
    public void OnEnemyKilled(Enemy enemy)
    {
        if(!Active || enemy == null) return;
        bool isBoss = enemy.IsBoss;
        if(enemy._typeofEnemy != Enemy.typeofEnemy.InfiniteStage && !isBoss) return;

        int kills = 1;
        int reaper = ComboTier(AugmentDefs.ComboReaper);
        if(reaper > 0 && lastHitCrit) kills += reaper;
        int discord = ComboTier(AugmentDefs.ComboDiscord);
        if(discord > 0 && enemy.IsPoisoned) kills += discord;
        long before = expKills;
        expKills += kills;
        if(isBoss) expBossKills++;
        if(L("stardustshot") > 0 && expKills / 100 > before / 100)
            stardustSizeStack = Mathf.Min(2f, stardustSizeStack + AugmentDefs.StardustShotPer100(L("stardustshot")));

        Vector3 pos = enemy.transform.position;

        // 죽음의 표식
        if(L("deathmark") > 0) {
            AddStack("deathmark", 1);
            if(Stack("deathmark") >= AugmentDefs.DeathmarkEvery(L("deathmark"))) { stacks["deathmark"] = 0; DeathMark(); }
        }
        // 번개 세트 6: 처치 20마다 낙뢰
        if(Synergy(AugTag.Thunder, 6) && expKills / 20 > before / 20) {
            var t = RandomEnemy();
            if(t != null) Thunderbolt(t);
        }
        // 눈송이
        if(L("snowflake") > 0) {
            float slow = L("frostshot") > 0 ? AugmentDefs.FrostSlow(L("frostshot")) : 0.2f;
            foreach(var t in EnemiesInRadius(pos, AugmentDefs.SnowflakeRadius(L("snowflake")))) t.AddSlow(slow, 2f);
        }
        // 얼어서 죽으면 빙하 폭발
        if(enemy.Frozen) FrostBurst(pos);
        // 전염
        bool spread = (enemy.IsPoisoned && L("contagion") > 0) || Synergy(AugTag.Plague, 8);
        if(spread) Spread(enemy);

        // 폭발: 총으로 잡으면 점화탄, 폭발로 잡으면 연쇄 폭발이 있을 때만 이어짐
        bool igniteOn = L("ignite") > 0 || Synergy(AugTag.Chain, 6);
        if(!igniteOn) return;
        int depth = effectDepth;
        if(depth > 0 && depth >= MaxChainDepth) return;
        float mult = L("ignite") > 0 ? AugmentDefs.IgniteDamage(L("ignite")) : 0.5f;
        explosions.Add(new PendingExplosion {
            pos = pos,
            depth = depth + 1,
            damage = (long)Mathf.Max(1, PlayerAttack * mult),
            radius = ExplosionRadius,
            time = Time.time + (depth == 0 ? 0f : 0.08f)
        });

        if(depth + 1 >= 10 && L("hammer") > 0 && Time.time >= hammerReadyTime) {
            hammerReadyTime = Time.time + 3f;
            Shockwave();
        }
    }

    void Spread(Enemy from)
    {
        int n = (L("contagion") > 0 ? AugmentDefs.ContagionTargets(L("contagion")) : 3) + (Synergy(AugTag.Plague, 4) ? 2 : 0);
        int stacks_ = Mathf.Max(1, from.PoisonStacks);
        var targets = NearestEnemies(from.transform.position, 3f, n + 2, from);
        int done = 0;
        foreach(var t in targets) {
            if(done >= n) break;
            if(t.IsBoss && !Synergy(AugTag.Plague, 6)) continue;
            t.AddPoison(stacks_, PoisonSeconds);
            RemasterFx.Line(from.transform.position, t.transform.position, AugmentDefs.TagColor(AugTag.Plague));
            done++;
        }
        if(done > 0 && L("mutate") > 0) AddStack("mutate", AugmentDefs.MutatePer(L("mutate")) * done);
    }
    #endregion

    #region 실드 (InfiniteStage 가 불러요)
    // 실드 피해를 막을 수 있으면 줄여서 돌려줘요
    public int FilterShieldLoss(int loss)
    {
        if(!Active) return loss;
        if(invulnLeft > 0) return 0;
        if(aegisLeft > 0) {
            aegisLeft--;
            ui.Toast("아이기스가 막았어요!", AugmentDefs.TagColor(AugTag.Guard));
            return 0;
        }
        return loss;
    }

    public void OnShieldLost(int loss)
    {
        if(!Active || loss <= 0) return;
        shieldLostThisStage = true;
        Vector3 ship = ShipPos;

        if(L("revenge") > 0) revengeLeft = AugmentDefs.RevengeSeconds(L("revenge"));

        float counter = L("counter") > 0 ? AugmentDefs.CounterDamage(L("counter")) : Synergy(AugTag.Guard, 4) ? 2f : 0f;
        if(counter > 0) {
            RemasterFx.Burst(ship, 4f, AugmentDefs.TagColor(AugTag.Guard));
            long dmg = (long)Mathf.Max(1, PlayerAttack * counter);
            foreach(var t in EnemiesInRadius(ship, 4f)) HitEffect(t, dmg, 99);
        }

        if(L("chronostop") > 0 && Stack("chronoUsed") < AugmentDefs.ChronoUses) {
            AddStack("chronoUsed", 1);
            float secs = AugmentDefs.ChronoSeconds(L("chronostop"));
            foreach(var t in AllEnemies()) t.Stun(secs);
            ui.Toast($"크로노스의 정지! ({AugmentDefs.ChronoUses - (int)Stack("chronoUsed")}번 남음)", AugmentDefs.TagColor(AugTag.Gravity), true);
        }

        int blizzard = ComboTier(AugmentDefs.ComboBlizzard);
        if(blizzard > 0) {
            foreach(var t in AllEnemies()) {
                t.AddSlow(0.5f, 3f);
                if(blizzard == 2 && !t.IsBoss) t.Freeze(1.5f);
            }
        }

        int fate = ComboTier(AugmentDefs.ComboFate);
        if(fate > 0) SpawnBlackHole(fate == 2);

        if(Synergy(AugTag.Guard, 8) && !invulnUsedThisStage) {
            invulnUsedThisStage = true;
            invulnLeft = 10f;
            ui.Toast("아테나의 가호: 10초 무적!", AugmentDefs.TagColor(AugTag.Guard), true);
        }
    }

    // 실드가 0이 될 때 버틸 수 있으면 true (아테나의 방벽)
    public bool TrySurviveLethal()
    {
        if(!Active || L("barrier") <= 0 || Stack("barrierUsed") >= L("barrier")) return false;
        AddStack("barrierUsed", 1);
        RemasterFx.Shockwave(ShipPos);
        int prev = effectDepth;
        effectDepth = 99;
        try {
            foreach(var t in AllEnemies()) if(!t.IsBoss) t.ForceKill();
        } finally { effectDepth = prev; }
        ui.Toast("아테나의 방벽! 버텨 냈어요", AugmentDefs.TagColor(AugTag.Guard), true);
        return true;
    }
    #endregion

    #region 스테이지
    public void OnStageCleared(int clearedStage)
    {
        if(!loaded) return;
        if(clearedStage + 1 > bestStage) bestStage = clearedStage + 1;

        // 수호: 맹세 누적, 재생, 아이기스·무적 다시 채움
        if(L("oath") > 0 && !shieldLostThisStage) AddStack("oath", AugmentDefs.OathPer(L("oath")));
        if(L("regen") > 0 && clearedStage % AugmentDefs.RegenEvery(L("regen")) == 0 && InfiniteStage.Instance != null) {
            InfiniteStage.Instance.AddShieldNow(1);
            ui.Toast("재생: 실드 +1", AugmentDefs.TagColor(AugTag.Guard));
        }
        shieldLostThisStage = false;
        invulnUsedThisStage = false;
        aegisLeft = L("aegis") > 0 ? AugmentDefs.AegisBlocks(L("aegis")) : 0;

        if(RemasterBalance.IsAugmentStage(clearedStage))
            QueuePull(1);
        ui.RefreshHUD();
    }
    #endregion

    #region 매 프레임
    void Update()
    {
        if(explosions.Count > 0) {
            if(!Active) explosions.Clear();
            else {
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

        if(loaded && Active) { TickEffects(Time.deltaTime); TickUltimates(Time.deltaTime); }
        else { fires.Clear(); blackHoles.Clear(); }

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

    void TickEffects(float dt)
    {
        lightningBudget = Mathf.Min(40f, lightningBudget + dt * 40f);
        hasteClock += dt;
        if(invulnLeft > 0) invulnLeft -= dt;
        if(revengeLeft > 0) revengeLeft -= dt;

        enemyCountTimer -= dt;
        if(enemyCountTimer <= 0) {
            enemyCountTimer = 0.25f;
            var em = EnemyManager.Instance;
            enemyCount = em != null ? em.enemyData.Count + em.bossData.Count : 0;
        }

        // 잔불
        for(int i = fires.Count - 1; i >= 0; i--) {
            var f = fires[i];
            if(Time.time < f.next) continue;
            f.next = Time.time + 0.5f;
            f.ticks--;
            RemasterFx.Burst(f.pos, f.radius, new Color(1f, 0.5f, 0.15f, 0.6f));
            foreach(var t in EnemiesInRadius(f.pos, f.radius)) HitEffect(t, f.damage, 99);
            if(f.ticks <= 0) fires.RemoveAt(i); else fires[i] = f;
        }

        // 뇌운
        if(L("storm") > 0) {
            stormTimer += dt;
            if(stormTimer >= AugmentDefs.StormInterval(L("storm"))) {
                stormTimer = 0;
                for(int i = 0; i < 5; i++) {
                    var t = RandomEnemy();
                    if(t != null) Thunderbolt(t);
                }
            }
        }

        // 블랙홀 (중력 우물, 중력 세트 6)
        float interval = L("horizon") > 0 ? AugmentDefs.HorizonInterval(L("horizon")) : Synergy(AugTag.Gravity, 6) ? 40f : 0f;
        if(interval > 0) {
            if(Synergy(AugTag.Gravity, 6)) interval *= 0.5f;
            horizonTimer += dt;
            if(horizonTimer >= interval) { horizonTimer = 0; SpawnBlackHole(false); }
        }
        TickBlackHoles(dt);

        // 북풍
        if(L("northwind") > 0) {
            northwindTimer += dt;
            if(northwindTimer >= AugmentDefs.NorthwindInterval(L("northwind"))) {
                northwindTimer = 0;
                foreach(var t in AllEnemies()) t.Freeze(t.IsBoss ? 1f : 3f);
                RemasterFx.Shockwave(ShipPos);
                ui.Toast("북풍!", AugmentDefs.TagColor(AugTag.Frost));
            }
        }

        // 네메시스의 역병
        if(L("nemesis") > 0) TickNemesis(dt);
    }

    void TickNemesis(float dt)
    {
        if(nemesisCooldown > 0) nemesisCooldown -= dt;
        if(nemesisAt >= 0) {
            nemesisAt -= dt;
            if(nemesisAt < 0) {
                nemesisAt = -1;
                nemesisCooldown = 15f;
                int prev = effectDepth;
                effectDepth = 99;
                try {
                    foreach(var t in AllEnemies()) {
                        if(t.IsBoss) DamageMaxHp(t, AugmentDefs.NemesisBoss(L("nemesis")));
                        else t.ForceKill();
                    }
                } finally { effectDepth = prev; }
                RemasterFx.Shockwave(ShipPos);
                ui.Toast("네메시스의 역병!", AugmentDefs.TagColor(AugTag.Plague), true);
            }
            return;
        }
        nemesisCheck -= dt;
        if(nemesisCheck > 0 || nemesisCooldown > 0) return;
        nemesisCheck = 1f;
        int n = 0;
        foreach(var t in AllEnemies()) {
            if(t.IsBoss || !t.IsInRange) continue;
            if(!t.IsPoisoned) return;
            n++;
        }
        if(n >= 5) {
            nemesisAt = 3f;
            ui.Toast("역병이 퍼졌어요… 3초 뒤", AugmentDefs.TagColor(AugTag.Plague));
        }
    }
    #endregion

    #region 폭발
    static readonly Color ChainSparkColor = new Color(1f, 0.86f, 0.3f);

    void Explode(PendingExplosion e)
    {
        float r = e.radius;
        RemasterFx.Spark(e.pos, r, ChainSparkColor);   // 노란 불꽃 (폭격기의 주황 폭발과 구분)
        long dmg = (long)(e.damage * (double)ExplosionDamageMul);
        if(L("furnace") > 0) AddStack("furnace", AugmentDefs.FurnacePer(L("furnace")));

        // 조합 "공포": 폭발이 주변 적을 가운데로 끌어당김
        if(ComboTier(AugmentDefs.ComboFear) > 0) PullToward(e.pos, r * 1.6f, 0.4f, null);

        var targets = EnemiesInRadius(e.pos, r);
        int prev = effectDepth;
        effectDepth = e.depth;
        try {
            foreach(var t in targets) t.OnHit(dmg, false, false, 0, Armor.EffectPen, e.src);
        } finally {
            effectDepth = prev;
        }

        if(e.mini) return;

        // 잔불
        if(L("embers") > 0)
            fires.Add(new FireZone { pos = e.pos, radius = r * 0.8f, damage = (long)Mathf.Max(1, dmg * AugmentDefs.EmberTick(L("embers"))), ticks = 4, next = Time.time + 0.5f });
        // 파편
        if(L("shrapnel") > 0) {
            var pool = EnemiesInRadius(e.pos, r * 2.5f);
            long sd = (long)Mathf.Max(1, dmg * AugmentDefs.ShrapnelDamage(L("shrapnel")));
            for(int i = 0; i < 3 && pool.Count > 0; i++) {
                var t = pool[Random.Range(0, pool.Count)];
                RemasterFx.Line(e.pos, t.transform.position, AugmentDefs.TagColor(AugTag.Chain));
                HitEffect(t, sd, e.depth);
            }
        }
        // 열폭풍
        if(L("heatwave") > 0) {
            long hd = (long)Mathf.Max(1, dmg * AugmentDefs.HeatwaveDamage(L("heatwave")));
            foreach(var t in EnemiesInRadius(e.pos, r * 1.5f)) {
                if(!t.IsBoss) {
                    Vector3 d = t.transform.position - e.pos;
                    if(d.sqrMagnitude > 0.0001f) t.transform.position += d.normalized * 0.5f;
                }
                HitEffect(t, hd, e.depth);
            }
        }
        // 조합 "천둥망치": 폭발이 번개를 튕김
        int th = ComboTier(AugmentDefs.ComboThunderHammer);
        if(th > 0 && Random.value < (th == 2 ? 0.3f : 0.15f) && targets.Count > 0)
            Lightning(targets[0], LightningJumps, (long)(PlayerAttack * 0.5f));
        // 연쇄 세트 8: 작은 폭발 3번 더
        if(Synergy(AugTag.Chain, 8)) {
            for(int i = 0; i < 3; i++) {
                Vector2 off = Random.insideUnitCircle * r;
                explosions.Add(new PendingExplosion {
                    pos = e.pos + new Vector3(off.x, off.y), depth = e.depth, damage = e.damage * 3 / 10,
                    radius = r * 0.6f, time = Time.time + 0.1f + 0.08f * i, mini = true
                });
            }
        }
    }

    void Shockwave()
    {
        long dmg = (long)Mathf.Max(1, PlayerAttack * AugmentDefs.HammerDamage(L("hammer")));
        RemasterFx.Shockwave(ShipPos);
        HitAll(dmg);
        ui.Toast("대장장이의 망치!", AugmentDefs.TagColor(AugTag.Chain));
    }
    #endregion

    #region 번개 / 낙뢰
    int LightningJumps => 2 + (L("conduct") > 0 ? AugmentDefs.ConductJumps(L("conduct")) : 0) + (Synergy(AugTag.Thunder, 2) ? 1 : 0);

    void Lightning(Enemy from, int jumps, long baseDamage)
    {
        if(from == null || lightningBudget < 1f) return;
        lightningBudget -= 1f;
        var hit = new HashSet<Enemy> { from };
        Enemy cur = from;
        Color col = AugmentDefs.TagColor(AugTag.Thunder);
        float overload = L("overload") > 0 ? AugmentDefs.OverloadBonus(L("overload")) : 0f;
        float perJump = L("chainlight") > 0 ? AugmentDefs.ChainlightPer(L("chainlight")) : 0f;
        float rod = L("rod") > 0 ? AugmentDefs.RodBonus(L("rod")) : 0f;
        for(int j = 0; j < jumps; j++) {
            Enemy next = null;
            float best = float.MaxValue;
            Vector3 p = cur.transform.position;
            foreach(var t in EnemiesInRadius(p, rod > 0 ? 6f : 3.5f)) {
                if(hit.Contains(t)) continue;
                float d = (t.transform.position - p).sqrMagnitude;
                if(rod > 0 && t.IsBoss) d -= 1000f;          // 피뢰침: 보스 먼저
                else if(d > 3.5f * 3.5f) continue;
                if(d < best) { best = d; next = t; }
            }
            if(next == null) break;
            hit.Add(next);
            double dmg = baseDamage * (1 + overload) * (1 + perJump * j) * (next.IsBoss ? 1 + rod : 1);
            RemasterFx.Line(p, next.transform.position, col);
            HitEffect(next, (long)System.Math.Max(1, dmg), 1);
            if(Synergy(AugTag.Thunder, 4)) next.Stun(0.4f);
            CountLightning();
            cur = next;
        }
    }

    void CountLightning()
    {
        if(L("capacitor") <= 0) return;
        AddStack("lightning", 1);
        if(Stack("lightning") >= AugmentDefs.CapacitorEvery(L("capacitor"))) {
            stacks["lightning"] = 0;
            var t = RandomEnemy();
            if(t != null) Thunderbolt(t);
        }
    }

    void Thunderbolt(Enemy target)
    {
        Vector3 p = target.transform.position;
        Camera cam = Camera.main;
        float top = cam != null ? cam.orthographicSize + 1f : 10f;
        RemasterFx.Line(new Vector3(p.x, top), p, AugmentDefs.TagColor(AugTag.Thunder));
        RemasterFx.Burst(p, 1.2f, AugmentDefs.TagColor(AugTag.Thunder));
        long dmg = (long)Mathf.Max(1, PlayerAttack * 3f * (1f + (L("overload") > 0 ? AugmentDefs.OverloadBonus(L("overload")) : 0f)));
        foreach(var t in EnemiesInRadius(p, 1.2f)) HitEffect(t, dmg, 1);
        if(L("judgment") > 0 && target.gameObject.activeInHierarchy)
            Lightning(target, AugmentDefs.JudgmentJumps(L("judgment")), (long)(PlayerAttack * 0.5f));
    }
    #endregion

    #region 블랙홀
    void SpawnBlackHole(bool big)
    {
        // 적이 가장 많이 모인 곳 (최대 15마리만 살펴봐요)
        var all = AllEnemies();
        if(all.Count == 0) return;
        Enemy center = null;
        int bestN = -1;
        for(int i = 0; i < Mathf.Min(15, all.Count); i++) {
            var c = all[Random.Range(0, all.Count)];
            int n = 0;
            foreach(var o in all) if((o.transform.position - c.transform.position).sqrMagnitude < 4f) n++;
            if(n > bestN) { bestN = n; center = c; }
        }
        blackHoles.Add(new BlackHole { pos = center.transform.position, radius = big ? 5f : 3.5f, left = 2f, big = big });
    }

    void TickBlackHoles(float dt)
    {
        for(int i = blackHoles.Count - 1; i >= 0; i--) {
            var b = blackHoles[i];
            b.left -= dt;
            b.tick -= dt;
            bool tick = b.tick <= 0;
            if(tick) {
                b.tick = 0.25f;
                RemasterFx.Burst(b.pos, b.radius * 0.45f, AugmentDefs.TagColor(AugTag.Gravity));
            }
            long tickDmg = (long)Mathf.Max(1, PlayerAttack * 0.1f);
            foreach(var t in EnemiesInRadius(b.pos, b.radius)) {
                Vector3 d = b.pos - t.transform.position;
                float speed = t.IsBoss ? 0.4f * Mathf.Clamp01(Tuning.Get("blackhole_boss_pull", 0f) / 0.3f) : 3f;   // 2026-11-25: 보스는 안 끌려가요 (blackhole_boss_pull 0)
                t.transform.position += Vector3.ClampMagnitude(d, speed * dt);
                if(!t.IsBoss && Synergy(AugTag.Gravity, 8) && d.sqrMagnitude < 0.36f) { t.ForceKill(); continue; }
                if(tick) HitEffect(t, tickDmg, 99);
            }
            if(b.left <= 0) {
                blackHoles.RemoveAt(i);
                if(L("collapse") > 0) {
                    RemasterFx.Burst(b.pos, 2.5f, AugmentDefs.TagColor(AugTag.Gravity));
                    long dmg = (long)Mathf.Max(1, PlayerAttack * AugmentDefs.CollapseDamage(L("collapse")) * (b.big ? 2f : 1f));
                    foreach(var t in EnemiesInRadius(b.pos, 2.5f)) HitEffect(t, dmg, 1);
                }
            }
        }
    }
    #endregion

    #region 특수 일격
    void MoonStrike()
    {
        float crit = 2f + Player.instance.FinalCriticalDamage + CritDamageBonus(false);
        long dmg = (long)Mathf.Max(1, PlayerAttack * crit * 3f);
        RemasterFx.Shockwave(ShipPos);
        HitAll(dmg);
        ui.Toast("달의 사냥!", AugmentDefs.TagColor(AugTag.Crit), true);
    }

    void DeathMark()
    {
        Enemy target = null;
        foreach(var t in AllEnemies()) if(!t.IsBoss && t.IsInRange) { target = t; break; }
        if(target != null) {
            RemasterFx.Burst(target.transform.position, 1f, AugmentDefs.TagColor(AugTag.Curse));
            int prev = effectDepth;
            effectDepth = 99;
            try { target.ForceKill(); } finally { effectDepth = prev; }
            return;
        }
        foreach(var t in AllEnemies()) {
            if(!t.IsBoss) continue;
            RemasterFx.Burst(t.transform.position, 1.5f, AugmentDefs.TagColor(AugTag.Curse));
            DamageMaxHp(t, 0.1f);
            break;
        }
    }

    // 최대 체력의 일정 비율 피해 (구간 배율을 되돌려서 넘겨요)
    void DamageMaxHp(Enemy t, float ratio)
    {
        double raw = t.EnemyHealth * ratio / System.Math.Max(1e-9, t.NumberScale);
        HitEffect(t, WeaponBase.ClampDamage(raw), 99);
    }

    void HitAll(long dmg)
    {
        foreach(var t in AllEnemies()) HitEffect(t, dmg, 99);
    }

    // 효과 피해: 단수(depth)를 정해서 연쇄 폭발이 끝없이 이어지지 않게
    void HitEffect(Enemy t, long dmg, int depth, HitKind kind = HitKind.Weapon)
    {
        if(t == null || t._state == Enemy.EnemyState.dead || !t.gameObject.activeInHierarchy) return;
        int prev = effectDepth;
        var prevKind = CurrentHitKind;
        effectDepth = Mathf.Max(depth, prev);
        CurrentHitKind = kind;
        try { t.OnHit(dmg, false, false, 0, Armor.EffectPen); }
        finally { effectDepth = prev; CurrentHitKind = prevKind; }
    }
    #endregion

    #region 적 찾기
    Vector3 ShipPos => WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;

    static List<Enemy> AllEnemies() => EnemiesInRadius(Vector3.zero, 9999f);

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

    static Enemy RandomEnemy()
    {
        var all = AllEnemies();
        all.RemoveAll(e => !e.IsInRange);
        return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
    }

    // 주변 일반 적을 center 쪽으로 조금 당겨요
    static void PullToward(Vector3 center, float radius, float amount, Enemy exclude)
    {
        foreach(var t in EnemiesInRadius(center, radius)) {
            if(t == exclude || t.IsBoss) continue;
            Vector3 d = center - t.transform.position;
            t.transform.position += Vector3.ClampMagnitude(d, amount);
        }
    }
    #endregion
}
