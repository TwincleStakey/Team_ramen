using System.Collections;
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

    /// <summary>글자마다 나는 톤. 빌더가 꽂아 준다. 없으면 소리 없이 글자만 찍힌다.</summary>
    [SerializeField] private DialogueBlip blip;

    private string[] lines = new string[0];
    private int lineIndex;

    /// <summary>글자를 하나씩 찍는 중인 코루틴. 도중에 버튼을 누르면 끊고 한 번에 다 보여준다.</summary>
    private Coroutine typing;

    /// <summary>지금 찍는 중인가. 찍는 중에 버튼을 누르면 다음 마디가 아니라 이 마디를 마저 찍는다.</summary>
    private bool IsTyping { get { return typing != null; } }

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

        // 목소리는 손님마다 다르다. 대사를 만든 쪽에서 말투만 읽어 온다.
        if (blip != null) blip.SetPersona(CurrentPersonaId);

        ShowLine();
    }

    public void Close()
    {
        // 찍던 것을 안 멈추면 화면이 꺼진 뒤에도 코루틴이 남아 소리가 난다.
        FinishTyping();

        if (screenRoot != null) screenRoot.SetActive(false);
    }

    /// <summary>
    /// 버튼을 눌렀을 때. 남은 줄이 있으면 다음 줄, 없으면 조리로 넘어간다.
    /// 아직 글자를 찍는 중이면 먼저 이 마디를 한 번에 다 보여준다. 기다리기 답답하기 때문이다.
    /// </summary>
    private void Advance()
    {
        if (IsTyping)
        {
            FinishTyping();
            return;
        }

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

            // 글은 다 넣어 두고 보이는 글자 수만 늘린다. 한 글자마다 text 를 다시 넣으면
            // 그때마다 줄바꿈을 다시 계산해서, 글자가 늘 때마다 줄이 출렁인다.
            if (typing != null) StopCoroutine(typing);
            typing = gameObject.activeInHierarchy ? StartCoroutine(TypeLine()) : null;
            if (typing == null) dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        bool last = lineIndex >= lines.Length - 1;

        if (startButtonLabel != null) startButtonLabel.text = last ? "넵" : "▶";
        if (startButtonImage != null) startButtonImage.color = last ? startColor : nextColor;
    }

    /// <summary>
    /// 말풍선은 고정 크기이고 글은 그 안에서 가운데에 놓인다.
    /// </summary>
    /// <summary>
    /// 글자를 하나씩 찍는다. 찍힐 때마다 톤이 한 번 난다(기획서 10.2).
    ///
    /// 공백과 문장부호에서는 소리를 내지 않는다. 전부 소리를 내면 말이 아니라
    /// 기계음처럼 들린다. 마침표 뒤에서는 잠깐 쉬어 문장이 끊긴 것을 귀로 알게 한다.
    ///
    /// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰도 대사는 계속 나와야 한다.
    /// </summary>
    private IEnumerator TypeLine()
    {
        TMP_TextInfo info = dialogueText.textInfo;

        // 글자 수를 세려면 한 번 배치해 봐야 한다.
        dialogueText.ForceMeshUpdate();
        int count = info.characterCount;

        // 회색 마디는 통째로 띄워 두고 검정 마디부터 찍는다.
        int start = Mathf.Clamp(TypeStartIndex, 0, count);
        dialogueText.maxVisibleCharacters = start;

        float interval = blip != null ? blip.Interval : 0.04f;

        for (int i = start; i < count; i++)
        {
            dialogueText.maxVisibleCharacters = i + 1;

            char c = info.characterInfo[i].character;
            if (blip != null && !DialogueBlip.IsSilent(c)) blip.PlayTone();

            float wait = interval + DialogueBlip.PauseAfter(c);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        typing = null;
    }

    /// <summary>찍던 것을 끊고 이 마디를 한 번에 다 보여준다.</summary>
    private void FinishTyping()
    {
        if (typing != null)
        {
            StopCoroutine(typing);
            typing = null;
        }
        if (dialogueText != null) dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>지금 손님의 말투. 대사를 만든 쪽에서 읽기만 한다.</summary>
    private string CurrentPersonaId
    {
        get
        {
            var manager = FindFirstObjectByType<OrderManager>();
            if (manager == null || manager.CurrentScenario == null) return null;
            return manager.CurrentScenario.personaId;
        }
    }

    /// <summary>
    /// 최근 두 마디만 보여준다. 위는 지난 마디(회색), 아래는 방금 마디(검정).
    ///
    /// 다음을 누를 때마다 한 칸씩 밀린다. 세 번째를 들으면 첫 번째는 사라지고
    /// 두 번째가 회색으로 올라간다. 늘 두 마디만 있으므로 창을 넘쳐 잘릴 일이 없다.
    ///
    /// 마디째로 넣고 뺀다. 줄 단위로 자르면 잘리는 자리가 글자 한가운데라
    /// 맨 윗줄이 가로로 반 토막 난 채 남는다.
    /// </summary>
    private string ComposeVisible()
    {
        if (lines == null || lines.Length == 0) return "";

        int current = Mathf.Clamp(lineIndex, 0, lines.Length - 1);
        if (current == 0) return lines[0];

        return PastLineColorTag + lines[current - 1] + "</color>" + NewLine + lines[current];
    }

    /// <summary>
    /// 지난 마디에 입히는 색. 말풍선이 살구색이라 무채색 회색보다 갈색 계열이 자연스럽다.
    /// 읽을 수는 있되 방금 들은 말보다는 뒤로 물러나 보이는 밝기로 잡았다.
    /// </summary>
    private const string PastLineColorTag = "<color=#8A7A66>";

    private const string NewLine = "\n";

    /// <summary>
    /// 새로 찍기 시작할 글자 자리. 회색 마디는 이미 들은 말이라 바로 떠 있어야 하고,
    /// 검정 마디만 한 글자씩 찍힌다.
    ///
    /// 색 태그는 글자로 세지 않으므로, 회색 마디의 글자 수에 줄바꿈 하나를 더한 자리가
    /// 검정 마디의 첫 글자다.
    /// </summary>
    private int TypeStartIndex
    {
        get
        {
            if (lines == null || lineIndex <= 0 || lineIndex >= lines.Length) return 0;

            string past = lines[lineIndex - 1];
            return (past != null ? past.Length : 0) + 1;
        }
    }

    private void LayoutDialogue()
    {
        if (dialogueViewport == null) return;

        // 말풍선은 고정 크기다. 글상자를 창에 맞춰 두면 글이 그 안에서 가운데에 놓인다.
        // 예전에는 대사 길이를 재서 상자를 늘였다 줄였다 했는데, 말할 때마다 상자가 들썩여 보였다.
        dialogueText.rectTransform.sizeDelta = dialogueViewport.rect.size;
        dialogueText.rectTransform.anchoredPosition = Vector2.zero;
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
