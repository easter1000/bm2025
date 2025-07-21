using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class ScheduleManager
{
    private List<Team> allTeams;
    private readonly DateTime seasonStartDate = new DateTime(2025, 10, 21);
    private readonly DateTime seasonEndDate = new DateTime(2026, 4, 15); // 마지막 날 포함

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

    // 82경기 규칙에 따라 모든 경기 조합을 생성 (팀당 82경기 보장)
    private List<Schedule> CreateAllMatchups()
    {
        var matchups = new List<Schedule>();

        // ──────────────────────────────────────
        // 1) 같은 디비전 (4경기) + 다른 컨퍼런스 (2경기)
        // ──────────────────────────────────────
        foreach (var teamA in allTeams)
        {
            foreach (var teamB in allTeams)
            {
                if (teamA.team_id >= teamB.team_id) continue; // 중복 방지 (A<B)

                if (teamA.division == teamB.division)
                {
                    // 4경기 (홈2/원정2)
                    AddMatchups(matchups, teamA, teamB, 2);
                    AddMatchups(matchups, teamB, teamA, 2);
                }
                else if (teamA.conference != teamB.conference)
                {
                    // 2경기 (홈1/원정1)
                    AddMatchups(matchups, teamA, teamB, 1);
                    AddMatchups(matchups, teamB, teamA, 1);
                }
            }
        }

        // ──────────────────────────────────────
        // 2) 같은 컨퍼런스/다른 디비전 (10팀) → 팀당 6팀과 4경기, 4팀과 3경기
        //    팀 간 대칭을 유지하며, 각 팀의 4경기 슬롯 6개를 충족하도록 배정
        // ──────────────────────────────────────

        // 남은 게임 슬롯(4G 시리즈)을 추적
        var fourGameQuotaLeft = allTeams.ToDictionary(t => t.team_abbv, _ => 6);

        // 모든 해당 페어 수집 (conference 동일, division 다름)
        var sameConfPairs = new List<(Team A, Team B)>();
        for (int i = 0; i < allTeams.Count; i++)
        {
            for (int j = i + 1; j < allTeams.Count; j++)
            {
                Team A = allTeams[i];
                Team B = allTeams[j];
                if (A.conference == B.conference && A.division != B.division)
                {
                    sameConfPairs.Add((A, B));
                }
            }
        }

        // 랜덤 순회
        sameConfPairs = sameConfPairs.OrderBy(_ => Guid.NewGuid()).ToList();

        // 할당 결과 저장 (true=4경기, false=3경기) → 후속 조정에 사용
        var pairIsFourGame = new Dictionary<(string, string), bool>();

        foreach (var pair in sameConfPairs)
        {
            bool assignFour = false;

            if (fourGameQuotaLeft[pair.A.team_abbv] > 0 && fourGameQuotaLeft[pair.B.team_abbv] > 0)
            {
                // 두 팀 모두 4게임 슬롯 여유 있을 때 60% 확률로 4경기
                assignFour = UnityEngine.Random.value < 0.6f;
            }

            // 만약 할당이 4경기인데 한쪽이라도 quota=0 이면 강제로 3경기
            if (assignFour && (fourGameQuotaLeft[pair.A.team_abbv] == 0 || fourGameQuotaLeft[pair.B.team_abbv] == 0))
                assignFour = false;

            if (assignFour)
            {
                pairIsFourGame[(pair.A.team_abbv, pair.B.team_abbv)] = true;
                fourGameQuotaLeft[pair.A.team_abbv]--;
                fourGameQuotaLeft[pair.B.team_abbv]--;

                // 4경기: 홈2/원정2
                AddMatchups(matchups, pair.A, pair.B, 2);
                AddMatchups(matchups, pair.B, pair.A, 2);
            }
            else
            {
                pairIsFourGame[(pair.A.team_abbv, pair.B.team_abbv)] = false;

                // 3경기: 2-1 랜덤 분배
                bool aGetsTwoHome = UnityEngine.Random.value > 0.5f;
                int aHome = aGetsTwoHome ? 2 : 1;
                int bHome = 3 - aHome;
                AddMatchups(matchups, pair.A, pair.B, aHome);
                AddMatchups(matchups, pair.B, pair.A, bHome);
            }
        }

        // ──────────────────────────────────────
        // 3) 만약 quota가 남은 팀이 있으면 (이론상 드물지만) 3→4 업그레이드
        // ──────────────────────────────────────
        if (fourGameQuotaLeft.Values.Any(v => v > 0))
        {
            foreach (var pair in sameConfPairs)
            {
                if (fourGameQuotaLeft[pair.A.team_abbv] == 0 || fourGameQuotaLeft[pair.B.team_abbv] == 0)
                    continue;

                if (!pairIsFourGame[(pair.A.team_abbv, pair.B.team_abbv)])
                {
                    // 현재 3경기 → 4경기로 업그레이드 (추가 1경기씩 홈/원정 균등)
                    AddMatchups(matchups, pair.A, pair.B, 1);
                    AddMatchups(matchups, pair.B, pair.A, 1);

                    pairIsFourGame[(pair.A.team_abbv, pair.B.team_abbv)] = true;
                    fourGameQuotaLeft[pair.A.team_abbv]--;
                    fourGameQuotaLeft[pair.B.team_abbv]--;

                    if (fourGameQuotaLeft.Values.All(v => v == 0))
                        break;
                }
            }
        }

        // 최종 검증: 모든 팀 82경기인지 로그
        var gamesPerTeam = new Dictionary<string, int>();
        foreach (var s in matchups)
        {
            if (!gamesPerTeam.ContainsKey(s.HomeTeamAbbr)) gamesPerTeam[s.HomeTeamAbbr] = 0;
            if (!gamesPerTeam.ContainsKey(s.AwayTeamAbbr)) gamesPerTeam[s.AwayTeamAbbr] = 0;
            gamesPerTeam[s.HomeTeamAbbr]++;
            gamesPerTeam[s.AwayTeamAbbr]++;
        }

        foreach (var kv in gamesPerTeam)
        {
            if (kv.Value != 82)
            {
                Debug.LogWarning($"[ScheduleManager] Team {kv.Key} has {kv.Value} games instead of 82.");
            }
        }

        return matchups;
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