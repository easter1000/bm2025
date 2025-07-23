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

        // 끝난 유저 경기 결과 저장
        Debug.Log($"[GameFlowManager] Saving user game result for {finishedGameInfo.GameId}.");
        SaveGameResult(finishedGameInfo, userGameResult);

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