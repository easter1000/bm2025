using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// 커스텀 예외: 경기 날짜를 찾지 못했을 때 throw
public class ScheduleDateNotFoundException : Exception
{
    public ScheduleDateNotFoundException(string msg) : base(msg) { }
}

public class ScheduleManager
{
    // 싱글턴 인스턴스
    private static readonly ScheduleManager _instance = new ScheduleManager();
    public static ScheduleManager Instance => _instance;

    private ScheduleManager() { }

    private List<Team> allTeams;
    private readonly DateTime seasonStartDate = new DateTime(2025, 10, 21);
    private readonly DateTime seasonEndDate = new DateTime(2026, 4, 15);

    public void GenerateNewSeasonSchedule(int season)
    {
        allTeams = LocalDbManager.Instance.GetAllTeams();
        if (allTeams == null || allTeams.Count != 30)
        {
            Debug.LogError("팀 데이터가 30개가 아니므로 스케줄을 생성할 수 없습니다.");
            return;
        }

        LocalDbManager.Instance.ClearScheduleTable();

        const int MAX_ATTEMPTS = 5;
        int attempt = 0;
        List<Schedule> finalSchedule = null;

        while (attempt < MAX_ATTEMPTS)
        {
            attempt++;

            // 1. 매치업 생성
            List<Schedule> allMatchups = CreateAllMatchupsGuaranteed();

            if (!ValidateMatchupCounts(allMatchups))
            {
                Debug.LogWarning($"[ScheduleManager] 매치업 검증 실패 – 시도 {attempt}. 다시 생성합니다.");
                continue; // 다음 루프로 재시도
            }

            try
            {
                // 2. 날짜 배정
                finalSchedule = AssignDatesToMatchups(allMatchups, season);

                if (finalSchedule != null)
                {
                    break; // 성공
                }
            }
            catch (ScheduleDateNotFoundException ex)
            {
                Debug.LogWarning($"[ScheduleManager] {ex.Message} – 시도 {attempt} 실패, 다시 시도합니다.");
                // 루프 계속 – 재시도
            }
        }

        if (finalSchedule == null)
        {
            Debug.LogError($"[ScheduleManager] 스케줄 생성에 {MAX_ATTEMPTS}회 모두 실패했습니다. 중단합니다.");
            return;
        }

        // 하루 중복 경기 검증
        LogDuplicateTeamGamesPerDay(finalSchedule);

        // 3. DB 저장
        LocalDbManager.Instance.InsertSchedule(finalSchedule);

        Debug.Log($"[ScheduleManager] {season} 시즌에 대한 {finalSchedule.Count}개의 경기를 성공적으로 생성하고 저장했습니다. (시도 {attempt}회)");
    }

    // ScheduleManager.cs 파일의 CreateAllMatchupsGuaranteed 함수를 아래의 최종 완성 버전으로 교체해 주세요.

/// <summary>
/// NBA 82경기 규칙에 따라 모든 경기 조합을 '보장된' 방식으로 생성합니다.
/// 안전한 딕셔너리 조회를 통해 런타임 오류를 방지합니다.
/// </summary>
private List<Schedule> CreateAllMatchupsGuaranteed()
{
    var matchups = new List<Schedule>();

    // --- STEP-1 : Decide, **guaranteed**, which inter-division pairs are 4-game vs 3-game sets ---
    //  목표 : 컨퍼런스 내에서 각 팀당 exactly 6 "4-게임 시리즈"를 갖도록
    // 딕셔너리 초기화 (컨퍼런스 간 다른 디비전 매치업의 3/4게임 여부 저장)
    var interDivisionMatchupType = new Dictionary<(string, string), int>();

    var rand = new System.Random();

    foreach (var conference in allTeams.Select(t => t.conference).Distinct())
    {
        // 1-A. 준비 – 해당 컨퍼런스 팀 목록과 가능한 페어(디비전이 다른 팀들) 구하기
        var conferenceTeams = allTeams.Where(t => t.conference == conference).ToList();

        var allPairs = new List<(Team A, Team B)>();
        for (int i = 0; i < conferenceTeams.Count; i++)
        {
            for (int j = i + 1; j < conferenceTeams.Count; j++)
            {
                if (conferenceTeams[i].division != conferenceTeams[j].division)
                {
                    allPairs.Add((conferenceTeams[i], conferenceTeams[j]));
                }
            }
        }

        const int TARGET_FOUR_GAMES_PER_TEAM = 6; // 24경기

        bool success = false;
        int attempt = 0;
        const int MAX_ATTEMPTS = 500; // 충분히 많은 재시도 횟수

        while (!success && attempt < MAX_ATTEMPTS)
        {
            attempt++;

            // 카운터 초기화
            var fourGameCount = conferenceTeams.ToDictionary(t => t.team_abbv, _ => 0);
            var localMatchupType = new Dictionary<(string, string), int>();

            // 무작위 순서로 순회
            var shuffledPairs = allPairs.OrderBy(_ => rand.Next()).ToList();

            foreach (var (teamA, teamB) in shuffledPairs)
            {
                if (fourGameCount[teamA.team_abbv] < TARGET_FOUR_GAMES_PER_TEAM &&
                    fourGameCount[teamB.team_abbv] < TARGET_FOUR_GAMES_PER_TEAM)
                {
                    localMatchupType[(teamA.team_abbv, teamB.team_abbv)] = 4;
                    fourGameCount[teamA.team_abbv]++;
                    fourGameCount[teamB.team_abbv]++;
                }
                else
                {
                    localMatchupType[(teamA.team_abbv, teamB.team_abbv)] = 3;
                }
            }

            // 검증 – 모든 팀이 정확히 6개의 4-게임 시리즈를 가졌는지
            success = fourGameCount.Values.All(cnt => cnt == TARGET_FOUR_GAMES_PER_TEAM);

            if (success)
            {
                // 전역 딕셔너리에 반영
                foreach (var kvp in localMatchupType)
                {
                    interDivisionMatchupType[kvp.Key] = kvp.Value;
                }
            }
        }

        if (!success)
        {
            Debug.LogError($"[ScheduleManager] {conference} 컨퍼런스의 3/4-게임 시리즈 결정에 실패했습니다. (시도 횟수 {MAX_ATTEMPTS})");
        }
    }
    
    // 2단계: 결정된 내용을 바탕으로 모든 경기 최종 생성
    for (int i = 0; i < allTeams.Count; i++)
    {
        for (int j = i + 1; j < allTeams.Count; j++)
        {
            var teamA = allTeams[i];
            var teamB = allTeams[j];

            if (teamA.conference != teamB.conference)
            {
                AddMatchups(matchups, teamA, teamB, 1);
                AddMatchups(matchups, teamB, teamA, 1);
            }
            else
            {
                if (teamA.division == teamB.division)
                {
                    AddMatchups(matchups, teamA, teamB, 2);
                    AddMatchups(matchups, teamB, teamA, 2);
                }
                else
                {
                    // --- 여기가 수정된 핵심 부분 ---
                    var key1 = (teamA.team_abbv, teamB.team_abbv);
                    var key2 = (teamB.team_abbv, teamA.team_abbv);
                    int numGames = 0;

                    if (interDivisionMatchupType.ContainsKey(key1))
                    {
                        numGames = interDivisionMatchupType[key1];
                    }
                    else if (interDivisionMatchupType.ContainsKey(key2))
                    {
                        numGames = interDivisionMatchupType[key2];
                    }
                    else
                    {
                        // 이 로그가 표시된다면, 1단계 로직에 여전히 문제가 있다는 의미입니다.
                        Debug.LogError($"[ScheduleManager] 치명적 오류: {teamA.team_abbv}와 {teamB.team_abbv}의 경기 유형을 찾을 수 없습니다.");
                        continue; // 이 경기는 생성하지 않고 넘어갑니다.
                    }
                    
                    if (numGames == 4)
                    {
                        AddMatchups(matchups, teamA, teamB, 2);
                        AddMatchups(matchups, teamB, teamA, 2);
                    }
                    else // numGames == 3
                    {
                        bool aGetsTwoHome = UnityEngine.Random.value > 0.5f;
                        AddMatchups(matchups, teamA, teamB, aGetsTwoHome ? 2 : 1);
                        AddMatchups(matchups, teamB, teamA, aGetsTwoHome ? 1 : 2);
                    }
                }
            }
        }
    }

    return matchups;
}

    private void AddMatchups(List<Schedule> list, Team home, Team away, int count)
    {
        for (int i = 0; i < count; i++)
        {
            string gameId = $"{home.team_abbv}_vs_{away.team_abbv}_{Guid.NewGuid().ToString().Substring(0, 4)}";
            list.Add(new Schedule { GameId = gameId, HomeTeamAbbr = home.team_abbv, AwayTeamAbbr = away.team_abbv });
        }
    }

    /// <summary>
    /// 생성된 경기들에 날짜를 분배합니다. 팀의 하루 중복 경기를 방지합니다.
    /// </summary>
    private List<Schedule> AssignDatesToMatchups(List<Schedule> matchups, int season)
    {
        var shuffledMatchups = matchups.OrderBy(a => Guid.NewGuid()).ToList();

        // 각 팀이 경기를 하는 날짜 집합
        var teamScheduledDates = new Dictionary<string, HashSet<DateTime>>();
        foreach (var team in allTeams)
        {
            teamScheduledDates[team.team_abbv] = new HashSet<DateTime>();
        }

        // 날짜별 경기 수
        var gamesPerDay = new Dictionary<DateTime, int>();

        // 두 팀 조합(무순)별 이미 배정된 날짜 리스트 – 재경기 간격 계산용
        var pairScheduledDates = new Dictionary<(string, string), List<DateTime>>();

        try
        {
            foreach (var match in shuffledMatchups)
            {
                DateTime bestDate = FindBestDateForMatch(teamScheduledDates, gamesPerDay, pairScheduledDates, match);

                match.GameDate = bestDate.ToString("yyyy-MM-dd");
                match.Season = season;
                match.GameStatus = "Scheduled";

                // 상태 업데이트
                teamScheduledDates[match.HomeTeamAbbr].Add(bestDate.Date);
                teamScheduledDates[match.AwayTeamAbbr].Add(bestDate.Date);

                if (!gamesPerDay.ContainsKey(bestDate.Date))
                {
                    gamesPerDay[bestDate.Date] = 0;
                }
                gamesPerDay[bestDate.Date]++;

                var pairKey = GetPairKey(match.HomeTeamAbbr, match.AwayTeamAbbr);
                if (!pairScheduledDates.ContainsKey(pairKey))
                {
                    pairScheduledDates[pairKey] = new List<DateTime>();
                }
                pairScheduledDates[pairKey].Add(bestDate.Date);
            }

            return shuffledMatchups.OrderBy(g => DateTime.Parse(g.GameDate)).ToList();
        }
        catch (ScheduleDateNotFoundException)
        {
            // 상위에서 재시도할 수 있도록 null 반환
            return null;
        }
    }

    private DateTime FindBestDateForMatch(
        Dictionary<string, HashSet<DateTime>> teamScheduledDates,
        Dictionary<DateTime, int> gamesPerDay,
        Dictionary<(string, string), List<DateTime>> pairScheduledDates,
        Schedule match)
    {
        int totalDays = (seasonEndDate - seasonStartDate).Days;
        int maxRandomAttempts = 700; // 시도 횟수 증가 – 간격 최적화를 위해

        DateTime bestCandidate = seasonStartDate;
        int bestScore = -1; // 더 높을수록 좋음 (두 팀 사이 최소일수)
        int bestGamesSameDay = 99;

        for (int i = 0; i < maxRandomAttempts; i++)
        {
            int randomDayOffset = UnityEngine.Random.Range(0, totalDays + 1);
            DateTime proposedDate = seasonStartDate.AddDays(randomDayOffset).Date;

            bool homeBusy = teamScheduledDates[match.HomeTeamAbbr].Contains(proposedDate);
            bool awayBusy = teamScheduledDates[match.AwayTeamAbbr].Contains(proposedDate);
            int gamesOnDay = gamesPerDay.ContainsKey(proposedDate) ? gamesPerDay[proposedDate] : 0;

            if (homeBusy || awayBusy || gamesOnDay >= 12) continue;

            // 간격 점수 계산
            var pairKey = GetPairKey(match.HomeTeamAbbr, match.AwayTeamAbbr);
            int minGap = 9999;
            if (pairScheduledDates.TryGetValue(pairKey, out var prevDates))
            {
                foreach (var d in prevDates)
                {
                    int diff = Math.Abs((d - proposedDate).Days);
                    if (diff < minGap) minGap = diff;
                }
            }

            // prevDates 없으면 minGap 그대로 9999 (최상)

            if (minGap > bestScore || (minGap == bestScore && gamesOnDay < bestGamesSameDay))
            {
                bestScore = minGap;
                bestCandidate = proposedDate;
                bestGamesSameDay = gamesOnDay;
            }
        }

        if (bestScore >= 0)
        {
            return bestCandidate;
        }

        // 위 랜덤 탐색 실패 시 – 기존 로직으로 순차 탐색
        for (int dayOffset = 0; dayOffset <= totalDays; dayOffset++)
        {
            DateTime proposedDate = seasonStartDate.AddDays(dayOffset).Date;

            bool homeBusy = teamScheduledDates[match.HomeTeamAbbr].Contains(proposedDate);
            bool awayBusy = teamScheduledDates[match.AwayTeamAbbr].Contains(proposedDate);
            int gamesOnDay = gamesPerDay.ContainsKey(proposedDate) ? gamesPerDay[proposedDate] : 0;

            if (!homeBusy && !awayBusy && gamesOnDay < 12)
            {
                return proposedDate;
            }
        }

        // 여기까지 오면 스케줄링이 불가능 – 마지막 수단
        throw new ScheduleDateNotFoundException($"{match.HomeTeamAbbr} vs {match.AwayTeamAbbr} 경기의 유효한 날짜를 찾지 못했습니다.");
    }

    /// <summary>
    /// 두 팀 약어를 정렬해 항상 동일한 키를 반환한다.
    /// </summary>
    private (string, string) GetPairKey(string teamA, string teamB)
    {
        return string.CompareOrdinal(teamA, teamB) < 0 ? (teamA, teamB) : (teamB, teamA);
    }

    /// <summary>
    /// 생성된 매치업이 모든 팀에 대해 82경기를 보장하는지 검증하는 유틸리티 메소드
    /// </summary>
    private bool ValidateMatchupCounts(List<Schedule> matchups)
    {
        var gamesPerTeam = allTeams.ToDictionary(t => t.team_abbv, _ => 0);
        foreach (var s in matchups)
        {
            gamesPerTeam[s.HomeTeamAbbr]++;
            gamesPerTeam[s.AwayTeamAbbr]++;
        }

        bool allValid = true;
        foreach (var kvp in gamesPerTeam)
        {
            if (kvp.Value != 82)
            {
                Debug.LogWarning($"[검증 실패] 팀 {kvp.Key}의 경기 수가 {kvp.Value}경기입니다. (82경기가 아님)");
                allValid = false;
            }
        }
        
        if (matchups.Count != 1230) // 30팀 * 82경기 / 2
        {
            Debug.LogWarning($"[검증 실패] 총 경기 수가 {matchups.Count}개입니다. (1230개가 아님)");
            allValid = false;
        }

        if (allValid)
        {
            Debug.Log("[검증 성공] 모든 팀이 82경기를 가지며, 총 경기 수는 1230개입니다.");
        }
        return allValid;
    }

    /// <summary>
    /// 동일 팀이 같은 날짜에 두 번 이상 배정됐는지 검사하여 로그를 출력한다.
    /// </summary>
    /// <param name="schedules">최종 스케줄 목록</param>
    private void LogDuplicateTeamGamesPerDay(List<Schedule> schedules)
    {
        var countPerTeamDate = new Dictionary<(string Team, DateTime Date), int>();

        foreach (var g in schedules)
        {
            if (!DateTime.TryParse(g.GameDate, out DateTime parsedDate))
            {
                Debug.LogWarning($"[스케줄 검증] 잘못된 날짜 형식: {g.GameDate}");
                continue;
            }

            var date = parsedDate.Date;

            var homeKey = (g.HomeTeamAbbr, date);
            var awayKey = (g.AwayTeamAbbr, date);

            if (!countPerTeamDate.ContainsKey(homeKey)) countPerTeamDate[homeKey] = 0;
            countPerTeamDate[homeKey]++;

            if (!countPerTeamDate.ContainsKey(awayKey)) countPerTeamDate[awayKey] = 0;
            countPerTeamDate[awayKey]++;
        }

        var duplicates = countPerTeamDate.Where(kvp => kvp.Value > 1).ToList();

        if (duplicates.Count == 0)
        {
            Debug.Log("[검증 성공] 하루에 두 경기 이상 치르는 팀이 없습니다.");
        }
        else
        {
            foreach (var dup in duplicates)
            {
                Debug.LogWarning($"[검증 실패] 팀 {dup.Key.Team}가 {dup.Key.Date:yyyy-MM-dd}에 {dup.Value}경기를 보유하고 있습니다.");
            }
        }
    }
}