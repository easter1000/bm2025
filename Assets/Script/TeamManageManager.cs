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
    private int _nextFocusedPlayerId = -1;

    private void Start()
    {
        myTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;

        BuildTeamDataList();
        PopulateLogoGrid();

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
            
            // 주전 선수가 5명 미만일 경우, 빈 포지션을 채웁니다.
            if (starters.Count < 5 && abbr == myTeamAbbr) // 내 팀에만 자동 배치 적용
            {
                FillEmptyStarterPositions(starters, bench);
                
                // 변경된 주전 라인업을 DB에 저장
                List<int> starterIdsToUpdate = starters.Select(p => p.PlayerId).ToList();
                LocalDbManager.Instance.UpdateBestFive(abbr, starterIdsToUpdate);
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

    private void FillEmptyStarterPositions(List<PlayerLine> starters, List<PlayerLine> bench)
    {
        if (bench.Count == 0) return;

        string[] requiredPositions = { "PG", "SG", "SF", "PF", "C" };
        HashSet<string> currentStarterPositions = new HashSet<string>(starters.Select(p => p.AssignedPosition));

        foreach (var pos in requiredPositions)
        {
            if (starters.Count >= 5) break;
            if (currentStarterPositions.Contains(pos)) continue;

            // 1. 해당 포지션에 가장 적합한 벤치 선수 찾기
            PlayerLine bestCandidate = bench
                .Where(p => !p.IsInjured) // 부상당하지 않은 선수 중에서
                .OrderByDescending(p => p.Position == pos) // 1순위: 포지션 일치
                .ThenByDescending(p => p.OverallScore)   // 2순위: OVR
                .FirstOrDefault();

            if (bestCandidate != null)
            {
                bestCandidate.AssignedPosition = pos;
                starters.Add(bestCandidate);
                bench.Remove(bestCandidate);
                currentStarterPositions.Add(pos);
            }
        }

        // 그래도 5명이 안 채워졌다면(예: 벤치 멤버 부족), 남은 선수 중 OVR 높은 순으로 채움
        while (starters.Count < 5 && bench.Count > 0)
        {
            PlayerLine bestRemaining = bench.Where(p => !p.IsInjured).OrderByDescending(p => p.OverallScore).FirstOrDefault();
            if (bestRemaining != null)
            {
                // 빈 포지션 중 하나를 할당
                string emptyPos = requiredPositions.FirstOrDefault(p => !currentStarterPositions.Contains(p));
                if (!string.IsNullOrEmpty(emptyPos))
                {
                    bestRemaining.AssignedPosition = emptyPos;
                    starters.Add(bestRemaining);
                    bench.Remove(bestRemaining);
                    currentStarterPositions.Add(emptyPos);
                }
                else
                {
                    // 모든 포지션이 찼는데 주전이 5명이 안되는 경우는 논리적으로 거의 없지만, 방어 코드
                    break; 
                }
            }
            else
            {
                break; // 부상 아닌 선수가 더이상 없음
            }
        }
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

    private void ShowTeam(TeamData team, int focusPlayerId = -1)
    {
        currentTeam = team;
        if (teamItemUI != null)
        {
            // Clicking the team item does nothing in this manager.
            teamItemUI.Init(team, (selectedTeam) => { /* Do Nothing */ }, focusPlayerId);

            teamItemUI.OnPlayerLineClicked -= OnPlayerSelected;
            teamItemUI.OnPlayerLineClicked += OnPlayerSelected;
            teamItemUI.OnPlayerLineDoubleClicked -= OnPlayerDoubleClicked;
            teamItemUI.OnPlayerLineDoubleClicked += OnPlayerDoubleClicked;
            
            // 초기 선수 선택 및 UI 업데이트
            PlayerLine initialPlayer = teamItemUI.GetInitialSelectedPlayer();
            if (initialPlayer != null)
            {
                OnPlayerSelected(initialPlayer);
            }
        }
        else
        {
             UpdateReleaseButtonVisibility();
        }
    }

    private void OnPlayerDoubleClicked(PlayerLine playerLine)
    {
        if (currentTeam.abbreviation != myTeamAbbr) return;

        bool isStarter = teamItemUI.IsStarter(playerLine.PlayerId);

        if (isStarter)
        {
            // 주전 -> 벤치 (벤치에서 가장 적합한 선수와 교체)
            PlayerLine bestBenchPlayer = FindBestBenchPlayerForPosition(playerLine.AssignedPosition);
            if (bestBenchPlayer != null)
            {
                SwapPlayers(playerLine, bestBenchPlayer);
            }
        }
        else
        {
            // 벤치 -> 주전 (해당 포지션의 주전과 교체)
            PlayerLine starterToReplace = FindStarterByPosition(playerLine.Position);
            if (starterToReplace != null)
            {
                SwapPlayers(starterToReplace, playerLine);
            }
        }
    }

    private PlayerLine FindBestBenchPlayerForPosition(string position)
    {
        return currentTeam.players
            .Where(p => !teamItemUI.IsStarter(p.PlayerId))
            .OrderByDescending(p => p.Position == position)
            .ThenByDescending(p => p.OverallScore)
            .FirstOrDefault();
    }
    
    private PlayerLine FindStarterByPosition(string position)
    {
        return currentTeam.players
            .Where(p => teamItemUI.IsStarter(p.PlayerId) && p.AssignedPosition == position)
            .FirstOrDefault();
    }

    private void SwapPlayers(PlayerLine starter, PlayerLine substitute)
    {
        // 0. 새로 주전이 될 선수의 ID를 기억
        _nextFocusedPlayerId = substitute.PlayerId;

        // 1. 데이터 업데이트 (AssignedPosition 교환)
        string starterOriginalPosition = starter.AssignedPosition;
        starter.AssignedPosition = null;
        substitute.AssignedPosition = starterOriginalPosition;

        // 2. DB 업데이트 (best_five)
        var starters = currentTeam.players.Where(p => p.AssignedPosition != null).ToList();
        
        // 포지션 코드를 기준으로 정렬 (PG-SG-SF-PF-C)
        starters.Sort((a, b) => PositionStringToCode(a.AssignedPosition).CompareTo(PositionStringToCode(b.AssignedPosition)));
        
        List<int> starterIds = starters.Select(p => p.PlayerId).ToList();

        LocalDbManager.Instance.UpdateBestFive(currentTeam.abbreviation, starterIds);

        // 3. UI 새로고침
        RefreshTeamDisplay();
    }
    private int PositionStringToCode(string pos)
    {
        return pos switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 99
        };
    }
    private void RefreshTeamDisplay()
    {
        string currentTeamAbbr = currentTeam.abbreviation;
        BuildTeamDataList();
        TeamData refreshedTeam = displayTeams.FirstOrDefault(t => t.abbreviation == currentTeamAbbr);
        if (refreshedTeam != null)
        {
            ShowTeam(refreshedTeam, _nextFocusedPlayerId);
            _nextFocusedPlayerId = -1; // 사용 후 초기화
        }
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
            bool isPlayerSelected = selectedPlayerLine != null;
            releasePlayerButton.gameObject.SetActive(isMyTeam && isPlayerSelected);
            
            if (isMyTeam && isPlayerSelected)
            {
                releasePlayerButton.onClick.RemoveAllListeners();
                releasePlayerButton.onClick.AddListener(OnReleasePlayerClicked);
            }
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