using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Services.Authentication;

// 설정 창 A+C (2026-11-26 사용자 "설정은 A랑 C 섞어서", 미리보기 그대로 = UIGen/settings)
// - 예전 설정 페이지(Canvas/Tabs Above Buttons/Settings)가 열리면 그 위를 덮어요 (던전·모험 안의 일시정지 설정은 그대로)
// - 소리: 막대를 눌러 끌면 예전 Setting.changeMusicVolume / changeSFXVolume 그대로 (저장도 예전 키)
// - 토글 (게임에 있는 기능만): 데미지 숫자 보기 · 화면 꺼짐 방지 · 저사양 모드(30fps). PlayerPrefs opt_* 에 저장
// - 칸: 계정(ID·로그인 방식) · 리뷰(스토어) · 개인정보(광고 동의 설정) · 도움말. 로그아웃 = 묻는 창 뒤 예전 그대로, 초기화 = 두 번 누르고 묻는 창([취소][초기화])
public partial class RemasterUI
{
    GenView stView;
    GameObject stPage;
    bool stResetArmed, stOptApplied;
    public static bool ShowDamageText = true;   // DamagePopup 이 읽어요

    static bool OptOn(int k) => PlayerPrefs.GetInt("opt_" + k, k == 2 ? 0 : 1) == 1;   // 0 데미지 숫자 · 1 화면 꺼짐 방지 · 2 저사양
    static void ApplyOptions()
    {
        ShowDamageText = OptOn(0);
        Screen.sleepTimeout = OptOn(1) ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        Application.targetFrameRate = OptOn(2) ? 30 : 60;
    }

    void UpdateSettingsPage()
    {
        // 게임 시작 뒤 한 번 (GameManager.Start 가 60fps 로 정한 다음)
        if(!stOptApplied && Time.frameCount > 10) { stOptApplied = true; ApplyOptions(); }
        if(stPage == null) { stPage = GenFindPage("Settings", "Tabs Above Buttons"); if(stPage == null) return; }
        if(!stPage.activeInHierarchy) { if(stView != null) { GenClose(stView); stView = null; } return; }
        if(stView == null || stView.layer == null) BuildSettings();
    }

    Setting StSetting() => FindAnyObjectByType<Setting>(FindObjectsInactive.Include);

    void BuildSettings()
    {
        stResetArmed = false;
        stView = GenBuild("settings", "SettingsMix", true, (RectTransform)stPage.transform);
        if(stView == null) return;
        var v = stView;
        GenHit(v, "close", () => GenPageBack(stPage));
        // 소리 막대
        var st = StSetting();
        if(st != null) {
            StSlider(v, "mus", st.musicSliders != null && st.musicSliders.Length > 0 ? st.musicSliders[0] : null, s => st.changeMusicVolume(s));
            StSlider(v, "sfx", st.SFXSliders != null && st.SFXSliders.Length > 0 ? st.SFXSliders[0] : null, s => st.changeSFXVolume(s));
        }
        // 토글
        for(int k = 0; k < 3; k++) {
            int kk = k;
            GenHit(v, "t" + k + ".off", () => { PlayerPrefs.SetInt("opt_" + kk, OptOn(kk) ? 0 : 1); ApplyOptions(); RefreshSettings(); }, 4f);
        }
        // 칸
        GenHit(v, "tile0", () => {
            string id = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "-";
            string how = PlayerPrefs.HasKey("GoogleLogIn") ? "구글 계정" : PlayerPrefs.HasKey("GuestLogIn") ? "게스트" : "-";
            ShowMessage("계정", $"로그인: {how}\nID: {id}");
        });
        GenHit(v, "tile1", () => {
#if UNITY_IOS
            Application.OpenURL("https://apps.apple.com/us/app/%EA%B0%80%EB%94%94%EC%96%B8-%EC%98%A4%EB%B8%8C-%ED%94%8C%EB%9E%98%EB%8B%9B-%EB%B0%A9%EC%B9%98%ED%98%95-rpg/id6483863042");
#else
            Application.OpenURL("https://play.google.com/store/apps/details?id=com.aragames.guardianofplanet");
#endif
        });
        GenHit(v, "tile2", () => {
            var ad = AdmobManager.Instance;
            if(ad != null && ad.IsPrivacyOptionsRequired()) ad.ShowPrivacyOptionsForm();
            else ShowMessage("개인정보", "이 지역은 따로 정할 광고 동의 설정이 없어요");
        });
        GenHit(v, "tile3", () => ShowMessage("도움말", "원정에서 스테이지를 올리고, 막히면 장비·무기를 키워요\n행성 모드와 던전에서 재료를 모아요\n장비는 같은 등급 3개를 합성할 수 있어요"));
        GenHit(v, "copy", () => {
            string id = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
            GUIUtility.systemCopyBuffer = id;
            ShowMessage("복사했어요", id);
        });
        // 로그아웃: 예전처럼 한 번 묻고 (예전 Popup_Logout). 취소·바깥 = 그대로
        GenHit(v, "logout", () => ShowMessage("로그아웃", "로그아웃하고 첫 화면으로 돌아갈까요?", "로그아웃", () => {
            var s2 = StSetting(); GenPageBack(stPage); if(s2 != null) s2.SignOut();
        }, cancel: "취소"));
        GenHit(v, "reset", () => {
            if(!stResetArmed) { stResetArmed = true; RefreshSettings(); return; }
            stResetArmed = false; RefreshSettings();
            // 묻는 창 (2026-11-27 점검: 예전엔 바깥을 눌러도 초기화됐어요 → 이제 '초기화' 버튼만)
            ShowMessage("게임 초기화", "모든 진행이 지워져요. 되돌릴 수 없어요", "초기화", () => { if(DataPersistenceManager.instance != null) DataPersistenceManager.instance.DeleteGame(); }, cancel: "취소");
        });
        RefreshSettings();
    }

    void RefreshSettings()
    {
        var v = stView;
        if(v == null || v.layer == null) return;
        for(int k = 0; k < 3; k++) { bool on = OptOn(k); GenShow(v, "t" + k + ".on", on); GenShow(v, "t" + k + ".off", !on); }
        GenText(v, "id", AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "로그인 전");
        GenText(v, "reset", stResetArmed ? "한 번 더 누르면 초기화" : "게임 초기화");
        GenText(v, "ver", "버전 " + Application.version);
        var st = StSetting();
        if(st != null) {
            StSliderShow(v, "mus", st.musicSliders != null && st.musicSliders.Length > 0 ? st.musicSliders[0] : null);
            StSliderShow(v, "sfx", st.SFXSliders != null && st.SFXSliders.Length > 0 ? st.SFXSliders[0] : null);
        }
    }

    // 막대: 내보낼 때 100% 로 잰 채움·손잡이를 값만큼 줄이고 옮겨요. 막대 안 폭 = 196px (200 − 테두리 2×2)
    readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> stKnobBase = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
    void StSliderShow(GenView v, string key, Slider s)
    {
        if(s == null) return;
        float frac = Mathf.InverseLerp(s.minValue, s.maxValue, s.value);
        GenFill(v, key + ".fill", frac);
        if(v.objs.TryGetValue(key + ".knob", out var kl))
            foreach(var g in kl) {
                if(g == null) continue;
                var rt = (RectTransform)g.transform;
                if(!stKnobBase.TryGetValue(rt, out var b)) stKnobBase[rt] = b = rt.anchoredPosition;
                rt.anchoredPosition = b + new Vector2((frac - 1f) * 196f * K, 0f);
            }
    }
    void StSlider(GenView v, string key, Slider s, System.Action<Slider> apply)
    {
        if(s == null || !v.rects.TryGetValue(key + ".fill", out var r)) return;
        // 끌기 칸: 막대 폭(채움 100% = 196px) + 위아래로 넉넉히
        var hit = PxBox("Drag_" + key, v.box, r.x - 8f, r.y - 14f, r.width + 16f, r.height + 28f);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        var d = hit.gameObject.AddComponent<GenDrag>();
        d.pad = 8f * K;
        d.onFrac = f => { s.value = Mathf.Lerp(s.minValue, s.maxValue, f); apply(s); StSliderShow(v, key, s); };
    }
}

// 눌러 끌면 가로 위치(0~1)를 알려 주는 칸
public class GenDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public System.Action<float> onFrac;
    public float pad;
    void Report(PointerEventData e)
    {
        var rt = (RectTransform)transform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp)) return;
        float w = rt.rect.width - pad * 2f;
        float x = lp.x - rt.rect.xMin - pad;
        onFrac?.Invoke(Mathf.Clamp01(w > 0f ? x / w : 0f));
    }
    public void OnPointerDown(PointerEventData e) => Report(e);
    public void OnDrag(PointerEventData e) => Report(e);
}
