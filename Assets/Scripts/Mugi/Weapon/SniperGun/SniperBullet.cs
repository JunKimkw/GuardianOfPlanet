using System.Collections.Generic;
using UnityEngine;

// 저격탄 (2026-11-20 사용자 결정: 보스 사냥꾼 + 남는 피해 이월)
// - 보스에게 피해 + (tuning.txt sniper_boss_bonus, 기본 +50%)
// - 적을 죽이고 남는 피해를 들고 뒤의 적으로 계속 뚫고 가요 (예: 2,500 탄이 600 체력 적을 죽이면 1,900 을 들고 다음 적으로).
//   남은 피해가 처음의 sniper_carry_min(5%)보다 작아지면 멈춰요. 테스트 모드(적이 안 죽음)에서도 "죽였을 피해"로 계산해요.
// - 적을 못 죽이면 멈춰요. 단, 관통 패시브(Lv.200)·철갑탄 증강은 지금처럼 그냥 뚫고 가요
public class SniperBullet : Bullet
{
    public long Bullet_Damage;
    public bool penetrateEffect;
    public float critDamage_Bullet;
    [HideInInspector] public int pierceLeft;   // 철갑탄: 더 뚫을 수 있는 적 수

    long startDamage;
    readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Sniper;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.SniperGun;

    void Update()
    {
        MoveForward();
        if(IsOffScreen()) DestroyBullet();
    }

    public void setBulletStats(long damage, bool penetrate, float critDamage)
    {
        Bullet_Damage = startDamage = damage;
        penetrateEffect = penetrate;
        critDamage_Bullet = critDamage;
        hitEnemies.Clear();
    }

    // 색 바꾸기 (2026-11-24 사용자: 특이점 저격의 작은 저격탄이 기본 저격탄과 구분이 안 돼요). null 이면 원래 색
    //  풀에서 같이 쓰는 탄이라 저격총은 쏠 때마다 원래 색으로 되돌려요
    SpriteRenderer[] tintSr;
    Color[] tintBase;
    public void Tint(Color? c)
    {
        if(tintSr == null) {
            tintSr = GetComponentsInChildren<SpriteRenderer>(true);
            tintBase = new Color[tintSr.Length];
            for(int i = 0; i < tintSr.Length; i++) tintBase[i] = tintSr[i].color;
        }
        for(int i = 0; i < tintSr.Length; i++) if(tintSr[i] != null) tintSr[i].color = c ?? tintBase[i];
    }

    // 보스 사냥꾼: 보스에게 피해 + (레일건도 같이 써요)
    public static long BossBonus(Enemy e, long damage) =>
        e != null && e.IsBoss ? WeaponBase.ClampDamage(damage * (1.0 + Tuning.Get("sniper_boss_bonus", 0.5f))) : damage;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent(out Enemy e)) {
            if(e._state == Enemy.EnemyState.dead || !hitEnemies.Add(e)) return;
            double hpBefore = e.CurrentHealth;
            e.OnHit(BossBonus(e, Bullet_Damage), isStunning, isCritical, PlayerCritDamage + critDamage_Bullet, Armor.Pen(TraitType), SideWeapon.Sniper);

            // 남는 피해 이월: 이 적을 죽이고도 남았으면 남은 비율만큼 들고 계속 가요
            double dealt = e.LastHitTotal;
            if(hpBefore > 0 && dealt > hpBefore) {
                long left = (long)(Bullet_Damage * (1.0 - hpBefore / dealt));
                if(left >= startDamage * Tuning.Get("sniper_carry_min", 0.05f)) {
                    Bullet_Damage = left;
                    return;
                }
            }
            if(penetrateEffect) return;
            if(pierceLeft > 0) { pierceLeft--; return; }
            DestroyBullet();
            return;
        }
        if(!Hit(collision.gameObject, Bullet_Damage, PlayerCritDamage + critDamage_Bullet) || penetrateEffect) return;
        if(pierceLeft > 0) { pierceLeft--; return; }
        DestroyBullet();
    }
}
