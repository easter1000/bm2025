using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RosterManager
{
    private const int MaxRosterSize = 15;

    /// <summary>
    /// 모든 팀의 로스터를 시즌 시작에 맞춰 15명으로 조정합니다.
    /// 15명을 초과하는 팀은 OVR이 낮은 선수부터 방출됩니다.
    /// </summary>
    public static void AdjustAllRostersToSeasonStart()
    {
        Debug.Log("[RosterManager] Starting roster adjustments for all teams...");
        var allTeams = LocalDbManager.Instance.GetAllTeams();
        var allRatings = LocalDbManager.Instance.GetAllPlayerRatings();
        
        foreach (var team in allTeams)
        {
            // [핵심 버그 수정] 팀 약어(team_abbv)가 아닌 전체 팀 이름(team_name)으로 선수를 찾아야 함
            var teamPlayers = allRatings.Where(p => p.team == team.team_name).ToList();

            if (teamPlayers.Count > MaxRosterSize)
            {
                Debug.Log($"Team {team.team_name} has {teamPlayers.Count} players. Adjusting to {MaxRosterSize}...");
                
                // OVR이 낮은 순서대로 정렬
                var playersToRelease = teamPlayers
                    .OrderBy(p => p.overallAttribute)
                    .Take(teamPlayers.Count - MaxRosterSize)
                    .ToList();

                foreach (var player in playersToRelease)
                {
                    Debug.Log($"Releasing player: {player.name} (OVR: {player.overallAttribute}) from {team.team_abbv}");
                    LocalDbManager.Instance.ReleasePlayer(player.player_id);
                }
            }
        }

        // [핵심 추가] 모든 로스터 조정이 끝난 후, 전체 팀의 연봉을 다시 계산
        LocalDbManager.Instance.RecalculateAndSaveAllTeamSalaries();
        
        Debug.Log("[RosterManager] All rosters have been adjusted and salaries recalculated.");
    }
} 