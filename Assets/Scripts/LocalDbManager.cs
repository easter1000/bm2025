using UnityEngine;
using System.IO;
using System.Linq;
using SQLite4Unity3d;
using System;
using System.Collections.Generic;

public class LocalDbManager : MonoBehaviour
{
    private static LocalDbManager _instance;
    public static LocalDbManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LocalDbManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LocalDbManager");
                    _instance = go.AddComponent<LocalDbManager>();
                }
            }
            return _instance;
        }
    }

    private SQLiteConnection _db;
    private string _dbPath;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _dbPath = Path.Combine(Application.persistentDataPath, "game_records.db");
        bool firstRun = !File.Exists(_dbPath);
        _db = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

        if (firstRun)
        {
            Debug.Log("[DB] First run: Creating and populating database...");
            _db.CreateTable<Team>();
            _db.CreateTable<PlayerRating>();
            _db.CreateTable<PlayerStatus>();
            _db.CreateTable<PlayerStat>();
            _db.CreateTable<User>();
            _db.CreateTable<TeamFinance>();
            _db.CreateTable<Schedule>();
            Debug.Log("[DB] Tables created.");
            PopulateInitialData();
        }
        else
        {
            Debug.Log("[DB] Database already exists. Initializing connection.");
        }
        Debug.Log($"[DB] Initialized at {_dbPath}");
    }

    private void PopulateInitialData()
    {
        // --- 1. 선수 데이터 로드 및 처리 ---
        TextAsset playersJson = Resources.Load<TextAsset>("players");
        if (playersJson == null) { Debug.LogError("[DB] players.json not found!"); return; }
        PlayerMasterDataList playerList = JsonUtility.FromJson<PlayerMasterDataList>(playersJson.text);
        if (playerList == null || playerList.players == null) { Debug.LogError("[DB] Failed to parse players.json."); return; }

        var ratings = new List<PlayerRating>();
        var statuses = new List<PlayerStatus>();
        var teamSalaries = new Dictionary<string, long>();

        foreach (var p in playerList.players)
        {
            float currentValue = CalculatePlayerCurrentValue(p.overallAttribute, p.age, p.contract_value);
            ratings.Add(new PlayerRating
            {
                player_id = p.player_id,
                name = p.name,
                team = p.team,
                age = p.age,
                position = p.position,
                backNumber = p.backnumber.ToString(),
                height = p.height,
                weight = p.weight,
                overallAttribute = p.overallAttribute,
                currentValue = currentValue, // [수정됨] 계산된 가치 할당
                closeShot = p.closeShot,
                midRangeShot = p.midRangeShot,
                threePointShot = p.threePointShot,
                freeThrow = p.freeThrow,
                layup = p.layup,
                drivingDunk = p.drivingDunk,
                drawFoul = p.drawFoul,
                interiorDefense = p.interiorDefense,
                perimeterDefense = p.perimeterDefense,
                steal = p.steal,
                block = p.block,
                speed = p.speed,
                stamina = p.stamina,
                passIQ = p.passIQ,
                ballHandle = p.ballHandle,
                offensiveRebound = p.offensiveRebound,
                defensiveRebound = p.defensiveRebound,
                potential = p.potential
            });
            statuses.Add(new PlayerStatus { PlayerId = p.player_id, YearsLeft = p.contract_years_left, Salary = p.contract_value, Stamina = 100, IsInjured = false, InjuryDaysLeft = 0});
            if (!teamSalaries.ContainsKey(p.team)) teamSalaries[p.team] = 0;
            teamSalaries[p.team] += p.contract_value;
        }
        _db.InsertAll(ratings); _db.InsertAll(statuses);
        Debug.Log($"[DB] Populated PlayerRating and PlayerStatus with {playerList.players.Length} players.");
        
        // --- 2. 팀 데이터 로드 및 처리 ---
        TextAsset teamsJson = Resources.Load<TextAsset>("teams");
        if (teamsJson == null) { Debug.LogError("[DB] teams.json not found!"); return; }
        TeamMasterDataList teamList = JsonUtility.FromJson<TeamMasterDataList>(teamsJson.text);
        if (teamList == null || teamList.teams == null) { Debug.LogError("[DB] Failed to parse teams.json."); return; }
        
        var teams = new List<Team>();
        foreach (var t in teamList.teams)
        {
            List<int> starterIds = new List<int>();
            var playersInTeam = ratings.Where(p => p.team == t.team_name).ToList();
            // 이미 선발 라인업에 배정된 선수들의 ID를 추적하여 중복 배정을 방지합니다.
            HashSet<int> selectedStarterIds = new HashSet<int>();
            for (int pos = 1; pos <= 5; pos++)
            {
                int minPos = Mathf.Max(1, pos - 1);
                int maxPos = Mathf.Min(5, pos + 1);

                // 1) 먼저 정확히 해당 포지션에 맞는 후보 중 최고 OVR 선수를 찾습니다.
                var exactMatch = playersInTeam
                    .Where(p => p.position == pos && !selectedStarterIds.Contains(p.player_id))
                    .OrderByDescending(p => p.overallAttribute)
                    .FirstOrDefault();

                PlayerRating chosen = exactMatch;

                // 2) 정확 매칭 선수가 없으면 ±1 범위안에서 최고 OVR 선수를 선택합니다.
                if (chosen == null)
                {
                    chosen = playersInTeam
                        .Where(p => p.position >= minPos && p.position <= maxPos && !selectedStarterIds.Contains(p.player_id))
                        .OrderByDescending(p => p.overallAttribute)
                        .FirstOrDefault();
                }

                if (chosen != null)
                {
                    starterIds.Add(chosen.player_id);
                    selectedStarterIds.Add(chosen.player_id);
                }
                else
                {
                    // 여전히 후보가 없으면 0으로 채워 빈 슬롯을 표시합니다.
                    starterIds.Add(0);
                }
            }
            teams.Add(new Team { team_id = t.team_id, team_name = t.team_name, team_abbv = t.team_abbv, conference = t.conference, division = t.division, team_color = t.team_color, team_logo = t.team_logo, best_five = string.Join(",", starterIds) });
        }
        _db.InsertAll(teams);
        Debug.Log($"[DB] Populated Team with {teams.Count} teams and calculated best five.");

        // --- 3. 팀 재정 데이터 처리 ---
        var teamFinances = new List<TeamFinance>();
        long currentSeasonSalaryCap = 141000000;
        foreach (var teamSalaryPair in teamSalaries)
        {
            teamFinances.Add(new TeamFinance { TeamAbbr = teamSalaryPair.Key, Season = 2025, Wins = 0, Losses = 0, SalaryCap = currentSeasonSalaryCap, CurrentTeamSalary = teamSalaryPair.Value, TeamBudget = 200000000 });
        }
        _db.InsertAll(teamFinances);
        Debug.Log($"[DB] Populated TeamFinance for {teamFinances.Count} teams.");
    }

    #region Public DB Accessors
    
    // --- User ---
    public User GetUser() => _db.Table<User>().FirstOrDefault();

    // --- Team & Schedule ---
    public List<Team> GetAllTeams() => _db.Table<Team>().ToList();
    public Team GetTeam(string teamAbbr) => _db.Table<Team>().FirstOrDefault(t => t.team_abbv == teamAbbr);
    public Team GetTeam(int teamId) => _db.Find<Team>(teamId);
    public TeamFinance GetTeamFinance(string teamAbbr, int season) => _db.Table<TeamFinance>().FirstOrDefault(f => f.TeamAbbr == teamAbbr && f.Season == season);
    public void ClearScheduleTable() { _db.DropTable<Schedule>(); _db.CreateTable<Schedule>(); Debug.Log("[DB] Schedule table cleared and recreated."); }
    public void InsertSchedule(List<Schedule> schedule) => _db.InsertAll(schedule);
    public List<Schedule> GetGamesForDate(string date) => _db.Table<Schedule>().Where(g => g.GameDate == date && g.GameStatus == "Scheduled").ToList();
    public void UpdateGameResult(string gameId, int homeScore, int awayScore)
    {
        var game = _db.Find<Schedule>(gameId);
        if (game != null) { game.HomeTeamScore = homeScore; game.AwayTeamScore = awayScore; game.GameStatus = "Final"; _db.Update(game); }
    }
    public void UpdateTeamWinLossRecord(string teamAbbr, bool won, int season)
    {
        var teamFinance = GetTeamFinance(teamAbbr, season);
        if (teamFinance != null)
        {
            if (won) teamFinance.Wins++;
            else teamFinance.Losses++;
            _db.Update(teamFinance);
        }
    }
    public List<Schedule> GetScheduleForTeam(string teamAbbr, int season)
    {
        // 특정 팀의 한 시즌 전체 경기 일정을 가져옵니다.
        return _db.Table<Schedule>()
                .Where(g => (g.HomeTeamAbbr == teamAbbr || g.AwayTeamAbbr == teamAbbr) && g.Season == season)
                .OrderBy(g => g.GameDate)
                .ToList();
    }
    // --- Player ---
    public PlayerRating GetPlayerRating(int playerId) => _db.Find<PlayerRating>(playerId);
    public List<PlayerRating> GetAllPlayerRatings() => _db.Table<PlayerRating>().ToList();
    // 팀 약어(예: "DAL") 또는 전체 팀명("Dallas Mavericks") 어느 쪽이든 받아서 해당 팀 선수 목록을 반환합니다.
    public List<PlayerRating> GetPlayersByTeam(string teamIdentifier)
    {
        // 먼저 Team 테이블에서 매칭되는 엔티티를 찾아 전체 이름과 약어를 모두 확보합니다.
        Team teamEntity = _db.Table<Team>().FirstOrDefault(t => t.team_abbv == teamIdentifier || t.team_name == teamIdentifier);

        if (teamEntity == null)
        {
            // Team 테이블에 없으면 식별자를 그대로 사용하여 검색합니다.
            return _db.Table<PlayerRating>().Where(p => p.team == teamIdentifier).ToList();
        }

        string fullName = teamEntity.team_name;
        string abbr = teamEntity.team_abbv;

        // PlayerRating.team 컬럼은 현재 전체 팀명을 저장하고 있으므로 우선 전체 이름으로 검색한 뒤,
        // 혹시라도 약어로 저장된 데이터가 있을 가능성까지 대비해 약어도 함께 포함합니다.
        return _db.Table<PlayerRating>()
                 .Where(p => p.team == fullName || p.team == abbr)
                 .ToList();
    }
    public PlayerStatus GetPlayerStatus(int playerId) => _db.Table<PlayerStatus>().FirstOrDefault(p => p.PlayerId == playerId);
    public void InsertPlayerStats(List<PlayerStat> stats) => _db.InsertAll(stats);

    #endregion

    #region Value Calculation Helpers
    private float CalculatePlayerCurrentValue(int overall, int age, long salary)
    {
        float value = overall * 10.0f;
        float agePenalty = Mathf.Max(0, age - 28) * 7.5f;
        value -= agePenalty;
        long marketValue = GetMarketValueByOverall(overall);
        float contractBonus = (float)(marketValue - salary) / 1000000;
        value += contractBonus;
        return Mathf.Max(1.0f, value);
    }
    private long GetMarketValueByOverall(int overall)
    {
        if (overall >= 95) return 50000000; if (overall >= 90) return 40000000; if (overall >= 85) return 30000000;
        if (overall >= 80) return 20000000; if (overall >= 75) return 12000000; if (overall >= 70) return 5000000;
        return 2000000;
    }
    #endregion

    void OnDestroy()
    {
        if (_db != null)
        {
            _db.Close();
        }
    }

    #region JSON Helper Classes
    [Serializable]
    public class PlayerMasterData
    {
        public int player_id;
        public string name;
        public string team;
        public string height;
        public int weight;
        public int age;
        public int position;
        public int overallAttribute;
        public int closeShot;
        public int midRangeShot;
        public int threePointShot;
        public int freeThrow;
        public int layup;
        public int drivingDunk;
        public int drawFoul;
        public int interiorDefense;
        public int perimeterDefense;
        public int steal;
        public int block;
        public int speed;
        public int stamina;
        public int passIQ;
        public int ballHandle;
        public int offensiveRebound;
        public int defensiveRebound;
        public int potential;
        public string backnumber;
        public int contract_years_left;
        public long contract_value;
    }

    [Serializable] 
    public class PlayerMasterDataList 
    { 
        public PlayerMasterData[] players; 
    }

    [Serializable] 
    public class TeamMasterData 
    { 
        public int team_id; 
        public string team_name; 
        public string team_abbv; 
        public string conference; 
        public string division; 
        public string team_color; 
        public string team_logo; 
    }
    
    [Serializable] 
    public class TeamMasterDataList 
    { 
        public TeamMasterData[] teams; 
    }
    #endregion
}