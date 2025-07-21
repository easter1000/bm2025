using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 달력 그리드의 단일 날짜 셀. 날짜 표시, 로고 표시, 클릭 이벤트를 제공한다.
/// </summary>
public class CalendarCell : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image leagueLogoImage;
    [SerializeField] private Image teamLogoImage;

    [Header("Assets")]
    [SerializeField] private string nbaLogoResourcePath = "team_photos/nba";

    private Sprite _nbaLogo;

    private DateTime _date;
    private bool _validDate;

    public event Action<DateTime> OnCellClicked;

    private void Awake()
    {
        if (string.IsNullOrEmpty(nbaLogoResourcePath) == false)
        {
            _nbaLogo = Resources.Load<Sprite>(nbaLogoResourcePath);
        }
    }

    /// <summary>
    /// 셀 설정.
    /// </summary>
    public void Configure(int day, int month, int year, bool hasGame, Sprite displayLogo, bool isUserGame)
    {
        _validDate = day > 0;
        if (_validDate)
        {
            _date = new DateTime(year, month, day);
        }

        // 날짜 텍스트/배경 처리
        backgroundImage.gameObject.SetActive(_validDate);
        dateText.gameObject.SetActive(_validDate);
        if (_validDate)
        {
            dateText.text = day.ToString();
        }

        // 로고 처리
        // 내 팀이 포함된 경기(isUserGame)가 있을 때만 로고를 표시하도록 변경
        if (_validDate && isUserGame && displayLogo != null)
        {
            if (_nbaLogo != null) leagueLogoImage.sprite = _nbaLogo;
            teamLogoImage.sprite = displayLogo;
            leagueLogoImage.gameObject.SetActive(true);
            teamLogoImage.gameObject.SetActive(true);
        }
        else
        {
            leagueLogoImage.gameObject.SetActive(false);
            teamLogoImage.gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_validDate)
        {
            OnCellClicked?.Invoke(_date);
        }
    }
} 