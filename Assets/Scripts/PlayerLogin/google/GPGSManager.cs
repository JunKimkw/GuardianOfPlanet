#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

// 구글 플레이 게임즈 로그인.
// - 버튼: 구글 계정 선택 창 → 서버 인증 코드 → Unity 로그인.
// - 자동: 보통은 AuthManager 가 세션 토큰으로 처리해요. 토큰이 없어졌는데 예전에 구글로 로그인했던 기기면 조용히 구글 로그인을 다시 시도해요.
// - 에디터: 구글 로그인이 안 되니까 게스트로 대신 들어가요 (테스트용).
public class GPGSManager : MonoBehaviour
{
    public string Token;
    public bool isLogining;
    public GameObject LoginButton;

    const string PrefKey = "GoogleLogIn";

    void Awake()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if(LoginButton != null) LoginButton.SetActive(false);
#else
        if(LoginButton != null) LoginButton.SetActive(true);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.Activate();
        if(!string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey))) {
            AuthManager.ExternalAutoLoginRunning = true;
            StartCoroutine(SilentLoginAfterSession());
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator SilentLoginAfterSession()
    {
        // 세션 토큰 자동 로그인이 먼저 끝나길 기다려요
        float t = 0f;
        while(AuthManager.AutoLoginRunning && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
        if(AuthenticationService.Instance.IsSignedIn) { AuthManager.ExternalAutoLoginRunning = false; yield break; }
        var task = SignInWithGoogle(false);
        while(!task.IsCompleted) yield return null;
        AuthManager.ExternalAutoLoginRunning = false;
    }
#endif

    // 구글 로그인 버튼 (씬에서 연결됨)
    public async void SignInButtonClicked()
    {
        isLogining = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        await SignInWithGoogle(true);
#else
        Debug.Log("[GPGS] 에디터에서는 구글 로그인이 안 돼서 게스트로 로그인해요.");
        await AuthManager.SignInGuest(true);
#endif
    }

    // 예전 씬 연결 호환용
    public void SignIn() => SignInButtonClicked();

#if UNITY_ANDROID && !UNITY_EDITOR
    async Task SignInWithGoogle(bool interactive)
    {
        if(!AuthManager.TryBeginSignIn()) return;
        bool ok = false;
        try {
            await ServiceHub.EnsureInitialized();
            string code = await GetServerAuthCode(interactive);
            if(string.IsNullOrEmpty(code)) {
                if(interactive) AuthManager.ShowError("구글 로그인이 취소됐거나 실패했어요.");
                return;
            }
            Token = code;

            var auth = AuthenticationService.Instance;
            if(auth.IsSignedIn) {
                // 이미 게스트로 들어와 있으면 그 계정에 구글을 연결해요 (진행 상황 유지)
                try {
                    await auth.LinkWithGooglePlayGamesAsync(code);
                } catch(AuthenticationException e) when(e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) {
                    AuthManager.ShowError("이 구글 계정은 이미 다른 진행 기록과 연결돼 있어요.");
                    return;
                }
            } else {
                await auth.SignInWithGooglePlayGamesAsync(code);
            }
            PlayerPrefs.SetString(PrefKey, "1");
            ok = true;
            Debug.Log("[GPGS] 구글 로그인 성공");
        } catch(Exception e) {
            Debug.LogException(e);
            if(interactive) AuthManager.ShowError("구글 로그인에 실패했어요. 잠시 후 다시 시도해 주세요.");
        } finally {
            AuthManager.EndSignIn();
        }
        if(ok) await AuthManager.CompleteSignIn(interactive);
    }

    static Task<string> GetServerAuthCode(bool interactive)
    {
        var tcs = new TaskCompletionSource<string>();
        Action<SignInStatus> after = status => {
            if(status != SignInStatus.Success) {
                Debug.Log("[GPGS] 구글 인증 결과: " + status);
                tcs.TrySetResult(null);
                return;
            }
            PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.TrySetResult(code));
        };
        if(interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(s => after(s));
        else PlayGamesPlatform.Instance.Authenticate(s => after(s));
        return tcs.Task;
    }
#endif
}
