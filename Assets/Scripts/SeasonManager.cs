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
        _dbManager = LocalDbManager.Instance;
        _tradeManager = FindAnyObjectByType<TradeManager>();
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

    private void AdvanceDay()
    {
        // 자동 시간 흐름 로직 제거
    }

    /// <summary>
    /// AI 팀들이 서로 트레이드를 시도하고, 유저에게 제안할 트레이드 목록을 반환.
    /// </summary>
    /// <returns>유저에게 제안된 TradeOffer 리스트</returns>
    public List<TradeOffer> AttemptAiToAiTrades()
    {
        if (_tradeManager == null) 
        {
            Debug.LogError("TradeManager is not initialized!");
            _tradeManager = FindAnyObjectByType<TradeManager>();
            if (_tradeManager == null) return new List<TradeOffer>();
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

        // 1. 예산 초과 팀 강제 구조조정
        foreach (var team in aiTeams)
        {
            var finance = teamFinances.GetValueOrDefault(team.team_abbv);
            if (finance == null || finance.CurrentTeamSalary <= finance.TeamBudget) continue;

            Debug.LogWarning($"[Salary Cap] {team.team_abbv} is over budget! Salary: ${finance.CurrentTeamSalary:N0}, Budget: ${finance.TeamBudget:N0}. Attempting to resolve...");

            bool tradeResolved = AttemptSalaryCapTrade(team, teamFinances);
            
            if (!tradeResolved)
            {
                ReleaseLowValuePlayersUntilBudgetMet(team, teamFinances);
            }
        }

        // 2. 일반 AI 트레이드 시도
        for (int i = 0; i < aiTeams.Count; i++)
        {
            for (int j = i + 1; j < aiTeams.Count; j++)
            {
                if (rand.Next(0, 100) < 5) // 5% 확률로 트레이드 시도
                {
                    ProposeFairTradeBetweenTeams(aiTeams[i], aiTeams[j], teamFinances, rand);
                }
            }
        }
        
        // 3. AI -> User 트레이드 제안
        Team userTeam = allTeams.FirstOrDefault(t => t.team_abbv == _userTeamAbbr);
        if (userTeam != null)
        {
            foreach (var aiTeam in aiTeams)
            {
                if (rand.Next(0, 1000) < 15) // 1.5% 확률
                {
                    var offer = GenerateAndProposeSmartTrade(aiTeam, teamFinances[aiTeam.team_abbv], userTeam, teamFinances[userTeam.team_abbv], rand, true);
                    if (offer != null)
                    {
                        userTradeOffers.Add(offer);
                    }
                }
            }
        }
        return userTradeOffers;
    }

    private bool AttemptSalaryCapTrade(Team overBudgetTeam, Dictionary<string, TeamFinance> finances)
    {
        var overBudgetRoster = _dbManager.GetPlayersByTeamWithStatus(overBudgetTeam.team_abbv);
        
        // 연봉 높은 순으로 선수 정렬
        var sortedRoster = overBudgetRoster.OrderByDescending(p => p.Status.Salary).ToList();

        // 예산에 여유가 있는 팀 찾기
        var potentialPartners = finances.Where(f => f.Value.CurrentTeamSalary < f.Value.TeamBudget && f.Key != overBudgetTeam.team_abbv && f.Key != _userTeamAbbr)
                                        .ToList();
        
        BestTradeOption bestTrade = null;
        var rand = new System.Random(); // 트레이드 평가를 위한 랜덤 인스턴스

        foreach (var myPlayerInfo in sortedRoster)
        {
            if (myPlayerInfo.Status.Salary == 0) continue;
            long myAnnualSalary = myPlayerInfo.Status.Salary / myPlayerInfo.Status.YearsLeft;

            foreach (var partnerEntry in potentialPartners)
            {
                var partnerTeam = _dbManager.GetTeam(partnerEntry.Key);
                var partnerRoster = _dbManager.GetPlayersByTeamWithStatus(partnerTeam.team_abbv);

                foreach (var theirPlayerInfo in partnerRoster)
                {
                    long theirAnnualSalary = (theirPlayerInfo.Status.YearsLeft > 0) ? (theirPlayerInfo.Status.Salary / theirPlayerInfo.Status.YearsLeft) : 0;
                    long salaryChange = theirAnnualSalary - myAnnualSalary;

                    // 연봉 절감 효과가 없으면 이 트레이드는 고려하지 않음
                    if (salaryChange >= 0) continue;
                    
                    var result = _tradeManager.EvaluateTrade(
                        overBudgetTeam.team_abbv, new List<PlayerRating> { myPlayerInfo.Rating },
                        partnerTeam.team_abbv, new List<PlayerRating> { theirPlayerInfo.Rating },
                        rand
                    );

                    if (result.IsAccepted && (bestTrade == null || salaryChange < bestTrade.SalaryChange))
                    {
                        bestTrade = new BestTradeOption
                        {
                            MyPlayer = myPlayerInfo.Rating,
                            TheirPlayer = theirPlayerInfo.Rating,
                            PartnerTeamAbbr = partnerTeam.team_abbv,
                            SalaryChange = salaryChange
                        };
                    }
                }
            }
        }
        
        if (bestTrade != null)
        {
            Debug.Log($"[Salary Cap Trade] Executing best option: {bestTrade.MyPlayer.name} for {bestTrade.TheirPlayer.name}. Salary savings: ${-bestTrade.SalaryChange:N0}");
            _tradeManager.ExecuteTrade(
                overBudgetTeam.team_abbv, new List<PlayerRating> { bestTrade.MyPlayer },
                bestTrade.PartnerTeamAbbr, new List<PlayerRating> { bestTrade.TheirPlayer }
            );
            return true;
        }

        return false; // 적절한 트레이드 대상을 찾지 못함
    }

    private class BestTradeOption
    {
        public PlayerRating MyPlayer { get; set; }
        public PlayerRating TheirPlayer { get; set; }
        public string PartnerTeamAbbr { get; set; }
        public long SalaryChange { get; set; }
    }

    private void ReleaseLowValuePlayersUntilBudgetMet(Team team, Dictionary<string, TeamFinance> finances)
    {
        var finance = finances[team.team_abbv];
        var roster = _dbManager.GetPlayersByTeamWithStatus(team.team_abbv)
                                .OrderBy(p => p.Rating.currentValue)
                                .ToList();
        
        Debug.LogWarning($"[Salary Cap] {team.team_abbv} failed to find a trade. Releasing players...");

        foreach (var playerInfo in roster)
        {
            if (finance.CurrentTeamSalary <= finance.TeamBudget) break;

            Debug.Log($"[Salary Cap] Releasing {playerInfo.Rating.name} (Value: {playerInfo.Rating.currentValue}, Salary: ${playerInfo.Status.Salary:N0})");
            
            // 선수를 FA로 보냄
            _dbManager.UpdatePlayerTeam(new List<int> { playerInfo.Rating.player_id }, "FA");
            
            // 재정 정보 업데이트
            long annualSalary = (playerInfo.Status.YearsLeft > 0) ? (playerInfo.Status.Salary / playerInfo.Status.YearsLeft) : 0;
            finance.CurrentTeamSalary -= annualSalary;
            _dbManager.UpdateTeamFinance(finance);
        }

        if (finance.CurrentTeamSalary > finance.TeamBudget)
        {
            Debug.LogError($"[Salary Cap] CRITICAL: {team.team_abbv} could not get under budget even after releasing players!");
        }
        else
        {
            Debug.Log($"[Salary Cap] {team.team_abbv} is now under budget. New Salary: ${finance.CurrentTeamSalary:N0}");
        }
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