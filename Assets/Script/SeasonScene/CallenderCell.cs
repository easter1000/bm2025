using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 시즌 달력에서 하루를 표현하는 셀. 날짜, 경기 여부, 로고 표시를 담당한다.
/// </summary>
public class CallenderCell : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text dateText;          // 날짜 표시 텍스트
    [SerializeField] private Image backgroundImage;      // 셀 배경 또는 캘린더 슬롯 이미지
    [SerializeField] private Image leagueLogoImage;      // NBA 로고
    [SerializeField] private Image teamLogoImage;        // 상대팀 로고

    [Header("Assets")]
    [Tooltip("Resources/Images 폴더 등에 존재하는 nba.png 경로")] 
    [SerializeField] private string nbaLogoResourcePath = "Images/nba"; // 확장자 제외

    private Sprite _nbaLogo;

    private void Awake()
    {
        // Resources 에서 NBA 로고를 한 번만 로드해 캐싱
        if (_nbaLogo == null)
        {
            _nbaLogo = Resources.Load<Sprite>(nbaLogoResourcePath);
            if (_nbaLogo == null)
            {
                Debug.LogWarning($"[CallenderCell] NBA 로고를 '{nbaLogoResourcePath}'에서 찾지 못했습니다.");
            }
        }
    }

    /// <summary>
    /// 셀 정보를 설정한다.
    /// </summary>
    /// <param name="day">1~31 사이 유효 날짜. 0 이하이면 비어있는 날로 간주.</param>
    /// <param name="hasGame">경기 일정 여부</param>
    /// <param name="opponentLogo">상대팀 로고 스프라이트 (hasGame 이 true 일 때만 사용)</param>
    public void Configure(int day, bool hasGame, Sprite opponentLogo)
    {
        bool validDate = day > 0;

        // 1) 날짜 및 셀 활성화 처리
        backgroundImage.gameObject.SetActive(validDate);
        dateText.gameObject.SetActive(validDate);
        if (validDate)
        {
            dateText.text = day.ToString();
        }

        // 2) 경기 여부에 따른 로고 표시
        if (validDate && hasGame && opponentLogo != null && _nbaLogo != null)
        {
            leagueLogoImage.sprite = _nbaLogo;
            teamLogoImage.sprite = opponentLogo;

            leagueLogoImage.gameObject.SetActive(true);
            teamLogoImage.gameObject.SetActive(true);
        }
        else
        {
            leagueLogoImage.gameObject.SetActive(false);
            teamLogoImage.gameObject.SetActive(false);
        }
    }
} 