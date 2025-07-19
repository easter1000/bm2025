using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    
    private List<GamePlayer> _homeTeamRoster;
    private List<GamePlayer> _awayTeamRoster;
    private List<GamePlayer> _playersOnCourt = new List<GamePlayer>();

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
        if (_homeTeamRoster == null || _awayTeamRoster == null)
        {
            _status = SimStatus.Finished;
            return;
        }
        BuildOffenseBehaviorTree();
        _status = SimStatus.Running;
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
            _timeUntilNextPossession = Random.Range(10f, 20f); 
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
    }
    
    // (이하 모든 함수는 이전 버전과 동일하므로, 코드는 생략하지 않고 모두 포함합니다.)
    
    #region Initialization & Core Logic
    private void InitializeGame()
    {
        CurrentState = new GameState();
        var allPlayersData = LocalDbManager.Instance.GetAllPlayerRatings();
        var teamsData = allPlayersData.GroupBy(p => p.team).Where(g => g.Count() >= 5).ToList();

        if (teamsData.Count < 2) { Debug.LogError("Not enough teams to play."); return; }

        int homeIndex = Random.Range(0, teamsData.Count);
        int awayIndex;
        do { awayIndex = Random.Range(0, teamsData.Count); } while (homeIndex == awayIndex);

        _homeTeamName = teamsData[homeIndex].Key;
        _awayTeamName = teamsData[awayIndex].Key;

        _homeTeamRoster = teamsData[homeIndex].Select(p => new GamePlayer(p, 0)).ToList();
        _awayTeamRoster = teamsData[awayIndex].Select(p => new GamePlayer(p, 1)).ToList();

        var homeStarters = SelectStarters(_homeTeamRoster);
        var awayStarters = SelectStarters(_awayTeamRoster);
        
        _playersOnCourt.AddRange(homeStarters);
        _playersOnCourt.AddRange(awayStarters);

        Debug.Log($"--- Matchup Set: {_homeTeamName} (Home) vs {_awayTeamName} (Away) ---");
        AddLog($"Home Starters: {string.Join(", ", homeStarters.Select(p => p.Rating.name))}");
        AddLog($"Away Starters: {string.Join(", ", awayStarters.Select(p => p.Rating.name))}");
    }

    // [핵심 수정] SelectStarters 함수를 완전히 새로운 로직으로 교체
    private List<GamePlayer> SelectStarters(List<GamePlayer> roster)
    {
        var starters = new List<GamePlayer>();

        // 1. 각 포지션(1~5)에 대해 최고의 선수를 찾아 우선적으로 선발
        for (int position = 1; position <= 5; position++)
        {
            var bestAtPosition = roster
                .Where(p => p.Rating.position == position) // 해당 포지션의 선수들만 필터링
                .OrderByDescending(p => p.Rating.overallAttribute) // 능력치 순으로 정렬
                .FirstOrDefault(); // 가장 좋은 선수 한 명을 선택 (없으면 null)

            if (bestAtPosition != null)
            {
                starters.Add(bestAtPosition);
            }
        }

        // 2. 만약 5명이 채워지지 않았다면, 나머지 선수 중 최고 능력치 순으로 빈 자리를 채움
        int needed = 5 - starters.Count;
        if (needed > 0)
        {
            var bestOfTheRest = roster
                .Except(starters) // 이미 선발된 선수는 제외
                .OrderByDescending(p => p.Rating.overallAttribute) // 나머지 중 능력치 순으로 정렬
                .Take(needed); // 필요한 만큼만 선택

            starters.AddRange(bestOfTheRest);
        }

        // 최종적으로 5명을 넘지 않도록 보장 (로스터가 매우 특이한 경우를 대비)
        return starters.Take(5).ToList();
    }
    private void BuildOffenseBehaviorTree()
    {
        _rootOffenseNode = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
            new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_Try3PointShot() }),
            new Sequence(new List<Node> { new Condition_CanDrive(), new Action_DriveAndFinish() }),
            new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryMidRangeShot() }),
            new Action_PassToBestTeammate()
        });
    }

    public void ConsumeTime(float seconds)
    {
        _timeUntilNextPossession = seconds;
    }
    #endregion

    #region Stamina and Substitution Logic
    private void UpdateAllPlayerStamina(float gameTimeDelta)
    {
        var allPlayers = _homeTeamRoster.Concat(_awayTeamRoster);
        foreach (var player in allPlayers)
        {
            if (_playersOnCourt.Contains(player))
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
        foreach (var playerOut in _playersOnCourt.ToList())
        {
            if (playerOut.CurrentStamina < staminaSubOutThreshold)
            {
                var teamRoster = playerOut.TeamId == 0 ? _homeTeamRoster : _awayTeamRoster;
                
                var bestAvailableSub = teamRoster
                    .Where(p => !_playersOnCourt.Contains(p))
                    .Where(p => p.Rating.position == playerOut.Rating.position)
                    .Where(p => p.CurrentStamina > staminaSubInThreshold)
                    .OrderByDescending(p => p.Rating.overallAttribute)
                    .FirstOrDefault();

                if (bestAvailableSub != null)
                {
                    PerformSubstitution(playerOut, bestAvailableSub);
                }
            }
        }
    }
    
    private void PerformSubstitution(GamePlayer playerOut, GamePlayer playerIn)
    {
        _playersOnCourt.Remove(playerOut);
        _playersOnCourt.Add(playerIn);
        AddLog($"{playerIn.Rating.name} (Stamina: {(int)playerIn.CurrentStamina}) subs in for {playerOut.Rating.name} (Stamina: {(int)playerOut.CurrentStamina})");
    }
    #endregion

    #region Helper & Printing Functions
    
    public Node GetRootNode() 
    { 
        return _rootOffenseNode; 
    }
    
    private GamePlayer GetRandomAttacker()
    {
        var attackingTeamId = CurrentState.PossessingTeamId;
        var onCourtAttackers = _playersOnCourt.Where(p => p.TeamId == attackingTeamId).ToList();
        if (onCourtAttackers.Count == 0) return null;

        float totalWeight = onCourtAttackers.Sum(p => p.Rating.overallAttribute);
        float randomPoint = Random.Range(0, totalWeight);

        foreach (var player in onCourtAttackers)
        {
            float weight = player.Rating.overallAttribute;
            if (randomPoint < weight)
            {
                return player;
            }
            randomPoint -= weight;
        }
        
        return onCourtAttackers.FirstOrDefault();
    }

    public List<GamePlayer> GetPlayersOnCourt(int teamId)
    {
        return _playersOnCourt.Where(p => p.TeamId == teamId).ToList();
    }

    public void AddLog(string description)
    {
        float clock = Mathf.Max(0, CurrentState.GameClockSeconds);
        var entry = new GameLogEntry
        {
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
        if (defenders == null || defenders.Count == 0)
        {
            return null;
        }
        return defenders[Random.Range(0, defenders.Count)];
    }
    
    public NodeState ResolveShootingFoul(GamePlayer shooter, GamePlayer defender, int freeThrows)
    {
        defender.Stats.Fouls++;
        AddLog($"{defender.Rating.name} commits a shooting foul on {shooter.Rating.name}. ({defender.Stats.Fouls} PF)");
        
        shooter.Stats.FieldGoalsAttempted++;
        if (freeThrows == 3)
        {
            shooter.Stats.ThreePointersAttempted++;
        }
        
        var freeThrowAction = new Action_ShootFreeThrows(shooter, freeThrows);
        return freeThrowAction.Evaluate(this, shooter);
    }

    public void ResolveRebound(GamePlayer shooter)
    {
        ConsumeTime(Random.Range(2, 5));
        var allPlayers = GetPlayersOnCourt(0).Concat(GetPlayersOnCourt(1)).ToList();
        var reboundScores = new Dictionary<GamePlayer, float>();
        float totalScore = 0;
        foreach (var p in allPlayers)
        {
            var adjustedP = GetAdjustedRating(p);
            float score = (p.TeamId == shooter.TeamId) ? adjustedP.offensiveRebound : adjustedP.defensiveRebound;
            score += Random.Range(1, 20);
            reboundScores.Add(p, score);
            totalScore += score;
        }

        float randomPoint = Random.Range(0, totalScore);
        GamePlayer rebounder = null;
        foreach (var p in reboundScores)
        {
            if (randomPoint < p.Value) { rebounder = p.Key; break; }
            randomPoint -= p.Value;
        }
        if (rebounder == null) rebounder = allPlayers.First();

        if (rebounder.TeamId == shooter.TeamId)
        {
            rebounder.Stats.OffensiveRebounds++;
            AddLog($"{rebounder.Rating.name} grabs the offensive rebound!");
            CurrentState.ShotClockSeconds = 14f;
            CurrentState.PossessingTeamId = rebounder.TeamId;
            // 공격 리바운드 시에는 LastPasser를 유지하여 세컨 찬스 어시스트를 가능하게 함
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
        if (stamina >= 70)
        {
            return player.Rating;
        }
        float fatigueFactor = (70f - stamina) / 70f;
        var adjustedRating = new PlayerRating();
        var baseRating = player.Rating;
        adjustedRating.player_id = baseRating.player_id;
        adjustedRating.name = baseRating.name;
        float maxPenalty = 15.0f; 
        float penalty = fatigueFactor * maxPenalty;
        adjustedRating.closeShot = (int)(baseRating.closeShot - penalty);
        adjustedRating.midRangeShot = (int)(baseRating.midRangeShot - penalty);
        adjustedRating.threePointShot = (int)(baseRating.threePointShot - penalty);
        adjustedRating.layup = (int)(baseRating.layup - penalty);
        adjustedRating.drivingDunk = (int)(baseRating.drivingDunk - penalty);
        adjustedRating.speed = (int)(baseRating.speed - penalty);
        adjustedRating.perimeterDefense = (int)(baseRating.perimeterDefense - penalty);
        adjustedRating.interiorDefense = (int)(baseRating.interiorDefense - penalty);
        adjustedRating.block = (int)(baseRating.block - penalty);
        adjustedRating.steal = (int)(baseRating.steal - penalty);
        adjustedRating.freeThrow = baseRating.freeThrow;
        adjustedRating.passIQ = baseRating.passIQ;
        adjustedRating.drawFoul = (int)(baseRating.drawFoul - (penalty * 0.5f));
        adjustedRating.offensiveRebound = (int)(baseRating.offensiveRebound - penalty);
        adjustedRating.defensiveRebound = (int)(baseRating.defensiveRebound - penalty);
        adjustedRating.ballHandle = (int)(baseRating.ballHandle - (penalty * 0.7f));
        return adjustedRating;
    }
    
    private void PrintFinalLogs()
    {
        Debug.Log("--- FULL GAME LOG RECAP ---");
        StringBuilder sb = new StringBuilder();
        foreach (var log in _gameLog)
        {
            sb.AppendLine(log.ToString());
        }
        Debug.Log(sb.ToString());
    }

    private void PrintFinalBoxScore()
    {
        Debug.Log("--- FINAL BOX SCORE ---");
        Debug.Log($"--- {_homeTeamName} ---");
        var homeBoxScore = _homeTeamRoster.OrderByDescending(p => p.Stats.Points);
        foreach (var p in homeBoxScore)
        {
            string line = $"{p.Rating.name}: {p.Stats.Points} PTS, {p.Stats.FieldGoalsMade}/{p.Stats.FieldGoalsAttempted} FG, {p.Stats.DefensiveRebounds+p.Stats.OffensiveRebounds} REB, {p.Stats.Assists} AST, {p.Stats.Fouls} PF";
            Debug.Log(line);
        }

        Debug.Log($"--- {_awayTeamName} ---");
        var awayBoxScore = _awayTeamRoster.OrderByDescending(p => p.Stats.Points);
        foreach (var p in awayBoxScore)
        {
            string line = $"{p.Rating.name}: {p.Stats.Points} PTS, {p.Stats.FieldGoalsMade}/{p.Stats.FieldGoalsAttempted} FG, {p.Stats.DefensiveRebounds+p.Stats.OffensiveRebounds} REB, {p.Stats.Assists} AST, {p.Stats.Fouls} PF";
            Debug.Log(line);
        }
    }
    #endregion
}