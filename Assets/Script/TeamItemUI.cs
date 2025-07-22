using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using madcamp3.Assets.Script.Player;

public class TeamItemUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI txtTeamName;
    [SerializeField] private TextMeshProUGUI txtAbbreviation;
    [SerializeField] private Image teamColor;

    [Header("Logo")]
    [SerializeField] private Image imgTeamLogo;

    [Header("Starting Players (5)")]
    [SerializeField] private PlayerLineController pgPlayer;
    [SerializeField] private PlayerLineController sgPlayer;
    [SerializeField] private PlayerLineController sfPlayer;
    [SerializeField] private PlayerLineController pfPlayer;
    [SerializeField] private PlayerLineController cPlayer;

    [Header("Bench")]
    [SerializeField] private RectTransform benchContent; // ScrollView Viewport 하위 Content
    [SerializeField] private PlayerLineController benchPlayerPrefab;

    [Header("Team Averages")]
    [SerializeField] private TextMeshProUGUI startingAvgText;
    [SerializeField] private Image startingAvgBackground;
    [SerializeField] private TextMeshProUGUI substituteAvgText;
    [SerializeField] private Image substituteAvgBackground;

    [Header("Interaction")]
    [SerializeField] private Button itemButton;

    [Header("Player Detail")]
    [SerializeField] private PlayerDetailUI playerDetailUI;

    private TeamData teamData;
    private Action<TeamData> onClickCallback;

    private static readonly Color RowColorEven = new Color32(0xF2, 0xF2, 0xF2, 0xFF); // 짝수 행
    private static readonly Color RowColorOdd  = new Color32(0xE5, 0xE5, 0xE5, 0xFF); // 홀수 행

    // 선택된 PlayerLine에 표시되는 하이라이트(검은색 테두리)
    private GameObject highlightedPlayerObj;

    public void Init(TeamData data, Action<TeamData> onClick)
    {
        teamData = data;
        onClickCallback = onClick;

        if (txtTeamName) txtTeamName.text = data.teamName;
        if (txtAbbreviation) txtAbbreviation.text = data.abbreviation;

        // 팀 로고 로드
        if (imgTeamLogo != null)
        {
            Sprite logo = Resources.Load<Sprite>($"team_photos/{data.abbreviation}");
            if (logo == null)
            {
                logo = Resources.Load<Sprite>("team_photos/default_logo");
            }
            imgTeamLogo.sprite = logo;
            imgTeamLogo.preserveAspect = true;
        }

        if (teamColor != null)
        {
            teamColor.color = data.teamColor;
        }

        // -------------- Starting Players --------------
        // DB에서 해당 팀 엔티티를 가져와 best_five(주전 5명 ID 목록)를 이용한다.
        HashSet<int> starterIds = new HashSet<int>();

        var teamEntity = LocalDbManager.Instance.GetTeam(data.abbreviation);
        if (teamEntity != null && !string.IsNullOrEmpty(teamEntity.best_five))
        {
            foreach (string idStr in teamEntity.best_five.Split(','))
            {
                if (int.TryParse(idStr, out int pid)) starterIds.Add(pid);
            }
        }

        // starterIds가 비어있으면 기존 로직과 동일하게 상위 5명(정렬된 playerLines) 사용
        var starters = starterIds.Count > 0
            ? data.playerLines.Where(pl => starterIds.Contains(pl.PlayerId)).Take(5).ToList()
            : data.playerLines.Take(5).ToList();

        // ---- Team Average 계산 ----
        if (startingAvgText != null && startingAvgBackground != null)
        {
            float avgStart = starters.Count > 0 ? (float)starters.Average(p => p.OverallScore) : 0f;
            startingAvgText.text = Mathf.RoundToInt(avgStart).ToString();
            startingAvgBackground.color = GetColorByScore(avgStart);
        }

        var benchPlayersList = data.playerLines.Skip(5).ToList();
        if (substituteAvgText != null && substituteAvgBackground != null)
        {
            float avgSub = benchPlayersList.Count > 0 ? (float)benchPlayersList.Average(p => p.OverallScore) : 0f;
            substituteAvgText.text = Mathf.RoundToInt(avgSub).ToString();
            substituteAvgBackground.color = GetColorByScore(avgSub);
        }

        PlayerLineController[] starterCtrls = { pgPlayer, sgPlayer, sfPlayer, pfPlayer, cPlayer };

        // Helper local method
        void RegisterClick(PlayerLineController plc)
        {
            if (plc == null) return;
            plc.OnClicked -= ShowPlayerDetail;
            plc.OnClicked += ShowPlayerDetail;

            // 하이라이트 람다 추가 (중복 가능성 낮음)
            plc.OnClicked += (pl) => UpdatePlayerHighlight(plc.gameObject);
        }

        for (int idx = 0; idx < starterCtrls.Length; idx++)
        {
            var ctrl = starterCtrls[idx];
            if (ctrl == null) continue;

            string desiredPos = idx switch
            {
                0 => "PG",
                1 => "SG",
                2 => "SF",
                3 => "PF",
                4 => "C",
                _ => ""
            };

            PlayerLine pl = starters.FirstOrDefault(p => p.AssignedPosition == desiredPos);
            if (pl == null) pl = starters.FirstOrDefault(p => p.Position == desiredPos);
            if (pl == null) pl = starters.FirstOrDefault();
            if (pl == null) continue;

            starters.Remove(pl);

            // 반대 순서 색: index 0 -> Odd, 1 -> Even, ...
            Color bg = (idx % 2 == 0) ? RowColorOdd : RowColorEven;
            ctrl.SetPlayerLine(pl, bg);

            RegisterClick(ctrl);
        }

        // 아직 남은 주전(드물겠지만) 나머지 슬롯 채우기
        for (int idx = 0; idx < starterCtrls.Length && starters.Count > 0; idx++)
        {
            var ctrl = starterCtrls[idx];
            if (ctrl == null) continue;
            if (!string.IsNullOrEmpty(ctrl.PlayerNameText.text)) continue; // 이미 채워짐

            var pl = starters[0];
            starters.RemoveAt(0);
            Color bg = (idx % 2 == 0) ? RowColorOdd : RowColorEven;
            ctrl.SetPlayerLine(pl, bg);
            RegisterClick(ctrl);
        }

        // -------------- Bench Players --------------
        if (benchContent != null)
        {
            // 레이아웃 그룹 & 컨텐츠 사이즈 피터 설정 (없으면 추가)
            var vlg = benchContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg == null) vlg = benchContent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 0f;

            var fitter = benchContent.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter == null) fitter = benchContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            // anchor/pivot을 상단 중앙으로
            var benchRT = benchContent;
            benchRT.anchorMin = new Vector2(0.5f, 1f);
            benchRT.anchorMax = new Vector2(0.5f, 1f);
            benchRT.pivot = new Vector2(0.5f, 1f);
            benchRT.anchoredPosition = Vector2.zero;

            // 기존 자식 제거
            foreach (Transform child in benchContent)
            {
                Destroy(child.gameObject);
            }

            var benchPlayers = data.playerLines.Skip(5).ToList();
            for (int i = 0; i < benchPlayers.Count; i++)
            {
                var plInstance = Instantiate(benchPlayerPrefab.gameObject, benchContent);
                plInstance.transform.localScale = Vector3.one;
                var plc = plInstance.GetComponent<PlayerLineController>();
                if (plc == null) continue;

                // LayoutElement 설정하여 높이 60, 너비 자동
                var le = plInstance.GetComponent<UnityEngine.UI.LayoutElement>();
                if (le == null) le = plInstance.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredHeight = 60f;
                le.minHeight = 60f;
                le.flexibleHeight = 0f;

                le.preferredWidth = 842f;
                le.minWidth = 842f;
                le.flexibleWidth = 0f;

                Color bgColor = (i % 2 == 0) ? RowColorEven : RowColorOdd;
                plc.SetPlayerLine(benchPlayers[i], bgColor);
                RegisterClick(plc);
            }
        }

        // 버튼 클릭 → 팀 선택 콜백 (확인 다이얼로그는 NewGameManager에서 처리)
        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => {
                onClickCallback?.Invoke(teamData);
            });
        }

        // 초기 상세 정보: 첫 번째 주전 선수를 표시
        if (playerDetailUI != null && starterCtrls.Length > 0 && starterCtrls[0] != null && starterCtrls[0].Data != null)
        {
            ShowPlayerDetail(starterCtrls[0].Data);
            UpdatePlayerHighlight(starterCtrls[0].gameObject);
        }

        void ShowPlayerDetail(PlayerLine pl)
        {
            if (playerDetailUI == null || pl == null) return;
            var rating = LocalDbManager.Instance.GetAllPlayerRatings().FirstOrDefault(r => r.player_id == pl.PlayerId);
            if (rating != null)
            {
                playerDetailUI.SetPlayer(rating);
            }
        }

        // ----- 내부 메서드 : 플레이어 라인 하이라이트 -----
        void UpdatePlayerHighlight(GameObject newObj)
        {
            if (highlightedPlayerObj == newObj) return;

            // 1) 기존 BorderLines 비활성화
            if (highlightedPlayerObj != null)
            {
                Transform prevBorder = highlightedPlayerObj.transform.Find("BorderLines");
                if (prevBorder != null) prevBorder.gameObject.SetActive(false);
            }

            // 2) 새 BorderLines 생성/활성화
            if (newObj != null)
            {
                Transform borderT = newObj.transform.Find("BorderLines");
                if (borderT == null)
                {
                    GameObject borderRoot = new GameObject("BorderLines", typeof(RectTransform));
                    borderRoot.transform.SetParent(newObj.transform, false);
                    borderRoot.transform.SetAsLastSibling();

                    RectTransform rootRT = borderRoot.GetComponent<RectTransform>();
                    rootRT.anchorMin = Vector2.zero;
                    rootRT.anchorMax = Vector2.one;
                    const float thickness = 3f;
                    float inset = thickness * 0.5f; // 테두리 절반만큼 안쪽으로 들여 그린다
                    rootRT.offsetMin = new Vector2(inset, inset);
                    rootRT.offsetMax = new Vector2(-inset, -inset);

                    Color borderColor = Color.black;

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

            highlightedPlayerObj = newObj;
        }
    }

    private Color GetColorByScore(float score)
    {
        if (score >= 80f)
        {
            ColorUtility.TryParseHtmlString("#4147F5", out Color c);
            return c;
        }
        else if (score >= 60f)
        {
            ColorUtility.TryParseHtmlString("#00CA51", out Color c);
            return c;
        }
        ColorUtility.TryParseHtmlString("#FF0C0C", out Color col);
        return col;
    }
} 