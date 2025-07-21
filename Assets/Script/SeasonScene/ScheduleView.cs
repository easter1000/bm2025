using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using System.Globalization;
using UnityEngine.UI;

public class ScheduleView : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private ScheduleCell scheduleCellPrefab;
    [SerializeField] private TMP_Text dateLabel;
    [Header("Scroll Components")]
    [SerializeField] private ScrollRect scrollRect; // Assign via Inspector

    private string _userTeamAbbr;
    private int _currentSeason;

    private void Awake()
    {
        // Ensure ContentSizeFitter exists so content height expands with children
        if (contentParent != null)
        {
            var fitter = contentParent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = contentParent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

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
            // 레이아웃 강제 갱신하여 스크롤 범위 업데이트
            StartCoroutine(WaitAndRebuild());
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

        // 레이아웃을 즉시 다시 계산하고 스크롤 위치 초기화
        StartCoroutine(WaitAndRebuild());
    }

    private void UpdateDateLabel(DateTime dt)
    {
        if (dateLabel == null) return;
        string formatted = dt.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-US")).ToUpper();
        dateLabel.text = formatted;
    }

    /// <summary>
    /// 콘텐츠의 레이아웃을 강제로 갱신하고 스크롤을 최상단(=1)으로 맞춥니다.
    /// </summary>
    private System.Collections.IEnumerator WaitAndRebuild()
    {
        // Wait till end of frame so layout groups have processed sizes
        yield return null;

        if (contentParent is RectTransform rt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        // Keep scroll at top after rebuild (user can scroll freely afterward)
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
} 