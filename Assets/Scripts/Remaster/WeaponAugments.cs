using System.Collections.Generic;
using UnityEngine;

// 무기 전용 증강과 무기 합체 (설계서 2-4-1, 증강_목록_초안.md 3·4장)
// - 무기 A 전용 증강 3개를 모두(★1 이상) 모으면 짝 무기 B 의 합체 카드(프리즘)가 뽑기에 나와요.
// - 카드를 고르면 A 전용 증강 3개는 사라지고, 합체 무기 전용 증강 3개가 나오기 시작해요.
// - 위력: 합체 직후 A 의 3배, 합체 전용 증강을 다 모으면 5배 (WeaponPower).
// 합체했는지는 카드 증강의 레벨(1)로 저장돼서 따로 저장할 게 없어요.
public partial class AugmentManager
{
    // 원정 중일 때만 증강 레벨 (무기 코드에서 불러요)
    public static int WL(string id)
    {
        var m = Instance;
        return m != null && m.Active ? m.Level(id) : 0;
    }

    // 합체 전용 증강 3개를 모두 ★1 이상 모았는지
    public bool AllFusionAugs(int f)
    {
        foreach(var id in AugmentDefs.Fusions[f].augs) if(Level(id) <= 0) return false;
        return true;
    }

    public bool FusionOwned(int f) => f >= 0 && Level(AugmentDefs.Fusions[f].card) > 0;

    // 무기 A 가 합체한 번호 (없으면 -1)
    public int FusionOf(SideWeapon a)
    {
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++)
            if(AugmentDefs.Fusions[i].a == a && FusionOwned(i)) return i;
        return -1;
    }

    // 원정 중이고 그 합체를 했으면 true (무기 코드에서 불러요)
    public static bool Fused(int f)
    {
        var m = Instance;
        return m != null && m.Active && m.FusionOwned(f);
    }

    // 카드가 뽑기에 나올 수 있는지: A 를 출격했고, A 전용 증강 3개를 다 모았고, A 가 아직 합체 안 했음
    bool FusionCardAvailable(AugmentDef card)
    {
        var f = AugmentDefs.FusionOfCard(card);
        if(f == null || FusionOf(f.a) >= 0) return false;
        if(!IsEquipped(f.a)) return false;
        foreach(var d in AugmentDefs.WeaponAugs(f.a))
            if(Level(d.id) <= 0) return false;
        return true;
    }

    // 뽑기 후보 거르기 (Pick 에서 불러요). false 면 이번 원정에는 안 나와요
    bool WeaponAugAllowed(AugmentDef d)
    {
        if(d.tag != AugTag.Weapon) return true;
        if(d.fusionCard) return FusionCardAvailable(d);
        if(d.ultimate) return FusionOwned(d.fusion) && AllFusionAugs(d.fusion);   // 종결: 합체 전용 3개를 다 모은 뒤
        if(d.fusion >= 0) return FusionOwned(d.fusion);     // 합체 무기 전용: 합체한 뒤에만
        return FusionOf(d.requiredWeapon) < 0;              // 합체한 무기 A 의 전용 증강은 더 안 나와요
    }

    // 합체 카드를 받았을 때 (Apply 에서 불러요)
    void DoFuse(AugmentDef card, bool announce)
    {
        var f = AugmentDefs.FusionOfCard(card);
        if(f == null) return;
        foreach(var d in AugmentDefs.WeaponAugs(f.a)) {
            levels.Remove(d.id);
            order.Remove(d.id);
        }
        OnAugmentsChanged();
        if(announce)
            ui.Toast($"무기 합체! {AugmentDefs.WeaponName(f.a)} + {AugmentDefs.WeaponName(f.b)}\n= {f.name} (위력 ×{AugmentDefs.FusionPower:0})", AugmentDefs.GradeColor(AugGrade.Prism), true);
    }

    // 무기 피해 배율: 합체 위력 + 숫자 없는 증강의 ★2 이상 보너스 (WeaponBase.HitDamage 에서 곱해요)
    public static float WeaponPower(SideWeapon w)
    {
        var m = Instance;
        if(w == SideWeapon.None || m == null || !m.Active) return 1f;
        float p = 1f;
        int f = m.FusionOf(w);
        if(f >= 0) {
            int got = 0;
            foreach(var id in AugmentDefs.Fusions[f].augs) if(m.Level(id) > 0) got++;
            p *= Mathf.Lerp(AugmentDefs.FusionPower, AugmentDefs.FusionPowerFull, got / 3f);
        }
        switch(w) {
            case SideWeapon.DoubleGun: p *= 1f + Q(m.Level("dg_cross")); break;
            case SideWeapon.Lazer: p *= 1f + Q(m.Level("lz_twin")); break;
        }
        return p;
    }

    static float Q(int L) => L > 0 ? AugmentDefs.QualityBonus(L) : 0f;

    // 무기 증강 폭발 (폭발 탄두, 관통 폭발). 연쇄 폭발·용광로 같은 폭발 증강과 이어져요
    public static void WeaponBlast(Vector3 pos, long damage, float radius)
    {
        var m = Instance;
        if(m == null || !m.Active || damage <= 0) return;
        m.explosions.Add(new PendingExplosion { pos = pos, depth = 1, damage = damage, radius = radius, time = Time.time });
    }

    // 무기 코드에서 쓰는 적 찾기 (화면 안, 살아 있는 적)
    public static List<Enemy> EnemiesNear(Vector3 pos, float radius) => EnemiesInRadius(pos, radius);

    public static List<Enemy> NearestInRange(Vector3 pos, int count, ICollection<Enemy> exclude = null, bool bossFirst = false)
    {
        var all = AllEnemies();
        all.RemoveAll(e => !e.IsInRange || (exclude != null && exclude.Contains(e)));
        all.Sort((a, b) => {
            if(bossFirst && a.IsBoss != b.IsBoss) return a.IsBoss ? -1 : 1;
            return (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude);
        });
        if(all.Count > count) all.RemoveRange(count, all.Count - count);
        return all;
    }

    // 화면 안에서 체력이 가장 많은 적 (보스 먼저)
    public static Enemy StrongestInRange()
    {
        Enemy best = null;
        foreach(var e in AllEnemies()) {
            if(!e.IsInRange) continue;
            if(best == null || (e.IsBoss && !best.IsBoss) || (e.IsBoss == best.IsBoss && e.CurrentHealth > best.CurrentHealth)) best = e;
        }
        return best;
    }

    // 무기 효과로 주는 피해 (레이저 종류로 처리: 굴절 등)
    public static void WeaponHit(Enemy e, long damage, bool crit, float critDamage, HitKind kind = HitKind.Weapon)
    {
        if(e == null || e._state == Enemy.EnemyState.dead || !e.gameObject.activeInHierarchy) return;
        var prev = CurrentHitKind;
        CurrentHitKind = kind;
        try { e.OnHit(damage, false, crit, critDamage); }
        finally { CurrentHitKind = prev; }
    }

    // ---- 개발용: 테스트 창의 "무기 고르기"
    public bool DebugPickMode => debugPickMode;
    public bool DebugPicked(SideWeapon w) => debugPickMode && debugPicked.Contains(w);

    // 없는 무기는 Lv.1 로 줘요 (Lv.1 은 저장돼요)
    void DebugEnsureOwned(SideWeapon w)
    {
        var weapon = SideWeaponOf(w);
        if(weapon != null && weapon.UpgradeLevel <= 0) { weapon.UpgradeLevel = 1; weapon.SetDamage(); }
        WeaponShopBase.DebugUnlockAll = true;
    }

    void DebugStartPick()
    {
        if(debugPickMode) return;
        debugPickMode = true;
        debugEquipAll = false;
        debugPicked.Clear();
    }

    public void DebugToggleWeapon(SideWeapon w)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStartPick();
        DebugEnsureOwned(w);
        if(!debugPicked.Remove(w)) debugPicked.Add(w);
        ui.RefreshHUD();
#endif
    }

    // 합체 무기 고르기: A 를 출격하고 바로 합체 (A 가 다른 합체 중이면 풀고 바꿔요)
    public void DebugPickFusion(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var fd = AugmentDefs.Fusions[f];
        DebugStartPick();
        DebugEnsureOwned(fd.a);
        debugPicked.Add(fd.a);
        int cur = FusionOf(fd.a);
        if(cur == f) { DebugUnfuse(f); ui.RefreshHUD(); return; }   // 다시 누르면 합체 풀기
        if(cur >= 0) DebugUnfuse(cur);
        Apply(AugmentDefs.Get(fd.card), true);
#endif
    }

    // 합체 풀기: 카드와 합체 전용 증강을 지워요
    void DebugUnfuse(int f)
    {
        var fd = AugmentDefs.Fusions[f];
        levels.Remove(fd.card); order.Remove(fd.card);
        foreach(var id in fd.augs) { levels.Remove(id); order.Remove(id); }
        levels.Remove(AugmentDefs.UltIds[f]); order.Remove(AugmentDefs.UltIds[f]);
        OnAugmentsChanged();
    }

    public void DebugPickAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStartPick();
        foreach(SideWeapon w in System.Enum.GetValues(typeof(SideWeapon)))
            if(w != SideWeapon.None) { DebugEnsureOwned(w); debugPicked.Add(w); }
        ui.RefreshHUD();
#endif
    }

    public void DebugPickClear(bool backToLoadout)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugPicked.Clear();
        if(backToLoadout) debugPickMode = false;
        ui.RefreshHUD();
#endif
    }

    public void DebugFuse(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(f < 0 || f >= AugmentDefs.Fusions.Length) return;
        var fd = AugmentDefs.Fusions[f];
        if(FusionOf(fd.a) >= 0) { ui.Toast($"[개발] {AugmentDefs.WeaponName(fd.a)}는 이미 합체했어요", Color.white); return; }
        Apply(AugmentDefs.Get(fd.card), true);
#endif
    }
}

// 궤도 위성 (위성 추가 증강, 독안개 위성). 본체 궤도를 복제해서 만들고, 피해는 본체 궤도 값으로 줘요
public class OrbitalSatellite : MonoBehaviour
{
    // 표시용이에요. 위치는 MoveInCircle, 피해는 본체 Orbital 이 위성 자리마다 판정해요 (Orbital.Update)
}

// 위성 레이저 드론 (360 레이저 + 궤도 합체). 우주선 주위를 돌며 가까운 적에게 레이저를 쏴요
public class LaserDrone : MonoBehaviour
{
    public int index, count = 1;
    float angle, fireTimer;
    SpriteRenderer sr;
    static readonly Color DroneColor = new Color(0.55f, 0.95f, 1f);

    public static LaserDrone Create(Transform parent)
    {
        var go = new GameObject("LaserDrone");
        go.transform.SetParent(parent, false);
        var d = go.AddComponent<LaserDrone>();
        d.sr = go.AddComponent<SpriteRenderer>();
        d.sr.sprite = DroneArt.Drone;   // 드론과 같은 그림 (하늘색)
        d.sr.color = DroneColor;
        d.sr.sortingLayerName = "Weapons";   // Default 레이어는 배경(Map)보다 아래라 가려졌어요
        d.sr.sortingOrder = 50;
        go.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
        return d;
    }

    void Update()
    {
        var lz = Lazer.instance;
        var wm = WeaponManager.instance;
        if(lz == null || wm == null || wm.mainWeapon == null) return;
        Vector3 ship = wm.mainWeapon.transform.position;
        angle = Mathf.Repeat(angle + Time.deltaTime * 1.6f, Mathf.PI * 2f);
        float a = angle + Mathf.PI * 2f * index / Mathf.Max(1, count);
        transform.position = ship + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.7f;

        fireTimer -= Time.deltaTime;
        if(fireTimer > 0f || wm.shootType == WeaponManager.ShootType.DontShoot) return;
        fireTimer = 0.4f;
        bool focus = AugmentManager.WL("sat_focus") > 0;
        Enemy target = focus ? AugmentManager.StrongestInRange() : ClosestTo(transform.position);
        if(target == null) return;
        float mul = 0.35f * (focus ? AugmentDefs.SatFocus(AugmentManager.WL("sat_focus")) : 1f);
        Vector2 to = target.transform.position - transform.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f);   // 쏘는 적 쪽을 봐요
        RemasterFx.Line(transform.position, target.transform.position, DroneColor);
        AugmentManager.WeaponHit(target, WeaponBase.ClampDamage(lz.HitDamage * (double)mul), Random.Range(1, 101) <= Player.instance.FinalCriticalChance + lz.ExtraCritChance,
                                 Player.instance.FinalCriticalDamage + lz.ExtraCritDamage, HitKind.Laser);
        AugmentManager.WeaponHitEvent(SideWeapon.Lazer);
    }

    static Enemy ClosestTo(Vector3 p)
    {
        var list = AugmentManager.NearestInRange(p, 1);
        return list.Count > 0 ? list[0] : null;
    }
}

// 레이저 빔 꾸미기 (굵은 광선, 넓은 빔, 2중 회전, 프리즘 빔).
// 본체 빔(LaserBim)과 판정(본체 BoxCollider2D)의 폭을 바꾸고, 추가 갈래는 본체 아래 자식으로 복제해요.
// 자식 판정은 본체 Rigidbody2D 에 붙어서 충돌이 본체 레이저 스크립트로 전달돼요.
public class BeamRig
{
    readonly Transform root, mainBeam;
    readonly BoxCollider2D box;
    readonly SpriteRenderer mainSprite;
    readonly Vector2 boxSize0;
    readonly Vector3 beamScale0;
    readonly Color color0;
    readonly List<GameObject> extras = new List<GameObject>();

    public BeamRig(Transform root, ShootLazer beam)
    {
        this.root = root;
        box = root.GetComponent<BoxCollider2D>();
        if(box != null) boxSize0 = box.size;
        if(beam != null) {
            mainBeam = beam.transform;
            beamScale0 = mainBeam.localScale;
            mainSprite = beam.lazerRenderer;
            if(mainSprite != null) color0 = mainSprite.color;
        }
    }

    // beams: 전체 갈래 수 (1 = 본체만), widthMul: 폭 배율, rainbow: 프리즘 빔 색
    public void Apply(bool shooting, int beams, float widthMul, bool rainbow)
    {
        if(box != null) box.size = new Vector2(boxSize0.x * widthMul, boxSize0.y);
        if(mainBeam != null) mainBeam.localScale = new Vector3(beamScale0.x * widthMul, beamScale0.y, beamScale0.z);
        int need = Mathf.Max(0, beams - 1);
        while(extras.Count < need && mainBeam != null) extras.Add(MakeExtra());
        float t = Time.time * 0.6f;
        if(mainSprite != null) mainSprite.color = rainbow ? Rainbow(t) : color0;
        for(int i = 0; i < extras.Count; i++) {
            var go = extras[i];
            bool on = shooting && i < need;
            if(go.activeSelf != on) go.SetActive(on);
            if(!on) continue;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 360f * (i + 1) / beams);
            var col = go.GetComponent<BoxCollider2D>();
            if(col != null && box != null) { col.size = box.size; col.offset = box.offset; }
            if(go.transform.childCount > 0) {
                var b = go.transform.GetChild(0);
                b.localScale = mainBeam.localScale;
                var sr = b.GetComponent<SpriteRenderer>();
                if(sr != null) { sr.enabled = true; sr.color = rainbow ? Rainbow(t + (i + 1f) / beams) : color0; }
            }
        }
    }

    public void Hide()
    {
        foreach(var go in extras) if(go != null && go.activeSelf) go.SetActive(false);
    }

    GameObject MakeExtra()
    {
        var go = new GameObject("ExtraBeam");
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        var col = go.AddComponent<BoxCollider2D>();
        if(box != null) { col.size = box.size; col.offset = box.offset; col.sharedMaterial = box.sharedMaterial; }
        var clone = Object.Instantiate(mainBeam.gameObject, go.transform);
        clone.transform.localPosition = mainBeam.localPosition;
        clone.transform.localRotation = Quaternion.identity;
        var sl = clone.GetComponent<ShootLazer>();
        if(sl != null) Object.Destroy(sl);
        go.SetActive(false);
        return go;
    }

    static Color Rainbow(float t) => Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.55f, 1f);
}
