using System.Collections.Generic;
using UnityEngine;

// 무기 전직 (2026-11-26 사용자 결정, 성장요소_조사.md 6장)
// - 예전 특성(무기마다 공속·탄속·피해% 3개, 광석)을 바꿔요. 예전 저장은 특성에 쓴 광석을 돌려주고 특성은 0으로
// - 무기마다 전직 레벨 하나 (광석, 최대 20). 무기 Lv.50 이 되면 전직 Lv.1 은 공짜
//   전직 레벨 = ① 그 무기 피해 +% ② 고른 칸 효과가 모두 전직 레벨에 비례
// - 칸 6개: 무기 레벨 50 · 100 · 200 · 300 · 500 · 1000 에 열리고, 칸마다 둘 중 하나를 골라요 (언제든 바꾸기, 광석)
//   0 사격 방식: 속사형(공속) / 강타형(1발 피해)
//   1 시작 증강: 그 무기 전용 증강 ① / ② 를 들고 출발 (기본총: 시작 증강 뽑기 +1 / 리롤 +N)
//   2 치명 vs 안정: 치명 확률 / 기본 피해
//   3 연타 vs 첫 타: 같은 적을 계속 맞히면 피해가 쌓임 / 체력이 가득 찬 적에게 큰 피해
//   4 경제: 이 무기로 잡으면 골드 / 경험치
//   5 마무리: 방어 관통 / 치명 피해
// - 효과는 대부분 Enemy.OnHit 한 곳에서 (어느 무기가 때렸는지 알아요). 공속만 WeaponBase.Trait
// - 숫자는 모두 tuning (job_*). 값은 성장 예산표에서 다시 정해요
public static class WeaponJob
{
    public const int Slots = 6, WeaponCount = 11;   // TraitsData.WeaponType 0~10
    // 2026-11-28 사용자: 개조 상한 100 · 개조 레벨당 피해 1% · 칸은 '개조 레벨'로 열려요 (예전: 상한 20, 칸은 무기 레벨 50~1000)
    //  레벨당 효과는 예전(상한 20)의 1/5 → Lv.100 = 예전 Lv.20 과 같은 크기
    public static int MaxLevel => Mathf.Max(1, Mathf.RoundToInt(Tuning.Get("job_max_level", 100f)));
    public static int OpenWeaponLv => Mathf.Max(1, Mathf.RoundToInt(Tuning.Get("job_open_weapon_lv", 50f)));   // 개조가 열리는 무기 레벨 (Lv.1 공짜)
    static readonly float[] SlotLvDefault = { 1, 10, 25, 40, 60, 80 };
    public static int SlotLv(int s) => Mathf.Max(1, Mathf.RoundToInt(Tuning.Get("job_slot_lv" + s, SlotLvDefault[Mathf.Clamp(s, 0, Slots - 1)])));   // 칸이 열리는 개조 레벨
    public static readonly string[] SlotName = { "사격 방식", "시작 증강", "치명 vs 안정", "연타 vs 첫 타", "경제", "마무리" };

    static int[] levels = new int[WeaponCount];
    static int[] picks = NewPicks();
    static int[] NewPicks() { var p = new int[WeaponCount * Slots]; for(int i = 0; i < p.Length; i++) p[i] = -1; return p; }

    // ── 무기 ──
    public static WeaponBase WeaponOf(TraitsData.WeaponType w)
    {
        var m = WeaponManager.instance;
        if(m == null) return null;
        switch(w) {
            case TraitsData.WeaponType.MainWeapon: return m.mainWeapon;
            case TraitsData.WeaponType.DoubleGun: return m.doubleGun;
            case TraitsData.WeaponType.TrackingMissile: return m.trackingGun;
            case TraitsData.WeaponType.SniperGun: return m.sniperGun;
            case TraitsData.WeaponType.Bomber: return m.BomberGun;
            case TraitsData.WeaponType.BounceGun: return m.bounceGun;
            case TraitsData.WeaponType.Poison: return m.poison;
            case TraitsData.WeaponType.CircleLaser: return m.lazer;
            case TraitsData.WeaponType.StrLaser: return m.strLaser;
            case TraitsData.WeaponType.CodeA: return m.orbital;
            case TraitsData.WeaponType.Meteor: return m.meteor;
        }
        return null;
    }
    public static int WeaponLevel(TraitsData.WeaponType w) { var b = WeaponOf(w); return b != null ? b.UpgradeLevel : 0; }
    public static string WeaponName(TraitsData.WeaponType w) => w switch {
        TraitsData.WeaponType.MainWeapon => "기본총", TraitsData.WeaponType.DoubleGun => "쌍발총", TraitsData.WeaponType.TrackingMissile => "유도탄",
        TraitsData.WeaponType.SniperGun => "저격총", TraitsData.WeaponType.Bomber => "폭격기", TraitsData.WeaponType.BounceGun => "튕김총",
        TraitsData.WeaponType.Poison => "독 비행기", TraitsData.WeaponType.CircleLaser => "360 레이저", TraitsData.WeaponType.StrLaser => "블랙홀",
        TraitsData.WeaponType.CodeA => "드론", TraitsData.WeaponType.Meteor => "운석", _ => "?" };

    // ── 레벨·칸 ──
    // 무기 Lv.50 이면 전직 Lv.1 은 공짜
    public static int Level(TraitsData.WeaponType w)
    {
        int i = (int)w; if(i < 0 || i >= WeaponCount) return 0;
        int l = levels[i];
        if(l == 0 && WeaponLevel(w) >= OpenWeaponLv) l = 1;
        return Mathf.Clamp(l, 0, MaxLevel);
    }
    public static bool SlotOpen(TraitsData.WeaponType w, int slot) => Level(w) >= SlotLv(slot);
    public static int Pick(TraitsData.WeaponType w, int slot) { int i = (int)w * Slots + slot; return i >= 0 && i < picks.Length ? picks[i] : -1; }
    // 열린 칸에서 고른 쪽 (안 열렸거나 안 골랐으면 -1)
    public static int Active(TraitsData.WeaponType w, int slot) => SlotOpen(w, slot) ? Pick(w, slot) : -1;
    public static int OpenUnpicked(TraitsData.WeaponType w) { int n = 0; for(int s = 0; s < Slots; s++) if(SlotOpen(w, s) && Pick(w, s) < 0) n++; return n; }

    // 비용: 예전 특성 가격표 (1천 ~ 5조 광석) × job_cost_mul
    static readonly long[] Price = { 1000, 5000, 10000, 50000, 100000, 500000, 1000000, 5000000, 10000000, 50000000, 100000000, 500000000, 1000000000, 5000000000, 10000000000, 50000000000, 100000000000, 500000000000, 1000000000000, 5000000000000 };
    public static long LevelUpCost(TraitsData.WeaponType w)
    {
        int l = Level(w);
        if(l >= MaxLevel || WeaponLevel(w) < OpenWeaponLv) return -1;
        return (long)(PriceAt(l) * (double)Tuning.Get("job_cost_mul", 1f));
    }
    public static long SwapCost(TraitsData.WeaponType w) => (long)(PriceAt(Level(w)) * (double)Tuning.Get("job_swap_mul", 0.25f));
    // 임시 가격 (2026-11-28 사용자: "임시로 x3 넣어 줘") = job_cost_base × job_cost_growth^(지금 레벨 − 1) → Lv.1→2 5천, 마지막 Lv.19→20 약 1.9조
    //  예전 표(Price, 레벨마다 약 ×4.6, 마지막 5조)는 광산으로 끝까지 못 올려요. 진짜 값은 P4 행성 기지 채굴장 생산량에 맞춰 다시 정해요
    //  job_cost_growth 를 0 으로 하면 예전 표로 돌아가요
    static double PriceAt(int l)
    {
        float g = Tuning.Get("job_cost_growth", 1.2f);
        if(g <= 0f) return Price[Mathf.Clamp(l, 0, Price.Length - 1)];
        double v = Tuning.Get("job_cost_base", 5000f) * System.Math.Pow(g, Mathf.Max(0, l - 1));
        return System.Math.Min(v, 9e17);
    }

    public static bool TryLevelUp(TraitsData.WeaponType w)
    {
        long c = LevelUpCost(w);
        if(c < 0 || GameManager.GetOre() < c) return false;
        GameManager.SetOre(-c);
        levels[(int)w] = Level(w) + 1;
        return true;
    }
    // 처음 고르기는 공짜, 바꾸기는 광석
    public static bool TryPick(TraitsData.WeaponType w, int slot, int choice)
    {
        if(!SlotOpen(w, slot) || choice < 0 || choice > 1) return false;
        int cur = Pick(w, slot);
        if(cur == choice) return true;
        if(cur >= 0) { long c = SwapCost(w); if(GameManager.GetOre() < c) return false; GameManager.SetOre(-c); }
        picks[(int)w * Slots + slot] = choice;
        return true;
    }

    // ── 효과 크기 (전직 레벨 1당) ──
    static float T(string k, float d) => Tuning.Get(k, d);
    public static float DamagePer => T("job_dmg_per", 0.01f);           // 그 무기 피해 +5%
    public static string Describe(TraitsData.WeaponType w, int slot, int choice, int lv)
    {
        float L = Mathf.Max(1, lv);
        string P(float v) => $"+{v * 100f:0.#}%";
        switch(slot * 2 + choice) {
            case 0: return $"공속 {P(L * T("job_rapid_as", 0.006f))}";
            case 1: return $"1발 피해 {P(L * T("job_heavy_dmg", 0.01f))}";
            case 2: return w == TraitsData.WeaponType.MainWeapon ? $"시작 증강 뽑기 +{StartCount(lv)}" : $"{AugName(w, 0)} Lv.{StartCount(lv)} 들고 출발";
            case 3: return w == TraitsData.WeaponType.MainWeapon ? $"시작 리롤 +{StartCount(lv) * 2}" : $"{AugName(w, 1)} Lv.{StartCount(lv)} 들고 출발";
            case 4: return $"치명 확률 +{L * T("job_crit_cc", 0.2f):0.#}%p";
            case 5: return $"기본 피해 {P(L * T("job_steady_dmg", 0.006f))}";
            case 6: return $"맞힐수록 쌓임 최대 {P(L * T("job_combo_max", 0.008f))}";
            case 7: return $"가득 찬 적 피해 {P(L * T("job_first_dmg", 0.016f))}";
            case 8: return $"처치 골드 {P(L * T("job_gold", 0.006f))}";
            case 9: return $"처치 경험치 {P(L * T("job_exp", 0.006f))}";
            case 10: return $"방어 관통 {P(L * T("job_pen", 0.006f))}";
            case 11: return $"치명 피해 {P(L * T("job_cdmg", 0.02f))}";
        }
        return "";
    }
    public static string ChoiceName(TraitsData.WeaponType w, int slot, int choice)
    {
        switch(slot * 2 + choice) {
            case 0: return "속사형"; case 1: return "강타형";
            case 2: return w == TraitsData.WeaponType.MainWeapon ? "증강 뽑기" : AugName(w, 0);
            case 3: return w == TraitsData.WeaponType.MainWeapon ? "리롤" : AugName(w, 1);
            case 4: return "치명형"; case 5: return "안정형";
            case 6: return "연타"; case 7: return "첫 타";
            case 8: return "골드"; case 9: return "경험치";
            case 10: return "관통"; case 11: return "치명 피해";
        }
        return "";
    }
    // 고르기 창 (2026-11-26 job5_2): 한 줄 설명 + 큰 숫자
    // (2026-11-26 사용자: "치명타가 더 아파요" 같은 글은 너무 어려 보여요 → 짧은 RPG 설명체)
    public static string Sentence(TraitsData.WeaponType w, int slot, int choice)
    {
        bool main = w == TraitsData.WeaponType.MainWeapon;
        switch(slot * 2 + choice) {
            case 0: return "공격 속도 증가";
            case 1: return "한 발의 피해 증가";
            case 2: return main ? "판 시작 시 증강 추가 선택" : $"{AugName(w, 0)} 증강을 지닌 채 시작";
            case 3: return main ? "판 시작 시 리롤 추가 지급" : $"{AugName(w, 1)} 증강을 지닌 채 시작";
            case 4: return "치명타 확률 증가";
            case 5: return "기본 피해 증가";
            case 6: return "같은 적을 연속으로 맞히면 피해 누적";
            case 7: return "체력이 가득 찬 적에게 추가 피해";
            case 8: return "이 무기로 처치 시 골드 추가 획득";
            case 9: return "이 무기로 처치 시 경험치 추가 획득";
            case 10: return "적의 방어력 일부 무시";
            case 11: return "치명타 피해 증가";
        }
        return "";
    }
    public static string Amount(TraitsData.WeaponType w, int slot, int choice, int lv)
    {
        float L = Mathf.Max(1, lv);
        string P(float v) => $"+{v * 100f:0.#}%";
        bool main = w == TraitsData.WeaponType.MainWeapon;
        switch(slot * 2 + choice) {
            case 0: return P(L * T("job_rapid_as", 0.006f));
            case 1: return P(L * T("job_heavy_dmg", 0.01f));
            case 2: return main ? $"+{StartCount(lv)}번" : $"Lv.{StartCount(lv)}";
            case 3: return main ? $"+{StartCount(lv) * 2}번" : $"Lv.{StartCount(lv)}";
            case 4: return $"+{L * T("job_crit_cc", 0.2f):0.#}%p";
            case 5: return P(L * T("job_steady_dmg", 0.006f));
            case 6: return "최대 " + P(L * T("job_combo_max", 0.008f));
            case 7: return P(L * T("job_first_dmg", 0.016f));
            case 8: return P(L * T("job_gold", 0.006f));
            case 9: return P(L * T("job_exp", 0.006f));
            case 10: return P(L * T("job_pen", 0.006f));
            case 11: return P(L * T("job_cdmg", 0.02f));
        }
        return "";
    }
    // 받침이 있으면 "으로" (ㄹ 받침은 "로")
    public static string Ro(string word)
    {
        if(string.IsNullOrEmpty(word)) return "로";
        char c = word[word.Length - 1];
        if(c < 0xAC00 || c > 0xD7A3) return "로";
        int jong = (c - 0xAC00) % 28;
        return jong == 0 || jong == 8 ? "로" : "으로";
    }

    // 고르기 창 맨 아랫줄 (2026-11-26: 시작 증강은 "전직 Lv 마다 Lv.7 마다" 로 겹쳐 나와서 따로)
    public static string PerLevelLine(TraitsData.WeaponType w, int slot, int choice)
    {
        if(slot == 1) {
            int step = Mathf.Max(1, Mathf.RoundToInt(T("job_aug_step", 35f)));
            bool main = w == TraitsData.WeaponType.MainWeapon;
            // 언제 커지는지 레벨로 바로 (2026-11-28 사용자: "35마다 증강 레벨 +1 이 뭔 소리야")
            //  예) 개조 Lv.35·70 에 증강 Lv.2·3 / 기본총: 개조 Lv.35·70 에 뽑기 2·3번
            var at = new List<string>(); var to = new List<string>();
            for(int l = step; l <= MaxLevel; l += step) { at.Add(l.ToString()); int c = StartCount(l); to.Add((main && choice == 1 ? c * 2 : c).ToString()); }
            if(at.Count == 0) return main ? (choice == 0 ? "판 시작에 증강 뽑기 1번 더" : "판 시작에 리롤 2번 더") : "증강 Lv.1 로 시작";
            string lvs = string.Join("·", at), vals = string.Join("·", to);
            return main ? $"개조 Lv.{lvs} 에 {(choice == 0 ? "뽑기" : "리롤")} {vals}번" : $"개조 Lv.{lvs} 에 증강 Lv.{vals}";
        }
        return "개조 레벨마다 " + PerLevel(w, slot, choice);   // (2026-11-26 사용자: "전직 Lv 마다" → "전직 레벨마다")
    }

    // 레벨에 따라 커지는 칸 설명 (전직 Lv 마다)
    public static string PerLevel(TraitsData.WeaponType w, int slot, int choice)
    {
        string P(float v) => $"+{v * 100f:0.#}%";
        switch(slot * 2 + choice) {
            case 0: return P(T("job_rapid_as", 0.006f)); case 1: return P(T("job_heavy_dmg", 0.01f));
            case 2: case 3: return $"Lv.{Mathf.RoundToInt(T("job_aug_step", 35f))} 마다 +1";
            case 4: return $"+{T("job_crit_cc", 0.2f):0.#}%p"; case 5: return P(T("job_steady_dmg", 0.006f));
            case 6: return P(T("job_combo_max", 0.008f)); case 7: return P(T("job_first_dmg", 0.016f));
            case 8: return P(T("job_gold", 0.006f)); case 9: return P(T("job_exp", 0.006f));
            case 10: return P(T("job_pen", 0.006f)); case 11: return P(T("job_cdmg", 0.02f));
        }
        return "";
    }
    public static int StartCount(int lv) => 1 + Mathf.Max(0, lv) / Mathf.Max(1, Mathf.RoundToInt(T("job_aug_step", 35f)));

    // 시작 증강 ①·② (무기 전용 증강 S 두 개)
    static readonly Dictionary<TraitsData.WeaponType, string[]> StartAugs = new Dictionary<TraitsData.WeaponType, string[]> {
        { TraitsData.WeaponType.DoubleGun, new[] { "dg_rapid", "dg_accel" } }, { TraitsData.WeaponType.Bomber, new[] { "bm_big", "bm_second" } },
        { TraitsData.WeaponType.TrackingMissile, new[] { "tk_multi", "tk_homing" } }, { TraitsData.WeaponType.Poison, new[] { "ps_spread", "ps_long" } },
        { TraitsData.WeaponType.CodeA, new[] { "ob_extra", "ob_fast" } }, { TraitsData.WeaponType.CircleLaser, new[] { "lz_long", "lz_thick" } },
        { TraitsData.WeaponType.StrLaser, new[] { "bh_big", "bh_long" } }, { TraitsData.WeaponType.SniperGun, new[] { "sn_fast", "sn_ap" } },
        { TraitsData.WeaponType.BounceGun, new[] { "bn_more", "bn_accel" } }, { TraitsData.WeaponType.Meteor, new[] { "mt_rain", "mt_big" } },
    };
    public static string StartAugId(TraitsData.WeaponType w, int choice) => StartAugs.TryGetValue(w, out var a) && choice >= 0 && choice < a.Length ? a[choice] : null;
    static string AugName(TraitsData.WeaponType w, int choice) { var d = AugmentDefs.Get(StartAugId(w, choice)); return d != null ? d.name : "전용 증강"; }

    // ── 맞힐 때 (Enemy.OnHit) ──
    // 기본총은 SideWeapon 이 None 이라 Bullet 이 맞히는 순간에만 표시해요
    public static bool MainHitContext;
    public static int WeaponIndexOf(SideWeapon src)
    {
        if(src == SideWeapon.None) return MainHitContext ? (int)TraitsData.WeaponType.MainWeapon : -1;
        switch(src) {
            case SideWeapon.DoubleGun: return (int)TraitsData.WeaponType.DoubleGun;
            case SideWeapon.Bomber: return (int)TraitsData.WeaponType.Bomber;
            case SideWeapon.TrackingGun: return (int)TraitsData.WeaponType.TrackingMissile;
            case SideWeapon.Poison: return (int)TraitsData.WeaponType.Poison;
            case SideWeapon.Orbital: return (int)TraitsData.WeaponType.CodeA;
            case SideWeapon.Lazer: return (int)TraitsData.WeaponType.CircleLaser;
            case SideWeapon.StrLaser: return (int)TraitsData.WeaponType.StrLaser;
            case SideWeapon.Sniper: return (int)TraitsData.WeaponType.SniperGun;
            case SideWeapon.BounceGun: return (int)TraitsData.WeaponType.BounceGun;
            case SideWeapon.Meteor: return (int)TraitsData.WeaponType.Meteor;
        }
        return -1;
    }

    // 치명·치명 피해·관통을 바꾸고 피해 배율을 돌려줘요
    public static double OnHit(Enemy e, int wi, ref bool crit, ref float critDamage, ref float armorPen)
    {
        if(wi < 0 || wi >= WeaponCount) return 1.0;
        var w = (TraitsData.WeaponType)wi;
        int L = Level(w);
        if(L <= 0) return 1.0;
        double mul = 1.0 + L * DamagePer;
        if(Active(w, 0) == 1) mul *= 1.0 + L * T("job_heavy_dmg", 0.01f);
        if(Active(w, 2) == 0 && !crit && Random.value * 100f < L * T("job_crit_cc", 0.2f)) crit = true;
        if(Active(w, 2) == 1) mul *= 1.0 + L * T("job_steady_dmg", 0.006f);
        int s3 = Active(w, 3);
        if(s3 == 0) {
            if(e.jobComboWeapon == wi) e.jobComboStacks = Mathf.Min(8, e.jobComboStacks + 1); else { e.jobComboWeapon = wi; e.jobComboStacks = 1; }
            mul *= 1.0 + e.jobComboStacks / 8.0 * L * T("job_combo_max", 0.008f);
        } else if(s3 == 1 && e.EnemyHealth > 0 && e.CurrentHealth >= e.EnemyHealth * T("job_first_hp", 0.9f)) {
            mul *= 1.0 + L * T("job_first_dmg", 0.016f);
        }
        int s5 = Active(w, 5);
        if(s5 == 0) armorPen = Mathf.Clamp01(armorPen + L * T("job_pen", 0.006f));
        else if(s5 == 1 && crit) critDamage += L * T("job_cdmg", 0.02f);
        return mul;
    }

    // 처치 골드·경험치 (마지막으로 때린 무기)
    public static float KillGoldMul(int wi) => wi >= 0 && wi < WeaponCount && Active((TraitsData.WeaponType)wi, 4) == 0 ? 1f + Level((TraitsData.WeaponType)wi) * T("job_gold", 0.006f) : 1f;
    public static float KillExpMul(int wi) => wi >= 0 && wi < WeaponCount && Active((TraitsData.WeaponType)wi, 4) == 1 ? 1f + Level((TraitsData.WeaponType)wi) * T("job_exp", 0.006f) : 1f;

    // 공속 (WeaponBase.Trait 에 더해요)
    public static float AttackSpeed(TraitsData.WeaponType w) => Active(w, 0) == 0 ? Mathf.Min(0.6f, Level(w) * T("job_rapid_as", 0.006f)) : 0f;

    // ── 저장 ──
    public static void Load(GameData d)
    {
        levels = new int[WeaponCount];
        picks = NewPicks();
        if(d.jobLevels != null) for(int i = 0; i < WeaponCount && i < d.jobLevels.Count; i++) levels[i] = Mathf.Clamp(d.jobLevels[i], 0, MaxLevel);
        if(d.jobPicks != null) for(int i = 0; i < picks.Length && i < d.jobPicks.Count; i++) picks[i] = Mathf.Clamp(d.jobPicks[i], -1, 1);
        if(d.jobLevels == null) RefundOldTraits();
    }
    public static void Save(GameData d)
    {
        d.jobLevels = new List<int>(levels);
        d.jobPicks = new List<int>(picks);
    }

    // 예전 특성에 쓴 광석을 돌려주고 특성은 0으로 (한 번만: jobLevels 가 저장되면 다시 안 해요)
    static void RefundOldTraits()
    {
        var tm = TraitsManager.instance;
        if(tm == null || tm.TraitsData == null) return;
        long refund = 0;
        foreach(var t in tm.TraitsData) {
            if(t == null) continue;
            for(int k = 0; k < t.Trait_Level && k < t.totalPrice.Length; k++) refund += t.totalPrice[k];
            t.Trait_Level = 0;
            t.SetTraitData();
        }
        if(refund > 0) {
            GameManager.SetOre(refund);
            Debug.Log($"[개조] 예전 특성 광석 {refund:N0} 돌려줌");
        }
    }
}
