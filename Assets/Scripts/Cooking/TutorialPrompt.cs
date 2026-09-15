using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그릇 바로 위에 뜨는 튜토리얼 안내. 두 줄까지 들어간다.
///
/// 무지개 테두리가 "어느 통" 인지를 가리키고, 이 문구가 "무엇을 어떻게" 인지를 말한다.
/// 둘 중 하나만 있으면 멘마처럼 두 번 넣는 재료에서 넣은 것인지 아닌지 헷갈린다.
///
/// 문구는 TutorialManager 가 들고 있다. 단계를 아는 쪽이 거기라서, 문장을 여기로 나누면
/// 단계를 고칠 때 두 곳을 같이 고쳐야 하고 한쪽만 고치면 안내가 어긋난다.
/// 튜토리얼이 끝나면 통째로 사라진다.
/// </summary>
public class TutorialPrompt : MonoBehaviour
{
    /// <summary>문구를 적을 곳. 빌더가 꽂아 준다.</summary>
    [SerializeField] private TextMeshProUGUI label;

    /// <summary>글자를 받치는 판. 문구가 없을 때는 판까지 같이 감춘다.</summary>
    [SerializeField] private Image panel;

    /// <summary>
    /// 글자와 판 가장자리 사이 여백. 9-슬라이스 테두리가 사방 8이라 그보다는 넓어야
    /// 글자가 테두리에 붙지 않는다.
    /// </summary>
    [SerializeField] private Vector2 padding = new Vector2(12f, 8f);

    /// <summary>글이 이보다 넓어지면 줄을 접는다. 화면 밖으로 나가지 않게 잡은 값이다.</summary>
    [SerializeField] private float maxTextWidth = 400f;

    /// <summary>
    /// 조리 화면에 들어선 뒤 안내가 뜨기까지 기다리는 시간(초).
    ///
    /// 판이 내려오자마자 안내가 튀어나오면 조리대를 볼 틈이 없어서 한 박자 둔다.
    /// 3 이었는데 시작이 굼떠 보여서 1 로 줄였다. 한 박자면 충분하고, 그 뒤로는
    /// 글이 한 글자씩 찍히는 동안 어차피 조리대를 볼 시간이 더 있다.
    /// </summary>
    [SerializeField] private float appearDelay = 1f;

    /// <summary>글자가 찍히는 속도(초당 글자 수).</summary>
    [SerializeField] private float charsPerSecond = 18f;

    /// <summary>마지막으로 자리를 잡아 준 문구. 같은 문구에 매 프레임 다시 재지 않는다.</summary>
    private string laidOut;

    /// <summary>손님 대사 화면. 이게 떠 있는 동안에는 안내를 감춘다.</summary>
    private OrderScreenUI orderScreen;

    /// <summary>이 시각이 지나야 안내를 띄운다. 조리 화면에 막 들어선 순간을 피하려는 것이다.</summary>
    private float showAt;

    private bool wasOrderScreenOpen = true;

    /// <summary>지금 몇 글자까지 찍었나. 문구가 바뀌면 0 부터 다시 센다.</summary>
    private float typed;

    /// <summary>
    /// 안내가 다 찍혔는가. 어두운 판과 무지개 테두리가 이걸 보고 켜진다.
    ///
    /// 글이 찍히는 중에 화면이 어두워지고 통이 빛나면, 읽으라는 글과 보라는 통이 한꺼번에
    /// 들어와 어느 쪽도 안 읽힌다. 글을 다 읽힌 다음에 가리킨다.
    /// </summary>
    public static bool Revealed { get; private set; }

    /// <summary>
    /// 안내가 한 번이라도 다 찍힌 적이 있는가. 어두운 판(TutorialDim)이 이것을 본다.
    ///
    /// <see cref="Revealed"/> 는 문구가 바뀔 때마다 잠깐 거짓이 된다. 판이 그걸 그대로
    /// 따라가면 안내가 넘어갈 때마다 화면이 밝아졌다 다시 어두워져서, 게임이 한 번
    /// 리셋된 것처럼 깜빡인다. 판은 한 번 덮으면 조리가 끝날 때까지 덮고 있어야 한다.
    ///
    /// 주문 화면으로 올라가거나 튜토리얼이 끝나면 그때 내린다.
    /// </summary>
    public static bool RevealedOnce { get; private set; }

    private void Update()
    {
        if (orderScreen == null) orderScreen = FindFirstObjectByType<OrderScreenUI>();

        // 주문 화면이 닫히는 순간(= 조리 시작)부터 한 박자 센다.
        // 화면이 바뀌자마자 안내가 튀어나오면 조리 화면을 볼 틈이 없다.
        bool orderOpen = orderScreen != null && orderScreen.IsOpen;
        if (wasOrderScreenOpen && !orderOpen) showAt = Time.unscaledTime + appearDelay;
        wasOrderScreenOpen = orderOpen;

        TutorialManager tutorial = TutorialManager.Instance;
        string text = tutorial != null ? tutorial.PromptText : null;

        bool show = !string.IsNullOrEmpty(text)
                    && !orderOpen
                    && Time.unscaledTime >= showAt;

        if (panel != null && panel.enabled != show) panel.enabled = show;
        if (label == null) return;

        if (label.enabled != show) label.enabled = show;

        if (!show)
        {
            Revealed = false;

            // "한 번 봤다" 는 조리 화면을 벗어날 때만 내린다. 재료를 넣고 한 박자 쉬는 동안
            // 안내가 잠깐 비는데, 거기서 같이 내리면 어두운 판이 걷혀 화면이 깜빡인다.
            if (orderOpen || tutorial == null || !tutorial.IsRunning) RevealedOnce = false;

            // 다음에 다시 뜰 때 처음부터 찍도록 되돌린다. 안 그러면 같은 문구가 다시 뜰 때
            // typed 가 이미 끝까지 차 있어 타자기가 한 번도 안 돈다.
            laidOut = null;
            typed = 0f;
            return;
        }

        if (label.text != text) label.text = text;

        if (laidOut != text)
        {
            Layout(text);
            typed = 0f;
            Sfx.Play("sfx_flow_hint", 0.5f);
        }

        // 한 글자씩 찍는다. 글은 다 넣어 두고 보이는 글자 수만 늘린다 —
        // 글자마다 text 를 다시 넣으면 줄바꿈을 다시 계산해 줄이 출렁인다.
        typed += charsPerSecond * Time.unscaledDeltaTime;
        label.maxVisibleCharacters = Mathf.FloorToInt(typed);

        Revealed = typed >= text.Length;
        if (Revealed) RevealedOnce = true;
    }

    private void OnDisable()
    {
        Revealed = false;
        RevealedOnce = false;
    }

    /// <summary>
    /// 판을 글에 맞춰 늘린다. 대사 말풍선과 같은 방식이다.
    ///
    /// 가로는 maxTextWidth 까지만 늘리고 그 뒤로는 줄을 접는다. 세로는 접힌 줄 수만큼 늘어난다.
    /// 한 줄짜리 "파를 넣어주세요." 와 두 줄짜리 Tab 안내가 같은 판을 쓰면,
    /// 짧은 문구일 때 판이 휑하게 남는다.
    ///
    /// 크기는 올림해서 정수로 맞춘다. 반칸에 걸치면 9-슬라이스 테두리가 흐려진다.
    /// </summary>
    private void Layout(string text)
    {
        laidOut = text;

        Vector2 wanted = label.GetPreferredValues(text, maxTextWidth, 0f);
        float w = Mathf.Ceil(Mathf.Min(wanted.x, maxTextWidth));
        float h = Mathf.Ceil(wanted.y);

        label.rectTransform.sizeDelta = new Vector2(w, h);

        if (panel != null)
        {
            panel.rectTransform.sizeDelta = new Vector2(w + padding.x * 2f, h + padding.y * 2f);
        }
    }
}
