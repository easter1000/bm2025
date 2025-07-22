using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

public class GameSimulator : MonoBehaviour
{
    public static event Action<GameResult> OnGameFinished;

    [Header("Simulation Settings")]
    public float simulationSpeed = 8.0f;

    [Header("Stamina & Substitution Settings")]
    public float staminaSubOutThreshold = 40f;
    public float staminaSubInThreshold = 75f;
    public float staminaDepletionRate = 0.2f;
    public float staminaRecoveryRate = 0.4f;
    public float substitutionCheckInterval = 5.0f;

    [Header("Game State")]
    public GameState CurrentState { get; private set; }
    
    public static event Action<GameSimulator.GameState> OnGameStateUpdated;
    public static event Action<GameSimulator.GamePlayer, GameSimulator.GamePlayer> OnPlayerSubstituted;
    
    #region Inner Classes (Data Structures)
    public class GamePlayer
    {
        public PlayerRating Rating;
        public PlayerStats Stats;
        public float CurrentStamina;
        public bool IsOnCourt;
        public int TeamId; // 0 for home, 1 for away
        public float SecondsOnCourt; // 초 단위 출전 시간 추적

        public class PlayerStats
        {
            public int Points;
            public int Assists;
            public int OffensiveRebounds;
            public int DefensiveRebounds;
            public int Steals;
            public int Blocks;
            public int Turnovers;
            public int Fouls;
            public int FieldGoalsMade;
            public int FieldGoalsAttempted;
            public int ThreePointersMade;
            public int ThreePointersAttempted;
            public int FreeThrowsMade;
            public int FreeThrowsAttempted;
            public int PlusMinus;
        }

        public GamePlayer(PlayerRating rating, int teamId)
        {
            Rating = rating;
            Stats = new PlayerStats();
            CurrentStamina = 100f;
            IsOnCourt = false;
            TeamId = teamId;
            SecondsOnCourt = 0f;
        }

        public PlayerStat ExportToPlayerStat(int playerId, int season, string gameId)
        {
            return new PlayerStat
            {
                PlayerId = playerId,
                PlayerName = this.Rating.name,
                TeamAbbr = this.Rating.team,
                Season = season,
                GameId = gameId,
                SecondsPlayed = (int)this.SecondsOnCourt,
                Points = this.Stats.Points,
                Assists = this.Stats.Assists,
                Rebounds = this.Stats.OffensiveRebounds + this.Stats.DefensiveRebounds,
                Steals = this.Stats.Steals,
                Blocks = this.Stats.Blocks,
                Turnovers = this.Stats.Turnovers,
                FieldGoalsMade = this.Stats.FieldGoalsMade,
                FieldGoalsAttempted = this.Stats.FieldGoalsAttempted,
                ThreePointersMade = this.Stats.ThreePointersMade,
                ThreePointersAttempted = this.Stats.ThreePointersAttempted,
                FreeThrowsMade = this.Stats.FreeThrowsMade,
                FreeThrowsAttempted = this.Stats.FreeThrowsAttempted,
                PlusMinus = this.Stats.PlusMinus,
                RecordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }

    public class GameState
    {
        public string GameId;
        public int Season;
        public string HomeTeamName;
        public string AwayTeamName;
        public int HomeScore;
        public int AwayScore;
        public int Quarter = 1;
        public float GameClockSeconds = 720f; // 12 minutes
        public float ShotClockSeconds = 24f;
        public int PossessingTeamId = 0; // 0 for home, 1 for away
        public GamePlayer LastPasser; // For assist tracking
    }
    
    public class GameLogEntry
    {
        public string TimeStamp;
        public string Description;
        public int HomeScore;
        public int AwayScore;

        public override string ToString() => $"[{TimeStamp}] {Description} (Score: {HomeScore}-{AwayScore})";
    }

    public class GameResult
    {
        public int HomeScore;
        public int AwayScore;
        public List<PlayerStat> PlayerStats;
    }
    #endregion
    
    private List<GamePlayer> _homeTeamRoster;
    private List<GamePlayer> _awayTeamRoster;
    
    private List<GameLogEntry> _gameLog = new List<GameLogEntry>();
    private Node _rootOffenseNode;
    private enum SimStatus { Initializing, Running, Finished }
    private SimStatus _status = SimStatus.Initializing;
    private float _timeUntilNextPossession = 0f;
    private float _timeUntilNextSubCheck = 0f;

    void Start()
    {
        Schedule gameToPlay = GameDataHolder.CurrentGameInfo;
        if (gameToPlay == null)
        {
            Debug.LogError("시작할 경기 정보를 찾을 수 없습니다. (GameDataHolder.CurrentGameInfo is null)");
            this.enabled = false;
            return;
        }

        SetupGame(gameToPlay);

        if (_status == SimStatus.Finished)
        {
            Debug.LogError("Game setup failed, cannot start due to insufficient players.");
            return;
        }

        BuildOffenseBehaviorTree();
        _status = SimStatus.Running;
        
        AddLog($"--- Game Start: {CurrentState.HomeTeamName} vs {CurrentState.AwayTeamName} ---");
        AddLog($"--- Start of Quarter {CurrentState.Quarter} ---");
    }

    void Update()
    {
        if (_status != SimStatus.Running) return;
        
        float gameTimeDelta = Time.deltaTime * simulationSpeed;
        CurrentState.GameClockSeconds -= gameTimeDelta;
        _timeUntilNextPossession -= gameTimeDelta;
        _timeUntilNextSubCheck -= Time.deltaTime;

        // 공격 시간(Shot Clock) 업데이트
        if (CurrentState.ShotClockSeconds > 0)
        {
            CurrentState.ShotClockSeconds -= gameTimeDelta;
        }

        UpdateAllPlayerStamina(gameTimeDelta);
        UpdatePlayerTime(gameTimeDelta);

        if (_timeUntilNextPossession <= 0)
        {
            GamePlayer ballHandler = GetRandomAttacker();
            if(ballHandler != null) 
            {
                CurrentState.LastPasser = null; 
                _rootOffenseNode.Evaluate(this, ballHandler);
            }
            // 공격 속도 조절: 다음 공격까지의 시간을 10-20초에서 12-24초로 늘려 템포를 늦춤
            _timeUntilNextPossession = UnityEngine.Random.Range(12f, 24f); 
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

                var finalPlayerStats = _homeTeamRoster.Concat(_awayTeamRoster)
                    .Select(p => p.ExportToPlayerStat(p.Rating.player_id, CurrentState.Season, CurrentState.GameId))
                    .ToList();
                
                var result = new GameResult
                {
                    HomeScore = CurrentState.HomeScore,
                    AwayScore = CurrentState.AwayScore,
                    PlayerStats = finalPlayerStats
                };

                OnGameFinished?.Invoke(result);
                this.enabled = false;
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
    private void SetupGame(Schedule gameInfo)
    {
        CurrentState = new GameState();
        var homeTeamInfo = LocalDbManager.Instance.GetTeam(gameInfo.HomeTeamAbbr);
        var awayTeamInfo = LocalDbManager.Instance.GetTeam(gameInfo.AwayTeamAbbr);
        CurrentState.HomeTeamName = homeTeamInfo.team_name;
        CurrentState.AwayTeamName = awayTeamInfo.team_name;
        CurrentState.Season = gameInfo.Season;
        CurrentState.GameId = gameInfo.GameId;
        
        _homeTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.HomeTeamAbbr).Select(p => new GamePlayer(p, 0)).ToList();
        _awayTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.AwayTeamAbbr).Select(p => new GamePlayer(p, 1)).ToList();

        if (_homeTeamRoster.Count < 5 || _awayTeamRoster.Count < 5)
        {
            _status = SimStatus.Finished;
            return;
        }

        SelectStarters(_homeTeamRoster);
        SelectStarters(_awayTeamRoster);
    }

    private void SelectStarters(List<GamePlayer> roster)
    {
        roster.ForEach(p => p.IsOnCourt = false);
        var sortedRoster = roster.OrderByDescending(p => p.Rating.overallAttribute).ToList();
        var filledPositions = new HashSet<int>();

        foreach (var player in sortedRoster)
        {
            if (filledPositions.Count >= 5) break;
            if (!filledPositions.Contains(player.Rating.position))
            {
                player.IsOnCourt = true;
                filledPositions.Add(player.Rating.position);
            }
        }
        
        if (roster.Count(p => p.IsOnCourt) < 5)
        {
            roster.Where(p => !p.IsOnCourt).Take(5 - roster.Count(p => p.IsOnCourt)).ToList().ForEach(p => p.IsOnCourt = true);
        }
    }
    
    public void RecordAssist(GamePlayer passer)
    {
        if (passer != null)
        {
            passer.Stats.Assists++;
            AddLog($"{passer.Rating.name} gets the assist.");
        }
    }

    public void UpdatePlusMinusOnScore(int scoringTeamId, int points)
    {
        foreach (var player in GetAllPlayersOnCourt())
        {
            if (player.TeamId == scoringTeamId)
            {
                player.Stats.PlusMinus += points;
            }
            else
            {
                player.Stats.PlusMinus -= points;
            }
        }
    }

    private void BuildOffenseBehaviorTree()
    {
        _rootOffenseNode = new Selector(new List<Node> {
            // 최우선 순위: 공격 시간이 5초 미만으로 쫓기는 상황
            new Sequence(new List<Node> {
                new Condition_IsShotClockLow(),
                new Selector(new List<Node> { // 성공률 낮은 버전의 슛 노드들 사용
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_TryForced3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_TryForcedDrive() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryForcedMidRangeShot() }),
                    new Action_TryForced3PointShot() // 최후의 수단으로 그냥 3점 시도
                })
            }),

            // 일반적인 공격 상황 (공격 시간 넉넉함)
            new Selector(new List<Node> {
                // 패스를 1~2회 시도하여 더 좋은 기회를 모색
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
                
                // 다양한 공격 옵션 중 하나를 선택 (Selector 활용)
                new Selector(new List<Node> {
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_Try3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_DriveAndFinish() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryMidRangeShot() })
                }),

                // 위에서 아무것도 선택되지 않았다면, 마지막으로 패스 시도
                new Action_PassToBestTeammate()
            })
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

    private void UpdatePlayerTime(float gameTimeDelta)
    {
        var allRosterPlayers = _homeTeamRoster.Concat(_awayTeamRoster);
        foreach (var player in allRosterPlayers)
        {
            if (player.IsOnCourt)
            {
                player.SecondsOnCourt += gameTimeDelta;
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
            var bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, true);
            if (bestAvailableSub == null && playerOut.CurrentStamina < 15f)
            {
                AddLog($"--- EMERGENCY SUB for {playerOut.Rating.name} (Stamina: {(int)playerOut.CurrentStamina}) ---");
                bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, false);
            }

            if (bestAvailableSub != null)
            {
                PerformSubstitution(playerOut, bestAvailableSub);
                // break; // 한 번에 한 명만 교체하던 제한을 제거하여, 지친 선수가 여러 명이면 모두 교체하도록 수정
            }
        }
    }

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
            .ThenByDescending(p => p.CurrentStamina)
            .ThenByDescending(p => p.Rating.overallAttribute)
            .FirstOrDefault();

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
            CurrentState.ShotClockSeconds = 14f; // 공격 리바운드 시 14초로 리셋
            CurrentState.PossessingTeamId = rebounder.TeamId;
        }
        else
        {
            rebounder.Stats.DefensiveRebounds++;
            AddLog($"{rebounder.Rating.name} grabs the defensive rebound.");
            CurrentState.PossessingTeamId = rebounder.TeamId;
            CurrentState.ShotClockSeconds = 24f; // 수비 리바운드 시 24초로 리셋
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
        PrintTeamBoxScore(CurrentState.HomeTeamName, _homeTeamRoster);
        PrintTeamBoxScore(CurrentState.AwayTeamName, _awayTeamRoster);
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