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

    [Header("Interaction")]
    [SerializeField] private Button itemButton;

    private TeamData teamData;
    private Action<TeamData> onClickCallback;

    private static readonly Color RowColorA = Color.white; // 주전 배경(흰색)
    private static readonly Color RowColorB = new Color32(0xF2, 0xF2, 0xF2, 0xFF); // 벤치 짝수
    private static readonly Color RowColorC = new Color32(0xE5, 0xE5, 0xE5, 0xFF); // 벤치 홀수

    public void Init(TeamData data, Action<TeamData> onClick)
    {
        teamData = data;
        onClickCallback = onClick;

        if (txtTeamName) txtTeamName.text = data.teamName;
        if (txtAbbreviation) txtAbbreviation.text = data.abbreviation;

        // -------------- Starting Players --------------
        var starters = data.playerLines.Take(5).ToList();
        // Helper 로컬 함수
        void Assign(PlayerLineController ctrl, string positionCode)
        {
            if (ctrl == null) return;
            PlayerLine pl = starters.FirstOrDefault(p => p.AssignedPosition == positionCode);
            if (pl == null) pl = starters.FirstOrDefault(p => p.Position == positionCode);
            if (pl == null) pl = starters.FirstOrDefault();
            if (pl == null) return;
            starters.Remove(pl);
            ctrl.SetPlayerLine(pl, RowColorA);
        }

        Assign(pgPlayer, "PG");
        Assign(sgPlayer, "SG");
        Assign(sfPlayer, "SF");
        Assign(pfPlayer, "PF");
        Assign(cPlayer, "C");

        // 만약 남는 주전이 있다면 나머지 슬롯에 순차 배치
        var remainingCtrls = new[] { pgPlayer, sgPlayer, sfPlayer, pfPlayer, cPlayer }.Where(c => c != null && string.IsNullOrEmpty(c.PlayerNameText.text)).ToList();
        foreach (var ctrl in remainingCtrls)
        {
            if (starters.Count == 0) break;
            var pl = starters[0];
            starters.RemoveAt(0);
            ctrl.SetPlayerLine(pl, RowColorA);
        }

        // -------------- Bench Players --------------
        if (benchContent != null)
        {
            foreach (Transform child in benchContent)
            {
                Destroy(child.gameObject);
            }

            var benchPlayers = data.playerLines.Skip(5).ToList();
            for (int i = 0; i < benchPlayers.Count; i++)
            {
                var plInstance = Instantiate(benchPlayerPrefab.gameObject, benchContent);
                var plc = plInstance.GetComponent<PlayerLineController>();
                if (plc == null) continue;

                Color bgColor = (i % 2 == 0) ? RowColorB : RowColorC;
                plc.SetPlayerLine(benchPlayers[i], bgColor);
            }
        }

        // 버튼 클릭 → 콜백
        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke(teamData));
        }
    }
} 