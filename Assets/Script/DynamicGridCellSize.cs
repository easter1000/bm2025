using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Viewport 크기에 비례하여 GridLayoutGroup의 셀 크기를 동적으로 조정합니다.
/// TeamItemUI 카드가 해상도에 맞춰 자동 리사이즈되도록 하기 위한 스크립트입니다.
/// Content(=GridLayoutGroup) 오브젝트에 부착하세요.
/// </summary>
public class DynamicGridCellSize : MonoBehaviour
{
    [Tooltip("셀 폭 = 기준 RectTransform.width * widthPercent")] [Range(0.05f, 1f)]
    [SerializeField] private float widthPercent = 0.8f;

    [Tooltip("셀 높이 = 기준 RectTransform.height * heightPercent")] [Range(0.05f, 1f)]
    [SerializeField] private float heightPercent = 0.6f;

    [Tooltip("크기 계산에 사용할 RectTransform (비워두면 Content 자신을 사용)")]
    [SerializeField] private RectTransform referenceRect;

    private GridLayoutGroup grid;
    private RectTransform selfRect;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        selfRect = transform as RectTransform;
        if (grid == null)
        {
            Debug.LogError("DynamicGridCellSize: GridLayoutGroup가 필요합니다.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        UpdateCellSize();
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateCellSize();
    }

    private void UpdateCellSize()
    {
        RectTransform baseRect = referenceRect != null ? referenceRect : selfRect;
        if (baseRect == null) return;

        float newWidth = baseRect.rect.width * widthPercent;
        float newHeight = baseRect.rect.height * heightPercent;
        grid.cellSize = new Vector2(newWidth, newHeight);
    }
} 