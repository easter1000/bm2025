using UnityEngine;
using UnityEngine.SceneManagement;

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

    private void OnGameSimulationFinished(GameSimulator.GameResult result)
    {
        Debug.Log("게임 시뮬레이션 종료 이벤트를 수신했습니다. 후처리 작업을 시작합니다.");

        // 1. 경기 결과 저장
        LocalDbManager.Instance.InsertPlayerStats(result.PlayerStats);
        LocalDbManager.Instance.UpdateGameResult(
            GameDataHolder.CurrentGameInfo.GameId,
            result.HomeScore,
            result.AwayScore
        );
        LocalDbManager.Instance.UpdateTeamWinLossRecord(
            GameDataHolder.CurrentGameInfo.HomeTeamAbbr,
            result.HomeScore > result.AwayScore,
            GameDataHolder.CurrentGameInfo.Season
        );
        LocalDbManager.Instance.UpdateTeamWinLossRecord(
            GameDataHolder.CurrentGameInfo.AwayTeamAbbr,
            result.AwayScore > result.HomeScore,
            GameDataHolder.CurrentGameInfo.Season
        );

        // 2. 날짜를 하루 진행
        LocalDbManager.Instance.AdvanceUserDate();
        
        // 3. AI 팀들의 트레이드 시도
        // SeasonManager 인스턴스가 없을 수도 있으므로 null 체크
        if (SeasonManager.Instance != null)
        {
            SeasonManager.Instance.AttemptAiToAiTrades();
        }

        // 4. 시즌 씬으로 돌아가기
        Debug.Log("모든 작업 완료. SeasonScene으로 돌아갑니다.");
        SceneManager.LoadScene("SeasonScene");
    }
} 