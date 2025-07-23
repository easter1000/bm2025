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

    [Header("Auto-Sub Toggle")]
    public Toggle autoSubToggle;
    public Animator autoSubToggleAnimator; // 토글 애니메이터
    public GameObject autoSubText;       // "AUTO" 텍스트 GameObject

    [Header("Game Simulator")]
    [SerializeField] private GameSimulator gameSimulator;

    [Header("Court & Puck References")]
    public PlayerPuck playerPuckPrefab;
    public Transform[] homeCourtPositions;
    public Transform[] awayCourtPositions;

    private Dictionary<int, PlayerPuck> _playerPucks = new Dictionary<int, PlayerPuck>();
    private Dictionary<string, Color> _teamColors = new Dictionary<string, Color>();

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

    public void InitializePlayerPucks(List<GamePlayer> homeRoster, List<GamePlayer> awayRoster)
    {
        foreach (var puck in _playerPucks.Values)
        {
            Destroy(puck.gameObject);
        }
        _playerPucks.Clear();

        var homeStarters = homeRoster.OrderByDescending(p => p.Rating.overallAttribute).Take(5).ToList();
        for(int i = 0; i < homeStarters.Count; i++)
        {
            if (i < homeCourtPositions.Length)
            {
                PlayerPuck puck = Instantiate(playerPuckPrefab, homeCourtPositions[i]);
                puck.Setup(homeStarters[i]);
                _playerPucks.Add(homeStarters[i].Rating.player_id, puck);
            }
        }

        var awayStarters = awayRoster.OrderByDescending(p => p.Rating.overallAttribute).Take(5).ToList();
        for(int i = 0; i < awayStarters.Count; i++)
        {
            if (i < awayCourtPositions.Length)
            {
                PlayerPuck puck = Instantiate(playerPuckPrefab, awayCourtPositions[i]);
                puck.Setup(awayStarters[i]);
                _playerPucks.Add(awayStarters[i].Rating.player_id, puck);
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
        if (_playerPucks.ContainsKey(playerOut.Rating.player_id))
        {
            PlayerPuck puckToUpdate = _playerPucks[playerOut.Rating.player_id];
            
            _playerPucks.Remove(playerOut.Rating.player_id);
            puckToUpdate.Setup(playerIn);
            _playerPucks.Add(playerIn.Rating.player_id, puckToUpdate);
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