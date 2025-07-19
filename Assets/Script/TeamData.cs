using System;

[Serializable]
public class TeamData
{
    public int teamId;        // 고유 팀 ID
    public string teamName;
    public string[] players; // 선수 명단
    public int currentRank;
    public float winRate;

    public TeamData(int id, string name, string[] players, int rank, float winRate)
    {
        this.teamId = id;
        this.teamName = name;
        this.players = players;
        this.currentRank = rank;
        this.winRate = winRate;
    }
}