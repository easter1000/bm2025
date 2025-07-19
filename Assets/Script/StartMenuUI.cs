using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartMenuUI : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        // 버튼 클릭 이벤트 등록
        newGameButton?.onClick.AddListener(OnNewGame);
        loadGameButton?.onClick.AddListener(OnLoadGame);
        settingsButton?.onClick.AddListener(OnSettings);
        quitButton?.onClick.AddListener(OnQuit);
    }

    private void OnNewGame()
    {
        // 새 게임을 시작할 때 호출됩니다.
        // "Game" 씬 이름을 실제 인게임 씬 이름으로 변경하세요.
        //SceneManager.LoadScene("Game");
        Debug.Log("새 게임 버튼이 눌렸습니다. 구현 필요!");
    }

    private void OnLoadGame()
    {
        // 저장된 데이터를 불러오는 로직을 여기에 구현하세요.
        Debug.Log("불러오기 버튼이 눌렸습니다. 구현 필요!");
    }

    private void OnSettings()
    {
        // 설정 메뉴를 열거나 설정 씬으로 전환하는 로직을 여기에 구현하세요.
        Debug.Log("설정 버튼이 눌렸습니다. 구현 필요!");
    }

    private void OnQuit()
    {
        // 게임 종료
    #if UNITY_EDITOR
        // 에디터에서 플레이 중지
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        // 빌드된 애플리케이션 종료
        Application.Quit();
    #endif
    }
} 