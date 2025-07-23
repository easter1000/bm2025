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
            Debug.LogError("TradeManager is not initialized! This should not happen if SeasonManager is set up correctly.");
            return new List<TradeOffer>();
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
        const int maxAttempts = 5; // 무한 루프 방지를 위한 시도 횟수 제한

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // 매 시도마다 최신 정보 갱신
            var finance = finances.GetValueOrDefault(team.team_abbv);
            if (finance == null) return;
            var roster = _dbManager.GetPlayersByTeam(team.team_abbv);
            var starters = GetStarters(team, roster);

            // 1. 문제 식별
            bool isOverBudget = finance.CurrentTeamSalary > finance.TeamBudget;
            bool hasRosterGaps = starters.Count < 5;

            // 2. 문제가 없으면 즉시 종료
            if (!isOverBudget && !hasRosterGaps)
            {
                if (attempt > 1) Debug.Log($"[AI Management] {team.team_abbv}: Issues resolved in {attempt - 1} attempts.");
                return;
            }

            Debug.LogWarning($"[AI Management] {team.team_abbv} (Attempt {attempt}): Issues found! Over budget: {isOverBudget}, Roster gaps: {hasRosterGaps}");

            // 3. 문제 해결 시도 (가장 저렴하고 빠른 방법부터)

            // 3.1. 주전 공백이 있으면 FA 영입부터 시도
            if (hasRosterGaps)
            {
                if (AttemptToFillGapsViaFA(team, finance, roster, starters))
                {
                    continue; // 해결됐으면 처음부터 문제 다시 확인
                }
            }

            // 3.2. FA 영입이 안됐거나, 여전히 문제가 남아있다면 트레이드 시도
            if (AttemptToFixIssuesByTrade(team, finance, roster, starters, allTeams, finances, rand))
            {
                continue;
            }

            // 3.3. 트레이드도 실패했다면, 최후의 수단으로 방출
            if (isOverBudget)
            {
                ReleasePlayersToMeetBudget(team, finance, roster);
                continue;
            }
            
            if (hasRosterGaps && roster.Count >= 15)
            {
                ReleasePlayerToMakeRosterSpace(team, roster);
                continue;
            }

            Debug.LogError($"[AI Management] CRITICAL: Could not resolve issues for {team.team_abbv} after all attempts.");
            break;
        }
    }

    /// <summary>
    /// 주전 라인업의 빈 자리를 FA 영입으로 채우려 시도합니다.
    /// </summary>
    /// <returns>한 명이라도 영입에 성공하면 true를 반환합니다.</returns>
    private bool AttemptToFillGapsViaFA(Team team, TeamFinance finance, List<PlayerRating> roster, List<PlayerRating> starters)
    {
        if (roster.Count >= 15) return false;

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
                    
                    var newStarters = GetStarters(team, _dbManager.GetPlayersByTeam(team.team_abbv));
                    newStarters.Add(recruit);
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
    /// 예산 또는 주전 공백 문제를 트레이드로 해결하려 시도합니다.
    /// </summary>
    private bool AttemptToFixIssuesByTrade(Team team, TeamFinance finance, List<PlayerRating> roster, List<PlayerRating> starters, List<Team> allTeams, Dictionary<string, TeamFinance> finances, System.Random rand)
    {
        bool isOverBudget = finance.CurrentTeamSalary > finance.TeamBudget;
        var neededPositions = Enumerable.Range(1, 5).Where(p => !starters.Any(s => s.position == p)).ToList();
        bool needsPlayers = neededPositions.Any();

        if (!isOverBudget && !needsPlayers) return false;

        var potentialPartners = allTeams.Where(t => t.team_abbv != team.team_abbv && t.team_abbv != _userTeamAbbr && t.team_abbv != "FA").ToList();
        BestTradeOption bestTrade = null;

        // 우리 팀에서 트레이드 할 선수 후보군 (가치가 높은 순으로)
        var myTradeCandidates = roster.OrderByDescending(p => p.currentValue).ToList();

        foreach (var myPlayer in myTradeCandidates)
        {
            foreach (var partnerTeam in potentialPartners)
            {
                var partnerRoster = _dbManager.GetPlayersByTeam(partnerTeam.team_abbv);
                foreach (var theirPlayer in partnerRoster)
                {
                    // 1. 트레이드 유효성 검사 (각 팀의 재정, 로스터 사이즈 등)
                    if (!IsTradeViable(team, finance, myPlayer, partnerTeam, finances[partnerTeam.team_abbv], theirPlayer)) continue;

                    // 2. 트레이드 평가 (상대방이 수락할지?)
                    var result = _tradeManager.EvaluateTrade(team.team_abbv, new List<PlayerRating> { myPlayer }, partnerTeam.team_abbv, new List<PlayerRating> { theirPlayer }, rand);
                    if (!result.IsAccepted) continue;
                    
                    // 3. 우리 팀 입장에서의 이득 계산
                    float score = CalculateTradeScoreForMyTeam(myPlayer, theirPlayer, isOverBudget, neededPositions);

                    if (bestTrade == null || score > bestTrade.Score)
                    {
                        bestTrade = new BestTradeOption
                        {
                            MyPlayer = myPlayer,
                            TheirPlayer = theirPlayer,
                            PartnerTeamAbbr = partnerTeam.team_abbv,
                            Score = score
                        };
                    }
                }
            }
        }
        
        if (bestTrade != null && bestTrade.Score > 0) // 이득이 되는 트레이드일 경우에만 실행
        {
            Debug.Log($"[AI Management] Executing strategic trade for {team.team_abbv}: {bestTrade.MyPlayer.name} for {bestTrade.TheirPlayer.name} with {bestTrade.PartnerTeamAbbr}. Score: {bestTrade.Score}");
            _tradeManager.ExecuteTrade(team.team_abbv, new List<PlayerRating> { bestTrade.MyPlayer }, bestTrade.PartnerTeamAbbr, new List<PlayerRating> { bestTrade.TheirPlayer });
            _dbManager.RecalculateAndSaveAllTeamSalaries();
            return true;
        }

        return false; 
    }

    private bool IsTradeViable(Team myTeam, TeamFinance myFinance, PlayerRating myPlayer, Team partnerTeam, TeamFinance partnerFinance, PlayerRating theirPlayer)
    {
        long mySalary = _dbManager.GetPlayerStatus(myPlayer.player_id)?.Salary ?? 0;
        long theirSalary = _dbManager.GetPlayerStatus(theirPlayer.player_id)?.Salary ?? 0;
        
        // 상대팀이 예산을 초과하게 되는 트레이드는 불가
        if (partnerFinance.CurrentTeamSalary - theirSalary + mySalary > partnerFinance.TeamBudget) return false;
        
        return true;
    }

    private float CalculateTradeScoreForMyTeam(PlayerRating myPlayer, PlayerRating theirPlayer, bool isOverBudget, List<int> neededPositions)
    {
        float score = 0;

        // 1. 연봉 변화 점수
        long mySalary = _dbManager.GetPlayerStatus(myPlayer.player_id)?.Salary ?? 0;
        long theirSalary = _dbManager.GetPlayerStatus(theirPlayer.player_id)?.Salary ?? 0;
        long salaryChange = mySalary - theirSalary; // 양수 = 연봉 절감
        if (isOverBudget)
        {
            score += salaryChange / 10000f; // 예산 압박이 클수록 연봉 절감의 가치가 큼
        }

        // 2. 포지션 필요성 점수
        if (neededPositions.Contains(theirPlayer.position) && !neededPositions.Contains(myPlayer.position))
        {
            score += theirPlayer.overallAttribute * 2; // 필요한 포지션의 좋은 선수를 얻으면 큰 점수
        }

        // 3. 전반적인 선수 가치 변화
        score += (theirPlayer.currentValue - myPlayer.currentValue) / 100f;

        return score;
    }


    /// <summary>
    /// 예산이 맞을 때까지 가치가 낮은 선수를 방출합니다.
    /// </summary>
    private void ReleasePlayersToMeetBudget(Team team, TeamFinance finance, List<PlayerRating> roster)
    {
        var rosterWithStatus = roster.Select(p => new { Rating = p, Status = _dbManager.GetPlayerStatus(p.player_id) })
                                     .Where(p => p.Status != null)
                                .OrderBy(p => p.Rating.currentValue)
                                .ToList();
        
        Debug.LogWarning($"[AI Management] {team.team_abbv} must release players to meet budget.");

        while(finance.CurrentTeamSalary > finance.TeamBudget && rosterWithStatus.Count > 8)
        {
            var playerToRelease = rosterWithStatus.First();
            Debug.Log($"[AI Management] Releasing {playerToRelease.Rating.name} from {team.team_abbv}.");
            _dbManager.ReleasePlayer(playerToRelease.Rating.player_id);
            
            long annualSalary = (playerToRelease.Status.YearsLeft > 0) ? (playerToRelease.Status.Salary / playerToRelease.Status.YearsLeft) : 0;
            finance.CurrentTeamSalary -= annualSalary;
            rosterWithStatus.RemoveAt(0);
        }
        
        _dbManager.UpdateBestFive(team.team_abbv, GetStarters(team, _dbManager.GetPlayersByTeam(team.team_abbv)).Select(p => p.player_id).ToList());
    }

    /// <summary>
    /// FA 영입을 위해 로스터 공간을 확보하려고 가치가 가장 낮은 선수를 방출합니다.
    /// </summary>
    private void ReleasePlayerToMakeRosterSpace(Team team, List<PlayerRating> roster)
    {
        if (roster.Count < 15) return;
    
        var playerToRelease = roster.OrderBy(p => p.currentValue).FirstOrDefault();
        if(playerToRelease != null)
        {
            Debug.LogWarning($"[AI Management] {team.team_abbv} releases {playerToRelease.name} to make roster space.");
            _dbManager.ReleasePlayer(playerToRelease.player_id);
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
            return new List<PlayerRating>();
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

    private class BestTradeOption
    {
        public PlayerRating MyPlayer { get; set; }
        public PlayerRating TheirPlayer { get; set; }
        public string PartnerTeamAbbr { get; set; }
        public float Score { get; set; }
    }

    private void ProposeFairTradeBetweenTeams(Team teamA, Team teamB, Dictionary<string, TeamFinance> finances, System.Random rand)
    {
        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv).OrderByDescending(p => p.currentValue).ToList();
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv).OrderByDescending(p => p.currentValue).ToList();

        if (rosterA.Count < 2 || rosterB.Count < 2) return;
        
        // 시도해볼만한 랜덤한 선수 1:1 트레이드
        var playerA = rosterA[rand.Next(0, rosterA.Count)];
        var playerB = rosterB[rand.Next(0, rosterB.Count)];

        var resultA = _tradeManager.EvaluateTrade(
            teamA.team_abbv, new List<PlayerRating> { playerA },
            teamB.team_abbv, new List<PlayerRating> { playerB },
            rand
        );
        
        // teamB 입장에서의 평가
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

        // [수정] 동일 전략 팀 간 트레이드 중단 확률을 70% -> 30%로 완화
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
            return null; // AI-AI 트레이드는 여기서 처리하고 반환하지 않음
        }
    }
    
    private PlayerRating FindTradableAsset(List<PlayerRating> roster, TeamStrategy myStrategy, TeamStrategy theirStrategy)
    {
        IEnumerable<PlayerRating> candidates;

        if (myStrategy == TeamStrategy.Contending)
        {
            // 컨텐딩팀: 어리고 포텐 높은 선수(미래 가치)를 팔아 즉시 전력감(현재 가치)을 원함
            candidates = roster.Where(p => (p.potential - p.overallAttribute) >= 8 && p.age < 25)
                                 .OrderByDescending(p => p.currentValue);
            
            // [플랜 B] 팔만한 유망주가 없으면, 팀 내 최저 OVR 선수라도 팔아서 로스터를 정리하려 함
            if (!candidates.Any())
            {
                candidates = roster.OrderBy(p => p.overallAttribute);
            }
        }
        else if (myStrategy == TeamStrategy.Rebuilding)
        {
            // 리빌딩팀: 나이 많고 비싼 선수(현재 가치)를 팔아 유망주나 샐러리캡을 원함
            candidates = roster.Where(p => p.age > 28 && p.currentValue > 40)
                                 .OrderByDescending(p => p.currentValue);

            // [플랜 B] 팔만한 베테랑이 없으면, 가치가 가장 높은 선수(에이스)를 팔아서 리빌딩 자원을 얻으려 함
            if (!candidates.Any())
            {
                candidates = roster.OrderByDescending(p => p.currentValue);
            }
        }
        else // Standard
        {
            // 스탠다드팀: 팀의 밸런스를 맞추기 위해, 주전급이 아닌 선수 중 가장 가치가 높은 선수를 트레이드 블록에 올림
            candidates = roster.OrderByDescending(p => p.currentValue).Skip(5);
        }

        return candidates.FirstOrDefault();
    }
}