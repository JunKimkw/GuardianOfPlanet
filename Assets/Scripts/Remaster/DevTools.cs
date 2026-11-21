using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// 개발용 도구 (에디터 / 개발 빌드 전용, 2026-11-19)
// - 무기별 실제 DPS 기록 (테스트 창 "DPS 기록" 버튼): 정해진 간격마다 콘솔과 Tuning/dps_log.txt 에 표를 남겨요
// - 스크린샷 (F12): 작업 폴더 previews/ 에 저장해요 (Claude 가 그림을 직접 볼 수 있게)
public static class DevTools
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ---- 무기별 DPS 기록
    public static bool DpsOn { get; private set; }

    class Stat { public double dmg; public int hits; }
    static readonly Dictionary<string, Stat> total = new Dictionary<string, Stat>();
    static readonly Dictionary<string, Stat> window = new Dictionary<string, Stat>();
    static float totalStart, windowStart;

    // 호출 스택에서 이 클래스가 보이면 그 무기의 피해로 쳐요 (탄·장판·코루틴 모두)
    static readonly Dictionary<string, string> Owners = new Dictionary<string, string> {
        { "MainBullet", "기본총" }, { "MainWeapon", "기본총" },
        { "DoubleGunBullet", "쌍발총" }, { "DoubleGun", "쌍발총" },
        { "BomberBullet", "폭격기" }, { "Bomber", "폭격기" },
        { "TrackingBullet", "유도탄" }, { "TrackingGun", "유도탄" },
        { "BounceBullet", "튕김총" }, { "BounceGun", "튕김총" }, { "CometShot", "혜성탄" },
        { "MeteorFall", "운석" }, { "MeteorGun", "운석" },
        { "SniperBullet", "저격총" }, { "SniperGun", "저격총" },
        { "PoisonArea", "독 비행기" }, { "PoisonBullet", "독 비행기" }, { "PoisonPlane", "독 비행기" },
        { "Lazer", "360 레이저" }, { "LaserDrone", "위성 레이저 드론" },
        { "strLazer", "블랙홀" }, { "BlackHole", "블랙홀" }, { "GravBombFx", "블랙홀" },
        { "Orbital", "드론" }, { "Drone", "드론" }, { "DroneBullet", "드론" },
        { "UltimateFx", "종결 증강" },
    };

    public static void ToggleDps()
    {
        DpsOn = !DpsOn;
        total.Clear();
        window.Clear();
        totalStart = windowStart = Time.time;
        Debug.Log(DpsOn ? "[DPS 기록] 시작 (게임 시간 기준)" : "[DPS 기록] 끔");
    }

    // Enemy.OnHit 에서 불러요. damage = 실제로 깎은 피해 (화면 숫자 단위)
    public static void RecordHit(double damage)
    {
        if(!DpsOn) return;
        string src = Source();
        Add(total, src, damage);
        Add(window, src, damage);
    }

    static void Add(Dictionary<string, Stat> d, string k, double dmg)
    {
        if(!d.TryGetValue(k, out var s)) d[k] = s = new Stat();
        s.dmg += dmg;
        s.hits++;
    }

    static string Source()
    {
        var st = new System.Diagnostics.StackTrace(2, false);
        string fallback = null;
        for(int i = 0; i < st.FrameCount; i++) {
            var t = st.GetFrame(i).GetMethod()?.DeclaringType;
            while(t != null && t.IsNested && t.Name.StartsWith("<")) t = t.DeclaringType;   // 코루틴·람다
            if(t == null) continue;
            if(Owners.TryGetValue(t.Name, out var owner)) return owner;
            if(fallback == null && t != typeof(Enemy) && t != typeof(DevTools) && t != typeof(AugmentManager))
                fallback = t.Name;
        }
        return "증강·기타" + (fallback != null ? $" ({fallback})" : "");
    }

    // AugmentManager.Update 에서 불러요
    public static void Tick()
    {
        if(!DpsOn) return;
        float interval = Mathf.Max(2f, Tuning.Get("dps_log_interval", 10f));
        if(Time.time - windowStart < interval) return;
        Dump(Time.time - windowStart, Time.time - totalStart);
        window.Clear();
        windowStart = Time.time;
    }

    static void Dump(float wSec, float tSec)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[DPS 기록] {System.DateTime.Now:HH:mm:ss}  게임 속도 x{Time.timeScale:0.#}  적 {EnemyCount()}마리");
        sb.AppendLine($"무기 | 최근 {wSec:0}초 DPS | 누적 {tSec:0}초 DPS | 초당 타격 | 1타 평균");
        var keys = new List<string>(total.Keys);
        keys.Sort((a, b) => total[b].dmg.CompareTo(total[a].dmg));
        double sumW = 0, sumT = 0;
        foreach(var k in keys) {
            var t = total[k];
            window.TryGetValue(k, out var w);
            double wd = w != null ? w.dmg / wSec : 0;
            sumW += wd;
            sumT += t.dmg / tSec;
            sb.AppendLine($"{k} | {BigNumber.Format(wd)} | {BigNumber.Format(t.dmg / tSec)} | {t.hits / tSec:0.0} | {BigNumber.Format(t.dmg / Mathf.Max(1, t.hits))}");
        }
        sb.AppendLine($"합계 | {BigNumber.Format(sumW)} | {BigNumber.Format(sumT)} | |");
        string text = sb.ToString();
        Debug.Log(text);
#if UNITY_EDITOR
        try { File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tuning", "dps_log.txt")), text); } catch { }
#endif
    }

    static int EnemyCount()
    {
        var em = EnemyManager.Instance;
        if(em == null) return 0;
        int n = 0;
        foreach(var e in em.enemyData) if(e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy) n++;
        foreach(var e in em.bossData) if(e != null && e._state != Enemy.EnemyState.dead && e.gameObject.activeInHierarchy) n++;
        return n;
    }

    // ---- 스크린샷 (F12)
    const string WorkFolderPreviews = @"C:\Users\kimjy\OneDrive\デスクトップ\unity_games_code\GuardianOfPlanet\previews";

    public static string Screenshot()
    {
        string dir = Directory.Exists(WorkFolderPreviews) ? WorkFolderPreviews : Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.Combine(dir, $"shot_{System.DateTime.Now:MMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[스크린샷] " + path);
        return Path.GetFileName(path);
    }

    // ---- 게임 속도 (F7 과 같아요)
    public static float CycleSpeed()
    {
        float[] speeds = { 1f, 2f, 5f, 10f, 20f };
        int i = System.Array.FindIndex(speeds, v => Mathf.Approximately(v, Time.timeScale));
        float next = speeds[(i + 1) % speeds.Length];
        GameManager.instance.setTimeScale(next);
        GameManager.instance.saveGameSpeedandAuto(next, WeaponManager.instance.shootType == WeaponManager.ShootType.AutoShoot);
        return next;
    }
#else
    public static bool DpsOn => false;
    public static void RecordHit(double damage) { }
    public static void Tick() { }
    public static void ToggleDps() { }
    public static string Screenshot() => "";
    public static float CycleSpeed() => Time.timeScale;
#endif
}
