using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuickGameSimulator
{
    public GameResult SimulateGame(Schedule gameInfo)
    {
        // DB에서 팀 정보와 선수 능력치를 가져옵니다.
        var homePlayers = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.HomeTeamAbbr);
        var awayPlayers = LocalDbManager.Instance.GetPlayersByTeam(gameInfo.AwayTeamAbbr);

        // 1. 팀의 종합 OVR 계산
        float homePower = (float)homePlayers.OrderByDescending(p => p.overallAttribute).Take(8).Average(p => p.overallAttribute);
        float awayPower = (float)awayPlayers.OrderByDescending(p => p.overallAttribute).Take(8).Average(p => p.overallAttribute);

        // 2. 점수 계산 (팀 파워 + 무작위성)
        int homeScore = Mathf.FloorToInt((homePower - awayPower) * 1.5f + Random.Range(100, 115));
        int awayScore = Mathf.FloorToInt((awayPower - homePower) * 1.5f + Random.Range(100, 115));
        
        // 동점 방지
        if (homeScore == awayScore) homeScore++;

        // 3. 선수 스탯 분배 (간단한 버전)
        // 팀 점수를 선수들의 OVR에 비례하여 분배
        List<PlayerStat> stats = new List<PlayerStat>();
        DistributeStats(stats, homePlayers, homeScore, gameInfo.GameId, gameInfo.Season);
        DistributeStats(stats, awayPlayers, awayScore, gameInfo.GameId, gameInfo.Season);

        return new GameResult
        {
            HomeScore = homeScore,
            AwayScore = awayScore,
            PlayerStats = stats
        };
    }

    private void DistributeStats(List<PlayerStat> statsList, List<PlayerRating> players, int teamScore, string gameId, int season)
    {
        if (players == null || players.Count == 0) return;
        float totalOvr = players.Sum(p => p.overallAttribute);
        if (totalOvr == 0) return;

        foreach (var player in players)
        {
            float contribution = player.overallAttribute / totalOvr;
            statsList.Add(new PlayerStat {
                PlayerId = player.player_id,
                Season = season,
                GameId = gameId,
                Points = Mathf.RoundToInt(teamScore * contribution),
                // 리바운드, 어시스트 등도 유사하게 배분 가능
                Rebounds = Random.Range(0, 12),
                Assists = Random.Range(0, 10),
                RecordedAt = System.DateTime.UtcNow.ToString("s")
            });
        }
    }
}

// 이 구조체는 두 시뮬레이터 모두가 공통으로 사용합니다.
public struct GameResult
{
    public int HomeScore;
    public int AwayScore;
    public List<PlayerStat> PlayerStats;
}