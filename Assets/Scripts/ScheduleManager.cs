using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class ScheduleManager
{
    private List<Team> allTeams;
    private readonly DateTime seasonStartDate = new DateTime(2025, 10, 21);
    private readonly DateTime seasonEndDate = new DateTime(2025, 4, 15); // 마지막 날 포함

    // SeasonManager가 호출하는 메인 함수
    public void GenerateNewSeasonSchedule(int season)
    {
        // 1. DB에서 모든 팀 정보를 가져옵니다.
        allTeams = LocalDbManager.Instance.GetAllTeams();
        if (allTeams == null || allTeams.Count != 30)
        {
            Debug.LogError("Team data is not 30. Cannot generate a schedule.");
            return;
        }

        // 2. 이전에 생성된 스케줄이 있다면 모두 삭제합니다.
        LocalDbManager.Instance.ClearScheduleTable();

        // 3. 82경기 규칙에 따라 모든 경기 조합을 생성합니다.
        List<Schedule> allMatchups = CreateAllMatchups();

        // 4. 생성된 경기들을 10/21 ~ 4/14 사이의 날짜에 분배합니다.
        List<Schedule> finalSchedule = AssignDatesToMatchups(allMatchups, season);
        
        // 5. 완성된 스케줄을 DB에 한 번에 저장합니다.
        LocalDbManager.Instance.InsertSchedule(finalSchedule);

        Debug.Log($"[ScheduleManager] Successfully generated and saved {finalSchedule.Count} games for the {season} season.");
    }

    // 82경기 규칙에 따라 모든 경기 조합을 생성
    private List<Schedule> CreateAllMatchups()
    {
        var matchups = new List<Schedule>();

        foreach (var teamA in allTeams)
        {
            // 같은 디비전 내 4개 팀과 4번씩 (홈2, 원정2) -> 16경기
            var teamsInSameDivision = allTeams.Where(t => t.team_id != teamA.team_id && t.division == teamA.division).ToList();
            foreach (var teamB in teamsInSameDivision)
            {
                AddMatchups(matchups, teamA, teamB, 2); // 홈 2, 원정 2
                AddMatchups(matchups, teamB, teamA, 2);
            }

            // 다른 컨퍼런스 15개 팀과 2번씩 (홈1, 원정1) -> 30경기
            var teamsInOtherConf = allTeams.Where(t => t.conference != teamA.conference).ToList();
            foreach (var teamB in teamsInOtherConf)
            {
                AddMatchups(matchups, teamA, teamB, 1); // 홈 1, 원정 1
                AddMatchups(matchups, teamB, teamA, 1);
            }
            
            // 같은 컨퍼런스, 다른 디비전 10개 팀과 3~4번씩 (총 36경기)
            var teamsInSameConf = allTeams.Where(t => t.team_id != teamA.team_id && t.conference == teamA.conference && t.division != teamA.division).ToList();
            // (로테이션이 복잡하므로 여기서는 간소화하여 6팀과는 4번, 4팀과는 3번 만나는 것으로 가정)
            // ... 이 부분은 게임의 특성에 맞게 규칙을 정해야 합니다.
            // 여기서는 36경기를 맞추기 위해 모든 팀과 3~4회 랜덤하게 만나는 식으로 간소화
            foreach(var teamB in teamsInSameConf) {
                int games = UnityEngine.Random.Range(3, 5); // 3 or 4 games
                AddMatchups(matchups, teamA, teamB, games / 2);
                AddMatchups(matchups, teamB, teamA, games - (games / 2));
            }
        }
        
        // 중복된 경기 제거 (A vs B, B vs A가 모두 생성되었으므로)
        return matchups.GroupBy(m => new { H = m.HomeTeamAbbr, A = m.AwayTeamAbbr, D = m.GameDate })
                       .Select(g => g.First())
                       .ToList();
    }

    private void AddMatchups(List<Schedule> list, Team home, Team away, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // GameId를 고유한 문자열로 생성
            string gameId = $"{home.team_abbv}_vs_{away.team_abbv}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            list.Add(new Schedule { GameId = gameId, HomeTeamAbbr = home.team_abbv, AwayTeamAbbr = away.team_abbv });
        }
    }

    // 생성된 경기들을 날짜에 분배
    private List<Schedule> AssignDatesToMatchups(List<Schedule> matchups, int season)
    {
        var finalSchedule = new List<Schedule>();
        var shuffledMatchups = matchups.OrderBy(a => Guid.NewGuid()).ToList();

        var gamesOnDate = new Dictionary<string, int>(); // "YYYY-MM-DD" -> game count
        var teamLastPlayedDate = new Dictionary<string, DateTime>();

        foreach (var match in shuffledMatchups)
        {
            DateTime bestDate = FindBestDateForMatch(gamesOnDate, teamLastPlayedDate, match);
            
            string dateString = bestDate.ToString("yyyy-MM-dd");
            match.GameDate = dateString;
            match.Season = season;
            match.GameStatus = "Scheduled";
            
            finalSchedule.Add(match);
            
            // 정보 업데이트
            if (!gamesOnDate.ContainsKey(dateString)) gamesOnDate[dateString] = 0;
            gamesOnDate[dateString]++;
            
            teamLastPlayedDate[match.HomeTeamAbbr] = bestDate;
            teamLastPlayedDate[match.AwayTeamAbbr] = bestDate;
        }

        return finalSchedule.OrderBy(g => g.GameDate).ToList();
    }

    private DateTime FindBestDateForMatch(Dictionary<string, int> gamesOnDate, Dictionary<string, DateTime> teamLastPlayedDate, Schedule match)
    {
        TimeSpan seasonDuration = seasonEndDate - seasonStartDate;
        int totalDays = seasonDuration.Days;

        int attempts = 0;
        while (attempts < 500) // 무한 루프 방지
        {
            int randomDay = UnityEngine.Random.Range(0, totalDays + 1);
            DateTime proposedDate = seasonStartDate.AddDays(randomDay);
            string dateString = proposedDate.ToString("yyyy-MM-dd");

            // 제약 조건 검사
            bool homeTeamBusy = teamLastPlayedDate.ContainsKey(match.HomeTeamAbbr) && teamLastPlayedDate[match.HomeTeamAbbr].Date == proposedDate.Date;
            bool awayTeamBusy = teamLastPlayedDate.ContainsKey(match.AwayTeamAbbr) && teamLastPlayedDate[match.AwayTeamAbbr].Date == proposedDate.Date;
            bool tooManyGamesToday = gamesOnDate.ContainsKey(dateString) && gamesOnDate[dateString] >= 12; // 하루 최대 12경기
            
            if (!homeTeamBusy && !awayTeamBusy && !tooManyGamesToday)
            {
                return proposedDate;
            }
            attempts++;
        }

        // 500번 시도 후에도 못 찾으면 그냥 가능한 첫 날짜를 반환 (예외 처리)
        Debug.LogWarning($"Could not find optimal date for {match.HomeTeamAbbr} vs {match.AwayTeamAbbr}. Placing on first available day.");
        return seasonStartDate;
    }
}