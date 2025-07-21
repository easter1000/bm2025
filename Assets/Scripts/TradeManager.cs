using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Added for .Select() and .ToList()

public class TradeManager : MonoBehaviour
{
    // TODO: LocalDbManager 인스턴스 연결
    private LocalDbManager _dbManager;
    private SeasonManager _seasonManager;

    void Start()
    {
        _dbManager = LocalDbManager.Instance;
        _seasonManager = FindAnyObjectByType<SeasonManager>();
    }

    /// <summary>
    /// 두 팀 간의 트레이드 제안을 평가하고 실행 여부를 결정하는 핵심 함수
    /// </summary>
    /// <param name="proposingTeamAbbr">제안하는 팀의 약어</param>
    /// <param name="offeredPlayers">제안하는 팀이 내놓는 선수 목록</param>
    /// <param name="targetTeamAbbr">제안받는 팀의 약어</param>
    /// <param name="requestedPlayers">제안받는 팀에게 요구하는 선수 목록</param>
    /// <returns>트레이드 성공 시 true, 실패 시 false</returns>
    public bool EvaluateAndExecuteTrade(
        string proposingTeamAbbr, List<PlayerRating> offeredPlayers,
        string targetTeamAbbr, List<PlayerRating> requestedPlayers)
    {
        int currentSeason = _seasonManager.GetCurrentSeason();
        var targetTeamFinance = _dbManager.GetTeamFinance(targetTeamAbbr, currentSeason);
        
        long offeredSalary = CalculateTotalSalary(offeredPlayers);
        long requestedSalary = CalculateTotalSalary(requestedPlayers);
        
        // 1. 재정적 타당성 검사
        long newTeamSalary = targetTeamFinance.CurrentTeamSalary - requestedSalary + offeredSalary;
        if (newTeamSalary > targetTeamFinance.SalaryCap)
        {
            Debug.Log($"[Trade Rejected] Financials: {targetTeamAbbr} would exceed salary cap.");
            return false;
        }

        // 2. 팀 필요성 분석
        var targetTeamRoster = _dbManager.GetPlayersByTeam(targetTeamAbbr);
        float positionBonus = AnalyzeTeamNeeds(targetTeamRoster, offeredPlayers, requestedPlayers);

        // 3. 최종 가치 평가
        float offeredValue = offeredPlayers.Sum(p => p.currentValue);
        float requestedValue = requestedPlayers.Sum(p => p.currentValue);
        
        float finalOfferedValue = offeredValue + positionBonus;

        // 리빌딩 팀은 더 많은 가치를 요구하고, 컨텐딩 팀은 약간의 손해를 감수할 수 있음
        // TODO: SeasonManager로부터 팀 전략(Rebuilding/Contending)을 받아와서 적용
        float requiredRatio = Random.Range(0.95f, 1.05f); 

        if (finalOfferedValue > requestedValue * requiredRatio)
        {
            ExecuteTrade(proposingTeamAbbr, offeredPlayers, targetTeamAbbr, requestedPlayers);
            // TODO: 트레이드 후 팀 재정 정보 업데이트
            return true;
        }
        else
        {
            Debug.Log($"[Trade Rejected] Value: {targetTeamAbbr} declined. " +
                      $"(Offered Value: {finalOfferedValue:N0}, Requested Value: {requestedValue * requiredRatio:N0})");
            return false;
        }
    }

    private float AnalyzeTeamNeeds(List<PlayerRating> teamRoster, List<PlayerRating> incomingPlayers, List<PlayerRating> outgoingPlayers)
    {
        var positionStrengths = new Dictionary<int, float>();
        for (int i = 1; i <= 5; i++)
        {
            var playersInPos = teamRoster.Where(p => p.position == i);
            positionStrengths[i] = playersInPos.Any() ? (float)playersInPos.Average(p => p.overallAttribute) : 60f;
        }
        
        float bonus = 0;
        
        // 받는 선수들이 약점 포지션을 보강해주는가?
        foreach (var player in incomingPlayers)
        {
            // 우리팀의 해당 포지션 평균 OVR보다 높은 선수가 오면 보너스
            bonus += Mathf.Max(0, player.overallAttribute - positionStrengths[player.position]) * 0.1f; // OVR 1점당 0.1의 가치
        }
        
        // 주는 선수들이 강점 포지션의 선수인가?
        foreach (var player in outgoingPlayers)
        {
            // 우리팀의 해당 포지션 평균 OVR보다 낮은 선수를 내보내면 보너스 (정리 개념)
            bonus += Mathf.Max(0, positionStrengths[player.position] - player.overallAttribute) * 0.05f;
        }
        
        return bonus;
    }

    private long CalculateTotalSalary(List<PlayerRating> players)
    {
        long totalSalary = 0;
        foreach (var player in players)
        {
            var status = _dbManager.GetPlayerStatus(player.player_id);
            if (status != null) totalSalary += status.Salary;
        }
        return totalSalary;
    }

    private void ExecuteTrade(string teamA_Abbr, List<PlayerRating> playersFromA, string teamB_Abbr, List<PlayerRating> playersFromB)
    {
        _dbManager.UpdatePlayerTeam(playersFromA.Select(p => p.player_id).ToList(), teamB_Abbr);
        _dbManager.UpdatePlayerTeam(playersFromB.Select(p => p.player_id).ToList(), teamA_Abbr);

        Debug.Log($"[Trade Executed] {teamA_Abbr} <-> {teamB_Abbr} 트레이드가 성사되었습니다.");
        foreach(var p in playersFromA) Debug.Log($"  - {p.name} -> {teamB_Abbr}");
        foreach(var p in playersFromB) Debug.Log($"  - {p.name} -> {teamA_Abbr}");
    }
} 