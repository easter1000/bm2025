using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class SeasonManager : MonoBehaviour
{
    public static SeasonManager Instance;

    private DateTime _currentDate;
    private QuickGameSimulator _quickSim;
    private string _userTeamAbbr;
    private int _currentSeason;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _quickSim = new QuickGameSimulator();
    }

    void OnEnable()
    {
        GameSimulator.OnGameFinished += HandleGameFinished;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameSimulator.OnGameFinished -= HandleGameFinished;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // --- 여기가 캘린더 생성의 시작점입니다 ---
    // UI의 "새 시즌 시작" 버튼 등에 이 함수를 연결합니다.
    public void StartNewSeason(int season)
    {
        Debug.Log($"--- Starting New Season Generation for {season} ---");
        _currentSeason = season;
        
        // 1. 유저 팀 정보를 가져옵니다. (LocalDbManager에 GetUser() 함수 필요)
        _userTeamAbbr = LocalDbManager.Instance.GetUser().SelectedTeamAbbr; 
        
        // 2. ScheduleManager를 생성하여 스케줄 생성을 지시합니다.
        var scheduleGenerator = new ScheduleManager();
        scheduleGenerator.GenerateNewSeasonSchedule(season);
        
        // 3. 게임의 현재 날짜를 시즌 시작일로 설정합니다.
        _currentDate = new DateTime(season, 10, 21);

        Debug.Log($"New season {season} has successfully started. Current date: {_currentDate:yyyy-MM-dd}");
        
        // 이 시점에서 리그 순위, 경기 일정 등 시즌 UI를 업데이트하는 함수를 호출할 수 있습니다.
        // UIManager.Instance.UpdateSeasonView();
    }

    // 다음 날 진행 (UI 버튼 등에 연결)
    public void SimulateNextDay()
    {
        string dateString = _currentDate.ToString("yyyy-MM-dd");
        var gamesToday = LocalDbManager.Instance.GetGamesForDate(dateString);

        if (gamesToday.Count == 0)
        {
            Debug.Log($"No games scheduled for {dateString}. Proceeding to next day.");
            ProceedToNextDay();
            return;
        }

        Debug.Log($"--- Simulating Day: {dateString}, {gamesToday.Count} games scheduled ---");

        foreach (var game in gamesToday)
        {
            // 유저 팀의 경기인지 확인
            if (game.HomeTeamAbbr == _userTeamAbbr || game.AwayTeamAbbr == _userTeamAbbr)
            {
                // 유저 경기 -> 상세 시뮬레이션 시작
                Debug.Log($"User game detected! {game.AwayTeamAbbr} at {game.HomeTeamAbbr}. Loading game scene...");
                StartDetailedSimulation(game);
                return; // 상세 시뮬레이션이 끝날 때까지 대기
            }
            else
            {
                // AI 경기 -> 빠른 시뮬레이션
                ProcessQuickSimulation(game);
            }
        }

        // 오늘의 모든 AI 경기가 끝났으면 다음 날로
        ProceedToNextDay();
    }

    private void ProcessQuickSimulation(Schedule game)
    {
        GameResult result = _quickSim.SimulateGame(game);
        UpdateDatabaseWithResult(game, result);
        Debug.Log($"[Quick Sim] Result: {game.AwayTeamAbbr} {result.AwayScore} @ {game.HomeTeamAbbr} {result.HomeScore}");
    }

    private void StartDetailedSimulation(Schedule game)
    {
        // 1. GameDataHolder에 경기 정보를 저장
        GameDataHolder.CurrentGameInfo = game;
        // 2. 게임 씬 로드
        SceneManager.LoadScene("GameScene"); // 게임 씬의 이름
    }

    // 상세 시뮬레이션이 끝났을 때 이벤트에 의해 호출되는 콜백 함수
    private void HandleGameFinished(GameResult result)
    {
        Debug.Log("Detailed simulation finished. Returning to season scene.");
        Schedule finishedGame = GameDataHolder.CurrentGameInfo;
        UpdateDatabaseWithResult(finishedGame, result);
        
        // 시즌 씬으로 복귀
        SceneManager.LoadScene("SeasonScene"); // 메인 시즌 씬의 이름
    }

    // 씬이 로드된 후 호출될 함수
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 시즌 씬으로 돌아왔을 때, 다음 날로 진행
        if (scene.name == "SeasonScene")
        {
             // 바로 다음날로 진행하면 UI를 볼 시간이 없으므로,
             // 이 로직은 SimulateNextDay 버튼을 다시 누르는 것으로 대체하거나,
             // 약간의 딜레이 후 자동 진행시킬 수 있음.
             // 여기서는 다음 날로 바로 진행하는 코드를 작성.
             ProceedToNextDay();
        }
    }

    // 공통 DB 업데이트 로직
    private void UpdateDatabaseWithResult(Schedule game, GameResult result)
    {
        LocalDbManager.Instance.UpdateGameResult(game.GameId, result.HomeScore, result.AwayScore);
        LocalDbManager.Instance.InsertPlayerStats(result.PlayerStats);
        LocalDbManager.Instance.UpdateTeamWinLossRecord(game.HomeTeamAbbr, result.HomeScore > result.AwayScore, _currentSeason);
        LocalDbManager.Instance.UpdateTeamWinLossRecord(game.AwayTeamAbbr, result.AwayScore > result.HomeScore, _currentSeason);
    }

    private void ProceedToNextDay()
    {
        _currentDate = _currentDate.AddDays(1);
        Debug.Log($"Advanced to next day: {_currentDate:yyyy-MM-dd}");
        // 시즌 화면 UI 업데이트 로직 호출...
    }
}