// PlayerTradeLine.cs
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerTradeLine : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image teamLogoImage;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI salaryText;
    [SerializeField] private TextMeshProUGUI overallText;
    [SerializeField] private Image backgroundImage;

    private TextMeshProUGUI[] childTexts;
    private bool isSelected;
    private bool isLocked = false;
    private PlayerRating rating;

    public event System.Action<PlayerRating> OnLineClicked;
    public event System.Action OnSelectionChanged;
    public bool IsSelected => isSelected;

    public int GetPlayerID()
    {
        return rating != null ? rating.player_id : -1;
    }

    private void Awake()
    {
        // 자식 텍스트 컴포넌트 캐싱
        childTexts = GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
    }

    /// <summary>
    /// 라인에 선수 정보를 세팅한다.
    /// </summary>
    public void Setup(PlayerRating rating, PlayerStatus status, string teamAbbr)
    {
        if (rating == null) return;
        this.rating = rating;

        // 팀 로고
        if (teamLogoImage != null)
        {
            string logoName = (teamAbbr == "FA") ? "free" : teamAbbr.ToLower();
            Sprite sprite = Resources.Load<Sprite>($"team_photos/{logoName}");
            if (sprite != null) teamLogoImage.sprite = sprite;
        }

        // 이름 & OVR
        if (playerNameText != null) playerNameText.text = rating.name;
        if (overallText != null) overallText.text = rating.overallAttribute.ToString();

        // 연봉(연 단위)
        if (salaryText != null && status != null && status.YearsLeft > 0)
        {
            long annual = status.Salary / status.YearsLeft;
            salaryText.text = FormatMoney(annual);
        }

        // 초기화 시에는 항상 선택되지 않은 상태로 시작
        isSelected = false;
        UpdateColors();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnLineClicked?.Invoke(this.rating); // 상세 정보 표시 이벤트 호출
        if (isLocked) return; // 잠금 상태일 경우 동작하지 않음

        isSelected = !isSelected;
        UpdateColors();
        OnSelectionChanged?.Invoke(); // 상태 변경 이벤트 호출
    }

    /// <summary>
    /// 이 라인의 상호작용 가능 여부를 설정합니다.
    /// </summary>
    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    private void UpdateColors()
    {
        Color bg = isSelected ? Color.white : Color.black;
        Color txt = isSelected ? Color.black : Color.white;

        if (backgroundImage != null) backgroundImage.color = bg;

        if (childTexts == null || childTexts.Length == 0)
        {
            childTexts = GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        }
        foreach (var t in childTexts)
        {
            t.color = txt;
        }
    }

    private string FormatMoney(long amount)
    {
        if (amount < 0) return "-";

        string unit = "";
        double value = amount;

        if (amount >= 1_000_000_000)
        {
            unit = "B";
            value = amount / 1_000_000_000.0;
        }
        else if (amount >= 1_000_000)
        {
            unit = "M";
            value = amount / 1_000_000.0;
        }
        else if (amount >= 1_000)
        {
            unit = "K";
            value = amount / 1_000.0;
        }
        else
        {
            return $"$ {amount:N0}";
        }

        int intDigits = value >= 1 ? (int)Mathf.Floor(Mathf.Log10((float)value) + 1) : 1;
        int decimals = Mathf.Max(0, 4 - intDigits);
        string format = $"F{decimals}";
        string strVal = value.ToString(format).TrimEnd('0').TrimEnd('.');
        return $"$ {strVal}{unit}";
    }
} 