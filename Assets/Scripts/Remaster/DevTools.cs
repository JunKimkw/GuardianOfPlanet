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

    // 2026-11-22 (피해 숫자 정하기용): 무기를 바꿔 가며 재기 쉽게, 연습장에서 고른 무기(합체면 합체 이름)가 바뀌면
    //  앞 무기 측정을 Tuning/dps_runs.txt 에 한 줄로 덧붙이고(dps_run_min 초 이상 쟀을 때만) 새로 재기 시작해요.
    //  끌 때도 지금 무기 측정을 덧붙여요. dps_log.txt 는 예전처럼 최근 표만 (덮어써요)
    static string runLabel;
    static float ignoreUntil;   // 무기를 바꾼 직후 이 시각까지는 안 세요 (앞 무기 독 장판·블랙홀 등이 섞이지 않게)

    public static void ToggleDps()
    {
        if(DpsOn) AppendRun("끔");
        DpsOn = !DpsOn;
        total.Clear();
        window.Clear();
        totalStart = windowStart = Time.time;
        runLabel = DpsOn ? RunLabel() : null;
        if(DpsOn) AppendLine($"---- 측정 시작 {System.DateTime.Now:MM-dd HH:mm:ss}");
        Debug.Log(DpsOn ? "[DPS 기록] 시작 (게임 시간 기준, 무기를 바꾸면 따로 기록해요)" : "[DPS 기록] 끔");
    }

    // ---- 측정 모드가 직접 시작·끝을 정해요 (2026-11-22: 회차 반복·미리 돌리기). 결과는 Tuning/dps_measure.txt
    //  MeasureBegin: 지난 기록을 비우고 warmup 초 동안은 안 세다가 그다음부터 세요. MeasureEnd: 한 줄 남기고 꺼요. MeasureAbort: 안 남기고 꺼요
    static bool measureDriven;
    static string measureNote;

    public static void MeasureBegin(string label, string note, float warmup)
    {
        total.Clear();
        window.Clear();
        DpsOn = true;
        measureDriven = true;
        runLabel = label;
        measureNote = note;
        ignoreUntil = Time.time + warmup;
        totalStart = windowStart = ignoreUntil;
    }

    public static void MeasureEnd()
    {
        if(measureDriven) AppendRun("측정");
        MeasureAbort();
    }

    public static void MeasureAbort()
    {
        if(!measureDriven) return;
        measureDriven = false;
        DpsOn = false;
        runLabel = null;
        measureNote = null;
        total.Clear();
        window.Clear();
    }

    public static void MeasureLine(string line) => AppendLine(line, true);

    // 지금 재는 무기 이름: 연습장에서 하나만 고른 무기 (합체면 "합체 이름 (무기)"), 아니면 "여러 무기"
    static string RunLabel()
    {
        var m = AugmentManager.Instance;
        if(m == null) return "?";
        var solo = m.DebugSoloWeapon();
        if(solo == SideWeapon.None) return "여러 무기";
        string name = AugmentDefs.WeaponName(solo);
        int f = m.FusionOf(solo);
        return f >= 0 ? $"{AugmentDefs.Fusions[f].name} ({name})" : name;
    }

    static void AppendRun(string why)
    {
        float sec = Time.time - totalStart;
        if(runLabel == null) return;
        if(!measureDriven && (sec < Tuning.Get("dps_run_min", 15f) || total.Count == 0)) return;   // 측정 모드는 피해가 없어도 남겨요 (빠진 칸 찾기용)
        sec = Mathf.Max(0.01f, sec);
        var keys = new List<string>(total.Keys);
        keys.Sort((a, b) => total[b].dmg.CompareTo(total[a].dmg));
        var sb = new StringBuilder();
        sb.Append($"{System.DateTime.Now:HH:mm:ss} | {(AugmentManager.MeasureScene != null ? "[" + AugmentManager.MeasureScene + "] " : "")}{runLabel} | 적 {EnemyCount()}마리 | 적 무적 {(AugmentManager.DebugArenaInvincible ? "켬" : "끔")} | {(measureDriven ? measureNote + " | " : "")}{sec:0}초 ({why})");
        foreach(var k in keys) {
            var t = total[k];
            sb.Append($" | {k} DPS {t.dmg / sec:0.##} (1타 {t.dmg / Mathf.Max(1, t.hits):0.##}, 초당 {t.hits / sec:0.0}타)");
        }
        AppendLine(sb.ToString(), measureDriven);
    }

    // measure: 측정 모드 줄은 Tuning/dps_measure.txt, 손으로 켠 DPS 기록은 Tuning/dps_runs.txt
    static void AppendLine(string line, bool measure = false)
    {
        Debug.Log("[DPS 측정] " + line);
#if UNITY_EDITOR
        try { File.AppendAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tuning", measure ? "dps_measure.txt" : "dps_runs.txt")), line + "\n"); } catch { }
#endif
    }

    // Enemy.OnHit 에서 불러요. damage = 실제로 깎은 피해 (화면 숫자 단위)
    public static void RecordHit(double damage)
    {
        if(!DpsOn || Time.time < ignoreUntil) return;
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
        string label = measureDriven ? runLabel : RunLabel();   // 측정 모드는 측정 코드가 직접 시작·끝을 정해요
        if(label != runLabel) {   // 무기를 바꿨으면 앞 무기 측정을 남기고 새로
            AppendRun("무기 바꿈");
            total.Clear();
            window.Clear();
            ignoreUntil = Time.time + Tuning.Get("dps_switch_warmup", 3f);
            totalStart = windowStart = ignoreUntil;
            runLabel = label;
            return;
        }
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
    public static void MeasureBegin(string label, string note, float warmup) { }
    public static void MeasureEnd() { }
    public static void MeasureAbort() { }
    public static void MeasureLine(string line) { }
    public static string Screenshot() => "";
    public static float CycleSpeed() => Time.timeScale;
#endif
}
