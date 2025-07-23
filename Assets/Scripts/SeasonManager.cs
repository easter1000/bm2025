using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class SeasonManager : MonoBehaviour
{
    private static SeasonManager _instance;
    public static SeasonManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SeasonManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("SeasonManager");
                    _instance = obj.AddComponent<SeasonManager>();
                }
            }
            return _instance;
        }
    }

    private LocalDbManager _dbManager;
    private TradeManager _tradeManager;
    
    // 하루가 지나는 시간(초). 실제 게임 시간 기준
    // public float realSecondsPerDay = 1.0f; // 더 이상 사용하지 않음

    private int _currentSeason; // 값은 DB에서 로드
    private System.DateTime _currentDate; // 값은 DB에서 로드
    // private float _dayTimer = 0f; // 더 이상 사용하지 않음
    private string _userTeamAbbr; // 유저 팀 약어 저장

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _dbManager = LocalDbManager.Instance;
        _tradeManager = FindAnyObjectByType<TradeManager>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            Debug.Log("No EventSystem found in the scene, creating a new one.");
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    void Start()
    {
        // _dbManager와 _tradeManager 초기화 코드를 Awake로 이동
    }

    public void InitializeNewSeason(int season, string userTeamAbbr)
    {
        _currentSeason = season;
        _userTeamAbbr = userTeamAbbr;
        _dbManager = LocalDbManager.Instance; // DB 매니저 인스턴스 확인
        _tradeManager = FindAnyObjectByType<TradeManager>();
        ScheduleManager.Instance.GenerateNewSeasonSchedule(season);
    }

    void Update()
    {
        // 자동 시간 흐름 로직 제거
    }

    public int GetCurrentSeason()
    {
        // DB에서 직접 가져오거나, 시작 시점에 로드
        var user = LocalDbManager.Instance.GetUser();
        return user?.CurrentSeason ?? DateTime.Now.Year;
    }
    
    public DateTime GetCurrentDate()
    {
        var user = LocalDbManager.Instance.GetUser();
        if (user != null && DateTime.TryParse(user.CurrentDate, out DateTime date))
        {
            return date;
        }
        return DateTime.Now; // Fallback
    }

    /// <summary>
    /// AI 팀들이 서로 트레이드를 시도하고, 유저에게 제안할 트레이드 목록을 반환.
    /// </summary>
    /// <returns>유저에게 제안된 TradeOffer 리스트</returns>
    public List<TradeOffer> AttemptAiToAiTrades()
    {
        if (_tradeManager == null)
        {
            _tradeManager = TradeManager.Instance;
        }
        if (_dbManager == null) _dbManager = LocalDbManager.Instance;

        _userTeamAbbr = _dbManager.GetUser()?.SelectedTeamAbbr;
        _currentSeason = _dbManager.GetUser()?.CurrentSeason ?? DateTime.Now.Year;

        var userTradeOffers = new List<TradeOffer>();
        List<Team> allTeams = _dbManager.GetAllTeams();
        var teamFinances = _dbManager.GetTeamFinancesForSeason(_currentSeason)
                                     .ToDictionary(f => f.TeamAbbr);
        
        List<Team> aiTeams = allTeams.Where(t => t.team_abbv != _userTeamAbbr && t.team_abbv != "FA").ToList();
        var rand = new System.Random();

        // =================================================================
        // 1. [NEW] AI 팀 문제 식별 및 자동 해결 단계
        // =================================================================
        Debug.Log("[AI Management] Starting AI team issue resolution phase...");
        foreach (var team in aiTeams)
        {
            ResolveTeamIssues(team, allTeams, teamFinances, rand);
        }
        Debug.Log("[AI Management] AI team issue resolution phase finished.");


        // =================================================================
        // 2. [EXISTING] 일반 AI 기회 트레이드 시도 단계
        // =================================================================
        for (int i = 0; i < aiTeams.Count; i++)
        {
            for (int j = i + 1; j < aiTeams.Count; j++)
            {
                if (rand.Next(0, 100) < 5) // 5% chance for a random trade attempt
                {
                    ProposeFairTradeBetweenTeams(aiTeams[i], aiTeams[j], teamFinances, rand);
                }
            }
        }

        // 3. AI -> User 트레이드 제안
        Team userTeam = allTeams.FirstOrDefault(t => t.team_abbv == _userTeamAbbr);
        if (userTeam != null)
        {
            // 어떤 팀이 먼저 제안할지 순서를 섞음
            var shuffledAiTeams = aiTeams.OrderBy(t => rand.Next()).ToList();
            foreach (var aiTeam in shuffledAiTeams)
            {
                if (rand.Next(0, 100) < 100)
                {
                    var offer = GenerateAndProposeSmartTrade(aiTeam, teamFinances[aiTeam.team_abbv], userTeam, teamFinances[userTeam.team_abbv], rand, true);
                    if (offer != null)
                    {
                        userTradeOffers.Add(offer);
                        break;
                    }
                }
            }
        }
        return userTradeOffers;
    }

    // ============== NEW METHODS for AI Team Management ==============

    /// <summary>
    /// 특정 팀의 재정 및 로스터 문제를 해결하기 위한 총괄 메서드.
    /// 문제가 해결될 때까지 FA영입, 트레이드, 방출을 순차적으로 시도합니다.
    /// </summary>
    private void ResolveTeamIssues(Team team, List<Team> allTeams, Dictionary<string, TeamFinance> finances, System.Random rand)
    {
        return;
    }

    /// <summary>
    /// 주전 라인업의 빈 자리를 FA 영입으로 채우려 시도합니다.
    /// </summary>
    /// <returns>한 명이라도 영입에 성공하면 true를 반환합니다.</returns>
    private bool AttemptToFillGapsViaFA(Team team, TeamFinance finance, List<PlayerInfo> rosterWithStatus, List<PlayerRating> starters)
    {
        if (rosterWithStatus.Count >= 15) return false;

        var starterPositions = new HashSet<int>(starters.Select(p => p.position));
        int[] allPositions = { 1, 2, 3, 4, 5 }; // PG, SG, SF, PF, C
        var neededPositions = allPositions.Where(p => !starterPositions.Contains(p)).ToList();

        if (!neededPositions.Any()) return false;

        var freeAgents = _dbManager.GetFreeAgents();
        if (!freeAgents.Any()) return false;

        bool signedAnyone = false;
        foreach (var posCode in neededPositions)
        {
            var recruit = freeAgents
                .OrderByDescending(fa => fa.position == posCode)
                .ThenByDescending(fa => fa.overallAttribute)
                .FirstOrDefault();

            if (recruit != null)
            {
                long estimatedSalary = _tradeManager.CalculateMarketSalary(recruit.currentValue);
                if (finance.CurrentTeamSalary + estimatedSalary <= finance.TeamBudget)
                {
                    Debug.Log($"[AI Management] {team.team_abbv} signs FA {recruit.name} to fill position {posCode}.");
                    _dbManager.UpdatePlayerTeam(new List<int> { recruit.player_id }, team.team_abbv);
                    
                    var newRoster = _dbManager.GetPlayersByTeam(team.team_abbv);
                    var newStarters = GetStarters(team, newRoster);
                    _dbManager.UpdateBestFive(team.team_abbv, newStarters.Select(p => p.player_id).ToList());
                    
                    _dbManager.RecalculateAndSaveAllTeamSalaries();
                    freeAgents.Remove(recruit);
                    signedAnyone = true;
                }
            }
        }
        return signedAnyone;
    }

        /// <summary>
    /// 예산 또는 주전 공백 문제를 트레이드로 해결하려 시도합니다. (효율성 및 데이터 구조 개선 버전)
    /// [수정] 일정 수준 이상의 이득이 되는 거래를 찾으면 즉시 시도하고, 성공하면 종료합니다.
    /// </summary>
    private bool AttemptToFixIssuesByTrade(Team team, TeamFinance finance, List<PlayerInfo> rosterWithStatus, List<PlayerRating> starters, List<Team> allTeams, Dictionary<string, TeamFinance> finances, System.Random rand)
    {
        bool isOverBudget = finance.CurrentTeamSalary > finance.TeamBudget;
        var neededPositions = Enumerable.Range(1, 5).Where(p => !starters.Any(s => s.position == p)).ToList();
        bool needsPlayers = neededPositions.Any();

        if (!isOverBudget && !needsPlayers) return false;

        // 모든 잠재적 파트너 팀의 선수 정보(Rating+Status)를 미리 로드합니다.
        var allPartnerRosters = allTeams
            .Where(t => t.team_abbv != team.team_abbv && t.team_abbv != _userTeamAbbr && t.team_abbv != "FA")
            .ToDictionary(t => t.team_abbv, t => _dbManager.GetPlayersByTeamWithStatus(t.team_abbv));

        // 우리 팀이 트레이드로 내놓을 선수 목록을 가져옵니다.
        var myTradeCandidates = GetMyTradeCandidates(rosterWithStatus, isOverBudget);

        // 모든 조합을 찾는 대신, 실행 가능한 첫 트레이드를 시도합니다.
        foreach (var myPlayerInfo in myTradeCandidates)
        {
            // 파트너 순서를 무작위로 섞어 매번 같은 팀과 먼저 시도하는 것을 방지합니다.
            foreach (var partnerEntry in allPartnerRosters.OrderBy(x => rand.Next()))
            {
                var partnerTeamAbbr = partnerEntry.Key;
                var partnerRoster = partnerEntry.Value;
                var theirTradeCandidates = GetTheirTradeCandidates(partnerRoster, myPlayerInfo, isOverBudget, neededPositions);

                foreach (var theirPlayerInfo in theirTradeCandidates)
                {
                    var myPlayerRating = myPlayerInfo.Rating;
                    var theirPlayerRating = theirPlayerInfo.Rating;

                    // 1. 기본적인 트레이드 가능 여부 확인
                    if (!IsTradeViable(team, finance, myPlayerRating, allTeams.First(t => t.team_abbv == partnerTeamAbbr), finances[partnerTeamAbbr], theirPlayerRating))
                    {
                        continue;
                    }

                    // 2. 우리 팀 입장에서 이득이 되는지 먼저 계산
                    float score = CalculateTradeScoreForMyTeam(myPlayerInfo, theirPlayerInfo, isOverBudget, neededPositions);

                    // 이득이 0 이하인 트레이드는 고려하지 않음 (score가 높을수록 좋음)
                    if (score <= 0)
                    {
                        continue;
                    }

                    // 3. 상대방이 수락할지 평가
                    var result = _tradeManager.EvaluateTrade(team.team_abbv, new List<PlayerRating> { myPlayerRating }, partnerTeamAbbr, new List<PlayerRating> { theirPlayerRating }, rand);
                    
                    // 4. 상대방이 수락하면 즉시 트레이드 실행 후 종료
                    if (result.IsAccepted)
                    {
                        Debug.Log($"[AI Management] Found viable trade. Executing: {team.team_abbv} trades {myPlayerRating.name} for {theirPlayerRating.name} with {partnerTeamAbbr}. Score: {score}");
                        
                        _tradeManager.ExecuteTrade(team.team_abbv, new List<PlayerRating> { myPlayerRating }, partnerTeamAbbr, new List<PlayerRating> { theirPlayerRating });
                        _dbManager.RecalculateAndSaveAllTeamSalaries();
                        
                        // 성공적으로 문제를 해결했으므로 true 반환
                        return true; 
                    }
                    // 거절당하면 다음 후보 선수로 계속 진행
                }
            }
        }
        
        // 모든 후보를 시도했지만 성공적인 트레이드가 없었음
        return false; 
    }

    // [FIXED] PlayerStatus에 currentValue가 없던 오류 수정
    private List<PlayerInfo> GetMyTradeCandidates(List<PlayerInfo> roster, bool isOverBudget)
    {
        if (isOverBudget)
        {
            // 연봉이 높은 선수를 우선으로
            return roster.OrderByDescending(p => p.Status.Salary).ToList();
        }
        
        // 주전 보강: 가치가 높은 선수 우선 (currentValue는 Rating에 있음)
        return roster.OrderByDescending(p => p.Rating.currentValue).ToList();
    }
    
    // [NEW] 누락되었던 GetTheirTradeCandidates 메서드 구현
    private List<PlayerInfo> GetTheirTradeCandidates(List<PlayerInfo> partnerRoster, PlayerInfo myPlayer, bool myTeamIsOverBudget, List<int> myNeededPositions)
    {
        // 상대방이 나에게서 받을 선수의 가치
        var myPlayerValue = myPlayer.Rating.currentValue;

        // 1. 내가 필요한 포지션의 선수들을 우선적으로 고려
        if (myNeededPositions.Any())
        {
            return partnerRoster
                .Where(p => myNeededPositions.Contains(p.Rating.position))
                .OrderBy(p => Math.Abs(p.Rating.currentValue - myPlayerValue)) // 가치가 비슷한 선수
                .ToList();
        }

        // 2. 예산 감축이 목표일 경우, 나보다 연봉이 낮은 선수들을 고려
        if (myTeamIsOverBudget)
        {
            return partnerRoster
                .Where(p => p.Status.Salary < myPlayer.Status.Salary)
                .OrderBy(p => Math.Abs(p.Rating.currentValue - myPlayerValue))
                .ToList();
        }

        // 3. 일반적인 경우, 비슷한 가치의 모든 선수를 고려
        return partnerRoster.OrderBy(p => Math.Abs(p.Rating.currentValue - myPlayerValue)).ToList();
    }


    private bool IsTradeViable(Team myTeam, TeamFinance myFinance, PlayerRating myPlayer, Team partnerTeam, TeamFinance partnerFinance, PlayerRating theirPlayer)
    {
        long mySalary = _dbManager.GetPlayerStatus(myPlayer.player_id)?.Salary ?? 0;
        long theirSalary = _dbManager.GetPlayerStatus(theirPlayer.player_id)?.Salary ?? 0;
        
        if (partnerFinance.CurrentTeamSalary - theirSalary + mySalary > partnerFinance.TeamBudget) return false;
        
        return true;
    }

    // [FIXED] PlayerRating 대신 PlayerInfo를 받도록 시그니처 및 내부 로직 수정
    private float CalculateTradeScoreForMyTeam(PlayerInfo myPlayer, PlayerInfo theirPlayer, bool isOverBudget, List<int> neededPositions)
    {
        float score = 0;

        // 1. 연봉 변화 점수 (Status 객체에서 바로 접근)
        long mySalary = myPlayer.Status.Salary;
        long theirSalary = theirPlayer.Status.Salary;
        long salaryChange = mySalary - theirSalary; // 양수 = 연봉 절감
        if (isOverBudget)
        {
            score += salaryChange / 10000f; 
        }

        // 2. 포지션 필요성 점수 (Rating 객체에서 바로 접근)
        if (neededPositions.Contains(theirPlayer.Rating.position) && !neededPositions.Contains(myPlayer.Rating.position))
        {
            score += theirPlayer.Rating.overallAttribute * 2;
        }

        // 3. 전반적인 선수 가치 변화 (Rating 객체에서 바로 접근)
        score += (theirPlayer.Rating.currentValue - myPlayer.Rating.currentValue) / 100f;

        return score;
    }


    /// <summary>
    /// 예산이 맞을 때까지 가치가 낮은 선수를 방출합니다.
    /// </summary>
    // [FIXED] List<PlayerRating> 대신 List<PlayerInfo>를 받도록 수정
    private void ReleasePlayersToMeetBudget(Team team, TeamFinance finance, List<PlayerInfo> rosterWithStatus)
    {
        var sortedRoster = rosterWithStatus
            .Where(p => p.Status != null)
            .OrderBy(p => p.Rating.currentValue) // 가치가 낮은 순으로 정렬
            .ToList();
        
        Debug.LogWarning($"[AI Management] {team.team_abbv} must release players to meet budget.");

        while(finance.CurrentTeamSalary > finance.TeamBudget && sortedRoster.Count > 8)
        {
            var playerToRelease = sortedRoster.First();
            Debug.Log($"[AI Management] Releasing {playerToRelease.Rating.name} from {team.team_abbv}.");
            _dbManager.ReleasePlayer(playerToRelease.Rating.player_id);
            
            long annualSalary = (playerToRelease.Status.YearsLeft > 0) ? (playerToRelease.Status.Salary / playerToRelease.Status.YearsLeft) : 0;
            finance.CurrentTeamSalary -= annualSalary;
            sortedRoster.RemoveAt(0);
        }
        
        _dbManager.UpdateBestFive(team.team_abbv, GetStarters(team, _dbManager.GetPlayersByTeam(team.team_abbv)).Select(p => p.player_id).ToList());
    }

    /// <summary>
    /// FA 영입을 위해 로스터 공간을 확보하려고 가치가 가장 낮은 선수를 방출합니다.
    /// </summary>
    // [FIXED] List<PlayerRating> 대신 List<PlayerInfo>를 받도록 수정
    private void ReleasePlayerToMakeRosterSpace(Team team, List<PlayerInfo> roster)
    {
        if (roster.Count < 15) return;
    
        var playerToRelease = roster.OrderBy(p => p.Rating.currentValue).FirstOrDefault();
        if(playerToRelease != null)
        {
            Debug.LogWarning($"[AI Management] {team.team_abbv} releases {playerToRelease.Rating.name} to make roster space.");
            _dbManager.ReleasePlayer(playerToRelease.Rating.player_id);
            _dbManager.UpdateBestFive(team.team_abbv, GetStarters(team, _dbManager.GetPlayersByTeam(team.team_abbv)).Select(p => p.player_id).ToList());
        }
    }

    /// <summary>
    /// 팀의 현재 주전 라인업을 반환하는 헬퍼 메서드.
    /// </summary>
    private List<PlayerRating> GetStarters(Team team, List<PlayerRating> roster)
    {
        var teamEntity = _dbManager.GetTeam(team.team_abbv);
        if (string.IsNullOrEmpty(teamEntity?.best_five))
        {
            // best_five가 없는 경우, 포지션별 OVR이 가장 높은 선수로 자동 설정 (임시 방편)
            return roster.GroupBy(p => p.position)
                         .Select(g => g.OrderByDescending(p => p.overallAttribute).First())
                         .Take(5)
                         .ToList();
        }

        var starterIds = new HashSet<int>();
        foreach(var idStr in teamEntity.best_five.Split(','))
        {
            if (int.TryParse(idStr, out int pid) && pid > 0)
            {
                starterIds.Add(pid);
            }
        }
        return roster.Where(p => starterIds.Contains(p.player_id)).ToList();
    }

    // [FIXED] 클래스 정의와 사용되는 속성명 일치 (MyPlayerInfo -> MyPlayer)
    private class BestTradeOption
    {
        public PlayerInfo MyPlayer { get; set; }
        public PlayerInfo TheirPlayer { get; set; }
        public string PartnerTeamAbbr { get; set; }
        public float Score { get; set; }
    }

    private void ProposeFairTradeBetweenTeams(Team teamA, Team teamB, Dictionary<string, TeamFinance> finances, System.Random rand)
    {
        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv).OrderByDescending(p => p.currentValue).ToList();
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv).OrderByDescending(p => p.currentValue).ToList();

        if (rosterA.Count < 2 || rosterB.Count < 2) return;
        
        var playerA = rosterA[rand.Next(0, rosterA.Count)];
        var playerB = rosterB[rand.Next(0, rosterB.Count)];

        var resultA = _tradeManager.EvaluateTrade(
            teamA.team_abbv, new List<PlayerRating> { playerA },
            teamB.team_abbv, new List<PlayerRating> { playerB },
            rand
        );
        
        var resultB = _tradeManager.EvaluateTrade(
            teamB.team_abbv, new List<PlayerRating> { playerB },
            teamA.team_abbv, new List<PlayerRating> { playerA },
            rand
        );

        if (resultA.IsAccepted && resultB.IsAccepted)
        {
            Debug.Log($"[Fair Trade] Executing trade between {teamA.team_abbv} and {teamB.team_abbv}: {playerA.name} <-> {playerB.name}");
            _tradeManager.ExecuteTrade(
                teamA.team_abbv, new List<PlayerRating> { playerA },
                teamB.team_abbv, new List<PlayerRating> { playerB }
            );
        }
    }

    private enum TeamStrategy { Rebuilding, Contending, Standard }

    private TeamStrategy GetTeamStrategy(TeamFinance finance)
    {
        if (finance == null || (finance.Wins + finance.Losses == 0)) return TeamStrategy.Standard;
        float winPercentage = (float)finance.Wins / (finance.Wins + finance.Losses);
        if (winPercentage > 0.65) return TeamStrategy.Contending;
        if (winPercentage < 0.35) return TeamStrategy.Rebuilding;
        return TeamStrategy.Standard;
    }
    
    private TradeOffer GenerateAndProposeSmartTrade(Team teamA, TeamFinance financeA, Team teamB, TeamFinance financeB, System.Random rand, bool isUserTeamInvolved = false)
    {
        var strategyA = GetTeamStrategy(financeA);
        var strategyB = GetTeamStrategy(financeB);

        if (strategyA == strategyB && rand.Next(0, 100) < 30) return null;

        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv);
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv);

        if (rosterA.Count < 8 || rosterB.Count < 8) return null;

        PlayerRating playerToTradeFromA = FindTradableAsset(rosterA, strategyA, strategyB);
        PlayerRating playerToTradeFromB = FindTradableAsset(rosterB, strategyB, strategyA);

        if (playerToTradeFromA == null || playerToTradeFromB == null) return null;
        
        var offer = new TradeOffer(
            teamA, new List<PlayerRating> { playerToTradeFromA },
            teamB, new List<PlayerRating> { playerToTradeFromB }
        );

        if (isUserTeamInvolved)
        {
            Debug.Log($"[USER TRADE PROPOSAL] {teamA.team_abbv} offers {playerToTradeFromA.name} for {teamB.team_abbv}'s {playerToTradeFromB.name}");
            return offer;
        }
        else
        {
            Debug.Log($"[AI-AI Trade Proposal] {teamA.team_abbv} ({strategyA}) -> {teamB.team_abbv} ({strategyB})");
            var result = _tradeManager.EvaluateTrade(
                teamA.team_abbv, new List<PlayerRating> { playerToTradeFromA },
                teamB.team_abbv, new List<PlayerRating> { playerToTradeFromB },
                rand
            );

            if (result.IsAccepted)
            {
                _tradeManager.ExecuteTrade(
                    teamA.team_abbv, new List<PlayerRating> { playerToTradeFromA },
                    teamB.team_abbv, new List<PlayerRating> { playerToTradeFromB }
                );
            }
            return null;
        }
    }
    
    private PlayerRating FindTradableAsset(List<PlayerRating> roster, TeamStrategy myStrategy, TeamStrategy theirStrategy)
    {
        IEnumerable<PlayerRating> candidates;

        if (myStrategy == TeamStrategy.Contending)
        {
            candidates = roster.Where(p => (p.potential - p.overallAttribute) >= 8 && p.age < 25)
                                 .OrderByDescending(p => p.currentValue);
            
            if (!candidates.Any())
            {
                candidates = roster.OrderBy(p => p.overallAttribute);
            }
        }
        else if (myStrategy == TeamStrategy.Rebuilding)
        {
            candidates = roster.Where(p => p.age > 28 && p.currentValue > 40)
                                 .OrderByDescending(p => p.currentValue);

            if (!candidates.Any())
            {
                candidates = roster.OrderByDescending(p => p.currentValue);
            }
        }
        else // Standard
        {
            candidates = roster.OrderByDescending(p => p.currentValue).Skip(5);
        }

        return candidates.FirstOrDefault();
    }
}