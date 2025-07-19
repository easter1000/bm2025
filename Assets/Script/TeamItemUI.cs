using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class TeamItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtTeamName;
    [SerializeField] private TextMeshProUGUI txtPlayers;
    [SerializeField] private TextMeshProUGUI txtRank;
    [SerializeField] private Button itemButton;

    private TeamData teamData;
    private Action<TeamData> onClickCallback;

    public void Init(TeamData data, Action<TeamData> onClick)
    {
        teamData = data;
        onClickCallback = onClick;
        txtTeamName.text = data.teamName;
        txtPlayers.text = string.Join(", ", data.players);
        txtRank.text = $"순위: {data.currentRank}";
        itemButton.onClick.RemoveAllListeners();
        itemButton.onClick.AddListener(() => onClickCallback?.Invoke(teamData));
    }
} 