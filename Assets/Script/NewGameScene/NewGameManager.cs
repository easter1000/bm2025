using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq; // 추가: 팀 정렬에 사용
using System.Collections; // 추가: 코루틴 사용
using System.Reflection; // LocalDbManager private method 호출용
using System; // 날짜 처리 및 User 저장용
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

    [Header("Dialogs")]
    [SerializeField] private ConfirmDialog confirmDialog;

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
        // Confirm dialog will handle confirmation; no global confirm button
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
        int basePad = Mathf.RoundToInt(Mathf.Max(0f, (viewport.rect.width - targetCellWidth) / 2f));
        int extraPad = 10; // 요청: 양 옆 10px 추가
        grid.padding.left = basePad + extraPad;
        grid.padding.right = basePad + extraPad;

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
        if (confirmDialog != null)
        {
            confirmDialog.Show($"{data.teamName} 팀을 선택하시겠습니까?", () => OnTeamConfirmed(), null);
        }
    }

    private void OnTeamConfirmed()
    {
        if (selectedTeam == null) return;
        
        // 1. 유저 정보 저장
        string coachName = coachNameInputField.text;
        string teamAbbr = selectedTeam.abbreviation;
        int season = 2025;
        LocalDbManager.Instance.SaveOrUpdateUser(coachName, teamAbbr, season);
        
        // 2. [핵심 추가] 모든 팀의 로스터를 15명으로 조정
        RosterManager.AdjustAllRostersToSeasonStart();

        // 3. 팀 인트로 단계로 전환
        SwitchStep(Step.TeamIntro);
    }

    #endregion

    #region Team Intro

    private void PlayTeamIntro()
    {
        string intro = $"{selectedTeam.teamName}의 새로운 감독이 된 당신! 선수들과 함께 우승을 향해 나아가세요.";
        teamIntroTyper.Play(intro, ShowTeamIntroContinue);
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
        if (!teamIntroContinueButton)
        {
            SaveUserData();
            InitializeSeason(2025); // 시즌 시작 전 스케줄 생성
            SceneManager.LoadScene(gameSceneName);
            return;
        }
        teamIntroContinueButton.gameObject.SetActive(true);
        teamIntroContinueButton.onClick.RemoveAllListeners();
        teamIntroContinueButton.onClick.AddListener(() => {
            SaveUserData();
            InitializeSeason(2025); // 시즌 시작 전 스케줄 생성
            SceneManager.LoadScene(gameSceneName);
        });
    }

    #endregion

    // --- NEW: Save user information to the local database ---
    private void SaveUserData()
    {
        if (selectedTeam == null)
        {
            Debug.LogWarning("[NewGameManager] SaveUserData called but selectedTeam is null.");
            return;
        }

        string coachName = coachNameInputField != null ? coachNameInputField.text : string.Empty;
        string teamAbbr = selectedTeam.abbreviation;
        int season = 2025;

        LocalDbManager.Instance.SaveOrUpdateUser(coachName, teamAbbr, season);
    }

    // 새 시즌을 초기화하고 스케줄을 생성한다.
    private void InitializeSeason(int season)
    {
        SeasonManager sm = SeasonManager.Instance;
        if (sm == null)
        {
            GameObject obj = new GameObject("SeasonManager");
            sm = obj.AddComponent<SeasonManager>();
        }
        sm.StartNewSeason(season);
    }

    #region Dummy Data

    private void InitDummyTeams()
    {
        teams.Clear();

        var teamEntities = LocalDbManager.Instance.GetAllTeams();
        if (teamEntities == null || teamEntities.Count == 0)
        {
            Debug.LogError("[NewGameManager] Team 테이블을 불러오지 못했습니다.");
            return;
        }

        foreach (var teamEntity in teamEntities)
        {
            string teamName = teamEntity.team_name;
            string abbr = teamEntity.team_abbv;

            // 모든 선수 목록 로드 (팀 기준)
            var allPlayers = LocalDbManager.Instance.GetPlayersByTeam(abbr);
            if (allPlayers == null || allPlayers.Count < 5) continue;

            // best_five 문자열은 PG,SG,SF,PF,C 순서대로 player_id가 쉼표로 구분되어 있습니다.
            // 각 player_id → AssignedPosition 매핑을 만든 뒤 HashSet 도 함께 준비합니다.
            Dictionary<int, string> idToAssignedPos = new();
            HashSet<int> starterIds = new();
            if (!string.IsNullOrEmpty(teamEntity.best_five))
            {
                string[] parts = teamEntity.best_five.Split(',');
                for (int i = 0; i < parts.Length && i < 5; i++)
                {
                    if (int.TryParse(parts[i], out int pid))
                    {
                        starterIds.Add(pid);
                        // i (0~4) → position code (1~5)
                        idToAssignedPos[pid] = PositionCodeToString(i + 1);
                    }
                }
            }

            List<PlayerLine> starters = new List<PlayerLine>();
            List<PlayerLine> bench = new List<PlayerLine>();

            foreach (var pr in allPlayers)
            {
                PlayerLine pl = new PlayerLine
                {
                    PlayerName = pr.name,
                    Position = PositionCodeToString(pr.position),
                    BackNumber = pr.backNumber,
                    Age = pr.age,
                    Height = pr.height,
                    Weight = pr.weight,
                    OverallScore = pr.overallAttribute,
                    Potential = pr.potential,
                    PlayerId = pr.player_id,
                    AssignedPosition = idToAssignedPos.ContainsKey(pr.player_id) ? idToAssignedPos[pr.player_id] : null
                };

                if (starterIds.Contains(pr.player_id) && starters.Count < 5)
                {
                    starters.Add(pl);
                }
                else
                {
                    bench.Add(pl);
                }
            }

            // Fallback: if starters <5 fill with best bench players
            if (starters.Count < 5)
            {
                var add = bench.OrderByDescending(p => p.OverallScore).Take(5 - starters.Count).ToList();
                foreach (var pl in add)
                {
                    pl.AssignedPosition = pl.Position;
                    starters.Add(pl);
                    bench.Remove(pl);
                }
            }

            // combine lists starters first then bench
            List<PlayerLine> playerLines = new List<PlayerLine>();
            playerLines.AddRange(starters);
            playerLines.AddRange(bench);

            TeamData teamData = new TeamData(teamEntity.team_id, teamName, abbr, playerLines);
            teams.Add(teamData);
        }
    }

    private string PositionCodeToString(int code)
    {
        return code switch
        {
            1 => "PG",
            2 => "SG",
            3 => "SF",
            4 => "PF",
            5 => "C",
            _ => "E"
        };
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