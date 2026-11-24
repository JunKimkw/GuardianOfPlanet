using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 무기 상점(강화 칸)의 공통 부모: 잠금, 가격, 레벨업, 패시브 표시.
// 무기마다 다른 것(연결된 무기, 해금 행성, 설명, 패시브 효과)만 자식 클래스가 정해요.
public abstract class WeaponShopBase : MonoBehaviour
{
    [Header("UI 바꾸기")]
    public TextMeshProUGUI LevelText;
    public TextMeshProUGUI WeaponStat;
    public TextMeshProUGUI priceText;

    [HideInInspector] public int purchaseSize = 1;
    protected long TotalPrice;

    [Header("#-----잠금")]
    public GameObject LockCover;
    public TextMeshProUGUI LockText;

    [Header("#--------Passives")]
    public List<WeaponPassive> passiveObjects;

    public abstract WeaponBase Weapon { get; }
    protected abstract int UnlockPlanetIndex { get; }   // -1 이면 처음부터 열림
    protected abstract string StatLabel { get; }        // "우주선 데미지의" 등
    protected abstract string Intro { get; }            // 레벨 0일 때 설명
    protected virtual string DamageLabel => "현재 데미지";

    // 레벨 100 미만 구간 가격 (기본 무기만 더 싸요)
    protected virtual int LowBase => 100;
    protected virtual int[] LowSlopes => new[] { 15, 16, 17, 17 };   // 0~9, 10~49, 50~99, 100~199

    static readonly Color Affordable = new Color(0, 1, 0.2204754f);

    // 개발용: 테스트 창의 "무기 전부 열기" (저장되지 않아요)
    public static bool DebugUnlockAll;

    // 그 무기의 행성을 클리어해야 상점에서 사고, 원정에서도 써요 (2026-11-25 사용자: 행성마다 정해진 무기가 있으니까).
    //  행성에서는 그 행성의 무기를 미리 써 봐요 (안 샀으면 Lv.1)
    public bool IsUnlocked => DebugUnlockAll || UnlockPlanetIndex < 0 || StageManager.instance.planets[UnlockPlanetIndex].PlanetCleared;
    public bool ExpeditionUnlocked => IsUnlocked;
    public int PlanetIndex => UnlockPlanetIndex;

    Image buyButtonImage;

    protected virtual void OnEnable()
    {
        setPassiveValues();
        if(UnlockPlanetIndex >= 0 && LockCover != null) {
            var planet = StageManager.instance.planets[UnlockPlanetIndex];
            LockCover.SetActive(!IsUnlocked);
            if(LockText != null) LockText.text = planet.PlanetName + " 행성 클리어";
        }
    }

    protected virtual void Update()
    {
        var w = Weapon;
        if(w == null) return;

        WeaponStat.text = (w.UpgradeLevel > 0
            ? $"{StatLabel} {w.DamageMultiplier * 100:F0}%로 공격\n{DamageLabel}: {BigNumber.Format(w.HitDamage * CurrentNumberScale)}"
            : Intro);
        LevelText.text = "Lv." + w.UpgradeLevel;
        TotalPrice = getTotalMoney(w.UpgradeLevel, purchaseSize);
        priceText.text = GameManager.MoneyString(TotalPrice);

        if(IsUnlocked) {
            if(buyButtonImage == null && transform.childCount > 2) buyButtonImage = transform.GetChild(2).GetComponent<Image>();
            if(buyButtonImage != null) buyButtonImage.color = GameManager.GetMoney() >= TotalPrice ? Affordable : Color.white;
        }
    }

    // 지금 원정 스테이지의 구간 배율 (적 체력과 똑같이 곱해져서 실제로 보이는 데미지)
    static double CurrentNumberScale => InfiniteStage.Instance != null ? RemasterBalance.NumberScale(InfiniteStage.Instance.CurrentStage) : 1;

    // 강화 버튼 (씬에서 연결됨)
    public void LevelUP()
    {
        var w = Weapon;
        if(w == null || !IsUnlocked) return;   // 예전엔 잠긴 무기도 살 수 있었어요(잠금 덮개만 가렸음)
        TotalPrice = getTotalMoney(w.UpgradeLevel, purchaseSize);
        if(GameManager.GetMoney() < TotalPrice) return;

        GameManager.SetMoney(-TotalPrice);
        if(!w.gameObject.activeSelf) w.gameObject.SetActive(true);
        for(int i = 0; i < purchaseSize; i++) w.UpgradeWeapon();
        setPassiveValues();
        SoundManager.Instance.Invoke("playCoinSFX", SoundManager.Instance.click.length);
    }

    public long getTotalMoney(int level, int count)
    {
        long total = 0;
        for(int e = 0; e < count; e++) total += PriceOf(level + e);
        return total;
    }

    protected virtual long PriceOf(int i)
    {
        if(i >= 500) return (long)(50000000 * Mathf.Pow(1.0075f, i - 500));
        if(i >= 400) return (long)(15000000 * Mathf.Pow(1.015f, i - 400));
        if(i >= 300) return (long)(1000000 * Mathf.Pow(1.025f, i - 300));
        if(i >= 200) return (long)(100000 * Mathf.Pow(1.0175f, i - 200));
        var s = LowSlopes;
        if(i >= 100) return (long)(LowBase + i * s[3] * Mathf.Pow(1.03f, i - 100));
        if(i >= 50) return LowBase + i * s[2];
        if(i >= 10) return LowBase + i * s[1];
        return LowBase + i * s[0];
    }

    // 패시브 다시 계산 (WeaponManager 가 매 프레임 불러요)
    public void setPassiveValues()
    {
        if(Weapon != null) ApplyPassives(Weapon.UpgradeLevel);
    }

    protected abstract void ApplyPassives(int lv);

    // index 번째 패시브가 열렸는지 (열렸으면 덮개를 치워요)
    protected bool Passive(int index, int needLevel)
    {
        bool on = Weapon.UpgradeLevel >= needLevel;
        if(on && passiveObjects != null && index < passiveObjects.Count && passiveObjects[index] != null)
            passiveObjects[index].PassiveCover.SetActive(false);
        return on;
    }

    // 레벨 100부터 레벨마다 쌓이는 % 보너스
    protected static float PerLevelFrom100(int lv, float perLevel) => lv >= 100 ? (lv - 99) * perLevel : 0f;
}
