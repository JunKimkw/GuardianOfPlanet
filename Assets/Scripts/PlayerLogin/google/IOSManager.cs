using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

// 애플 로그인 (iOS).
// - 버튼: 애플 로그인 창 → identity token → Unity 로그인.
// - 자동: 보통은 AuthManager 가 세션 토큰으로 처리해요. 토큰이 없어졌는데 예전에 애플로 로그인했던 기기면 QuickLogin 으로 조용히 다시 시도해요.
// - 안드로이드에서는 버튼을 숨겨요. 에디터에서는 게스트로 대신 들어가요.
public class IOSManager : MonoBehaviour
{
    public string Token { get; private set; }
    public string Error { get; private set; }
    public bool isLogining;
    public GameObject LoginButton;

    const string PrefKey = "IOSLogIn";

#if UNITY_IOS && !UNITY_EDITOR
    IAppleAuthManager m_AppleAuthManager;
#endif

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if(LoginButton != null) LoginButton.SetActive(false);
#elif UNITY_IOS && !UNITY_EDITOR
        if(AppleAuthManager.IsCurrentPlatformSupported)
            m_AppleAuthManager = new AppleAuthManager(new PayloadDeserializer());
        if(LoginButton != null) LoginButton.SetActive(m_AppleAuthManager != null);
        if(m_AppleAuthManager != null && !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey))) {
            AuthManager.ExternalAutoLoginRunning = true;
            StartCoroutine(SilentLoginAfterSession());
        }
#else
        // 에디터: 안드로이드 타깃이면 구글 버튼만 보이게 (게스트 대체 로그인은 구글 버튼이 담당)
        if(LoginButton != null) LoginButton.SetActive(false);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    void Update()
    {
        m_AppleAuthManager?.Update();
    }

    IEnumerator SilentLoginAfterSession()
    {
        float t = 0f;
        while(AuthManager.AutoLoginRunning && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
        if(AuthenticationService.Instance.IsSignedIn) { AuthManager.ExternalAutoLoginRunning = false; yield break; }

        bool done = false;
        m_AppleAuthManager.QuickLogin(new AppleAuthQuickLoginArgs(),
            credential => { SignInWithApple(credential, false).ContinueWith(task => done = true); },
            error => { Debug.Log("[Apple] 조용한 재로그인 실패"); done = true; });
        t = 0f;
        while(!done && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        AuthManager.ExternalAutoLoginRunning = false;
    }
#endif

    // 애플 로그인 버튼 (씬에서 연결됨)
    public async void SignInButtonClicked()
    {
        isLogining = true;
#if UNITY_IOS && !UNITY_EDITOR
        LoginToApple();
#else
        Debug.Log("[Apple] 에디터에서는 애플 로그인이 안 돼서 게스트로 로그인해요.");
        await AuthManager.SignInGuest(true);
#endif
    }

    public void SignIn() => SignInButtonClicked();

    public void LoginToApple()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if(m_AppleAuthManager == null) {
            AuthManager.ShowError("이 기기에서는 애플 로그인을 쓸 수 없어요.");
            return;
        }
        var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);
        m_AppleAuthManager.LoginWithAppleId(loginArgs,
            credential => { _ = SignInWithApple(credential, true); },
            error => {
                Error = error != null ? error.GetAuthorizationErrorCode().ToString() : "unknown";
                Debug.Log("[Apple] 로그인 실패: " + Error);
                AuthManager.ShowError("애플 로그인이 취소됐거나 실패했어요.");
            });
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    async Task SignInWithApple(ICredential credential, bool interactive)
    {
        var apple = credential as IAppleIDCredential;
        if(apple == null || apple.IdentityToken == null) {
            if(interactive) AuthManager.ShowError("애플 로그인 정보를 받지 못했어요.");
            return;
        }
        Token = Encoding.UTF8.GetString(apple.IdentityToken, 0, apple.IdentityToken.Length);

        if(!AuthManager.TryBeginSignIn()) return;
        bool ok = false;
        try {
            await ServiceHub.EnsureInitialized();
            var auth = AuthenticationService.Instance;
            if(auth.IsSignedIn) {
                try {
                    await auth.LinkWithAppleAsync(Token);
                } catch(AuthenticationException e) when(e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) {
                    AuthManager.ShowError("이 애플 계정은 이미 다른 진행 기록과 연결돼 있어요.");
                    return;
                }
            } else {
                await auth.SignInWithAppleAsync(Token);
            }
            PlayerPrefs.SetString(PrefKey, "1");
            ok = true;
            Debug.Log("[Apple] 애플 로그인 성공");
        } catch(Exception e) {
            Debug.LogException(e);
            if(interactive) AuthManager.ShowError("애플 로그인에 실패했어요. 잠시 후 다시 시도해 주세요.");
        } finally {
            AuthManager.EndSignIn();
        }
        if(ok) await AuthManager.CompleteSignIn(interactive);
    }
#endif
}
