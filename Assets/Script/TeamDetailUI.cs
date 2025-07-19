using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TeamDetailUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI txtTeamName;
    [SerializeField] private TextMeshProUGUI txtPlayers;
    [SerializeField] private TextMeshProUGUI txtRank;
    [SerializeField] private TextMeshProUGUI txtWinRate;

    public void Show(TeamData data)
    {
        txtTeamName.text = data.teamName;
        txtPlayers.text = "선수: " + string.Join(", ", data.players);
        txtRank.text = $"현재 순위: {data.currentRank}";
        txtWinRate.text = $"승률: {data.winRate:P1}";
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
} 