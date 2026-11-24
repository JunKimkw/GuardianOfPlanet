using UnityEngine;

// 독 비행기: 10초마다 독탄을 여러 방향으로 뿌려요 (조준과 상관없이 계속 발사)
public class PoisonPlane : WeaponBase
{
    public static PoisonPlane instance;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    [HideInInspector] public int PoisonDamage;

    public bool extraBullet;
    public bool extraHit;
    public bool extraSizeOfPoisonArea;
    public bool Poison_canCrit;

    public float TimerAs;

    static readonly int[] Angles3 = { 300, 180, 60 };
    static readonly int[] Angles4 = { 0, 180, 90, 270 };

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Poison;
    public override SideWeapon Side => SideWeapon.Poison;
    public override int BaseDamage => Balanced("poison", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        PoisonDamage = Balanced("poison", 25f);

    void Update()
    {
        TimerAs += Time.deltaTime * AugmentManager.AttackSpeedFactor * (1f + AugmentManager.Silver("pb_reload"));   // 실버 "독탄 장전" (역병 폭탄)   // 증강 공격속도(시간 가속·무거운 사슬·강철 장갑)만큼 빨리 차요 (2026-11-21 점검: 예전엔 안 먹었어요)
        if(TimerAs >= 10 * (1 - Trait(TraitsData.TraitsType.AttackSpeed))) {
            TimerAs = 0;
            long dmg = ClampDamage(HitDamage * (1.0 + AugmentManager.Silver("pb_power")));   // 실버 "맹독" (역병 폭탄)
            int pm = AugmentManager.WL("pb_more");   // 역병 살포: 독탄 +
            if(AugmentManager.Fused(AugmentDefs.FzPlague) && FireAimed((extraBullet ? 4 : 3) + (pm > 0 ? AugmentDefs.PbMore(pm) : 0), dmg)) return;   // 역병 폭탄: 적을 조준
            int spread = AugmentManager.WL("ps_spread");
            if(spread > 0) {
                // 살포 확장: 방향을 늘려 고르게 뿌려요 (180도 = 조준 방향)
                int n = (extraBullet ? 4 : 3) + spread;
                for(int i = 0; i < n; i++) CreateBullet(180 + Mathf.RoundToInt(360f * i / n), dmg);
            } else {
                foreach(int a in extraBullet ? Angles4 : Angles3)
                    CreateBullet(a, dmg);
            }
        }
    }

    public void CreateBullet(int angle) => CreateBullet(angle, HitDamage);

    // 합체 "역병 폭탄": 가까운 적들을 하나씩 노려서 쏴요.
    // 적보다 탄이 많으면 남는 탄은 그 적 주위(우주선 쪽 반원)에 둘러 떨어져요 (예전엔 한 자리에 겹쳐서 하나로 보였어요).
    // 노린 자리까지 날아가 터지고, 가다가 다른 적에 닿으면 그 전에 터져요. 화면에 적이 없으면 false (원래대로 쏴요)
    bool FireAimed(int count, long dmg)
    {
        Vector3 from = ShootingPoint.position;
        var targets = AugmentManager.NearestInRange(from, count);
        if(targets.Count == 0) return false;
        int t = targets.Count;
        float ring = Tuning.Get("plague_ring_radius", 1f);
        for(int i = 0; i < count; i++) {
            var e = targets[i % t];
            int rep = i / t;                                   // 이 적을 몇 번째로 노리는 탄인지 (0 = 적 자리)
            Vector3 aim = e.pos;
            if(rep > 0) {
                int extras = (count - 1 - i % t) / t;          // 이 적 주위로 가는 탄 수
                Vector3 toShip = from - e.pos;
                toShip.z = 0f;
                toShip = toShip.sqrMagnitude > 0.0001f ? toShip.normalized : Vector3.down;
                // 우주선 쪽 반원에 고르게 (적 뒤쪽은 가는 길에 적에 막혀서 피해요)
                float ang = extras <= 1 ? 75f : Mathf.Lerp(-75f, 75f, (rep - 1) / (float)(extras - 1));
                aim = e.pos + Quaternion.Euler(0f, 0f, ang) * toShip * ring;
            }
            Vector3 d = aim - from;
            d.z = 0f;
            if(d.sqrMagnitude < 0.0001f) d = Vector3.up * 0.01f;
            PoisonBullet b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Poison).GetComponent<PoisonBullet>();
            b.BulletDamage = dmg;
            b.transform.position = from;
            // 독탄은 자기 아래(-up)로 날아가요
            b.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, -d.x) * Mathf.Rad2Deg - 90f);
            b.burstDistance = d.magnitude;
            b.gameObject.SetActive(true);
        }
        return true;
    }

    void CreateBullet(int angle, long dmg)
    {
        PoisonBullet b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Poison).GetComponent<PoisonBullet>();
        b.BulletDamage = dmg;
        b.transform.position = ShootingPoint.position;
        b.transform.rotation = Quaternion.Euler(0, 0, angle) * transform.rotation;
        b.burstDistance = -1f;   // 원래 거리에서 터져요
        b.gameObject.SetActive(true);
    }

    public override void DonShoot() => transform.rotation = Quaternion.identity;
    public override void NormalShooting() => Face(MouseWorld);
    // 자동 모드: 가장 가까운 적 쪽으로 (예전엔 자동 모드에서도 마지막 터치 방향으로만 쐈어요)
    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        if(e != null) Face(e.pos);
    }
}
