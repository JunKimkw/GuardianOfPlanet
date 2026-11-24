using UnityEngine;

// 유도탄
public class TrackingGun : GunWeapon
{
    public static TrackingGun instance;

    [HideInInspector] public int Damage;

    [Header("#-----Weapon Info")]
    public Transform ShootingPoint;

    public float missileSpeedScale;
    public float ExtraCritDamage;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.TrackingMissile;
    public override SideWeapon Side => SideWeapon.TrackingGun;
    public override int BaseDamage => Balanced("track", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Balanced("track", 25f);

    // (Lv.500 패시브 "공격 속도"는 예전엔 장전 계산에 안 들어가서 효과가 없었어요. 이제 GunWeapon 에서 반영돼요)
    // 2026-11-21 사용자 결정: 기본 유도탄 = 록온 일제 사격 (처음 합체 "록온 미사일"로 만든 것을 기본으로 옮겼어요)
    // 합체 = 유도탄 + 쌍발총 "마이크로 미사일": 록온한 적마다 작고 빠른 미사일이 여러 발씩 연달아 (쌍발총의 연사를 미사일 떼로)
    static bool Micro => AugmentManager.Fused(AugmentDefs.FzMicro);

    // 장전: 예전 유도탄 × lockon_reload_mul (한 번에 lockon_count 발이라 그만큼 길게 → DPS 는 예전과 같아요)
    public override float getAttackSpeed() => base.getAttackSpeed() * Tuning.Get("lockon_reload_mul", 3f) / (1f + AugmentManager.Silver("mc_reload"));   // 실버 "속사 장전" (마이크로)

    protected override void Fire() => StartCoroutine(LockOnVolley());

    // 록온 일제 사격 (기본 유도탄, 2026-11-21):
    //  1) 화면의 적 lockon_count(3)마리(+ 다탄두, + 탄 추가 증강, 마이크로 미사일이면 + 다중 록온)에게 가까운 순으로 빨간 록온 표식이 "삑, 삑" 차례로 찍혀요
    //  2) 다 찍히면 우주선에서 미사일이 한꺼번에 부채꼴로 퍼져 나가 하얀 연기 꼬리를 그리며 휘어 각자 표식 찍힌 적에게 꽂혀요 (TrackingBullet.lockTarget)
    //  마이크로 미사일: 적마다 micro_per_target(5)발(+ 미사일 포드)이 micro_gap 간격으로 연달아, 작고(micro_size) 빠르게(micro_speed), 1발 피해 × micro_damage_mul
    //  적이 표식 수보다 적으면 같은 적에게 여러 발. 고폭 탄두(합체 R)면 꽂힐 때 범위 폭발
    System.Collections.IEnumerator LockOnVolley()
    {
        bool micro = Micro;
        int n = Mathf.Max(1, Tuning.GetInt("lockon_count", 3)) + AugmentManager.ExtraShots();
        int multi = AugmentManager.WL("tk_multi");
        if(multi > 0) n += AugmentDefs.TkMulti(multi);   // 다탄두 = 록온 +
        double microLockMul = 1.0;
        if(micro) {
            int more = AugmentManager.WL("lk_more");
            if(more > 0) n += AugmentDefs.LkMore(more);
            // 록온 더 (2026-11-23 사용자: 록온 적을 더 늘리고 미사일은 그만큼 더, 1발 피해는 줄이기): 록온 +micro_extra_lock, 1발 피해는 (원래 록온 수 ÷ 늘어난 록온 수) → 보스 혼자면 총 피해 그대로, 무리면 더 많은 적에게
            int extraLock = Mathf.Max(0, Tuning.GetInt("micro_extra_lock", 6));
            if(extraLock > 0) { microLockMul = n / (double)(n + extraLock); n += extraLock; }
        }
        var list = AugmentManager.NearestInRange(transform.position, 999);
        if(list.Count == 0) yield break;
        float gap = Tuning.Get("lockon_mark_gap", 0.08f);
        int microPer = 1;
        if(micro) {
            microPer = Mathf.Max(1, Tuning.GetInt("micro_per_target", 5));
            int pod0 = AugmentManager.WL("mc_pod");
            if(pod0 > 0) microPer += AugmentDefs.McPod(pod0);
        }
        var targets = new System.Collections.Generic.List<Enemy>();
        int[] shares = null;
        if(micro && Tuning.Get("micro_spread", 0f) > 0f && list.Count > n) {
            // 미사일 나눠 쏘기 (2026-11-23 사용자: 마이크로 미사일 나눠 쏘기): 총 미사일 수(록온 수 × 적마다 발 수)는 그대로,
            //  화면에 적이 많으면 가까운 적부터 더 많은 적에게 고르게 나눠요 (예: 3마리 × 5발 → 15마리 × 1발). 적이 적으면 예전처럼 몰아요
            int total = n * microPer, k = Mathf.Min(list.Count, total);
            shares = new int[k];
            for(int i = 0; i < k; i++) shares[i] = total / k + (i < total % k ? 1 : 0);
            float g = gap * n / k;   // 표식 찍는 전체 시간은 예전과 같게
            for(int i = 0; i < k; i++) {
                targets.Add(list[i]);
                LockOnMark.Place(list[i], 4f);
                if(g > 0f) yield return new WaitForSeconds(g);
            }
        } else {
            for(int i = 0; i < n; i++) {
                var e = list[i % list.Count];
                targets.Add(e);
                LockOnMark.Place(e, 4f);
                if(gap > 0f) yield return new WaitForSeconds(gap);
            }
        }
        yield return new WaitForSeconds(Tuning.Get("lockon_fire_delay", 0.12f));
        float spread = Tuning.Get("lockon_spread", 140f);
        long dmg = MissileDamage;
        int per = 1;
        float size = 1f, speed = 1f, microGap = 0f;
        if(micro) {
            per = shares != null ? shares[0] : microPer;   // 나눠 쏘기면 가장 많이 받는 적의 발 수만큼 바퀴
            dmg = WeaponBase.ClampDamage(dmg * (double)Tuning.Get("micro_damage_mul", 0.3f) * microLockMul);
            size = Tuning.Get("micro_size", 0.6f);
            speed = Tuning.Get("micro_speed", 1.3f);
            microGap = Tuning.Get("micro_gap", 0.05f);
        }
        yield return StartCoroutine(Salvo(targets, per, dmg, size, speed, micro ? 0.6f : 1f, microGap, micro ? 35f : 6f, spread, false, shares));
    }

    // 일제 발사: 표식 찍힌 적들에게 한 바퀴마다 적마다 1발씩 per 바퀴 (부채꼴 spread, 흩어짐 jitter).
    // 표식은 첫 발이 이미 찍어 둔 것(LockOnMark.Place)을 쓰고, 둘째 발부터 표식에 몫을 더해요. 종결 "전탄 발사"도 이걸 써요 (2026-11-21 사용자: 유도탄과 똑같은 미사일로 훨씬 많이)
    public System.Collections.IEnumerator Salvo(System.Collections.Generic.List<Enemy> targets, int per, long dmg, float size, float speed, float smokeMul, float gap, float jitter, float spread, bool passThrough = false, int[] shares = null)
    {
        RemasterFx.Burst(ShootingPoint.position, 0.4f, Color.white);   // 일제 발사
        for(int k = 0; k < per; k++) {
            for(int i = 0; i < targets.Count; i++) {
                if(shares != null && i < shares.Length && k >= shares[i]) continue;   // 이 적 몫은 다 쐈어요 (2026-11-23 사용자: 마이크로 미사일 나눠 쏘기)
                var e = targets[i];
                if(e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) { if(k == 0) LockOnMark.Release(e); continue; }
                if(k > 0) LockOnMark.Place(e, 4f);   // 이 적을 노리는 미사일이 하나 더 (표식은 다 꽂힐 때까지 남아요)
                float ang = targets.Count == 1 ? 0f : Mathf.Lerp(-spread / 2f, spread / 2f, i / (targets.Count - 1f));
                ang += Random.Range(-jitter, jitter);
                var b = TakeBullet<TrackingBullet>(PoolObjectTypeBullet.Tracking, ShootingPoint.position, transform.rotation * Quaternion.Euler(0f, 0f, ang));
                // (2026-11-23 사용자: "증강으로 늘어난 미사일은 꼬리가 없어") 풀이 모자라 새로 만든 미사일은 켜진 채로 나와서,
                //  록온 대상을 넣기 전에 OnEnable 이 먼저 돌아 꼬리·록온이 꺼진 채였어요 → 끄고 값을 넣은 뒤 아래에서 다시 켜요
                b.gameObject.SetActive(false);
                b.lockTarget = e;
                b.speedMul = speed;
                b.smokeMul = smokeMul;
                b.passThrough = passThrough;
                b.setBulletStats(dmg, missileSpeedScale, Player.instance.FinalCriticalDamage + ExtraCritDamage);
                b.transform.localScale *= size;
                b.gameObject.SetActive(true);
            }
            if(gap > 0f && k + 1 < per) yield return new WaitForSeconds(gap);
        }
    }

    public override void CreateBullet() => CreateBullet(0f);

    void CreateBullet(float angle)
    {
        var b = TakeBullet<TrackingBullet>(PoolObjectTypeBullet.Tracking, ShootingPoint.position, transform.rotation * Quaternion.Euler(0f, 0f, angle));
        b.lockTarget = null;
        // 치명타 피해: 예전엔 패시브 값만 넘겨서 플레이어 치명타 피해가 빠져 있었어요
        b.setBulletStats(MissileDamage, missileSpeedScale, Player.instance.FinalCriticalDamage + ExtraCritDamage);
        b.gameObject.SetActive(true);
    }

    // 미사일 1발 피해: 추적 강화 피해 + (2026-11-22 증강 하나씩 측정 → 조정안 v3 (탄 개수는 그대로, 피해만))
    long MissileDamage {
        get {
            int hl = AugmentManager.WL("tk_homing");
            double fuel = 1.0 + AugmentManager.Silver("mc_fuel");   // 실버 "추진제" (마이크로 미사일)
            return ClampDamage(HitDamage * (hl > 0 ? 1.0 + AugmentDefs.TkHomingDamage(hl) : 1.0) * fuel);
        }
    }
}
