using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayLineUI : MonoBehaviour
{
    [Header("Home Team")]
    [SerializeField] private TextMeshProUGUI homeRankText;
    [SerializeField] private Image homeLogo;
    [SerializeField] private TextMeshProUGUI homeAbbrText;
    [SerializeField] private TextMeshProUGUI homeWinLoseText;

    [Header("Away Team")]
    [SerializeField] private TextMeshProUGUI awayRankText;
    [SerializeField] private Image awayLogo;
    [SerializeField] private TextMeshProUGUI awayAbbrText;
    [SerializeField] private TextMeshProUGUI awayWinLoseText;

    [Header("Score")] [SerializeField] private TextMeshProUGUI scoreText;

    public void Setup(int homeRank,int awayRank,Team homeTeam,Team awayTeam,int homeScore,int awayScore)
    {
        if (homeRankText) homeRankText.text = GetRankString(homeRank);
        if (awayRankText) awayRankText.text = GetRankString(awayRank);

        if (homeAbbrText) homeAbbrText.text = homeTeam.team_abbv;
        if (awayAbbrText) awayAbbrText.text = awayTeam.team_abbv;

        if (homeLogo)
        {
            var logo = Resources.Load<Sprite>($"team_photos/{homeTeam.team_abbv.ToLower()}") ?? Resources.Load<Sprite>("team_photos/default_logo");
            homeLogo.sprite = logo;
        }
        if (awayLogo)
        {
            var logo = Resources.Load<Sprite>($"team_photos/{awayTeam.team_abbv.ToLower()}") ?? Resources.Load<Sprite>("team_photos/default_logo");
            awayLogo.sprite = logo;
        }

        bool homeWin = homeScore>awayScore;
        if (homeWinLoseText)
        {
            homeWinLoseText.text = homeWin ? "WIN" : "LOSE";
            homeWinLoseText.color = homeWin ? Color.green : Color.red;
        }
        if (awayWinLoseText)
        {
            awayWinLoseText.text = homeWin ? "LOSE" : "WIN";
            awayWinLoseText.color = homeWin ? Color.red : Color.green;
        }
        if (scoreText) scoreText.text = $"{homeScore}:{awayScore}";
    }

    private string GetRankString(int rank)
    {
        if (rank <= 0) return rank.ToString();

        switch (rank % 100)
        {
            case 11:
            case 12:
            case 13:
                return rank + "th";
        }

        switch (rank % 10)
        {
            case 1:
                return rank + "st";
            case 2:
                return rank + "nd";
            case 3:
                return rank + "rd";
            default:
                return rank + "th";
        }
    }
} 