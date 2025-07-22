using SQLite4Unity3d;

// [신규] Team 클래스: 팀의 기본 정보와 주전 라인업을 관리합니다.
[Table("Team")]
public class Team
{
    [PrimaryKey]
    public int team_id { get; set; }
    public string team_name { get; set; }
    public string team_abbv { get; set; }
    public string conference { get; set; } // [추가됨] 예: "East" 또는 "West"
    public string division { get; set; }   // [추가됨] 예: "Atlantic", "Pacific"
    public string team_color { get; set; }
    public string team_logo { get; set; }
    public string best_five { get; set; }
}

// PlayerRating 클래스는 선수의 핵심 능력치, 기본 정보, 그리고 현재 가치를 저장합니다.
[Table("PlayerRating")]
public class PlayerRating
{
    [PrimaryKey]
    public int player_id { get; set; }
    public string name { get; set; }
    public string team { get; set; }
    public int age { get; set; }
    public int position { get; set; }
    public string backNumber { get; set; }
    public string height { get; set; }  // "6-5" 형식
    public int weight { get; set; }     // 파운드(lb)
    public int overallAttribute { get; set; }
    public float currentValue { get; set; }
    public int closeShot { get; set; }
    public int midRangeShot { get; set; }
    public int threePointShot { get; set; }
    public int freeThrow { get; set; }
    public int layup { get; set; }
    public int drivingDunk { get; set; }
    public int drawFoul { get; set; }
    public int interiorDefense { get; set; }
    public int perimeterDefense { get; set; }
    public int steal { get; set; }
    public int block { get; set; }
    public int speed { get; set; }
    public int stamina { get; set; }
    public int passIQ { get; set; }
    public int ballHandle { get; set; }
    public int offensiveRebound { get; set; }
    public int defensiveRebound { get; set; }
    public int potential { get; set; }
    public float injury { get; set; } // [추가] 부상 위험도 (0.01 ~ 0.1)
}

// PlayerStatus 클래스는 선수의 변하는 상태와 계약 정보를 관리합니다.
[Table("PlayerStatus")]
public class PlayerStatus
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int PlayerId { get; set; }
    public int YearsLeft { get; set; }
    public long Salary { get; set; }
    public int Stamina { get; set; }
    public bool IsInjured { get; set; }
    public int InjuryDaysLeft { get; set; }
}

// PlayerStat 클래스는 선수의 경기별 기록을 저장합니다.
[Table("PlayerStat")]
public class PlayerStat
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } // 선수 이름
    public string TeamAbbr { get; set; }   // 소속팀 약어
    public int Season { get; set; }
    public string GameId { get; set; }
    public int SecondsPlayed { get; set; } // 초 단위 출전 시간
    public int Points { get; set; }
    public int Assists { get; set; }
    public int Rebounds { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public int ThreePointersMade { get; set; }
    public int ThreePointersAttempted { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public int PersonalFouls { get; set; }
    public int PlusMinus { get; set; }
    public string RecordedAt { get; set; }
}

// User 테이블: 사용자의 게임 진행 상태와 정보를 저장합니다.
[Table("User")]
public class User
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string CoachName { get; set; }
    public string SelectedTeamAbbr { get; set; }
    public int CurrentSeason { get; set; }
    public string CurrentDate { get; set; }
}

// TeamFinance 테이블: 리그의 모든 팀 재정 정보를 관리합니다.
[Table("TeamFinance")]
public class TeamFinance
{
    [PrimaryKey] public string TeamAbbr { get; set; }
    public int Season { get; set; }
    public int Standing { get; set; } // [추가] 팀의 현재 등수
    public int Wins { get; set; } // [추가됨]
    public int Losses { get; set; } // [추가됨]
    public long CurrentTeamSalary { get; set; }
    public long TeamBudget { get; set; }
}

[Table("Schedule")]
public class Schedule
{
    [PrimaryKey] // AutoIncrement 제거, GameId를 직접 생성하여 할당
    public string GameId { get; set; } // 각 경기의 고유 ID (int -> string)
    
    public int Season { get; set; } // 시즌 (예: 2025)
    
    public string GameDate { get; set; } // 경기 날짜 "YYYY-MM-DD" 형식
    
    public string HomeTeamAbbr { get; set; }
    
    public string AwayTeamAbbr { get; set; }
    
    public int? HomeTeamScore { get; set; } // 경기가 끝나기 전까지는 NULL
    
    public int? AwayTeamScore { get; set; } // NULL이 가능하도록 int? 타입 사용
    
    public string GameStatus { get; set; } // "Scheduled", "Final" 등
}