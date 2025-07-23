using System.Collections.Generic;
using madcamp3.Assets.Script.Player;

public class TeamData
{
    public int teamId;
    public string teamName;
    public string abbreviation;
    public List<PlayerLine> players;
    public string teamColor;

    public TeamData(int id, string name, string abbr, List<PlayerLine> playerLines, string color)
    {
        teamId = id;
        teamName = name;
        abbreviation = abbr;
        players = playerLines;
        teamColor = color;
    }
}
