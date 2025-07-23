using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

// IGameSimulator 인터페이스 구현
public class GameSimulator : MonoBehaviour, IGameSimulator
{
    public static GameSimulator Instance { get; private set; }

    // GameResult는 이제 GamaData.cs의 공용 타입을 사용합니다.
    public static event Action<GameResult> OnGameFinished; 

    [Header("Simulation Settings")]
    public float SimulationSpeed { get; set; } = 12.0f; // 기본 12배속 (48분 경기 -> 4분)

    [Header("Stamina & Substitution Settings")]
    public float staminaSubOutThreshold = 40f;
    public float staminaSubInThreshold = 75f;
    public float staminaDepletionRate = 0.2f;
    public float staminaRecoveryRate = 0.4f;
    public float substitutionCheckInterval = 5.0f;

    [Header("Game State")]
    public GameState CurrentState { get; private set; } // GamaData.cs의 GameState 사용 
    
    // GamePlayer, GameState는 GamaData.cs의 것을 사용하므로 이벤트 타입 변경
    public static event Action<GameState> OnGameStateUpdated;
    public static event Action<GamePlayer, GamePlayer> OnPlayerSubstituted;
    public static event Action<string> OnUILogGenerated; // UI 표시용 로그 이벤트

    public bool IsUserTeamAutoSubbed { get; set; } = false; // [추가] 유저팀 자동 교체 여부
    private int _userTeamId = -1; // [추가] 0=홈, 1=어웨이, -1=AI vs AI

    private List<GamePlayer> _homeTeamRoster; // GamaData.cs의 GamePlayer 사용
    private List<GamePlayer> _awayTeamRoster; // GamaData.cs의 GamePlayer 사용
    
    private List<GameLogEntry> _gameLog = new List<GameLogEntry>(); // GamaData.cs의 GameLogEntry 사용
    private Node _rootOffenseNode;
    private System.Random _random; // System.Random 인스턴스 추가
    private enum SimStatus { Initializing, Running, Finished }
    private SimStatus _status = SimStatus.Initializing;
    private float _timeUntilNextPossession = 0f;
    private float _timeUntilNextSubCheck = 0f;
    private float _timeUntilNextInjuryCheck = 30f; // [추가] 다음 부상 체크까지 남은 게임 시간
    public int GetUserTeamId() => _userTeamId; // [추가] 유저 팀 ID를 반환하는 public 메서드
    void Awake()
    {
        Instance = this;
        _random = new System.Random(); // Awake에서 초기화
    }

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
        
        float gameTimeDelta = Time.deltaTime * SimulationSpeed;
        CurrentState.GameClockSeconds -= gameTimeDelta;
        _timeUntilNextPossession -= gameTimeDelta;
        _timeUntilNextSubCheck -= Time.deltaTime;
        _timeUntilNextInjuryCheck -= gameTimeDelta; // [추가]

        // 공격 시간(Shot Clock) 업데이트
        if (CurrentState.ShotClockSeconds > 0)
        {
            CurrentState.ShotClockSeconds -= gameTimeDelta;
        }

        UpdateAllPlayerStamina(gameTimeDelta);
        UpdatePlayerTime(gameTimeDelta);

        if (_timeUntilNextPossession <= 0)
        {
            if (CurrentState.LastPasser == null) // LastPasser로 현재 볼 핸들러를 추적하지 않고, 새 공격 시작 시에만 초기화
            {
                CurrentState.LastPasser = GetRandomAttacker(); 
            }
            
            var ballHandler = CurrentState.LastPasser;

            if(ballHandler != null) 
            {
                // ActionNode의 Evaluate는 GamePlayer를 받음. LastPasser가 GamePlayer 타입
                _rootOffenseNode.Evaluate(this, ballHandler);
            }
        }

        if (_timeUntilNextInjuryCheck <= 0)
        {
            CheckForInjuries();
            _timeUntilNextInjuryCheck = 60f; // 다음 체크는 1분(게임 시간) 뒤
        }

        if (_timeUntilNextSubCheck <= 0)
        {
            CheckForSubstitutions();
            _timeUntilNextSubCheck = substitutionCheckInterval;
        }

        if (CurrentState.GameClockSeconds <= 0)
        {
            AddLog($"--- End of Quarter {CurrentState.Quarter} ---");

            // 4쿼터 이상 진행됐고, 점수가 동점이 아닐 경우에만 게임 종료
            if (CurrentState.Quarter >= 4 && CurrentState.HomeScore != CurrentState.AwayScore)
            {
                _status = SimStatus.Finished;
                AddLog("--- FINAL ---");
                PrintFinalLogs();
                PrintFinalBoxScore();

                // [수정] 경기 종료 후 스태미나/부상 상태 DB에 저장
                var allPlayers = _homeTeamRoster.Concat(_awayTeamRoster).ToList();
                foreach (var p in allPlayers)
                {
                    int minutesPlayed = (int)(p.Stats.MinutesPlayedInSeconds / 60);
                    bool isInjured = p.IsEjected && p.Stats.PersonalFouls < 6;
                    int injuryDuration = isInjured ? GenerateInjuryDuration() : 0;
                    LocalDbManager.Instance.UpdatePlayerAfterGame(p.Rating.player_id, minutesPlayed, isInjured, injuryDuration);
                }

                var finalPlayerStats = allPlayers
                    .Select(p => {
                        string teamAbbr = p.TeamId == 0 ? CurrentState.HomeTeamAbbr : CurrentState.AwayTeamAbbr;
                        return p.ExportToPlayerStat(p.Rating.player_id, CurrentState.Season, CurrentState.GameId, CurrentState.GameDate, teamAbbr);
                    })
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
            else // 4쿼터지만 동점이거나, 1~3쿼터가 끝난 경우
            {
                CurrentState.Quarter++;
                CurrentState.GameClockSeconds = (CurrentState.Quarter > 4) ? 300f : 720f; // 연장전은 5분(300초)
                CurrentState.ShotClockSeconds = 24f;
                CurrentState.LastPasser = null; // 쿼터 시작 시 볼 핸들러 초기화
                AddLog($"--- Start of Quarter {CurrentState.Quarter} ---");
            }
        }
        
        OnGameStateUpdated?.Invoke(CurrentState);
    }
    
    #region Initialization & Core Logic
    private void SetupGame(Schedule gameInfo)
    {
        CurrentState = new GameState(); // GamaData.cs의 GameState 사용
        var homeTeamInfo = LocalDbManager.Instance.GetTeam(gameInfo.HomeTeamAbbr);
        var awayTeamInfo = LocalDbManager.Instance.GetTeam(gameInfo.AwayTeamAbbr);
        CurrentState.HomeTeamName = homeTeamInfo.team_name;
        CurrentState.AwayTeamName = awayTeamInfo.team_name;
        CurrentState.HomeTeamAbbr = homeTeamInfo.team_abbv; // [추가]
        CurrentState.AwayTeamAbbr = awayTeamInfo.team_abbv; // [추가]
        CurrentState.Season = gameInfo.Season;
        CurrentState.GameId = gameInfo.GameId;
        CurrentState.GameDate = gameInfo.GameDate; // [추가]
        
        // [추가] 유저 팀 ID 확인
        string userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;
        if (homeTeamInfo.team_abbv == userTeamAbbr) _userTeamId = 0;
        else if (awayTeamInfo.team_abbv == userTeamAbbr) _userTeamId = 1;

        _homeTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.HomeTeamAbbr)
            .Select(p => new GamePlayer(p, 0)).ToList();
        _awayTeamRoster = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.AwayTeamAbbr)
            .Select(p => new GamePlayer(p, 1)).ToList();

        // [추가] 경기 전 선수 스태미나 설정
        foreach (var p in _homeTeamRoster.Concat(_awayTeamRoster))
        {
            var status = LocalDbManager.Instance.GetPlayerStatus(p.Rating.player_id);
            if (status != null)
            {
                p.MaxStaminaForGame = status.Stamina; // [수정] 오늘의 최대 스태미나 설정
                p.CurrentStamina = status.Stamina;
                p.IsCurrentlyInjured = status.IsInjured; // [수정] 부상 상태 저장
                if (status.IsInjured)
                {
                    // 부상 중인 선수는 경기는 뛰지만, 능력치가 감소됨 (퇴장시키지 않음)
                    AddLog($"--- NOTICE: {p.Rating.name} is playing through an injury. ---");
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
            _status = SimStatus.Finished;
            return;
        }

        SelectStarters(_homeTeamRoster);
        SelectStarters(_awayTeamRoster);

        // UIManager 초기화 호출 추가
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetUpScoreboard(homeTeamInfo, awayTeamInfo);
            UIManager.Instance.InitializePlayerPucks(_homeTeamRoster, _awayTeamRoster);
        }
    }

    private void SelectStarters(List<GamePlayer> roster)
    {
        roster.ForEach(p => p.IsOnCourt = false);
        var sortedRoster = roster
            .Where(p => !p.IsEjected) // 퇴장/부상 당하지 않은 선수 중에서만 선택
            .OrderByDescending(p => p.EffectiveOverall) // [수정] 기본 OVR 대신 실질 OVR로 선발
            .ToList();
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
    
    public void RecordAssist(GamePlayer passer)
    {
        if (passer != null && CurrentState.LastPasser != passer)
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
            new Sequence(new List<Node> {
                new Condition_IsShotClockLow(),
                new Selector(new List<Node> { 
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(_random), new Action_TryForced3PointShot(_random) }),
                    new Sequence(new List<Node> { new Condition_CanDrive(_random), new Action_TryForcedDrive(_random) }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(_random), new Action_TryForcedMidRangeShot(_random) }),
                    new Action_TryForced3PointShot(_random)
                })
            }),
            new Selector(new List<Node> {
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(_random), new Action_PassToBestTeammate(_random) }),
                new Selector(new List<Node> {
                    new Sequence(new List<Node> { new Condition_IsOpenFor3(_random), new Action_Try3PointShot(_random) }),
                    new Sequence(new List<Node> { new Condition_CanDrive(_random), new Action_DriveAndFinish(_random) }),
                    new Sequence(new List<Node> { new Condition_IsGoodForMidRange(_random), new Action_TryMidRangeShot(_random) })
                }),
                new Sequence(new List<Node> { new Condition_IsGoodPassOpportunity(_random), new Action_PassToBestTeammate(_random) }),
                new Action_PassToBestTeammate(_random)
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
                player.CurrentStamina = Mathf.Min(player.MaxStaminaForGame, player.CurrentStamina);
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
                // LivePlayerStats에 출전 시간 기록 (int 캐스팅 제거)
                player.Stats.MinutesPlayedInSeconds += gameTimeDelta;
            }
        }
    }

    private void CheckForSubstitutions()
    {
        // [수정] AI팀과, '자동 진행'이 켜진 유저팀에 대해서만 교체 로직 실행
        if (_userTeamId != 0 || IsUserTeamAutoSubbed)
        {
            ProcessTeamSubstitutions(_homeTeamRoster);
        }
        if (_userTeamId != 1 || IsUserTeamAutoSubbed)
        {
            ProcessTeamSubstitutions(_awayTeamRoster);
        }
    }

    private void ProcessTeamSubstitutions(List<GamePlayer> teamRoster)
    {
        bool substitutionMade;
        do
        {
            substitutionMade = false;

            // 코트 위의 선수 중 가장 교체가 시급한 선수 1명을 찾음 (가장 지쳤거나, 가장 비효율적인 선수)
            var playerOut = teamRoster
                .Where(p => p.IsOnCourt && !p.IsEjected)
                .OrderBy(p => p.CurrentStamina) // 1순위: 가장 지친 선수
                .FirstOrDefault(p => {
                    var bestSub = FindBestSubstitute(teamRoster, p, false);
                    // 조건: 스태미나가 임계치 미만이거나, 벤치 선수보다 실질 OVR이 5 이상 낮은 경우
                    return p.CurrentStamina < staminaSubOutThreshold || 
                           (bestSub != null && p.EffectiveOverall < bestSub.EffectiveOverall - 5);
                });

            if (playerOut != null)
            {
                // 해당 선수를 위한 최적의 교체 선수를 찾음
                var bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, true);
                if (bestAvailableSub == null && playerOut.CurrentStamina < 15f)
                {
                    bestAvailableSub = FindBestSubstitute(teamRoster, playerOut, false);
                }

                if (bestAvailableSub != null)
                {
                    PerformSubstitution(playerOut, bestAvailableSub);
                    substitutionMade = true; // 교체가 이루어졌으므로, 팀 상태를 다시 처음부터 체크하기 위해 루프를 반복
                }
            }
        } while (substitutionMade); // 교체가 한 번이라도 발생했다면, 추가 교체가 필요한지 다시 검사
    }

    private GamePlayer FindBestSubstitute(List<GamePlayer> roster, GamePlayer playerOut, bool requireHighStamina)
    {
        var positionFlexibility = new Dictionary<int, List<int>> {
            { 1, new List<int> { 1, 2 } }, { 2, new List<int> { 2, 1, 3 } },
            { 3, new List<int> { 3, 2, 4 } }, { 4, new List<int> { 4, 5, 3 } },
            { 5, new List<int> { 5, 4 } }
        };
        var outgoingPosition = playerOut.Rating.position;

        var candidates = roster.Where(p => !p.IsOnCourt && !p.IsEjected); // [수정] 퇴장/부상 선수 제외

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
                .Where(p => !p.IsOnCourt && !p.IsEjected) // [수정] 퇴장/부상 선수 제외
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
        
        string teamName = _homeTeamRoster.Any(p => p.Rating.player_id == playerIn.Rating.player_id) ? CurrentState.HomeTeamName : CurrentState.AwayTeamName;
        AddLog($"--- {teamName} SUB: {playerIn.Rating.name} IN (Stamina: {(int)playerIn.CurrentStamina}), {playerOut.Rating.name} OUT (Stamina: {(int)playerOut.CurrentStamina}) ---");
        AddUILog($"SUB: {playerIn.Rating.name} IN (Stamina:{(int)playerIn.CurrentStamina}), {playerOut.Rating.name} OUT (Stamina:{(int)playerOut.CurrentStamina})", playerIn);
    }

    /// <summary>
    /// [추가] UI로부터 수동 교체 요청을 처리하는 메서드
    /// </summary>
    public bool RequestManualSubstitution(GamePlayer playerIn, GamePlayer playerOut)
    {
        // 1. 유효성 검사: 두 선수가 모두 존재하고, 같은 팀이며, 서로 다른 선수여야 함
        if (playerIn == null || playerOut == null || playerIn.TeamId != playerOut.TeamId || playerIn == playerOut)
        {
            Debug.LogWarning("Invalid substitution request: Players are null, not on the same team, or the same player.");
            return false;
        }

        // 2. 유효성 검사: 한 명은 코트에, 다른 한 명은 벤치에 있어야 함
        if (playerIn.IsOnCourt == playerOut.IsOnCourt)
        {
            Debug.LogWarning("Invalid substitution request: Both players are on court or on bench.");
            return false;
        }

        // 3. 유효성 검사: 퇴장당한 선수는 경기에 투입될 수 없음
        if (playerIn.IsEjected)
        {
            Debug.LogWarning($"Invalid substitution request: {playerIn.Rating.name} is ejected.");
            return false;
        }
        
        // playerIn이 벤치 선수, playerOut이 코트 선수여야 함. 아니라면 순서를 바꿈.
        if (playerIn.IsOnCourt)
        {
            var temp = playerIn;
            playerIn = playerOut;
            playerOut = temp;
        }
        
        // 실제 교체 수행
        PerformSubstitution(playerOut, playerIn);
        return true;
    }
    #endregion

    #region New Foul Out & Injury Logic

    public void EjectPlayer(GamePlayer player, string reason)
    {
        player.IsEjected = true;
        player.IsOnCourt = false;

        AddLog($"--- PLAYER EJECTED: {player.Rating.name} is out for the rest of the game due to {reason}. ---");
        AddUILog($"EJECTED: {player.Rating.name} ({reason})", player);

        var teamRoster = (player.TeamId == 0) ? _homeTeamRoster : _awayTeamRoster;
        var substitute = FindBestSubstitute(teamRoster, player, false);
        
        if (substitute != null)
        {
            PerformSubstitution(player, substitute);
        }
        else
        {
            AddLog($"--- {player.Rating.name}'s team has no available substitutes! They must play shorthanded. ---");
            // 5명 미만으로 플레이하는 로직은 복잡하므로 여기서는 로그만 남김
        }
    }

    private void CheckForInjuries()
    {
        foreach (var player in GetAllPlayersOnCourt())
        {
            // [수정 a] 분당 체크이므로 /48 제거. injury_possibility = injury_rating * (100 - stamina) / 5
            float injuryPossibility = player.Rating.injury * (100f - player.CurrentStamina) / 5f;

            if (UnityEngine.Random.Range(0f, 100f) < injuryPossibility)
            {
                EjectPlayer(player, "Injury");
                // 부상 발생 시, 해당 선수의 루프는 중단하고 다음 선수 체크
                // (한 프레임에 여러명 부상 방지)
                break; 
            }
        }
    }

    private int GenerateInjuryDuration()
    {
        float rand = UnityEngine.Random.Range(0f, 1f);
        if (rand < 0.82f) // 1~7일 (82%)
        {
            return UnityEngine.Random.Range(1, 8);
        }
        else if (rand < 0.97f) // 8~30일 (15%)
        {
            return UnityEngine.Random.Range(8, 31);
        }
        else // 31~178일 (3%)
        {
            return UnityEngine.Random.Range(31, 179);
        }
    }

    private void RecalculateEffectiveOverall(GamePlayer player)
    {
        var adjustedRating = GetAdjustedRating(player);
        // 실질 OVR 계산: 주요 능력치들의 평균으로 간단히 계산 (세부 조정 가능)
        int effectiveOvr = (
            adjustedRating.closeShot + adjustedRating.midRangeShot + adjustedRating.threePointShot +
            adjustedRating.drivingDunk + adjustedRating.layup + adjustedRating.freeThrow +
            adjustedRating.interiorDefense + adjustedRating.perimeterDefense + adjustedRating.steal + adjustedRating.block +
            adjustedRating.speed + adjustedRating.passIQ + adjustedRating.ballHandle +
            adjustedRating.offensiveRebound + adjustedRating.defensiveRebound
        ) / 15;

        player.EffectiveOverall = effectiveOvr;
    }

    #endregion

    #region Helper & Printing Functions
    public Node GetRootNode() => _rootOffenseNode;

    public List<GamePlayer> GetAllPlayersOnCourt() => _homeTeamRoster.Concat(_awayTeamRoster).Where(p => p.IsOnCourt).ToList();
    
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

    public void AddUILog(string message, GamePlayer eventOwner)
    {
        float clock = Mathf.Max(0, CurrentState.GameClockSeconds);
        string timeStamp = $"Q{CurrentState.Quarter} {(int)clock / 60:00}:{(int)clock % 60:00}";

        string teamAbbreviation = "";
        if (eventOwner != null)
        {
            // [수정] team_abbv를 PlayerRating이 아닌 CurrentState에 저장된 팀 약칭에서 가져옴
            teamAbbreviation = (eventOwner.TeamId == 0) ? CurrentState.HomeTeamAbbr : CurrentState.AwayTeamAbbr;
        }

        OnUILogGenerated?.Invoke($"{timeStamp} | {teamAbbreviation} | {message}");
    }

    public GamePlayer GetRandomDefender(int attackingTeamId)
    {
        var defenders = GetPlayersOnCourt(1 - attackingTeamId);
        if (defenders.Count == 0) return null;
        return defenders[UnityEngine.Random.Range(0, defenders.Count)];
    }
    
    public NodeState ResolveShootingFoul(GamePlayer shooter, GamePlayer defender, int freeThrows)
    {
        defender.Stats.PersonalFouls++; // Fouls -> PersonalFouls
        if (defender.Stats.PersonalFouls >= 6)
        {
            EjectPlayer(defender, "fouling out");
        }
        
        return new Action_ShootFreeThrows(shooter, freeThrows, _random).Evaluate(this, shooter);
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
            CurrentState.LastPasser = rebounder; // 공 잡은 선수 설정
            CurrentState.PotentialAssister = null; // 리바운드 시 어시스트 초기화
        }
        else
        {
            rebounder.Stats.DefensiveRebounds++;
            AddLog($"{rebounder.Rating.name} grabs the defensive rebound.");
            AddUILog($"{rebounder.Rating.name} grabs the defensive rebound.", rebounder);
            CurrentState.PossessingTeamId = rebounder.TeamId;
            CurrentState.ShotClockSeconds = 24f;
            CurrentState.LastPasser = null; // 공격권 전환, 어시스트 초기화
            CurrentState.PotentialAssister = null; // 공격권 전환, 어시스트 초기화
        }
    }

    public void ResolveTurnover(GamePlayer offensivePlayer, GamePlayer defensivePlayer, bool isSteal)
    {
        offensivePlayer.Stats.Turnovers++;
        if (isSteal)
        {
            defensivePlayer.Stats.Steals++;
            AddLog($"{defensivePlayer.Rating.name} steals the ball from {offensivePlayer.Rating.name}!");
            AddUILog($"{defensivePlayer.Rating.name} STEALS the ball from {offensivePlayer.Rating.name}", defensivePlayer);
        }
        else
        {
            AddLog($"{offensivePlayer.Rating.name} commits a turnover.");
            AddUILog($"{offensivePlayer.Rating.name} commits a turnover", offensivePlayer);
        }
        
        CurrentState.PossessingTeamId = 1 - offensivePlayer.TeamId;
        CurrentState.ShotClockSeconds = 24f;
        CurrentState.LastPasser = null;
        CurrentState.PotentialAssister = null;
    }

    public void ResolveBlock(GamePlayer shooter, GamePlayer blocker)
    {
        blocker.Stats.Blocks++;
        AddLog($"{blocker.Rating.name} BLOCKS the shot from {shooter.Rating.name}!");
        AddUILog($"{blocker.Rating.name} BLOCKS the shot by {shooter.Rating.name}", blocker);
        ResolveRebound(shooter); // 블락된 공은 리바운드로 이어짐
    }
    
    public PlayerRating GetAdjustedRating(GamePlayer player)
    {
        if (player == null) return new PlayerRating();
        
        var baseRating = player.Rating;
        var adjustedRating = new PlayerRating();
        
        // --- [수정 b] 부상자 필터 로직 ---
        if (player.IsCurrentlyInjured)
        {
            // 부상 시 능력치를 절반으로 감소 (필요한 모든 능력치 추가)
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
            // 부상이 아닐 경우 기존 능력치 복사
            adjustedRating = baseRating;
        }

        // --- 스태미나에 따른 능력치 감소 (기존 로직) ---
        float stamina = player.CurrentStamina;
        if (stamina >= 75f) return adjustedRating; // 스태미나 75 이상이면 패널티 없음
        
        float fatigueFactor = (75f - stamina) / 75f; // 기준 75로 수정
        float maxPenalty = 15.0f; 
        float penalty = fatigueFactor * maxPenalty;

        // 이미 복사된 adjustedRating에 페널티 적용
        adjustedRating.closeShot = (int)Mathf.Max(1, adjustedRating.closeShot - penalty);
        adjustedRating.midRangeShot = (int)Mathf.Max(1, adjustedRating.midRangeShot - penalty);
        adjustedRating.threePointShot = (int)Mathf.Max(1, adjustedRating.threePointShot - penalty);
        adjustedRating.layup = (int)Mathf.Max(1, adjustedRating.layup - penalty);
        adjustedRating.drivingDunk = (int)Mathf.Max(1, adjustedRating.drivingDunk - penalty);
        adjustedRating.speed = (int)Mathf.Max(1, adjustedRating.speed - penalty);
        adjustedRating.perimeterDefense = (int)Mathf.Max(1, adjustedRating.perimeterDefense - penalty);
        adjustedRating.interiorDefense = (int)Mathf.Max(1, adjustedRating.interiorDefense - penalty);
        adjustedRating.block = (int)Mathf.Max(1, adjustedRating.block - penalty);
        adjustedRating.steal = (int)Mathf.Max(1, adjustedRating.steal - penalty);
        adjustedRating.drawFoul = (int)Mathf.Max(1, adjustedRating.drawFoul - (penalty * 0.5f));
        adjustedRating.offensiveRebound = (int)Mathf.Max(1, adjustedRating.offensiveRebound - penalty);
        adjustedRating.defensiveRebound = (int)Mathf.Max(1, adjustedRating.defensiveRebound - penalty);
        adjustedRating.ballHandle = (int)Mathf.Max(1, adjustedRating.ballHandle - (penalty * 0.7f));
        
        return adjustedRating;
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
            string line = $"{p.Rating.name}: {p.Stats.Points} PTS, {p.Stats.FieldGoalsMade}/{p.Stats.FieldGoalsAttempted} FG, {p.Stats.DefensiveRebounds+p.Stats.OffensiveRebounds} REB, {p.Stats.Assists} AST, {p.Stats.PersonalFouls} PF"; // Fouls -> PersonalFouls
            Debug.Log(line);
        }
    }
    #endregion
}