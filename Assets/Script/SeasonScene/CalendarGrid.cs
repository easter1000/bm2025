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
    [SerializeField] private Image prevMonthBackground;
    [Tooltip("다음 달 이동 오브젝트 (Image 또는 Button)")]
    [SerializeField] private GameObject nextMonthObj;
    [SerializeField] private Image nextMonthBackground;
    [SerializeField] private TMP_Text monthLabel;

    private int _currentYear;
    private int _currentMonth;
    private CalendarCell _selectedCell; // 현재 선택된 셀을 추적

    private const int TOTAL_SLOTS = 42; // 7x6 달력 그리드

    [Header("Schedule View")]
    [SerializeField] private ScheduleView scheduleView;

    private SeasonSceneManager _seasonSceneManager;

    private void Awake()
    {
        // 내비게이션 오브젝트에 클릭 리스너 연결 (Button 또는 Image)
        AddClickListener(prevMonthObj, -1);
        AddClickListener(nextMonthObj, +1);

        _seasonSceneManager = FindObjectOfType<SeasonSceneManager>();
        if (_seasonSceneManager == null)
        {
            Debug.LogError("[CalendarGrid] SeasonSceneManager를 찾을 수 없습니다!");
        }

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
        if ((_currentYear == 2025 && _currentMonth + delta < 10) || (_currentYear == 2026 && _currentMonth + delta > 4)) return;
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

    public void PopulateCalendar()
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
        
        // '오늘' 날짜를 User 테이블에서 직접 가져오기
        string userDateStr = LocalDbManager.Instance.GetUser()?.CurrentDate;
        DateTime today = DateTime.MinValue; // 비교를 위해 초기값 설정
        if (!string.IsNullOrEmpty(userDateStr) && DateTime.TryParse(userDateStr, out DateTime parsedDate))
        {
            today = parsedDate.Date;
        }

        CalendarCell todayCell = null; // 오늘 날짜에 해당하는 셀을 저장할 변수

        for (int slot = 0; slot < TOTAL_SLOTS; slot++)
        {
            CalendarCell cell = Instantiate(cellPrefab, gridParent);

            int dayNum = slot - offset + 1;
            if (dayNum >= 1 && dayNum <= daysInMonth)
            {
                DateTime cellDate = new DateTime(_currentYear, _currentMonth, dayNum);
                bool isToday = (today != DateTime.MinValue && cellDate.Date == today);

                // 날짜 문자열 yyyy-MM-dd
                string dateStr = cellDate.ToString("yyyy-MM-dd");
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
                        if (!string.IsNullOrEmpty(opponentAbbr))
                        {
                            // 로고 파일 이름은 소문자일 수 있으므로 ToLower() 추가
                            displayLogo = Resources.Load<Sprite>($"team_photos/{opponentAbbr.ToLower()}");
                        }
                    }
                }

                cell.Configure(dayNum, _currentMonth, _currentYear, isToday, displayLogo, isUserGame);

                if (isToday)
                {
                    todayCell = cell; // 오늘 날짜 셀 저장
                }

                // --- Handle click event ---
                cell.OnCellClicked += (date) => HandleCellClicked(cell, date);
            }
            else
            {
                cell.Configure(0, 0, 0, false, null, false);
            }
        }

        // 모든 셀이 생성된 후, 오늘 날짜의 셀을 자동으로 클릭 처리
        if (todayCell != null)
        {
            HandleCellClicked(todayCell, today);
        }

        if (monthLabel)
        {
            string monthName = CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(_currentMonth).ToUpper();
            monthLabel.text = $"{monthName} {_currentYear}";
        }

        Color enabledColor, disabledColor;
        ColorUtility.TryParseHtmlString("#BABABA", out disabledColor);
        ColorUtility.TryParseHtmlString("#0093FF", out enabledColor);

        if (_currentYear == 2025 && _currentMonth == 10)
        {
            prevMonthBackground.color = disabledColor;
        } else {
            prevMonthBackground.color = enabledColor;
        }
        if (_currentYear == 2026 && _currentMonth == 4)
        {
            nextMonthBackground.color = disabledColor;
        } else {
            nextMonthBackground.color = enabledColor;
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
        UpdateMonthLabel();
    }

    private void HandleCellClicked(CalendarCell cell, DateTime date)
    {
        // 이전에 선택된 셀이 있었다면 선택 해제
        if (_selectedCell != null)
        {
            _selectedCell.SetSelected(false);
        }

        // 새로 선택된 셀 처리
        cell.SetSelected(true);
        _selectedCell = cell;

        // ScheduleView 업데이트
        if (scheduleView != null)
        {
            scheduleView.ShowScheduleForDate(date);
        }
    }

    private void UpdateMonthLabel()
    {
        if (monthLabel)
        {
            string monthName = CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(_currentMonth).ToUpper();
            monthLabel.text = $"{monthName} {_currentYear}";
        }
    }
}