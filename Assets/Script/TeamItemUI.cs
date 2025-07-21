using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using TMPro;
using madcamp3.Assets.Script.Player;

public class TeamItemUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI txtTeamName;
    [SerializeField] private TextMeshProUGUI txtAbbreviation;

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

    public void Init(TeamData data, Action<TeamData> onClick)
    {
        teamData = data;
        onClickCallback = onClick;

        if (txtTeamName) txtTeamName.text = data.teamName;
        if (txtAbbreviation) txtAbbreviation.text = data.abbreviation;

        // -------------- Starting Players --------------
        var starters = data.playerLines.Take(5).ToList();

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
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke(teamData));
        }

        // 초기 상세 정보: 첫 번째 주전 선수를 표시
        if (playerDetailUI != null && starterCtrls.Length > 0 && starterCtrls[0] != null && starterCtrls[0].Data != null)
        {
            ShowPlayerDetail(starterCtrls[0].Data);
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