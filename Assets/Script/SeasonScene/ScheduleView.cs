using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System.Globalization;
using System.Collections; // Added for IEnumerator

public class ScheduleView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private ScheduleCell scheduleCellPrefab;
    [SerializeField] private TMP_Text dateLabel;

    private string _userTeamAbbr;
    private int _currentSeason;
    private Schedule _userGameOnSelectedDate;

    private void Awake()
    {
        var user = LocalDbManager.Instance.GetUser();
        if (user != null)
        {
            _userTeamAbbr = user.SelectedTeamAbbr;
            _currentSeason = user.CurrentSeason;

            if (DateTime.TryParse(user.CurrentDate, out DateTime initDate))
            {
                UpdateDateLabel(initDate);
            }
            else
            {
                UpdateDateLabel(DateTime.Now);
            }
        }
        else
        {
            // 유저 정보가 없을 경우의 기본값
            _userTeamAbbr = "BOS"; // 예시 팀
            _currentSeason = DateTime.Now.Year;
            UpdateDateLabel(DateTime.Now);
            Debug.LogWarning("User 정보를 찾을 수 없어 기본값으로 설정합니다.");
        }
    }

    private void Start()
    {
        // TradeScene 또는 SeasonScene 로딩 직후, 사용자의 '현재 날짜' 스케줄을 자동 표시
        DateTime today = SeasonManager.Instance != null ? SeasonManager.Instance.GetCurrentDate() : DateTime.Now;
        ShowScheduleForDate(today);
    }

    /// <summary>
    /// 선택한 날짜에 대한 스케줄을 표시한다.
    /// </summary>
    public void ShowScheduleForDate(DateTime date)
    {

        // 1) 기존 아이템 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        UpdateDateLabel(date);
        _userGameOnSelectedDate = null; // 리셋

        string dateStr = date.ToString("yyyy-MM-dd");
        List<Schedule> games = LocalDbManager.Instance.GetGamesForDate(dateStr);
        if (games == null || games.Count == 0)
        {

            // 자식이 없어도 ContentSizeFitter가 0 높이를 계산하도록 즉시 + 지연 Rebuild 수행
            if (contentParent != null)
            {
                RectTransform rt = contentParent as RectTransform;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                StartCoroutine(DelayedLayoutRebuild());
            }

            return;
        }

        // 내 팀 경기가 가장 위로 오도록 정렬
        games.Sort((a, b) => {
            bool aIsUser = (a.HomeTeamAbbr == _userTeamAbbr || a.AwayTeamAbbr == _userTeamAbbr);
            bool bIsUser = (b.HomeTeamAbbr == _userTeamAbbr || b.AwayTeamAbbr == _userTeamAbbr);
            if (aIsUser == bIsUser) return 0;
            return aIsUser ? -1 : 1; // 내 팀 경기(a)가 우선이면 -1
        });

        // 3) 셀 생성
        foreach (var game in games)
        {
            ScheduleCell cell = Instantiate(scheduleCellPrefab, contentParent);
            cell.Configure(game, _currentSeason);
        }

        // 레이아웃 최신화
        if (contentParent != null)
        {
            // 1) 즉시 한 번 Rebuild 시도 (대부분의 경우 충분)
            RectTransform rt = contentParent as RectTransform;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            // 수동으로 PreferredHeight 계산 후 적용 (ContentSizeFitter가 제대로 동작하지 않는 경우 대비)
            float prefH = UnityEngine.UI.LayoutUtility.GetPreferredHeight(rt);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prefH);

            // 2) 파괴 대기 중인 오브젝트가 실제로 제거되고, 새 셀이 완전히 배치된 뒤
            //    다시 한 번 레이아웃을 강제로 갱신해야 정확한 Content 높이가 계산된다.
            //    EndOfFrame까지 대기 후 재빌드하는 코루틴을 사용한다.
            StartCoroutine(DelayedLayoutRebuild());
        }

        // 내 팀의 예정된 경기가 있는지 확인
        _userGameOnSelectedDate = games.FirstOrDefault(g => 
            (g.HomeTeamAbbr == _userTeamAbbr || g.AwayTeamAbbr == _userTeamAbbr) && g.GameStatus == "Scheduled");
    }

    private void OnStartGameClicked()
    {
        if (_userGameOnSelectedDate == null)
        {
            Debug.LogError("시작할 경기가 선택되지 않았습니다.");
            return;
        }

        // GameDataHolder에 선택된 경기 정보를 저장
        GameDataHolder.CurrentGameInfo = _userGameOnSelectedDate;

        Debug.Log($"경기 시작: {_userGameOnSelectedDate.GameId}. gamelogic_test 씬으로 이동합니다.");
        
        // 경기 씬 로드
        UnityEngine.SceneManagement.SceneManager.LoadScene("gamelogic_test");
    }

    private void UpdateDateLabel(DateTime dt)
    {
        if (dateLabel == null) return;
        string formatted = dt.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-US")).ToUpper();
        dateLabel.text = formatted;
    }

    /// <summary>
    /// Destroy()가 실제로 오브젝트를 제거하고, 새로 생성된 셀들의 레이아웃이 확정된 뒤
    /// ContentSizeFitter가 다시 계산되도록 EndOfFrame까지 기다렸다가 한 번 더 Rebuild.
    /// </summary>
    private System.Collections.IEnumerator DelayedLayoutRebuild()
    {
        yield return new WaitForEndOfFrame();
        if (contentParent != null)
        {
            RectTransform rt = contentParent as RectTransform;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            float prefH2 = UnityEngine.UI.LayoutUtility.GetPreferredHeight(rt);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prefH2);
        }
    }
} 