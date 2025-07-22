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
                    GameObject obj = new GameObject("LocalDbManager");
                    _instance = obj.AddComponent<LocalDbManager>();
                }
            }
            return _instance;
        }
    }
    
    private SQLiteConnection _db;
    private string _dbPath;

    // [수정] 데이터베이스 연결에 안전하게 접근하기 위한 속성
    private SQLiteConnection Connection
    {
        get
        {
            if (_db == null)
            {
                InitializeDatabase();
            }
            return _db;
        }
    }

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
        if (_db != null) return; // [수정] 중복 초기화 방지

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
            float currentValue = CalculatePlayerCurrentValue(p.overallAttribute, p.age, p.potential, (long)(p.contract_value / p.contract_years_left));
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
            
            // [수정] 팀 연봉 총액을 '연봉' 기준으로 계산
            long annualSalary = 0;
            if (p.contract_years_left > 0)
            {
                annualSalary = p.contract_value / p.contract_years_left;
            }
            else
            {
                Debug.LogWarning($"Player {p.name} (ID: {p.player_id}) has 0 contract years left. Annual salary is calculated as 0.");
            }

            if (!teamSalaries.ContainsKey(p.team)) teamSalaries[p.team] = 0;
            teamSalaries[p.team] += annualSalary;
        }
        Connection.InsertAll(ratings); Connection.InsertAll(statuses);
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
        Connection.InsertAll(teams);
        Debug.Log($"[DB] Populated Team with {teams.Count} teams and calculated best five.");

        // --- 3. 팀 재정 데이터 처리 ---
        var teamFinances = new List<TeamFinance>();
        var nameToAbbr = teamList.teams.ToDictionary(t => t.team_name, t => t.team_abbv);

        foreach (var teamSalaryPair in teamSalaries)
        {
            // 키가 팀 전체 이름일 경우 약어로 변환, 이미 약어일 경우 그대로 사용
            string abbr = nameToAbbr.ContainsKey(teamSalaryPair.Key) ? nameToAbbr[teamSalaryPair.Key] : teamSalaryPair.Key;

            teamFinances.Add(new TeamFinance 
            { 
                TeamAbbr = abbr, 
                Season = 2025, 
                Standing = 0, // 초기 등수
                Wins = 0, 
                Losses = 0,
                CurrentTeamSalary = teamSalaryPair.Value, 
                TeamBudget = 200000000 
            });
        }
        Connection.InsertAll(teamFinances);
        Debug.Log($"[DB] Populated TeamFinance for {teamFinances.Count} teams.");
    }

    #region Public DB Accessors
    
    // --- User ---
    public User GetUser() => Connection.Table<User>().FirstOrDefault();

    // --- NEW: Save or update the current user information ---
    public void SaveOrUpdateUser(string coachName, string selectedTeamAbbr, int currentSeason = 2025)
    {
        if (string.IsNullOrEmpty(selectedTeamAbbr))
        {
            Debug.LogWarning("[DB] SaveOrUpdateUser called with empty team abbreviation.");
            return;
        }
        User existing = GetUser();
        if (existing == null)
        {
            string today = "2025-10-21"; // 최초 생성 시에만 시작 날짜 설정
            User newUser = new User
            {
                CoachName = coachName,
                SelectedTeamAbbr = selectedTeamAbbr,
                CurrentSeason = currentSeason,
                CurrentDate = today
            };
            Connection.Insert(newUser);
            Debug.Log("[DB] User record created.");
        }
        else
        {
            // 기존 유저 정보 업데이트 시, 날짜는 수정하지 않음
            existing.CoachName = coachName;
            existing.SelectedTeamAbbr = selectedTeamAbbr;
            existing.CurrentSeason = currentSeason;
            Connection.Update(existing);
            Debug.Log("[DB] User record updated (name/team/season).");
        }
    }

    /// <summary>
    /// 현재 유저의 날짜를 하루 뒤로 업데이트합니다.
    /// </summary>
    public void AdvanceUserDate()
    {
        User existing = GetUser();
        if (existing != null)
        {
            if (DateTime.TryParse(existing.CurrentDate, out DateTime currentDate))
            {
                DateTime nextDate = currentDate.AddDays(1);
                existing.CurrentDate = nextDate.ToString("yyyy-MM-dd");
                Connection.Update(existing);
                Debug.Log($"[DB] User date advanced to {existing.CurrentDate}.");
            }
            else
            {
                Debug.LogError($"[DB] Could not parse date: {existing.CurrentDate}");
            }
        }
    }

    // --- Team & Schedule ---
    public List<Team> GetAllTeams() => Connection.Table<Team>().ToList();
    public Team GetTeam(string teamAbbr) => Connection.Table<Team>().FirstOrDefault(t => t.team_abbv == teamAbbr);
    public Team GetTeam(int teamId) => Connection.Find<Team>(teamId);
    public TeamFinance GetTeamFinance(string teamAbbr, int season) => Connection.Table<TeamFinance>().FirstOrDefault(f => f.TeamAbbr == teamAbbr && f.Season == season);
    public void ClearScheduleTable() { Connection.DropTable<Schedule>(); Connection.CreateTable<Schedule>(); Debug.Log("[DB] Schedule table cleared and recreated."); }
    public void InsertSchedule(List<Schedule> schedule) => Connection.InsertAll(schedule);
    public List<Schedule> GetGamesForDate(string date) => Connection.Table<Schedule>().Where(g => g.GameDate == date && g.GameStatus == "Scheduled").ToList();
    public void UpdateGameResult(string gameId, int homeScore, int awayScore)
    {
        var game = Connection.Find<Schedule>(gameId);
        if (game != null) { game.HomeTeamScore = homeScore; game.AwayTeamScore = awayScore; game.GameStatus = "Final"; Connection.Update(game); }
    }
    public void UpdateTeamWinLossRecord(string teamAbbr, bool won, int season)
    {
        var teamFinance = GetTeamFinance(teamAbbr, season);
        if (teamFinance != null)
        {
            if (won) teamFinance.Wins++;
            else teamFinance.Losses++;
            Connection.Update(teamFinance);
        }
    }
    public List<Schedule> GetScheduleForTeam(string teamAbbr, int season)
    {
        // 특정 팀의 한 시즌 전체 경기 일정을 가져옵니다.
        return Connection.Table<Schedule>()
                .Where(g => (g.HomeTeamAbbr == teamAbbr || g.AwayTeamAbbr == teamAbbr) && g.Season == season)
                .OrderBy(g => g.GameDate)
                .ToList();
    }
    // --- Player ---
    public PlayerRating GetPlayerRating(int playerId) => Connection.Find<PlayerRating>(playerId);
    public List<PlayerRating> GetAllPlayerRatings() => Connection.Table<PlayerRating>().ToList();
    // 팀 약어(예: "DAL") 또는 전체 팀명("Dallas Mavericks") 어느 쪽이든 받아서 해당 팀 선수 목록을 반환합니다.
    public List<PlayerRating> GetPlayersByTeam(string teamIdentifier)
    {
        // 먼저 Team 테이블에서 매칭되는 엔티티를 찾아 전체 이름과 약어를 모두 확보합니다.
        Team teamEntity = Connection.Table<Team>().FirstOrDefault(t => t.team_abbv == teamIdentifier || t.team_name == teamIdentifier);

        if (teamEntity == null)
        {
            // Team 테이블에 없으면 식별자를 그대로 사용하여 검색합니다.
            return Connection.Table<PlayerRating>().Where(p => p.team == teamIdentifier).ToList();
        }

        string fullName = teamEntity.team_name;
        string abbr = teamEntity.team_abbv;

        // PlayerRating.team 컬럼은 현재 전체 팀명을 저장하고 있으므로 우선 전체 이름으로 검색한 뒤,
        // 혹시라도 약어로 저장된 데이터가 있을 가능성까지 대비해 약어도 함께 포함합니다.
        return Connection.Table<PlayerRating>()
                 .Where(p => p.team == fullName || p.team == abbr)
                 .ToList();
    }

    public PlayerStatus GetPlayerStatus(int playerId)
    {
        return Connection.Table<PlayerStatus>().FirstOrDefault(s => s.PlayerId == playerId);
    }

    public void InsertPlayerStats(List<PlayerStat> stats) => Connection.InsertAll(stats);

    /// <summary>
    /// 특정 선수를 DB에서 방출하여 FA(자유 계약) 상태로 만듭니다.
    /// </summary>
    public void ReleasePlayer(int playerId)
    {
        var rating = GetPlayerRating(playerId);
        if (rating != null)
        {
            rating.team = "FA"; // Free Agent
            Connection.Update(rating);
        }

        var status = GetPlayerStatus(playerId);
        if (status != null)
        {
            status.Salary = 0;
            status.YearsLeft = 0;
            Connection.Update(status);
        }
    }

    /// <summary>
    /// 모든 팀의 현재 로스터를 기반으로 연봉 총액을 다시 계산하여 DB에 저장합니다.
    /// </summary>
    public void RecalculateAndSaveAllTeamSalaries()
    {
        var allTeams = GetAllTeams();
        var allRatings = GetAllPlayerRatings();
        var allStatuses = Connection.Table<PlayerStatus>().ToList();

        foreach (var team in allTeams)
        {
            var teamPlayers = allRatings.Where(p => p.team == team.team_abbv);
            long totalAnnualSalary = 0;

            foreach (var player in teamPlayers)
            {
                var status = allStatuses.FirstOrDefault(s => s.PlayerId == player.player_id);
                if (status != null && status.YearsLeft > 0)
                {
                    totalAnnualSalary += status.Salary / status.YearsLeft;
                }
            }

            var teamFinance = GetTeamFinance(team.team_abbv, 2025);
            if (teamFinance != null)
            {
                teamFinance.CurrentTeamSalary = totalAnnualSalary;
                Connection.Update(teamFinance);
            }
        }
        Debug.Log("[DB] All team salaries have been recalculated and updated.");
    }

    public List<TeamFinance> GetTeamFinancesForSeason(int season)
    {
        return Connection.Table<TeamFinance>().Where(t => t.Season == season).ToList();
    }

    public void UpdatePlayerTeam(List<int> playerIds, string newTeamAbbr)
    {
        foreach (int playerId in playerIds)
        {
            var player = Connection.Find<PlayerRating>(p => p.player_id == playerId);
            if (player != null)
            {
                player.team = newTeamAbbr;
                Connection.Update(player);
            }
        }
    }

    public void UpdateTeamFinance(TeamFinance finance)
    {
        if (finance == null) return;
        Connection.Update(finance);
    }

    #endregion

    #region Value Calculation Helpers
    
    // [핵심 수정] 새로운 선수 가치 평가 시스템
    private float CalculatePlayerCurrentValue(int overall, int age, int potential, long salary)
    {
        int factor = 53;

        // 1. 성과 가치 (Performance Value): OVR이 높을수록 기하급수적으로 증가
        float performanceValue = Mathf.Pow((overall - factor), 3f);

        // 2. 미래 가치 (Post Value): 나이가 어리고 잠재력이 높을수록 증가
        float potentialValue = Mathf.Pow((potential - factor), 1.7f) * 20f;
        // age가 30일 때 1, 30보다 크면 0, 30보다 작으면 천천히 증가 (예: exp 곡선)
        float ageFactor;
        if (age >= 30)
            ageFactor = (40 - age) / 10f;
        else
            ageFactor = (float)(3.0 * Math.Exp(-Math.Log(3.0) * (age - 20.0) / 10.0));

        // age가 30일 때 1, 30보다 작으면 1보다 조금씩 커짐, 30보다 크면 0
        float postValue = potentialValue * ageFactor;

        // 3. 계약 가치 (Contract Value): 적정 연봉 대비 실제 연봉이 얼마나 저렴한가
        long marketValue = ConvertValueToMarketSalary(performanceValue + postValue);

        float contractConstant = 0.2f;
        float contractValue = (marketValue - salary) * contractConstant;

        return marketValue + contractValue;
    }

    /// <summary>
    /// [신규] 선수의 현재 가치(currentValue)를 시장가(연봉)로 변환하는 '번역기' 함수
    /// </summary>
    public long ConvertValueToMarketSalary(float currentValue)
    {
        // 기준이 되는 가치와 연봉 범위
        const float VALUE_MIN = 5000f;
        const float VALUE_MAX = 100000f;
        const long SALARY_MIN = 2000000;
        const long SALARY_MAX = 60000000;

        // VALUE_MIN일 때 SALARY_MIN, VALUE_MAX일 때 SALARY_MAX가 나오도록 2차함수로 변환합니다.
        float x = currentValue;
        float a = (float)(SALARY_MAX - SALARY_MIN) / (VALUE_MAX * VALUE_MAX - VALUE_MIN * VALUE_MIN);
        long salary = (long)(a * x * x + SALARY_MIN - a * VALUE_MIN * VALUE_MIN);
        return salary;
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