using System.Linq;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;

// 랭킹 창 A (2026-11-26 사용자 "랭킹은 A", 미리보기 그대로 = UIGen/ranking)
// - 예전 랭킹 페이지(Canvas/Tabs Above Buttons/LeaderBoards)가 열리면 그 위를 덮어요
// - 순위 = 스테이지 리더보드 (예전 '무한 스테이지' 판, LeaderBoard_InfiniteStage). 1~3위 단상, 4~8위 줄, 맨 아래 내 순위
// - 불러오는 중·로그인 안 됨은 이름 칸에 표시. 창을 열 때마다 새로 불러와요
public partial class RemasterUI
{
    GenView rkView;
    GameObject rkPage;
    const string RkBoard = "LeaderBoard_InfiniteStage";

    void UpdateRankingPage()
    {
        if(rkPage == null) { rkPage = GenFindPage("LeaderBoards", "Tabs Above Buttons"); if(rkPage == null) return; }
        if(!rkPage.activeInHierarchy) { if(rkView != null) { GenClose(rkView); rkView = null; } return; }
        if(rkView == null || rkView.layer == null) BuildRanking();
    }

    void BuildRanking()
    {
        rkView = GenBuild("ranking", "RankingA", true, (RectTransform)rkPage.transform);
        if(rkView == null) return;
        var v = rkView;
        GenHit(v, "close", () => GenPageBack(rkPage));
        // 불러오기 전: 빈 칸
        for(int k = 0; k < 3; k++) { GenText(v, "p" + k + ".name", k == 0 ? "불러오는 중" : "-"); GenText(v, "p" + k + ".score", ""); }
        for(int k = 0; k < 5; k++) GenShow(v, "r" + k, false);
        GenText(v, "me.rank", "-"); GenText(v, "me.name", "나"); GenText(v, "me.score", "");
        GenText(v, "note", "오른 스테이지로 순위를 매겨요 · 1,000위까지");
        LoadRanking(v);
    }

    async void LoadRanking(GenView v)
    {
        bool listDone = false;   // 순위 목록을 받았는지 (못 받았으면 1위 칸에 실패를 보여요 — 2026-11-27 점검: '불러오는 중'에 멈춰 있었어요)
        try {
            if(!AuthenticationService.Instance.IsSignedIn) { GenText(v, "p0.name", "로그인이 필요해요"); return; }
            var res = await ServiceHub.Leaderboards.GetScoresAsync(RkBoard, new GetScoresOptions() { Limit = 8 });
            if(v == null || v.layer == null) return;   // 그사이 창을 닫았어요
            listDone = true;
            var list = res != null && res.Results != null ? res.Results : new System.Collections.Generic.List<Unity.Services.Leaderboards.Models.LeaderboardEntry>();
            for(int k = 0; k < 3; k++) {
                bool on = k < list.Count;
                GenTextFit(v, "p" + k + ".name", on ? UserNameManager.Display(list[k].PlayerName) : "-", 104f);
                GenText(v, "p" + k + ".score", on ? "Stage " + list[k].Score.ToString("N0") : "");
            }
            for(int k = 0; k < 5; k++) {
                int i = k + 3;
                bool on = i < list.Count;
                GenShow(v, "r" + k, on);
                if(!on) continue;
                GenText(v, "r" + k + ".rank", (i + 1).ToString());
                GenTextFit(v, "r" + k + ".name", UserNameManager.Display(list[i].PlayerName), 150f);
                GenText(v, "r" + k + ".score", "Stage " + list[i].Score.ToString("N0"));
            }
            var me = await ServiceHub.Leaderboards.GetPlayerScoreAsync(RkBoard);
            if(v == null || v.layer == null || me == null) return;
            GenText(v, "me.rank", (me.Rank + 1).ToString("N0"));
            GenTextFit(v, "me.name", UserNameManager.Display(me.PlayerName), 150f);
            GenText(v, "me.score", "Stage " + me.Score.ToString("N0"));
        } catch(System.Exception e) {
            Debug.LogWarning("[랭킹] 불러오기 실패: " + e.Message);
            if(v != null && v.layer != null) {
                if(!listDone) GenTextFit(v, "p0.name", "불러오지 못했어요", 104f);
                else GenText(v, "me.name", "아직 순위가 없어요");   // 내 기록이 없으면 GetPlayerScore 가 실패해요
            }
        }
    }
}
