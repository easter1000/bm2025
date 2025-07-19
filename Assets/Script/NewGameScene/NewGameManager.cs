using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq; // 추가: 팀 정렬에 사용
using System.Collections; // 추가: 코루틴 사용
using madcamp3.Assets.Script.Player;

public class NewGameManager : MonoBehaviour
{
    private enum Step
    {
        Prologue,
        NameInput,
        TeamSelection,
        TeamIntro
    }

    [SerializeField] private Step initialStep = Step.Prologue;

    [Header("Panels")] public GameObject prologuePanel; // 이미지+나레이션
    public GameObject nameInputPanel;
    public GameObject teamSelectionPanel;
    public GameObject teamIntroPanel;

    [Header("Prologue")]
    public NarrationTyper prologueTyper;
    [TextArea] public string prologueText = "당신은 이번 시즌 새로 부임한 감독입니다...";

    [Header("Name Input")]
    public TMP_InputField coachNameInputField;
    public Button coachNameConfirmButton;

    [Header("Team Selection")]
    public Transform teamListContent; // ScrollView의 Content
    public GameObject teamItemPrefab;
    // TeamDetailUI removed
    public Button teamSelectConfirmButton;

    [Header("Team Intro")]
    public NarrationTyper teamIntroTyper;
    public Image teamIntroImage; // 팀 이미지

    [Header("Continue Buttons")]
    [Tooltip("나레이션이 끝난 뒤 눌러서 다음 단계로 넘어가는 버튼들")]
    public Button prologueContinueButton;
    public Button teamIntroContinueButton;

    [Tooltip("선택 후 이동할 게임 씬 이름")]
    public string gameSceneName = "Game";

    private Step currentStep;
    private List<TeamData> teams = new();
    private TeamData selectedTeam;

    private const string SelectedTeamIdKey = "SelectedTeamId";

    // 추가: 팀 리스트 레이아웃이 이미 설정됐는지 여부
    private bool teamListLayoutConfigured = false;

    private void Start()
    {
        InitDummyTeams();
        coachNameConfirmButton.onClick.AddListener(OnNameConfirmed);
        teamSelectConfirmButton.onClick.AddListener(OnTeamConfirmed);
        // ScrollView 레이아웃 설정 (한 번만)
        ConfigureTeamListLayout();
        // 초기에는 계속 버튼을 숨깁니다.
        if (prologueContinueButton) prologueContinueButton.gameObject.SetActive(false);
        if (teamIntroContinueButton) teamIntroContinueButton.gameObject.SetActive(false);

        // 선택한 초기 스텝으로 전환
        SwitchStep(initialStep);
    }

    #region Step Control

    private void SwitchStep(Step step)
    {
        currentStep = step;
        prologuePanel.SetActive(step == Step.Prologue);
        nameInputPanel.SetActive(step == Step.NameInput);
        teamSelectionPanel.SetActive(step == Step.TeamSelection);
        teamIntroPanel.SetActive(step == Step.TeamIntro);

        switch (step)
        {
            case Step.Prologue:
                if (prologueContinueButton) prologueContinueButton.gameObject.SetActive(false);
                prologueTyper.Play(prologueText, ShowPrologueContinue);
                break;
            case Step.NameInput:
                break;
            case Step.TeamSelection:
                PopulateTeamList();
                break;
            case Step.TeamIntro:
                if (teamIntroContinueButton) teamIntroContinueButton.gameObject.SetActive(false);
                PlayTeamIntro();
                break;
        }
    }

    #endregion

    #region Name Input

    private void OnNameConfirmed()
    {
        if (string.IsNullOrWhiteSpace(coachNameInputField.text)) return;
        // 이름 저장 후 팀 인트로로 이동
        SwitchStep(Step.TeamIntro);
    }

    #endregion

    #region Team Selection

    // 추가: ScrollView Content 에 GridLayoutGroup 설정
    private void ConfigureTeamListLayout()
    {
        if (teamListLayoutConfigured || teamListContent == null) return;

        GridLayoutGroup grid = teamListContent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = teamListContent.gameObject.AddComponent<GridLayoutGroup>();

        // 초기 셀 크기는 일단 프리팹 기준으로 두고, 실제 크기는 PopulateTeamList() 시점에 Viewport 비율로 다시 계산한다.

        // 가로 슬라이드: 한 행(Row)만 두고 셀을 좌우로 나열
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount; // 행 개수 고정 = 1
        grid.constraintCount = 1;
        grid.startAxis = GridLayoutGroup.Axis.Vertical; // 열을 우측으로 확장
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.MiddleLeft;
        grid.spacing = new Vector2(20f, 0f);

        // ContentSizeFitter는 GridLayoutGroup과 함께 쓰면 무한 레이아웃 루프를 유발할 수 있으므로 제거
        ContentSizeFitter fitter = teamListContent.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(fitter);
#else
            Destroy(fitter);
#endif
        }

        // RectTransform 앵커 / 피벗 설정 (좌측 기준으로 배치되도록)
        RectTransform contentRect = teamListContent.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0.5f);
        contentRect.anchorMax = new Vector2(0, 0.5f);
        contentRect.pivot = new Vector2(0, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;

        teamListLayoutConfigured = true;

        // ScrollRect 가로 스크롤 비활성화, 세로 스크롤 활성화
        ScrollRect sr = teamListContent.GetComponentInParent<ScrollRect>();
        if (sr != null)
        {
            sr.horizontal = true;
            sr.vertical = false;
        }
    }

    private void PopulateTeamList()
    {
        // 더 이상 순위로 정렬하지 않고, 생성된 순서대로 사용합니다.
        foreach (Transform child in teamListContent) Destroy(child.gameObject);
        foreach (var team in teams)
        {
            GameObject obj = Instantiate(teamItemPrefab, teamListContent);
            TeamItemUI item = obj.GetComponent<TeamItemUI>();

            item.Init(team, OnTeamItemClicked);
        }

        // 루프용 클론 추가 (맨 앞 · 맨 뒤)
        int realCount = teamListContent.childCount;
        if (realCount > 0)
        {
            Transform first = teamListContent.GetChild(0);
            Transform last = teamListContent.GetChild(realCount - 1);

            // 마지막 카드 클론을 맨 앞에 삽입
            GameObject lastClone = Instantiate(last.gameObject, teamListContent);
            lastClone.name = lastClone.name + "_LoopClone";
            lastClone.transform.SetAsFirstSibling();

            // 첫 카드 클론을 맨 뒤에 삽입 (SetAsLastSibling는 기본이므로 그대로)
            GameObject firstClone = Instantiate(first.gameObject, teamListContent);
            firstClone.name = firstClone.name + "_LoopClone";
        }

        teamSelectConfirmButton.interactable = false;
        selectedTeam = null;

        // Content 크기 및 셀 크기를 Viewport 비율에 맞춰 조정
        UpdateTeamListContentWidth();

        // SnapScrollRect 페이지 재계산 (Content 폭이 확정된 뒤 실행해야 정확함)
        SnapScrollRect snap = teamListContent.GetComponentInParent<SnapScrollRect>();
        if (snap != null)
        {
            snap.RecalculatePages();
            // 한 프레임 뒤에 첫 페이지(1)로 이동해야 SnapScrollRect가 pagePositions를 계산 완료함
            StartCoroutine(SetInitialSnapPage(snap));
        }
    }

    /// <summary>
    /// 팀 카드 개수에 맞춰 Content(RectTransform) 가로 크기를 계산해서 적용한다.
    /// ContentSizeFitter를 쓰지 않고도 동일 효과를 얻는다.
    /// </summary>
    private void UpdateTeamListContentWidth()
    {
        GridLayoutGroup grid = teamListContent.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        // Viewport 기준으로 셀 크기 및 패딩 계산 (가로 70%, 세로 90%, 좌우 균등 패딩 → 카드 중앙 정렬)
        ScrollRect sr = teamListContent.GetComponentInParent<ScrollRect>();
        RectTransform viewport = (sr != null && sr.viewport != null) ? sr.viewport : sr?.transform as RectTransform;

        if (viewport == null) return;

        // DynamicGridCellSize에 의한 셀 크기 반영
        DynamicGridCellSize dyn = teamListContent.GetComponent<DynamicGridCellSize>();
        if (dyn != null) dyn.ForceUpdate();

        float targetCellWidth = grid.cellSize.x;
        float targetCellHeight = grid.cellSize.y;

        // Viewport가 셀보다 작을 때 음수 패딩이 되지 않도록 Clamp
        int sidePadding = Mathf.RoundToInt(Mathf.Max(0f, (viewport.rect.width - targetCellWidth) / 2f));
        grid.padding.left = sidePadding;
        grid.padding.right = sidePadding;

        int totalItems = teamListContent.childCount;

        float cell = grid.cellSize.x;
        float spacing = grid.spacing.x;

        float width = grid.padding.left + grid.padding.right + totalItems * cell + Mathf.Max(0, totalItems - 1) * spacing;

        RectTransform rect = teamListContent.GetComponent<RectTransform>();
        // 가로, 세로 크기 갱신 (세로는 Viewport와 동일)
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.rect.height);
    }

    private void OnTeamItemClicked(TeamData data)
    {
        selectedTeam = data;
        teamSelectConfirmButton.interactable = true;
    }

    private void OnTeamConfirmed()
    {
        if (selectedTeam == null) return;
        // 선택한 팀 ID를 PlayerPrefs에 저장하여 이후 씬에서 사용 가능
        PlayerPrefs.SetInt(SelectedTeamIdKey, selectedTeam.teamId);
        PlayerPrefs.Save();
        // 팀 선택 후 이름 입력 단계로 이동
        SwitchStep(Step.NameInput);
    }

    #endregion

    #region Team Intro

    private void PlayTeamIntro()
    {
        // 팀 관련 나레이션 예시
        string intro = $"{selectedTeam.teamName}의 새로운 감독이 된 당신! 선수들과 함께 우승을 향해 나아가세요.";
        teamIntroTyper.Play(intro, ShowTeamIntroContinue);
        // TODO: teamIntroImage.sprite = ... 팀별 이미지 설정
    }

    private void ShowPrologueContinue()
    {
        if (!prologueContinueButton) { SwitchStep(Step.TeamSelection); return; }
        prologueContinueButton.gameObject.SetActive(true);
        prologueContinueButton.onClick.RemoveAllListeners();
        prologueContinueButton.onClick.AddListener(() => SwitchStep(Step.TeamSelection));
    }

    private void ShowTeamIntroContinue()
    {
        if (!teamIntroContinueButton) { SceneManager.LoadScene(gameSceneName); return; }
        teamIntroContinueButton.gameObject.SetActive(true);
        teamIntroContinueButton.onClick.RemoveAllListeners();
        teamIntroContinueButton.onClick.AddListener(() => SceneManager.LoadScene(gameSceneName));
    }

    #endregion

    #region Dummy Data

    private void InitDummyTeams()
    {
        teams.Clear();

        // 포지션 목록 (농구)
        string[] positions = { "PG", "SG", "SF", "PF", "C" };

        // 랜덤 이름 풀 (간단히 숫자)
        int globalPlayerIndex = 1;

        System.Random sysRand = new System.Random();

        for (int teamIndex = 0; teamIndex < 30; teamIndex++)
        {
            // 팀명 및 줄임말
            string teamName = $"팀 {teamIndex + 1}";
            string abbreviation = string.Concat(Enumerable.Range(0, 3).Select(_ => (char)sysRand.Next('A', 'Z' + 1)));

            List<PlayerLine> playerLines = new List<PlayerLine>();

            // 15명 생성 (0-4 주전, 5-14 예비)
            for (int i = 0; i < 15; i++)
            {
                PlayerLine p = new PlayerLine();

                // 고유 이름 생성
                p.PlayerName = $"선수{globalPlayerIndex++}";

                // 기본 랜덤 값들
                p.BackNumber = UnityEngine.Random.Range(0, 100); // 0~99
                p.Position = positions[UnityEngine.Random.Range(0, positions.Length)];
                p.Age = UnityEngine.Random.Range(19, 41); // 19~40세

                // 키 (feet-inches 형식 "6-5")
                int foot = UnityEngine.Random.Range(5, 8); // 5~7피트
                int inch = UnityEngine.Random.Range(0, 12); // 0~11인치
                p.Height = $"{foot}-{inch}";

                p.Weight = UnityEngine.Random.Range(150, 301); // 150~300 파운드
                p.OverallScore = UnityEngine.Random.Range(0, 100);
                p.Potential = UnityEngine.Random.Range(0, 100);

                // 주전 5명(0~4)은 AssignedPosition 설정 (본인과 다를 수도 있음)
                if (i < 5)
                {
                    p.AssignedPosition = positions[UnityEngine.Random.Range(0, positions.Length)];
                }

                playerLines.Add(p);
            }

            TeamData teamData = new TeamData(teamIndex + 1, teamName, abbreviation, playerLines);

            teams.Add(teamData);
        }
    }

    #endregion

    private IEnumerator SetInitialSnapPage(SnapScrollRect snap)
    {
        // 최대 5프레임까지 대기하면서 pagePositions 계산 완료 기다림
        int safety = 5;
        while (safety-- > 0 && snap != null && (!snap.HasValidPages() || snap.CurrentPageCount < 2))
        {
            yield return null;
        }

        if (snap != null)
        {
            snap.JumpToPage(1, true);
        }
    }
} 