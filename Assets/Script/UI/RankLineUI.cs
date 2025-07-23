using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankLineUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image teamLogo;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI teamAbbrText;
    [SerializeField] private TextMeshProUGUI teamNameText;
    [SerializeField] private TextMeshProUGUI winsText;
    [SerializeField] private TextMeshProUGUI lossesText;

    /// <summary>
    /// 순위 라인 UI를 팀 데이터로 초기화합니다.
    /// </summary>
    public void Setup(int rank, Team teamData, TeamFinance teamFinance)
    {
        if (teamData == null || teamFinance == null) return;

        // 1. 순위, 팀 약어, 팀 이름 설정
        if (rankText) rankText.text = GetRankString(rank);
        if (teamAbbrText) teamAbbrText.text = teamData.team_abbv;
        if (teamNameText) teamNameText.text = teamData.team_name;

        // 2. 승리 및 패배 텍스트 설정
        if (winsText) winsText.text = $"Win: {teamFinance.Wins}";
        if (lossesText) lossesText.text = $"Lose: {teamFinance.Losses}";

        // 3. 팀 로고 설정
        if (teamLogo)
        {
            Sprite logoSprite = Resources.Load<Sprite>($"team_photos/{teamData.team_abbv.ToLower()}");
            if (logoSprite != null)
            {
                teamLogo.sprite = logoSprite;
            }
            else
            {
                // 기본 로고 또는 에러 처리
                Debug.LogWarning($"Logo for {teamData.team_abbv} not found.");
            }
        }
    }

    /// <summary>
    /// 숫자를 서수 형식의 문자열로 변환합니다. (1 -> 1st, 2 -> 2nd)
    /// </summary>
    private string GetRankString(int rank)
    {
        if (rank <= 0) return rank.ToString();

        switch (rank % 100)
        {
            case 11:
            case 12:
            case 13:
                return rank + "th";
        }

        switch (rank % 10)
        {
            case 1:
                return rank + "st";
            case 2:
                return rank + "nd";
            case 3:
                return rank + "rd";
            default:
                return rank + "th";
        }
    }
} 