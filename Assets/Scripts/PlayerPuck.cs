using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerPuck : MonoBehaviour
{
    // Inspector 창에서 연결할 UI 요소들
    [SerializeField] private Image borderImage;
    [SerializeField] private Image playerPhotoImage;
    [SerializeField] private TextMeshProUGUI nameText;
    
    private GameSimulator.GamePlayer _player;
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
    public void Setup(GameSimulator.GamePlayer player)
    {
        _player = player;
        
        // 1. 선수 이름 설정
        nameText.text = player.Rating.name;

        // 2. 테두리 색상 설정 (UIManager에게 물어봄)
        Color teamColor = UIManager.Instance.GetTeamColor(player.Rating.team);
        borderImage.color = teamColor;

        // 3. 선수 사진 로드
        LoadPlayerPhoto(player.Rating.player_id);
    }

    // 선수 사진을 Resources 폴더에서 불러오는 함수
    private void LoadPlayerPhoto(int playerId)
    {
        // 파일 경로 생성 (예: "player_photos/1234")
        string path = $"player_photos/{playerId}";
        
        // 경로에서 스프라이트 로드 시도
        Sprite playerSprite = Resources.Load<Sprite>(path);

        // 만약 로드에 실패했다면 (파일이 없다면)
        if (playerSprite == null)
        {
            // 기본 이미지 로드
            playerSprite = Resources.Load<Sprite>("player_photos/default_image");
        }

        // 최종적으로 로드된 이미지를 UI에 적용
        if (playerPhotoImage != null)
        {
            playerPhotoImage.sprite = playerSprite;
        }
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