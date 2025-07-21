using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ScheduleCell : MonoBehaviour
{
    // 홈 & 원정 로고
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("myLogoImage")] private Image homeLogoImage;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("oppLogoImage")] private Image awayLogoImage;

    // 약어 텍스트
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("myAbbrText")] private TMP_Text homeAbbrText;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("oppAbbrText")] private TMP_Text awayAbbrText;

    // 순위 텍스트 (1st, 2nd ...)
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("myRankText")] private TMP_Text homeRankText;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("oppRankText")] private TMP_Text awayRankText;

    // 승/패 텍스트
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("myWinsText")] private TMP_Text homeWinsText;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("myLossesText")] private TMP_Text homeLossesText;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("oppWinsText")] private TMP_Text awayWinsText;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("oppLossesText")] private TMP_Text awayLossesText;

    private int _season;

    public void Configure(Schedule game, int season)
    {
        _season = season;

        string homeAbbr = game.HomeTeamAbbr;
        string awayAbbr = game.AwayTeamAbbr;

        // 로고 및 텍스트 세팅
        SetLogoAndTexts(homeLogoImage, homeAbbrText, homeAbbr);
        SetLogoAndTexts(awayLogoImage, awayAbbrText, awayAbbr);

        // 기록 및 순위
        var homeFinance = LocalDbManager.Instance.GetTeamFinance(homeAbbr, season);
        var awayFinance = LocalDbManager.Instance.GetTeamFinance(awayAbbr, season);

        int homeWins = homeFinance?.Wins ?? 0;
        int homeLosses = homeFinance?.Losses ?? 0;
        int awayWins = awayFinance?.Wins ?? 0;
        int awayLosses = awayFinance?.Losses ?? 0;

        homeWinsText.text = homeWins.ToString();
        homeLossesText.text = homeLosses.ToString();
        awayWinsText.text = awayWins.ToString();
        awayLossesText.text = awayLosses.ToString();

        int homeRank = GetRank(homeAbbr, season);
        int awayRank = GetRank(awayAbbr, season);
        homeRankText.text = ToOrdinal(homeRank);
        awayRankText.text = ToOrdinal(awayRank);
    }

    private void SetLogoAndTexts(Image img, TMP_Text abbrText, string teamAbbr)
    {
        img.sprite = Resources.Load<Sprite>($"team_photos/{teamAbbr}");
        abbrText.text = teamAbbr;
    }

    private int GetRank(string teamAbbr, int season)
    {
        var allTeams = LocalDbManager.Instance.GetAllTeams();
        var standings = new System.Collections.Generic.List<(string abbr, int wins, int losses)>();
        foreach (var t in allTeams)
        {
            var tf = LocalDbManager.Instance.GetTeamFinance(t.team_abbv, season);
            if (tf != null) standings.Add((t.team_abbv, tf.Wins, tf.Losses));
        }
        standings.Sort((a, b) => {
            int cmp = b.wins.CompareTo(a.wins);
            if (cmp != 0) return cmp;
            return a.losses.CompareTo(b.losses);
        });
        int rankIdx = standings.FindIndex(s => s.abbr == teamAbbr);
        return rankIdx >= 0 ? rankIdx + 1 : standings.Count;
    }

    private string ToOrdinal(int num)
    {
        if (num <= 0) return num.ToString();
        if ((num % 100) >= 11 && (num % 100) <= 13) return num + "th";
        switch (num % 10)
        {
            case 1: return num + "st";
            case 2: return num + "nd";
            case 3: return num + "rd";
            default: return num + "th";
        }
    }
} 