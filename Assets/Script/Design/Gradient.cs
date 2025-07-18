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

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int i = 0; i < resolution; i++)
        {
            Color color = gradient.Evaluate((float)i / (resolution - 1));
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
    }
}