using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System.Globalization;

public class ScheduleView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private ScheduleCell scheduleCellPrefab;
    [SerializeField] private TMP_Text dateLabel;
    
    [Header("Actions")]
    [SerializeField] private Button startGameButton;

    private string _userTeamAbbr;
    private int _currentSeason;
    private Schedule _userGameOnSelectedDate;

    private void Awake()
    {
        var user = LocalDbManager.Instance.GetUser();
        if (user != null)
        {
            _userTeamAbbr = user.SelectedTeamAbbr;
            _currentSeason = user.CurrentSeason;

            if (DateTime.TryParse(user.CurrentDate, out DateTime initDate))
            {
                UpdateDateLabel(initDate);
            }
            else
            {
                UpdateDateLabel(DateTime.Now);
            }
        }
        else
        {
            // 유저 정보가 없을 경우의 기본값
            _userTeamAbbr = "BOS"; // 예시 팀
            _currentSeason = DateTime.Now.Year;
            UpdateDateLabel(DateTime.Now);
            Debug.LogWarning("User 정보를 찾을 수 없어 기본값으로 설정합니다.");
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
            startGameButton.gameObject.SetActive(false); // 초기에는 비활성화
        }
    }

    /// <summary>
    /// 선택한 날짜에 대한 스케줄을 표시한다.
    /// </summary>
    public void ShowScheduleForDate(DateTime date)
    {
        if (contentParent == null || scheduleCellPrefab == null)
        {
            Debug.LogError("[ScheduleView] contentParent 또는 prefab이 설정되지 않았습니다.");
            return;
        }

        // 기존 아이템 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        UpdateDateLabel(date);
        _userGameOnSelectedDate = null; // 리셋

        string dateStr = date.ToString("yyyy-MM-dd");
        List<Schedule> games = LocalDbManager.Instance.GetGamesForDate(dateStr);
        if (games == null || games.Count == 0)
        {
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            return;
        }

        // 내 팀 경기가 가장 위로 오도록 정렬
        games.Sort((a, b) => {
            bool aIsUser = (a.HomeTeamAbbr == _userTeamAbbr || a.AwayTeamAbbr == _userTeamAbbr);
            bool bIsUser = (b.HomeTeamAbbr == _userTeamAbbr || b.AwayTeamAbbr == _userTeamAbbr);
            if (aIsUser == bIsUser) return 0;
            return aIsUser ? -1 : 1; // 내 팀 경기(a)가 우선이면 -1
        });

        foreach (var game in games)
        {
            ScheduleCell cell = Instantiate(scheduleCellPrefab, contentParent);
            cell.Configure(game, _currentSeason);
        }
        
        // 레이아웃 최신화 (즉시)
        if (contentParent != null)
        {
            // 즉시 레이아웃 Rebuild → ContentSizeFitter가 크기 재계산하도록
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
        }

        // 내 팀의 예정된 경기가 있는지 확인
        _userGameOnSelectedDate = games.FirstOrDefault(g => 
            (g.HomeTeamAbbr == _userTeamAbbr || g.AwayTeamAbbr == _userTeamAbbr) && g.GameStatus == "Scheduled");

        // 버튼 상태 업데이트
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(_userGameOnSelectedDate != null);
        }
    }

    private void OnStartGameClicked()
    {
        if (_userGameOnSelectedDate == null)
        {
            Debug.LogError("시작할 경기가 선택되지 않았습니다.");
            return;
        }

        // GameDataHolder에 선택된 경기 정보를 저장
        GameDataHolder.CurrentGameInfo = _userGameOnSelectedDate;

        Debug.Log($"경기 시작: {_userGameOnSelectedDate.GameId}. gamelogic_test 씬으로 이동합니다.");
        
        // 경기 씬 로드
        UnityEngine.SceneManagement.SceneManager.LoadScene("gamelogic_test");
    }

    private void UpdateDateLabel(DateTime dt)
    {
        if (dateLabel == null) return;
        string formatted = dt.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-US")).ToUpper();
        dateLabel.text = formatted;
    }
} 