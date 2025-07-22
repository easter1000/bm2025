using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 게임의 전체적인 흐름(경기 시작, 종료, 씬 전환)을 관리합니다.
/// gamelogic_test 씬에 빈 게임 오브젝트를 만들고 이 스크립트를 추가해야 합니다.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    void OnEnable()
    {
        // GameSimulator가 게임 종료 이벤트를 발생시키면, OnGameSimulationFinished 메서드를 호출하도록 등록
        GameSimulator.OnGameFinished += OnGameSimulationFinished;
    }

    void OnDisable()
    {
        // 오브젝트가 비활성화되거나 파괴될 때 등록을 해제하여 메모리 누수 방지
        GameSimulator.OnGameFinished -= OnGameSimulationFinished;
    }

    private void OnGameSimulationFinished(GameResult userGameResult)
    {
        Debug.Log("게임 시뮬레이션 종료. 후처리 작업을 시작합니다.");

        Schedule finishedGameInfo = GameDataHolder.CurrentGameInfo;

        // 1. 끝난 유저 경기 결과 저장
        Debug.Log($"[GameFlowManager] Saving user game result for {finishedGameInfo.GameId}.");
        SaveGameResult(finishedGameInfo, userGameResult);
        
        // 2. 오늘 있었던 나머지 AI 경기들을 시뮬레이션하고 결과 저장
        Debug.Log("[GameFlowManager] Simulating remaining AI games for the day.");
        string userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;
        List<Schedule> allGamesToday = LocalDbManager.Instance.GetGamesForDate(finishedGameInfo.GameDate);

        var remainingAiGames = allGamesToday
            .Where(g => g.GameId != finishedGameInfo.GameId && g.GameStatus == "Scheduled")
            .ToList();

        if (remainingAiGames.Count > 0)
        {
            BackgroundGameSimulator simulator = new BackgroundGameSimulator();
            foreach (var game in remainingAiGames)
            {
                Debug.Log($" - Simulating: {game.AwayTeamAbbr} at {game.HomeTeamAbbr}");
                var result = simulator.SimulateFullGame(game);
                SaveGameResult(game, result);
            }
        }
        else
        {
            Debug.Log("[GameFlowManager] No other AI games to simulate today.");
        }


        // 3. 날짜를 하루 진행
        LocalDbManager.Instance.AdvanceUserDate();
        
        // 4. AI 팀들의 트레이드 시도
        if (SeasonManager.Instance != null)
        {
            SeasonManager.Instance.AttemptAiToAiTrades();
        }

        // 5. 시즌 씬으로 돌아가기
        Debug.Log("모든 작업 완료. SeasonScene으로 돌아갑니다.");
        SceneManager.LoadScene("SeasonScene");
    }

    /// <summary>
    /// 경기 결과를 DB에 저장하는 헬퍼 메서드
    /// </summary>
    private void SaveGameResult(Schedule game, GameResult result)
    {
        var db = LocalDbManager.Instance;
        // 플레이어 스탯 저장
        if (result.PlayerStats != null && result.PlayerStats.Count > 0)
        {
            db.InsertPlayerStats(result.PlayerStats);
        }
        // 게임 결과 업데이트
        db.UpdateGameResult(game.GameId, result.HomeScore, result.AwayScore);
        // 팀 승패 기록 업데이트
        db.UpdateTeamWinLossRecord(game.HomeTeamAbbr, result.HomeScore > result.AwayScore, game.Season);
        db.UpdateTeamWinLossRecord(game.AwayTeamAbbr, result.AwayScore > result.HomeScore, game.Season);
    }
} 