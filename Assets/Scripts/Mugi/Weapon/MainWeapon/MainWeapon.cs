using UnityEngine;

// 기본총
public class MainWeapon : GunWeapon
{
    public static MainWeapon instance;

    [HideInInspector] public int Damage;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    public float ExtraCritChance;
    public float ExtraCritDamage;
    public bool diamondPerHit;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.MainWeapon;
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 0, (0, 1), (20, 2), (100, 3), (200, 4), (300, 3), (500, 1));

    static Vector3 mainBulletBase;
    static Vector3 MainBulletBaseScale {
        get {
            if(mainBulletBase == Vector3.zero) {
                var pf = ObjectPoolBullet.Instance != null ? ObjectPoolBullet.Instance.PrefabOf(PoolObjectTypeBullet.Main) : null;
                mainBulletBase = pf != null ? pf.transform.localScale : new Vector3(0.05f, 0.05f, 1f);
            }
            return mainBulletBase;
        }
    }

    // 기본총 룰렛 연사 (2026-11-25): 모든 무기 몫(AttackSpeedFactor)은 이미 들어가서 나머지만
    public override float getAttackSpeed() => base.getAttackSpeed() / AugmentManager.RouletteMainSpeedExtra;

    // 기본총 룰렛 (2026-11-25): 탄 +1(나란히 rl_gap 칸) · 관통 · 탄 크기 · 치명타
    //  (사용자: 부채꼴로 갈라지면 적을 못 맞히고, 처음엔 두 발이 겹쳐서 큰 탄처럼 보였어요 → 같은 방향으로 옆으로 나란히)
    public override void CreateBullet()
    {
        int n = 1 + AugmentManager.RouletteShots;
        float gap = Tuning.Get("rl_gap", 0.28f);
        float crit = ExtraCritChance + AugmentManager.RouletteMainCritExtra;
        float size = AugmentManager.RouletteSize;
        for(int i = 0; i < n; i++) {
            Vector3 pos = ShootingPoint.position + transform.right * ((i - (n - 1) * 0.5f) * gap);
            var b = TakeBullet<MainBullet>(PoolObjectTypeBullet.Main, pos, transform.rotation, crit);
            b.setBulletStats(HitDamage, Player.instance.FinalCriticalDamage + ExtraCritDamage);
            b.pierceLeft = AugmentManager.RoulettePierce;
            // 크기를 프리팹 기준으로 직접 정해요 (2026-11-25 사용자: 탄이 여러 발이면 가끔 오른쪽 탄이 더 컸어요. 풀에 남은 크기에 기대지 않게)
            b.transform.localScale = MainBulletBaseScale * ((AugmentManager.Instance != null ? AugmentManager.Instance.BulletScaleMultiplier : 1f) * size);
            b.gameObject.SetActive(true);
        }
    }
}
