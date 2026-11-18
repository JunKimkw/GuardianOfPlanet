using System.Collections;
using UnityEngine;

// 저격총: 장전 마지막 35% 동안 조준선을 보여 주고 쏴요 (항상 치명타)
public class SniperGun : GunWeapon
{
    public static SniperGun instance;

    [HideInInspector] public int Damage;

    [Header("#-----Laser")]
    public CreateLine m_createLine;

    [Header("#-----Shooting Point")]
    public Transform ShootingPoint;

    public float ExtraCritDamge;
    public bool penetratingEffect;
    public bool DoubleAttack;

    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.SniperGun;
    public override SideWeapon Side => SideWeapon.Sniper;

    static bool Railgun => AugmentManager.Fused(AugmentDefs.FzRailgun);
    int railShots;

    // 속사 장전 (무기 전용), 충전 가속 (레일건)
    public override float getAttackSpeed()
    {
        float t = base.getAttackSpeed();
        int f = AugmentManager.WL("sn_fast");
        if(f > 0) t *= 1f - AugmentDefs.SnFast(f);
        int c = AugmentManager.WL("rg_charge");
        if(Railgun && c > 0) t *= 1f - AugmentDefs.RgCharge(c);
        return t;
    }
    public override int BaseDamage => Damage;

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Tiered(UpgradeLevel, 20, (0, 4), (50, 5), (100, 6), (200, 6), (300, 5), (500, 3));

    protected override void Update()
    {
        if(!IsShooting) return;
        TimerAs += Time.deltaTime;

        float reload = getAttackSpeed();
        float aimStart = Mathf.Approximately(reload, MaximumAttackSpeed) ? reload : reload * 0.65f;

        if(TimerAs >= reload) {
            m_createLine.m_lineRenderer.enabled = false;
            TimerAs = 0;
            Fire();
            if(DoubleAttack) StartCoroutine(DelayedAttack());
            SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.Sniper);
        } else if(TimerAs >= aimStart) {
            m_createLine.m_lineRenderer.enabled = true;
            m_createLine.ShootLaser();
        }
    }

    // 패시브(Lv.500): 잠시 뒤 75% 확률로 한 번 더
    IEnumerator DelayedAttack()
    {
        yield return new WaitForSeconds(MaximumAttackSpeed / 2);
        if(Random.Range(0f, 100f) < 75f) CreateBullet();
    }

    float CritExtra {
        get {
            int h = AugmentManager.WL("sn_head");
            return ExtraCritDamge + (h > 0 ? AugmentDefs.SnHead(h) : 0f);   // 헤드샷
        }
    }

    public override void CreateBullet()
    {
        if(Railgun) { FireRail(); return; }
        var b = TakeBullet<SniperBullet>(PoolObjectTypeBullet.Sniper, ShootingPoint.position, transform.rotation);
        b.isCritical = true;
        b.setBulletStats(HitDamage, penetratingEffect, CritExtra);
        b.pierceLeft = AugmentManager.WL("sn_ap");   // 철갑탄
        b.gameObject.SetActive(true);
    }

    // 합체 "레일건": 화면 끝까지 꿰뚫는 빔. 초전자포면 몇 번째마다 4배 굵기·3배 피해
    void FireRail()
    {
        railShots++;
        int mega = AugmentManager.WL("rg_mega");
        bool big = mega > 0 && railShots % AugmentDefs.RgMegaEvery(mega) == 0;
        float width = 0.45f * (big ? 4f : 1f);
        long dmg = WeaponBase.ClampDamage(HitDamage * (big ? 3.0 : 1.0));
        const float len = 30f;
        Vector3 from = ShootingPoint.position;
        Vector3 dir = transform.up;
        Vector2 center = from + dir * (len / 2f);
        RemasterFx.Beam(from, from + dir * len, big ? new Color(1f, 0.9f, 0.4f) : new Color(0.5f, 0.85f, 1f), width);
        int blast = AugmentManager.WL("rg_blast");
        float critDmg = Player.instance.FinalCriticalDamage + CritExtra;
        var done = new System.Collections.Generic.HashSet<GameObject>();
        foreach(var c in Physics2D.OverlapBoxAll(center, new Vector2(width, len), transform.eulerAngles.z)) {
            if(c == null || !done.Add(c.gameObject)) continue;
            if(c.TryGetComponent(out Enemy e)) {
                if(e._state == Enemy.EnemyState.dead) continue;
                e.OnHit(dmg, RollStun(), true, critDmg);
                AugmentManager.WeaponHitEvent(SideWeapon.Sniper);   // 종결 증강 게이지
                if(blast > 0) AugmentManager.WeaponBlast(e.transform.position, WeaponBase.ClampDamage(dmg * (double)AugmentDefs.RgBlast(blast)), 0.8f);
            } else if(c.TryGetComponent(out Dummy d)) {
                d.addDamage(dmg, true, critDmg);
            }
        }
    }

    public override void DonShoot()
    {
        base.DonShoot();
        m_createLine.m_lineRenderer.enabled = false;
        TimerAs = 0;
    }
}
