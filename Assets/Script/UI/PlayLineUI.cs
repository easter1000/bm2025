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
        if (homeRankText) homeRankText.text = homeRank.ToString();
        if (awayRankText) awayRankText.text = awayRank.ToString();

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
} 