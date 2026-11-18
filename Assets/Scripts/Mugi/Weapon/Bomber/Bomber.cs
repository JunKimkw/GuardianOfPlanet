using UnityEngine;

// 폭격기: 적이 가장 많이 모인 곳으로 폭탄을 던져요
public class Bomber : GunWeapon
{
    public static Bomber instance;

    [HideInInspector] public int Damage;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;
    public Quaternion rotate;

    public bool ExploseRangeIncrease;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Bomber;
    public override SideWeapon Side => SideWeapon.Bomber;
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 100, (0, 4), (20, 6), (100, 7), (200, 6), (300, 5), (500, 3));

    // 폭탄 목표: 적이 가장 많이 모인 곳 (없으면 마우스/가까운 적). 화면 안으로 맞추고, 적이 다가오는 만큼 조금 앞을 노려요
    Vector3 aimPoint;
    Enemy aimEnemy;

    Vector3 LeadTarget(Enemy e)
    {
        Vector3 ship = transform.position;
        Vector3 p = e.pos;
        float dist = Vector3.Distance(ship, p);
        float eta = dist / Mathf.Max(0.5f, BomberBullet.FlySpeed);                       // 폭탄이 도착할 때까지 걸리는 시간 (탄속)
        float lead = Mathf.Min(dist * 0.8f, e.enemyMovingSpeed * eta);                    // 그동안 적이 다가오는 만큼 앞을 노려요
        return Vector3.MoveTowards(p, ship, lead);
    }

    static Vector3 ClampToScreen(Vector3 p)
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize - 0.6f, w = cam.orthographicSize * cam.aspect - 0.6f;
        return new Vector3(Mathf.Clamp(p.x, -w, w), Mathf.Clamp(p.y, -h, h), 0f);
    }

    public override void CreateBullet()
    {
        long dmg = HitDamage;
        if(AugmentManager.Fused(AugmentDefs.FzGuided)) {
            GuidedVolley(dmg);
            return;
        }
        Vector3 center = ClampToScreen(aimPoint);
        SpawnBomb(center, dmg);

        //리마스터: 융단 폭격 - 목표 지점을 가로지르는 줄로 폭탄 추가
        int extra = AugmentManager.Instance != null ? AugmentManager.Instance.CarpetBombCount : 0;
        Vector3 dir = (center - transform.position).normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float gap = BomberBullet.BaseRadius * 1.4f;
        for(int i = 0; i < extra; i++) {
            int step = i / 2 + 1;
            float sign = (i % 2 == 0) ? 1f : -1f;
            SpawnBomb(ClampToScreen(center + side * (gap * step * sign)), dmg);
        }
    }

    // 합체 "유도 폭격": 폭탄이 적을 쫓아가요. 미사일 추가 / 폭격 편대면 서로 다른 적에게 여러 발
    void GuidedVolley(long dmg)
    {
        int count = 1;
        int ml = AugmentManager.WL("gb_missile"), fl = AugmentManager.WL("gb_fleet");
        if(ml > 0) count += AugmentDefs.GbMissile(ml);
        if(fl > 0) count = Mathf.Max(count, AugmentDefs.GbFleet(fl));
        var targets = AugmentManager.NearestInRange(transform.position, count);
        if(aimEnemy != null && aimEnemy.IsInRange && !targets.Contains(aimEnemy)) {
            targets.Insert(0, aimEnemy);
            if(targets.Count > count) targets.RemoveAt(targets.Count - 1);
        }
        if(targets.Count == 0) { SpawnBomb(ClampToScreen(aimPoint), dmg); return; }
        for(int i = 0; i < count; i++) {
            var e = targets[i % targets.Count];
            SpawnBomb(ClampToScreen(e.pos), dmg, e);
        }
    }

    void SpawnBomb(Vector3 target, long dmg, Enemy homing = null)
    {
        Vector3 from = ShootingPoint.position;
        var b = TakeBullet<BomberBullet>(PoolObjectTypeBullet.Bomber, from, LookRotation(from, target));
        b.gameObject.SetActive(true);
        b.Launch(dmg, target, homing);
    }

    void Aim(Vector3 fallback)
    {
        // 화면 안의 적만 노려요 (예전엔 화면 밖 무리를 골라 화면 끝 빈 곳에서 터졌어요)
        Enemy crowd = EnemyManager.Instance.FindCrowdedEnemy();
        if(crowd != null && !crowd.IsInRange) crowd = EnemyManager.Instance.FindCloseEnemy();
        aimEnemy = crowd;
        aimPoint = crowd != null ? LeadTarget(crowd) : fallback;
        rotate = LookRotation(transform.position, aimPoint);
    }

    public override void NormalShooting()
    {
        Vector3 mouse = MouseWorld;
        Face(mouse);
        IsShooting = true;
        Aim(new Vector3(mouse.x, mouse.y, 0f));
    }

    public override void AutoShooting()
    {
        Enemy e = EnemyManager.Instance.FindCloseEnemy();
        if(e == null) {
            Face(Vector3.zero);
            IsShooting = false;
            return;
        }
        Face(e.pos);
        IsShooting = true;
        Aim(LeadTarget(e));   // 화면 안에 적이 있을 때만 쏴요 (FindCloseEnemy 는 화면 안만 찾아요)
    }
}
