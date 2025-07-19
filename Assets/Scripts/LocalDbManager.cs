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
            _db.CreateTable<PlayerStat>();
            _db.CreateTable<PlayerRating>();
            _db.CreateTable<PlayerStatus>();
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

        foreach (var p in masterList.players)
        {
            ratings.Add(new PlayerRating
            {
                player_id = p.player_id, name = p.name, team = p.team, position = p.position,
                overallAttribute = p.overallAttribute, closeShot = p.closeShot, midRangeShot = p.midRangeShot,
                threePointShot = p.threePointShot, freeThrow = p.freeThrow, layup = p.layup,
                drivingDunk = p.drivingDunk, drawFoul = p.drawFoul, interiorDefense = p.interiorDefense,
                perimeterDefense = p.perimeterDefense, steal = p.steal, block = p.block, speed = p.speed,
                stamina = p.stamina, passIQ = p.passIQ, ballHandle = p.ballHandle,
                offensiveRebound = p.offensiveRebound, defensiveRebound = p.defensiveRebound, potential = p.potential
            });
            
            statuses.Add(new PlayerStatus
            {
                PlayerId = p.player_id, ContractType = "Standard", YearsLeft = 3, Stamina = 100,
                IsInjured = false, InjuryDaysLeft = 0, LastChecked = DateTime.UtcNow.ToString("s")
            });
        }
        _db.InsertAll(ratings);
        _db.InsertAll(statuses);
        Debug.Log($"[DB] Populated database with {masterList.players.Length} players.");
    }

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
}