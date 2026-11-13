using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System;
using System.Collections.Generic;

// 보상형 광고 (리마스터 정리)
// - 씬 버튼이 부르는 함수 이름(Show...)은 그대로 유지
// - 광고/동의 콜백은 Android Java 스레드에서 올 수 있어서 Unity API는 Update()에서 실행
// - 광고 동의(UMP): EEA/영국 등 동의가 필요한 지역에서만 동의 창이 떠요
// - 에디터/개발 빌드에서는 Google 테스트 광고 (자기 광고 노출·클릭은 AdMob 정책 위반)
public class AdmobManager : MonoBehaviour
{
    [Header("Loading Ads Screen")]
    public GameObject LoadingAdsScreen;
    public GameObject LoadingCircle;
    public GameObject LoadingAdsError;
    public GameObject NoAdsTab;

    public static AdmobManager Instance { get; private set; }

    RewardedAd rewardedAd;
    bool isAdsInitialized;
    bool isLoading;
    readonly List<Action> loadWaiters = new List<Action>();
    readonly Queue<Action> mainThreadActions = new Queue<Action>();

    string RewardedId {
        get {
            bool test = Debug.isDebugBuild;
#if UNITY_ANDROID
            return test ? "ca-app-pub-3940256099942544/5224354917" : "ca-app-pub-1864928959429416/5359044322";
#elif UNITY_IPHONE
            return test ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-1864928959429416/8545542497";
#else
            return "unused";
#endif
        }
    }

    void RunOnMainThread(Action action)
    {
        lock(mainThreadActions) {
            mainThreadActions.Enqueue(action);
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ConsentRequestParameters request = new ConsentRequestParameters();
        ConsentInformation.Update(request, (FormError updateError) => RunOnMainThread(() => {
            if(updateError != null)
                Debug.LogWarning("UMP: consent info update failed: " + updateError.Message);
            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) => RunOnMainThread(() => {
                if(formError != null)
                    Debug.LogWarning("UMP: consent form error: " + formError.Message);
                if(ConsentInformation.CanRequestAds())
                    InitializeAds();
            }));
        }));

        // 이전 실행에서 이미 동의를 받았으면 바로 초기화
        if(ConsentInformation.CanRequestAds())
            InitializeAds();
    }

    void InitializeAds()
    {
        if(isAdsInitialized)
            return;
        isAdsInitialized = true;
        MobileAds.Initialize(initStatus => RunOnMainThread(() => {
            Debug.Log("Ads Initialised !!");
            LoadRewardedAd();
        }));
    }

    // 설정 화면의 "개인정보 설정" 버튼용 (동의 지역 유저가 동의를 바꿀 수 있어야 함)
    public bool IsPrivacyOptionsRequired()
    {
        return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    }

    public void ShowPrivacyOptionsForm()
    {
        ConsentForm.ShowPrivacyOptionsForm((FormError error) => RunOnMainThread(() => {
            if(error != null)
                Debug.LogWarning("UMP: privacy options form error: " + error.Message);
            if(ConsentInformation.CanRequestAds())
                InitializeAds();
        }));
    }

    #region Rewarded

    // 광고를 불러오고, 끝나면(성공/실패 모두) callback 호출
    public void LoadRewardedAd(Action callback = null)
    {
        if(callback != null)
            loadWaiters.Add(callback);
        if(rewardedAd != null && rewardedAd.CanShowAd()) {
            FlushWaiters();
            return;
        }
        if(isLoading)
            return;
        if(!ConsentInformation.CanRequestAds()) {
            Debug.LogWarning("UMP: ads cannot be requested without consent.");
            FlushWaiters();
            return;
        }

        if(rewardedAd != null) {
            rewardedAd.Destroy();
            rewardedAd = null;
        }
        isLoading = true;
        RewardedAd.Load(RewardedId, new AdRequest(), (RewardedAd ad, LoadAdError error) => RunOnMainThread(() => {
            isLoading = false;
            if(error != null || ad == null) {
                Debug.LogWarning("Rewarded failed to load " + error);
            } else {
                rewardedAd = ad;
                RewardedAdEvents(rewardedAd);
            }
            FlushWaiters();
        }));
    }

    void FlushWaiters()
    {
        var list = new List<Action>(loadWaiters);
        loadWaiters.Clear();
        foreach(var a in list)
            a?.Invoke();
    }

    // 공용: 광고를 보여 주고 보상. 광고 제거 구매자는 noAdsTab 안내 후 바로 보상
    void ShowRewarded(Action onReward, bool showNoAdsTab)
    {
        if(GameManager.instance.noAdsBought) {
            if(showNoAdsTab && NoAdsTab != null)
                NoAdsTab.SetActive(true);
            onReward?.Invoke();
            return;
        }
        LoadingAdsScreen.SetActive(true);
        LoadRewardedAd(() => {
            if(rewardedAd != null && rewardedAd.CanShowAd()) {
                var ad = rewardedAd;
                rewardedAd = null; // 한 번 보여 준 광고는 다시 못 씀
                ad.Show((Reward reward) => RunOnMainThread(() => {
                    LoadingAdsScreen.SetActive(false);
                    onReward?.Invoke();
                }));
            } else {
                LoadingAdsScreen.SetActive(false);
                LoadingAdsError.SetActive(true);
                Debug.Log("Rewarded ad not ready");
            }
        });
    }

    // 리마스터: 증강 리롤 등에서 쓰는 공용 함수
    public void ShowRewardedAdGeneric(Action onReward)
    {
        ShowRewarded(onReward, false);
    }

    public void ShowRewardedAdDailyGachaTicket()
    {
        ShowRewarded(() => DailyRewardManager.instance.adWatched(), true);
    }

    public void ShowRewardedAdDungeonTicket()
    {
        ShowRewarded(() => DungeonManager.instance.RewardByAds(), true);
    }

    public void ShowRewardedAdRewardDouble()
    {
        ShowRewarded(() => StageManager.instance.RewardDoubleAd(), true);
    }

    public void showRewardedAdRewardTower()
    {
        ShowRewarded(() => TowerManager.Instance.ReduceTimeByAds(), true);
    }

    public void showRewardedAdRewardTowerReward()
    {
        ShowRewarded(() => TowerManager.Instance.DoubleTowerRewardAds(), true);
    }

    public void showRewardedAdAttendance()
    {
        ShowRewarded(() => AttendanceCheck.Instance.playerRewardAds(), true);
    }

    public void showRewardedAdAchievement()
    {
        ShowRewarded(null, true);
    }

    public void RewardedAdEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () => RunOnMainThread(() => {
            // 다음 광고 미리 불러오기
            LoadRewardedAd();
        });
        ad.OnAdFullScreenContentFailed += (AdError error) => RunOnMainThread(() => {
            Debug.LogError("Rewarded ad failed to open full screen content with error : " + error);
            LoadingAdsScreen.SetActive(false);
            LoadRewardedAd();
        });
    }

    #endregion

    void Update()
    {
        while(true) {
            Action action;
            lock(mainThreadActions) {
                if(mainThreadActions.Count == 0)
                    break;
                action = mainThreadActions.Dequeue();
            }
            action();
        }

        if(LoadingAdsScreen != null && LoadingAdsScreen.gameObject.activeSelf && LoadingCircle != null) {
            LoadingCircle.transform.Rotate(new Vector3(0, 0, 2f));
        }
    }
}
