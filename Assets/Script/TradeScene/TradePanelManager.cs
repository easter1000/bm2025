using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using madcamp3.Assets.Script.Player;

/// <summary>
/// 트레이드 패널 전체를 관리하는 매니저.
/// 1) 팀 로고 버튼들을 GridLayoutGroup 하위에 동적 생성한다.
/// 2) 로고 클릭 시 TeamItemUI 에 해당 팀 정보를 표시한다.
/// 3) TeamItemUI 의 카드 클릭(onClick) 시 TradeScene 으로 이동하며,
///    선택된 팀의 약어(abbreviation)를 PlayerPrefs 로 전달한다.
/// </summary>
public class TradePanelManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TeamItemUI teamItemUI;            // 팀 상세 정보를 보여줄 카드
    [SerializeField] private Transform logoGridContent;        // GridLayoutGroup 이 붙은 transform

    private readonly List<TeamData> displayTeams = new();      // 나(사용자) 팀을 제외한 29개 팀
    private TeamData currentTeam;                              // 현재 화면에 표시 중인 팀
    private string myTeamAbbr;                                 // 사용자가 플레이 중인 팀 약어
    private GameObject highlightedLogoObj;                     // 노란색 테두리가 적용된 로고 객체

    private const string TradeTargetKey = "TradeTargetTeamAbbr";
    private const string TradeSceneName = "TradeScene";        // 이동할 씬 이름

    private void Start()
    {
        // 1) 유저(코치)가 선택한 팀 약어 파악
        myTeamAbbr = LocalDbManager.Instance.GetUser()?.SelectedTeamAbbr;

        // 2) 팀 데이터 준비 (29개)
        BuildTeamDataList();

        // 3) 그리드에 로고 버튼 생성
        PopulateLogoGrid();

        // 4) 초기 팀 정보 표시 – 첫 번째 팀
        if (displayTeams.Count > 0)
        {
            ShowTeam(displayTeams[0]);
        }
        else
        {
            Debug.LogWarning("[TradePanelManager] 표시할 팀 데이터가 없습니다.");
        }
    }

    #region Build Team Data

    /// <summary>
    /// LocalDbManager 의 테이블을 기반으로 TeamData 리스트를 구축한다.
    /// NewGameManager.InitDummyTeams 와 동일(유사) 로직을 사용하지만,
    ///   1) 내 팀은 제외한다.
    ///   2) 결과를 team_id 기준 오름차순으로 정렬하여 '1~30번째' 고정 순서를 따른다.
    /// </summary>
    private void BuildTeamDataList()
    {
        displayTeams.Clear();

        var teamEntities = LocalDbManager.Instance.GetAllTeams();
        if (teamEntities == null || teamEntities.Count == 0)
        {
            Debug.LogError("[TradePanelManager] Team 테이블을 불러오지 못했습니다.");
            return;
        }

        foreach (var teamEntity in teamEntities)
        {
            // 내 팀은 스킵
            if (!string.IsNullOrEmpty(myTeamAbbr) && teamEntity.team_abbv == myTeamAbbr)
                continue;

            string teamName = teamEntity.team_name;
            string abbr = teamEntity.team_abbv;

            // 해당 팀 선수 목록 로드
            var allPlayers = LocalDbManager.Instance.GetPlayersByTeam(abbr);
            if (allPlayers == null || allPlayers.Count < 5) continue;

            // best_five 문자열(PG,SG,SF,PF,C 순) → player_id 매핑
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
                        idToAssignedPos[pid] = PositionCodeToString(i + 1); // 1~5 → PG~C
                    }
                }
            }

            List<PlayerLine> starters = new();
            List<PlayerLine> bench = new();

            foreach (var pr in allPlayers)
            {
                PlayerLine pl = new()
                {
                    PlayerName = pr.name,
                    Position = PositionCodeToString(pr.position),
                    BackNumber = pr.backNumber,
                    Age = pr.age,
                    Height = pr.height,
                    Weight = pr.weight,
                    OverallScore = pr.overallAttribute,
                    Potential = pr.potential,
                    PlayerId = pr.player_id,
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

            // 주전이 5명 미만이라면 벤치에서 최고 OVR 순으로 채운다.
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

            // 최종 playerLines: starters (5) + bench
            List<PlayerLine> playerLines = new();
            playerLines.AddRange(starters);
            playerLines.AddRange(bench);

            TeamData td = new(teamEntity.team_id, teamName, abbr, playerLines, teamEntity.team_color);
            displayTeams.Add(td);
        }

        // team_id (1~30) 기준 정렬 – 첫 번째부터 30번째까지 순서 보장
        displayTeams.Sort((a, b) => a.teamId.CompareTo(b.teamId));
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

    #endregion

    #region UI – Logo Grid

    private void PopulateLogoGrid()
    {
        if (logoGridContent == null)
        {
            Debug.LogError("[TradePanelManager] logoGridContent 참조가 없습니다.");
            return;
        }

        // 기존 자식 제거
        foreach (Transform child in logoGridContent)
        {
            Destroy(child.gameObject);
        }

        // 29개 팀 로고 버튼 생성
        foreach (var t in displayTeams)
        {
            CreateLogoButton(t);
        }

        CreateFreeLogoButton();
    }

    private void CreateLogoButton(TeamData team)
    {
        // 새로운 GameObject 생성 후 필요 컴포넌트(Image, Button) 추가
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

        // 클릭 이벤트 – 새로 추가된 Button 컴포넌트 이용
        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            TeamData capturedTeam = team;
            GameObject capturedObj = obj;
            btn.onClick.AddListener(() => OnLogoClicked(capturedTeam, capturedObj));
        }
    }

    /// <summary>
    /// 로고 클릭 시 호출: 팀 정보 표시 + 노란색 테두리 하이라이트
    /// </summary>
    private void OnLogoClicked(TeamData team, GameObject logoObj)
    {
        // 1) 팀 상세 표시
        ShowTeam(team);

        // 2) 하이라이트 업데이트
        UpdateLogoHighlight(logoObj);
    }

    private void UpdateLogoHighlight(GameObject newLogoObj)
    {
        if (highlightedLogoObj == newLogoObj) return;

        // 1) 이전 하이라이트의 BorderRect 비활성화
        if (highlightedLogoObj != null)
        {
            Transform prevBorder = highlightedLogoObj.transform.Find("BorderLines");
            if (prevBorder != null) prevBorder.gameObject.SetActive(false);
        }

        // 2) 새 하이라이트 적용 – BorderRect 활성화 또는 생성
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

                // Helper to create each line
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

                // Top
                CreateLine("Top", new Vector2(0,1), new Vector2(1,1));
                borderRoot.transform.Find("Top").GetComponent<RectTransform>().sizeDelta = new Vector2(0, thickness);
                // Bottom
                CreateLine("Bottom", new Vector2(0,0), new Vector2(1,0));
                borderRoot.transform.Find("Bottom").GetComponent<RectTransform>().sizeDelta = new Vector2(0, thickness);
                // Left
                CreateLine("Left", new Vector2(0,0), new Vector2(0,1));
                borderRoot.transform.Find("Left").GetComponent<RectTransform>().sizeDelta = new Vector2(thickness, 0);
                // Right
                CreateLine("Right", new Vector2(1,0), new Vector2(1,1));
                borderRoot.transform.Find("Right").GetComponent<RectTransform>().sizeDelta = new Vector2(thickness, 0);
            }
            else
            {
                borderT.gameObject.SetActive(true);
            }
        }

        // 3) 레퍼런스 갱신
        highlightedLogoObj = newLogoObj;
    }

    private void CreateFreeLogoButton()
    {
        GameObject obj = new GameObject("Logo_Free", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(logoGridContent, false);
        obj.transform.localScale = Vector3.one;

        Image img = obj.GetComponent<Image>();
        if (img != null)
        {
            Sprite freeLogo = Resources.Load<Sprite>("team_photos/free");
            img.sprite = freeLogo;
            img.preserveAspect = true;
        }
    }

    #endregion

    #region Team Display & Scene Navigation

    private void ShowTeam(TeamData team)
    {
        currentTeam = team;
        if (teamItemUI != null)
        {
            teamItemUI.Init(team, OnTeamItemClicked);
        }
        // 초기 호출 시 선택된 로고 없는 경우를 대비해 skip (UpdateLogoHighlight는 OnLogoClicked에서 처리)
    }

    private void OnTeamItemClicked(TeamData team)
    {
        // 1) 선택 팀 약어 저장
        PlayerPrefs.SetString(TradeTargetKey, team.abbreviation);

        // 2) TradeScene 로드
        SceneManager.LoadScene(TradeSceneName);
    }

    #endregion
} 