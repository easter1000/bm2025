using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Added for .Select() and .ToList()

public class TradeManager : MonoBehaviour
{

    private LocalDbManager _dbManager;
    private SeasonManager _seasonManager;

    void Awake()
    {
        _dbManager = LocalDbManager.Instance;
        _seasonManager = SeasonManager.Instance;
    }
    void Start()
    {
        if (_dbManager == null) _dbManager = LocalDbManager.Instance;
        if (_seasonManager == null) _seasonManager = SeasonManager.Instance;
    }

    /// <summary>
    /// 두 팀 간의 트레이드 제안을 평가하고 실행 여부를 결정하는 핵심 함수
    /// </summary>
    /// <param name="proposingTeamAbbr">제안하는 팀의 약어</param>
    /// <param name="offeredPlayers">제안하는 팀이 내놓는 선수 목록</param>
    /// <param name="targetTeamAbbr">제안받는 팀의 약어</param>
    /// <param name="requestedPlayers">제안받는 팀에게 요구하는 선수 목록</param>
    /// <returns>
    /// 0: 즉시 수락 (이득인 거래)
    /// > 0: 요구하는 추가 금액 (달러)
    /// -1: 거절 (부당한 거래)
    /// </returns>
    public TradeEvaluationResult EvaluateTrade(
        string proposingTeamAbbr, List<PlayerRating> offeredPlayers,
        string targetTeamAbbr, List<PlayerRating> requestedPlayers, System.Random rand)
    {
        int currentSeason = _seasonManager.GetCurrentSeason();
        var targetTeamFinance = _dbManager.GetTeamFinance(targetTeamAbbr, currentSeason);
        
        long offeredSalary = CalculateTotalSalary(offeredPlayers);
        long requestedSalary = CalculateTotalSalary(requestedPlayers);

        var targetTeamRoster = _dbManager.GetPlayersByTeam(targetTeamAbbr);

        // 1. 팀 필요성 분석
        float positionValueBonus = AnalyzeTeamNeeds(targetTeamRoster, offeredPlayers, requestedPlayers);

        // 2. 최종 가치 평가
        float offeredValue = offeredPlayers.Sum(p => p.currentValue);
        float requestedValue = requestedPlayers.Sum(p => p.currentValue);
        float finalOfferedValue = offeredValue + positionValueBonus;

        // 리빌딩 팀은 더 많은 가치를 요구하고, 컨텐딩 팀은 약간의 손해를 감수할 수 있음
        // TODO: SeasonManager로부터 팀 전략(Rebuilding/Contending)을 받아와서 적용
        float requiredRatio = (float)(rand.NextDouble() * (1.2 - 0.95) + 0.95); // Random.Range(0.95f, 1.2f)
        float adjustedRequestedValue = requestedValue * requiredRatio;
        float valueDifference = finalOfferedValue - adjustedRequestedValue;

        // 손해보는 거래: 차액을 현금으로 요구
        long requiredCash = (long)Mathf.Abs(valueDifference);
        
        var result = new TradeEvaluationResult
        {
            IsAccepted = false,
            RequiredCash = requiredCash,
            ValueDifference = valueDifference
        };

        long newTeamSalary = CalculateTotalSalary(targetTeamRoster) - requestedSalary + offeredSalary;
        if (newTeamSalary > targetTeamFinance.TeamBudget + requiredCash)
        {
            return result; // 재정적 타당성 실패
        }

        if (valueDifference >= 0)
        {
            result.IsAccepted = true;
            result.RequiredCash = 0;
            return result;
        }

        // 제안 가치가 요구 가치의 50% 미만이면 협상 불가
        const float rejectionThreshold = 0.5f;
        if (finalOfferedValue < adjustedRequestedValue * rejectionThreshold)
        {
            return result;
        }
        
        result.IsAccepted = true; // 협상 가능
        return result;
    }

    private float AnalyzeTeamNeeds(List<PlayerRating> teamRoster, List<PlayerRating> incomingPlayers, List<PlayerRating> outgoingPlayers)
    {
        float bonus = 0;

        for (int i = 1; i <= 5; i++)
        {
            var currentStrength = 0f;
            var playersInPos = teamRoster.Where(p => p.position == i);
            if(playersInPos.Any())
            {
                currentStrength = (float)(playersInPos.Sum(p => (p.currentValue / 10000) * (p.currentValue / 10000)) / (float)Mathf.Pow(playersInPos.Count() + 3, 1.5f));
            }

            var potentialStrength = 0f;
            var potentialPlayersInPos = teamRoster.Where(p => p.position == i);
            potentialPlayersInPos = potentialPlayersInPos.Concat(incomingPlayers.Where(p => p.position == i));
            potentialPlayersInPos = potentialPlayersInPos.Except(outgoingPlayers.Where(p => p.position == i));
            if(potentialPlayersInPos.Any())
            {
                potentialStrength = (float)(potentialPlayersInPos.Sum(p => (p.currentValue / 10000) * (p.currentValue / 10000)) / (float)Mathf.Pow(potentialPlayersInPos.Count() + 3, 1.5f));
            }

            bonus += (potentialStrength - currentStrength) * 10;
        }

        return bonus;
    }

    private long CalculateTotalSalary(List<PlayerRating> players)
    {
        long totalAnnualSalary = 0;
        foreach (var player in players)
        {
            var status = _dbManager.GetPlayerStatus(player.player_id);
            if (status != null && status.YearsLeft > 0)
            {
                totalAnnualSalary += status.Salary / status.YearsLeft; // 연 단위 연봉으로 합산
            }
        }
        return totalAnnualSalary;
    }

    public void ExecuteTrade(string teamA_Abbr, List<PlayerRating> playersFromA, string teamB_Abbr, List<PlayerRating> playersFromB)
    {
        _dbManager.UpdatePlayerTeam(playersFromA.Select(p => p.player_id).ToList(), teamB_Abbr);
        _dbManager.UpdatePlayerTeam(playersFromB.Select(p => p.player_id).ToList(), teamA_Abbr);

        Debug.Log($"[Trade Executed] {teamA_Abbr} <-> {teamB_Abbr} 트레이드가 성사되었습니다.");
        foreach(var p in playersFromA) Debug.Log($"  - {p.name} -> {teamB_Abbr}");
        foreach(var p in playersFromB) Debug.Log($"  - {p.name} -> {teamA_Abbr}");
    }
}