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
    readonly HashSet<GameObject> hitOthers = new HashSet<GameObject>();   // 더미 (훑기와 충돌이 같은 더미를 두 번 때리지 않게)

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Sniper;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.SniperGun;

    void Update()
    {
        Vector3 from = transform.position;
        MoveForward();
        Sweep(from, transform.position);
        if(!gameObject.activeSelf) return;   // 훑다가 멈췄으면 이미 풀로 돌아갔어요
        if(IsOffScreen()) DestroyBullet();
    }

    // 지나간 길 훑기 (2026-11-25 "저격총이 가끔 안 쏘는" 버그)
    //  저격탄은 작고(0.2 × 0.4칸) 빨라서(초당 20칸 + 탄속) 프레임마다 순간이동하면 게임 속도 ×2 이상·낮은 프레임에서
    //  한 프레임에 1칸 넘게 건너뛰어 작은 적을 그냥 통과했어요. 멈춘 적이면 매번 같은 길이라 한 발도 안 맞기도 했어요.
    //  이제 이번 프레임에 지나간 길에 닿은 적을 가까운 순서로 직접 맞혀요 (충돌 알림이 와도 hitEnemies 로 한 번만)
    static readonly RaycastHit2D[] sweepBuf = new RaycastHit2D[32];
    static readonly IComparer<RaycastHit2D> byDistance = Comparer<RaycastHit2D>.Create((a, b) => a.distance.CompareTo(b.distance));
    BoxCollider2D box;
    void Sweep(Vector3 a, Vector3 b)
    {
        Vector2 d = b - a;
        float dist = d.magnitude;
        if(dist < 0.0001f) return;
        if(box == null) box = GetComponent<BoxCollider2D>();
        float radius = 0.5f * (box != null ? box.size.x * Mathf.Abs(transform.lossyScale.x) : 0.2f);
        var filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;   // 충돌 알림과 같게: 트리거는 빼요
        int n = Physics2D.CircleCast(a, radius, d / dist, filter, sweepBuf, dist);
        if(n <= 0) return;
        if(n > 1) System.Array.Sort(sweepBuf, 0, n, byDistance);
        for(int i = 0; i < n; i++) {
            if(!gameObject.activeSelf) return;
            var c = sweepBuf[i].collider;
            if(c == null || c.gameObject == gameObject) continue;
            HitObject(c.gameObject);
        }
    }

    public void setBulletStats(long damage, bool penetrate, float critDamage)
    {
        Bullet_Damage = startDamage = damage;
        penetrateEffect = penetrate;
        critDamage_Bullet = critDamage;
        hitEnemies.Clear();
        hitOthers.Clear();
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

    void OnCollisionEnter2D(Collision2D collision) => HitObject(collision.gameObject);

    void HitObject(GameObject go)
    {
        if(go.TryGetComponent(out Enemy e)) {
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
        if(!go.TryGetComponent(out Dummy _) || !hitOthers.Add(go)) return;   // 적·더미가 아니면 무시 (훑기에 다른 충돌체도 걸려요)
        if(!Hit(go, Bullet_Damage, PlayerCritDamage + critDamage_Bullet) || penetrateEffect) return;
        if(pierceLeft > 0) { pierceLeft--; return; }
        DestroyBullet();
    }
}
