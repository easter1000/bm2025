// TradeSceneManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using madcamp3.Assets.Script.Player;
using UnityEngine.SceneManagement;

public class TradeSceneManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject playerTradeLinePrefab;
    [SerializeField] private Button backButton;

    [Header("Detail View")]
    [SerializeField] private PlayerDetailUI playerDetailUI;

    [Header("My Team UI References")]
    public Image myLogoImage; public TextMeshProUGUI myPlayersCountText; public TextMeshProUGUI myRemainBudgetText; public TextMeshProUGUI myCurrentBudgetText; [SerializeField] private Transform myPlayerScrollContent;

    [Header("Opponent Team UI References")]
    public Image oppLogoImage; public TextMeshProUGUI oppPlayersCountText; public TextMeshProUGUI oppRemainBudgetText; public TextMeshProUGUI oppCurrentBudgetText; [SerializeField] private Transform oppPlayerScrollContent;

    [Header("Confirm Buttons")]
    [SerializeField] private Image myTeamConfirmButton;
    [SerializeField] private TextMeshProUGUI myTeamConfirmText;
    [SerializeField] private Image oppTeamConfirmButton;
    [SerializeField] private TextMeshProUGUI oppTeamConfirmText;

    [Header("Trade Button")]
    [SerializeField] private Button tradeButton;

    [Header("Managers and Dialog")]
    [SerializeField] private ConfirmDialog confirmDialog;
    private TradeManager tradeManager;
    private string myTeamAbbr;
    private string oppTeamAbbr;

    private const int MaxRosterSize = 15;
    // 2) spawnedLines 리스트를 팀별로 분리하여 추가
    private readonly List<GameObject> spawnedLinesMy = new(); private readonly List<GameObject> spawnedLinesOpp = new();

    private bool isMyTeamConfirmed = false;
    private bool isOppTeamConfirmed = false;
    private readonly Color confirmEnabledColor = new Color(161f / 255f, 1f, 0f);
    private readonly Color confirmLockedColor = new Color(239f / 255f, 1f, 211f / 255f);
    private const string confirmText = "결정";
    private const string cancelText = "취소";
    private bool _hasStarted = false; // Start 이후 Initialize 호출 여부 플래그 추가

    private void OnEnable()
    {
        // Start 가 이미 한 번 실행된 뒤라면, 씬이 다시 Enable 될 때마다 UI 를 재초기화한다.
        // if (_hasStarted)
        // {
        //     Initialize();
        // }
    }

    private void Start()
    {
        tradeManager = FindFirstObjectByType<TradeManager>();
        if (tradeManager == null) Debug.LogError("TradeManager not found in scene!");
        if (confirmDialog == null) Debug.LogError("ConfirmDialog is not assigned!");

        if (myTeamConfirmButton != null) AddClickTrigger(myTeamConfirmButton.gameObject, OnMyTeamConfirmClicked);
        if (oppTeamConfirmButton != null) AddClickTrigger(oppTeamConfirmButton.gameObject, OnOppTeamConfirmClicked);
        if (tradeButton != null) tradeButton.onClick.AddListener(OnTradeButtonClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        Initialize();
        _hasStarted = true;
    }

    private void OnBackButtonClicked()
    {
        SceneManager.LoadScene("SeasonScene");
    }

    private void Initialize()
    {
        myTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;
        oppTeamAbbr = PlayerPrefs.GetString("TradeTargetTeamAbbr", string.Empty); if (string.IsNullOrEmpty(oppTeamAbbr) || oppTeamAbbr == myTeamAbbr) { Debug.LogWarning("[TradeSceneManager] 상대 팀 약어가 유효하지 않아 FA를 사용합니다"); oppTeamAbbr = "FA"; }

        SetupTeamUI(myTeamAbbr, myLogoImage, myPlayersCountText, myRemainBudgetText, myCurrentBudgetText, myPlayerScrollContent, spawnedLinesMy);
        SetupTeamUI(oppTeamAbbr, oppLogoImage, oppPlayersCountText, oppRemainBudgetText, oppCurrentBudgetText, oppPlayerScrollContent, spawnedLinesOpp);
        UpdateConfirmButtonsState();
        UpdateTradeButtonState(); // 초기 상태 설정

        // PlayerDetail 초기값 설정
        if (playerDetailUI != null && !string.IsNullOrEmpty(myTeamAbbr))
        {
            var myPlayers = LocalDbManager.Instance.GetPlayersByTeam(myTeamAbbr);
            var firstPlayer = myPlayers?.OrderByDescending(p => p.overallAttribute).FirstOrDefault();
            if (firstPlayer != null)
            {
                playerDetailUI.SetPlayer(firstPlayer);
            }
        }
    }

    private long CalculateTotalAnnualSalary(IEnumerable<PlayerRating> ratings)
    {
        long sum = 0;
        if (ratings == null) return sum;

        foreach (var pr in ratings)
        {
            PlayerStatus status = LocalDbManager.Instance.GetPlayerStatus(pr.player_id);
            if (status != null && status.YearsLeft > 0)
            {
                sum += status.Salary / status.YearsLeft;
            }
        }
        return sum;
    }

    // 4) SetupTeamUI(), PopulatePlayerScroll(), AdjustScrollContentHeight() 의 시그니처를 수정하여 팀별로 사용하도록 함
    private void SetupTeamUI(string teamAbbr, Image logoImg, TextMeshProUGUI playersCountTXT, TextMeshProUGUI remainTXT, TextMeshProUGUI currentTXT, Transform scrollContent, List<GameObject> dstList) {
        if (string.IsNullOrEmpty(teamAbbr)) {
            Debug.LogError("[TradeSceneManager] 팀 약어가 유효하지 않아 FA를 사용합니다");
            teamAbbr = "FA";
        }

        if (logoImg) {
            string logoName = (teamAbbr == "FA") ? "free" : teamAbbr.ToLower();
            Sprite s = Resources.Load<Sprite>($"team_photos/{logoName}");
            if (s) {
                logoImg.sprite = s;
            } else {
                Debug.LogWarning($"[TradeSceneManager] 로고 파일을 찾을 수 없습니다: team_photos/{logoName}");
            }
        }

        List<PlayerRating> players = LocalDbManager.Instance.GetPlayersByTeam(teamAbbr);
        
        IEnumerable<PlayerRating> displayPlayers = players;
        if (teamAbbr != "FA")
        {
            displayPlayers = players.Where(p => {
                var status = LocalDbManager.Instance.GetPlayerStatus(p.player_id);
                return status != null && !status.IsInjured;
            });
        }
        
        PopulatePlayerScroll(displayPlayers, teamAbbr, scrollContent, dstList);

        if (teamAbbr == "FA")
        {
            if (playersCountTXT) playersCountTXT.text = $"{players?.Count ?? 0}";
            if (currentTXT) currentTXT.text = "$ 0";
            if (remainTXT) remainTXT.text = "$ 0";
        }
        else
        {
            if (playersCountTXT) playersCountTXT.text = $"{players?.Count ?? 0} / {MaxRosterSize}";
            TeamFinance finance = LocalDbManager.Instance.GetTeamFinance(teamAbbr, 2025);
            if (finance != null) {
                long currentBudget = finance.TeamBudget; 
                long remainBudget = currentBudget - CalculateTotalAnnualSalary(players);
                if (currentTXT) currentTXT.text = FormatMoney(currentBudget);
                if (remainTXT) remainTXT.text = FormatMoney(remainBudget);
            }
        }
    }

    private void PopulatePlayerScroll(IEnumerable<PlayerRating> players, string teamAbbr, Transform content, List<GameObject> dstList)
    {
        if (content == null || playerTradeLinePrefab == null) return;

        // ContentSizeFitter를 설정하여 컨텐츠 높이를 자동으로 조절하도록 합니다.
        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var go in dstList)
        {
            if (go != null)
            {
                var line = go.GetComponent<PlayerTradeLine>();
                if (line != null) line.OnSelectionChanged -= UpdateConfirmButtonsState;
                Destroy(go);
            }
        }
        dstList.Clear();

        if (players == null) return;

        foreach (var pr in players.Where(p => p != null).OrderByDescending(p => p.overallAttribute))
        {
            GameObject go = Instantiate(playerTradeLinePrefab, content);
            go.transform.localScale = Vector3.one;

            // LayoutElement를 추가/설정하여 높이를 명시적으로 지정
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();

            if (le != null)
            {
                le.preferredHeight = 60f; // 개별 아이템의 높이를 60으로 고정
                le.minHeight = 60f;
            }

            PlayerTradeLine line = go.GetComponent<PlayerTradeLine>();
            if (line != null)
            {
                PlayerStatus status = LocalDbManager.Instance.GetPlayerStatus(pr.player_id);
                line.Setup(pr, status, teamAbbr);
                line.OnLineClicked += ShowPlayerDetail;

                // 상대가 FA일 경우 내 선수 선택을 막는다.
                if (teamAbbr == myTeamAbbr && oppTeamAbbr == "FA")
                {
                    line.SetLocked(true); 
                }
                else
                {
                    line.OnSelectionChanged += UpdateConfirmButtonsState;
                }
            }
            dstList.Add(go);
        }
    }

    public void ShowPlayerDetail(PlayerRating rating)
    {
        if (playerDetailUI == null || rating == null) return;
        
        playerDetailUI.SetPlayer(rating);
        playerDetailUI.gameObject.SetActive(true);
    }

    private void AddClickTrigger(GameObject obj, UnityEngine.Events.UnityAction action)
    {
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = obj.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => { action.Invoke(); });
        trigger.triggers.Add(entry);
    }

    private void OnMyTeamConfirmClicked()
    {
        if (isMyTeamConfirmed)
        {
            isMyTeamConfirmed = false;
        }
        else
        {
            bool anySelected = spawnedLinesMy.Any(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false);
            if (!anySelected) return;
            isMyTeamConfirmed = true;
        }

        foreach (var go in spawnedLinesMy)
        {
            go.GetComponent<PlayerTradeLine>()?.SetLocked(isMyTeamConfirmed);
        }
        UpdateConfirmButtonsState();
    }

    private void OnOppTeamConfirmClicked()
    {
        if (isOppTeamConfirmed)
        {
            isOppTeamConfirmed = false;
        }
        else
        {
            bool anySelected = spawnedLinesOpp.Any(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false);
            if (!anySelected) return;
            isOppTeamConfirmed = true;
        }

        foreach (var go in spawnedLinesOpp)
        {
            go.GetComponent<PlayerTradeLine>()?.SetLocked(isOppTeamConfirmed);
        }
        UpdateConfirmButtonsState();
    }

    private void UpdateConfirmButtonsState()
    {
        UpdateSingleButtonState(isMyTeamConfirmed,
            spawnedLinesMy.Any(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false),
            myTeamConfirmButton, myTeamConfirmText);

        UpdateSingleButtonState(isOppTeamConfirmed,
            spawnedLinesOpp.Any(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false),
            oppTeamConfirmButton, oppTeamConfirmText);
        
        UpdateTradeButtonState();
    }

    private void UpdateSingleButtonState(bool isConfirmed, bool anySelected, Image button, TextMeshProUGUI text)
    {
        if (button == null) return;

        if (isConfirmed)
        {
            button.color = confirmLockedColor;
            if (text != null)
            {
                text.text = cancelText;
                text.color = Color.black;
            }
        }
        else
        {
            button.color = anySelected ? confirmEnabledColor : Color.gray;
            if (text != null)
            {
                text.text = confirmText;
                text.color = Color.white;
            }
        }
    }

    private void UpdateTradeButtonState()
    {
        if (tradeButton == null) return;

        bool isOpponentFA = (oppTeamAbbr == "FA");

        int mySelectedCount = spawnedLinesMy.Count(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false);
        int oppSelectedCount = spawnedLinesOpp.Count(go => go.GetComponent<PlayerTradeLine>()?.IsSelected ?? false);
        int myCurrentTotal = spawnedLinesMy.Count;
        int oppCurrentTotal = spawnedLinesOpp.Count;

        bool rosterSizesOk = (myCurrentTotal - mySelectedCount + oppSelectedCount) <= MaxRosterSize;
        if (!isOpponentFA)
        {
            rosterSizesOk &= (oppCurrentTotal - oppSelectedCount + mySelectedCount) <= MaxRosterSize;
        }

        if (isOpponentFA)
        {
            // FA 영입 시: 내 팀 선수 선택 불가, 상대 팀만 확정되면 가능
            bool isReady = isOppTeamConfirmed && mySelectedCount == 0;
            tradeButton.interactable = isReady && rosterSizesOk;
        }
        else
        {
            // 일반 트레이드 시: 양 팀 모두 확정 필요
            bool isReady = isMyTeamConfirmed && isOppTeamConfirmed;
            tradeButton.interactable = isReady && rosterSizesOk;
        }
    }

    private void OnTradeButtonClicked()
    {
        var mySelectedPlayers = GetSelectedPlayers(spawnedLinesMy);
        var oppSelectedPlayers = GetSelectedPlayers(spawnedLinesOpp);

        if (oppTeamAbbr == "FA")
        {
            HandleFreeAgentSigning(oppSelectedPlayers);
            return;
        }

        if (mySelectedPlayers.Count == 0 && oppSelectedPlayers.Count == 0)
        {
            confirmDialog.Show("트레이드할 선수를 선택해주세요.", () => { }, null);
            return;
        }

        // 4. 트레이드 평가
        var result = tradeManager.EvaluateTrade(
            myTeamAbbr, mySelectedPlayers,
            oppTeamAbbr, oppSelectedPlayers,
            new System.Random() // 트레이드 평가를 위한 랜덤 인스턴스 전달
        );

        // 5. 결과 처리
        if (result.IsAccepted && result.RequiredCash == 0)
        {
            // 즉시 수락
            confirmDialog.Show("상대방이 제안을 흔쾌히 수락합니다. 거래하시겠습니까?", () => FinalizeTrade(mySelectedPlayers, oppSelectedPlayers, 0), () => {});
        }
        else if (result.IsAccepted)
        {
            // 추가 금액 요구
            string message = $"상대 팀이 트레이드를 위해 추가로 ${result.RequiredCash:N0}를 요구합니다. 수락하시겠습니까?";
            confirmDialog.Show(message,
                onYes: () =>
                {
                    var myFinance = LocalDbManager.Instance.GetTeamFinance(myTeamAbbr, 2025);
                    if (myFinance.TeamBudget >= result.RequiredCash)
                    {
                        // TODO: 예산 차감 로직 추가
                        FinalizeTrade(mySelectedPlayers, oppSelectedPlayers, result.RequiredCash);
                    }
                    else
                    {
                        confirmDialog.Show("예산이 부족하여 추가 금액을 지불할 수 없습니다.", () => {}, () => {});
                    }
                },
                onNo: () => { }
            );
        }
        else
        {
            confirmDialog.Show("트레이드 제안이 거절되었습니다.", () => { }, null);
        }
    }

    private void HandleFreeAgentSigning(List<PlayerRating> faPlayersToSign)
    {
        if (faPlayersToSign.Count == 0)
        {
            confirmDialog.Show("영입할 선수를 선택해주세요.", () => {}, null);
            return;
        }

        var myFinance = LocalDbManager.Instance.GetTeamFinance(myTeamAbbr, 2025);
        var myPlayers = LocalDbManager.Instance.GetPlayersByTeam(myTeamAbbr);

        // 1. 로스터 인원 체크
        if (myPlayers.Count + faPlayersToSign.Count > MaxRosterSize)
        {
            confirmDialog.Show($"로스터는 최대 {MaxRosterSize}명까지 가능합니다. (현재: {myPlayers.Count}명, 영입 시도: {faPlayersToSign.Count}명)", () => {}, null);
            return;
        }

        // 2. 예산 체크
        long totalSalaryOfNewPlayers = CalculateTotalAnnualSalary(faPlayersToSign);
        long currentTeamSalary = CalculateTotalAnnualSalary(myPlayers);
        
        if (currentTeamSalary + totalSalaryOfNewPlayers > myFinance.TeamBudget)
        {
            confirmDialog.Show("예산이 부족하여 모든 선수를 영입할 수 없습니다.", () => {}, null);
            return;
        }

        // 3. 영입 성공
        string playerNames = string.Join(", ", faPlayersToSign.Select(p => p.name));
        string message = $"다음 선수들을 영입하시겠습니까?\n({playerNames})";
        
        confirmDialog.Show(message,
            onYes: () => {
                FinalizeTrade(new List<PlayerRating>(), faPlayersToSign, 0);
            },
            onNo: () => {}
        );
    }

    private void FinalizeTrade(List<PlayerRating> myPlayers, List<PlayerRating> oppPlayers, long cashPaid)
    {
        // 1. 선수 팀 정보 업데이트
        var db = LocalDbManager.Instance;
        db.UpdatePlayerTeam(myPlayers.Select(p => p.player_id).ToList(), oppTeamAbbr);
        db.UpdatePlayerTeam(oppPlayers.Select(p => p.player_id).ToList(), myTeamAbbr);

        // FA 영입일 경우, 각 선수에게 새로운 계약 할당
        if (oppTeamAbbr == "FA")
        {
            foreach (var player in oppPlayers)
            {
                db.AssignContractToPlayer(player.player_id);
            }
        }

        // 2. 재정 정보 업데이트
        if (cashPaid > 0) // result.RequiredCash 사용
        {
            var myFinance = db.GetTeamFinance(myTeamAbbr, 2025);
            myFinance.TeamBudget -= cashPaid; // result.RequiredCash 사용
            db.UpdateTeamFinance(myFinance);
        }

        // 3. 연봉 총액 재계산
        db.RecalculateAndSaveAllTeamSalaries();

        // 4. UI 새로고침
        Initialize();
        
        // 5. 성공 다이얼로그 표시
        confirmDialog.Show("트레이드가 성공적으로 성사되었습니다!", () => {
            // OK 버튼 누르면 아무것도 안함
        });
    }

    private List<PlayerRating> GetSelectedPlayers(List<GameObject> spawnedLines)
    {
        var selectedPlayers = new List<PlayerRating>();
        foreach(var go in spawnedLines)
        {
            var line = go.GetComponent<PlayerTradeLine>();
            if (line != null && line.IsSelected)
            {
                // We need to get the full PlayerRating object. Let's assume the line stores the ID.
                var rating = LocalDbManager.Instance.GetPlayerRating(line.GetComponent<PlayerTradeLine>().GetPlayerID());
                if (rating != null)
                {
                    selectedPlayers.Add(rating);
                }
            }
        }
        return selectedPlayers;
    }

    private string FormatMoney(long amount)
    {
        if (amount < 0) {
            return "-" + FormatMoney(-amount);
        }

        string unit = string.Empty;
        double value = amount;

        if (amount >= 1_000_000_000)
        {
            unit = "B";
            value = amount / 1_000_000_000.0;
        }
        else if (amount >= 1_000_000)
        {
            unit = "M";
            value = amount / 1_000_000.0;
        }
        else if (amount >= 1_000)
        {
            unit = "K";
            value = amount / 1_000.0;
        }
        else
        {
            return $"$ {amount:N0}";
        }

        int intDigits = value >= 1 ? (int)Math.Floor(Math.Log10(value) + 1) : 1;
        int decimals = Mathf.Max(0, 4 - intDigits);
        string format = $"F{decimals}";
        string strVal = value.ToString(format).TrimEnd('0').TrimEnd('.');

        return $"$ {strVal}{unit}";
    }
} 