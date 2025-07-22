using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerPuck : MonoBehaviour
{
    // Inspector 창에서 연결할 UI 요소들
    [SerializeField] private Image circleImage;
    [SerializeField] private TextMeshProUGUI numberText;
    
    private GamePlayer _player;
    private Button _button;

    void Awake()
    {
        // 버튼 컴포넌트를 가져와서, 클릭 시 OnPuckClicked 함수가 호출되도록 연결
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnPuckClicked);
        }
    }

    // UIManager가 이 함수를 호출하여 선수 정보를 설정
    public void Setup(GamePlayer player)
    {
        _player = player;
        
        // 1. 원의 색상을 팀 컬러로 설정
        circleImage.color = UIManager.Instance.GetTeamColor(player.Rating.team);

        // 2. 텍스트에 등번호(backNumber) 설정
        numberText.text = player.Rating.backNumber;
    }

    // 이 선수 원이 클릭되었을 때 호출되는 함수
    public void OnPuckClicked()
    {
        if (_player != null && UIManager.Instance != null)
        {
            // UIManager에게 내 스탯을 화면에 보여달라고 요청
            UIManager.Instance.ShowPlayerStats(_player);
        }
    }
}