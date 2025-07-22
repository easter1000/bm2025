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

    // AI가 유저에게 트레이드를 제안할 때 발생하는 이벤트
    public static event Action<TradeOffer> OnTradeOfferedToUser;

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
    /// AI 팀들이 서로 트레이드를 시도하도록 하는 함수
    /// </summary>
    public void AttemptAiToAiTrades()
    {
        if (_tradeManager == null) 
        {
            Debug.LogError("TradeManager is not initialized!");
            _tradeManager = FindAnyObjectByType<TradeManager>();
            if (_tradeManager == null) return;
        }
        if (_dbManager == null) _dbManager = LocalDbManager.Instance;

        _userTeamAbbr = _dbManager.GetUser()?.SelectedTeamAbbr;
        _currentSeason = _dbManager.GetUser()?.CurrentSeason ?? DateTime.Now.Year;

        List<Team> allTeams = _dbManager.GetAllTeams();
        var teamFinances = _dbManager.GetTeamFinancesForSeason(_currentSeason)
                                     .ToDictionary(f => f.TeamAbbr);
        
        List<Team> aiTeams = allTeams.Where(t => t.team_abbv != _userTeamAbbr && t.team_abbv != "FA").ToList();

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
                if (UnityEngine.Random.Range(0, 100) < 5) // 5% 확률로 트레이드 시도
                {
                    ProposeFairTradeBetweenTeams(aiTeams[i], aiTeams[j], teamFinances);
                }
            }
        }
        
        // 3. AI -> User 트레이드 제안
        Team userTeam = allTeams.FirstOrDefault(t => t.team_abbv == _userTeamAbbr);
        if (userTeam != null)
        {
            foreach (var aiTeam in aiTeams)
            {
                if (UnityEngine.Random.Range(0, 1000) < 15) // 1.5% 확률
                {
                    // 이 부분은 GenerateAndProposeSmartTrade를 재활용하거나 새 로직을 만들어야 함
                    ProposeTradeToUser(aiTeam, userTeam, teamFinances);
                }
            }
        }
    }

    private void ProposeTradeToUser(Team aiTeam, Team userTeam, Dictionary<string, TeamFinance> finances)
    {
        // 기존의 스마트 트레이드 제안 로직을 그대로 활용
        GenerateAndProposeSmartTrade(aiTeam, finances[aiTeam.team_abbv], userTeam, finances[userTeam.team_abbv], true);
    }

    private bool AttemptSalaryCapTrade(Team overBudgetTeam, Dictionary<string, TeamFinance> finances)
    {
        var overBudgetRoster = _dbManager.GetPlayersByTeamWithStatus(overBudgetTeam.team_abbv);
        
        // 연봉 높은 순으로 선수 정렬
        var sortedRoster = overBudgetRoster.OrderByDescending(p => p.Status.Salary).ToList();

        // 예산에 여유가 있는 팀 찾기
        var potentialPartners = finances.Where(f => f.Value.CurrentTeamSalary < f.Value.TeamBudget && f.Key != overBudgetTeam.team_abbv && f.Key != _userTeamAbbr)
                                        .ToList();

        foreach (var myPlayer in sortedRoster)
        {
            if (myPlayer.Status.Salary == 0) continue;

            foreach (var partnerEntry in potentialPartners)
            {
                var partnerTeam = _dbManager.GetTeam(partnerEntry.Key);
                var partnerRoster = _dbManager.GetPlayersByTeamWithStatus(partnerTeam.team_abbv);

                // 상대팀에서는 저연봉 선수 찾기
                var theirPlayer = partnerRoster.OrderBy(p => p.Status.Salary).FirstOrDefault();
                
                if (theirPlayer == null) continue;

                // 연봉 차이가 커야 트레이드 의미가 있음
                if (myPlayer.Status.Salary > theirPlayer.Status.Salary)
                {
                    Debug.Log($"[Salary Cap Trade] Attempting: {overBudgetTeam.team_abbv} gives {myPlayer.Rating.name}(${myPlayer.Status.Salary:N0}) for {partnerTeam.team_abbv}'s {theirPlayer.Rating.name}(${theirPlayer.Status.Salary:N0})");
                    
                    bool success = _tradeManager.EvaluateAndExecuteTrade(
                        overBudgetTeam.team_abbv, new List<PlayerRating>{ myPlayer.Rating },
                        partnerTeam.team_abbv, new List<PlayerRating>{ theirPlayer.Rating }
                    );

                    if (success)
                    {
                        Debug.Log("[Salary Cap Trade] Success!");
                        // 재정 정보 즉시 업데이트
                        finances[overBudgetTeam.team_abbv].CurrentTeamSalary -= (myPlayer.Status.Salary - theirPlayer.Status.Salary);
                        finances[partnerTeam.team_abbv].CurrentTeamSalary += (myPlayer.Status.Salary - theirPlayer.Status.Salary);
                        return true; // 트레이드 성공
                    }
                }
            }
        }

        return false; // 적절한 트레이드 대상을 찾지 못함
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
            _dbManager.UpdatePlayerTeam(playerInfo.Rating.player_id, "FA");
            
            // 재정 정보 업데이트
            finance.CurrentTeamSalary -= playerInfo.Status.Salary;
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

    private void ProposeFairTradeBetweenTeams(Team teamA, Team teamB, Dictionary<string, TeamFinance> finances)
    {
        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv).OrderByDescending(p => p.currentValue).ToList();
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv).OrderByDescending(p => p.currentValue).ToList();

        if (rosterA.Count < 2 || rosterB.Count < 2) return;

        // 양 팀에서 비슷한 가치의 선수 찾기 (예: 2~5번째 선수 중 랜덤)
        int rank = UnityEngine.Random.Range(1, Math.Min(5, Math.Min(rosterA.Count, rosterB.Count)));
        PlayerRating playerA = rosterA[rank];
        PlayerRating playerB = rosterB[rank];
        
        // 가치 차이가 너무 크면 트레이드 중단 (예: 20% 이상 차이)
        if (Mathf.Abs(playerA.currentValue - playerB.currentValue) / playerA.currentValue > 0.2f)
        {
            return;
        }
        
        Debug.Log($"[Fair Trade] Proposing trade between {teamA.team_abbv} and {teamB.team_abbv}: {playerA.name} <-> {playerB.name}");

        _tradeManager.EvaluateAndExecuteTrade(
            teamA.team_abbv, new List<PlayerRating> { playerA },
            teamB.team_abbv, new List<PlayerRating> { playerB }
        );
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
        if (strategyA == strategyB && UnityEngine.Random.Range(0, 100) < 30) return;

        var rosterA = _dbManager.GetPlayersByTeam(teamA.team_abbv);
        var rosterB = _dbManager.GetPlayersByTeam(teamB.team_abbv);

        if (rosterA.Count < 8 || rosterB.Count < 8) return;

        PlayerRating playerToTradeFromA = FindTradableAsset(rosterA, strategyA, strategyB);
        PlayerRating playerToTradeFromB = FindTradableAsset(rosterB, strategyB, strategyA);

        if (playerToTradeFromA == null || playerToTradeFromB == null) return;
        
        if (isUserTeamInvolved)
        {
            // [수정] UI 팝업을 띄우기 위해 이벤트를 발생시킴
            var offer = new TradeOffer(
                teamA, new List<PlayerRating> { playerToTradeFromA },
                teamB, new List<PlayerRating> { playerToTradeFromB }
            );
            OnTradeOfferedToUser?.Invoke(offer);

            Debug.Log($"[USER TRADE PROPOSAL] {teamA.team_abbv} offers {playerToTradeFromA.name} for {teamB.team_abbv}'s {playerToTradeFromB.name}");
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