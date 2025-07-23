using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // EventSystem 네임스페이스 추가

public class PlayerPuck : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    // Inspector 창에서 연결할 UI 요소들
    [SerializeField] private Image circleImage;
    [SerializeField] private TextMeshProUGUI numberText;
    
    public GamePlayer Player { get; private set; } // 외부에서 읽을 수 있도록 public으로 변경
    private Transform _originalParent;
    private Canvas _rootCanvas;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = gameObject.AddComponent<CanvasGroup>(); // 드래그 시 투명도 조절을 위해
        _rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Setup(GamePlayer player, Color teamColor)
    {
        this.Player = player;
        
        // 1. 원의 색상을 팀 컬러로 설정
        circleImage.color = teamColor;

        // 2. 텍스트에 등번호(backNumber) 설정
        numberText.text = player.Rating.backNumber;
        _originalParent = transform.parent; // 초기 부모 저장
    }

    // 1. 클릭 이벤트 (I-Pointer-Click-Handler)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Player != null && UIManager.Instance != null)
        {
            UIManager.Instance.ShowPlayerStats(Player);
        }
    }

    // 2. 드래그 시작 (I-Begin-Drag-Handler)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Player.TeamId != UIManager.Instance.GetUserTeamId()) return; // 유저 팀 선수만 드래그 가능

        _canvasGroup.alpha = 0.6f; // 반투명하게 만듦
        _canvasGroup.blocksRaycasts = false; // 다른 UI 이벤트를 막지 않도록
        transform.SetParent(_rootCanvas.transform); // 캔버스 최상단으로 이동
        transform.SetAsLastSibling();
    }

    // 3. 드래그 중 (I-Drag-Handler)
    public void OnDrag(PointerEventData eventData)
    {
        if (Player.TeamId != UIManager.Instance.GetUserTeamId()) return;
        
        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
    }

    // 4. 드래그 종료 (I-End-Drag-Handler)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (Player.TeamId != UIManager.Instance.GetUserTeamId()) return;

        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        // 드롭되지 않았다면 원래 위치로 복귀
        if (transform.parent == _rootCanvas.transform) 
        {
            transform.SetParent(_originalParent);
            _rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    // 5. 드롭 (I-Drop-Handler) - 다른 퍽이 '이' 퍽 위에 드롭되었을 때
    public void OnDrop(PointerEventData eventData)
    {
        // [추가] 드롭 대상(자기 자신)이 우리 팀 선수가 아니면 교체 로_gic을 실행하지 않음
        if (this.Player.TeamId != UIManager.Instance.GetUserTeamId()) return;
        
        PlayerPuck draggedPuck = eventData.pointerDrag.GetComponent<PlayerPuck>();
        if (draggedPuck != null && draggedPuck != this)
        {
            // 드래그된 퍽(벤치)과 드롭된 퍽(코트)의 교체를 요청
            UIManager.Instance.RequestManualSubstitution(draggedPuck, this);
        }
    }
}