using UnityEngine;
using UnityEngine.UI; // UI 관련 클래스를 사용하기 위해 필수!
using TMPro;

public class SliderController : MonoBehaviour
{
    // Inspector 창에서 제어할 슬라이더를 연결해줍니다.
    public Slider targetSlider;
    [SerializeField]
    public TextMeshProUGUI valueText;
    public int minValue = 0;
    public int maxValue = 99;

    [SerializeField]
    private int sliderValue = 0;

    void Start()
    {
        if (targetSlider != null)
        {
            // Slider는 0~1의 정규화된 값을 사용합니다.
            targetSlider.minValue = 0f;
            targetSlider.maxValue = 1f;
            targetSlider.wholeNumbers = false; // 부드럽게 이동

            // Inspector 초기값(int) → 정규화 값으로 변환
            sliderValue = Mathf.Clamp(sliderValue, minValue, maxValue);
            float normalized = (maxValue == minValue) ? 0f : (sliderValue - minValue) / (float)(maxValue - minValue);
            targetSlider.value = Mathf.Clamp01(normalized);

            // 값이 바뀔 때마다 UI 및 변수 동기화
            targetSlider.onValueChanged.AddListener(OnSliderValueChanged);

            // 첫 텍스트 갱신
            OnSliderValueChanged(targetSlider.value);
        }
    }

    private void OnSliderValueChanged(float normalizedValue)
    {
        // 정규화 값(0~1)을 실제 정수 값으로 변환
        sliderValue = Mathf.RoundToInt(normalizedValue * (maxValue - minValue) + minValue);

        if (valueText != null)
        {
            valueText.text = sliderValue.ToString();
        }
    }

    // 외부에서 직접 값을 설정할 수 있는 함수
    public void SetSliderValue(int value)
    {
        // 값의 범위를 minValue와 maxValue 사이로 제한합니다.
        sliderValue = Mathf.Clamp(value, minValue, maxValue);

        if (targetSlider != null && maxValue != minValue)
        {
            float normalized = (sliderValue - minValue) / (float)(maxValue - minValue);
            targetSlider.value = Mathf.Clamp01(normalized);
        }

        if (valueText != null)
        {
            valueText.text = sliderValue.ToString();
        }
    }
}