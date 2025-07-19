using UnityEngine;
using System.IO;
using System.Linq;
using SQLite4Unity3d;
using System;

public class LocalDbManager : MonoBehaviour
{
    private SQLiteConnection _db;
    private string _dbPath;

    [Table("PlayerStat")]
    public class PlayerStat
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    // PlayerRecord 테이블 정의 예시 (없으면 에러)
    [Table("PlayerRecord")]
    public class PlayerRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public string Note { get; set; }
    }

    [Table("PlayerRating")]
    public class PlayerRating
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public float Overall { get; set; }
        public float Offense { get; set; }
        public float Defense { get; set; }
        public float Potential { get; set; }
        public string LastUpdated { get; set; }
    }

    [Table("PlayerStatus")]
    public class PlayerStatus
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public string ContractType { get; set; }
        public int YearsLeft { get; set; }
        public int Stamina { get; set; }
        public bool IsInjured { get; set; }
        public int InjuryDaysLeft { get; set; }
        public string LastChecked { get; set; }
    }

    void Awake()
    {
        _dbPath = Path.Combine(Application.persistentDataPath, "game_records.db");
        bool firstRun = !File.Exists(_dbPath);

        try
        {
            _db = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

            if (firstRun)
            {
                _db.CreateTable<PlayerRecord>();
                _db.CreateTable<PlayerStat>();
                _db.CreateTable<PlayerRating>();
                _db.CreateTable<PlayerStatus>();
                Debug.Log("[DB] Created tables for PlayerRecord, PlayerStat, PlayerRating, PlayerStatus");

                // JSON 파싱
                var ta = Resources.Load<TextAsset>("players"); // Resources/players.json
                if (ta == null)
                {
                    Debug.LogError("[DB] players.json not found in Resources!");
                    return;
                }
                var list = JsonUtility.FromJson<PlayerJsonList>(ta.text);
                if (list == null || list.players == null)
                {
                    Debug.LogError("[DB] players.json parse error!");
                    return;
                }
                foreach (var p in list.players)
                {
                    // 중복 방지: 이미 있으면 Insert 안 함
                    if (_db.Table<PlayerRating>().FirstOrDefault(x => x.PlayerId == p.id) == null)
                    {
                        _db.Insert(new PlayerRating
                        {
                            PlayerId = p.id,
                            Overall = p.overall,
                            Offense = p.offense,
                            Defense = p.defense,
                            Potential = p.potential,
                            LastUpdated = DateTime.UtcNow.ToString("s")
                        });
                    }
                    if (_db.Table<PlayerStatus>().FirstOrDefault(x => x.PlayerId == p.id) == null)
                    {
                        _db.Insert(new PlayerStatus
                        {
                            PlayerId = p.id,
                            ContractType = p.contractType ?? "FreeAgent",
                            YearsLeft = p.contractYears,
                            Stamina = 100,
                            IsInjured = false,
                            InjuryDaysLeft = 0,
                            LastChecked = DateTime.UtcNow.ToString("s")
                        });
                    }
                }
            }
            Debug.Log($"[DB] Initialized at {_dbPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[DB] Initialization failed: " + ex);
        }
    }

    void OnDestroy()
    {
        if (_db != null)
        {
            _db.Close();
            _db.Dispose();
            Debug.Log("[DB] Closed connection");
        }
    }

    [System.Serializable]
    public class PlayerJson
    {
        public int id;
        public float overall, offense, defense, potential;
        public string contractType;
        public int contractYears;
    }
    [System.Serializable]
    public class PlayerJsonList { public PlayerJson[] players; }
}
