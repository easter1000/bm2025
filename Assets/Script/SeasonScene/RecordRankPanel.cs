using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class RecordRankPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform scrollContent; // RectTransform으로 변경
    [SerializeField] private GameObject rankLinePrefab; // RankLineUI 프리팹

    private void OnEnable()
    {
        // PopulateRankings()가 UI 레이아웃 업데이트 이후에 실행되도록 Coroutine으로 변경
        StartCoroutine(PopulateRankingsAfterFrame());
    }

    private System.Collections.IEnumerator PopulateRankingsAfterFrame()
    {
        // UI가 한 프레임 업데이트될 시간을 줍니다.
        yield return new WaitForEndOfFrame();
        PopulateRankings();
    }
    
    private void PopulateRankings()
    {
        if (scrollContent == null || rankLinePrefab == null)
        {
            Debug.LogError("Required UI references are not set in RecordRankPanel.");
            return;
        }

        // 1. 기존 리스트 클리어
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        // 2. 데이터베이스에서 팀 재정 및 팀 정보 가져오기
        var user = LocalDbManager.Instance.GetUser();
        if (user == null) return;

        List<TeamFinance> allFinances = LocalDbManager.Instance.GetTeamFinancesForSeason(user.CurrentSeason);
        List<Team> allTeams = LocalDbManager.Instance.GetAllTeams();
        Dictionary<string, Team> teamDict = allTeams.ToDictionary(t => t.team_abbv);

        // 3. 승률 기준으로 순위 정렬 (승률이 같으면 승리 횟수 기준)
        var sortedFinances = allFinances
            .OrderByDescending(f => (float)f.Wins / (f.Wins + f.Losses))
            .ThenByDescending(f => f.Wins)
            .ToList();

        // 4. 스크롤 뷰에 순위 라인 채우기
        for (int i = 0; i < sortedFinances.Count; i++)
        {
            TeamFinance finance = sortedFinances[i];
            if (teamDict.TryGetValue(finance.TeamAbbr, out Team teamData))
            {
                GameObject rankLineObj = Instantiate(rankLinePrefab, scrollContent);
                RankLineUI rankLineUI = rankLineObj.GetComponent<RankLineUI>();
                if (rankLineUI != null)
                {
                    rankLineUI.Setup(i + 1, teamData, finance);
                }
            }
        }
        
        // 5. Content 높이 계산 및 설정
        Canvas.ForceUpdateCanvases(); // UI 변경사항 즉시 적용
        CalculateAndSetContentHeight(sortedFinances.Count);
    }

    private void CalculateAndSetContentHeight(int itemCount)
    {
        if (scrollContent == null || itemCount == 0) return;

        GridLayoutGroup grid = scrollContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            Debug.LogError("GridLayoutGroup component not found on the scroll content.");
            return;
        }

        int columnCount = 1; // 기본값 및 사용자 정보에 따라 1로 설정
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            columnCount = grid.constraintCount;
        }
        else
        {
            Debug.LogWarning("GridLayoutGroup's constraint is not FixedColumnCount. Assuming 1 column for height calculation.");
        }
        
        if (columnCount <= 0) columnCount = 1; // 0으로 나누기 방지

        int rowCount = Mathf.CeilToInt((float)itemCount / columnCount);

        float contentHeight = grid.padding.top + grid.padding.bottom + 
                              (rowCount * grid.cellSize.y) + 
                              ((rowCount - 1) * grid.spacing.y);

        scrollContent.sizeDelta = new Vector2(scrollContent.sizeDelta.x, contentHeight);
    }
} 