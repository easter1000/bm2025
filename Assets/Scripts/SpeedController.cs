using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem; // 새로운 Input System을 사용하기 위해 추가

public class SpeedController : MonoBehaviour
{
    [Header("Target Simulator")]
    [SerializeField] private GameSimulator gameSimulator;

    [Header("UI Elements")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button fastForwardButton;
    [SerializeField] private TextMeshProUGUI speedText;

    private readonly List<float> speedMultipliers = new List<float> { 0.25f, 0.5f, 1f, 2f, 4f, 8f };
    private int currentSpeedIndex = 2; // 기본 1x
    private bool isPaused = false;
    private const float BASE_SPEED = 12.0f;
    private Color defaultSpeedTextColor;

    void Start()
    {
        // OnGameStateUpdated 이벤트 구독
        gameSimulator = FindFirstObjectByType<GameSimulator>();
        if (gameSimulator != null)
        {
            gameSimulator.OnGameStateUpdated += UpdateUI;
        }

        if (speedText != null)
        {
            defaultSpeedTextColor = speedText.color;
        }

        pauseButton.onClick.AddListener(Pause);
        playButton.onClick.AddListener(Play);
        fastForwardButton.onClick.AddListener(ToggleSpeed);

        ApplySpeed();
        UpdateUI(gameSimulator.CurrentState); // 초기 UI 업데이트
    }

    void Update()
    {
        // [수정] 새로운 Input System 방식으로 스페이스바 입력 감지
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (isPaused)
            {
                Play();
            }
            else
            {
                Pause();
            }
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (gameSimulator != null)
        {
            gameSimulator.OnGameStateUpdated -= UpdateUI;
        }
    }

    public void Play()
    {
        isPaused = false;
        ApplySpeed();
    }
    
    public void Pause()
    {
        isPaused = true;
        ApplySpeed();
    }
    
    public void ToggleSpeed()
    {
        currentSpeedIndex = (currentSpeedIndex + 1) % speedMultipliers.Count;
        ApplySpeed();
    }

    private void ApplySpeed()
    {
        if (gameSimulator == null) return;

        if (isPaused)
        {
            gameSimulator.SimulationSpeed = 0f;
        }
        else
        {
            gameSimulator.SimulationSpeed = BASE_SPEED * speedMultipliers[currentSpeedIndex];
        }
        UpdateUI(gameSimulator.CurrentState); // 속도 변경 시 UI 즉시 업데이트
    }
    
    // GameState를 매개변수로 받도록 수정
    private void UpdateUI(GameState currentState)
    {
        if (this == null)
        {
            if (gameSimulator != null) gameSimulator.OnGameStateUpdated -= UpdateUI;
            return;
        }
        
        pauseButton.interactable = !isPaused;
        playButton.interactable = isPaused;

        if (speedText != null)
        {
            speedText.text = $"{speedMultipliers[currentSpeedIndex]}x";
            // 일시정지 시 텍스트 색상을 변경하여 상태를 표시
            speedText.color = isPaused ? Color.gray : defaultSpeedTextColor;
        }
    }
} 