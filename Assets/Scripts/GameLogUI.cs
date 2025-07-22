using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GameLogUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logEntryPrefab;
    [SerializeField] private Transform logContainer;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int maxLogEntries = 50; // 로그 최대 표시 갯수

    private readonly Queue<GameObject> logEntries = new Queue<GameObject>();

    // 외부(GameSimulator)에서 호출할 로그 추가 함수
    public void AddLogEntry(string message)
    {
        if (logEntryPrefab == null || logContainer == null) return;

        // 새 로그를 추가하기 전에, 스크롤이 맨 아래에 있는지 확인
        bool isScrolledToBottom = (scrollRect == null) || (scrollRect.verticalNormalizedPosition <= 0.01f);

        // 로그가 최대치를 넘으면 가장 오래된 로그를 제거
        if (logEntries.Count >= maxLogEntries)
        {
            Destroy(logEntries.Dequeue());
        }

        // 새 로그 항목을 생성하고 텍스트 설정
        TextMeshProUGUI newEntry = Instantiate(logEntryPrefab, logContainer);
        newEntry.text = message;
        logEntries.Enqueue(newEntry.gameObject);

        // 스크롤이 맨 아래에 있었을 경우에만 자동으로 스크롤
        if (isScrolledToBottom)
        {
            // 한 프레임 뒤에 실행하여 UI 레이아웃이 업데이트될 시간을 줌
            StartCoroutine(ForceScrollDown());
        }
    }

    private System.Collections.IEnumerator ForceScrollDown()
    {
        // UI가 업데이트될 때까지 한 프레임 대기
        yield return new WaitForEndOfFrame();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
} 