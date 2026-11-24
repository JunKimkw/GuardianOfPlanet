using System.Collections.Generic;
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
    public override int BaseDamage => Balanced("bomber", 25f);   // 무기 밸런스 (WeaponBase.LevelCurve)

    void Awake() => instance = this;

    public override void SetDamage() =>
        Damage = Balanced("bomber", 25f);

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
            FocusVolley(ClampDamage(dmg * (1.0 + AugmentManager.Silver("gb_power"))));   // 실버 "폭약 증량"
            return;
        }
        Vector3 center = ClampToScreen(aimPoint);
        SpawnBomb(center, dmg);

        //리마스터: 연속 투하 (예전 이름 융단 폭격) - 목표 지점을 가로지르는 줄로 폭탄 추가
        int extra = AugmentManager.Instance != null ? AugmentManager.Instance.CarpetBombCount : 0;
        Vector3 dir = (center - transform.position).normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float gap = BomberBullet.BaseRadius * 1.4f;
        long extraDmg = ClampDamage(dmg * (double)AugmentDefs.CarpetDamage);   // 추가 폭탄 피해 (2026-11-22 증강 하나씩 측정 → 조정안 v3 (탄 개수는 그대로, 피해만))
        for(int i = 0; i < extra; i++) {
            int step = i / 2 + 1;
            float sign = (i % 2 == 0) ? 1f : -1f;
            SpawnBomb(ClampToScreen(center + side * (gap * step * sign)), extraDmg);
        }
    }

    // 합체 "집중 포화" (2026-11-24 사용자: 유도 폭격 → 보스 전용 새 무기 "집중 포화" (A안)):
    //  1) 표적: 가장 강한 적 하나 (보스 먼저, 그다음 체력이 많은 순). 표적이 살아 있고 화면 안이면 계속 그 적만 노려요
    //  2) 장전마다 유도 폭탄 1발(+ 집중 사격)이 표적을 쫓아가 터져요. 폭발 범위는 좁아요 (focus_radius_mul) → 무리에는 약해요
    //  3) 같은 표적을 맞힐 때마다 쌓임 +1 → 피해 + focus_stack_per(5%) × 쌓임, 최대 focus_stack_max(20)번 (+ 집중 강화) = 최대 2배
    //     표적이 바뀌면 처음부터 (실버 "조준 유지"면 일부 남겨요). 보스는 오래 살아서 끝까지 쌓이고, 졸개는 금방 죽어서 거의 안 쌓여요
    //  4) 최후의 일격(R): 최대까지 쌓이면 표적에 거대 폭발 → 쌓임 절반으로
    Enemy focusTarget;
    int focusStacks;
    public static int FocusMaxStacks {
        get {
            int l = AugmentManager.WL("fc_stack");
            return Mathf.Max(1, Tuning.GetInt("focus_stack_max", 20) + (l > 0 ? AugmentDefs.FcStack(l) : 0));
        }
    }
    public double FocusMul => 1.0 + Tuning.Get("focus_stack_per", 0.05f) * focusStacks;

    static bool AliveInRange(Enemy e) => e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy && e.IsInRange;

    void FocusVolley(long dmg)
    {
        if(!AliveInRange(focusTarget)) {
            var next = AugmentManager.StrongestInRange();
            if(next != focusTarget) {
                float keep = AugmentManager.Silver("fc_keep") * Tuning.Get("focus_keep_mul", 1.5f);   // 실버 "조준 유지": 쌓임 일부를 남겨요
                focusStacks = Mathf.FloorToInt(focusStacks * Mathf.Clamp01(keep));
            }
            focusTarget = next;
        }
        if(focusTarget == null) { SpawnBomb(ClampToScreen(aimPoint), dmg); return; }
        LockOnMark.Place(focusTarget, 1.5f);   // 빨간 표적 표식
        int count = 1;
        int salvo = AugmentManager.WL("fc_salvo");
        if(salvo > 0) count += AugmentDefs.FcSalvo(salvo);
        long hit = ClampDamage(dmg * FocusMul * Tuning.Get("focus_damage_mul", 0.12f));   // 빨리 쏘는 만큼 1발 피해는 작게
        for(int i = 0; i < count; i++) SpawnBomb(ClampToScreen(focusTarget.pos), hit, focusTarget);
    }

    // 폭탄이 표적을 맞혔을 때 (BomberBullet 이 불러요)
    public void OnFocusHit(Enemy e)
    {
        if(e == null || e != focusTarget) return;
        int max = FocusMaxStacks;
        focusStacks = Mathf.Min(max, focusStacks + 1);
        float k = focusStacks / (float)max;
        RemasterFx.Ring(e.pos, Mathf.Lerp(0.4f, 1.1f, k), Color.Lerp(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.15f, 0.15f), k), 0.2f);   // 쌓일수록 크고 빨갛게
        int fin = AugmentManager.WL("fc_finale");
        if(fin > 0 && focusStacks >= max) {   // 최후의 일격
            long big = ClampDamage(HitDamage * FocusMul * Tuning.Get("focus_damage_mul", 0.12f) * AugmentDefs.FcFinale(fin));   // 지금 폭탄 1발(작아진 것) × 배율
            RemasterFx.Ring(e.pos, 2.2f, Color.white, 0.35f);
            RemasterFx.Burst(e.pos, 1.6f, new Color(1f, 0.4f, 0.2f));
            AugmentManager.WeaponHit(e, big, RollCrit(), Player.instance.FinalCriticalDamage, HitKind.Weapon, Armor.Pen(TraitType), SideWeapon.Bomber);
            focusStacks = max / 2;
        }
    }

    // 집중 포화 상시 공격: 호위 폭격기 (2026-11-24 사용자: 집중 포화 상시 무기 = 호위 폭격기 (B안))
    //  우주선 양옆에 작은 폭격기 escort_count(2)대(+ 실버 "호위기 추가")가 따라다니며, 가까운 적에게 소형 폭탄을 escort_interval 초마다 떨어뜨려요.
    //  표적 폭격(보스)과 따로 가까운 적을 정리해요. 폭탄 1발 = 폭격기 1발 × escort_mul. 그림은 escortSprite (빨간 우주선, Bomber 프리팹에서 연결)
    public Sprite escortSprite;
    readonly System.Collections.Generic.List<Transform> escorts = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<float> escortTimers = new System.Collections.Generic.List<float>();

    protected override void Update()
    {
        base.Update();
        bool on = AugmentManager.Fused(AugmentDefs.FzGuided);
        UpdateEscorts(on, on && IsShooting);
    }

    void OnDisable() => UpdateEscorts(false, false);

    void UpdateEscorts(bool on, bool fire)
    {
        int want = on ? Mathf.Max(0, Tuning.GetInt("escort_count", 2)) + AugmentManager.WL("fc_escort") : 0;   // 실버 "호위기 추가"
        while(escorts.Count > want) {
            int i = escorts.Count - 1;
            if(escorts[i] != null) Destroy(escorts[i].gameObject);
            escorts.RemoveAt(i);
            escortTimers.RemoveAt(i);
        }
        if(want == 0) return;
        var wm = WeaponManager.instance;
        Vector3 ship = wm != null && wm.mainWeapon != null ? wm.mainWeapon.transform.position : transform.position;
        ship.z = 0f;
        while(escorts.Count < want) {
            var go = new GameObject("FocusEscort");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = escortSprite;
            sr.sortingLayerName = RemasterFx.FxLayer;
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * Tuning.Get("escort_size", 0.04f);
            go.transform.position = ship;
            escorts.Add(go.transform);
            escortTimers.Add(Random.Range(0f, 0.3f));   // 같이 쏘지 않게 조금씩 어긋나게
        }
        float dt = Time.deltaTime;
        float side = Tuning.Get("escort_side", 0.8f), back = Tuning.Get("escort_back", -0.25f);
        float interval = Mathf.Max(0.05f, Tuning.Get("escort_interval", 0.9f));
        for(int i = 0; i < escorts.Count; i++) {
            var t = escorts[i];
            if(t == null) continue;
            t.localScale = Vector3.one * Tuning.Get("escort_size", 0.04f);   // tuning 을 바꾸면 플레이 중에도 바로
            // 자리: 양옆으로 번갈아 (3대째부터는 조금 더 바깥·뒤), 살짝 떠다녀요
            int rank = i / 2;
            float sign = i % 2 == 0 ? -1f : 1f;
            Vector3 home = ship + new Vector3(sign * side * (1f + 0.5f * rank), back - 0.25f * rank + 0.12f * Mathf.Sin(Time.time * 2.2f + i), 0f);
            t.position = Vector3.Lerp(t.position, home, Mathf.Clamp01(8f * dt));
            var near = AugmentManager.NearestInRange(t.position, 1);
            Vector3 look = near.Count > 0 ? near[0].pos : t.position + Vector3.up;
            Vector3 d = look - t.position;
            if(d.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f), Mathf.Clamp01(10f * dt));
            if(!fire || near.Count == 0) continue;
            escortTimers[i] += dt * AugmentManager.AttackSpeedFactor;
            if(escortTimers[i] < interval) continue;
            escortTimers[i] = 0f;
            Vector3 from = t.position;
            Vector3 to = near[0].pos;
            to.z = 0f;
            var bomb = TakeBullet<BomberBullet>(PoolObjectTypeBullet.Bomber, from, LookRotation(from, to));
            bomb.transform.localScale *= Tuning.Get("escort_bomb_size", 0.55f);
            bomb.gameObject.SetActive(true);
            bomb.Launch(ClampDamage(HitDamage * (double)Tuning.Get("escort_mul", 0.08f) * (1.0 + AugmentManager.Silver("fc_escort_power"))), to, null, Tuning.Get("escort_bomb_size", 0.55f));
        }
    }

    // 실버 "속사 폭격" (집중 포화)
    // 집중 포화 (2026-11-24 사용자: "록온하고 폭격기 쏘는 게 너무 느려서 의미가 없어 보여"): 폭격기 장전(5초) × focus_reload_mul(0.12) ≈ 0.6초마다 퍼부어요
    //  1발 피해는 focus_damage_mul(0.12)로 줄여서 초당 피해는 예전과 비슷하게 (쌓임은 약 12초면 최대)
    public override float getAttackSpeed() => base.getAttackSpeed() / (1f + AugmentManager.Silver("gb_rapid"))
                                              * (AugmentManager.Fused(AugmentDefs.FzGuided) ? Tuning.Get("focus_reload_mul", 0.12f) : 1f);

    void SpawnBomb(Vector3 target, long dmg, Enemy homing = null)
    {
        Vector3 from = ShootingPoint.position;
        var b = TakeBullet<BomberBullet>(PoolObjectTypeBullet.Bomber, from, LookRotation(from, target));
        b.gameObject.SetActive(true);
        // 집중 포화 표적 폭탄은 크게 (focus_bomb_size, 호위기 폭탄보다 크게)
        float size = AugmentManager.Fused(AugmentDefs.FzGuided) ? Tuning.Get("focus_bomb_size", 1f) : 1f;
        if(size != 1f) b.transform.localScale *= size;
        b.Launch(dmg, target, homing, size);
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
