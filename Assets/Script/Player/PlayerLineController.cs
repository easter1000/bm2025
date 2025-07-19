using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace madcamp3.Assets.Script.Player
{
    public class PlayerLineController : MonoBehaviour
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

            // 텍스트 필드 갱신
            if (PositionText) PositionText.text = playerLine.Position;
            if (BackNumberText) BackNumberText.text = playerLine.BackNumber.ToString();
            if (PlayerNameText) PlayerNameText.text = playerLine.PlayerName;
            if (AgeText) AgeText.text = playerLine.Age.ToString();
            if (heightText) heightText.text = playerLine.Height;
            if (WeightText) WeightText.text = playerLine.Weight.ToString();
            if (OverallScoreText) OverallScoreText.text = playerLine.OverallScore.ToString();

            // 슬라이더 잠재력 표시 (0~99)
            if (PotentialSlider) PotentialSlider.SetSliderValue(playerLine.Potential);

            if (BackgroundImage) BackgroundImage.color = backgroundColor;
        }
    }
}