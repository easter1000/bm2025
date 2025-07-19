using System;

[Serializable]
public class PlayerMasterData
{
    // JSON 파일의 필드명과 정확히 일치해야 합니다.
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
}

[Serializable]
public class PlayerMasterDataList
{
    public PlayerMasterData[] players;
}