using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems; // 이미지 클릭 감지를 위해
using System.Collections.Generic;
using System.Globalization; // 영어 월 표기를 위해
using System.Linq;

/// <summary>
/// CallenderCell 들을 생성/관리하며 월 이동을 지원하는 달력 그리드.
/// </summary>
public class CalendarGrid : MonoBehaviour
{
    [Header("Prefabs & Parents")]
    [SerializeField] private CalendarCell cellPrefab;
    [SerializeField] private Transform gridParent; // GridLayoutGroup가 부착된 Transform

    [Header("Navigation UI")]
    [Tooltip("이전 달 이동 오브젝트 (Image 또는 Button)")]
    [SerializeField] private GameObject prevMonthObj;
    [Tooltip("다음 달 이동 오브젝트 (Image 또는 Button)")]
    [SerializeField] private GameObject nextMonthObj;
    [SerializeField] private TMP_Text monthLabel;

    private int _currentYear;
    private int _currentMonth;

    private const int TOTAL_SLOTS = 42; // 7x6 달력 그리드

    [Header("Schedule View")]
    [SerializeField] private ScheduleView scheduleView;

    private void Awake()
    {
        // 내비게이션 오브젝트에 클릭 리스너 연결 (Button 또는 Image)
        AddClickListener(prevMonthObj, -1);
        AddClickListener(nextMonthObj, +1);

        InitializeDateFromUser();
    }

    private void Start()
    {
        PopulateCalendar();
    }

    /// <summary>
    /// 월 변경 (+1 / -1)
    /// </summary>
    private void ChangeMonth(int delta)
    {
        _currentMonth += delta;
        if (_currentMonth < 1)
        {
            _currentMonth = 12;
            _currentYear--;
        }
        else if (_currentMonth > 12)
        {
            _currentMonth = 1;
            _currentYear++;
        }
        PopulateCalendar();
    }

    private void AddClickListener(GameObject obj, int monthDelta)
    {
        if (obj == null) return;

        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => ChangeMonth(monthDelta));
            return;
        }

        // Button 컴포넌트가 없다면 EventTrigger를 추가해 클릭 감지
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = obj.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener((_) => ChangeMonth(monthDelta));
        trigger.triggers.Add(entry);
    }

    private void PopulateCalendar()
    {
        if (cellPrefab == null || gridParent == null)
        {
            Debug.LogError("[CalendarGrid] Prefab 또는 gridParent가 설정되지 않았습니다.");
            return;
        }

        // 1) 기존 셀 제거
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        // 2) 날짜 계산
        int daysInMonth = DateTime.DaysInMonth(_currentYear, _currentMonth);
        DateTime firstDay = new DateTime(_currentYear, _currentMonth, 1);
        int offset = (int)firstDay.DayOfWeek; // 0=Sunday, 1=Mon...

        // 3) 셀 생성
        string userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;

        for (int slot = 0; slot < TOTAL_SLOTS; slot++)
        {
            CalendarCell cell = Instantiate(cellPrefab, gridParent);

            int dayNum = slot - offset + 1;
            if (dayNum >= 1 && dayNum <= daysInMonth)
            {
                // 날짜 문자열 yyyy-MM-dd
                string dateStr = new DateTime(_currentYear, _currentMonth, dayNum).ToString("yyyy-MM-dd");
                List<Schedule> games = LocalDbManager.Instance.GetGamesForDate(dateStr);
                bool hasGame = games != null && games.Count > 0;

                // --- Determine opponent logo if the user's team plays on this date ---
                Sprite displayLogo = null;

                bool isUserGame = false;
                if (hasGame && !string.IsNullOrEmpty(userTeamAbbr))
                {
                    // Check if any game includes the user's team
                    Schedule userGame = games.FirstOrDefault(g => g.HomeTeamAbbr == userTeamAbbr || g.AwayTeamAbbr == userTeamAbbr);
                    if (userGame != null)
                    {
                        isUserGame = true;

                        // Identify opponent team abbreviation
                        string opponentAbbr = userGame.HomeTeamAbbr == userTeamAbbr ? userGame.AwayTeamAbbr : userGame.HomeTeamAbbr;
                        displayLogo = Resources.Load<Sprite>($"team_photos/{opponentAbbr}");
                    }
                }

                // If user's team is not playing, keep previous behaviour (no logo)

                cell.Configure(dayNum, _currentMonth, _currentYear, hasGame, displayLogo, isUserGame);
                cell.OnCellClicked += HandleCellClicked;
            }
            else
            {
                // 빈 슬롯도 outline 회색으로 유지 (isUserGame = false)
                cell.Configure(0, _currentMonth, _currentYear, false, null, false);
            }
        }

        if (monthLabel)
        {
            string monthName = CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(_currentMonth).ToUpper();
            monthLabel.text = $"{monthName} {_currentYear}";
        }
    }

    private void InitializeDateFromUser()
    {
        string curDateStr = LocalDbManager.Instance.GetUser()?.CurrentDate;
        DateTime dt;
        if (string.IsNullOrEmpty(curDateStr) || !DateTime.TryParse(curDateStr, out dt))
        {
            dt = DateTime.Now;
            Debug.LogWarning("[CalendarGrid] User.CurrentDate를 파싱하지 못해 오늘 날짜를 사용합니다: " + dt.ToString("yyyy-MM-dd"));
        }

        _currentYear = dt.Year;
        _currentMonth = dt.Month;
    }

    private void HandleCellClicked(DateTime date)
    {
        scheduleView?.ShowScheduleForDate(date);
    }
}