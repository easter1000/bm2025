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
        if (gameSimulator == null)
        {
            gameSimulator = FindObjectOfType<GameSimulator>();
            if (gameSimulator == null)
            {
                Debug.LogError("SpeedController cannot find GameSimulator in the scene!");
                this.enabled = false;
                return;
            }
        }

        if (speedText != null)
        {
            defaultSpeedTextColor = speedText.color;
        }

        pauseButton.onClick.AddListener(Pause);
        playButton.onClick.AddListener(Play);
        fastForwardButton.onClick.AddListener(ToggleSpeed);

        ApplySpeed();
        UpdateUI();
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

    private void Play()
    {
        isPaused = false;
        ApplySpeed();
        UpdateUI();
    }

    private void Pause()
    {
        isPaused = true;
        ApplySpeed();
        UpdateUI();
    }

    private void ToggleSpeed()
    {
        if (isPaused)
        {
            isPaused = false;
        }

        currentSpeedIndex = (currentSpeedIndex + 1) % speedMultipliers.Count;
        
        ApplySpeed();
        UpdateUI();
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
    }

    private void UpdateUI()
    {
        // GameObject를 비활성화하는 대신 버튼의 상호작용 가능 여부를 제어하여 레이아웃이 움직이지 않도록 함
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