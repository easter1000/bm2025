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
    [SerializeField] private Image teamLogoImage;

    [Header("Selection Colors")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color todayColor = new Color(0.6f, 1f, 0.6f); // 밝은 초록색

    private DateTime _date;
    private bool _validDate;
    private Color _baseColor;

    public event Action<DateTime> OnCellClicked;

    /// <summary>
    /// 셀 설정.
    /// </summary>
    public void Configure(int day, int month, int year, bool isToday, Sprite displayLogo, bool isUserGame)
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
            // 오늘 날짜이면 초록색, 아니면 기본색을 베이스 색으로 설정
            _baseColor = isToday ? todayColor : defaultColor;
            backgroundImage.color = _baseColor;
        }

        // 로고 처리
        // 내 팀이 포함된 경기(isUserGame)가 있을 때만 로고를 표시하도록 변경
        if (_validDate && isUserGame && displayLogo != null)
        {
            teamLogoImage.sprite = displayLogo;
            teamLogoImage.gameObject.SetActive(true);
        }
        else
        {
            teamLogoImage.gameObject.SetActive(false);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (backgroundImage != null && _validDate)
        {
            backgroundImage.color = isSelected ? selectedColor : _baseColor;
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