using System.Collections.Generic;
using UnityEngine;

// 무기 10종 생성·조준·저장. 무기마다 같은 일을 반복하던 코드를 목록 하나로 돌려요.
public class WeaponManager : MonoBehaviour, IDataPersistence
{
    public static WeaponManager instance;

    public enum ShootType { NormalShoot, AutoShoot, DontShoot };
    [Header("#-----ShootType")]
    public ShootType shootType;

    // 프리팹과 상점 연결 (인스펙터 값 유지를 위해 이름 그대로)
    [Header("#-----Main Weapon")] public MainWeapon mainWeapon_Pfb; public MainWeaponShop mainWeaponShop;
    [HideInInspector] public MainWeapon mainWeapon;
    [Header("#-----Double Gun")] public DoubleGun doubleGun_Pfb; public DoubleGunShop doubleGunShop;
    [HideInInspector] public DoubleGun doubleGun;
    [Header("#-----Bomber")] public Bomber Bomber_Pfb; public BomberShop BomberShop;
    [HideInInspector] public Bomber BomberGun;
    [Header("#-----Tracking Gun")] public TrackingGun trackingGun_Pfb; public TrackingGunShop trackingGunShop;
    [HideInInspector] public TrackingGun trackingGun;
    [Header("#-----Poison Gun")] public PoisonPlane poison_Pfb; public PoisonShop poisonShop;
    [HideInInspector] public PoisonPlane poison;
    [Header("#-----Orbital Gun")] public Orbital orbital_Pfb; public OrbitalShop orbitalShop;
    [HideInInspector] public Orbital orbital;
    [Header("#-----360Lazer Gun")] public Lazer lazer_Pfb; public LazerShop lazerShop;
    [HideInInspector] public Lazer lazer;
    [Header("#-----strLaser Gun")] public strLazer strLaser_pfb; public strLazerShop strLaserShop;
    [HideInInspector] public strLazer strLaser;
    [Header("#-----Sniper Gun")] public SniperGun sniperGun_Pfb; public SniperGunShop sniperGunShop;
    [HideInInspector] public SniperGun sniperGun;
    [Header("#-----Bounce Gun")] public BounceGun bounceGun_pfb; public BounceGunShop bounceGun_Shop;
    [HideInInspector] public BounceGun bounceGun;

    [Header("#-----Gun Spawn Location")]
    public Transform BottomPoint;
    public Transform TopPoint;

    public GameObject ExclamationMark;

    // 모든 무기 / 상점 (같은 순서)
    public readonly List<WeaponBase> Weapons = new List<WeaponBase>();
    public readonly List<WeaponShopBase> Shops = new List<WeaponShopBase>();

    T Spawn<T>(T prefab) where T : WeaponBase
    {
        Camera cam = Camera.main;
        float y = (cam.ScreenToWorldPoint(BottomPoint.position).y + cam.ScreenToWorldPoint(TopPoint.position).y) / 2;
        T w = Instantiate(prefab, new Vector3(0, y), Quaternion.identity, transform);
        w.gameObject.SetActive(false);
        return w;
    }

    void Register(WeaponBase w, WeaponShopBase shop)
    {
        Weapons.Add(w);
        Shops.Add(shop);
    }

    void Awake()
    {
        instance = this;

        mainWeapon = Spawn(mainWeapon_Pfb); mainWeaponShop.mainWeapon = mainWeapon; Register(mainWeapon, mainWeaponShop);
        doubleGun = Spawn(doubleGun_Pfb); doubleGunShop.weapon = doubleGun; Register(doubleGun, doubleGunShop);
        BomberGun = Spawn(Bomber_Pfb); BomberShop.bomber = BomberGun; Register(BomberGun, BomberShop);
        trackingGun = Spawn(trackingGun_Pfb); trackingGunShop.weapon = trackingGun; Register(trackingGun, trackingGunShop);
        poison = Spawn(poison_Pfb); poisonShop.weapon = poison; Register(poison, poisonShop);
        orbital = Spawn(orbital_Pfb); orbitalShop.weapon = orbital; Register(orbital, orbitalShop);
        lazer = Spawn(lazer_Pfb); lazerShop.weapon = lazer; Register(lazer, lazerShop);
        strLaser = Spawn(strLaser_pfb); strLaserShop.weapon = strLaser; Register(strLaser, strLaserShop);
        sniperGun = Spawn(sniperGun_Pfb); sniperGunShop.weapon = sniperGun; Register(sniperGun, sniperGunShop);
        bounceGun = Spawn(bounceGun_pfb); bounceGun_Shop.weapon = bounceGun; Register(bounceGun, bounceGun_Shop);

        SetPurchaseSize(1);
    }

    void Start()
    {
        InvokeRepeating(nameof(checkExclamationMark), 0, 1f);
    }

    void Update()
    {
        // 특성·패시브가 바뀔 수 있어서 매 프레임 다시 계산 (예전과 같음)
        RefreshAll();

        foreach(var w in Weapons) {
            if(w == orbital) continue;   // 궤도는 조준이 없어요
            switch(shootType) {
                case ShootType.NormalShoot: w.NormalShooting(); break;
                case ShootType.AutoShoot: w.AutoShooting(); break;
                case ShootType.DontShoot: w.DonShoot(); break;
            }
        }

        // 일반 스테이지에서 자동 공격 시간 소모
        if(StageManager.instance.isInStage && Time.timeScale > 0.5f && shootType == ShootType.AutoShoot) {
            if(Player.instance.AutoShootTime > 0)
                Player.instance.AutoShootTime -= Time.unscaledDeltaTime;
            else {
                shootType = ShootType.NormalShoot;
                Player.instance.AutoShootTime = 0;
            }
        }
    }

    void RefreshAll()
    {
        for(int i = 0; i < Weapons.Count; i++) {
            Weapons[i].SetDamage();
            Shops[i].setPassiveValues();
        }
    }

    public int getTotalWeaponLevel()
    {
        int sum = 0;
        foreach(var w in Weapons) sum += w.UpgradeLevel;
        return sum;
    }

    // 증강 시스템용: 보조 무기 종류 → 무기
    public WeaponBase GetSideWeapon(SideWeapon type)
    {
        switch(type) {
            case SideWeapon.DoubleGun: return doubleGun;
            case SideWeapon.Bomber: return BomberGun;
            case SideWeapon.TrackingGun: return trackingGun;
            case SideWeapon.Poison: return poison;
            case SideWeapon.Orbital: return orbital;
            case SideWeapon.Lazer: return lazer;
            case SideWeapon.StrLaser: return strLaser;
            case SideWeapon.Sniper: return sniperGun;
            case SideWeapon.BounceGun: return bounceGun;
        }
        return null;
    }

    // 살 수 있는 강화가 하나라도 있으면 느낌표
    public void checkExclamationMark()
    {
        long money = GameManager.GetMoney();
        bool any = false;
        for(int i = 0; i < Weapons.Count && !any; i++)
            any = Shops[i].IsUnlocked && money >= Shops[i].getTotalMoney(Weapons[i].UpgradeLevel, 1);
        ExclamationMark.SetActive(any);
    }

    public void SetPurchaseSize(int size)
    {
        foreach(var s in Shops) s.purchaseSize = size;
    }

    public void AutoAttack()
    {
        if(shootType != ShootType.AutoShoot) {
            if(Player.instance.AutoShootTime > 0)
                shootType = ShootType.AutoShoot;
        } else {
            shootType = ShootType.NormalShoot;
        }
        GameManager.instance.saveGameSpeedandAuto(Time.timeScale, shootType == ShootType.AutoShoot);
    }

    public void noLimitAutoAttack()
    {
        shootType = shootType == ShootType.NormalShoot ? ShootType.AutoShoot : ShootType.NormalShoot;
    }

    public void LoadData(GameData data)
    {
        foreach(var w in Weapons) {
            data.UpgradeLevel_Weapon.TryGetValue(w.id, out int lvl);
            w.UpgradeLevel = lvl;
            if(lvl > 0) w.gameObject.SetActive(true);
        }
        RefreshAll();
    }

    public void SaveData(GameData data)
    {
        foreach(var w in Weapons)
            data.UpgradeLevel_Weapon[w.id] = w.UpgradeLevel;
    }
}
