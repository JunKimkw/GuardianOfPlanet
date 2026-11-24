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
    public override int BaseDamage => Balanced("sniper", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Balanced("sniper", 25f);

    protected override void Update()
    {
        if(!IsShooting) return;
        if(Railgun) RailBolts();
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

    // 레일건 상시 번개 (2026-11-24 사용자: 신 제우스의 무기처럼 주변에 번개를 상시로)
    //  rail_bolt_interval 초마다 우주선에서 rail_bolt_range 칸 안의 가까운 적 rail_bolt_targets 마리에게 지그재그 번개 (1발 × rail_bolt_mul)
    float boltTimer;
    void RailBolts()
    {
        boltTimer += Time.deltaTime * AugmentManager.AttackSpeedFactor;
        float boost = AugmentManager.Silver("rg_static");   // 실버 "정전기": 번개 피해 + · 간격 -
        if(boltTimer < Mathf.Max(0.05f, Tuning.Get("rail_bolt_interval", 0.5f) / (1f + boost))) return;
        Vector3 o = ShootingPoint.position;
        o.z = 0f;
        float range = Tuning.Get("rail_bolt_range", 4f);
        int bolts = Mathf.Max(1, Tuning.GetInt("rail_bolt_targets", 3));
        int more = AugmentManager.WL("rg_bolt");
        if(more > 0) bolts += AugmentDefs.RgBolts(more);   // 실버 "번개 추가" (2026-11-24 사용자: 증강으로 번개를 더 늘리게, 실버로 따로)
        var near = AugmentManager.NearestInRange(o, bolts);
        if(near.Count == 0 || ((Vector2)(near[0].pos - o)).magnitude > range) return;   // 주변에 적이 없으면 기다려요
        boltTimer = 0f;
        long dmg = ClampDamage(HitDamage * (double)Tuning.Get("rail_bolt_mul", 0.15f) * (1.0 + boost));
        var cyan = new Color(0.45f, 0.9f, 1f);
        foreach(var e in near) {
            Vector3 p = e.pos;
            p.z = 0f;
            if((p - o).magnitude > range) break;
            Bolt(o, p, cyan);
            RemasterFx.Burst(p, 0.3f, Color.white);
            AugmentManager.WeaponHit(e, dmg, RollCrit(), Player.instance.FinalCriticalDamage + CritExtra, HitKind.Weapon, Armor.Pen(TraitType), SideWeapon.Sniper);
        }
    }

    // 지그재그 번개 한 줄기 (종결 "천공" 충전 번개와 같은 그림)
    static void Bolt(Vector3 a, Vector3 b, Color c)
    {
        Vector3 d = b - a;
        Vector3 side = new Vector3(-d.y, d.x, 0f).normalized;
        int seg = Mathf.Clamp(Mathf.RoundToInt(d.magnitude / 0.6f), 2, 6);
        Vector3 prev = a;
        for(int i = 1; i <= seg; i++) {
            Vector3 p = i == seg ? b : a + d * (i / (float)seg) + side * Random.Range(-0.35f, 0.35f);
            RemasterFx.Line(prev, p, i % 2 == 0 ? Color.white : c);
            prev = p;
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
        b.Tint(null);   // 특이점 저격이 보라색으로 바꿔 둔 탄일 수 있어요
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
        // 연출 (2026-11-21): 빛무리 + 하얀 심 + 총구 번쩍 + 맞은 적마다 불꽃 (예전엔 가는 빔 한 줄). 판정은 그대로
        Color glowC = big ? new Color(1f, 0.85f, 0.35f) : new Color(0.45f, 0.85f, 1f);
        RemasterFx.Beam(from, from + dir * len, glowC, width * 1.2f);
        RemasterFx.Beam(from, from + dir * len, Color.white, width * 0.4f);
        RemasterFx.Burst(from + dir * 0.3f, width * (big ? 2f : 1.2f), Color.white);
        if(big) RemasterFx.Ring(from, 2f, glowC, 0.3f);
        int blast = AugmentManager.WL("rg_blast");
        float critDmg = Player.instance.FinalCriticalDamage + CritExtra;
        var done = new System.Collections.Generic.HashSet<GameObject>();
        foreach(var c in Physics2D.OverlapBoxAll(center, new Vector2(width, len), transform.eulerAngles.z)) {
            if(c == null || !done.Add(c.gameObject)) continue;
            if(c.TryGetComponent(out Enemy e)) {
                if(e._state == Enemy.EnemyState.dead) continue;
                RemasterFx.Burst(e.transform.position, 0.35f, Color.white);   // 맞은 자리 불꽃
                e.OnHit(SniperBullet.BossBonus(e, dmg), RollStun(), true, critDmg, Armor.Pen(TraitType), SideWeapon.Sniper);   // 보스 사냥꾼 (저격총과 같아요) + 합체 보스 배율 (2026-11-24)
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
