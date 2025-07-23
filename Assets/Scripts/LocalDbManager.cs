using UnityEngine;
using System.IO;
using System.Linq;
using SQLite4Unity3d;
using System;
using System.Collections.Generic;

public class LocalDbManager : MonoBehaviour
{
    #region Singleton
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
                    GameObject obj = new GameObject("LocalDbManager");
                    _instance = obj.AddComponent<LocalDbManager>();
                }
            }
            return _instance;
        }
    }
    #endregion

    // [수정] DB 연결 객체 대신, DB 파일 경로만 멤버 변수로 관리합니다.
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

        // [수정] DB 경로를 설정하고, 필요 시 최초 설정을 진행합니다.
        InitializeDatabase();
    }

    /// <summary>
    /// 데이터베이스 경로를 설정하고, 파일이 없을 경우 테이블과 초기 데이터를 생성합니다.
    /// </summary>
    private void InitializeDatabase()
    {
        _dbPath = Path.Combine(Application.persistentDataPath, "game_records.db");
        
        // DB 파일이 없는 최초 실행 시에만 테이블과 데이터를 생성합니다.
        if (!File.Exists(_dbPath))
        {
            Debug.Log("[DB] First run: Creating and populating database...");
            
            // using 구문을 통해 DB 연결을 생성하고, 작업 완료 후 자동으로 닫습니다.
            using (var db = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                // 테이블 생성
                CreateAllTables(db);
                // 초기 데이터 삽입
                PopulateInitialData(db);
            }
        }
        else
        {
            Debug.Log("[DB] Database already exists. Initializing path.");
        }
        Debug.Log($"[DB] Initialized at {_dbPath}");
    }

    /// <summary>
    /// 데이터베이스에 모든 테이블을 생성합니다.
    /// </summary>
    private void CreateAllTables(SQLiteConnection db)
    {
        db.CreateTable<Team>();
        db.CreateTable<PlayerRating>();
        db.CreateTable<PlayerStatus>();
        db.CreateTable<PlayerStat>();
        db.CreateTable<User>();
        db.CreateTable<TeamFinance>();
        db.CreateTable<Schedule>();
        Debug.Log("[DB] Tables created.");
    }
    
    /// <summary>
    /// JSON 파일로부터 초기 데이터를 읽어와 DB에 삽입합니다.
    /// </summary>
    private void PopulateInitialData(SQLiteConnection db)
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
            float currentValue = CalculatePlayerCurrentValue(p.overallAttribute, p.age, p.potential, (long)(p.contract_value / p.contract_years_left));
            ratings.Add(new PlayerRating { /* ... PlayerRating 초기화 ... */ });
            statuses.Add(new PlayerStatus { PlayerId = p.player_id, YearsLeft = p.contract_years_left, Salary = p.contract_value, Stamina = 100, IsInjured = false, InjuryDaysLeft = 0});
            
            long annualSalary = p.contract_years_left > 0 ? p.contract_value / p.contract_years_left : 0;
            if (!teamSalaries.ContainsKey(p.team)) teamSalaries[p.team] = 0;
            teamSalaries[p.team] += annualSalary;
        }
        db.InsertAll(ratings); 
        db.InsertAll(statuses);
        Debug.Log($"[DB] Populated PlayerRating and PlayerStatus with {playerList.players.Length} players.");
        
        // --- 2. 팀 데이터 로드 및 처리 ---
        TextAsset teamsJson = Resources.Load<TextAsset>("teams");
        if (teamsJson == null) { Debug.LogError("[DB] teams.json not found!"); return; }
        TeamMasterDataList teamList = JsonUtility.FromJson<TeamMasterDataList>(teamsJson.text);
        if (teamList == null || teamList.teams == null) { Debug.LogError("[DB] Failed to parse teams.json."); return; }
        
        var teams = new List<Team>();
        foreach (var t in teamList.teams)
        {
            // ... Best Five 계산 로직 ...
            teams.Add(new Team { /* ... Team 초기화 ... */ });
        }
        db.InsertAll(teams);
        Debug.Log($"[DB] Populated Team with {teams.Count} teams and calculated best five.");

        // --- 3. 팀 재정 데이터 처리 ---
        var teamFinances = new List<TeamFinance>();
        var nameToAbbr = teamList.teams.ToDictionary(t => t.team_name, t => t.team_abbv);

        foreach (var teamSalaryPair in teamSalaries)
        {
            string abbr = nameToAbbr.ContainsKey(teamSalaryPair.Key) ? nameToAbbr[teamSalaryPair.Key] : teamSalaryPair.Key;
            teamFinances.Add(new TeamFinance { /* ... TeamFinance 초기화 ... */ });
        }
        db.InsertAll(teamFinances);
        Debug.Log($"[DB] Populated TeamFinance for {teamFinances.Count} teams.");
    }


    #region Public DB Accessors
    // 모든 Public 메서드는 'using' 구문을 사용하여 스레드로부터 안전하게 DB에 접근합니다.

    public void AssignRandomInjuryRiskToAllPlayers()
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            Debug.Log("[DB UTILITY] Assigning random injury risk to all players...");
            var allPlayers = db.Table<PlayerRating>().ToList();
            if (allPlayers.Any(p => p.injury > 0))
            {
                Debug.LogWarning("[DB UTILITY] Injury risk already seems to be assigned. Aborting.");
                return;
            }

            foreach (var player in allPlayers)
            {
                player.injury = UnityEngine.Random.Range(0.01f, 0.101f);
            }
            db.UpdateAll(allPlayers);
            Debug.Log($"[DB UTILITY] Finished assigning injury risk for {allPlayers.Count} players.");
        }
    }

    public User GetUser() 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<User>().FirstOrDefault();
        }
    }

    public void SaveOrUpdateUser(string selectedTeamAbbr, int currentSeason = 2025)
    {
        if (string.IsNullOrEmpty(selectedTeamAbbr))
        {
            Debug.LogWarning("[DB] SaveOrUpdateUser called with empty team abbreviation.");
            return;
        }
        using (var db = new SQLiteConnection(_dbPath))
        {
            User existing = db.Table<User>().FirstOrDefault();
            if (existing == null)
            {
                User newUser = new User
                {
                    SelectedTeamAbbr = selectedTeamAbbr,
                    CurrentSeason = currentSeason,
                    CurrentDate = "2025-10-21"
                };
                db.Insert(newUser);
                Debug.Log("[DB] User record created.");
            }
            else
            {
                existing.SelectedTeamAbbr = selectedTeamAbbr;
                existing.CurrentSeason = currentSeason;
                db.Update(existing);
                Debug.Log("[DB] User record updated (name/team/season).");
            }
        }
    }

    public void AdvanceUserDate()
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            User existing = db.Table<User>().FirstOrDefault();
            if (existing != null)
            {
                if (DateTime.TryParse(existing.CurrentDate, out DateTime currentDate))
                {
                    DateTime nextDate = currentDate.AddDays(1);
                    existing.CurrentDate = nextDate.ToString("yyyy-MM-dd");
                    db.Update(existing);
                    Debug.Log($"[DB] User date advanced to {existing.CurrentDate}.");
                }
                else
                {
                    Debug.LogError($"[DB] Could not parse date: {existing.CurrentDate}");
                }
            }
        }
    }

    public List<Team> GetAllTeams() 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<Team>().ToList();
        }
    }
    
    public Team GetTeam(string teamAbbr) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<Team>().FirstOrDefault(t => t.team_abbv == teamAbbr);
        }
    }
    
    public Team GetTeam(int teamId) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Find<Team>(teamId);
        }
    }

    public TeamFinance GetTeamFinance(string teamAbbr, int season) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<TeamFinance>().FirstOrDefault(f => f.TeamAbbr == teamAbbr && f.Season == season);
        }
    }

    public void ClearScheduleTable() 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            db.DropTable<Schedule>();
            db.CreateTable<Schedule>();
            Debug.Log("[DB] Schedule table cleared and recreated.");
        }
    }

    public void InsertSchedule(List<Schedule> schedule) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            db.InsertAll(schedule);
        }
    }

    public List<Schedule> GetGamesForDate(string date) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<Schedule>().Where(g => g.GameDate == date).ToList();
        }
    }

    public void UpdateGameResult(string gameId, int homeScore, int awayScore)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var game = db.Find<Schedule>(gameId);
            if (game != null)
            {
                game.HomeTeamScore = homeScore;
                game.AwayTeamScore = awayScore;
                game.GameStatus = "Final";
                db.Update(game);
            }
        }
    }

    public void UpdateTeamWinLossRecord(string teamAbbr, bool won, int season)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var teamFinance = db.Table<TeamFinance>().FirstOrDefault(f => f.TeamAbbr == teamAbbr && f.Season == season);
            if (teamFinance != null)
            {
                if (won) {
                    teamFinance.Wins++;
                    teamFinance.TeamBudget += 1000000;
                }
                else {
                    teamFinance.Losses++;
                    teamFinance.TeamBudget -= 1000000;
                }
                db.Update(teamFinance);
            }
        }
    }

    public List<Schedule> GetScheduleForTeam(string teamAbbr, int season)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<Schedule>()
                     .Where(g => (g.HomeTeamAbbr == teamAbbr || g.AwayTeamAbbr == teamAbbr) && g.Season == season)
                     .OrderBy(g => g.GameDate)
                     .ToList();
        }
    }
    
    public List<Schedule> GetScheduleForSeason(int season)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<Schedule>().Where(g => g.Season == season).ToList();
        }
    }

    public PlayerRating GetPlayerRating(int playerId) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Find<PlayerRating>(playerId);
        }
    }

    public List<PlayerRating> GetAllPlayerRatings() 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<PlayerRating>().ToList();
        }
    }

    public List<PlayerRating> GetPlayersByTeam(string teamAbbr)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            Team teamEntity = db.Table<Team>().FirstOrDefault(t => t.team_abbv == teamAbbr || t.team_name == teamAbbr);
            if (teamEntity == null)
            {
                return db.Table<PlayerRating>().Where(p => p.team == teamAbbr).ToList();
            }
            string fullName = teamEntity.team_name;
            string abbr = teamEntity.team_abbv;
            return db.Table<PlayerRating>().Where(p => p.team == fullName || p.team == abbr).ToList();
        }
    }

    public List<PlayerInfo> GetPlayersByTeamWithStatus(string teamAbbr)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var playerInfos = new List<PlayerInfo>();
            // 팀 선수 목록을 가져오는 로직을 인라인으로 처리
            var ratings = GetPlayersByTeam(teamAbbr); // 이 메서드는 이미 using을 사용하므로 호출 가능
            
            foreach (var rating in ratings)
            {
                // Status 정보는 현재 연결을 사용하여 조회
                var status = db.Table<PlayerStatus>().FirstOrDefault(s => s.PlayerId == rating.player_id);
                if (status != null)
                {
                    playerInfos.Add(new PlayerInfo { Rating = rating, Status = status });
                }
            }
            return playerInfos;
        }
    }

    public List<PlayerRating> GetFreeAgents()
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<PlayerRating>().Where(p => p.team == "FA").ToList();
        }
    }

    public PlayerStatus GetPlayerStatus(int playerId)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<PlayerStatus>().FirstOrDefault(s => s.PlayerId == playerId);
        }
    }

    public void InsertPlayerStats(List<PlayerStat> stats) 
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            db.InsertAll(stats);
        }
    }

    public void UpdatePlayerAfterGame(int playerId, int staminaUsed, bool isInjured, int injuryDays)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var status = db.Table<PlayerStatus>().FirstOrDefault(s => s.PlayerId == playerId);
            if (status == null) return;

            status.Stamina -= staminaUsed;
            if (status.Stamina < 0) status.Stamina = 0;

            if (isInjured)
            {
                status.IsInjured = true;
                if (injuryDays > status.InjuryDaysLeft)
                {
                    status.InjuryDaysLeft = injuryDays;
                }
            }
            db.Update(status);
        }
    }
    
    public void UpdateAllPlayerStatusForNewDay()
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var allStatuses = db.Table<PlayerStatus>().ToList();
            foreach (var status in allStatuses)
            {
                status.Stamina += 15;
                if (status.Stamina > 100) status.Stamina = 100;

                if (status.IsInjured)
                {
                    status.InjuryDaysLeft--;
                    if (status.InjuryDaysLeft <= 0)
                    {
                        status.IsInjured = false;
                        status.InjuryDaysLeft = 0;
                    }
                }
            }
            db.UpdateAll(allStatuses);
            Debug.Log($"[DB] Advanced day for {allStatuses.Count} player statuses (Stamina/Injury recovery).");
        }
    }

    public void ReleasePlayer(int playerId)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var rating = db.Find<PlayerRating>(playerId);
            if (rating != null)
            {
                rating.team = "FA";
                db.Update(rating);
            }
        }
    }

    public void RecalculateAndSaveAllTeamSalaries()
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var allTeams = db.Table<Team>().ToList();
            var allRatings = db.Table<PlayerRating>().ToList();
            var allStatuses = db.Table<PlayerStatus>().ToList();

            foreach (var team in allTeams)
            {
                var teamPlayers = allRatings.Where(p => p.team == team.team_abbv || p.team == team.team_name);
                long totalAnnualSalary = 0;

                foreach (var player in teamPlayers)
                {
                    var status = allStatuses.FirstOrDefault(s => s.PlayerId == player.player_id);
                    if (status != null && status.YearsLeft > 0)
                    {
                        totalAnnualSalary += status.Salary / status.YearsLeft;
                    }
                }
                
                var teamFinance = db.Table<TeamFinance>().FirstOrDefault(f => f.TeamAbbr == team.team_abbv && f.Season == 2025);
                if (teamFinance != null)
                {
                    teamFinance.CurrentTeamSalary = totalAnnualSalary;
                    db.Update(teamFinance);
                }
            }
            Debug.Log("[DB] All team salaries have been recalculated and updated.");
        }
    }

    public List<TeamFinance> GetTeamFinancesForSeason(int season)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            return db.Table<TeamFinance>().Where(t => t.Season == season).ToList();
        }
    }

    public void UpdatePlayerTeam(List<int> playerIds, string newTeamAbbr)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            foreach (int playerId in playerIds)
            {
                var player = db.Find<PlayerRating>(p => p.player_id == playerId);
                if (player != null)
                {
                    player.team = newTeamAbbr;
                    db.Update(player);
                }
            }
        }
    }

    public void UpdateTeamFinance(TeamFinance finance)
    {
        if (finance == null) return;
        using (var db = new SQLiteConnection(_dbPath))
        {
            db.Update(finance);
        }
    }

    public void AssignContractToPlayer(int playerId)
    {
        using (var db = new SQLiteConnection(_dbPath))
        {
            var rating = db.Find<PlayerRating>(playerId);
            var status = db.Table<PlayerStatus>().FirstOrDefault(s => s.PlayerId == playerId);

            if (rating == null || status == null)
            {
                Debug.LogError($"[DB] AssignContractToPlayer: Cannot find player with ID {playerId}");
                return;
            }

            // 계약 할당 로직 (필요 시 수정)
            db.Update(status);
        }
    }
    
    public void UpdateBestFive(string teamAbbr, List<int> starterIds)
    {
        if (starterIds == null || starterIds.Count > 5)
        {
            Debug.LogError($"[DB] UpdateBestFive: Invalid starterIds list for team {teamAbbr}.");
            return;
        }

        string bestFiveStr = string.Join(",", starterIds);
        
        using (var db = new SQLiteConnection(_dbPath))
        {
            var command = db.CreateCommand("UPDATE Team SET best_five = ? WHERE team_abbv = ?", bestFiveStr, teamAbbr);
            int result = command.ExecuteNonQuery();

            if (result > 0)
            {
                Debug.Log($"[DB] Successfully updated best_five for team {teamAbbr}.");
            }
            else
            {
                Debug.LogWarning($"[DB] UpdateBestFive: Team with abbreviation '{teamAbbr}' not found.");
            }
        }
    }

    #endregion

    #region Value Calculation & JSON Helpers (No DB Access)
    
    private float CalculatePlayerCurrentValue(int overall, int age, int potential, long salary)
    {
        int factor = 53;
        float performanceValue = Mathf.Pow((overall - factor), 3f);
        float potentialValue = Mathf.Pow((potential - factor), 1.7f) * 20f;
        float ageFactor;
        if (age >= 30)
            ageFactor = (40 - age) / 10f;
        else
            ageFactor = (float)(3.0 * Math.Exp(-Math.Log(3.0) * (age - 20.0) / 10.0));
        float postValue = potentialValue * ageFactor;
        long marketValue = ConvertValueToMarketSalary(performanceValue + postValue);
        float contractConstant = 0.2f;
        float contractValue = (marketValue - salary) * contractConstant;
        return marketValue + contractValue;
    }

    public long ConvertValueToMarketSalary(float currentValue)
    {
        const float VALUE_MIN = 5000f;
        const float VALUE_MAX = 100000f;
        const long SALARY_MIN = 2000000;
        const long SALARY_MAX = 60000000;
        float x = currentValue;
        float a = (float)(SALARY_MAX - SALARY_MIN) / (VALUE_MAX * VALUE_MAX - VALUE_MIN * VALUE_MIN);
        long salary = (long)(a * x * x + SALARY_MIN - a * VALUE_MIN * VALUE_MIN);
        return salary;
    }
    #endregion
    
    // [제거] OnDestroy 메서드는 더 이상 필요 없습니다.
    // void OnDestroy() { ... }

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