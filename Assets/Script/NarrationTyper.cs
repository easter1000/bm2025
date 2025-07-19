using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class NarrationTyper : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI targetText;  // 출력할 UI Text
    [SerializeField] private float charInterval = 0.05f; // 글자 간격

    private Coroutine typingCoroutine;

    public void Play(string fullText, UnityAction onFinished = null)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeRoutine(fullText, onFinished));
    }

    private IEnumerator TypeRoutine(string fullText, UnityAction onFinished)
    {
        targetText.text = string.Empty;
        foreach (char c in fullText)
        {
            targetText.text += c;
            yield return new WaitForSeconds(charInterval);
        }
        onFinished?.Invoke();
    }
} 