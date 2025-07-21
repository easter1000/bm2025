using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace madcamp3.Assets.Script.Player
{
    public class PlayerLineController : MonoBehaviour, IPointerClickHandler
    {
        public TextMeshProUGUI PlayerNameText;
        public TextMeshProUGUI PositionText;
        public TextMeshProUGUI BackNumberText;
        public TextMeshProUGUI AgeText;
        public TextMeshProUGUI heightText;
        public TextMeshProUGUI WeightText;
        public TextMeshProUGUI OverallScoreText;
        public SliderController PotentialSlider;
        public Image BackgroundImage;
        [Header("Overall Score Background")] public Image OverallBackgroundImage;

        public PlayerLine Data { get; private set; }
        public event Action<PlayerLine> OnClicked;

        /// <summary>
        /// 전달된 PlayerLine 데이터를 UI 컴포넌트에 적용합니다.
        /// </summary>
        /// <param name="playerLine">선수 정보</param>
        public void SetPlayerLine(PlayerLine playerLine, Color backgroundColor)
        {
            if (playerLine == null)
            {
                Debug.LogWarning("PlayerLineController.SetPlayerLine : playerLine is null");
                return;
            }

            Data = playerLine;

            // 텍스트 필드 갱신
            if (PositionText) PositionText.text = playerLine.Position;
            if (BackNumberText) BackNumberText.text = playerLine.BackNumber.ToString();
            if (PlayerNameText) PlayerNameText.text = playerLine.PlayerName;
            if (AgeText) AgeText.text = playerLine.Age.ToString();
            if (heightText) heightText.text = playerLine.Height;
            if (WeightText) WeightText.text = playerLine.Weight.ToString();
            if (OverallScoreText) OverallScoreText.text = playerLine.OverallScore.ToString();

            // Overall 배경색 결정
            if (OverallBackgroundImage)
            {
                Color ovColor;
                int score = playerLine.OverallScore;
                if (score >= 80)
                {
                    ColorUtility.TryParseHtmlString("#4147F5", out ovColor);
                }
                else if (score >= 60)
                {
                    ColorUtility.TryParseHtmlString("#00CA51", out ovColor);
                }
                else
                {
                    ColorUtility.TryParseHtmlString("#FF0C0C", out ovColor);
                }
                OverallBackgroundImage.color = ovColor;
            }

            // 슬라이더 잠재력 표시 (0~99)
            if (PotentialSlider) PotentialSlider.SetSliderValue(playerLine.Potential);

            if (BackgroundImage) BackgroundImage.color = backgroundColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Data != null)
            {
                OnClicked?.Invoke(Data);
            }
        }
    }
}