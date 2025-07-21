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
            _db.CreateTable<PlayerRating>();
            _db.CreateTable<PlayerStatus>();
            _db.CreateTable<PlayerStat>();
            _db.CreateTable<User>();
            _db.CreateTable<TeamFinance>();
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
        TextAsset ta = Resources.Load<TextAsset>("players");
        if (ta == null) { Debug.LogError("[DB] players.json not found!"); return; }

        PlayerMasterDataList masterList = JsonUtility.FromJson<PlayerMasterDataList>(ta.text);
        if (masterList == null || masterList.players == null) { Debug.LogError("[DB] Failed to parse players.json."); return; }

        var ratings = new List<PlayerRating>();
        var statuses = new List<PlayerStatus>();
        var teamSalaries = new Dictionary<string, long>();

        foreach (var p in masterList.players)
        {
            // [수정됨] 선수의 현재 가치를 계산
            float currentValue = CalculatePlayerCurrentValue(p.overallAttribute, p.age, p.contract_value);

            ratings.Add(new PlayerRating
            {
                player_id = p.player_id,
                name = p.name,
                team = p.team,
                age = p.age,
                position = p.position,
                backNumber = p.backnumber,
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

            statuses.Add(new PlayerStatus
            {
                PlayerId = p.player_id,
                ContractType = "Standard",
                YearsLeft = p.contract_years_left,
                Salary = p.contract_value,
                Stamina = 100,
                IsInjured = false,
                InjuryDaysLeft = 0,
                LastChecked = DateTime.UtcNow.ToString("s")
            });

            if (!teamSalaries.ContainsKey(p.team))
            {
                teamSalaries[p.team] = 0;
            }
            teamSalaries[p.team] += p.contract_value;
        }
        _db.InsertAll(ratings);
        _db.InsertAll(statuses);
        Debug.Log($"[DB] Populated PlayerRating and PlayerStatus with {masterList.players.Length} players.");

        var teamFinances = new List<TeamFinance>();
        long currentSeasonSalaryCap = 141000000;
        long currentSeasonLuxuryTax = 171315000;

        foreach (var teamSalaryPair in teamSalaries)
        {
            teamFinances.Add(new TeamFinance
            {
                TeamAbbr = teamSalaryPair.Key,
                Season = 2025,
                SalaryCap = currentSeasonSalaryCap,
                LuxuryTaxThreshold = currentSeasonLuxuryTax,
                CurrentTeamSalary = teamSalaryPair.Value,
                TeamBudget = 200000000
            });
        }
        _db.InsertAll(teamFinances);
        Debug.Log($"[DB] Populated TeamFinance for {teamFinances.Count} teams.");
    }

    #region Value Calculation Helpers
    // [신규] 선수의 현재 가치를 계산하는 헬퍼 함수
    private float CalculatePlayerCurrentValue(int overall, int age, long salary)
    {
        // 1. OVR 기반 기본 가치 (OVR 1점당 10점)
        float value = overall * 10.0f;

        // 2. 나이 페널티 적용 (28세를 기준으로 1살 많아질 때마다 7.5점씩 감점)
        float agePenalty = Mathf.Max(0, age - 28) * 7.5f;
        value -= agePenalty;

        // 3. 계약 효율성 보너스/페널티 (연봉과 시장가치의 차이를 점수화)
        long marketValue = GetMarketValueByOverall(overall);
        // (시장가치 - 실제 연봉)을 100만 달러당 1점으로 환산하여 가감
        float contractBonus = (float)(marketValue - salary) / 1000000;
        value += contractBonus;

        return Mathf.Max(1.0f, value); // 가치가 음수가 되지 않도록 최소 1점으로 보정
    }

    // [신규] OVR에 따른 선수의 시장 가치(적정 연봉)를 반환하는 헬퍼 함수
    private long GetMarketValueByOverall(int overall)
    {
        if (overall >= 95) return 50000000;  // 슈퍼맥스급
        if (overall >= 90) return 40000000;  // 맥스급
        if (overall >= 85) return 30000000;  // 올스타급
        if (overall >= 80) return 20000000;  // 준수한 주전급
        if (overall >= 75) return 12000000;  // 주전/상위 벤치
        if (overall >= 70) return 5000000;   // 벤치 롤플레이어
        return 2000000;                      // 최저 연봉급
    }
    #endregion

    public List<PlayerRating> GetAllPlayerRatings()
    {
        return _db.Table<PlayerRating>().ToList();
    }

    public PlayerStatus GetPlayerStatus(int playerId)
    {
        return _db.Table<PlayerStatus>().FirstOrDefault(p => p.PlayerId == playerId);
    }

    void OnDestroy()
    {
        if (_db != null)
        {
            _db.Close();
        }
    }

    // JSON 파싱을 위한 도우미 클래스들
    [Serializable]
    public class PlayerMasterData
    {
        public int player_id;
        public string name;
        public string team;
        public int age;
        public int position;
        public int backnumber;
        public string height;
        public int weight;
        public int contract_years_left;
        public long contract_value;
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
    }

    [Serializable]
    public class PlayerMasterDataList
    {
        public PlayerMasterData[] players;
    }
}