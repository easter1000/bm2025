using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private void Awake()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }

    public void Show(string message, Action onYes)
    {
        Show(message, onYes, null);
    }

    public void Show(string message, Action onYes, Action onNo)
    {
        if (dialogRoot != null) dialogRoot.SetActive(true);
        if (messageText != null) messageText.text = message;

        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();

        yesButton.onClick.AddListener(() => {
            Hide();
            onYes?.Invoke();
        });

        if (onNo != null)
        {
            noButton.gameObject.SetActive(true);
            noButton.onClick.AddListener(() => {
                Hide();
                onNo.Invoke();
            });
        }
        else
        {
            noButton.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }
} 