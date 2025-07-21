using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class TradeSceneManager : MonoBehaviour
{
    [SerializeField]
    private GameObject teamLogoButtonPrefab;

    [SerializeField]
    private Transform gridParent;

    void Start()
    {
        GenerateTeamGrid();
    }

    private void GenerateTeamGrid()
    {
        User userInfo = LocalDbManager.Instance.GetUser();
        if (userInfo == null)
        {
            Debug.LogError("User info not found! Cannot generate team grid.");
            return;
        }
        string myTeamAbbr = userInfo.SelectedTeamAbbr;

        List<Team> allTeams = LocalDbManager.Instance.GetAllTeams();
        List<string> allTeamAbbrs = allTeams.OrderBy(t => t.team_abbv)
                                             .Select(t => t.team_abbv)
                                             .ToList();
        
        int myTeamIndex = allTeamAbbrs.IndexOf(myTeamAbbr);
        if (myTeamIndex != -1)
        {
            allTeamAbbrs[myTeamIndex] = "nba";
        }

        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        foreach (string teamAbbr in allTeamAbbrs)
        {
            InstantiateTeamButton(teamAbbr);
        }
    }

    private void InstantiateTeamButton(string abbr)
    {
        GameObject buttonGO = Instantiate(teamLogoButtonPrefab, gridParent);
        buttonGO.name = "Button_" + abbr;

        Image buttonImage = buttonGO.GetComponent<Image>();
        Sprite logoSprite = Resources.Load<Sprite>("team_photos/" + abbr);

        if (logoSprite != null)
        {
            buttonImage.sprite = logoSprite;
        }
        else
        {
            Debug.LogWarning($"Logo for {abbr} not found in Resources/TeamLogos/");
        }

        Button button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(() => OnTeamLogoClicked(abbr));
    }

    public void OnTeamLogoClicked(string teamAbbr)
    {
        if (teamAbbr == "FA")
        {
            Debug.Log("Navigate to Free Agent Market.");
            // ex: SceneManager.LoadScene("FreeAgentScene");
        }
        else
        {
            Debug.Log($"Selected Team: {teamAbbr}. Showing their tradeable players.");
            // ex: PlayerListPanel.ShowPlayersForTrade(teamAbbr);
        }
    }
}