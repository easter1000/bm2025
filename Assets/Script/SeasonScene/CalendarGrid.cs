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

    [Header("Actions")]
    [SerializeField] private Button advanceDayButton; // '일정 진행' 버튼

    private void Awake()
    {
        // 내비게이션 오브젝트에 클릭 리스너 연결 (Button 또는 Image)
        AddClickListener(prevMonthObj, -1);
        AddClickListener(nextMonthObj, +1);

        if (advanceDayButton != null)
        {
            advanceDayButton.onClick.AddListener(OnAdvanceDayClicked);
            advanceDayButton.gameObject.SetActive(false); // 초기에는 숨김
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
        DateTime today = SeasonManager.Instance.GetCurrentDate(); // '오늘' 날짜 가져오기

        for (int slot = 0; slot < TOTAL_SLOTS; slot++)
        {
            CalendarCell cell = Instantiate(cellPrefab, gridParent);

            int dayNum = slot - offset + 1;
            if (dayNum >= 1 && dayNum <= daysInMonth)
            {
                DateTime cellDate = new DateTime(_currentYear, _currentMonth, dayNum);
                bool isToday = (cellDate.Date == today.Date); // 오늘 날짜인지 확인

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

                // --- Handle click event ---
                cell.OnCellClicked += (date) => HandleCellClicked(cell, date);
            }
            else
            {
                cell.Configure(0, 0, 0, false, null, false);
            }
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

        // '오늘' 날짜인지 확인하여 버튼 표시 여부 결정
        DateTime today = SeasonManager.Instance.GetCurrentDate();
        if (advanceDayButton != null)
        {
            advanceDayButton.gameObject.SetActive(date.Date == today.Date);
        }

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

    private void OnAdvanceDayClicked()
    {
        DateTime today = SeasonManager.Instance.GetCurrentDate();
        string userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;

        if (string.IsNullOrEmpty(userTeamAbbr)) return;

        // 오늘 날짜의 경기 목록을 가져옴
        List<Schedule> gamesToday = LocalDbManager.Instance.GetGamesForDate(today.ToString("yyyy-MM-dd"));
        
        // 오늘 내 팀의 경기가 있는지 확인
        Schedule myGameToday = gamesToday?.FirstOrDefault(g => 
            (g.HomeTeamAbbr == userTeamAbbr || g.AwayTeamAbbr == userTeamAbbr) && g.GameStatus == "Scheduled");

        if (myGameToday != null)
        {
            // 내 경기가 있으면: GameDataHolder에 정보 저장하고 씬 이동
            GameDataHolder.CurrentGameInfo = myGameToday;
            Debug.Log($"[CalendarGrid] User game ({myGameToday.GameId}) found. Loading game scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("gamelogic_test");
        }
        else
        {
            // 내 경기가 없으면: 오늘 있는 다른 모든 AI 경기를 백그라운드에서 실행
            Debug.Log($"[CalendarGrid] No user game today. Simulating all AI games for {today:yyyy-MM-dd}.");
            var aiGames = gamesToday?.Where(g => g.GameStatus == "Scheduled").ToList();

            if (aiGames != null && aiGames.Count > 0)
            {
                BackgroundGameSimulator simulator = new BackgroundGameSimulator();
                foreach (var game in aiGames)
                {
                    Debug.Log($" - Simulating: {game.AwayTeamAbbr} at {game.HomeTeamAbbr}");
                    var result = simulator.SimulateFullGame(game);
                    SaveGameResult(game, result);
                }
            }
            else
            {
                Debug.Log("[CalendarGrid] No AI games to simulate today.");
            }

            // 모든 AI 경기 처리 후: 날짜를 하루 진행하고, DB 상태 업데이트 후, UI 새로고침
            LocalDbManager.Instance.AdvanceUserDate();
            LocalDbManager.Instance.UpdateAllPlayerStatusForNewDay(); // [추가] 모든 선수 스태미나/부상 회복
            SeasonManager.Instance.AttemptAiToAiTrades();
            
            // UI 업데이트
            PopulateCalendar(); 
            if (scheduleView != null)
            {
                // 다음 날의 스케줄을 보여주도록 업데이트
                scheduleView.ShowScheduleForDate(today.AddDays(1));
            }
            
            if (advanceDayButton != null)
            {
                advanceDayButton.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 경기 결과를 DB에 저장하는 헬퍼 메서드
    /// </summary>
    private void SaveGameResult(Schedule game, GameResult result)
    {
        var db = LocalDbManager.Instance;
        db.InsertPlayerStats(result.PlayerStats);
        db.UpdateGameResult(game.GameId, result.HomeScore, result.AwayScore);
        db.UpdateTeamWinLossRecord(game.HomeTeamAbbr, result.HomeScore > result.AwayScore, game.Season);
        db.UpdateTeamWinLossRecord(game.AwayTeamAbbr, result.AwayScore > result.HomeScore, game.Season);
    }
}