using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Globalization;

public class GameManager : MonoBehaviour, IDataPersistence
{
    public static GameManager instance;

    [Header("카메라")]
    public Camera mainCamera;

    [Header("스타트")]
    public GameObject StartScreen;

    [Header("Ingame Money")]
    public long money;
    public int Diamond;
    public long Ore;
    public long Parts;

    public long maxMoney;

    [Header("Game Information")]
    public float totalPlayedTime;

    [Header("GameSpeed")]
    public bool GameSpeedBought;

    [Header("#---Wifi Error")]
    public GameObject WifiErrorTab;

    [Header("#----Quit Tab")]
    public GameObject QuitTab;

    [Header("#----Real time")]
    public GameObject RealTimeErrorTab;
    // 기기 시간 조작 검사: 서버 응답의 Date 헤더(UTC)와 기기 시간을 비교해요.
    // 서버에 하나도 접속하지 못하면 검사를 건너뛰어요(게임을 멈추지 않음).
    static readonly string[] TIME_CHECK_URLS = { "https://www.google.com", "https://www.apple.com", "https://www.microsoft.com" };
    const double TIME_TOLERANCE_MINUTES = 15;
    const float CLOCK_JUMP_CHECK_INTERVAL = 10f;
    const double CLOCK_JUMP_THRESHOLD_SECONDS = 120;
    bool isCheckingServerTime;
    DateTime lastUtcSample;
    float lastRealtimeSample;

    float temp = 1;

    public bool noAdsBought;

    private void Awake()
    {
        Time.timeScale = 1;
        instance = this;

        //스크린 타임 방지
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.runInBackground = true;

        ResetClockSample();
        InvokeRepeating("CheckClockJump", CLOCK_JUMP_CHECK_INTERVAL, CLOCK_JUMP_CHECK_INTERVAL);

        RemoteSettings.ForceUpdate();
    }

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;
        StartScreen.gameObject.SetActive(true);
        RequestServerTimeCheck();
    }

    // Update is called once per frame
    void Update()
    {
        if(Time.timeScale > 0.9f) {
            //TotalPlayedTime
            totalPlayedTime += Time.unscaledDeltaTime;

            //최대 돈
            if(money >= maxMoney)
                maxMoney = money;
        }

        //Wifi connection
        if(Application.internetReachability == NetworkReachability.NotReachable && !WifiErrorTab.activeSelf) {
            Error();
        }

        //Escape Button
#if UNITY_ANDROID
        if(Input.GetKeyDown(KeyCode.Escape)) {
            QuitTab.SetActive(true);
            PauseGame();
        }
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if(hasFocus) {
            ResetClockSample();
            RequestServerTimeCheck();
        }
    }

    void ResetClockSample()
    {
        lastUtcSample = DateTime.UtcNow;
        lastRealtimeSample = Time.realtimeSinceStartup;
    }

    // 앱이 켜져 있는 동안 기기 시계가 실제 흐른 시간과 다르게 크게 움직이면(시간 조작 의심) 서버 시간을 확인해요.
    void CheckClockJump()
    {
        double utcElapsed = (DateTime.UtcNow - lastUtcSample).TotalSeconds;
        double realElapsed = Time.realtimeSinceStartup - lastRealtimeSample;
        ResetClockSample();

        if(Math.Abs(utcElapsed - realElapsed) > CLOCK_JUMP_THRESHOLD_SECONDS) {
            RequestServerTimeCheck();
        }
    }

    void RequestServerTimeCheck()
    {
        if(isCheckingServerTime || RealTimeErrorTab == null || RealTimeErrorTab.activeSelf)
            return;
        StartCoroutine(CheckServerTime());
    }

    IEnumerator CheckServerTime()
    {
        isCheckingServerTime = true;

        for(int i = 0; i < TIME_CHECK_URLS.Length; i++) {
            using(UnityWebRequest request = UnityWebRequest.Head(TIME_CHECK_URLS[i])) {
                request.timeout = 5;
                yield return request.SendWebRequest();

                string dateHeader = request.GetResponseHeader("Date");
                if(!string.IsNullOrEmpty(dateHeader) &&
                   DateTime.TryParseExact(dateHeader, "r", CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime serverUtc)) {

                    if(Math.Abs((serverUtc - DateTime.UtcNow).TotalMinutes) > TIME_TOLERANCE_MINUTES) {
                        Time.timeScale = 0;
                        RealTimeErrorTab.SetActive(true);
                    }
                    isCheckingServerTime = false;
                    yield break;
                }
            }
        }

        // 모든 서버 접속 실패: 검사를 건너뛰어요.
        isCheckingServerTime = false;
    }

    public void Error()
    {
        Time.timeScale = 0;
        WifiErrorTab.SetActive(true);
    }

    public void ErrorTabOkay()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif  
    }

    public void PauseGame()
    {
        temp = Time.timeScale;
        Time.timeScale = 0.0000001f;
    }
    public void ResumeGame()
    {
        if(temp < 1) {
            temp = 1;
        }
        setTimeScale(temp);
    }

    public void setTimeScale(float a)
    {
        Time.timeScale = a;
    }

    public void saveGameSpeedandAuto(float a, bool b)
    {
        if(a >= 1)
            PlayerPrefs.SetFloat("lastGameSpeed", a);
        PlayerPrefs.SetInt("lastAutoAttack", b ? 1 : 0);
    }

    public void loadGameSpeedandAuto()
    {
        if(PlayerPrefs.HasKey("lastGameSpeed")) {
            if(PlayerPrefs.GetFloat("lastGameSpeed") < 1) {
                PlayerPrefs.SetFloat("lastGameSpeed", 1);
            }
            setTimeScale(PlayerPrefs.GetFloat("lastGameSpeed"));
        } else {
            PlayerPrefs.SetFloat("lastGameSpeed", 1);
            setTimeScale(PlayerPrefs.GetFloat("lastGameSpeed"));
        }

        if(PlayerPrefs.HasKey("lastAutoAttack")) {
            WeaponManager.instance.shootType = PlayerPrefs.GetInt("lastAutoAttack") == 1 ? WeaponManager.ShootType.AutoShoot : WeaponManager.ShootType.NormalShoot;
        } else {
            PlayerPrefs.SetInt("lastAutoAttack", 0);
            WeaponManager.instance.shootType = PlayerPrefs.GetInt("lastAutoAttack") == 1 ? WeaponManager.ShootType.AutoShoot : WeaponManager.ShootType.NormalShoot;
        }

        saveGameSpeedandAuto(Time.timeScale, WeaponManager.instance.shootType == WeaponManager.ShootType.AutoShoot);
    }

    // ---- 큰 숫자 줄여 쓰기 (K, M, B, T, Q). 예전 7개 함수가 같은 일을 조금씩 다르게 복사하고 있던 것을 하나로.
    static readonly (double div, string suffix)[] Units = { (1e15, "Q"), (1e12, "T"), (1e9, "B"), (1e6, "M"), (1e3, "K") };

    // plainBelow 미만은 그대로(천 단위 쉼표), 그 이상은 단위를 붙여 format 으로 표시
    static string Abbrev(long M, long plainBelow, System.Func<double, string> format)
    {
        if(M < plainBelow) return M.ToString("#,##0");
        foreach(var u in Units)
            if(M >= u.div) return format(M / u.div) + u.suffix;
        return M.ToString("#,##0");
    }

    static string Fixed2(double v) => v.ToString("F2");
    static string Upto2(double v) => v.ToString("#,##0.##");
    static string Rounded(double v) => System.Math.Round(v).ToString("#,##0");
    static string Floored(double v) => System.Math.Floor(v).ToString("#,##0");

    public static string MoneyStringForGamemanager(long M) => Abbrev(M, 10000, Fixed2);
    public static string MoneyString(long M) => Abbrev(M, 10000, Upto2);
    public static string MoneyStringForShop(long M) => Abbrev(M, 10000, Rounded);
    public static string MoneyStringForTower(long M) => Abbrev(M, 1000, Rounded);
    // (예전엔 B·T 를 10배 크게 보여 줬어요)
    public string MoneyStringForAchievement(long M) => Abbrev(M, 10000, v => v < 1000 && M < 1000000 ? Upto2(v) : Fixed2(v));
    public string moneyStringForMine(long M) => Abbrev(M, 1000000, Floored);
    public string MoneyStringForInfiniteStage(long M) => Abbrev(M, 10000, Floored);

    //경험치 올리기
    public static void Enemy_Dead_Player_EXP_UP(int exp)
    {
        Player.instance.PlayerEXPUP(exp);
    }

    //돈 올리기
    public static void Enemy_Dead_Money_UP(long money) => instance.money = SafeAdd(instance.money, money);

    // 더하다가 long 최대값을 넘으면 최대값에서 멈추고, 0 아래로는 내려가지 않아요
    public static long SafeAdd(long a, long b)
    {
        if(b > 0 && a > long.MaxValue - b) return long.MaxValue;
        return System.Math.Max(0, a + b);
    }

    public static int GetTotalExpOfEnemy(Enemy enemy)
    {
        return (int)((enemy.Enemy_EXP + EquipingManager.Instance.ExtraEXPByNumber_Equip) * (1 + Player.GetStatsByType(Upgrade_StatsPrefab.StatsType.ExtraEXP)));
    }

    public static long GetTotalGoldOfEnemy(Enemy enemy)
    {
        return (long)((enemy.enemy_Money + EquipingManager.Instance.ExtraGoldByNumber_Equip) * (1 + Player.GetStatsByType(Upgrade_StatsPrefab.StatsType.ExtraMoney)));
    }

    public static int GetDiamond()
    {
        return instance.Diamond;
    }

    public static void SetDiamond(int n)
    {
        instance.Diamond += n;
    }

    public void addDiamond(int n) => Diamond += n;

    public static long GetOre()
    {
        return instance.Ore;
    }

    public static void SetOre(long n)
    {
        instance.Ore += n;
    }

    public void addOre(long n) => Ore += n;

    public static long GetMoney()
    {
        return instance.money;
    }

    public static void SetMoney(long n) => instance.money = SafeAdd(instance.money, n);

    public void addMoney(long n) => money = SafeAdd(money, n);

    public static long GetParts()
    {
        return instance.Parts;
    }

    public static void SetParts(long n)
    {
        instance.Parts += n;
    }

    public void addParts(long n) => Parts += n;

    public static void SetTimeScale()
    {
        if(Time.timeScale <= 1)
            Time.timeScale = 1.5f;
        else if(Time.timeScale == 1.5f) {
            if(instance.GameSpeedBought)
                Time.timeScale = 2f;
            else
                Time.timeScale = 1f;
        } else if (Time.timeScale == 2f){
            Time.timeScale = 1;
        } else if(Time.timeScale >= 3.5f)
            Time.timeScale = 1f;

        instance.saveGameSpeedandAuto(Time.timeScale, WeaponManager.instance.shootType == WeaponManager.ShootType.AutoShoot);
    }


    public void LoadData(GameData data)
    {
        this.money = data.Money;
        this.Diamond = data.Diamond;
        this.Ore = data.Ores;
        this.Parts = data.parts;
        this.totalPlayedTime = data.totalPlayedTime;
        this.maxMoney = data.maxMoney;
        this.GameSpeedBought = data.gameSpeedBought;
        noAdsBought = data.noAdsBought;
    }

    public void SaveData(GameData data)
    {
        data.Money = this.money;
        data.Diamond = this.Diamond;
        data.Ores = this.Ore;
        data.parts = this.Parts;
        data.totalPlayedTime = this.totalPlayedTime;
        data.maxMoney = this.maxMoney;
        data.gameSpeedBought = this.GameSpeedBought;
        data.noAdsBought = noAdsBought;
    }
}
