using System.Collections.Generic;

// 경기에 참여하는 선수의 실시간 데이터와 능력치를 통합 관리
public class GamePlayer
{
    public PlayerRating Rating { get; private set; }
    public LivePlayerStats Stats { get; private set; }
    public int TeamId { get; private set; }
    
    // [신규] 경기 중 실시간으로 변하는 체력 (100이 최대)
    public float CurrentStamina { get; set; } = 100f;

    // [추가] 오늘 경기의 최대 스태미나 (경기 시작 시 DB값으로 설정됨)
    public float MaxStaminaForGame { get; set; }

    // [핵심 추가] 선수가 현재 코트 위에 있는지 여부를 명시적으로 추적
    public bool IsOnCourt { get; set; } = false;

    // [추가] 6반칙 퇴장 또는 부상으로 경기에서 제외되었는지 여부
    public bool IsEjected { get; set; } = false;

    // [추가] DB상의 부상 상태를 경기 시작 시 저장
    public bool IsCurrentlyInjured { get; set; } = false;

    // [추가] 부상과 스태미나 페널티가 모두 적용된 선수의 현재 실질 OVR
    public int EffectiveOverall { get; set; }

    public GamePlayer(PlayerRating rating, int teamId)
    {
        Rating = rating;
        TeamId = teamId;
        Stats = new LivePlayerStats();
        MaxStaminaForGame = 100f; // 기본값 설정
        EffectiveOverall = rating.overallAttribute; // 초기값은 기본 OVR로 설정
    }

    public PlayerStat ExportToPlayerStat(int playerId, int season, string gameId, string gameDate, string teamAbbr)
    {
        var stat = new PlayerStat
        {
            PlayerId = playerId,
            Season = season,
            GameId = gameId,
            GameDate = gameDate, // gameDate 인자 사용
            PlayerName = this.Rating.name,
            TeamAbbr = teamAbbr, // 매개변수로 받은 정확한 팀 약어 사용
            SecondsPlayed = (int)this.Stats.MinutesPlayedInSeconds,
            Points = this.Stats.Points,
            Assists = this.Stats.Assists,
            Rebounds = this.Stats.DefensiveRebounds + this.Stats.OffensiveRebounds,
            Steals = this.Stats.Steals,
            Blocks = this.Stats.Blocks,
            Turnovers = this.Stats.Turnovers,
            FieldGoalsMade = this.Stats.FieldGoalsMade,
            FieldGoalsAttempted = this.Stats.FieldGoalsAttempted,
            ThreePointersMade = this.Stats.ThreePointersMade,
            ThreePointersAttempted = this.Stats.ThreePointersAttempted,
            FreeThrowsMade = this.Stats.FreeThrowsMade,
            FreeThrowsAttempted = this.Stats.FreeThrowsAttempted,
            PersonalFouls = this.Stats.PersonalFouls, // Fouls -> PersonalFouls
            PlusMinus = this.Stats.PlusMinus
        };
        return stat;
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
    public int PersonalFouls { get; set; }
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
    public int Season { get; set; }
    public string GameId { get; set; }
    public string GameDate { get; set; } // [추가] 경기 날짜
    public string HomeTeamName { get; set; } // 이름 -> 약칭으로 사용
    public string HomeTeamAbbr { get; set; } // [추가] 홈팀 약칭
    public string AwayTeamName { get; set; }
    public string AwayTeamAbbr { get; set; } // [추가] 어웨이팀 약칭
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    // --- [추가] 아래 속성들 복구 ---
    public int Quarter { get; set; } = 1;
    public float GameClockSeconds { get; set; } = 720f; // 12분
    public float ShotClockSeconds { get; set; } = 24f;
    public int PossessingTeamId { get; set; } = 0; // 0: Home, 1: Away
    
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