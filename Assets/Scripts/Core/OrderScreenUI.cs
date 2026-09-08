using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님에게 주문을 받는 화면. 씬은 하나지만 화면은 조리 화면과 별개다.
/// 배경이 불투명해 조리 화면을 완전히 가리고, 뒷판이 레이캐스트를 막아 조리 조작도 잠긴다.
///
/// 대사는 한 줄씩 넘긴다. 남은 줄이 있으면 버튼이 [다음], 마지막 줄이면 [조리 시작]이 된다.
/// [조리 시작]을 누르면 이 그룹만 꺼진다. 조리 화면은 그 아래에서 계속 살아 있으므로
/// 따로 시작 신호를 보낼 필요가 없다.
/// </summary>
public class OrderScreenUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private TextMeshProUGUI dayTimeText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button startButton;
    [SerializeField] private Image startButtonImage;
    [SerializeField] private TextMeshProUGUI startButtonLabel;

    /// <summary>아직 들을 말이 남았을 때의 버튼 색.</summary>
    [SerializeField] private Color nextColor = new Color(0.48f, 0.65f, 0.78f);

    /// <summary>마지막 줄까지 다 들었을 때의 버튼 색.</summary>
    [SerializeField] private Color startColor = new Color(0.91f, 0.54f, 0.42f);

    private string[] lines = new string[0];
    private int lineIndex;

    public bool IsOpen
    {
        get { return screenRoot != null && screenRoot.activeSelf; }
    }

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(Advance);

        // 이 스크립트는 screenRoot 바깥에 붙어 있어야 한다.
        // 안에 있으면 여기서 자기 자신을 꺼 버려 다시 켤 수 없다.
        Close();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(Advance);
    }

    /// <summary>새 손님이 왔을 때 연다. 대사는 첫 줄부터 시작한다.</summary>
    public void Open(int day, int hour, string dialogue, int totalRevenue)
    {
        if (screenRoot != null) screenRoot.SetActive(true);

        // 시각은 손님이 갈 때마다 한 시간씩 흐른다. 시간 제한은 없다(기획서 5.4).
        if (dayTimeText != null) dayTimeText.text = "영업 시간 " + day + "일차 / " + hour + " : 00";
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";

        lines = SplitLines(dialogue);
        lineIndex = 0;
        ShowLine();
    }

    public void Close()
    {
        if (screenRoot != null) screenRoot.SetActive(false);
    }

    /// <summary>버튼을 눌렀을 때. 남은 줄이 있으면 다음 줄, 없으면 조리로 넘어간다.</summary>
    private void Advance()
    {
        if (lineIndex < lines.Length - 1)
        {
            lineIndex++;
            ShowLine();
            return;
        }

        Close();
    }

    private void ShowLine()
    {
        if (dialogueText != null)
        {
            dialogueText.text = lines.Length > 0 ? lines[lineIndex] : "";
        }

        bool last = lineIndex >= lines.Length - 1;

        if (startButtonLabel != null) startButtonLabel.text = last ? "조리 시작  →" : "다음  →";
        if (startButtonImage != null) startButtonImage.color = last ? startColor : nextColor;
    }

    /// <summary>대사를 줄 단위로 자른다. 빈 줄은 버린다.</summary>
    private static string[] SplitLines(string dialogue)
    {
        if (string.IsNullOrEmpty(dialogue)) return new[] { "(받은 주문이 없습니다)" };

        string[] raw = dialogue.Split('\n');
        var kept = new System.Collections.Generic.List<string>(raw.Length);

        foreach (string line in raw)
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0) kept.Add(trimmed);
        }

        return kept.Count > 0 ? kept.ToArray() : new[] { "(받은 주문이 없습니다)" };
    }
}
