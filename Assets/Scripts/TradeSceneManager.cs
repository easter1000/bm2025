using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using madcamp3.Assets.Script.Player;
using UnityEngine.UI;

// 3단 트레이드 레이아웃을 총괄하는 메인 컨트롤러
public class TradeSceneManager : MonoBehaviour
{
    [Header("Column Contents")]
    [SerializeField] private Transform myRosterContent;
    [SerializeField] private Transform opponentRosterContent;

    [Header("Detail Controller")]
    [SerializeField] private PlayerDetailUI playerDetailUI; 
    
    [Header("Prefabs")]
    [SerializeField] private GameObject playerLineUIPrefab;

    private PlayerLineController _currentlySelectedLine;
    private Color _defaultBgColorEven = new Color32(242, 242, 242, 255);
    private Color _defaultBgColorOdd = new Color32(229, 229, 229, 255);
    private Color _highlightColor = new Color32(173, 216, 230, 255); // LightBlue

    void Start()
    {
        InitializeScene();
    }

    private void InitializeScene()
    {
        if (playerDetailUI != null)
        {
            playerDetailUI.gameObject.SetActive(false);
        }
        
        string targetTeamAbbr = "UTA"; 
        User userInfo = LocalDbManager.Instance.GetUser();
        string myTeamAbbr = userInfo.SelectedTeamAbbr;

        PopulateRosterColumn(myTeamAbbr, myRosterContent);
        PopulateRosterColumn(targetTeamAbbr, opponentRosterContent);
    }
    
    private void PopulateRosterColumn(string teamAbbr, Transform contentParent)
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        var players = LocalDbManager.Instance.GetPlayersByTeam(teamAbbr)
                                     .OrderByDescending(p => p.overallAttribute)
                                     .ToList();

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            GameObject itemGO = Instantiate(playerLineUIPrefab, contentParent);
            var lineController = itemGO.GetComponent<PlayerLineController>();

            if (lineController != null)
            {
                Color bgColor = (i % 2 == 0) ? _defaultBgColorEven : _defaultBgColorOdd;
                
                PlayerLine lineData = ConvertRatingToLine(player);
                
                lineController.SetPlayerLine(lineData, bgColor);
                lineController.OnClicked += (line) => HandlePlayerLineClicked(lineController, player);
            }
        }
    }
    
    private void HandlePlayerLineClicked(PlayerLineController clickedLine, PlayerRating clickedPlayerRating)
    {
        // 1. 이전에 선택된 라인이 있었다면 원래 배경색으로 복원
        if (_currentlySelectedLine != null && _currentlySelectedLine.GetComponent<Image>() != null)
        {
            bool isEven = _currentlySelectedLine.transform.GetSiblingIndex() % 2 == 0;
            _currentlySelectedLine.GetComponent<Image>().color = isEven ? _defaultBgColorEven : _defaultBgColorOdd;
        }

        // 2. 새로 클릭된 라인을 현재 선택된 라인으로 지정하고 하이라이트
        _currentlySelectedLine = clickedLine;
        if(_currentlySelectedLine.GetComponent<Image>() != null)
        {
            _currentlySelectedLine.GetComponent<Image>().color = _highlightColor;
        }
        
        // 3. 상세 정보 UI에 클릭된 선수의 데이터를 표시
        if (playerDetailUI != null)
        {
            if (!playerDetailUI.gameObject.activeSelf)
            {
                playerDetailUI.gameObject.SetActive(true);
            }
            playerDetailUI.SetPlayer(clickedPlayerRating);
        }
    }
    
    private PlayerLine ConvertRatingToLine(PlayerRating rating)
    {
        return new PlayerLine
        {
            PlayerName = rating.name,
            Position = PositionCodeToString(rating.position),
            BackNumber = rating.backNumber,
            Age = rating.age,
            Height = rating.height,
            Weight = rating.weight,
            OverallScore = rating.overallAttribute,
            Potential = rating.potential,
            PlayerId = rating.player_id
        };
    }

    private string PositionCodeToString(int code)
    {
        return code switch
        {
            1 => "PG", 2 => "SG", 3 => "SF", 4 => "PF", 5 => "C",
            _ => "?"
        };
    }
}