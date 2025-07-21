using SQLite4Unity3d;

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
    public int backNumber { get; set; } // 선수의 등 번호
    public string height { get; set; }  // "6-5" 형식
    public int weight { get; set; }     // 파운드(lb)
    public int overallAttribute { get; set; }
    public float currentValue { get; set; } // [추가됨] 선수의 현재 거래 가치
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
}

// PlayerStatus 클래스는 선수의 변하는 상태와 계약 정보를 관리합니다.
[Table("PlayerStatus")]
public class PlayerStatus
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int PlayerId { get; set; }
    public string ContractType { get; set; }
    public int YearsLeft { get; set; }
    public long Salary { get; set; }
    public int Stamina { get; set; }
    public bool IsInjured { get; set; }
    public int InjuryDaysLeft { get; set; }
    public string LastChecked { get; set; }
}

// PlayerStat 클래스는 선수의 경기별 기록을 저장합니다.
[Table("PlayerStat")]
public class PlayerStat
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int PlayerId { get; set; }
    public int Season { get; set; }
    public int GameNumber { get; set; }
    public int MinutesPlayed { get; set; }
    public int Points { get; set; }
    public int Assists { get; set; }
    public int Rebounds { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
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
    public string LastSaved { get; set; }
}

// TeamFinance 테이블: 리그의 모든 팀 재정 정보를 관리합니다.
[Table("TeamFinance")]
public class TeamFinance
{
    [PrimaryKey] public string TeamAbbr { get; set; }
    public int Season { get; set; }
    public long SalaryCap { get; set; }
    public long LuxuryTaxThreshold { get; set; }
    public long CurrentTeamSalary { get; set; }
    public long TeamBudget { get; set; }
}