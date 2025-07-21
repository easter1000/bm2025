using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SeasonManager : MonoBehaviour
{
    public static SeasonManager Instance { get; private set; }

    private LocalDbManager _dbManager;
    private TradeManager _tradeManager;
    
    // 하루가 지나는 시간(초). 실제 게임 시간 기준
    public float realSecondsPerDay = 1.0f; 

    private int _currentSeason = 2025;
    private System.DateTime _currentDate;
    private float _dayTimer = 0f;
    private string _userTeamAbbr; // 유저 팀 약어 저장

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {
        _dbManager = LocalDbManager.Instance;
        _tradeManager = FindAnyObjectByType<TradeManager>();
        if (_tradeManager == null)
        {
            Debug.LogWarning("TradeManager를 찾을 수 없습니다. 트레이드 관련 기능이 비활성화됩니다.");
        }

        _userTeamAbbr = "BOS";
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
        _dayTimer += Time.deltaTime;

        if (_dayTimer >= realSecondsPerDay)
        {
            _dayTimer -= realSecondsPerDay;
            AdvanceDay();
        }
    }

    public int GetCurrentSeason()
    {
        return _currentSeason;
    }

    private void AdvanceDay()
    {
        _currentDate = _currentDate.AddDays(1);
        // Debug.Log($"날짜가 하루 지났습니다. 현재 날짜: {_currentDate.ToShortDateString()}");

        // TODO: 경기 시뮬레이션 로직 호출

        // [수정] 매주 월요일이 아닌, 매일 AI 트레이드 시도
        AttemptAiToAiTrades();
    }

    /// <summary>
    /// AI 팀들이 서로 트레이드를 시도하도록 하는 함수
    /// </summary>
    private void AttemptAiToAiTrades()
    {
        if (_tradeManager == null) return; // TradeManager가 없으면 트레이드 시도 안 함

        // 매일 호출되므로, 이 로그는 너무 자주 나타나지 않도록 주석 처리합니다.
        // Debug.Log("AI 팀들의 트레이드 시장을 확인합니다...");

        List<Team> allTeams = _dbManager.GetAllTeams();
        var teamFinances = _dbManager.GetTeamFinancesForSeason(_currentSeason)
                                     .ToDictionary(f => f.TeamAbbr);
        
        // 유저 팀을 제외한 AI 팀 목록
        List<Team> aiTeams = allTeams.Where(t => t.team_abbv != _userTeamAbbr).ToList();
        Team userTeam = allTeams.FirstOrDefault(t => t.team_abbv == _userTeamAbbr);

        // AI <-> AI 트레이드
        for (int i = 0; i < aiTeams.Count; i++)
        {
            for (int j = i + 1; j < aiTeams.Count; j++)
            {
                // [수정] 트레이드 빈도 상향 (1% -> 4%)
                if (Random.Range(0, 100) < 25)
                {
                    ProposeTradeBetweenTeams(aiTeams[i], aiTeams[j], teamFinances);
                }
            }
        }

        // AI -> User 트레이드 제안
        if (userTeam != null)
        {
            foreach (var aiTeam in aiTeams)
            {
                // [수정] 트레이드 빈도 상향 (0.3% -> 1.5%)
                if (Random.Range(0, 1000) < 15)
                {
                    ProposeTradeBetweenTeams(aiTeam, userTeam, teamFinances, true);
                }
            }
        }
    }

    private void ProposeTradeBetweenTeams(Team teamA, Team teamB, Dictionary<string, TeamFinance> finances, bool isUserTeamInvolved = false)
    {
        var financeA = finances.ContainsKey(teamA.team_abbv) ? finances[teamA.team_abbv] : null;
        var financeB = finances.ContainsKey(teamB.team_abbv) ? finances[teamB.team_abbv] : null;

        if (financeA != null && financeB != null)
        {
            GenerateAndProposeSmartTrade(teamA, financeA, teamB, financeB, isUserTeamInvolved);
        }
    }
    
    private enum TeamStrategy { Rebuilding, Contending, Standard }

    private TeamStrategy GetTeamStrategy(TeamFinance finance)
    {
        float winPercentage = (finance.Wins + finance.Losses) == 0 ? 0.5f : (float)finance.Wins / (finance.Wins + finance.Losses);
        if (winPercentage > 0.65) return TeamStrategy.Contending;
        if (winPercentage < 0.35) return TeamStrategy.Rebuilding;
        return TeamStrategy.Standard;
    }
    
    private void GenerateAndProposeSmartTrade(Team teamA, TeamFinance financeA, Team teamB, TeamFinance financeB, bool isUserTeamInvolved = false)
    {
        var strategyA = GetTeamStrategy(financeA);
        var strategyB = GetTeamStrategy(financeB);

        // [수정] 동일 전략 팀 간 트레이드 중단 확률을 70% -> 30%로 완화
        if (strategyA == strategyB && Random.Range(0, 100) < 30) return;

        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv);
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv);

        if (rosterA.Count < 8 || rosterB.Count < 8) return;

        PlayerRating playerToTradeFromA = FindTradableAsset(rosterA, strategyA, strategyB);
        PlayerRating playerToTradeFromB = FindTradableAsset(rosterB, strategyB, strategyA);

        if (playerToTradeFromA == null || playerToTradeFromB == null) return;
        
        if (isUserTeamInvolved)
        {
            // TODO: 실제로는 UI 팝업을 띄우고 유저의 결정을 기다려야 함
            // public static event Action<TradeOffer> OnTradeOfferedToUser;
            // OnTradeOfferedToUser?.Invoke(new TradeOffer(teamA, playerToTradeFromA, teamB, playerToTradeFromB));
            Debug.LogWarning($"[USER TRADE PROPOSAL] {teamA.team_abbv}에서 트레이드를 제안했습니다!");
            Debug.LogWarning($"  - 주는 선수: {playerToTradeFromA.name} ({playerToTradeFromA.currentValue:F1})");
            Debug.LogWarning($"  - 받는 선수: {playerToTradeFromB.name} ({playerToTradeFromB.currentValue:F1})");
            // 지금은 자동으로 거절하는 것으로 처리
            Debug.Log("  (자동으로 거절되었습니다.)");
        }
        else
        {
            Debug.Log($"[AI-AI Trade Proposal] {teamA.team_abbv} ({strategyA}) -> {teamB.team_abbv} ({strategyB})");
            _tradeManager.EvaluateAndExecuteTrade(
                teamA.team_abbv, new List<PlayerRating> { playerToTradeFromA },
                teamB.team_abbv, new List<PlayerRating> { playerToTradeFromB }
            );
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