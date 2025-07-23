using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Panel References")]
    public GameObject playerStatPanel;

    // [추가] 스탯 패널 내부의 UI 요소들을 연결할 변수들
    [Header("Player Stat Panel Elements")]
    public Image statPanel_PhotoImage;
    public TextMeshProUGUI statPanel_PlayerName;
    public TextMeshProUGUI statPanel_Points;
    public TextMeshProUGUI statPanel_Rebounds;
    public TextMeshProUGUI statPanel_Assists;
    public TextMeshProUGUI statPanel_Stamina;
    
    [Header("Scoreboard References")]
    public TextMeshProUGUI homeTeamNameText;
    public TextMeshProUGUI awayTeamNameText;
    public TextMeshProUGUI homeScoreText;
    public TextMeshProUGUI awayScoreText;
    public TextMeshProUGUI gameClockText;
    public TextMeshProUGUI periodText; // quarterText에서 이름 변경
    public Image homeTeamLogo; // 새로 추가
    public Image awayTeamLogo; // 새로 추가
    public Image homeTeamBackground; // 새로 추가
    public Image awayTeamBackground; // 새로 추가

    [Header("Game Log UI")]
    public GameLogUI gameLogUI; // 새로 추가

    [Header("Court & Puck References")]
    public Transform courtPanel;
    public Transform homeBenchPanel; // [추가] 홈팀 벤치 패널
    public Transform awayBenchPanel; // [추가] 어웨이팀 벤치 패널

    [Header("Auto-Sub Toggle")]
    public Toggle autoSubToggle;
    public Animator autoSubToggleAnimator; // 토글 애니메이터
    public GameObject autoSubText;       // "AUTO" 텍스트 GameObject

    [Header("Game Simulator")]
    [SerializeField] private GameSimulator gameSimulator;

    [Header("Court & Puck References")]
    public PlayerPuck playerPuckPrefab; // [추가] 프리팹 변수 복구

    private Dictionary<int, PlayerPuck> _playerPucks = new Dictionary<int, PlayerPuck>();
    private Dictionary<string, Color> _teamColors = new Dictionary<string, Color>();
    private int _userTeamId = -1; // [추가]

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
        InitializeTeamColors();

        // [추가]
        if (gameSimulator != null)
        {
            _userTeamId = gameSimulator.GetUserTeamId(); // GameSimulator로부터 유저 팀 ID 가져오기
        }

        if (autoSubToggle != null)
        {
            autoSubToggle.onValueChanged.AddListener(OnAutoSubToggleChanged);
        }
    }

    void Start()
    {
        // gameSimulator가 Inspector에서 할당되지 않은 경우에만 찾도록 수정
        if (gameSimulator == null)
        {
            gameSimulator = FindFirstObjectByType<GameSimulator>();
            if (gameSimulator == null)
            {
                Debug.LogError("UIManager cannot find GameSimulator!");
            }
        }

        // Auto Sub Toggle 초기 설정
        if (autoSubToggle != null)
        {
            // Animator 컴포넌트 자동 할당
            if (autoSubToggleAnimator == null)
            {
                autoSubToggleAnimator = autoSubToggle.GetComponent<Animator>();
            }
            autoSubToggle.onValueChanged.AddListener(OnAutoSubToggleChanged);
            // 초기 상태 반영
            OnAutoSubToggleChanged(autoSubToggle.isOn);
        }
    }

    void OnEnable()
    {
        GameSimulator.OnGameStateUpdated += UpdateScoreboard;
        GameSimulator.OnPlayerSubstituted += UpdatePlayerPuck;
        GameSimulator.OnUILogGenerated += HandleUILog; // 새로 추가
    }

    void OnDisable()
    {
        GameSimulator.OnGameStateUpdated -= UpdateScoreboard;
        GameSimulator.OnPlayerSubstituted -= UpdatePlayerPuck;
        GameSimulator.OnUILogGenerated -= HandleUILog; // 새로 추가
    }
    
    private void OnAutoSubToggleChanged(bool isOn)
    {
        if (gameSimulator == null)
        {
            gameSimulator = FindObjectOfType<GameSimulator>();
        }

        if (gameSimulator != null)
        {
            gameSimulator.IsUserTeamAutoSubbed = isOn;
        }

        // 애니메이터 파라미터 설정
        if (autoSubToggleAnimator != null)
        {
            autoSubToggleAnimator.SetBool("IsOn", isOn);
        }

        // AUTO 텍스트 활성화/비활성화
        if (autoSubText != null)
        {
            autoSubText.SetActive(isOn);
        }
    }

    // [추가] PlayerPuck이 호출할 수 있도록 GetUserTeamId 메서드 추가
    public int GetUserTeamId()
    {
        return _userTeamId;
    }

    public void RequestManualSubstitution(PlayerPuck playerInPuck, PlayerPuck playerOutPuck)
    {
        if (gameSimulator != null)
        {
            gameSimulator.RequestManualSubstitution(playerInPuck.Player, playerOutPuck.Player);
        }
    }

    public void InitializePlayerPucks(List<GamePlayer> homeRoster, List<GamePlayer> awayRoster)
    {
        // 기존 퍽들 모두 제거
        foreach (var puck in _playerPucks.Values)
        {
            Destroy(puck.gameObject);
        }
        _playerPucks.Clear();

        // [수정] TeamColors 클래스 대신 내부 GetTeamColor 메서드 사용
        // 홈팀 선수들 배치
        foreach (var player in homeRoster)
        {
            Transform parentPanel = player.IsOnCourt ? courtPanel : homeBenchPanel;
            // 유저팀이 어웨이팀이면, 벤치 선수들은 생성하지 않음 (UI 간소화)
            if (parentPanel == homeBenchPanel && _userTeamId != 0) continue;

            GameObject puckObj = Instantiate(playerPuckPrefab.gameObject, parentPanel);
            PlayerPuck puckComponent = puckObj.GetComponent<PlayerPuck>();
            if (puckComponent != null)
            {
                puckComponent.Setup(player, GetTeamColor(gameSimulator.CurrentState.HomeTeamAbbr));
                _playerPucks.Add(player.Rating.player_id, puckComponent);
            }
        }

        // 어웨이팀 선수들 배치
        foreach (var player in awayRoster)
        {
            Transform parentPanel = player.IsOnCourt ? courtPanel : awayBenchPanel;
            // 유저팀이 홈팀이면, 어웨이 벤치 선수들은 생성하지 않음
            if (parentPanel == awayBenchPanel && _userTeamId != 1) continue;
            
            // AI팀의 경우 코트 위 선수만 표시
            if (!player.IsOnCourt) continue;

            GameObject puckObj = Instantiate(playerPuckPrefab.gameObject, parentPanel);
            PlayerPuck puckComponent = puckObj.GetComponent<PlayerPuck>();
            if (puckComponent != null)
            {
                puckComponent.Setup(player, GetTeamColor(gameSimulator.CurrentState.AwayTeamAbbr));
                _playerPucks.Add(player.Rating.player_id, puckComponent);
            }
        }
    }
    
    // [수정됨] SetTeamNames -> SetUpScoreboard 로 기능 확장 및 변경
    public void SetUpScoreboard(Team homeTeam, Team awayTeam)
    {
        // 1. 팀 이름을 약어(abbv)로 설정
        homeTeamNameText.text = homeTeam.team_abbv;
        awayTeamNameText.text = awayTeam.team_abbv;

        // 2. 팀 로고 설정 (Resources/team_photos/ 폴더에서 약어로 로드)
        homeTeamLogo.sprite = Resources.Load<Sprite>($"team_photos/{homeTeam.team_abbv}");
        awayTeamLogo.sprite = Resources.Load<Sprite>($"team_photos/{awayTeam.team_abbv}");

        // 3. 팀 배경색 설정
        Color color;
        if (ColorUtility.TryParseHtmlString(homeTeam.team_color, out color))
        {
            homeTeamBackground.color = color;
        }
        if (ColorUtility.TryParseHtmlString(awayTeam.team_color, out color))
        {
            awayTeamBackground.color = color;
        }
    }
    

    public void SetTeamNames(string homeName, string awayName)
    {
        homeTeamNameText.text = homeName;
        awayTeamNameText.text = awayName;
    }
    
    // [수정됨] 경기 시간 및 쿼터 표시 방식 변경
    private void UpdateScoreboard(GameState state)
    {
        if (state == null) return;
        homeScoreText.text = state.HomeScore.ToString();
        awayScoreText.text = state.AwayScore.ToString();

        // 쿼터 표시를 1Q, 2Q, 3Q, 4Q, OT1, OT2... 로 변경
        if (state.Quarter <= 4)
        {
            periodText.text = $"{state.Quarter}Q";
        }
        else
        {
            periodText.text = $"OT{state.Quarter - 4}";
        }
        
        // 게임 및 샷 클락 시간 업데이트
        float clock = Mathf.Max(0, state.GameClockSeconds);
        gameClockText.text = $"{(int)clock / 60:00}:{(int)clock % 60:00}";
    }
    
    private void UpdatePlayerPuck(GamePlayer playerOut, GamePlayer playerIn)
    {
        // 유저팀의 교체인 경우: 퍽의 부모를 바꿔 위치를 스왑
        if (playerIn.TeamId == _userTeamId)
        {
            if (_playerPucks.ContainsKey(playerOut.Rating.player_id) && _playerPucks.ContainsKey(playerIn.Rating.player_id))
            {
                PlayerPuck puckOut = _playerPucks[playerOut.Rating.player_id];
                PlayerPuck puckIn = _playerPucks[playerIn.Rating.player_id];

                // 두 퍽의 부모 트랜스폼을 교환
                Transform puckOutParent = puckOut.transform.parent;
                puckOut.transform.SetParent(puckIn.transform.parent);
                puckIn.transform.SetParent(puckOutParent);

                // 위치 초기화 (GridLayoutGroup 등 자동 정렬 레이아웃을 위해)
                puckOut.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                puckIn.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }
        }
        else // AI팀의 교체인 경우: 나가는 퍽은 비활성화, 들어오는 퍽은 새로 생성
        {
            // 1. 코트에서 나가는 선수 퍽 제거
            if (_playerPucks.ContainsKey(playerOut.Rating.player_id))
            {
                Destroy(_playerPucks[playerOut.Rating.player_id].gameObject);
                _playerPucks.Remove(playerOut.Rating.player_id);
            }

            // [수정] TeamColors 클래스 대신 내부 GetTeamColor 메서드 사용
            GameObject puckObj = Instantiate(playerPuckPrefab.gameObject, courtPanel);
            PlayerPuck puckComponent = puckObj.GetComponent<PlayerPuck>();
            if (puckComponent != null)
            {
                puckComponent.Setup(playerIn, GetTeamColor(gameSimulator.CurrentState.AwayTeamAbbr));
                _playerPucks.Add(playerIn.Rating.player_id, puckComponent);
            }
        }
    }

    // [핵심 업데이트] 선수 원 클릭 시 호출될 함수의 내용을 구체적으로 구현
    public void ShowPlayerStats(GamePlayer player)
    {
        if (playerStatPanel == null)
        {
            Debug.LogWarning("PlayerStatPanel이 UIManager에 연결되지 않았습니다.");
            return;
        }

        playerStatPanel.SetActive(true);

        // 1. 선수 사진 로딩 및 설정
        if (statPanel_PhotoImage != null)
        {
            string path = $"player_photos/{player.Rating.player_id}";
            Sprite playerSprite = Resources.Load<Sprite>(path);

            if (playerSprite == null)
            {
                playerSprite = Resources.Load<Sprite>("player_photos/default_image");
            }
            statPanel_PhotoImage.sprite = playerSprite;
        }
        
        // 2. 연결된 텍스트 변수들에 선수의 실시간 스탯 정보를 채워넣음
        statPanel_PlayerName.text = player.Rating.name;
        statPanel_Points.text = $"득점: {player.Stats.Points}";
        
        int totalRebounds = player.Stats.OffensiveRebounds + player.Stats.DefensiveRebounds;
        statPanel_Rebounds.text = $"리바운드: {totalRebounds}";
        
        statPanel_Assists.text = $"어시스트: {player.Stats.Assists}";
        
        statPanel_Stamina.text = $"체력: {(int)player.CurrentStamina}";
    }

    private void InitializeTeamColors()
    {
        Color color;
        if (ColorUtility.TryParseHtmlString("#FDB927", out color)) _teamColors.Add("LAL", color);
        if (ColorUtility.TryParseHtmlString("#006BB6", out color)) _teamColors.Add("GSW", color);
        if (ColorUtility.TryParseHtmlString("#CE1141", out color)) _teamColors.Add("CHI", color);
        if (ColorUtility.TryParseHtmlString("#007A33", out color)) _teamColors.Add("BOS", color);
        if (ColorUtility.TryParseHtmlString("#F9A01B", out color)) _teamColors.Add("IND", color);
        if (ColorUtility.TryParseHtmlString("#007AC1", out color)) _teamColors.Add("OKC", color);
        // ... 필요한 모든 팀을 여기에 추가 ...
    }

    public Color GetTeamColor(string teamAbbreviation)
    {
        if (_teamColors.ContainsKey(teamAbbreviation))
        {
            return _teamColors[teamAbbreviation];
        }
        return Color.gray;
    }

    // OnEnable/OnDisable에서 등록/해제할 이벤트 핸들러
    private void HandleUILog(string message)
    {
        if (gameLogUI != null)
        {
            gameLogUI.AddLogEntry(message);
        }
    }
}