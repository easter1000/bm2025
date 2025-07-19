using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class SnapScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Tooltip("스냅 이동 속도")] private float snapSpeed = 10f;
    [SerializeField, Tooltip("첫/마지막 카드 무한 루프 스크롤 허용")] private bool loop = true;

    private bool isDragging;
    private bool isLerping;
    private float[] pagePositions;
    private int currentPage;
    private float dragStartPos;
    private int startDragPage;
    private Coroutine recalcRoutine;

    private void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
    }

    private void Start()
    {
        RecalculatePages(); // schedules coroutine
    }

    public void RecalculatePages()
    {
        if (recalcRoutine != null) StopCoroutine(recalcRoutine);
        recalcRoutine = StartCoroutine(RecalculatePagesCoroutine());
    }

    private IEnumerator RecalculatePagesCoroutine()
    {
        // 한 프레임 대기하여 레이아웃 시스템이 안정된 뒤 계산
        yield return null;

        if (scrollRect == null || scrollRect.content == null) yield break;
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        int pageCount = content.childCount;
        if (pageCount <= 1 || grid == null)
        {
            pagePositions = null;
            yield break;
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
            float centerOffset = paddingLeft + cell * 0.5f + i * (cell + spacingX);
            float desiredContentPos = Mathf.Clamp(centerOffset - viewportWidth * 0.5f, 0, maxScrollable);
            pagePositions[i] = desiredContentPos / maxScrollable;
        }

        currentPage = Mathf.Clamp(currentPage, 0, pagePositions.Length - 1);
        isLerping = false;
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

        // 수직 이동 방지: Y 값을 항상 0으로 고정
        if (scrollRect != null && scrollRect.content != null)
        {
            Vector2 ap = scrollRect.content.anchoredPosition;
            if (Mathf.Abs(ap.y) > 0.001f)
            {
                ap.y = 0f;
                scrollRect.content.anchoredPosition = ap;
            }
        }
    }

    /// <summary>
    /// 외부에서 특정 페이지로 이동하고 싶을 때 호출 (인덱스는 0~pageCount-1).
    /// instant=true면 즉시 이동, false면 스냅 Lerp.
    /// </summary>
    public void JumpToPage(int index, bool instant = true)
    {
        if (pagePositions == null || pagePositions.Length == 0) return;
        currentPage = Mathf.Clamp(index, 0, pagePositions.Length - 1);
        if (instant)
        {
            scrollRect.horizontalNormalizedPosition = pagePositions[currentPage];
            isLerping = false;
        }
        else
        {
            isLerping = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        isLerping = false;

        dragStartPos = scrollRect.horizontalNormalizedPosition;
        startDragPage = currentPage;
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

        // 작은 스와이프에도 페이지가 넘어가도록 보정
        float delta = pos - dragStartPos;
        const float swipeThreshold = 0.005f; // normalized 기준 약 2%
        if (Mathf.Abs(delta) > swipeThreshold)
        {
            if (delta > 0)
            {
                currentPage = startDragPage + 1;
            }
            else
            {
                currentPage = startDragPage - 1;
            }

            if (!loop)
            {
                currentPage = Mathf.Clamp(currentPage, 0, pagePositions.Length - 1);
            }
        }

        isLerping = true;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (pagePositions == null || pagePositions.Length == 0) return;
        if (isDragging || isLerping) return;

        float delta = eventData.scrollDelta.y;
        if (Mathf.Abs(delta) < Mathf.Epsilon) return;

        int direction = delta > 0 ? -1 : 1; // 휠 ↑ : 이전 카드, 휠 ↓ : 다음 카드
        int target = currentPage + direction;

        if (!loop)
        {
            target = Mathf.Clamp(target, 0, pagePositions.Length - 1);
        }

        JumpToPage(target, false); // 부드럽게 이동

        eventData.Use();
    }

    // 외부 체크용 헬퍼 --------------------------
    public bool HasValidPages()
    {
        return pagePositions != null && pagePositions.Length > 0;
    }

    public int CurrentPageCount
    {
        get { return pagePositions != null ? pagePositions.Length : 0; }
    }
} 