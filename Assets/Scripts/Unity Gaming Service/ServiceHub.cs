using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;

// Unity 서비스 접근을 한 곳에서.
// LeaderboardsService.Instance / CloudSaveService.Instance 는 에디터에서 씬 로드 직후 패키지가 null로 지워 버려서
// ("has not been initialized" 오류) 서비스 레지스트리에서 직접 꺼내 써요. 빌드에서도 똑같이 동작해요.
public static class ServiceHub
{
    public static ILeaderboardsService Leaderboards => UnityServices.Instance.GetLeaderboardsService();
    public static ICloudSaveService CloudSave => UnityServices.Instance.GetCloudSaveService();

    public static bool Ready => UnityServices.State == ServicesInitializationState.Initialized;

    public static Task EnsureInitialized()
    {
        if(Ready) return Task.CompletedTask;
        return UnityServices.InitializeAsync();
    }
}
