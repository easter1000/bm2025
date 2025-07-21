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

        bool linear = (QualitySettings.activeColorSpace == ColorSpace.Linear);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int i = 0; i < resolution; i++)
        {
            Color color = gradient.Evaluate((float)i / (resolution - 1));
            // 색 공간 보정: Linear 모드일 때 Gamma → Linear 변환 필요 없음, 이미 linear 옵션으로 처리됨
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
        Color orig = image.color;
        image.color = new Color(1f, 1f, 1f, orig.a);
    }
}