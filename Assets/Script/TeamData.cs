using System;
using madcamp3.Assets.Script.Player;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class TeamData
{
    public int teamId;
    public string teamName;
    public List<PlayerLine> playerLines;
    public string abbreviation;

    public TeamData(int id, string name, string abbreviation, List<PlayerLine> playersDetailed)
    {
        this.teamId = id;
        this.teamName = name;
        this.abbreviation = abbreviation;
        this.playerLines = playersDetailed;
    }
}