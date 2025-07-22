using System.Collections.Generic;

// 경기에 참여하는 선수의 실시간 데이터와 능력치를 통합 관리
public class GamePlayer
{
    public PlayerRating Rating { get; private set; }
    public LivePlayerStats Stats { get; private set; }
    public int TeamId { get; private set; }
    
    // [신규] 경기 중 실시간으로 변하는 체력 (100이 최대)
    public float CurrentStamina { get; set; } = 100f;

    // [핵심 추가] 선수가 현재 코트 위에 있는지 여부를 명시적으로 추적
    public bool IsOnCourt { get; set; } = false;

    public GamePlayer(PlayerRating rating, int teamId)
    {
        Rating = rating;
        TeamId = teamId;
        Stats = new LivePlayerStats();
    }

    public PlayerStat ExportToPlayerStat(int playerId, int season, string gameId)
    {
        return new PlayerStat
        {
            PlayerId = playerId,
            PlayerName = this.Rating.name, // 이름 추가
            TeamAbbr = this.Rating.team,   // 팀 약어 추가
            Season = season,
            GameId = gameId,
            SecondsPlayed = (int)this.Stats.MinutesPlayedInSeconds,
            Points = this.Stats.Points,
            Assists = this.Stats.Assists,
            Rebounds = this.Stats.OffensiveRebounds + this.Stats.DefensiveRebounds,
            Steals = this.Stats.Steals,
            Blocks = this.Stats.Blocks,
            Turnovers = this.Stats.Turnovers,
            FieldGoalsMade = this.Stats.FieldGoalsMade,             // 이하 스탯 추가
            FieldGoalsAttempted = this.Stats.FieldGoalsAttempted,
            ThreePointersMade = this.Stats.ThreePointersMade,
            ThreePointersAttempted = this.Stats.ThreePointersAttempted,
            FreeThrowsMade = this.Stats.FreeThrowsMade,
            FreeThrowsAttempted = this.Stats.FreeThrowsAttempted,
            PlusMinus = this.Stats.PlusMinus,
            RecordedAt = System.DateTime.UtcNow.ToString("s")
        };
    }
}

// 한 경기에 대한 선수의 실시간 스탯 (DB의 PlayerStat과 구조가 동일)
public class LivePlayerStats
{
    public float MinutesPlayedInSeconds { get; set; } // int -> float으로 변경하여 정확한 시간 누적
    public int Points { get; set; }
    public int Assists { get; set; }
    public int OffensiveRebounds { get; set; }
    public int DefensiveRebounds { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int Fouls { get; set; }
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public int ThreePointersMade { get; set; }
    public int ThreePointersAttempted { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public int PlusMinus { get; set; }
}

// 경기의 현재 상태를 관리
public class GameState
{
    public string HomeTeamName { get; set; }
    public string AwayTeamName { get; set; }
    public int Season { get; set; }
    public string GameId { get; set; }

    public int Quarter { get; set; } = 1;
    public float GameClockSeconds { get; set; } = 720f; // 12분
    public float ShotClockSeconds { get; set; } = 24f;
    public int PossessingTeamId { get; set; } = 0; // 0: Home, 1: Away
    public int HomeScore { get; set; } = 0;
    public int AwayScore { get; set; } = 0;
    public GamePlayer LastPasser { get; set; } = null; 
    public GamePlayer PotentialAssister { get; set; } = null;
}

// 로그 출력을 위한 구조체
public struct GameLogEntry
{
    public string TimeStamp;
    public string Description;
    public int HomeScore;
    public int AwayScore;

    public override string ToString()
    {
        return $"{TimeStamp} | {Description,-80} | {HomeScore} - {AwayScore}";
    }
}

// 시뮬레이션 결과를 담는 공통 구조체
public struct GameResult
{
    public int HomeScore;
    public int AwayScore;
    public List<PlayerStat> PlayerStats;
}