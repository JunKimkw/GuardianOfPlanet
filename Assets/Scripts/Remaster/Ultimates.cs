using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 합체 무기 종결 증강 (2026-11-18 사용자 결정)
// - 합체 전용 증강 3개를 다 모으면 종결 증강(프리즘 ★1)이 뽑기에 나와요.
// - 그 무기가 싸우는 동안(최근 4초 안에 적을 맞힘) 게이지가 차고, 가득 차면(약 30초) 자동 발동.
// - 그 무기가 맞힐 때 1% 확률로 게이지 없이 바로 발동 (1초에 한 번만 굴려요).
// 연출은 그림 없이 코드로 그린 임시 연출이에요 (그림은 P7).
public partial class AugmentManager
{
    readonly float[] ultGauge = new float[8];
    readonly float[] ultLastHit = new float[8];
    readonly float[] ultNextRoll = new float[8];
    readonly bool[] ultBusy = new bool[8];

    void ResetUltimates()
    {
        System.Array.Clear(ultGauge, 0, ultGauge.Length);
        System.Array.Clear(ultBusy, 0, ultBusy.Length);
        for(int i = 0; i < ultLastHit.Length; i++) ultLastHit[i] = -99f;
    }

    bool UltOwned(int f) => f >= 0 && FusionOwned(f) && Level(AugmentDefs.UltIds[f]) > 0;

    // 게이지 (0~1). UI 에서 쓸 수 있게 열어 둬요
    public float UltGauge(int f) => f >= 0 && f < ultGauge.Length ? ultGauge[f] : 0f;

    // 무기가 적을 맞혔을 때 (무기 코드에서 불러요). 합체 무기 A 의 종결 증강 게이지·즉시 발동
    public static void WeaponHitEvent(SideWeapon a)
    {
        var m = Instance;
        if(m == null || !m.Active) return;
        int f = m.FusionOf(a);
        if(!m.UltOwned(f)) return;
        float now = Time.time;
        m.ultLastHit[f] = now;
        if(now < m.ultNextRoll[f]) return;
        m.ultNextRoll[f] = now + 1f;   // 1초에 한 번만 굴려요 (계속 때리는 무기도 "가끔 대박"이 되게)
        if(Random.value < Tuning.Get("ult_instant_chance", 0.01f)) m.TriggerUltimate(f, true);
    }

    void TickUltimates(float dt)
    {
        float fill = 1f / Mathf.Max(1f, Tuning.Get("ult_charge_seconds", 30f));
        for(int f = 0; f < AugmentDefs.Fusions.Length; f++) {
            if(!UltOwned(f) || ultBusy[f]) continue;
            if(Time.time - ultLastHit[f] > 4f) continue;   // 싸우는 중일 때만 차요
            ultGauge[f] += dt * fill;
            if(ultGauge[f] >= 1f) TriggerUltimate(f, false);
        }
    }

    void TriggerUltimate(int f, bool lucky)
    {
        if(ultBusy[f]) return;
        var weapon = SideWeaponOf(AugmentDefs.Fusions[f].a);
        if(weapon == null) return;
        ultGauge[f] = 0f;
        ultBusy[f] = true;
        ui.Toast($"{(lucky ? "대박! " : "")}종결: {AugmentDefs.UltNames[f]}", AugmentDefs.GradeColor(AugGrade.Prism), true);
        UltimateFx.Play(f, weapon.HitDamage, () => ultBusy[f] = false);
    }

    public void DebugTriggerUltimate(int f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(!FusionOwned(f)) { ui.Toast("[개발] 먼저 합체해 주세요", Color.white); return; }
        ultBusy[f] = false;
        TriggerUltimate(f, false);
#endif
    }
}

// 종결 증강 연출 + 피해 (코드로 그린 임시 연출)
public class UltimateFx : MonoBehaviour
{
    static UltimateFx inst;

    static UltimateFx Get()
    {
        if(inst == null) inst = new GameObject("UltimateFx").AddComponent<UltimateFx>();
        return inst;
    }

    public static void Play(int f, long dmg, System.Action done)
    {
        var fx = Get();
        fx.StartCoroutine(fx.Run(f, dmg, done));
    }

    IEnumerator Run(int f, long dmg, System.Action done)
    {
        IEnumerator body;
        switch(f) {
            case AugmentDefs.FzGuided: body = CarpetRaid(dmg); break;
            case AugmentDefs.FzSatLaser: body = OrbitalStrike(dmg); break;
            case AugmentDefs.FzRailgun: body = Piercing(dmg); break;
            case AugmentDefs.FzPlague: body = PlagueRain(dmg); break;
            case AugmentDefs.FzSplitDbl: body = StarShower(dmg); break;
            case AugmentDefs.FzHound: body = HoundPack(dmg); break;
            case AugmentDefs.FzToxSat: body = ToxicVeil(dmg); break;
            default: body = RainbowJudgment(dmg); break;
        }
        yield return StartCoroutine(body);
        done?.Invoke();
    }

    // ---- 공통
    static float HalfW => GameManager.instance.mainCamera.orthographicSize * GameManager.instance.mainCamera.aspect;
    static float HalfH => GameManager.instance.mainCamera.orthographicSize;
    static Vector3 Ship => WeaponManager.instance != null && WeaponManager.instance.mainWeapon != null ? WeaponManager.instance.mainWeapon.transform.position : Vector3.zero;
    static long Mul(long dmg, double m) => WeaponBase.ClampDamage(dmg * m);
    static float M(string key, float def) => Tuning.Get(key, def);

    static void Hit(Enemy e, long dmg)
    {
        bool crit = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
        AugmentManager.WeaponHit(e, dmg, crit, Player.instance.FinalCriticalDamage);
    }

    static void Boom(Vector3 p, float r, long dmg, Color c)
    {
        RemasterFx.Burst(p, r, c);
        foreach(var e in AugmentManager.EnemiesNear(p, r)) Hit(e, dmg);
    }

    static SpriteRenderer Shape(Color c, Vector2 size, int order = 520)
    {
        var go = new GameObject("ultShape");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RemasterFx.CircleSprite;
        sr.color = c;
        sr.sortingLayerName = RemasterFx.FxLayer;
        sr.sortingOrder = order;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return sr;
    }

    // 0 융단 공습: 폭격기 편대가 화면을 가로지르며 줄줄이 폭탄
    IEnumerator CarpetRaid(long dmg)
    {
        float w = HalfW, h = HalfH;
        const int rows = 7;
        float dur = 2f;
        long bomb = Mul(dmg, M("ult_guided_mul", 1f));
        var planes = new List<SpriteRenderer>();
        var ys = new float[rows];
        for(int r = 0; r < rows; r++) {
            ys[r] = Mathf.Lerp(h - 1f, -h + 1f, r / (rows - 1f));
            planes.Add(Shape(new Color(0.75f, 0.8f, 0.9f), new Vector2(0.9f, 0.35f)));
        }
        float t = 0f, nextDrop = 0f;
        while(t < dur) {
            t += Time.deltaTime;
            for(int r = 0; r < rows; r++) {
                float x = Mathf.Lerp(-w - 1.5f, w + 1.5f, t / dur) - (r % 2) * 0.6f;
                planes[r].transform.position = new Vector3(x, ys[r], 0f);
            }
            if(t >= nextDrop) {
                nextDrop += 0.12f;
                for(int r = 0; r < rows; r++) {
                    Vector3 p = planes[r].transform.position + Vector3.down * 0.3f;
                    if(Mathf.Abs(p.x) <= w) Boom(p, 1.1f, bomb, new Color(1f, 0.55f, 0.25f));
                }
            }
            yield return null;
        }
        foreach(var p in planes) Destroy(p.gameObject);
    }

    // 1 궤도 포격: 거대 레이저가 화면을 위에서 아래로
    IEnumerator OrbitalStrike(long dmg)
    {
        float w = HalfW, h = HalfH;
        float dur = 1.5f, t = 0f, nextDraw = 0f;
        long hit = Mul(dmg, M("ult_satlaser_mul", 6f));
        var done = new HashSet<Enemy>();
        var color = new Color(0.55f, 0.95f, 1f);
        while(t < dur) {
            t += Time.deltaTime;
            float y = Mathf.Lerp(h + 0.5f, -h - 0.5f, t / dur);
            if(t >= nextDraw) {
                nextDraw += 0.05f;
                RemasterFx.Beam(new Vector3(-w - 1f, y), new Vector3(w + 1f, y), color, 0.9f);
            }
            foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f))
                if(Mathf.Abs(e.transform.position.y - y) < 0.55f && done.Add(e)) Hit(e, hit);
            yield return null;
        }
    }

    // 2 천공: 우주선에서 사방으로 거대 빔이 차례로
    IEnumerator Piercing(long dmg)
    {
        const int beams = 12;
        long hit = Mul(dmg, M("ult_railgun_mul", 4f));
        float start = Random.Range(0f, 360f);
        for(int i = 0; i < beams; i++) {
            float ang = start + 360f * i / beams;
            Vector3 dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
            Vector3 from = Ship;
            const float len = 30f, width = 0.9f;
            RemasterFx.Beam(from, from + dir * len, Color.HSVToRGB(0.55f, 0.4f, 1f), width);
            var seen = new HashSet<GameObject>();
            foreach(var c in Physics2D.OverlapBoxAll(from + dir * (len / 2f), new Vector2(width, len), ang)) {
                if(c == null || !seen.Add(c.gameObject)) continue;
                if(c.TryGetComponent(out Enemy e)) Hit(e, hit);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // 3 역병의 비: 화면 전체 독 10중첩 + 독비 + 곳곳에 독 지대
    IEnumerator PlagueRain(long dmg)
    {
        float w = HalfW, h = HalfH;
        var green = new Color(0.5f, 0.95f, 0.35f);
        foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) {
            e.AddPoison(10, 6f);
            Hit(e, Mul(dmg, M("ult_plague_mul", 2f)));
        }
        var spots = AugmentManager.NearestInRange(Ship, 6);
        foreach(var e in spots) {
            var a = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.PoisonArea).GetComponent<PoisonArea>();
            a.poisonDamage = dmg;
            a.spreadLeft = 0;
            a.slowAmount = PoisonBullet.PlagueSlow;
            a.transform.position = e.transform.position;
            a.SetScaleMul(1.3f * AugmentManager.PoisonAreaScale);
            a.gameObject.SetActive(true);
        }
        for(int i = 0; i < 40; i++) {
            RemasterFx.Burst(new Vector3(Random.Range(-w, w), Random.Range(-h, h)), 0.35f, green);
            yield return new WaitForSeconds(0.04f);
        }
    }

    // 4 별똥별 폭풍: 하늘에서 쌍발총 탄이 쏟아지고 맞으면 갈라져요
    IEnumerator StarShower(long dmg)
    {
        float w = HalfW, h = HalfH;
        long hit = Mul(dmg, M("ult_splitdbl_mul", 1f));
        for(int i = 0; i < 40; i++) {
            var b = ObjectPoolBullet.Instance.GetPoolObject(PoolObjectTypeBullet.Double).GetComponent<DoubleGunBullet>();
            b.isCritical = Random.Range(1, 101) <= Player.instance.FinalCriticalChance;
            b.isStunning = false;
            b.transform.position = new Vector3(Random.Range(-w, w), h + 0.5f, 0f);
            b.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            b.setBulletStats(hit, 0f);
            b.splitGen = 0;
            b.rootSpeedScale = 0f;
            b.ignore = null;
            b.SetSplitLook();
            b.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.05f);
        }
    }

    // 5 사냥개 무리: 개 떼가 화면을 달려가며 지나가는 적을 물고 잠깐 기절
    IEnumerator HoundPack(long dmg)
    {
        float w = HalfW, h = HalfH;
        const int dogs = 6;
        float dur = 1.4f, t = 0f, nextTrail = 0f;
        long bite = Mul(dmg, M("ult_hound_mul", 3f));
        var bodies = new List<SpriteRenderer>();
        var heads = new List<SpriteRenderer>();
        var ys = new float[dogs];
        var bitten = new List<HashSet<Enemy>>();
        var fur = new Color(0.85f, 0.55f, 0.3f);
        for(int i = 0; i < dogs; i++) {
            ys[i] = Mathf.Lerp(h - 1.2f, -h + 1.2f, i / (dogs - 1f));
            bodies.Add(Shape(fur, new Vector2(0.9f, 0.45f)));
            heads.Add(Shape(fur * 1.1f, new Vector2(0.45f, 0.45f), 521));
            bitten.Add(new HashSet<Enemy>());
        }
        while(t < dur) {
            t += Time.deltaTime;
            for(int i = 0; i < dogs; i++) {
                bool right = i % 2 == 0;   // 번갈아 반대 방향으로 달려요
                float k = Mathf.Clamp01(t / dur - (i % 3) * 0.05f);
                float x = right ? Mathf.Lerp(-w - 1.5f, w + 1.5f, k) : Mathf.Lerp(w + 1.5f, -w - 1.5f, k);
                float bob = Mathf.Abs(Mathf.Sin(t * 18f + i)) * 0.15f;   // 뛰는 느낌
                var p = new Vector3(x, ys[i] + bob, 0f);
                bodies[i].transform.position = p;
                heads[i].transform.position = p + new Vector3(right ? 0.5f : -0.5f, 0.15f, 0f);
                foreach(var e in AugmentManager.EnemiesNear(p, 0.9f)) {
                    if(!bitten[i].Add(e)) continue;
                    Hit(e, bite);
                    if(e._state != Enemy.EnemyState.dead) e.Stun(0.5f);
                    RemasterFx.Burst(e.transform.position, 0.3f, new Color(1f, 0.4f, 0.3f));
                }
            }
            if(t >= nextTrail) {
                nextTrail += 0.08f;
                foreach(var b in bodies) RemasterFx.Burst(b.transform.position, 0.2f, new Color(1f, 0.8f, 0.5f));
            }
            yield return null;
        }
        foreach(var b in bodies) Destroy(b.gameObject);
        foreach(var b in heads) Destroy(b.gameObject);
    }

    // 6 독의 장막: 우주선에서 독의 고리가 화면 끝까지 퍼져요
    IEnumerator ToxicVeil(long dmg)
    {
        float maxR = Mathf.Sqrt(HalfW * HalfW + HalfH * HalfH) + 1f;
        float dur = 1.5f, t = 0f, nextRing = 0f;
        long hit = Mul(dmg, M("ult_toxsat_mul", 3f));
        var done = new HashSet<Enemy>();
        var green = new Color(0.55f, 1f, 0.45f);
        Vector3 c = Ship;
        while(t < dur) {
            t += Time.deltaTime;
            float r = Mathf.Lerp(0.5f, maxR, t / dur);
            if(t >= nextRing) {
                nextRing += 0.06f;
                RemasterFx.Ring(c, r, green, 0.2f);
            }
            foreach(var e in AugmentManager.EnemiesNear(c, r)) {
                if(!done.Add(e)) continue;
                e.AddPoison(6, 5f);
                Hit(e, hit);
            }
            yield return null;
        }
    }

    // 7 무지개 심판: 화면 전체가 무지개빛으로 3번 번쩍
    IEnumerator RainbowJudgment(long dmg)
    {
        float w = HalfW, h = HalfH;
        long hit = Mul(dmg, M("ult_prism_mul", 3f));
        for(int flash = 0; flash < 3; flash++) {
            float hue = Random.value;
            RemasterFx.Burst(Ship, Mathf.Max(w, h) * 1.6f, Color.HSVToRGB(hue, 0.5f, 1f));
            for(int i = 0; i < 8; i++)
                RemasterFx.Burst(new Vector3(Random.Range(-w, w), Random.Range(-h, h)), 1.2f, Color.HSVToRGB(Mathf.Repeat(hue + i / 8f, 1f), 0.6f, 1f));
            foreach(var e in AugmentManager.EnemiesNear(Vector3.zero, 999f)) Hit(e, hit);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
