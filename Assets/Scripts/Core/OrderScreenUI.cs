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

    /// <summary>손님 겉모습. 새 손님을 맞을 때만 다시 뽑는다.</summary>
    [SerializeField] private CustomerAppearance customerAppearance;

    /// <summary>
    /// 주문 화면이 떠 있는 동안 내려 둘 조리 화면 물건.
    ///
    /// 그릇은 튜토리얼 어두운 판 위로 올리려고 Canvas 가 따로 얹혀 있다(RamenLayoutBuilder 의
    /// LiftCanvas). 그래서 계층 순서를 무시하고 주문 화면 위로 떠올라 손님 얼굴을 덮는다.
    /// 손님을 보는 동안에는 조리대가 필요 없으니 통째로 내린다.
    /// </summary>
    [SerializeField] private GameObject[] hiddenWhileOpen;

    /// <summary>
    /// 손님 앞에 놓이는 라멘 그릇. 라멘을 낸 뒤에만 보인다 —
    /// 주문받는 동안 놓여 있으면 이미 준 것처럼 보인다.
    /// </summary>
    [SerializeField] private GameObject servedBowl;

    /// <summary>
    /// 밤 배경(뒷판). 밀려 올라갈 때 같이 걷힌다.
    ///
    /// 이 판은 화면(640x360)이 아니라 1920x1080 이다. 16:9 가 아닌 창에서 판 바깥이
    /// 비어 보이지 않게 크게 잡아 둔 것이다. 그래서 화면 높이만큼 밀어 올려도 이 판은
    /// 여전히 화면을 덮고 있다. 미끄러뜨리는 것만으로는 조리대가 안 드러나서 같이 지운다.
    /// </summary>
    [SerializeField] private Image backdrop;

    /// <summary>
    /// 시선이 내려가는 데 걸리는 시간.
    /// 0.28 은 툭 떨어지는 느낌이었다. 스르르 흐르려면 이만큼 걸려야 한다.
    /// </summary>
    [SerializeField] private float slideSeconds = 0.6f;

    /// <summary>조리대로 내려갈 때 걸리는 시간(초). 올라올 때보다 짧아야 휙 빠지는 맛이 난다.</summary>
    [SerializeField] private float closeSeconds = 0.34f;

    /// <summary>[제조하기] 를 누르고 화면이 움직이기까지 두는 짬(초).</summary>
    [SerializeField] private float closeDelay = 0.12f;

    /// <summary>먹는 동안 말풍선에 띄우는 말. 표정 그림이 들어오면 이 자리에 연출이 붙는다.</summary>
    private const string EatingLine = "…";

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

    /// <summary>미끄러뜨릴 상자. 화면 전체가 이 안에 들어 있다.</summary>
    private RectTransform SlideRect
    {
        get { return screenRoot != null ? screenRoot.transform as RectTransform : null; }
    }

    /// <summary>다 나왔을 때 서는 자리.</summary>
    private Vector2 slideHome;

    /// <summary>한 번에 얼마나 밀어 올릴지. 화면 높이만큼이다.</summary>
    private float slideDistance = 360f;

    private Coroutine sliding;

    /// <summary>
    /// 글이 접히기 시작하는 폭. 빌더가 잡아 준 처음 창 폭이 곧 최대치다.
    /// 이보다 넓어지면 말풍선이 손님 그림을 파고든다.
    /// </summary>
    private float maxTextWidth;

    /// <summary>말풍선 테두리와 글 사이 여백. 빌더가 창을 얼마나 안쪽으로 밀어 놨는지에서 읽는다.</summary>
    private Vector2 bubblePadding;

    private void Awake()
    {
        CaptureSlide();

        // 글에 맞춰 상자를 늘이려면 처음 크기를 먼저 기억해 둬야 한다.
        // 한 번 늘이고 나면 원래 값을 알 길이 없다.
        if (dialogueViewport != null)
        {
            maxTextWidth = dialogueViewport.sizeDelta.x;
            bubblePadding = new Vector2(dialogueViewport.anchoredPosition.x,
                                        -dialogueViewport.anchoredPosition.y);
        }

        if (startButton != null) startButton.onClick.AddListener(Advance);

        // 이 스크립트는 screenRoot 바깥에 붙어 있어야 한다.
        // 안에 있으면 여기서 자기 자신을 꺼 버려 다시 켤 수 없다.
        Close();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(Advance);
    }

    /// <summary>
    /// 서는 자리와 밀어 올릴 거리를 잡아 둔다.
    ///
    /// 거리는 화면 높이 그대로다. 한 화면만큼 밀면 보이던 것이 전부 위로 빠진다.
    /// 고개를 숙이면 눈앞의 것이 위로 올라가 사라지는 것과 같다.
    /// </summary>
    private void CaptureSlide()
    {
        RectTransform rect = SlideRect;
        if (rect == null) return;

        slideHome = rect.anchoredPosition;

        var area = rect.parent as RectTransform;
        if (area != null && area.rect.height > 0f) slideDistance = area.rect.height;
    }

    /// <summary>밀려 올라가 있는 자리. 화면 위로 한 화면만큼.</summary>
    private Vector2 SlideAway
    {
        get { return slideHome + new Vector2(0f, slideDistance); }
    }

    /// <summary>
    /// 시선을 올리거나(주문 화면이 내려옴) 내린다(조리대가 드러남).
    ///
    /// 자리는 정수 칸으로 끊는다. 픽셀아트라 반 칸에 놓이면 화면 전체가 한꺼번에 흐려진다.
    /// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰 있어도 전환은 흘러야 한다.
    /// </summary>
    private IEnumerator SlideRoutine(bool opening)
    {
        RectTransform rect = SlideRect;
        Vector2 from = opening ? SlideAway : slideHome;
        Vector2 to = opening ? slideHome : SlideAway;

        // 조리대로 내려갈 때는 한 박자 멈췄다 간다. [제조하기] 를 누른 손이 화면보다
        // 반 박자 빨라서, 곧바로 움직이면 누르자마자 끌려간 것처럼 읽힌다.
        if (!opening && closeDelay > 0f) yield return new WaitForSecondsRealtime(closeDelay);

        float duration = opening ? slideSeconds : closeSeconds;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;

            if (opening)
            {
                // 손님에게 시선이 올라올 때. 시작과 끝을 모두 부드럽게 —
                // 등속으로 움직이면 고개를 드는 게 아니라 판이 기계처럼 밀린다.
                //
                // smoothstep(3t^2-2t^3) 보다 한 단계 완만한 곡선이라 툭 멈추지 않고 스르르 선다.
                t = t * t * t * (t * (t * 6f - 15f) + 10f);
            }
            else
            {
                // 조리대로 내려갈 때. 천천히 떼었다가 끝에서 확 빠진다.
                // 양끝을 다 부드럽게 하면 "스르르" 가 되는데, 여기서는 시선을 휙 내리는
                // 동작이라 가속이 붙어야 "슈슉" 으로 읽힌다.
                t = t * t * t;
            }

            SetSlide(Vector2.Lerp(from, to, t), opening ? t : 1f - t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetSlide(to, opening ? 1f : 0f);
        sliding = null;

        if (!opening && screenRoot != null) screenRoot.SetActive(false);
    }

    /// <summary>상자를 옮기고 뒷판 진하기를 맞춘다.</summary>
    private void SetSlide(Vector2 position, float backdropAlpha)
    {
        RectTransform rect = SlideRect;
        if (rect != null)
            rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));

        if (backdrop != null)
        {
            Color c = backdrop.color;
            c.a = backdropAlpha;
            backdrop.color = c;
        }
    }

    /// <summary>
    /// 미끄러지는 중이면 끊고 열린 자리에 바로 세운다.
    ///
    /// 가게 문을 여는 장면에서 쓴다. 그때는 화면이 검게 덮여 있어 미끄러짐이 보이지도 않는데,
    /// 걷히는 동안 판이 흘러 들어오면 "이미 가게에 와 있다" 가 아니라 "무언가 지나갔다" 로 읽힌다.
    /// </summary>
    public void SnapOpen()
    {
        SnapSlide(true);
    }

    /// <summary>미끄러짐 없이 그 자리로. 켜지기 전이나 씬을 막 띄웠을 때 쓴다.</summary>
    private void SnapSlide(bool opened)
    {
        if (sliding != null) { StopCoroutine(sliding); sliding = null; }
        SetSlide(opened ? slideHome : SlideAway, opened ? 1f : 0f);
    }

    /// <summary>
    /// 미끄러짐이 끝날 때까지 기다린다.
    ///
    /// 화면이 다 올라온 뒤에 먹는 연출이 시작되어야 한다. 안 기다리면 판이 흐르는 도중에
    /// 검은 띠가 들어오고 얼굴이 커져, 두 움직임이 겹쳐 무슨 일이 일어나는지 안 보인다.
    /// </summary>
    public IEnumerator WaitForSlide()
    {
        while (sliding != null) yield return null;
    }

    /// <summary>새 손님이 왔을 때 연다. 대사는 첫 줄부터 시작한다.</summary>
    public void Open(int day, int hour, string dialogue, int totalRevenue)
    {
        // 이미 떠 있는 채로 다시 열리는 경우가 있다. 손님을 갈아 끼울 때(SwapCustomer)가 그렇다.
        // 그때 또 미끄러뜨리면 화면이 한 번 솟았다 내려와, 손님만 조용히 바뀌는 연출이 깨진다.
        bool wasOpen = screenRoot != null && screenRoot.activeSelf;

        if (screenRoot != null) screenRoot.SetActive(true);
        ShowCookingProps(false);

        // 아직 안 만들었다. 그릇은 내고 나서야 놓인다.
        if (servedBowl != null) servedBowl.SetActive(false);

        // 새 손님이다. 얼굴을 다시 뽑는 곳은 여기 하나뿐이다.
        // 스러짐도 여기서 되돌린다. 앞 손님이 사라진 채로 끝났어도 새 손님은 보여야 한다.
        // 등장 연출을 붙일 때는 GameManager 가 이 직후(같은 프레임)에 다시 지운다.
        EnsureAppearance();
        if (customerAppearance != null)
        {
            // 말투마다 그림이 하나씩 있다. 그림이 없는 말투는 SetPersona 안에서
            // 예전처럼 얼굴·몸통을 무작위로 짝지어 세운다.
            customerAppearance.SetPersona(CurrentPersonaId);
            customerAppearance.SetFade(0f);
        }

        // 시선이 손님에게로 올라온다. 이미 떠 있었으면 그대로 둔다.
        if (wasOpen) SnapSlide(true);
        else StartSlide(true);

        // 손님이 없는 동안 감춰 둔 것을 되돌린다.
        // 버튼이 말풍선 안에 있으므로 말풍선을 먼저 켜야 버튼도 살아난다.
        ShowBubble(true);
        if (startButton != null) startButton.gameObject.SetActive(true);

        // 시각은 손님이 갈 때마다 한 시간씩 흐른다. 시간 제한은 없다(기획서 5.4).
        if (dayTimeText != null) dayTimeText.text = "영업 시간 " + day + "일차 / " + hour + " : 00";
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";

        lines = SplitLines(dialogue);
        lineIndex = 0;

        // 목소리는 손님마다 다르다. 대사를 만든 쪽에서 말투만 읽어 온다.
        if (blip != null) blip.SetPersona(CurrentPersonaId);

        ShowLine();
    }

    /// <summary>
    /// 라멘을 낸 뒤 손님이 먹는 동안 보여 주는 화면.
    ///
    /// 주문 화면과 같은 자리를 쓰되 둘이 다르다. 얼굴을 다시 뽑지 않고(주문한 그 사람이
    /// 그대로 먹어야 한다), 주문 대사를 처음부터 되감지 않는다.
    /// 버튼은 감춘다 — 이 장면은 GameManager 가 시간을 재서 넘긴다.
    /// </summary>
    public void OpenEating(int day, int hour, int totalRevenue)
    {
        // 조리하다가 완성하기를 누르고 올라오는 길이다. 주문을 받을 때와 같은 움직임으로
        // 시선이 다시 손님에게 올라간다.
        //
        // 이미 떠 있는 채로 다시 불릴 수 있다. 그때 또 미끄러뜨리면 화면이 한 번 솟았다 내려온다.
        bool wasOpen = screenRoot != null && screenRoot.activeSelf;

        if (screenRoot != null) screenRoot.SetActive(true);
        ShowCookingProps(false);

        // 라멘을 냈다. 이제 손님 앞에 그릇이 놓인다.
        if (servedBowl != null) servedBowl.SetActive(true);

        if (wasOpen) SnapSlide(true);
        else StartSlide(true);

        if (dayTimeText != null) dayTimeText.text = "영업 시간 " + day + "일차 / " + hour + " : 00";
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";

        SetBubbleLine(EatingLine);

        if (startButton != null) startButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 말풍선에 한 줄만 통째로 띄운다. 타자기로 찍지 않는다 — 연출 박자에 맞춰
    /// 컷마다 갈아 끼우는 용도라, 한 글자씩 찍으면 박자가 밀린다.
    /// </summary>
    public void SetBubbleLine(string line)
    {
        // 안 멈추면 주문 대사가 이어서 찍힌다.
        FinishTyping();

        lines = new string[0];
        lineIndex = 0;

        if (dialogueText == null) return;

        dialogueText.text = line;
        LayoutDialogue();
    }

    /// <summary>
    /// 말풍선에 한 줄을 한 글자씩 찍기 시작한다. 찍는 동안 돌아온다 — 끝났는지는
    /// <see cref="IsBubbleTyping"/> 으로 본다.
    ///
    /// SetBubbleLine 은 통째로 띄운다. 컷마다 갈아 끼우는 데는 그쪽이 맞고,
    /// 이것은 마지막 컷처럼 "지금 말하고 있다"가 보여야 할 때 쓴다.
    /// </summary>
    public void StartTypingBubble(string line)
    {
        SetBubbleLine(line);
        if (dialogueText == null) return;

        typing = gameObject.activeInHierarchy ? StartCoroutine(TypeLine()) : null;
        if (typing == null) dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>아직 찍는 중인가.</summary>
    public bool IsBubbleTyping
    {
        get { return typing != null; }
    }

    /// <summary>찍다 말고 한 번에 다 보여 준다. 다 읽은 사람이 누르면 기다릴 이유가 없다.</summary>
    public void FinishBubbleLine()
    {
        FinishTyping();
    }

    /// <summary>인스펙터가 비어 있으면(빌더를 안 돌린 씬) 화면 안에서 한 번 찾아 둔다.</summary>
    /// <summary>
    /// 말풍선을 통째로 여닫는다. 손님이 나간 자리에 말풍선만 떠 있으면 이상하다.
    ///
    /// 말풍선은 빌더가 따로 꽂아 주지 않는다. 글자 창(Viewport)의 부모가 곧 말풍선이라
    /// 거기서 거슬러 올라간다.
    /// </summary>
    public void ShowBubble(bool visible)
    {
        if (dialogueViewport == null || dialogueViewport.parent == null) return;
        dialogueViewport.parent.gameObject.SetActive(visible);
    }

    /// <summary>말풍선 상자. 클로즈업할 때 EatingCutscene 이 같이 키운다.</summary>
    public RectTransform Bubble
    {
        get { return dialogueViewport != null ? dialogueViewport.parent as RectTransform : null; }
    }

    public CustomerAppearance Appearance
    {
        get
        {
            EnsureAppearance();
            return customerAppearance;
        }
    }

    private void EnsureAppearance()
    {
        if (customerAppearance == null && screenRoot != null)
            customerAppearance = screenRoot.GetComponentInChildren<CustomerAppearance>(true);
    }

    public void Close()
    {
        // 찍던 것을 안 멈추면 화면이 꺼진 뒤에도 코루틴이 남아 소리가 난다.
        FinishTyping();
        ShowCookingProps(true);

        // 꺼져 있거나 아직 살아나기 전이면 미끄러뜨릴 것도 없다.
        // Awake 에서도 이 함수를 부르는데, 거기서 코루틴을 돌리면 시작하지 못하고 끊긴다.
        if (screenRoot == null || !screenRoot.activeSelf || !gameObject.activeInHierarchy)
        {
            SnapSlide(false);
            if (screenRoot != null) screenRoot.SetActive(false);
            return;
        }

        // 시선이 조리대로 내려간다. 다 내려가면 그때 끈다.
        StartSlide(false);
    }

    private void StartSlide(bool opening)
    {
        if (sliding != null) StopCoroutine(sliding);

        if (!gameObject.activeInHierarchy)
        {
            SnapSlide(opening);
            return;
        }

        SetSlide(opening ? SlideAway : slideHome, opening ? 0f : 1f);
        sliding = StartCoroutine(SlideRoutine(opening));
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

    /// <summary>
    /// 지금 줄을 처음부터 다시 친다.
    ///
    /// 가게 문을 여는 장면에서 쓴다. 그때는 말풍선을 감춘 채로 화면을 차려 두는데,
    /// 감춰 둔 동안에도 타자기는 돌아서 손님이 드러날 무렵에는 이미 다 찍혀 있다.
    /// 드러난 뒤에 다시 쳐야 첫 마디가 찍히는 것이 보인다.
    /// </summary>
    public void ReplayCurrentLine()
    {
        ShowLine();
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

    /// <summary>
    /// 말풍선을 글에 맞춰 늘린다. 사방 여백만 두고 나머지 크기는 글이 정한다.
    ///
    /// 가로는 maxTextWidth 까지만 늘리고 그 뒤로는 줄을 접는다. 더 넓히면 손님을 파고든다.
    /// 세로는 접힌 줄 수만큼 늘어난다.
    ///
    /// 말풍선 피벗이 왼쪽 위라 늘어나는 방향이 오른쪽·아래다. 왼쪽 위 모서리는 늘 제자리에
    /// 있어서, 커질 때 상자가 통째로 움직이지 않는다. 꼬리와 넵 버튼은 말풍선 가장자리에
    /// 앵커가 걸려 있어 따라온다.
    ///
    /// 크기는 올림해서 정수로 맞춘다. 반칸에 걸치면 9-슬라이스 테두리가 흐려진다.
    /// </summary>
    private void LayoutDialogue()
    {
        if (dialogueViewport == null || dialogueText == null) return;

        Vector2 wanted = dialogueText.GetPreferredValues(dialogueText.text, maxTextWidth, 0f);
        float w = Mathf.Ceil(Mathf.Min(wanted.x, maxTextWidth));
        float h = Mathf.Ceil(wanted.y);

        dialogueViewport.sizeDelta = new Vector2(w, h);
        dialogueText.rectTransform.sizeDelta = new Vector2(w, h);
        dialogueText.rectTransform.anchoredPosition = Vector2.zero;

        var bubble = dialogueViewport.parent as RectTransform;
        if (bubble != null)
        {
            bubble.sizeDelta = new Vector2(w + bubblePadding.x * 2f,
                                           h + bubblePadding.y * 2f);
            PlaceBubble(bubble);
        }
    }

    /// <summary>
    /// 말풍선을 손님 머리 높이에 맞춰 왼쪽 위에 둔다.
    ///
    /// 손님마다 키가 다르다 — 어린이는 낮고 사극 손님은 갓 때문에 높다. 자리를 숫자로 박아 두면
    /// 누구에겐 머리에 겹치고 누구에겐 멀리 뜬다. 그래서 지금 손님 머리 꼭대기에서 잰다.
    ///
    /// 피벗이 오른쪽 위라, 여기 넣는 자리가 곧 말풍선의 오른쪽 위 모서리다.
    /// 글이 길어지면 아래왼쪽으로만 자라므로 머리 높이는 그대로 지킨다.
    /// </summary>
    private void PlaceBubble(RectTransform bubble)
    {
        EnsureAppearance();
        if (customerAppearance == null) return;

        var slot = customerAppearance.transform as RectTransform;
        if (slot == null) return;

        // 자리 아래변(카운터 선)에서 잰 귀 높이를 화면 높이로 옮긴다.
        float ear = slot.anchoredPosition.y - slot.rect.height * 0.5f
                    + customerAppearance.EarInSlot;

        // 피벗이 오른쪽 위라, 상자 한가운데가 귀에 오도록 절반만큼 올려 둔다.
        bubble.anchoredPosition = new Vector2(BubbleRightX, ear + bubble.rect.height * 0.5f);
    }

    /// <summary>조리 화면 물건을 껐다 켠다. 손님을 보는 동안에는 조리대가 필요 없다.</summary>
    private void ShowCookingProps(bool show)
    {
        if (hiddenWhileOpen == null) return;

        for (int i = 0; i < hiddenWhileOpen.Length; i++)
        {
            if (hiddenWhileOpen[i] != null) hiddenWhileOpen[i].SetActive(show);
        }
    }

    /// <summary>
    /// 말풍선 오른쪽 끝이 놓이는 자리.
    ///
    /// 얼굴(가운데 150 남짓)에 딱 붙이면 답답하고, 자리(300) 바깥까지 빼면 꼬리가
    /// 허공을 가리킨다. 그 사이에 둔다.
    /// </summary>
    private const float BubbleRightX = -130f;

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
