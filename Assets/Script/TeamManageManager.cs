using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using madcamp3.Assets.Script.Player;

public class TeamManageManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TeamItemUI teamItemUI;
    [SerializeField] private Transform logoGridContent;
    [SerializeField] private Button releasePlayerButton;
    [SerializeField] private ConfirmDialog confirmDialog;
    [SerializeField] private Image injuryImage;
    [SerializeField] private TMPro.TextMeshProUGUI injuryText;

    private readonly List<TeamData> displayTeams = new();
    private TeamData currentTeam;
    private string myTeamAbbr;
    private GameObject highlightedLogoObj;
    private PlayerLine selectedPlayerLine;

    private void Start()
    {
        myTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;

        BuildTeamDataList();
        PopulateLogoGrid();

        if (releasePlayerButton != null)
        {
            releasePlayerButton.onClick.RemoveAllListeners();
            releasePlayerButton.onClick.AddListener(OnReleasePlayerClicked);
        }

        TeamData myTeam = displayTeams.FirstOrDefault(t => t.abbreviation == myTeamAbbr);
        if (myTeam != null)
        {
            ShowTeam(myTeam);
            GameObject myTeamLogoObj = logoGridContent.Find($"Logo_{myTeam.abbreviation}")?.gameObject;
            if (myTeamLogoObj != null)
            {
                UpdateLogoHighlight(myTeamLogoObj);
            }
        }
        else if (displayTeams.Count > 0)
        {
            ShowTeam(displayTeams[0]);
             GameObject firstTeamLogoObj = logoGridContent.Find($"Logo_{displayTeams[0].abbreviation}")?.gameObject;
            if (firstTeamLogoObj != null)
            {
                UpdateLogoHighlight(firstTeamLogoObj);
            }
        }
        else
        {
            Debug.LogWarning("[TeamManageManager] 표시할 팀 데이터가 없습니다.");
        }
    }

    private void BuildTeamDataList()
    {
        displayTeams.Clear();

        var teamEntities = LocalDbManager.Instance.GetAllTeams();
        if (teamEntities == null || teamEntities.Count == 0)
        {
            Debug.LogError("[TeamManageManager] Team 테이블을 불러오지 못했습니다.");
            return;
        }

        foreach (var teamEntity in teamEntities)
        {
            string teamName = teamEntity.team_name;
            string abbr = teamEntity.team_abbv;

            var allPlayers = LocalDbManager.Instance.GetPlayersByTeam(abbr);
            if (allPlayers == null || allPlayers.Count < 5) continue;

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
                        idToAssignedPos[pid] = PositionCodeToString(i + 1);
                    }
                }
            }

            List<PlayerLine> starters = new();
            List<PlayerLine> bench = new();

            foreach (var pr in allPlayers)
            {
                PlayerStatus status = LocalDbManager.Instance.GetPlayerStatus(pr.player_id);
                PlayerLine pl = new()
                {
                    PlayerName = pr.name,
                    Position = PositionCodeToString(pr.position),
                    BackNumber = pr.backNumber,
                    Age = pr.age,
                    Height = pr.height,
                    Weight = pr.weight,
                    OverallScore = pr.overallAttribute,
                    Potential = status?.Stamina ?? 100, // Use Stamina for Potential
                    PlayerId = pr.player_id,
                    IsInjured = status?.IsInjured ?? false,
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

            List<PlayerLine> playerLines = new();
            playerLines.AddRange(starters);
            playerLines.AddRange(bench);

            TeamData td = new(teamEntity.team_id, teamName, abbr, playerLines, teamEntity.team_color);
            displayTeams.Add(td);
        }

        // Sort to bring my team to the front
        displayTeams.Sort((a, b) =>
        {
            if (a.abbreviation == myTeamAbbr) return -1;
            if (b.abbreviation == myTeamAbbr) return 1;
            return a.teamId.CompareTo(b.teamId);
        });
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
            _ => string.Empty,
        };
    }

    private void PopulateLogoGrid()
    {
        if (logoGridContent == null) return;

        foreach (Transform child in logoGridContent)
        {
            Destroy(child.gameObject);
        }

        foreach (var t in displayTeams)
        {
            CreateLogoButton(t);
        }
    }

    private void CreateLogoButton(TeamData team)
    {
        GameObject obj = new GameObject($"Logo_{team.abbreviation}", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(logoGridContent, false);
        obj.transform.localScale = Vector3.one;

        Image img = obj.GetComponent<Image>();
        if (img != null)
        {
            Sprite logo = Resources.Load<Sprite>($"team_photos/{team.abbreviation}") ??
                          Resources.Load<Sprite>("team_photos/default_logo");
            img.sprite = logo;
            img.preserveAspect = true;
        }

        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnLogoClicked(team, obj));
        }
    }

    private void OnLogoClicked(TeamData team, GameObject logoObj)
    {
        ShowTeam(team);
        UpdateLogoHighlight(logoObj);
    }

    private void UpdateLogoHighlight(GameObject newLogoObj)
    {
        if (highlightedLogoObj == newLogoObj) return;

        if (highlightedLogoObj != null)
        {
            Transform prevBorder = highlightedLogoObj.transform.Find("BorderLines");
            if (prevBorder != null) prevBorder.gameObject.SetActive(false);
        }

        if (newLogoObj != null)
        {
            Transform borderT = newLogoObj.transform.Find("BorderLines");
            if (borderT == null)
            {
                GameObject borderRoot = new GameObject("BorderLines", typeof(RectTransform));
                borderRoot.transform.SetParent(newLogoObj.transform, false);
                borderRoot.transform.SetAsLastSibling();

                RectTransform rootRT = borderRoot.GetComponent<RectTransform>();
                rootRT.anchorMin = Vector2.zero;
                rootRT.anchorMax = Vector2.one;
                rootRT.offsetMin = Vector2.zero;
                rootRT.offsetMax = Vector2.zero;

                const float thickness = 4f;
                Color borderColor = Color.yellow;

                void CreateLine(string name, Vector2 anchorMin, Vector2 anchorMax)
                {
                    GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image));
                    line.transform.SetParent(borderRoot.transform, false);
                    RectTransform rt = line.GetComponent<RectTransform>();
                    rt.anchorMin = anchorMin;
                    rt.anchorMax = anchorMax;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    Image img = line.GetComponent<Image>();
                    img.color = borderColor;
                    img.raycastTarget = false;
                }

                CreateLine("Top", new Vector2(0, 1), new Vector2(1, 1));
                borderRoot.transform.Find("Top").GetComponent<RectTransform>().sizeDelta = new Vector2(0, thickness);
                CreateLine("Bottom", new Vector2(0, 0), new Vector2(1, 0));
                borderRoot.transform.Find("Bottom").GetComponent<RectTransform>().sizeDelta = new Vector2(0, thickness);
                CreateLine("Left", new Vector2(0, 0), new Vector2(0, 1));
                borderRoot.transform.Find("Left").GetComponent<RectTransform>().sizeDelta = new Vector2(thickness, 0);
                CreateLine("Right", new Vector2(1, 0), new Vector2(1, 1));
                borderRoot.transform.Find("Right").GetComponent<RectTransform>().sizeDelta = new Vector2(thickness, 0);
            }
            else
            {
                borderT.gameObject.SetActive(true);
            }
        }
        highlightedLogoObj = newLogoObj;
    }

    private void ShowTeam(TeamData team)
    {
        currentTeam = team;
        if (teamItemUI != null)
        {
            // Clicking the team item does nothing in this manager.
            teamItemUI.Init(team, (selectedTeam) => { /* Do Nothing */ });

            teamItemUI.OnPlayerLineClicked -= OnPlayerSelected;
            teamItemUI.OnPlayerLineClicked += OnPlayerSelected;
        }
        UpdateReleaseButtonVisibility();
    }

    private void OnPlayerSelected(PlayerLine playerLine)
    {
        selectedPlayerLine = playerLine;
        UpdateReleaseButtonVisibility();
        UpdateInjuryStatusUI(playerLine);
    }

    private void UpdateInjuryStatusUI(PlayerLine playerLine)
    {
        if (injuryImage == null || injuryText == null) return;

        if (playerLine == null)
        {
            injuryImage.gameObject.SetActive(false);
            injuryText.gameObject.SetActive(false);
            return;
        }

        injuryImage.gameObject.SetActive(true);
        injuryText.gameObject.SetActive(true);

        PlayerStatus status = LocalDbManager.Instance.GetPlayerStatus(playerLine.PlayerId);
        if (status != null && status.IsInjured)
        {
            injuryImage.color = Color.red;
            injuryText.text = $"INJURED: {status.InjuryDaysLeft} days left";
        }
        else
        {
            injuryImage.color = Color.green;
            injuryText.text = "HEALTHY";
        }
    }

    private void UpdateReleaseButtonVisibility()
    {
        if (releasePlayerButton != null)
        {
            bool isMyTeam = (currentTeam != null && currentTeam.abbreviation == myTeamAbbr);
            releasePlayerButton.gameObject.SetActive(isMyTeam);
        }
    }

    private void OnReleasePlayerClicked()
    {
        if (selectedPlayerLine == null)
        {
            Debug.LogWarning("방출할 선수가 선택되지 않았습니다.");
            return;
        }

        if (confirmDialog != null)
        {
            string message = $"정말로 '{selectedPlayerLine.PlayerName}' 선수를 방출하시겠습니까?";
            confirmDialog.Show(message, 
                () => { ReleasePlayer(); }, // onYes
                () => { /* onNo, do nothing */ }
            );
        }
    }

    private void ReleasePlayer()
    {
        if (selectedPlayerLine == null) return;

        Debug.Log($"Releasing player: {selectedPlayerLine.PlayerName} (ID: {selectedPlayerLine.PlayerId})");
        string currentTeamAbbr = currentTeam.abbreviation;

        LocalDbManager.Instance.ReleasePlayer(selectedPlayerLine.PlayerId);
        
        // 데이터 및 UI 새로고침
        BuildTeamDataList();
        
        TeamData refreshedTeam = displayTeams.FirstOrDefault(t => t.abbreviation == currentTeamAbbr);
        
        if (refreshedTeam != null)
        {
            ShowTeam(refreshedTeam);
        }
        else if (displayTeams.Count > 0)
        {
            // 만약 현재 팀이 어떤 이유로든 사라졌다면, 목록의 첫 번째 팀을 보여줌
            ShowTeam(displayTeams[0]);
        }
        else
        {
            Debug.LogError("[TeamManageManager] ReleasePlayer 후 표시할 팀이 없습니다.");
        }
    }
} 