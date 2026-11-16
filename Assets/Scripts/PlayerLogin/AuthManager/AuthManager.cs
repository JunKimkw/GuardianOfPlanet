using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

// 로그인 공통 처리.
// - 자동 로그인: 한 번 로그인한 기기에는 Unity 세션 토큰이 남아 있어서, 게스트/구글/애플 상관없이 그 토큰으로 다시 들어가요.
// - 버튼 로그인(게스트/구글/애플)은 성공하면 모두 CompleteSignIn 으로 모여요.
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;

    // ★ 테스트용 스위치: 에디터에서 로그인·약관·닉네임 화면을 건너뛰어요.
    //   실제 흐름을 확인하려면 false 로 바꾸세요. 빌드(폰)에서는 항상 false 예요.
#if UNITY_EDITOR
    public const bool SkipLoginForTest = true;
#else
    public const bool SkipLoginForTest = false;
#endif

    public bool isLogining;

    // 시작 화면이 로딩을 끝낸 뒤 자동 로그인 결과를 기다릴 때 써요
    public static bool AutoLoginRunning { get; private set; }
    public static bool ExternalAutoLoginRunning;   // 구글/애플 조용한 재로그인 중
    public static bool AnyAutoLoginRunning => AutoLoginRunning || ExternalAutoLoginRunning;
    static bool signInBusy;

    async void Awake()
    {
        Instance = this;
        AutoLoginRunning = true;
        signInBusy = false;
        try {
            await ServiceHub.EnsureInitialized();
            await CheckAutoLogIn();
        } catch(Exception e) {
            Debug.LogWarning("[Auth] 자동 로그인 실패: " + e.Message);
        } finally {
            AutoLoginRunning = false;
        }
    }

    public async Task CheckAutoLogIn()
    {
        var auth = AuthenticationService.Instance;
        if(auth.IsSignedIn) {
            await CompleteSignIn(false);
            return;
        }
        if(!auth.SessionTokenExists) {
            // 테스트용: 에디터에서는 약관·로그인 화면 없이 바로 게스트로 들어가요
            if(SkipLoginForTest) await SignInGuest(false);
            return;
        }

        try {
            // 세션 토큰이 있으면 이 호출은 새 게스트를 만들지 않고 "그 계정"(게스트/구글/애플)으로 다시 들어가요
            await auth.SignInAnonymouslyAsync();
        } catch(Exception e) {
            // 토큰이 만료되거나 깨졌으면 지우고 로그인 화면을 보여 줘요 (구글/애플 버튼으로 같은 계정 복구 가능)
            Debug.LogWarning("[Auth] 저장된 세션으로 로그인 실패, 토큰 삭제: " + e.Message);
            auth.ClearSessionToken();
            return;
        }
        await CompleteSignIn(false);
    }

    // 게스트 로그인 버튼 (씬에서 연결됨)
    public async void SignIn()
    {
        isLogining = true;
        await SignInGuest(true);
    }

    public static async Task SignInGuest(bool fromButton)
    {
        if(signInBusy) return;
        signInBusy = true;
        try {
            await ServiceHub.EnsureInitialized();
            if(!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            PlayerPrefs.SetString("GuestLogIn", "true");
            Debug.Log("[Auth] 게스트 로그인 성공");
        } catch(Exception e) {
            Debug.LogException(e);
            ShowError("로그인에 실패했어요. 인터넷 연결을 확인해 주세요.");
            return;
        } finally {
            signInBusy = false;
        }
        await CompleteSignIn(fromButton);
    }

    // 구글/애플처럼 외부 계정으로 로그인할 때 중복 클릭 막기
    public static bool TryBeginSignIn()
    {
        if(signInBusy) return false;
        signInBusy = true;
        return true;
    }
    public static void EndSignIn() => signInBusy = false;

    // 어떤 방식이든 로그인에 성공하면 여기로 와요
    public static async Task CompleteSignIn(bool fromButton)
    {
        var auth = AuthenticationService.Instance;
        if(!auth.IsSignedIn) return;

        var start = StartScreenScript.Instance;
        bool already = start != null && start.isSignedIn;

        if(Player.instance != null) Player.instance.PlayerID = auth.PlayerId;
        await UserNameManager.FetchServerName();

        if(!already) {
            if(start != null) start.isSignedIn = true;
            DataPersistenceManager.instance.LoadGame();
            Debug.Log("[Auth] 로그인 완료 " + auth.PlayerId + " / " + UserNameManager.ServerName);
        }

        if(fromButton && start != null)
            start.checkIfSignedIn();
    }

    public static void ShowError(string msg)
    {
        Debug.LogWarning("[Auth] " + msg);
        var ui = FindFirstObjectByType<RemasterUI>();
        if(ui != null) ui.Toast(msg, new Color(1f, 0.6f, 0.55f));
    }
}
