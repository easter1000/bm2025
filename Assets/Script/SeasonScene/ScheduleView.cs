using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using System.Globalization;

public class ScheduleView : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private ScheduleCell scheduleCellPrefab;
    [SerializeField] private TMP_Text dateLabel;

    private string _userTeamAbbr;
    private int _currentSeason;

    private void Awake()
    {
        _userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;
        var user = LocalDbManager.Instance.GetUser();
        _currentSeason = user?.CurrentSeason ?? DateTime.Now.Year;

        DateTime initDate;
        if (user != null && DateTime.TryParse(user.CurrentDate, out initDate))
        {
            UpdateDateLabel(initDate);
        }
        else
        {
            UpdateDateLabel(DateTime.Now);
        }
    }

    /// <summary>
    /// 선택한 날짜에 대한 스케줄을 표시한다.
    /// </summary>
    public void ShowScheduleForDate(DateTime date)
    {
        if (contentParent == null || scheduleCellPrefab == null)
        {
            Debug.LogError("[ScheduleView] contentParent 또는 prefab이 설정되지 않았습니다.");
            return;
        }

        // 기존 아이템 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        UpdateDateLabel(date);

        string dateStr = date.ToString("yyyy-MM-dd");
        List<Schedule> games = LocalDbManager.Instance.GetGamesForDate(dateStr);
        if (games == null || games.Count == 0)
        {
            return;
        }

        // 내 팀 경기가 가장 위로 오도록 정렬
        games.Sort((a, b) => {
            bool aIsUser = (a.HomeTeamAbbr == _userTeamAbbr || a.AwayTeamAbbr == _userTeamAbbr);
            bool bIsUser = (b.HomeTeamAbbr == _userTeamAbbr || b.AwayTeamAbbr == _userTeamAbbr);
            if (aIsUser == bIsUser) return 0;
            return aIsUser ? -1 : 1; // 내 팀 경기(a)가 우선이면 -1
        });

        foreach (var game in games)
        {
            ScheduleCell cell = Instantiate(scheduleCellPrefab, contentParent);
            cell.Configure(game, _currentSeason);
        }
    }

    private void UpdateDateLabel(DateTime dt)
    {
        if (dateLabel == null) return;
        string formatted = dt.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-US")).ToUpper();
        dateLabel.text = formatted;
    }
} 