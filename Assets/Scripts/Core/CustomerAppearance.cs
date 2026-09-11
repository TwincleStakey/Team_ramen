using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 겉모습. 얼굴과 몸통을 따로 두고 손님이 바뀔 때마다 무작위로 짝지어 준다.
///
/// 얼굴 넷과 몸통 넷이면 조합이 열여섯 가지다. 그림 여덟 장으로 손님 열여섯 명을 만드는 셈이라,
/// 사람마다 통짜 그림을 그리는 것보다 훨씬 싸게 붙는다.
///
/// 언제 다시 뽑을지는 OrderScreenUI 가 정한다. 예전에는 화면이 켜질 때마다 스스로 뽑았는데,
/// 라멘을 낸 뒤 같은 손님이 먹는 장면을 보여 주려고 화면을 다시 켜면서 그 전제가 깨졌다.
/// 화면이 켜졌다고 새 손님인 것은 아니다.
/// </summary>
public class CustomerAppearance : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image headImage;
    [SerializeField] private Sprite[] bodies;
    [SerializeField] private Sprite[] heads;

    /// <summary>손님 한 명을 통짜로 그린 그림을 얹는 판. 얼굴·몸통 대신 이것만 쓴다.</summary>
    [SerializeField] private Image portraitImage;

    /// <summary>
    /// 손님 14명의 대기 동작 프레임을 한 줄로 이어 놓은 것.
    ///
    /// 어느 손님 것인지는 스프라이트 이름 앞머리로 가른다("Polite_0" → Polite).
    /// 손님별로 배열을 따로 두려면 직렬화되는 그릇을 새로 만들어야 하는데,
    /// 파일 이름을 personaId 로 맞춰 두면(Tools/bake_customers.py) 그럴 필요가 없다.
    /// </summary>
    [SerializeField] private Sprite[] portraits;

    /// <summary>
    /// 감는 도중 한 장이 머무는 시간(초). 사람은 한 번 깜빡이는 데 0.1초 남짓이다.
    /// </summary>
    [SerializeField] private float blinkFrameSeconds = 0.06f;

    /// <summary>눈을 뜬 채로 머무는 시간(초). 이 사이에서 매번 새로 뽑아 손님마다 박자가 어긋나게 한다.</summary>
    [SerializeField] private float openSecondsMin = 2.5f;
    [SerializeField] private float openSecondsMax = 5.5f;

    /// <summary>
    /// 목이 옷깃 속으로 들어가는 깊이.
    ///
    /// 얼굴 그림 아래에 목을 10칸 그려 붙여 두었다. 그중 이만큼이 옷깃에 묻히고
    /// 나머지가 밖으로 보인다. 지금은 9칸이 묻히고 1칸이 보인다.
    ///
    /// 처음에는 19칸을 겹쳐 목이 통째로 묻혔고(얼굴이 어깨에 바로 얹힌 꼴), 그다음 5칸으로
    /// 줄였더니 이번엔 목이 길어 보였다. 값을 키우면 목이 짧아지고 줄이면 길어진다.
    /// </summary>
    [SerializeField] private float neckOverlap = 9f;

    /// <summary>
    /// 몸통을 자리 아래변보다 얼마나 더 내릴지.
    ///
    /// 손님은 카운터 뒤에 서 있다. 이만큼 내리면 아래쪽이 자리 밖으로 나가고,
    /// 자리에 씌운 마스크가 거기서 잘라 내어 카운터에 가린 것처럼 보인다.
    ///
    /// 26 이었다가 14 로 줄였다. 자리 아래변(카운터 선)이 잘못 잡혀 있어 12칸 위에 있었는데,
    /// 그걸 배경 그림에서 다시 재어 12칸 내렸다. 같이 줄이지 않으면 손님이 통째로 12칸
    /// 내려앉는다. 지금 값은 몸통을 제자리에 두고 잘리는 높이만 카운터에 맞춘 것이다.
    /// </summary>
    [SerializeField] private float sinkBelowCounter = 14f;

    /// <summary>방금 서 있던 손님. 다음 손님이 같은 짝으로 나오지 않게 기억해 둔다.</summary>
    private int lastBody = -1;
    private int lastHead = -1;

    /// <summary>
    /// 얼굴과 몸통을 하나씩 뽑아 세운다.
    ///
    /// 앞 손님과 같은 짝이 다시 나오면 손님이 안 바뀐 것처럼 보인다. 조합이 열여섯뿐이라
    /// 그냥 뽑으면 열여섯 번에 한 번은 그렇게 된다. 겹치면 다시 뽑는다.
    /// </summary>
    /// <summary>
    /// 이 손님 말투에 맞는 그림을 세운다. 그림이 있는 손님은 통짜 그림을 쓰고,
    /// 없으면 예전처럼 얼굴·몸통을 무작위로 짝지어 세운다.
    /// </summary>
    public void SetPersona(string personaId)
    {
        Sprite[] frames = FramesFor(personaId);

        if (frames == null || frames.Length == 0)
        {
            if (portraitImage != null) portraitImage.enabled = false;
            if (bodyImage != null) bodyImage.enabled = true;
            if (headImage != null) headImage.enabled = true;
            Randomize();
            return;
        }

        if (bodyImage != null) bodyImage.enabled = false;
        if (headImage != null) headImage.enabled = false;

        idleFrames = frames;
        blinkOrder = BuildBlinkOrder(frames.Length);
        idleIndex = 0;
        idleElapsed = 0f;
        idleHold = NextHold(0);
        ShowPortrait(frames[0]);
    }

    /// <summary>
    /// 눈 깜빡임. 뜬 눈을 오래 물고 있다가 한 번 빠르게 감았다 뜬다.
    ///
    /// 그림은 0번이 뜬 눈이고 뒤로 갈수록 감긴다. 같은 간격으로 돌리면 깜빡임이 아니라
    /// "느리게 넘어가는 그림"으로 보인다. 사람은 3~6초에 한 번, 한 번에 0.1초 남짓 깜빡인다.
    ///
    /// 다시 뜰 때는 왔던 길을 되짚는다(0→1→2→1→0). 감긴 채로 0번으로 튀면 눈이 툭 열린다.
    /// </summary>
    private void Update()
    {
        if (blinkOrder == null || blinkOrder.Length < 2 || portraitImage == null) return;
        if (!portraitImage.enabled) return;
        if (held) return;

        idleElapsed += Time.unscaledDeltaTime;
        if (idleElapsed < idleHold) return;

        idleElapsed = 0f;
        idleIndex = (idleIndex + 1) % blinkOrder.Length;

        int frame = blinkOrder[idleIndex];
        portraitImage.sprite = idleFrames[frame];
        idleHold = NextHold(frame);
    }

    /// <summary>고정된 장이 있는 동안에는 깜빡이지 않는다.</summary>
    private bool held;

    /// <summary>지금 손님에게 있는 장 수. 없으면 0.</summary>
    public int FrameCount
    {
        get { return idleFrames == null ? 0 : idleFrames.Length; }
    }

    /// <summary>
    /// 깜빡임을 멈추고 한 장으로 고정한다. 시식 연출에서 쓴다 —
    /// 먹는 동안 눈을 감겨 두었다가 감동하는 순간 번쩍 뜨는 것이 그것이다.
    ///
    /// 0번이 뜬 눈이고 마지막 장이 가장 많이 감긴 눈이다. 손님마다 장 수가 2~4로 달라서
    /// 번호를 박아 쓰면 어떤 손님은 엉뚱한 장이 나온다. <see cref="OpenEyes"/> ·
    /// <see cref="CloseEyes"/> 를 쓰면 장 수와 무관하게 맞는다.
    /// </summary>
    public void HoldFrame(int frame)
    {
        if (idleFrames == null || idleFrames.Length == 0 || portraitImage == null) return;

        held = true;
        portraitImage.sprite = idleFrames[Mathf.Clamp(frame, 0, idleFrames.Length - 1)];
    }

    public void OpenEyes() { HoldFrame(0); }

    public void CloseEyes() { HoldFrame(FrameCount - 1); }

    /// <summary>고정을 풀고 다시 깜빡이게 한다. 뜬 눈에서 새로 시작한다.</summary>
    public void ReleaseFrame()
    {
        held = false;
        idleIndex = 0;
        idleElapsed = 0f;
        idleHold = NextHold(0);

        if (portraitImage != null && idleFrames != null && idleFrames.Length > 0)
            portraitImage.sprite = idleFrames[0];
    }

    /// <summary>이 장을 얼마나 물고 있을지. 뜬 눈(0번)만 길고 나머지는 짧다.</summary>
    private float NextHold(int frame)
    {
        if (frame != 0) return Mathf.Max(0.01f, blinkFrameSeconds);

        return Random.Range(Mathf.Max(0.1f, openSecondsMin), Mathf.Max(openSecondsMin, openSecondsMax));
    }

    /// <summary>
    /// 깜빡이는 차례. 감았다가 왔던 길로 되돌아온다.
    /// 두 장짜리는 되짚을 중간이 없어 그냥 감았다 뜬다.
    /// </summary>
    private static int[] BuildBlinkOrder(int frameCount)
    {
        if (frameCount < 2) return null;
        if (frameCount == 2) return new[] { 0, 1 };

        var order = new int[frameCount * 2 - 2];
        for (int i = 0; i < frameCount; i++) order[i] = i;
        for (int i = 1; i < frameCount - 1; i++) order[frameCount - 1 + i] = frameCount - 1 - i;
        return order;
    }

    /// <summary>이름 앞머리가 personaId 와 같은 프레임을 모은다.</summary>
    private Sprite[] FramesFor(string personaId)
    {
        if (string.IsNullOrEmpty(personaId) || portraits == null) return null;

        var found = new System.Collections.Generic.List<Sprite>();
        foreach (Sprite sprite in portraits)
        {
            if (sprite == null) continue;

            int underscore = sprite.name.LastIndexOf('_');
            string id = underscore < 0 ? sprite.name : sprite.name.Substring(0, underscore);
            if (id == personaId) found.Add(sprite);
        }

        return found.ToArray();
    }

    private void ShowPortrait(Sprite sprite)
    {
        if (portraitImage == null) return;

        portraitImage.enabled = true;
        portraitImage.sprite = sprite;

        // 몸통과 같은 자리에 놓는다. 아래를 카운터 밑으로 내려 자리 마스크가 잘라 내면
        // 카운터 뒤에 서 있는 것으로 보인다.
        RectTransform rect = portraitImage.rectTransform;
        rect.sizeDelta = sprite.rect.size;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, -sinkBelowCounter);
    }

    private Sprite[] idleFrames;

    /// <summary>깜빡이는 차례(프레임 번호). 되짚는 길까지 미리 펴 둔다.</summary>
    private int[] blinkOrder;

    private int idleIndex;
    private float idleElapsed;

    /// <summary>지금 장을 얼마나 더 물고 있을지.</summary>
    private float idleHold;

    /// <summary>
    /// 지금 손님 머리 꼭대기가 자리 안에서 어느 높이인가(자리 아래변이 0).
    ///
    /// 손님 그림은 머리 위 빈 줄을 잘라 구워 두었다(Tools/bake_customers.py).
    /// 그래서 그림 높이가 곧 머리 높이다 — 픽셀을 뒤져 볼 필요가 없다.
    /// 그림이 없는 손님은 얼굴 판 위끝을 그대로 돌려준다.
    /// </summary>
    public float HeadTopInSlot
    {
        get
        {
            if (portraitImage != null && portraitImage.enabled)
            {
                RectTransform rect = portraitImage.rectTransform;
                return rect.anchoredPosition.y + rect.rect.height;
            }

            if (headImage != null)
            {
                RectTransform rect = headImage.rectTransform;
                return rect.anchoredPosition.y + rect.rect.height;
            }

            return 0f;
        }
    }

    /// <summary>
    /// 지금 손님 귀 높이(자리 아래변이 0). 말풍선을 여기에 맞춘다 —
    /// 머리 꼭대기에 맞추면 말이 정수리에서 나오는 것처럼 보인다.
    ///
    /// 흉상 그림이라 머리가 위쪽 절반쯤을 차지한다. 꼭대기에서 그림 높이의 이만큼 내려오면
    /// 눈·귀 높이다. 손님마다 그림 높이가 달라도 비율이라 같이 따라간다.
    /// </summary>
    public float EarInSlot
    {
        get
        {
            if (portraitImage != null && portraitImage.enabled)
            {
                RectTransform rect = portraitImage.rectTransform;
                return rect.anchoredPosition.y + rect.rect.height * (1f - EarFromTop);
            }

            // 그림이 없는 손님은 얼굴 판 한가운데를 쓴다.
            if (headImage != null)
            {
                RectTransform rect = headImage.rectTransform;
                return rect.anchoredPosition.y + rect.rect.height * 0.5f;
            }

            return 0f;
        }
    }

    /// <summary>그림 꼭대기에서 귀까지가 그림 높이의 몇 할인가.</summary>
    private const float EarFromTop = 0.2f;

    public void Randomize()
    {
        if (bodies == null || bodies.Length == 0) return;
        if (heads == null || heads.Length == 0) return;

        int body = Random.Range(0, bodies.Length);
        int head = Random.Range(0, heads.Length);

        // 조합이 하나뿐이면 다시 뽑아도 같으므로 그대로 쓴다. 안 그러면 여기서 영영 돈다.
        if (bodies.Length * heads.Length > 1)
        {
            while (body == lastBody && head == lastHead)
            {
                body = Random.Range(0, bodies.Length);
                head = Random.Range(0, heads.Length);
            }
        }

        lastBody = body;
        lastHead = head;

        SetBody(bodies[body]);
        SetHead(heads[head]);
    }

    /// <summary>
    /// 스러진 정도. 0이면 평소 모습이고 1이면 완전히 사라진 상태다.
    ///
    /// 알파만 내리면 뒤 배경이 그대로 비쳐서 "흐려진다"로 보인다. 색을 검정 쪽으로 함께
    /// 당기면 어둠에 잠기듯 사라진다. 알파는 제곱으로 늦춰 두었다 —
    /// 먼저 어두워지고 그다음에 지워져야 인영이 스러지는 것으로 읽힌다.
    /// </summary>
    public void SetFade(float t)
    {
        t = Mathf.Clamp01(t);

        float shade = 1f - t;
        float alpha = 1f - t * t;
        var tint = new Color(shade, shade, shade, alpha);

        if (bodyImage != null) bodyImage.color = tint;
        if (headImage != null) headImage.color = tint;
        if (portraitImage != null) portraitImage.color = tint;
    }

    private void SetBody(Sprite sprite)
    {
        if (bodyImage == null || sprite == null) return;

        bodyImage.sprite = sprite;

        // 그림마다 크기가 조금씩 다르다. 상자를 그림에 맞춰야 늘어나거나 눌리지 않는다.
        RectTransform rect = bodyImage.rectTransform;
        rect.sizeDelta = sprite.rect.size;

        // 몸통은 자리의 아래변 기준으로 놓되 그보다 더 내린다. 키가 다른 몸통이 와도
        // 잘리는 높이가 같아서, 다들 같은 카운터 뒤에 서 있는 것으로 보인다.
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, -sinkBelowCounter);
    }

    private void SetHead(Sprite sprite)
    {
        if (headImage == null || sprite == null) return;

        headImage.sprite = sprite;

        RectTransform rect = headImage.rectTransform;
        rect.sizeDelta = sprite.rect.size;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);

        // 몸통 꼭대기에서 목이 묻히는 깊이만큼만 내려 얹는다. 몸통이 내려간 만큼 얼굴도 같이 내려간다.
        float bodyTop = bodyImage != null
            ? bodyImage.rectTransform.anchoredPosition.y + bodyImage.rectTransform.sizeDelta.y
            : 0f;
        rect.anchoredPosition = new Vector2(0f, Mathf.Round(bodyTop - neckOverlap));
    }
}
