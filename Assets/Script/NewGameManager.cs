using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq; // 추가: 팀 정렬에 사용

public class NewGameManager : MonoBehaviour
{
    private enum Step
    {
        Prologue,
        NameInput,
        TeamSelection,
        TeamIntro
    }

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
    public TeamDetailUI teamDetailUI;
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
        SwitchStep(Step.Prologue);
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
        // TODO: 저장하거나 GameManager에 전달
        SwitchStep(Step.TeamSelection);
    }

    #endregion

    #region Team Selection

    // 추가: ScrollView Content 에 GridLayoutGroup 설정
    private void ConfigureTeamListLayout()
    {
        if (teamListLayoutConfigured || teamListContent == null) return;

        GridLayoutGroup grid = teamListContent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = teamListContent.gameObject.AddComponent<GridLayoutGroup>();

        // 프리팹 사이즈를 셀 크기로 사용
        if (teamItemPrefab != null)
        {
            RectTransform prefabRect = teamItemPrefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                grid.cellSize = prefabRect.sizeDelta;
            }
        }

        // 가로 슬라이드: 한 행(Row)만 두고 셀을 좌우로 나열
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount; // 행 개수 고정 = 1
        grid.constraintCount = 1;
        grid.startAxis = GridLayoutGroup.Axis.Vertical; // 열을 우측으로 확장
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.MiddleLeft;
        grid.spacing = new Vector2(20f, 0f);

        // ContentSizeFitter 추가: 아이템 개수에 따라 Content 너비 자동 확장
        ContentSizeFitter fitter = teamListContent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = teamListContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

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
        // 1) 순위 기준으로 정렬
        teams = teams.OrderBy(t => t.currentRank).ToList();

        foreach (Transform child in teamListContent) Destroy(child.gameObject);
        foreach (var team in teams)
        {
            var item = Instantiate(teamItemPrefab, teamListContent).GetComponent<TeamItemUI>();
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
        teamDetailUI.Hide();

        // SnapScrollRect 페이지 재계산
        SnapScrollRect snap = teamListContent.GetComponentInParent<SnapScrollRect>();
        if (snap != null) snap.RecalculatePages();
    }

    private void OnTeamItemClicked(TeamData data)
    {
        selectedTeam = data;
        teamDetailUI.Show(data);
        teamSelectConfirmButton.interactable = true;
    }

    private void OnTeamConfirmed()
    {
        if (selectedTeam == null) return;
        // 선택한 팀 ID를 PlayerPrefs에 저장하여 이후 씬에서 사용 가능
        PlayerPrefs.SetInt(SelectedTeamIdKey, selectedTeam.teamId);
        PlayerPrefs.Save();
        SwitchStep(Step.TeamIntro);
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
        if (!prologueContinueButton) { SwitchStep(Step.NameInput); return; }
        prologueContinueButton.gameObject.SetActive(true);
        prologueContinueButton.onClick.RemoveAllListeners();
        prologueContinueButton.onClick.AddListener(() => SwitchStep(Step.NameInput));
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

        // 1) 450명 선수 풀 생성 후 셔플
        List<string> playerPool = Enumerable.Range(1, 450)
            .Select(n => $"선수{n}")
            .OrderBy(_ => Random.value)
            .ToList();

        int playersPerTeam = playerPool.Count / 30; // 15명

        // 2) 1~30위 순위를 랜덤 섞기
        List<int> shuffledRanks = Enumerable.Range(1, 30)
            .OrderBy(_ => Random.value)
            .ToList();

        // 3) 0.1~0.9 사이 승률 30개 랜덤 생성 후 내림차순 정렬 (높은 값이 1위)
        List<float> winRates = new List<float>();
        for (int i = 0; i < 30; i++) winRates.Add(Random.Range(0.1f, 0.9f));
        winRates.Sort((a, b) => b.CompareTo(a)); // 내림차순

        // 4) 팀 1~30 생성
        for (int i = 0; i < 30; i++)
        {
            // 선수 배정 (중복X)
            string[] players = playerPool.Skip(i * playersPerTeam).Take(playersPerTeam).ToArray();

            int rank = shuffledRanks[i];
            float winRate = winRates[rank - 1]; // 1위 = index 0 => 최대 승률

            teams.Add(new TeamData(
                i + 1,
                $"팀 {i + 1}",
                players,
                rank,
                winRate));
        }
    }

    #endregion
} 