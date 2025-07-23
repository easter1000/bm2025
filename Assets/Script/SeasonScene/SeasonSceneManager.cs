using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// 시즌 씬에서 상단(또는 좌측) 탭 버튼을 관리하는 매니저.
/// * 스케줄, 팀 관리, 트레이드, 경기 기록, 종료 버튼 5개를 지원.
/// * 각 버튼 클릭 시 대응되는 Panel(GameObject) 하나만 활성화하고 나머지는 비활성화합니다.
/// * 종료 버튼은 애플리케이션을 종료합니다(에디터 환경에서는 플레이 정지).
/// </summary>
public class SeasonSceneManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button scheduleButton;
    [SerializeField] private Button teamManagerButton;
    [SerializeField] private Button tradeButton;
    [SerializeField] private Button recordButton;
    [SerializeField] private Button quitButton;

    [Header("Panels")]
    [SerializeField] private GameObject calendarPanel;
    [SerializeField] private GameObject teamManagerPanel;
    [SerializeField] private GameObject tradePanel;
    [SerializeField] private TMP_Dropdown recordDropdown;
    [SerializeField] private GameObject recordRankPanel;
    [SerializeField] private GameObject recordPlayPanel;
    [SerializeField] private GameObject recordTradePanel;
    
    [Header("Actions")]
    [SerializeField] private Button advanceDayButton; // '일정 진행' 버튼

    [Header("My Team Header UI")]
    [SerializeField] private Image myTeamLogoImage;
    [SerializeField] private TMP_Text myTeamNameText;
    [SerializeField] private TMP_Text currentDateText;
    [SerializeField] private TMP_Text currentBudgetText;

    [Header("Dialogs")]
    [SerializeField] private ConfirmDialog confirmDialog;
    [SerializeField] private CalendarGrid _calendarGrid;

    private TradeManager _tradeManager;

    private void Awake()
    {
        // 버튼 이벤트 등록
        if (scheduleButton) scheduleButton.onClick.AddListener(OnScheduleClicked);
        if (teamManagerButton) teamManagerButton.onClick.AddListener(OnTeamManagerClicked);
        if (tradeButton) tradeButton.onClick.AddListener(OnTradeClicked);
        if (recordButton) recordButton.onClick.AddListener(OnRecordButtonClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);
        if (advanceDayButton) advanceDayButton.onClick.AddListener(OnAdvanceDayClicked);
        if (recordDropdown) recordDropdown.onValueChanged.AddListener(OnRecordDropdownChanged);


        _tradeManager = FindObjectOfType<TradeManager>();
        if (_tradeManager == null)
        {
            Debug.LogError("[SeasonSceneManager] TradeManager를 찾을 수 없습니다!");
        }
    }

    private void OnEnable()
    {
        UpdateHeaderUI();
    }

    private void OnDisable()
    {
    }

    private void Start()
    {
        // 시작 시 기본적으로 스케줄(캘린더) 패널 보여주기
        ShowOnlyPanel(calendarPanel);
        
        // 드롭다운 초기화
        SetupRecordDropdown();

        // 상단(좌측) 헤더 UI 업데이트
        UpdateHeaderUI();

        // '일정 진행' 버튼을 항상 활성화
        if(advanceDayButton != null)
        {
            advanceDayButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// '일정 진행' 버튼 클릭 시 실행되는 로직.
    /// </summary>
    public async void OnAdvanceDayClicked()
    {
        if (advanceDayButton) advanceDayButton.interactable = false;

        string userDateStr = LocalDbManager.Instance.GetUser()?.CurrentDate;
        if (!DateTime.TryParse(userDateStr, out DateTime today))
        {
            Debug.LogError("OnAdvanceDayClicked: 유효한 사용자 날짜를 가져올 수 없습니다.");
            if (advanceDayButton) advanceDayButton.interactable = true;
            return;
        }

        string userTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;
        if (string.IsNullOrEmpty(userTeamAbbr))
        {
            if (advanceDayButton) advanceDayButton.interactable = true;
            return;
        }

        var gamesToday = LocalDbManager.Instance.GetGamesForDate(today.ToString("yyyy-MM-dd"));
        var myGameToday = gamesToday?.FirstOrDefault(g => 
            (g.HomeTeamAbbr == userTeamAbbr || g.AwayTeamAbbr == userTeamAbbr) && g.GameStatus == "Scheduled");

        List<TradeOffer> userTradeOffers = null;

        await Task.Run(() =>
        {
            // 1. AI 팀 간 트레이드 시도 및 유저 제안 수집 (항상 실행)
            userTradeOffers = SeasonManager.Instance.AttemptAiToAiTrades();

            // 2. AI 경기 시뮬레이션 (유저 경기 제외하고 항상 실행)
            Debug.Log("[Background] Simulating other AI games for the day...");
            var gamesToSimulate = gamesToday?.Where(g => g.GameStatus == "Scheduled").ToList();
            if (myGameToday != null)
            {
                // 유저 경기가 있는 경우, 해당 경기를 시뮬레이션 목록에서 제거
                gamesToSimulate.Remove(myGameToday);
            }

            if (gamesToSimulate != null && gamesToSimulate.Count > 0)
            {
                BackgroundGameSimulator simulator = new BackgroundGameSimulator();
                foreach (var game in gamesToSimulate)
                {
                    var result = simulator.SimulateFullGame(game);
                    SaveGameResult(game, result);
                }
            }

            // 3. 유저 경기가 없을 때만 날짜 진행 및 선수 상태 업데이트
            if (myGameToday == null)
            {
                Debug.Log("[Background] No user game today. Advancing date...");
                LocalDbManager.Instance.AdvanceUserDate();

                Debug.Log("[Background] Updating player status for new day...");
                LocalDbManager.Instance.UpdateAllPlayerStatusForNewDay();
            }
        });

        // 3. [메인스레드] 후속 작업 정의
        Action continuationAction = () => {
            if (myGameToday != null)
            {
                // 유저 경기가 있으면 게임 씬으로 이동
                GameDataHolder.CurrentGameInfo = myGameToday;
                SceneManager.LoadScene("gamelogic_test");
            }
            else
            {
                // 유저 경기가 없었으면 AI 턴이 끝났으므로 UI 갱신
                Debug.Log("[MainThread] AI turn finished. Updating UI.");
                UpdateHeaderUI();
                if (_calendarGrid != null)
                {
                    _calendarGrid.PopulateCalendar();
                }

                if (advanceDayButton)
                {
                    advanceDayButton.interactable = true;
                    advanceDayButton.gameObject.SetActive(true);
                }
            }
        };

        // 4. [메인스레드] 트레이드 제안이 있으면 다이얼로그 표시, 없으면 후속 작업 바로 실행
        if (userTradeOffers != null && userTradeOffers.Any())
        {
            HandleTradeOffer(userTradeOffers.First(), continuationAction);
        }
        else
        {
            continuationAction();
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


    /// <summary>
    /// 사용자 팀 로고, 팀 이름, 현재 날짜, 예산 등을 화면에 업데이트한다.
    /// 씬 진입 시와 날짜/예산 변동 시 호출.
    /// </summary>
    public void UpdateHeaderUI()
    {
        var user = LocalDbManager.Instance.GetUser();
        if (user == null)
        {
            Debug.LogWarning("[SeasonSceneManager] User 정보가 없어 헤더를 업데이트할 수 없습니다.");
            return;
        }

        string teamAbbr = user.SelectedTeamAbbr;
        int season = user.CurrentSeason;

        // 1) 팀 로고 & 이름
        if (myTeamLogoImage != null)
        {
            Sprite logo = Resources.Load<Sprite>($"team_photos/{teamAbbr.ToLower()}");
            if (logo == null)
            {
                logo = Resources.Load<Sprite>("team_photos/default_logo");
            }
            myTeamLogoImage.sprite = logo;
        }

        if (myTeamNameText != null)
        {
            Team teamEntity = LocalDbManager.Instance.GetTeam(teamAbbr);
            myTeamNameText.text = teamEntity != null ? teamEntity.team_name : teamAbbr;
        }

        // 2) 현재 날짜
        if (currentDateText != null)
        {
            if (DateTime.TryParse(user.CurrentDate, out DateTime dt))
            {
                currentDateText.text = dt.ToString("yyyy년 MMM d일");
            }
            else
            {
                currentDateText.text = user.CurrentDate;
            }
        }

        // 3) 예산 (Budget)
        if (currentBudgetText != null)
        {
            var finance = LocalDbManager.Instance.GetTeamFinance(teamAbbr, season);
            if (finance != null)
            {
                currentBudgetText.text = $"$ {finance.TeamBudget:N0}"; // 천단위 구분기호
            }
            else
            {
                currentBudgetText.text = "$ -";
            }
        }
    }

    private void HandleTradeOffer(TradeOffer offer, Action onDialogClosed = null)
    {
        if (confirmDialog == null)
        {
            Debug.LogError("[SeasonSceneManager] ConfirmDialog가 연결되지 않았습니다.");
            onDialogClosed?.Invoke();
            return;
        }

        // 다이얼로그 게임오브젝트를 활성화
        confirmDialog.gameObject.SetActive(true);

        // 트레이드 제안 내용 메시지 생성
        var offeredPlayer = offer.PlayersOfferedByProposingTeam.FirstOrDefault();
        var requestedPlayer = offer.PlayersRequestedFromTargetTeam.FirstOrDefault();

        if (offeredPlayer == null || requestedPlayer == null) 
        {
            onDialogClosed?.Invoke();
            return;
        }

        string message = $"{offer.ProposingTeam.team_name}에서 트레이드를 제안했습니다:\n\n" +
                         $"<color=green>오는 선수: {offeredPlayer.name} (OVR: {offeredPlayer.overallAttribute})</color>\n" +
                         $"<color=red>떠나는 선수: {requestedPlayer.name} (OVR: {requestedPlayer.overallAttribute})</color>\n\n" +
                         "수락하시겠습니까?";

        // 다이얼로그 띄우기
        confirmDialog.Show(
            message,
            onYes: () => {
                // '예'를 눌렀을 때: 트레이드 실행
                _tradeManager.ExecuteTrade(
                    offer.ProposingTeam.team_abbv, offer.PlayersOfferedByProposingTeam,
                    offer.TargetTeam.team_abbv, offer.PlayersRequestedFromTargetTeam
                );

                // 트레이드 성공 후 후속 처리
                confirmDialog.Show("트레이드가 성공적으로 성사되었습니다!", () => {
                    UpdateHeaderUI(); // 예산 등 정보 업데이트
                    onDialogClosed?.Invoke();
                });
            },
            onNo: () => {
                // '아니오'를 눌렀을 때: 아무것도 안 함
                confirmDialog.Show("트레이드를 거절했습니다.", () => {
                    onDialogClosed?.Invoke();
                });
            }
        );
    }
    
    #region Button Callbacks

    private void OnScheduleClicked() => ShowOnlyPanel(calendarPanel);
    private void OnTeamManagerClicked() => ShowOnlyPanel(teamManagerPanel);
    private void OnTradeClicked() => ShowOnlyPanel(tradePanel);
    private void OnRecordButtonClicked()
    {
        if (recordDropdown == null) return;

        bool willShow = !recordDropdown.gameObject.activeSelf;
        recordDropdown.gameObject.SetActive(willShow);

        if (willShow)
        {
            StartCoroutine(OpenDropdownNextFrame());
        }
    }

    private System.Collections.IEnumerator OpenDropdownNextFrame()
    {
        yield return null; // 다음 프레임까지 대기
        recordDropdown.Show();
        // 플레이스홀더 유지
        if (recordDropdown.captionText)
        {
            recordDropdown.captionText.text = "기록 선택...";
        }
    }

    private void OnRecordDropdownChanged(int index)
    {
        if (index==0){
            ShowOnlyPanel(null); // no panel
            return;
        }
        switch (index)
        {
            case 1: // 리그 순위
                ShowOnlyPanel(recordRankPanel);
                break;
            case 2: // 경기 기록
                ShowOnlyPanel(recordPlayPanel);
                break;
        }
    }

    private void OnQuitClicked()
    {
        if (confirmDialog == null)
        {
            Debug.LogError("ConfirmDialog가 연결되지 않았습니다. 즉시 종료합니다.");
            QuitApplication();
            return;
        }

        confirmDialog.Show(
            "정말로 게임을 종료하시겠습니까?",
            onYes: () => {
                QuitApplication();
            },
            onNo: () => {
                // 아무것도 하지 않음
            }
        );
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetupRecordDropdown()
    {
        if (recordDropdown == null) return;

        recordDropdown.ClearOptions();
        recordDropdown.AddOptions(new List<string> { "기록 선택...", "리그 순위", "경기 기록" });
         
        // placeholder index 0
        recordDropdown.SetValueWithoutNotify(0); 
        if (recordDropdown.captionText)
        {
            recordDropdown.captionText.text = "기록 선택...";
        }
        recordDropdown.gameObject.SetActive(false);
    }

    #endregion

    #region Trade Scene Navigation

    private const string TradeTargetKey = "TradeTargetTeamAbbr";

    /// <summary>
    /// 선택한 상대 팀 약어를 저장한 뒤 TradeScene 으로 이동한다.
    /// SeasonScene 내부의 다른 UI 요소(예: 일정 셀, 팀 상세 패널 등)에서 호출할 수 있다.
    /// </summary>
    /// <param name="opponentAbbr">상대 팀 약어(예: "LAL")</param>
    public void OpenTradeSceneWithOpponent(string opponentAbbr)
    {
        if (string.IsNullOrEmpty(opponentAbbr))
        {
            Debug.LogWarning("[SeasonSceneManager] opponentAbbr 가 비어있어 FA로 설정합니다.");
            opponentAbbr = "FA";
        }

        // PlayerPrefs 에 저장 후 씬 전환
        PlayerPrefs.SetString(TradeTargetKey, opponentAbbr);
        PlayerPrefs.Save();
        SceneManager.LoadScene("TradeScene");
    }

    #endregion

    /// <summary>
    /// 전달된 패널 하나만 활성화하고 나머지는 모두 비활성화.
    /// null 패널이 전달되면 모든 패널을 비활성화만 합니다.
    /// </summary>
    private void ShowOnlyPanel(GameObject activePanel)
    {
        // 배열로 묶어서 처리
        GameObject[] panels = { calendarPanel, teamManagerPanel, tradePanel, recordRankPanel, recordPlayPanel, recordTradePanel };
        foreach (var p in panels)
        {
            if (p == null) continue;
            p.SetActive(p == activePanel);
        }

        // 기록 탭이 아닌 다른 탭을 눌렀을 경우 드롭다운을 숨기고 값을 초기화합니다.
        bool isRecordPanelActive = activePanel == recordRankPanel || activePanel == recordPlayPanel || activePanel == recordTradePanel;
        if (recordDropdown != null)
        {
            if (!isRecordPanelActive)
            {
                recordDropdown.gameObject.SetActive(false);
                recordDropdown.SetValueWithoutNotify(0);
                if (recordDropdown.captionText)
                {
                    recordDropdown.captionText.text = "기록 선택...";
                }
            }
            else
            {
                // 기록 패널을 열 때마다 드롭다운 캡션을 유지하되 필요 시 표시
                recordDropdown.gameObject.SetActive(true);
            }
        }
    }
} 