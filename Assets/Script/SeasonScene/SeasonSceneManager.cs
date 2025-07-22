using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private void Awake()
    {
        // 버튼 이벤트 등록
        if (scheduleButton) scheduleButton.onClick.AddListener(OnScheduleClicked);
        if (teamManagerButton) teamManagerButton.onClick.AddListener(OnTeamManagerClicked);
        if (tradeButton) tradeButton.onClick.AddListener(OnTradeClicked);
        if (recordButton) recordButton.onClick.AddListener(OnRecordClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void Start()
    {
        // 시작 시 기본적으로 스케줄(캘린더) 패널 보여주기
        ShowOnlyPanel(calendarPanel);
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