using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Leaderboards;
using Newtonsoft.Json;
using Unity.Services.Leaderboards.Models;
using TMPro;
using UnityEngine.UI;

public class LeaderBoardManager : MonoBehaviour
{
    public static LeaderBoardManager Instance;

    const string LeaderboardInfiniteStage = "LeaderBoard_InfiniteStage";
    const string LeaderboardTotalDamage = "LeaderBoard_TotalDamage";

    [Header("Infinite Stage Board")]
    public Transform ParentInfiniteStage;
    public Transform ParentTotalDamage;
    public LeaderBoardData BoardData_pfb;
    public List<LeaderBoardData> BoardDatas;
    public List<LeaderBoardData> BoardDatas2;

    [Header("1,2,3 Place UI")]
    public TextMeshProUGUI FirstPlace_UserName;
    public TextMeshProUGUI FirstPlace_UserScore;
    public TextMeshProUGUI SecondPlace_UserName;
    public TextMeshProUGUI SecondPlace_UserScore;
    public TextMeshProUGUI ThirdPlace_UserName;
    public TextMeshProUGUI ThirdPlace_UserScore;

    [Header("1,2,3 Place UI")]
    public TextMeshProUGUI FirstPlaceTotalDamage_UserName;
    public TextMeshProUGUI FirstPlaceTotalDamage_UserScore;
    public TextMeshProUGUI SecondPlaceTotalDamage_UserName;
    public TextMeshProUGUI SecondPlaceTotalDamage_UserScore;
    public TextMeshProUGUI ThirdPlaceTotalDamage_UserName;
    public TextMeshProUGUI ThirdPlaceTotalDamage_UserScore;

    [Header("My Score")]
    public TextMeshProUGUI MyUserRank_InfiniteStage;
    public TextMeshProUGUI MyUserName_InfiniteStage;
    public TextMeshProUGUI MyUserScore_InfiniteStage;

    [Header("My Score")]
    public TextMeshProUGUI MyUserRank_TotalDamage;
    public TextMeshProUGUI MyUserName_TotalDamage;
    public TextMeshProUGUI MyUserScore_TotalDamage;
    bool duplication;

    [Header("ScrollRect")]
    public ScrollRect scrollRectInfiniteStage;
    public RectTransform contentPanelInfiniteStage;
    public ScrollRect scrollRectTotalDamage;
    public RectTransform contentPanelTotalDamage;
    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }

    public async void AddInfiniteStageScore()
    {
        try {
            var scoreResponse = await ServiceHub.Leaderboards.AddPlayerScoreAsync(LeaderboardInfiniteStage, InfiniteStage.Instance.CurrentStage);
            setLeaderBoard();
            snapToMyScoreInfiniteStage();
        } catch(System.Exception e) {
            Debug.LogWarning("[LeaderBoard] stage score failed: " + e.Message);
        }
    }

    public async void AddTotalDamageScore(long score)
    {
        try {
            var scoreResponse = await ServiceHub.Leaderboards.AddPlayerScoreAsync(LeaderboardTotalDamage, score);
            setLeaderBoard();
            snapToMyScoreTotalDamage();
        } catch(System.Exception e) {
            Debug.LogWarning("[LeaderBoard] power score failed: " + e.Message);
        }
    }

    // 닉네임 중복 확인: 두 랭킹에 올라간 다른 플레이어 이름(# 앞부분)과 대소문자 무시하고 비교해요.
    // 랭킹에 점수가 없는 사람은 확인할 방법이 없어요 (Unity 이름 뒤에 #숫자가 붙어서 실제 계정은 겹치지 않아요).
    // 네트워크 오류면 막지 않고 통과시켜요.
    public static async System.Threading.Tasks.Task<bool> IsNameTaken(string name)
    {
        string key = name.ToLowerInvariant();
        string me = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        try {
            foreach(var board in new[] { LeaderboardInfiniteStage, LeaderboardTotalDamage }) {
                for(int offset = 0; offset < 5000; offset += 1000) {
                    var page = await ServiceHub.Leaderboards.GetScoresAsync(board, new GetScoresOptions() { Offset = offset, Limit = 1000 });
                    if(page?.Results == null) break;
                    foreach(var e in page.Results) {
                        if(e.PlayerId != me && UserNameManager.Display(e.PlayerName).ToLowerInvariant() == key)
                            return true;
                    }
                    if(page.Results.Count < 1000) break;
                }
            }
        } catch(System.Exception e) {
            Debug.LogWarning("[LeaderBoard] 닉네임 중복 확인 실패(통과 처리): " + e.Message);
        }
        return false;
    }

    public void InitializeLeaderBoardInfiniteStage()
    {
        for(int i = 0; i < 1; i++) {
            LeaderBoardData obInstance = null;
            obInstance = Instantiate(BoardData_pfb, ParentInfiniteStage);
            obInstance.gameObject.SetActive(false);
            obInstance.typeOfData = LeaderBoardData.BoardType.InfiniteStage;
            BoardDatas.Add(obInstance);
        }
    }

    public void activateLeaderBoardDataInifniteStage(int rank, string name, string score)
    {
        if(rank >= BoardDatas.Count) {
            InitializeLeaderBoardInfiniteStage();
            activateLeaderBoardDataInifniteStage(rank, name, score);
            return;
        }
        BoardDatas[rank].gameObject.SetActive(true);
        BoardDatas[rank].setUserData((rank + 1).ToString("N0"), name, score);
    }

    public void InitializeLeaderBoardTotalDamage()
    {
        for(int i = 0; i < 1; i++) {
            LeaderBoardData obInstance = null;
            obInstance = Instantiate(BoardData_pfb, ParentTotalDamage);
            obInstance.gameObject.SetActive(false);
            obInstance.typeOfData = LeaderBoardData.BoardType.TotalDamage;
            BoardDatas2.Add(obInstance);
        }
    }

    public void activateLeaderBoardDataTotalDamage(int rank, string name, string score)
    {
        if(rank >= BoardDatas2.Count) {
            InitializeLeaderBoardTotalDamage();
            activateLeaderBoardDataTotalDamage(rank, name, score);
            return;
        }
        BoardDatas2[rank].gameObject.SetActive(true);
        BoardDatas2[rank].setUserData((rank + 1).ToString("N0"), name, score);
    }

    public async void setLeaderBoard()
    {
        if(PlayerPrefs.GetInt("username") == 0) {
            return;
        }

        //Infinite Stage
        var scoresResponse =
      await ServiceHub.Leaderboards.GetScoresAsync(LeaderboardInfiniteStage, new GetScoresOptions() { Limit = 1000 });

        if(scoresResponse.Results != null && scoresResponse.Results.Count > 0) {
            FirstPlace_UserName.gameObject.SetActive(true);
            FirstPlace_UserScore.gameObject.SetActive(true);
            setFirstPlaceInfiniteStage(scoresResponse.Results[0].PlayerName, scoresResponse.Results[0].Score.ToString("N0"));
        } else {
            FirstPlace_UserName.gameObject.SetActive(false);
            FirstPlace_UserScore.gameObject.SetActive(false);
        }

        if(scoresResponse.Results.Count > 1) {
            SecondPlace_UserName.gameObject.SetActive(true);
            SecondPlace_UserScore.gameObject.SetActive(true);
            setSecondPlaceInfiniteStage(scoresResponse.Results[1].PlayerName, scoresResponse.Results[1].Score.ToString("N0"));
        } else {
            SecondPlace_UserName.gameObject.SetActive(false);
            SecondPlace_UserScore.gameObject.SetActive(false);
        }

        if(scoresResponse.Results.Count > 2) {
            ThirdPlace_UserName.gameObject.SetActive(true);
            ThirdPlace_UserScore.gameObject.SetActive(true);
            setThirdPlaceInfiniteStage(scoresResponse.Results[2].PlayerName, scoresResponse.Results[2].Score.ToString("N0"));
        } else {
            ThirdPlace_UserName.gameObject.SetActive(false);
            ThirdPlace_UserScore.gameObject.SetActive(false);
        }

        for(int i = 3; i < scoresResponse.Results.Count; i++) {
            if(UserNameManager.Display(scoresResponse.Results[i].PlayerName).Length > 0)
                activateLeaderBoardDataInifniteStage(i, scoresResponse.Results[i].PlayerName, scoresResponse.Results[i].Score.ToString("N0"));
        }

        setMyScoreInfiniteStage();

        //Total Damage
        var scoresTotalDamage =
          await ServiceHub.Leaderboards.GetScoresAsync(LeaderboardTotalDamage, new GetScoresOptions() { Limit = 1000 });

        if(scoresTotalDamage.Results != null && scoresTotalDamage.Results.Count > 0) {
            FirstPlace_UserName.gameObject.SetActive(true);
            FirstPlace_UserScore.gameObject.SetActive(true);
            setFirstPlaceTotalDamage(scoresTotalDamage.Results[0].PlayerName, scoresTotalDamage.Results[0].Score.ToString("N0"));
        } else {
            FirstPlace_UserName.gameObject.SetActive(false);
            FirstPlace_UserScore.gameObject.SetActive(false);
        }

        if(scoresTotalDamage.Results.Count > 1) {
            SecondPlace_UserName.gameObject.SetActive(true);
            SecondPlace_UserScore.gameObject.SetActive(true);
            setSecondPlaceTotalDamage(scoresTotalDamage.Results[1].PlayerName, scoresTotalDamage.Results[1].Score.ToString("N0"));
        } else {
            SecondPlace_UserName.gameObject.SetActive(false);
            SecondPlace_UserScore.gameObject.SetActive(false);
        }

        if(scoresTotalDamage.Results.Count > 2) {
            ThirdPlace_UserName.gameObject.SetActive(true);
            ThirdPlace_UserScore.gameObject.SetActive(true);
            setThirdPlaceTotalDamage(scoresTotalDamage.Results[2].PlayerName, scoresTotalDamage.Results[2].Score.ToString("N0"));
        } else {
            ThirdPlace_UserName.gameObject.SetActive(false);
            ThirdPlace_UserScore.gameObject.SetActive(false);
        }

        for(int i = 3; i < scoresTotalDamage.Results.Count; i++) {
            if(UserNameManager.Display(scoresTotalDamage.Results[i].PlayerName).Length > 0)
                activateLeaderBoardDataTotalDamage(i, scoresTotalDamage.Results[i].PlayerName, scoresTotalDamage.Results[i].Score.ToString("N0"));
        }

        setMyScoreTotalDamage();
    }

    public async void snapToMyScoreInfiniteStage()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform data = null;
        var myScore = await ServiceHub.Leaderboards.GetPlayerScoreAsync(LeaderboardInfiniteStage);

        string myName = UserNameManager.Display(myScore.PlayerName);

        for(int i = 0; i < BoardDatas.Count; i++) {
            BoardDatas[i].Focus.SetActive(false);
        }

        for(int i = 0; i < BoardDatas.Count; i++) {
            if(BoardDatas[i].UserName.text == myName) {
                BoardDatas[i].Focus.SetActive(true);
                data = BoardDatas[i].gameObject.GetComponent<RectTransform>();
                break;
            }
        }

        if(data != null) {
            Vector2 dataPosition = new Vector2(data.position.x, data.position.y + 300);

            contentPanelInfiniteStage.anchoredPosition =
                    (Vector2)scrollRectInfiniteStage.transform.InverseTransformPoint(contentPanelInfiniteStage.position)
                    - (Vector2)scrollRectInfiniteStage.transform.InverseTransformPoint(dataPosition);
        }
    }

    public async void snapToMyScoreTotalDamage()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform data = null;
        var myScore = await ServiceHub.Leaderboards.GetPlayerScoreAsync(LeaderboardTotalDamage);

        string myName = UserNameManager.Display(myScore.PlayerName);

        for(int i = 0; i < BoardDatas2.Count; i++) {
            BoardDatas2[i].Focus.SetActive(false);
        }

        for(int i = 0; i < BoardDatas2.Count; i++) {
            if(BoardDatas2[i].UserName.text == myName) {
                BoardDatas2[i].Focus.SetActive(true);
                data = BoardDatas2[i].gameObject.GetComponent<RectTransform>();
                break;
            }
        }

        if(data != null) {
            Vector2 dataPosition = new Vector2(data.position.x, data.position.y + 300);

            contentPanelTotalDamage.anchoredPosition =
                    (Vector2)scrollRectTotalDamage.transform.InverseTransformPoint(contentPanelTotalDamage.position)
                    - (Vector2)scrollRectTotalDamage.transform.InverseTransformPoint(dataPosition);
        }
    }

    public void setFirstPlaceInfiniteStage(string name, string score)
    {
        FirstPlace_UserName.text = UserNameManager.Display(name);
        FirstPlace_UserScore.text = "스테이지 " + score;
    }

    public void setSecondPlaceInfiniteStage(string name, string score)
    {
        SecondPlace_UserName.text = UserNameManager.Display(name);
        SecondPlace_UserScore.text = "스테이지 " + score;
    }

    public void setThirdPlaceInfiniteStage(string name, string score)
    {
        ThirdPlace_UserName.text = UserNameManager.Display(name);
        ThirdPlace_UserScore.text = "스테이지 " + score;
    }

    public void setFirstPlaceTotalDamage(string name, string score)
    {
        FirstPlaceTotalDamage_UserName.text = UserNameManager.Display(name);
        FirstPlaceTotalDamage_UserScore.text = score;
    }

    public void setSecondPlaceTotalDamage(string name, string score)
    {
        SecondPlaceTotalDamage_UserName.text = UserNameManager.Display(name);
        SecondPlaceTotalDamage_UserScore.text = score;
    }

    public void setThirdPlaceTotalDamage(string name, string score)
    {
        ThirdPlaceTotalDamage_UserName.text = UserNameManager.Display(name);
        ThirdPlaceTotalDamage_UserScore.text = score;
    }

    public async void setMyScoreInfiniteStage()
    {
        var scoreResponse =
          await ServiceHub.Leaderboards.GetPlayerScoreAsync(LeaderboardInfiniteStage);

        MyUserRank_InfiniteStage.text = (scoreResponse.Rank + 1).ToString("N0");
        MyUserName_InfiniteStage.text = UserNameManager.Display(scoreResponse.PlayerName);
        MyUserScore_InfiniteStage.text = "스테이지 " + scoreResponse.Score.ToString("N0");
    }

    public async void setMyScoreTotalDamage()
    {
        var scoreResponse =
          await ServiceHub.Leaderboards.GetPlayerScoreAsync(LeaderboardTotalDamage);

        MyUserRank_TotalDamage.text = (scoreResponse.Rank + 1).ToString("N0");
        MyUserName_TotalDamage.text = UserNameManager.Display(scoreResponse.PlayerName);
        MyUserScore_TotalDamage.text = scoreResponse.Score.ToString("N0");
    }

    public async void GetPlayerScore()
    {
        var scoreResponse =
            await ServiceHub.Leaderboards.GetPlayerScoreAsync(LeaderboardInfiniteStage);
        Debug.Log(JsonConvert.SerializeObject(scoreResponse));
    }

}
