using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class TradeUI : MonoBehaviour
{
    private LocalDbManager _dbManager;
    private TradeManager _tradeManager;

    // --- UI Element References (Unity Editor에서 연결 필요) ---
    // public Dropdown opposingTeamDropdown;
    // public Transform userTeamRosterPanel;
    // public Transform opposingTeamRosterPanel;
    // public Transform userTradeOfferPanel;
    // public Transform opposingTradeOfferPanel;
    // public Button proposeTradeButton;
    // public Text tradeStatusText;
    
    // --- 내부 데이터 ---
    private string _userTeamAbbr = "BOS"; // 임시 유저 팀
    private string _opposingTeamAbbr;
    private List<PlayerRating> _userRoster;
    private List<PlayerRating> _opposingRoster;
    private List<PlayerRating> _userOffer = new List<PlayerRating>();
    private List<PlayerRating> _opposingOffer = new List<PlayerRating>();

    void Start()
    {
        _dbManager = LocalDbManager.Instance;
        _tradeManager = FindAnyObjectByType<TradeManager>();
        
        // TODO: 드롭다운 메뉴에 모든 팀 채우기
        // TODO: 초기 화면 설정 (첫 번째 팀을 상대로)
    }
    
    public void OnOpposingTeamSelected(string teamAbbr)
    {
        _opposingTeamAbbr = teamAbbr;
        // TODO: 화면 리프레시 로직 호출
    }

    // 유저 로스터의 선수를 클릭했을 때 호출 (UI Button에 연결)
    public void SelectUserPlayer(PlayerRating player)
    {
        if (!_userOffer.Contains(player))
        {
            _userOffer.Add(player);
            // TODO: userTradeOfferPanel에 선수 UI 추가
        }
    }

    // 상대 로스터의 선수를 클릭했을 때 호출
    public void SelectOpposingPlayer(PlayerRating player)
    {
        if (!_opposingOffer.Contains(player))
        {
            _opposingOffer.Add(player);
            // TODO: opposingTradeOfferPanel에 선수 UI 추가
        }
    }

    // 제안하기 버튼 클릭 시 호출
    public void ProposeTrade()
    {
        if (_userOffer.Count == 0 || _opposingOffer.Count == 0)
        {
            // tradeStatusText.text = "양측에 최소 한 명의 선수를 포함해야 합니다.";
            Debug.LogWarning("Trade proposal failed: Both sides must offer at least one player.");
            return;
        }

        bool success = _tradeManager.EvaluateAndExecuteTrade(
            _userTeamAbbr, _userOffer,
            _opposingTeamAbbr, _opposingOffer
        );

        if (success)
        {
            // tradeStatusText.text = "트레이드 성공!";
            Debug.Log("User proposed trade was successful!");
            // TODO: 로스터 및 UI 리프레시
        }
        else
        {
            // tradeStatusText.text = "상대 팀이 제안을 거절했습니다.";
            Debug.Log("User proposed trade was rejected.");
        }

        // 제안 후 오퍼 목록 초기화
        _userOffer.Clear();
        _opposingOffer.Clear();
        // TODO: 오퍼 패널 UI 초기화
    }
} 