using System;
using madcamp3.Assets.Script.Player;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TeamData
{
    public int teamId;
    public string teamName;
    public List<PlayerLine> playerLines;
    public string abbreviation;
    public Color teamColor;

    public TeamData(int id, string name, string abbreviation, List<PlayerLine> playersDetailed, String color)
    {
        this.teamId = id;
        this.teamName = name;
        this.abbreviation = abbreviation;
        this.playerLines = playersDetailed;
        this.teamColor = Color.black;
        Color tempColor;
        if (ColorUtility.TryParseHtmlString(color, out tempColor))
        {
            this.teamColor = tempColor;
        }
    }
}