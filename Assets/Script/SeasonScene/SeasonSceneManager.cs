using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Linq; // Added for FirstOrDefault

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
    [SerializeField] private GameObject recordPanel;

    [Header("My Team Header UI")]
    [SerializeField] private Image myTeamLogoImage;
    [SerializeField] private TMP_Text myTeamNameText;
    [SerializeField] private TMP_Text currentDateText;
    [SerializeField] private TMP_Text currentBudgetText;

    [Header("Dialogs")]
    [SerializeField] private ConfirmDialog confirmDialog;

    private TradeManager _tradeManager;

    private void Awake()
    {
        // 버튼 이벤트 등록
        if (scheduleButton) scheduleButton.onClick.AddListener(OnScheduleClicked);
        if (teamManagerButton) teamManagerButton.onClick.AddListener(OnTeamManagerClicked);
        if (tradeButton) tradeButton.onClick.AddListener(OnTradeClicked);
        if (recordButton) recordButton.onClick.AddListener(OnRecordClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);

        _tradeManager = FindObjectOfType<TradeManager>();
        if (_tradeManager == null)
        {
            Debug.LogError("[SeasonSceneManager] TradeManager를 찾을 수 없습니다!");
        }
    }

    private void OnEnable()
    {
        UpdateHeaderUI();
        SeasonManager.OnTradeOfferedToUser += HandleTradeOffer;
    }

    private void OnDisable()
    {
        SeasonManager.OnTradeOfferedToUser -= HandleTradeOffer;
    }

    private void Start()
    {
        // 시작 시 기본적으로 스케줄(캘린더) 패널 보여주기
        ShowOnlyPanel(calendarPanel);

        // 상단(좌측) 헤더 UI 업데이트
        UpdateHeaderUI();
    }

    /// <summary>
    /// AI 팀으로부터 온 트레이드 제안을 처리하고 다이얼로그를 띄운다.
    /// </summary>
    private void HandleTradeOffer(TradeOffer offer)
    {
        if (confirmDialog == null)
        {
            Debug.LogError("[SeasonSceneManager] ConfirmDialog가 연결되지 않았습니다.");
            return;
        }

        // 트레이드 제안 내용 메시지 생성
        var offeredPlayer = offer.PlayersOfferedByProposingTeam.FirstOrDefault();
        var requestedPlayer = offer.PlayersRequestedFromTargetTeam.FirstOrDefault();

        if (offeredPlayer == null || requestedPlayer == null) return;

        string message = $"{offer.ProposingTeam.team_name}에서 트레이드를 제안했습니다:\n\n" +
                         $"<color=green>주는 선수: {offeredPlayer.name} (OVR: {offeredPlayer.overallAttribute})</color>\n" +
                         $"<color=red>받는 선수: {requestedPlayer.name} (OVR: {requestedPlayer.overallAttribute})</color>\n\n" +
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
                Debug.Log("트레이드가 성공적으로 성사되었습니다!");
                UpdateHeaderUI(); // 예산 등 정보 업데이트
                // TODO: 팀 관리 패널 등 다른 UI도 새로고침 필요
            },
            onNo: () => {
                // '아니오'를 눌렀을 때: 아무것도 안 함
                Debug.Log("트레이드를 거절했습니다.");
            }
        );
    }


    /// <summary>
    /// 사용자 팀 로고, 팀 이름, 현재 날짜, 예산 등을 화면에 업데이트한다.
    /// 씬 진입 시와 날짜/예산 변동 시 호출.
    /// </summary>
    private void UpdateHeaderUI()
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

    #region Button Callbacks

    private void OnScheduleClicked() => ShowOnlyPanel(calendarPanel);
    private void OnTeamManagerClicked() => ShowOnlyPanel(teamManagerPanel);
    private void OnTradeClicked() => ShowOnlyPanel(tradePanel);
    private void OnRecordClicked() => ShowOnlyPanel(recordPanel);

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        // 에디터에서는 플레이 모드를 중지
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 게임에서는 애플리케이션 종료
        Application.Quit();
#endif
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
        GameObject[] panels = { calendarPanel, teamManagerPanel, tradePanel, recordPanel };
        foreach (var p in panels)
        {
            if (p == null) continue;
            p.SetActive(p == activePanel);
        }
    }
} 