using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class BackgroundGameSimulator : IGameSimulator
{
    public GameState CurrentState { get; private set; } // 인터페이스 구현을 위해 public 속성으로 변경
    private List<GamePlayer> _homeTeamRoster; // GamaData.cs의 GamePlayer
    private List<GamePlayer> _awayTeamRoster; // GamaData.cs의 GamePlayer
    private List<GameLogEntry> _gameLog = new List<GameLogEntry>(); // GamaData.cs의 GameLogEntry
    private Node _rootOffenseNode;

    private float staminaSubOutThreshold = 40f;
    private float staminaSubInThreshold = 75f;
    private float staminaDepletionRate = 0.2f;
    private float staminaRecoveryRate = 0.4f;
    // 게임 시간 기준 60초마다 교체 검사
    private float substitutionCheckInterval = 60.0f; 
    private float _timeUntilNextSubCheck = 60.0f;

    public GameResult SimulateFullGame(Schedule gameToPlay)
    {
        if (!SetupGame(gameToPlay))
        {
            Debug.LogError($"백그라운드 게임 설정 실패: {gameToPlay.HomeTeamAbbr} vs {gameToPlay.AwayTeamAbbr}. 선수가 부족할 수 있습니다.");
            return new GameResult { HomeScore = 0, AwayScore = 0, PlayerStats = new List<PlayerStat>() };
        }

        BuildOffenseBehaviorTree();

        // 4쿼터 또는 동점일 경우 연장전 계속 진행
        while (CurrentState.Quarter <= 4 || (CurrentState.HomeScore == CurrentState.AwayScore))
        {
            // 쿼터 초기화
            CurrentState.GameClockSeconds = (CurrentState.Quarter > 4) ? 300f : 720f; // 연장전 5분
            CurrentState.ShotClockSeconds = 24f;
            CurrentState.LastPasser = null; // 쿼터 시작 시 볼 핸들러 초기화

            // 쿼터 진행 루프
            while(CurrentState.GameClockSeconds > 0)
            {
                float clockBeforePossession = CurrentState.GameClockSeconds;

                if (CurrentState.LastPasser == null)
                {
                    CurrentState.LastPasser = GetRandomAttacker();
                    if (CurrentState.LastPasser == null) break; // 플레이할 선수가 없으면 중단
                }
                
                // 행동 트리 실행
                _rootOffenseNode.Evaluate(this, CurrentState.LastPasser);
                
                float clockAfterPossession = CurrentState.GameClockSeconds;
                float timeElapsed = clockBeforePossession - clockAfterPossession;

                if (timeElapsed > 0)
                {
                    UpdateAllPlayerStamina(timeElapsed);
                    UpdatePlayerTime(timeElapsed); // <<< 출전 시간 기록 호출

                    _timeUntilNextSubCheck -= timeElapsed;
                    if (_timeUntilNextSubCheck <= 0)
                    {
                        CheckForSubstitutions();
                        _timeUntilNextSubCheck = substitutionCheckInterval;
                    }
                }

                // 샷클락 바이얼레이션 (공격이 끝나지 않았는데 샷클락이 0이 된 경우)
                if (CurrentState.ShotClockSeconds <= 0 && CurrentState.LastPasser != null)
                {
                    CurrentState.LastPasser.Stats.Turnovers++;
                    CurrentState.PossessingTeamId = 1 - CurrentState.PossessingTeamId;
                    CurrentState.ShotClockSeconds = 24f;
                    CurrentState.LastPasser = null;
                }
            }
            
            CurrentState.Quarter++;
        }

        var finalPlayerStats = _homeTeamRoster.Concat(_awayTeamRoster)
            .Select(p => p.ExportToPlayerStat(p.Rating.player_id, CurrentState.Season, CurrentState.GameId))
            .ToList();
        
        return new GameResult
        {
            HomeScore = CurrentState.HomeScore,
            AwayScore = CurrentState.AwayScore,
            PlayerStats = finalPlayerStats
        };
    }
    
    #region Core Logic & Helpers
    
    private bool SetupGame(Schedule gameInfo)
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
            return false;
        }

        SelectStarters(_homeTeamRoster);
        SelectStarters(_awayTeamRoster);
        return true;
    }

    private void BuildOffenseBehaviorTree()
    {
        _rootOffenseNode = new Selector(new List<Node> {
            new Sequence(new List<Node> {
                new Condition_IsShotClockLow(),
                new Selector(new List<Node> { 
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_TryForced3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_TryForcedDrive() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryForcedMidRangeShot() }),
                    new Action_TryForced3PointShot()
                })
            }),
            new Selector(new List<Node> {
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
                new Selector(new List<Node> {
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_Try3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_DriveAndFinish() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryMidRangeShot() })
                }),
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(), new Action_PassToBestTeammate() }),
                new Action_PassToBestTeammate()
            })
        });
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
                player.Stats.MinutesPlayedInSeconds += gameTimeDelta;
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
                bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, false);
            }

            if (bestAvailableSub != null)
            {
                PerformSubstitution(playerOut, bestAvailableSub);
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
    }
    
    private GamePlayer GetRandomAttacker()
    {
        var onCourtAttackers = GetPlayersOnCourt(CurrentState.PossessingTeamId);
        if (onCourtAttackers.Count == 0) return null;

        float totalWeight = onCourtAttackers.Sum(p => Mathf.Pow(p.Rating.overallAttribute, 2.0f));
        float randomPoint = UnityEngine.Random.Range(0, totalWeight);

        foreach (var player in onCourtAttackers)
        {
            float weight = Mathf.Pow(player.Rating.overallAttribute, 2.0f);
            if (randomPoint < weight) return player;
            randomPoint -= weight;
        }
        return onCourtAttackers.FirstOrDefault();
    }
    
    public List<GamePlayer> GetPlayersOnCourt(int teamId) => (teamId == 0 ? _homeTeamRoster : _awayTeamRoster).Where(p => p.IsOnCourt).ToList();
    public List<GamePlayer> GetAllPlayersOnCourt() => _homeTeamRoster.Concat(_awayTeamRoster).Where(p => p.IsOnCourt).ToList();

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
    
    public void AddLog(string description) 
    {
        // 백그라운드에서는 로그를 출력하지 않음
    }
    
    public void AddUILog(string message)
    {
        // 백그라운드에서는 UI 로그를 출력하지 않음
    }

    public void RecordAssist(GamePlayer passer)
    {
        if (passer != null)
        {
            passer.Stats.Assists++;
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
    
    public void ConsumeTime(float seconds)
    {
        CurrentState.GameClockSeconds -= seconds;
        CurrentState.ShotClockSeconds -= seconds;
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
            CurrentState.ShotClockSeconds = 14f;
            CurrentState.PossessingTeamId = rebounder.TeamId;
            CurrentState.LastPasser = rebounder;
        }
        else
        {
            rebounder.Stats.DefensiveRebounds++;
            CurrentState.PossessingTeamId = rebounder.TeamId;
            CurrentState.ShotClockSeconds = 24f;
            CurrentState.LastPasser = null;
        }
    }

    public void ResolveTurnover(GamePlayer offensivePlayer, GamePlayer defensivePlayer, bool isSteal)
    {
        offensivePlayer.Stats.Turnovers++;
        if (isSteal)
        {
            defensivePlayer.Stats.Steals++;
        }
        CurrentState.PossessingTeamId = 1 - offensivePlayer.TeamId;
        CurrentState.ShotClockSeconds = 24f;
        CurrentState.LastPasser = null;
        CurrentState.PotentialAssister = null;
    }

    public void ResolveBlock(GamePlayer shooter, GamePlayer blocker)
    {
        blocker.Stats.Blocks++;
        ResolveRebound(shooter);
    }

    #endregion
} 