using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

public class GameSimulator : MonoBehaviour
{
    [Header("Simulation Settings")]
    public float simulationSpeed = 8.0f;

    [Header("Stamina & Substitution Settings")]
    public float staminaSubOutThreshold = 40f;
    public float staminaSubInThreshold = 85f;
    public float staminaDepletionRate = 0.2f;
    public float staminaRecoveryRate = 0.4f;
    public float substitutionCheckInterval = 5.0f;

    [Header("Game State")]
    public GameState CurrentState { get; private set; }
    
    public static event Action<GameState> OnGameStateUpdated;
    public static event Action<GamePlayer, GamePlayer> OnPlayerSubstituted;

    // --- 데이터 구조 단순화: 전체 로스터와 각 선수의 IsOnCourt 플래그로만 관리 ---
    private List<GamePlayer> _homeTeamRoster;
    private List<GamePlayer> _awayTeamRoster;
    
    private List<GameLogEntry> _gameLog = new List<GameLogEntry>();
    private Node _rootOffenseNode;
    private string _homeTeamName;
    private string _awayTeamName;
    private enum SimStatus { Initializing, Running, Finished }
    private SimStatus _status = SimStatus.Initializing;
    private float _timeUntilNextPossession = 0f;
    private float _timeUntilNextSubCheck = 0f;

    void Start()
    {
        InitializeGame();
        if (_homeTeamRoster == null || _awayTeamRoster == null || GetPlayersOnCourt(0).Count < 5 || GetPlayersOnCourt(1).Count < 5)
        {
            _status = SimStatus.Finished;
            Debug.LogError("Game cannot start due to insufficient players.");
            return;
        }
        BuildOffenseBehaviorTree();
        _status = SimStatus.Running;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.InitializePlayerPucks(_homeTeamRoster, _awayTeamRoster);
            UIManager.Instance.SetTeamNames(_homeTeamName, _awayTeamName);
        }

        AddLog($"--- Game Start: {_homeTeamName} vs {_awayTeamName} ---");
        AddLog($"--- Start of Quarter {CurrentState.Quarter} ---");
    }

    void Update()
    {
        if (_status != SimStatus.Running) return;
        
        float gameTimeDelta = Time.deltaTime * simulationSpeed;
        CurrentState.GameClockSeconds -= gameTimeDelta;
        _timeUntilNextPossession -= gameTimeDelta;
        _timeUntilNextSubCheck -= Time.deltaTime;

        UpdateAllPlayerStamina(gameTimeDelta);

        if (_timeUntilNextPossession <= 0)
        {
            GamePlayer ballHandler = GetRandomAttacker();
            if(ballHandler != null) 
            {
                CurrentState.LastPasser = null; 
                _rootOffenseNode.Evaluate(this, ballHandler);
            }
            _timeUntilNextPossession = UnityEngine.Random.Range(10f, 20f); 
        }

        if (_timeUntilNextSubCheck <= 0)
        {
            CheckForSubstitutions();
            _timeUntilNextSubCheck = substitutionCheckInterval;
        }

        if (CurrentState.GameClockSeconds <= 0)
        {
            AddLog($"--- End of Quarter {CurrentState.Quarter} ---");
            CurrentState.Quarter++;

            if (CurrentState.Quarter > 4)
            {
                _status = SimStatus.Finished;
                AddLog("--- FINAL ---");
                PrintFinalLogs();
                PrintFinalBoxScore();
            }
            else
            {
                CurrentState.GameClockSeconds = 720f;
                AddLog($"--- Start of Quarter {CurrentState.Quarter} ---");
            }
        }
        
        OnGameStateUpdated?.Invoke(CurrentState);
    }
    
    #region Initialization & Core Logic
    private void InitializeGame()
    {
        CurrentState = new GameState();
        var allPlayersData = LocalDbManager.Instance.GetAllPlayerRatings();
        var teamsData = allPlayersData.GroupBy(p => p.team).Where(g => g.Count() >= 5).ToList();

        if (teamsData.Count < 2) { Debug.LogError("Not enough teams with 5+ players to start a game."); return; }

        int homeIndex = UnityEngine.Random.Range(0, teamsData.Count);
        int awayIndex;
        do { awayIndex = UnityEngine.Random.Range(0, teamsData.Count); } while (homeIndex == awayIndex);

        _homeTeamName = teamsData[homeIndex].Key;
        _awayTeamName = teamsData[awayIndex].Key;

        _homeTeamRoster = teamsData[homeIndex].Select(p => new GamePlayer(p, 0)).ToList();
        _awayTeamRoster = teamsData[awayIndex].Select(p => new GamePlayer(p, 1)).ToList();

        SelectStarters(_homeTeamRoster);
        SelectStarters(_awayTeamRoster);

        Debug.Log($"--- Matchup Set: {_homeTeamName} (Home) vs {_awayTeamName} (Away) ---");
        AddLog($"Home Starters: {string.Join(", ", GetPlayersOnCourt(0).Select(p => p.Rating.name))}");
        AddLog($"Away Starters: {string.Join(", ", GetPlayersOnCourt(1).Select(p => p.Rating.name))}");
    }

    private void SelectStarters(List<GamePlayer> roster)
    {
        roster.ForEach(p => p.IsOnCourt = false);
        var sortedRoster = roster.OrderByDescending(p => p.Rating.overallAttribute).ToList();
        var filledPositions = new HashSet<int>();

        // 1단계: 주 포지션 우선 배정
        foreach (var player in sortedRoster)
        {
            if (filledPositions.Count >= 5) break;
            if (!filledPositions.Contains(player.Rating.position))
            {
                player.IsOnCourt = true;
                filledPositions.Add(player.Rating.position);
            }
        }

        // 2단계: 빈 포지션이 있다면 유연성 활용
        if (filledPositions.Count < 5)
        {
            var positionFlexibility = new Dictionary<int, List<int>> {
                { 1, new List<int> { 1, 2 } }, { 2, new List<int> { 2, 1, 3 } },
                { 3, new List<int> { 3, 2, 4 } }, { 4, new List<int> { 4, 5, 3 } },
                { 5, new List<int> { 5, 4 } }
            };
            var emptyPositions = new List<int> { 1, 2, 3, 4, 5 }.Where(p => !filledPositions.Contains(p));

            foreach (var pos in emptyPositions)
            {
                var candidate = sortedRoster
                    .Where(p => !p.IsOnCourt)
                    .FirstOrDefault(p => positionFlexibility.ContainsKey(p.Rating.position) && positionFlexibility[p.Rating.position].Contains(pos));
                if (candidate != null)
                {
                    candidate.IsOnCourt = true;
                }
            }
        }
        
        // 3단계: 그래도 5명이 안되면 강제 배정
        int currentStarters = roster.Count(p => p.IsOnCourt);
        if (currentStarters < 5)
        {
            roster.Where(p => !p.IsOnCourt).Take(5 - currentStarters).ToList().ForEach(p => p.IsOnCourt = true);
        }
    }
    
    private void BuildOffenseBehaviorTree()
    {
        _rootOffenseNode = new Selector(new List<Node> {
            new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
            new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_Try3PointShot() }),
            new Sequence(new List<Node> { new Condition_CanDrive(), new Action_DriveAndFinish() }),
            new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryMidRangeShot() }),
            new Action_PassToBestTeammate()
        });
    }

    public void ConsumeTime(float seconds) => _timeUntilNextPossession = seconds;
    #endregion

    #region Stamina and Substitution Logic
    private void UpdateAllPlayerStamina(float gameTimeDelta)
    {
        var allRosterPlayers = _homeTeamRoster.Concat(_awayTeamRoster);
        foreach (var player in allRosterPlayers)
        {
            if (player.IsOnCourt)
            {
                float depletionModifier = 1.0f - ((player.Rating.stamina - 50) / 100f);
                player.CurrentStamina -= staminaDepletionRate * depletionModifier * gameTimeDelta;
                player.CurrentStamina = Mathf.Max(0, player.CurrentStamina);
            }
            else
            {
                player.CurrentStamina += staminaRecoveryRate * gameTimeDelta;
                player.CurrentStamina = Mathf.Min(100, player.CurrentStamina);
            }
        }
    }

    private void CheckForSubstitutions()
    {
        ProcessTeamSubstitutions(_homeTeamRoster);
        ProcessTeamSubstitutions(_awayTeamRoster);
    }

    private void ProcessTeamSubstitutions(List<GamePlayer> teamRoster)
    {
        var tiredPlayers = teamRoster
            .Where(p => p.IsOnCourt && p.CurrentStamina < staminaSubOutThreshold)
            .OrderBy(p => p.CurrentStamina)
            .ToList();

        foreach (var playerOut in tiredPlayers)
        {
            // 1. 이상적인 교체 시도 (체력 85 이상)
            var bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, true);

            // 2. 긴급 교체: 이상적인 교체 선수가 없고, 코트 위 선수의 체력이 15 미만일 때
            if (bestAvailableSub == null && playerOut.CurrentStamina < 15f)
            {
                AddLog($"--- EMERGENCY SUB for {playerOut.Rating.name} (Stamina: {(int)playerOut.CurrentStamina}) ---");
                // 체력 85 조건을 무시하고 벤치에서 최선의 선수를 찾음
                bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, false);
            }

            if (bestAvailableSub != null)
            {
                PerformSubstitution(playerOut, bestAvailableSub);
                break; // 한 번에 한 명만 교체하여 안정성 확보
            }
        }
    }

    // [신규 헬퍼 함수] 교체 선수 탐색 로직을 별도 함수로 분리하여 가독성 및 재사용성 향상
    private GamePlayer FindBestSubstitute(List<GamePlayer> roster, GamePlayer playerOut, bool requireHighStamina)
    {
        var positionFlexibility = new Dictionary<int, List<int>> {
            { 1, new List<int> { 1, 2 } }, { 2, new List<int> { 2, 1, 3 } },
            { 3, new List<int> { 3, 2, 4 } }, { 4, new List<int> { 4, 5, 3 } },
            { 5, new List<int> { 5, 4 } }
        };
        var outgoingPosition = playerOut.Rating.position;

        var candidates = roster.Where(p => !p.IsOnCourt);

        if (requireHighStamina)
        {
            candidates = candidates.Where(p => p.CurrentStamina > staminaSubInThreshold);
        }

        var bestSub = candidates
            .Where(p => positionFlexibility.ContainsKey(p.Rating.position) && positionFlexibility[p.Rating.position].Contains(outgoingPosition))
            .OrderByDescending(p => p.Rating.position == outgoingPosition)
            .ThenByDescending(p => p.CurrentStamina) // 체력 좋은 선수 우선
            .ThenByDescending(p => p.Rating.overallAttribute)
            .FirstOrDefault();

        // 포지션에 맞는 선수가 아예 없으면(긴급 상황 시), 그냥 벤치에서 가장 체력 좋은 선수라도 투입
        if (bestSub == null && !requireHighStamina) 
        {
            bestSub = roster
                .Where(p => !p.IsOnCourt)
                .OrderByDescending(p => p.CurrentStamina)
                .FirstOrDefault();
        }
        
        return bestSub;
    }
    
    private void PerformSubstitution(GamePlayer playerOut, GamePlayer playerIn)
    {
        playerOut.IsOnCourt = false;
        playerIn.IsOnCourt = true;
        
        AddLog($"{playerIn.Rating.name} (Stamina: {(int)playerIn.CurrentStamina}) subs in for {playerOut.Rating.name} (Stamina: {(int)playerOut.CurrentStamina})");
        OnPlayerSubstituted?.Invoke(playerOut, playerIn);
    }
    #endregion

    #region Helper & Printing Functions
    public Node GetRootNode() => _rootOffenseNode;

    public List<GamePlayer> GetAllPlayersOnCourt() => _homeTeamRoster.Concat(_awayTeamRoster).Where(p => p.IsOnCourt).ToList();
    
    private GamePlayer GetRandomAttacker()
    {
        var onCourtAttackers = GetPlayersOnCourt(CurrentState.PossessingTeamId);
        if (onCourtAttackers.Count == 0) return null;

        float totalWeight = onCourtAttackers.Sum(p => p.Rating.overallAttribute);
        float randomPoint = UnityEngine.Random.Range(0, totalWeight);

        foreach (var player in onCourtAttackers)
        {
            if (randomPoint < player.Rating.overallAttribute) return player;
            randomPoint -= player.Rating.overallAttribute;
        }
        return onCourtAttackers.FirstOrDefault();
    }

    public List<GamePlayer> GetPlayersOnCourt(int teamId) => (teamId == 0 ? _homeTeamRoster : _awayTeamRoster).Where(p => p.IsOnCourt).ToList();

    public void AddLog(string description)
    {
        float clock = Mathf.Max(0, CurrentState.GameClockSeconds);
        var entry = new GameLogEntry {
            TimeStamp = $"Q{CurrentState.Quarter} {(int)clock / 60:00}:{(int)clock % 60:00}",
            Description = description,
            HomeScore = CurrentState.HomeScore,
            AwayScore = CurrentState.AwayScore
        };
        _gameLog.Add(entry);
        Debug.Log(entry.ToString());
    }

    public GamePlayer GetRandomDefender(int attackingTeamId)
    {
        var defenders = GetPlayersOnCourt(1 - attackingTeamId);
        if (defenders.Count == 0) return null;
        return defenders[UnityEngine.Random.Range(0, defenders.Count)];
    }
    
    public NodeState ResolveShootingFoul(GamePlayer shooter, GamePlayer defender, int freeThrows)
    {
        defender.Stats.Fouls++;
        AddLog($"{defender.Rating.name} commits a shooting foul on {shooter.Rating.name}. ({defender.Stats.Fouls} PF)");
        
        shooter.Stats.FieldGoalsAttempted++;
        if (freeThrows == 3) shooter.Stats.ThreePointersAttempted++;
        
        var freeThrowAction = new Action_ShootFreeThrows(shooter, freeThrows);
        return freeThrowAction.Evaluate(this, shooter);
    }

    public void ResolveRebound(GamePlayer shooter)
    {
        ConsumeTime(UnityEngine.Random.Range(2, 5));
        var allPlayers = GetAllPlayersOnCourt();
        var reboundScores = new Dictionary<GamePlayer, float>();
        float totalScore = 0;

        foreach (var p in allPlayers)
        {
            var adjustedP = GetAdjustedRating(p);
            float score = (p.TeamId == shooter.TeamId) ? adjustedP.offensiveRebound : adjustedP.defensiveRebound;
            score += UnityEngine.Random.Range(1, 20);
            reboundScores.Add(p, score);
            totalScore += score;
        }

        float randomPoint = UnityEngine.Random.Range(0, totalScore);
        GamePlayer rebounder = reboundScores.FirstOrDefault(p => { randomPoint -= p.Value; return randomPoint < 0; }).Key;
        if (rebounder == null) rebounder = allPlayers.FirstOrDefault();

        if (rebounder.TeamId == shooter.TeamId)
        {
            rebounder.Stats.OffensiveRebounds++;
            AddLog($"{rebounder.Rating.name} grabs the offensive rebound!");
            CurrentState.ShotClockSeconds = 14f;
            CurrentState.PossessingTeamId = rebounder.TeamId;
        }
        else
        {
            rebounder.Stats.DefensiveRebounds++;
            AddLog($"{rebounder.Rating.name} grabs the defensive rebound.");
            CurrentState.PossessingTeamId = rebounder.TeamId;
            CurrentState.ShotClockSeconds = 24f;
            CurrentState.LastPasser = null;
        }
    }
    
    public PlayerRating GetAdjustedRating(GamePlayer player)
    {
        if (player == null) return new PlayerRating();
        float stamina = player.CurrentStamina;
        if (stamina >= 70) return player.Rating;
        
        float fatigueFactor = (70f - stamina) / 70f;
        var baseRating = player.Rating;
        float maxPenalty = 15.0f; 
        float penalty = fatigueFactor * maxPenalty;

        return new PlayerRating {
            player_id = baseRating.player_id, name = baseRating.name,
            closeShot = (int)Mathf.Max(1, baseRating.closeShot - penalty),
            midRangeShot = (int)Mathf.Max(1, baseRating.midRangeShot - penalty),
            threePointShot = (int)Mathf.Max(1, baseRating.threePointShot - penalty),
            layup = (int)Mathf.Max(1, baseRating.layup - penalty),
            drivingDunk = (int)Mathf.Max(1, baseRating.drivingDunk - penalty),
            speed = (int)Mathf.Max(1, baseRating.speed - penalty),
            perimeterDefense = (int)Mathf.Max(1, baseRating.perimeterDefense - penalty),
            interiorDefense = (int)Mathf.Max(1, baseRating.interiorDefense - penalty),
            block = (int)Mathf.Max(1, baseRating.block - penalty),
            steal = (int)Mathf.Max(1, baseRating.steal - penalty),
            freeThrow = baseRating.freeThrow, passIQ = baseRating.passIQ,
            drawFoul = (int)Mathf.Max(1, baseRating.drawFoul - (penalty * 0.5f)),
            offensiveRebound = (int)Mathf.Max(1, baseRating.offensiveRebound - penalty),
            defensiveRebound = (int)Mathf.Max(1, baseRating.defensiveRebound - penalty),
            ballHandle = (int)Mathf.Max(1, baseRating.ballHandle - (penalty * 0.7f))
        };
    }
    
    private void PrintFinalLogs()
    {
        Debug.Log("--- FULL GAME LOG RECAP ---");
        Debug.Log(string.Join("\n", _gameLog));
    }

    private void PrintFinalBoxScore()
    {
        Debug.Log("--- FINAL BOX SCORE ---");
        PrintTeamBoxScore(_homeTeamName, _homeTeamRoster);
        PrintTeamBoxScore(_awayTeamName, _awayTeamRoster);
    }

    private void PrintTeamBoxScore(string teamName, List<GamePlayer> roster)
    {
        Debug.Log($"--- {teamName} ---");
        foreach (var p in roster.OrderByDescending(p => p.Stats.Points))
        {
            string line = $"{p.Rating.name}: {p.Stats.Points} PTS, {p.Stats.FieldGoalsMade}/{p.Stats.FieldGoalsAttempted} FG, {p.Stats.DefensiveRebounds+p.Stats.OffensiveRebounds} REB, {p.Stats.Assists} AST, {p.Stats.Fouls} PF";
            Debug.Log(line);
        }
    }
    #endregion
}