using SQLite4Unity3d;

// PlayerRating 클래스는 선수의 핵심 능력치를 정의합니다.
[Table("PlayerRating")]
public class PlayerRating
{
    [PrimaryKey]
    public int player_id { get; set; }
    public string name { get; set; }
    public string team { get; set; }
    public int position { get; set; }
    public int overallAttribute { get; set; }
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

// PlayerStatus 클래스는 선수의 변하는 상태를 관리합니다.
[Table("PlayerStatus")]
public class PlayerStatus
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int PlayerId { get; set; }
    public string ContractType { get; set; }
    public int YearsLeft { get; set; }
    public int Stamina { get; set; }
    public bool IsInjured { get; set; }
    public int InjuryDaysLeft { get; set; }
    public string LastChecked { get; set; }
}

// PlayerStat (경기 기록용) - [최종 수정 버전]
[Table("PlayerStat")]
public class PlayerStat
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int PlayerId { get; set; }
    public int Season { get; set; }
    public int GameNumber { get; set; }
    
    // --- 기본 누적 스탯 ---
    public int MinutesPlayedInSeconds { get; set; }
    public int Points { get; set; }
    public int Assists { get; set; }
    public int OffensiveRebounds { get; set; }
    public int DefensiveRebounds { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int Fouls { get; set; }

    // --- 슛 관련 누적 스탯 ---
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public int ThreePointersMade { get; set; }
    public int ThreePointersAttempted { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }

    // --- 특수 스탯 ---
    public int PlusMinus { get; set; }

    public string RecordedAt { get; set; }
}