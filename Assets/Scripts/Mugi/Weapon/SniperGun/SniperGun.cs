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

    public override void CreateBullet()
    {
        var b = TakeBullet<SniperBullet>(PoolObjectTypeBullet.Sniper, ShootingPoint.position, transform.rotation);
        b.isCritical = true;
        b.setBulletStats(HitDamage, penetratingEffect, ExtraCritDamge);
        b.gameObject.SetActive(true);
    }

    public override void DonShoot()
    {
        base.DonShoot();
        m_createLine.m_lineRenderer.enabled = false;
        TimerAs = 0;
    }
}
