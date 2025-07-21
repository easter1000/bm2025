using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class GradientBackground : MonoBehaviour
{
    public Gradient gradient;
    [Tooltip("가로 그라데이션 여부")]
    [SerializeField] public bool isHorizontal = true;
    // 새로 추가: 그라데이션 해상도
    [SerializeField, Range(2, 1024)] private int resolution = 256;

    void Awake()
    {
        Image image = GetComponent<Image>();

        int width = isHorizontal ? resolution : 1;
        int height = isHorizontal ? 1 : resolution;

        // 텍스처를 항상 sRGB 공간으로 만들고, 필요 시 색을 변환해 저장합니다.
        bool isLinearSpace = (QualitySettings.activeColorSpace == ColorSpace.Linear);

        // linear 파라미터를 false 로 두어 텍스처를 sRGB로 생성합니다.
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int i = 0; i < resolution; i++)
        {
            Color color = gradient.Evaluate((float)i / (resolution - 1));
            // Linear 컬러 스페이스일 경우, GPU에서 sRGB → Linear 변환이 수행되므로
            // 텍스처 픽셀은 sRGB 값이어야 합니다. gradient.Evaluate 는 sRGB(gamma) 값을 반환하므로 그대로 사용.
            if (isHorizontal)
            {
                texture.SetPixel(i, 0, color);
            }
            else
            {
                texture.SetPixel(0, i, color);
            }
        }
        texture.Apply();

        image.sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));

        // Image.color 값이 스프라이트 색상에 곱해져 그라데이션 색이 변질되는 문제 방지
        // 알파(투명도)만 유지하고 RGB는 1,1,1로 고정하여 실질적으로 색상 영향이 없도록 한다.
        // 색상은 순수 White(1,1,1), 알파도 1로 고정하여 완전히 Image.color의 영향을 제거합니다.
        image.color = Color.white;
    }
}