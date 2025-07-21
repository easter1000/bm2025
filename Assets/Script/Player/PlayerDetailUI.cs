using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace madcamp3.Assets.Script.Player
{
    /// <summary>
    /// 선택된 선수의 상세 정보를 표시하는 UI 컨트롤러.
    /// 프리팹에서 각 Text / Image 레퍼런스를 연결해주면 됩니다.
    /// </summary>
    public class PlayerDetailUI : MonoBehaviour
    {
        [Header("Basic Info")]
        [SerializeField] private TextMeshProUGUI txtName;
        [SerializeField] private TextMeshProUGUI txtAge;
        [SerializeField] private TextMeshProUGUI txtHeight;
        [SerializeField] private TextMeshProUGUI txtWeight;
        [SerializeField] private TextMeshProUGUI txtBackNumber;
        [SerializeField] private TextMeshProUGUI txtPosition;
        [SerializeField] private Image imgPortrait;

        [Header("Individual Stats")]
        [SerializeField] private TextMeshProUGUI txtCloseShot;
        [SerializeField] private TextMeshProUGUI txtDrawFoul;
        [SerializeField] private TextMeshProUGUI txtStamina;
        [SerializeField] private TextMeshProUGUI txtMidRangeShot;
        [SerializeField] private TextMeshProUGUI txtInteriorDef;
        [SerializeField] private TextMeshProUGUI txtPassIQ;
        [SerializeField] private TextMeshProUGUI txtThreePoint;
        [SerializeField] private TextMeshProUGUI txtPerimeterDef;
        [SerializeField] private TextMeshProUGUI txtBallHandle;
        [SerializeField] private TextMeshProUGUI txtFreeThrow;
        [SerializeField] private TextMeshProUGUI txtSteal;
        [SerializeField] private TextMeshProUGUI txtRebOff;
        [SerializeField] private TextMeshProUGUI txtLayup;
        [SerializeField] private TextMeshProUGUI txtBlock;
        [SerializeField] private TextMeshProUGUI txtRebDef;
        [SerializeField] private TextMeshProUGUI txtDrivingDunk;
        [SerializeField] private TextMeshProUGUI txtSpeed;

        [Header("Averages (Texts)")]
        [SerializeField] private TextMeshProUGUI txtAvgOverall;
        [SerializeField] private TextMeshProUGUI txtAvgOffense;
        [SerializeField] private TextMeshProUGUI txtAvgDefense;
        [SerializeField] private TextMeshProUGUI txtAvgPhysical;
        [SerializeField] private TextMeshProUGUI txtAvgPotential;

        [Header("Averages (Background Images)")]
        [SerializeField] private Image bgAvgOverall;
        [SerializeField] private Image bgAvgOffense;
        [SerializeField] private Image bgAvgDefense;
        [SerializeField] private Image bgAvgPhysical;
        [SerializeField] private Image bgAvgPotential;

        /// <summary>
        /// UI에 선수 정보를 세팅한다.
        /// </summary>
        public void SetPlayer(PlayerRating rating)
        {
            if (rating == null) return;

            if (txtName) txtName.text = rating.name;
            if (txtAge) txtAge.text = rating.age.ToString();
            if (txtHeight) txtHeight.text = rating.height;
            if (txtWeight) txtWeight.text = rating.weight.ToString();
            if (txtBackNumber) txtBackNumber.text = rating.backNumber.ToString();
            if (txtPosition) txtPosition.text = PositionCodeToString(rating.position);

            // 포트레이트 로드
            if (imgPortrait)
            {
                Sprite spr = Resources.Load<Sprite>($"player_photos/{rating.player_id}");
                if (spr == null)
                {
                    spr = Resources.Load<Sprite>("player_photos/default_image");
                }
                imgPortrait.sprite = spr;
                AdjustPortrait();
            }

            if (txtCloseShot) txtCloseShot.text = rating.closeShot.ToString();
            if (txtDrawFoul) txtDrawFoul.text = rating.drawFoul.ToString();
            if (txtStamina) txtStamina.text = rating.stamina.ToString();
            if (txtMidRangeShot) txtMidRangeShot.text = rating.midRangeShot.ToString();
            if (txtInteriorDef) txtInteriorDef.text = rating.interiorDefense.ToString();
            if (txtPassIQ) txtPassIQ.text = rating.passIQ.ToString();
            if (txtThreePoint) txtThreePoint.text = rating.threePointShot.ToString();
            if (txtPerimeterDef) txtPerimeterDef.text = rating.perimeterDefense.ToString();
            if (txtBallHandle) txtBallHandle.text = rating.ballHandle.ToString();
            if (txtFreeThrow) txtFreeThrow.text = rating.freeThrow.ToString();
            if (txtSteal) txtSteal.text = rating.steal.ToString();
            if (txtRebOff) txtRebOff.text = rating.offensiveRebound.ToString();
            if (txtLayup) txtLayup.text = rating.layup.ToString();
            if (txtBlock) txtBlock.text = rating.block.ToString();
            if (txtRebDef) txtRebDef.text = rating.defensiveRebound.ToString();
            if (txtDrivingDunk) txtDrivingDunk.text = rating.drivingDunk.ToString();
            if (txtSpeed) txtSpeed.text = rating.speed.ToString();

            // ---- Sector averages ----
            float overall = rating.overallAttribute;

            float offense = Average(
                rating.closeShot, rating.midRangeShot, rating.threePointShot, rating.freeThrow, rating.layup,
                rating.drivingDunk, rating.drawFoul, rating.passIQ, rating.ballHandle, rating.offensiveRebound);

            float defense = Average(
                rating.interiorDefense, rating.perimeterDefense, rating.steal, rating.block, rating.defensiveRebound);

            float physical = Average(rating.speed, rating.stamina);
            float potential = rating.potential;

            SetAverage(txtAvgOverall, bgAvgOverall, overall);
            SetAverage(txtAvgOffense, bgAvgOffense, offense);
            SetAverage(txtAvgDefense, bgAvgDefense, defense);
            SetAverage(txtAvgPhysical, bgAvgPhysical, physical);
            SetAverage(txtAvgPotential, bgAvgPotential, potential);
        }

        #region Helpers
        private float Average(params int[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            int sum = 0;
            foreach (var v in values) sum += v;
            return (float)sum / values.Length;
        }

        private void SetAverage(TextMeshProUGUI txt, Image bg, float value)
        {
            if (txt) txt.text = Mathf.RoundToInt(value).ToString();
            if (bg) bg.color = GetColorByScore(value);
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

        private string PositionCodeToString(int code)
        {
            return code switch
            {
                1 => "PG",
                2 => "SG",
                3 => "SF",
                4 => "PF",
                5 => "C",
                _ => "?"
            };
        }

        private void AdjustPortrait()
        {
            if (imgPortrait == null || imgPortrait.sprite == null) return;

            imgPortrait.preserveAspect = true;

            // Pivot & anchor: bottom-center
            RectTransform rt = imgPortrait.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            // Fit to container while keeping aspect ratio; align bottom center, never overflow
            RectTransform parentRT = rt.parent as RectTransform;
            if (parentRT == null) return;

            float containerW = parentRT.rect.width;
            float containerH = parentRT.rect.height;

            float spriteW = imgPortrait.sprite.texture.width;
            float spriteH = imgPortrait.sprite.texture.height;
            float spriteAspect = spriteW / spriteH;
            float containerAspect = containerW / containerH;

            float finalW, finalH;
            if (spriteAspect >= containerAspect)
            {
                // 이미지가 상대적으로 넓음 → 너비 기준 맞춤
                finalW = containerW;
                finalH = finalW / spriteAspect;
            }
            else
            {
                // 이미지가 상대적으로 높음 → 높이 기준 맞춤
                finalH = containerH;
                finalW = finalH * spriteAspect;
            }

            rt.sizeDelta = new Vector2(finalW, finalH);
            rt.anchoredPosition = new Vector2(0f, 0f); // bottom center
        }
        #endregion
    }
} 