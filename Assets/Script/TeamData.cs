using System;
using madcamp3.Assets.Script.Player;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class TeamData
{
    public int teamId;        // 고유 팀 ID
    public string teamName;
    public string[] players; // 선수 명단
    public List<PlayerLine> playerLines; // 상세 선수 정보 (15명)
    public string abbreviation; // 팀 줄임말(최대 3글자)

    public TeamData(int id, string name, string abbreviation, List<PlayerLine> playersDetailed)
    {
        this.teamId = id;
        this.teamName = name;
        this.abbreviation = abbreviation;
        this.playerLines = playersDetailed;

        // 문자열 배열도 기존 코드 호환을 위해 채워둔다.
        this.players = playersDetailed.Select(p => p.PlayerName).ToArray();
    }
}