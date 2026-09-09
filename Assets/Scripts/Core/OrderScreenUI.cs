using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님에게 주문을 받는 화면. 씬은 하나지만 화면은 조리 화면과 별개다.
/// 배경이 불투명해 조리 화면을 완전히 가리고, 뒷판이 레이캐스트를 막아 조리 조작도 잠긴다.
///
/// 대사는 클릭할 때마다 한 줄씩 쌓인다. 남은 줄이 있으면 버튼이 [다음], 마지막 줄이면 [제조하기]가 된다.
/// [제조하기]를 누르면 이 그룹만 꺼진다. 조리 화면은 그 아래에서 계속 살아 있으므로
/// 따로 시작 신호를 보낼 필요가 없다.
/// </summary>
public class OrderScreenUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private TextMeshProUGUI dayTimeText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    /// <summary>대사를 잘라 내는 창. 글이 이보다 길어지면 위로 밀어 올린다.</summary>
    [SerializeField] private RectTransform dialogueViewport;
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
            // 창에 들어가는 만큼만 넣는다. 넘치는 마디는 아예 안 그린다.
            dialogueText.text = ComposeVisible();
            LayoutDialogue();
        }

        bool last = lineIndex >= lines.Length - 1;

        if (startButtonLabel != null) startButtonLabel.text = last ? "제조하기  →" : "다음  →";
        if (startButtonImage != null) startButtonImage.color = last ? startColor : nextColor;
    }

    /// <summary>
    /// 쌓인 대사의 높이를 재서 글상자 크기와 자리를 다시 잡는다.
    ///
    /// 창보다 짧으면 창 한가운데에 둔다. 마디가 늘면 상자가 위아래로 함께 자라
    /// 앞 대사가 조금씩 위로 밀린다.
    /// 창보다 길어지면 넘친 만큼 위로 올려 아래변을 창 아래변에 맞춘다. 그래야 방금 들은
    /// 마디가 늘 보이고, 옛 마디가 창 위로 빠져나가 마스크에 잘린다.
    /// </summary>
    /// <summary>
    /// 지금 들은 마디 하나만 보여준다.
    ///
    /// 앞서 들은 말은 남기지 않는다. 남기면 창을 넘긴 줄이 위에서 반 토막 난 채 걸리는데,
    /// 흐린 색으로 남기는 방식도 써 봤지만 화면이 복잡해져서 지금은 안 쓴다.
    /// 되살리려면 마디를 이어 붙이고 지난 것에 색 태그를 씌우면 된다.
    /// </summary>
    private string ComposeVisible()
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)];
    }

    private void LayoutDialogue()
    {
        if (dialogueViewport == null) return;

        RectTransform rect = dialogueText.rectTransform;
        float window = dialogueViewport.rect.height;

        // 폭은 창에서 그대로 가져온다. 글상자가 1픽셀만 넓어도 마스크가 양끝 글자를 깎아
        // 첫 글자와 끝 글자가 잘려 보인다. 여기서 매번 맞추면 어긋날 일이 없다.
        float width = dialogueViewport.rect.width;

        // ContentSizeFitter를 쓰지 않고 직접 잰다. 그쪽은 레이아웃이 다시 계산될 때까지
        // 값이 안 바뀌어서, 방금 넣은 글의 높이를 바로 읽을 수 없다.
        float height = dialogueText.GetPreferredValues(dialogueText.text, width, 0f).y;

        // 들어가는 만큼만 넣으므로 넘칠 일이 없다. 창 한가운데에 둔다.
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
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
