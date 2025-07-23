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
    private System.Random _random;

    private float staminaSubOutThreshold = 40f;
    private float staminaSubInThreshold = 75f;
    private float staminaDepletionRate = 0.2f;
    private float staminaRecoveryRate = 0.4f;
    // 게임 시간 기준 60초마다 교체 검사
    private float substitutionCheckInterval = 60.0f; 
    private float _timeUntilNextSubCheck = 60.0f;
    private float _timeUntilNextInjuryCheck = 30f; // [추가]

    public GameResult SimulateFullGame(Schedule gameToPlay)
    {
        Debug.Log($"[Sim-Debug] Starting simulation for {gameToPlay.AwayTeamAbbr} @ {gameToPlay.HomeTeamAbbr}");
        _random = new System.Random(); // 시뮬레이션 시작 시마다 초기화
        if (!SetupGame(gameToPlay))
        {
            Debug.LogError($"백그라운드 게임 설정 실패: {gameToPlay.HomeTeamAbbr} vs {gameToPlay.AwayTeamAbbr}. 선수가 부족할 수 있습니다.");
            return new GameResult { HomeScore = 0, AwayScore = 0, PlayerStats = new List<PlayerStat>() };
        }
        Debug.Log($"[Sim-Debug] Game setup complete.");

        BuildOffenseBehaviorTree();
        Debug.Log($"[Sim-Debug] Behavior tree built.");


        // 4쿼터 또는 동점일 경우 연장전 계속 진행 (종료 조건 수정)
        while (CurrentState.Quarter < 4 || (CurrentState.Quarter >= 4 && CurrentState.HomeScore == CurrentState.AwayScore))
        {
            CurrentState.Quarter++;
            Debug.Log($"[Sim-Debug] ---- Quarter {CurrentState.Quarter} Start ----");
            
            // 쿼터 초기화
            CurrentState.GameClockSeconds = (CurrentState.Quarter > 4) ? 300f : 720f; // 연장전 5분
            CurrentState.ShotClockSeconds = 24f;
            CurrentState.LastPasser = null; // 쿼터 시작 시 볼 핸들러 초기화

            // 쿼터 진행 루프
            while(CurrentState.GameClockSeconds > 0)
            {
                Debug.Log($"[Sim-Debug] Possession Start. Clock: {CurrentState.GameClockSeconds:F1}, ShotClock: {CurrentState.ShotClockSeconds:F1}, Score: {CurrentState.AwayScore}-{CurrentState.HomeScore}");
                float clockBeforePossession = CurrentState.GameClockSeconds;

                if (CurrentState.LastPasser == null)
                {
                    CurrentState.LastPasser = GetRandomAttacker();
                    if (CurrentState.LastPasser == null)
                    {
                        Debug.LogWarning($"[Sim-Debug] No attacker found. Breaking possession loop.");
                        break; 
                    }
                }
                
                // 행동 트리 실행
                _rootOffenseNode.Evaluate(this, CurrentState.LastPasser);
                
                float clockAfterPossession = CurrentState.GameClockSeconds;
                float timeElapsed = clockBeforePossession - clockAfterPossession;
                Debug.Log($"[Sim-Debug] Possession End. Time elapsed: {timeElapsed:F1}.");

                if (timeElapsed > 0)
                {
                    UpdateAllPlayerStamina(timeElapsed);
                    UpdatePlayerTime(timeElapsed); // <<< 출전 시간 기록 호출

                    _timeUntilNextInjuryCheck -= timeElapsed; // [추가]
                    _timeUntilNextSubCheck -= timeElapsed;

                    if (_timeUntilNextInjuryCheck <= 0)
                    {
                        Debug.Log($"[Sim-Debug] CheckForInjuries: {timeElapsed}");
                        CheckForInjuries();
                        _timeUntilNextInjuryCheck = 60f;
                    }
                    if (_timeUntilNextSubCheck <= 0)
                    {
                        Debug.Log($"[Sim-Debug] CheckForSubstitutions: {timeElapsed}");
                        CheckForSubstitutions();
                        _timeUntilNextSubCheck = substitutionCheckInterval;
                    }

                    Debug.Log($"[Sim-Debug] UpdateAllPlayerStamina: {timeElapsed}");
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
            Debug.Log($"[Sim-Debug] ---- Quarter {CurrentState.Quarter} End. Score: {CurrentState.AwayScore}-{CurrentState.HomeScore} ----");
        }

        var allPlayers = _homeTeamRoster.Concat(_awayTeamRoster).ToList();
        Debug.Log($"[Sim-Debug] Game simulation finished. Final Score: {CurrentState.AwayScore}-{CurrentState.HomeScore}. Saving results...");

        // [추가] 경기 종료 후 스태미나/부상 상태 DB에 저장
        foreach (var p in allPlayers)
        {
            int minutesPlayed = (int)(p.Stats.MinutesPlayedInSeconds / 60);
            // [수정] 부상으로 인한 퇴장(IsEjected)과 6반칙 퇴장을 명확히 구분
            bool isInjured = p.IsEjected && p.Stats.PersonalFouls < 6;
            int injuryDuration = isInjured ? GenerateInjuryDuration() : 0;
            
            LocalDbManager.Instance.UpdatePlayerAfterGame(p.Rating.player_id, minutesPlayed, isInjured, injuryDuration);
        }

        var finalPlayerStats = allPlayers
            .Select(p => {
                string teamAbbr = p.TeamId == 0 ? gameToPlay.HomeTeamAbbr : gameToPlay.AwayTeamAbbr;
                return p.ExportToPlayerStat(p.Rating.player_id, CurrentState.Season, CurrentState.GameId, gameToPlay.GameDate, teamAbbr);
            })
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
        // [추가] Background 시뮬레이터에서도 GameState에 팀 약칭 저장
        CurrentState.HomeTeamAbbr = homeTeamInfo.team_abbv;
        CurrentState.AwayTeamAbbr = awayTeamInfo.team_abbv;
        CurrentState.Season = gameInfo.Season;
        CurrentState.GameId = gameInfo.GameId;
        CurrentState.GameDate = gameInfo.GameDate;

        _homeTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.HomeTeamAbbr).Select(p => new GamePlayer(p, 0)).ToList();
        _awayTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.AwayTeamAbbr).Select(p => new GamePlayer(p, 1)).ToList();

        // [추가] 경기 전 선수 스태미나 설정
        foreach (var p in _homeTeamRoster.Concat(_awayTeamRoster))
        {
            var status = LocalDbManager.Instance.GetPlayerStatus(p.Rating.player_id);
            if (status != null)
            {
                p.MaxStaminaForGame = status.Stamina;
                p.CurrentStamina = status.Stamina;
                p.IsCurrentlyInjured = status.IsInjured;
                if (status.IsInjured)
                {
                    // 부상 중인 선수는 경기는 뛰지만, 능력치가 감소됨
                }
            }
        }

        // [추가] 선발 라인업을 정하기 전, 모든 선수의 초기 EffectiveOverall을 계산
        foreach (var p in _homeTeamRoster.Concat(_awayTeamRoster))
        {
            RecalculateEffectiveOverall(p);
        }

        if (_homeTeamRoster.Count(p => !p.IsEjected) < 5 || _awayTeamRoster.Count(p => !p.IsEjected) < 5)
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
            // 1. 샷클락이 부족할 때의 긴급 행동
            new Sequence(new List<Node> {
                new Condition_IsShotClockLow(),
                new Selector(new List<Node> { 
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_TryForced3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_TryForcedDrive() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryForcedMidRangeShot() }),
                    new Action_TryForced3PointShot() // 최후의 수단
                })
            }),
            // 2. 일반적인 공격 상황 (득점 우선)
            new Selector(new List<Node> {
                // 2a. 득점 시도를 가장 먼저 고려
                new Selector(new List<Node> {
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(), new Action_Try3PointShot() }),
                    new Sequence(new List<Node> { new Condition_CanDrive(), new Action_DriveAndFinish() }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(), new Action_TryMidRangeShot() })
                }),
                // 2b. 좋은 득점 기회가 없으면 패스를 고려
                new Sequence(new List<Node> { 
                    new Condition_IsGoodPassOpportunity(), 
                    new Action_PassToBestTeammate() 
                }),
                // 2c. 슛도, 패스도 마땅치 않으면 마지막으로 다시 패스 시도 (고립 상황 탈출)
                new Action_PassToBestTeammate()
            })
        });
    }

    private void SelectStarters(List<GamePlayer> roster)
    {
        roster.ForEach(p => p.IsOnCourt = false);
        var sortedRoster = roster.Where(p => !p.IsEjected).OrderByDescending(p => p.EffectiveOverall).ToList();
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
            roster.Where(p => !p.IsOnCourt && !p.IsEjected).Take(5 - roster.Count(p => p.IsOnCourt)).ToList().ForEach(p => p.IsOnCourt = true);
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
                player.CurrentStamina = Math.Max(0, player.CurrentStamina);
            }
            else
            {
                player.CurrentStamina += staminaRecoveryRate * gameTimeDelta;
                player.CurrentStamina = Math.Min(player.MaxStaminaForGame, player.CurrentStamina);
            }
            // [추가] 스태미나 변경 후 모든 선수의 실질 OVR을 다시 계산
            RecalculateEffectiveOverall(player);
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
        // 무한 루프를 방지하기 위해 최대 5번까지만 교체를 시도하는 for 루프로 변경
        const int maxSubstitutionsPerCheck = 5; 
        for (int i = 0; i < maxSubstitutionsPerCheck; i++)
        {
            var playerOut = teamRoster
                .Where(p => p.IsOnCourt && !p.IsEjected)
                .OrderBy(p => p.CurrentStamina)
                .FirstOrDefault(p => {
                    var bestSub = FindBestSubstitute(teamRoster, p, false);
                    return p.CurrentStamina < staminaSubOutThreshold || 
                           (bestSub != null && p.EffectiveOverall < bestSub.EffectiveOverall - 5);
                });

            // 교체할 선수가 더 이상 없으면 루프 종료
            if (playerOut == null)
            {
                break;
            }

            var bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, true);
            if (bestAvailableSub == null && playerOut.CurrentStamina < 15f)
            {
                bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, false);
            }

            if (bestAvailableSub != null)
            {
                PerformSubstitution(playerOut, bestAvailableSub);
            }
            else
            {
                // 교체할 선수는 찾았지만 마땅한 후보가 없으면 더 이상 진행하지 않음
                break;
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

        var candidates = roster.Where(p => !p.IsOnCourt && !p.IsEjected);

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
                .Where(p => !p.IsOnCourt && !p.IsEjected)
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
        float randomPoint = (float)(_random.NextDouble() * totalWeight);

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
        
        var baseRating = player.Rating;
        var adjustedRating = new PlayerRating();
        
        if (player.IsCurrentlyInjured)
        {
            adjustedRating.closeShot = baseRating.closeShot / 2;
            adjustedRating.midRangeShot = baseRating.midRangeShot / 2;
            adjustedRating.threePointShot = baseRating.threePointShot / 2;
            adjustedRating.freeThrow = baseRating.freeThrow / 2;
            adjustedRating.layup = baseRating.layup / 2;
            adjustedRating.drivingDunk = baseRating.drivingDunk / 2;
            adjustedRating.drawFoul = baseRating.drawFoul / 2;
            adjustedRating.interiorDefense = baseRating.interiorDefense / 2;
            adjustedRating.perimeterDefense = baseRating.perimeterDefense / 2;
            adjustedRating.steal = baseRating.steal / 2;
            adjustedRating.block = baseRating.block / 2;
            adjustedRating.speed = baseRating.speed / 2;
            adjustedRating.stamina = baseRating.stamina / 2;
            adjustedRating.passIQ = baseRating.passIQ / 2;
            adjustedRating.ballHandle = baseRating.ballHandle / 2;
            adjustedRating.offensiveRebound = baseRating.offensiveRebound / 2;
            adjustedRating.defensiveRebound = baseRating.defensiveRebound / 2;
        }
        else
        {
            adjustedRating = baseRating;
        }

        float stamina = player.CurrentStamina;
        if (stamina >= 70) return adjustedRating;
        
        float fatigueFactor = (70f - stamina) / 70f;
        float maxPenalty = 15.0f; 
        float penalty = fatigueFactor * maxPenalty;

        adjustedRating.closeShot = (int)Math.Max(1, adjustedRating.closeShot - penalty);
        adjustedRating.midRangeShot = (int)Math.Max(1, adjustedRating.midRangeShot - penalty);
        adjustedRating.threePointShot = (int)Math.Max(1, adjustedRating.threePointShot - penalty);
        adjustedRating.layup = (int)Math.Max(1, adjustedRating.layup - penalty);
        adjustedRating.drivingDunk = (int)Math.Max(1, adjustedRating.drivingDunk - penalty);
        adjustedRating.speed = (int)Math.Max(1, adjustedRating.speed - penalty);
        adjustedRating.perimeterDefense = (int)Math.Max(1, adjustedRating.perimeterDefense - penalty);
        adjustedRating.interiorDefense = (int)Math.Max(1, adjustedRating.interiorDefense - penalty);
        adjustedRating.block = (int)Math.Max(1, adjustedRating.block - penalty);
        adjustedRating.steal = (int)Math.Max(1, adjustedRating.steal - penalty);
        adjustedRating.drawFoul = (int)Math.Max(1, adjustedRating.drawFoul - (penalty * 0.5f));
        adjustedRating.offensiveRebound = (int)Math.Max(1, adjustedRating.offensiveRebound - penalty);
        adjustedRating.defensiveRebound = (int)Math.Max(1, adjustedRating.defensiveRebound - penalty);
        adjustedRating.ballHandle = (int)Math.Max(1, adjustedRating.ballHandle - (penalty * 0.7f));
        
        return adjustedRating;
    }
    
    public void AddLog(string description) 
    {
        // 백그라운드에서는 로그를 출력하지 않음
    }
    
    // IGameSimulator 인터페이스에 맞게 수정
    public void AddUILog(string message, GamePlayer eventOwner)
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
        return defenders[_random.Next(0, defenders.Count)];
    }
    
    public NodeState ResolveShootingFoul(GamePlayer shooter, GamePlayer defender, int freeThrows)
    {
        // LiveStats -> Stats 로 수정
        defender.Stats.PersonalFouls++;
        if (defender.Stats.PersonalFouls >= 6)
        {
            EjectPlayer(defender, "6 Personal Fouls");
        }

        shooter.Stats.FieldGoalsAttempted++;
        if (freeThrows == 3) shooter.Stats.ThreePointersAttempted++;
        
        var freeThrowAction = new Action_ShootFreeThrows(shooter, freeThrows);
        return freeThrowAction.Evaluate(this, shooter); 
    }

    public void ResolveRebound(GamePlayer shooter)
    {
        ConsumeTime((float)(_random.NextDouble() * (5 - 2) + 2)); // Random.Range(2, 5)
        var allPlayers = GetAllPlayersOnCourt();
        var reboundScores = new Dictionary<GamePlayer, float>();
        float totalScore = 0;

        foreach (var p in allPlayers)
        {
            var adjustedP = GetAdjustedRating(p);
            float score = (p.TeamId == shooter.TeamId) ? adjustedP.offensiveRebound : adjustedP.defensiveRebound;
            score += (float)(_random.NextDouble() * (20 - 1) + 1); // Random.Range(1, 20)
            reboundScores.Add(p, score);
            totalScore += score;
        }

        float randomPoint = (float)(_random.NextDouble() * totalScore);
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

    public void EjectPlayer(GamePlayer player, string reason)
    {
        player.IsEjected = true;
        player.IsOnCourt = false;

        var teamRoster = (player.TeamId == 0) ? _homeTeamRoster : _awayTeamRoster;
        var substitute = FindBestSubstitute(teamRoster, player, false);
        
        if (substitute != null)
        {
            PerformSubstitution(player, substitute);
        }
    }

    private void CheckForInjuries()
    {
        foreach (var player in GetAllPlayersOnCourt())
        {
            float injuryPossibility = player.Rating.injury * (100f - player.CurrentStamina) / 5f / 48f;
            if ((float)_random.NextDouble() * 100f < injuryPossibility)
            {
                EjectPlayer(player, "Injury");
                break;
            }
        }
    }

    private int GenerateInjuryDuration()
    {
        float rand = (float)_random.NextDouble();
        if (rand < 0.82f) return _random.Next(1, 8);
        else if (rand < 0.97f) return _random.Next(8, 31);
        else return _random.Next(31, 179);
    }

    private void RecalculateEffectiveOverall(GamePlayer player)
    {
        var adjustedRating = GetAdjustedRating(player);
        int effectiveOvr = (int) Math.Round((
            adjustedRating.closeShot + adjustedRating.midRangeShot + adjustedRating.threePointShot +
            adjustedRating.drivingDunk + adjustedRating.layup + adjustedRating.freeThrow +
            adjustedRating.interiorDefense + adjustedRating.perimeterDefense + adjustedRating.steal + adjustedRating.block +
            adjustedRating.speed + adjustedRating.passIQ + adjustedRating.ballHandle +
            adjustedRating.offensiveRebound + adjustedRating.defensiveRebound
        ) / 15.0);
        player.EffectiveOverall = effectiveOvr;
    }

    #endregion

} 