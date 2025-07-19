using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SnapScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Tooltip("스냅 이동 속도")] private float snapSpeed = 10f;
    [SerializeField, Tooltip("첫/마지막 카드 무한 루프 스크롤 허용")] private bool loop = true;

    private bool isDragging;
    private bool isLerping;
    private float[] pagePositions;
    private int currentPage;

    private void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
    }

    private void Start()
    {
        RecalculatePages();
    }

    public void RecalculatePages()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;

        // 레이아웃이 최신 상태가 아니면 강제로 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();

        int pageCount = content.childCount;
        if (pageCount <= 1 || grid == null)
        {
            pagePositions = null;
            return;
        }

        float cell = grid.cellSize.x;
        float spacingX = grid.spacing.x;
        float paddingLeft = grid.padding.left;

        float contentWidth = content.rect.width;
        float viewportWidth = viewport.rect.width;
        float maxScrollable = Mathf.Max(contentWidth - viewportWidth, 1f);

        pagePositions = new float[pageCount];

        for (int i = 0; i < pageCount; i++)
        {
            // 카드 i의 중앙이 Viewport 중앙에 오도록 필요한 Content 이동량 계산
            float centerOffset = paddingLeft + cell * 0.5f + i * (cell + spacingX);
            float desiredContentPos = Mathf.Clamp(centerOffset - viewportWidth * 0.5f, 0, maxScrollable);
            pagePositions[i] = desiredContentPos / maxScrollable;
        }

        currentPage = Mathf.Clamp(currentPage, 0, pagePositions.Length - 1);
    }

    private void Update()
    {
        if (isLerping && pagePositions != null && currentPage < pagePositions.Length)
        {
            float target = pagePositions[currentPage];
            float newPos = Mathf.Lerp(scrollRect.horizontalNormalizedPosition, target, Time.deltaTime * snapSpeed);
            scrollRect.horizontalNormalizedPosition = newPos;
            if (Mathf.Abs(newPos - target) < 0.001f) isLerping = false;
        }

        // 루프 스크롤 처리: 스냅 완료 후 클론 페이지에 위치하면 즉시 대응되는 실 페이지로 점프
        if (!isDragging && !isLerping && loop && pagePositions != null && pagePositions.Length > 2)
        {
            int lastIndex = pagePositions.Length - 1;
            if (currentPage == 0)
            {
                // 맨 앞(마지막 카드 클론) → 실제 마지막 카드
                currentPage = lastIndex - 1;
                scrollRect.horizontalNormalizedPosition = pagePositions[currentPage];
            }
            else if (currentPage == lastIndex)
            {
                // 맨 뒤(첫 카드 클론) → 실제 첫 카드
                currentPage = 1;
                scrollRect.horizontalNormalizedPosition = pagePositions[currentPage];
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        isLerping = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (pagePositions == null || pagePositions.Length == 0) return;
        float pos = scrollRect.horizontalNormalizedPosition;
        float min = float.MaxValue;
        for (int i = 0; i < pagePositions.Length; i++)
        {
            float dist = Mathf.Abs(pos - pagePositions[i]);
            if (dist < min)
            {
                min = dist;
                currentPage = i;
            }
        }
        isLerping = true;
    }
} 