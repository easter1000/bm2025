using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Viewport 크기에 비례하여 GridLayoutGroup의 셀 크기를 동적으로 조정합니다.
/// TeamItemUI 카드가 해상도에 맞춰 자동 리사이즈되도록 하기 위한 스크립트입니다.
/// Content(=GridLayoutGroup) 오브젝트에 부착하세요.
/// </summary>
public class DynamicGridCellSize : MonoBehaviour
{

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

        // referenceRect가 지정되지 않았으면 ScrollRect의 Viewport를 자동 사용
        if (referenceRect == null)
        {
            ScrollRect sr = GetComponentInParent<ScrollRect>();
            if (sr != null && sr.viewport != null)
            {
                referenceRect = sr.viewport;
            }
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

        float newWidth = 1350;
        float newHeight = 900;
        grid.cellSize = new Vector2(newWidth, newHeight);
    }

    /// <summary>
    /// 외부에서 강제로 셀 크기 다시 계산.
    /// </summary>
    public void ForceUpdate()
    {
        UpdateCellSize();
    }
} 