using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class RecordPlayPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private GameObject playLinePrefab; // PlayLineUI prefab

    private Dictionary<string, int> _teamRankCache;

    private void OnEnable()
    {
        StartCoroutine(PopulateAfterFrame());
    }

    private System.Collections.IEnumerator PopulateAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        PopulateGames();
    }

    private void PopulateGames()
    {
        if (scrollContent == null || playLinePrefab == null) return;

        foreach (Transform child in scrollContent) Destroy(child.gameObject);

        BuildTeamRankCache();

        var games = LocalDbManager.Instance.GetScheduleForSeason(LocalDbManager.Instance.GetUser().CurrentSeason)
            .Where(g=>g.GameStatus=="Final")
            .OrderByDescending(g=>g.GameDate)
            .ToList();

        foreach(var g in games)
        {
            GameObject go=Instantiate(playLinePrefab,scrollContent);
            var ui=go.GetComponent<PlayLineUI>();
            if(ui==null) continue;
            var homeTeam=LocalDbManager.Instance.GetTeam(g.HomeTeamAbbr);
            var awayTeam=LocalDbManager.Instance.GetTeam(g.AwayTeamAbbr);
            int homeRank=_teamRankCache.GetValueOrDefault(homeTeam.team_abbv,0)+1;
            int awayRank=_teamRankCache.GetValueOrDefault(awayTeam.team_abbv,0)+1;

            // [FIXED] Use the null-coalescing operator '??' to provide a default value (0)
            // for the nullable scores before passing them to the Setup method.
            ui.Setup(homeRank, awayRank, homeTeam, awayTeam, g.HomeTeamScore ?? 0, g.AwayTeamScore ?? 0);
        }
        Canvas.ForceUpdateCanvases();
        CalculateHeight(games.Count);
    }

    private void BuildTeamRankCache()
    {
        // This assumes GetTeamFinancesForSeason is now present in LocalDbManager
        var finances=LocalDbManager.Instance.GetTeamFinancesForSeason(LocalDbManager.Instance.GetUser().CurrentSeason);
        var sorted=finances.OrderByDescending(f=>(float)f.Wins/(f.Wins+f.Losses))
            .ThenByDescending(f=>f.Wins).ToList();
        _teamRankCache=new Dictionary<string,int>();
        for(int i=0;i<sorted.Count;i++) _teamRankCache[sorted[i].TeamAbbr]=i;
    }

    private void CalculateHeight(int itemCount)
    {
        GridLayoutGroup grid=scrollContent.GetComponent<GridLayoutGroup>();
        if(grid==null) return;
        int col= grid.constraint==GridLayoutGroup.Constraint.FixedColumnCount? grid.constraintCount:1;
        if(col<=0) col=1;
        int rows=Mathf.CeilToInt((float)itemCount/col);
        float h=grid.padding.top+grid.padding.bottom+rows*grid.cellSize.y+(rows-1)*grid.spacing.y;
        scrollContent.sizeDelta=new Vector2(scrollContent.sizeDelta.x,h);
    }
}