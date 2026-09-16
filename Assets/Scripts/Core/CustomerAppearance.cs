using System.Collections;
using System.Collections.Generic;
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
    /// 걸어 들어올 때 쓰는 인영 한 장씩. 스프라이트 이름이 "Silhouette_&lt;말투&gt;" 다.
    /// 빌더가 손님 그림 첫 장의 안쪽 빈 자리를 메워 구워 준다.
    /// </summary>
    [SerializeField] private Sprite[] silhouettes;

    /// <summary>지금 손님의 인영. 없으면 인영 대신 원래 그림을 그대로 칠한다.</summary>
    private Sprite silhouette;

    /// <summary>
    /// 주문 결과창 대사 옆에 붙는 작은 초상. 스프라이트 이름이 그대로 말투다("Polite").
    /// 손님 그림에서 얼굴만 잘라 48칸으로 구워 둔 것이다(Tools/make_customer_thumbs.py).
    ///
    /// 손님 그림을 그대로 줄여 쓰지 않는 까닭 — 그 그림은 Point 필터로 들어와 있어서
    /// 300칸을 40칸으로 줄이면 픽셀을 골라 쓰고 나머지를 버린다. 눈 한 줄이 통째로 사라진다.
    /// </summary>
    [SerializeField] private Sprite[] faces;

    /// <summary>지금 손님 초상. 그림이 없는 말투면 null 이다.</summary>
    public Sprite Face { get; private set; }

    /// <summary>
    /// 목에서 그릇 윗변까지(칸). 손님별 값이 없을 때 쓰는 기본값이다.
    ///
    /// 25 는 **입 높이**다. 입만 가리면 코가 그대로 드러나 "그릇에 입을 대고 있다" 가 아니라
    /// "그릇을 턱 밑에 들고 있다" 로 보인다. 손님별로 잰 값이 있으면 그쪽을 쓴다.
    /// </summary>
    private const float SipTopAboveNeckDefault = 25f;

    /// <summary>
    /// 말투마다 그릇 윗변이 목선에서 몇 칸 위에 서야 하는가.
    ///
    /// 목선은 <see cref="MeasureNeck"/> 가 잡는 **가장 잘록한 줄**이고, 실제로는 턱 밑이
    /// 아니라 옷깃 근처다. 거기서부터 재기 때문에 값이 20~40 대로 나온다.
    ///
    /// 재는 법 — 손님 그림에 목선부터 5칸 간격으로 눈금을 그어 놓고 코와 눈 아랫변을 읽는다.
    /// **코끝보다 위, 눈 아랫변보다 아래**가 들어갈 자리다. 그 사이가 10칸 남짓이라 좁다.
    /// 눈까지 덮으면 그릇을 뒤집어쓴 꼴이 되고, 코가 보이면 마시는 걸로 안 읽힌다.
    ///
    /// **값은 눈 아랫변에서 6칸 내린 것**이다. 코를 직접 겨누는 것보다 이쪽이 덜 틀린다 —
    /// 코끝은 그림마다 흐릿한데 눈은 또렷해서 읽기 쉽고, 눈보다 아래면 코는 저절로 덮인다.
    /// 안경을 쓴 둘(Grandpa·Otaku)만 안경테 아랫변 기준으로 더 내렸다.
    ///
    /// Polite 실측 — 입 +18, 코끝 +30, 눈 아랫변 +42. 그래서 36.
    ///
    /// **편차가 크다.** 29(Otaku)에서 50(Jeolla)까지 스무 칸이 넘게 벌어진다. 목선을 잡는
    /// 자리(가장 잘록한 줄)가 그림마다 옷깃이기도 하고 턱 밑이기도 해서다. 한 값으로는 안 된다.
    /// </summary>
    private static readonly Dictionary<string, float> sipTopAboveNeck =
        new Dictionary<string, float>
        {
            { "Child", 32f },
            { "Chungcheong", 34f },
            { "Emotional", 30f },
            { "Formal", 34f },
            { "Gourmet", 34f },
            { "Grandma", 38f },
            { "Grandpa", 30f },       // 안경테 아랫변에 맞춤
            { "Gyeongsang", 38f },
            { "Jeolla", 50f },
            { "Military", 42f },
            { "Otaku", 29f },         // 안경테 아랫변에 맞춤
            { "Polite", 36f },
            { "Sageuk", 38f },
            { "Youtuber", 38f },
        };

    /// <summary>지금 손님의 그릇 윗변 목표(목선에서 몇 칸 위). SetPersona 가 채운다.</summary>
    private float sipTopOffset = SipTopAboveNeckDefault;

    /// <summary>
    /// 프레임이 거꾸로 구워진 손님 — **0번이 눈 감음이고 마지막이 눈 뜸**이다.
    ///
    /// 열넷 중 충청 하나뿐이다. 나머지 열셋은 0번이 뜸·마지막이 감음이라, 그 전제가
    /// <see cref="OpenEyes"/> · <see cref="CloseEyes"/> · <see cref="BuildBlinkOrder"/> 에 박혀 있었다.
    /// 그대로 두면 충청은 **시식 컷신에서 눈을 뜬 채로 먹고**, 평소에는 감고 서 있다가 잠깐 뜬다.
    ///
    /// 그림을 다시 구워 순서를 맞추는 쪽이 깔끔하지만, `bake_customers.py` 가 굽는 장 순서를
    /// 바꾸면 그 그림을 쓰는 다른 곳도 같이 틀어진다. 여기서 뒤집는 편이 좁게 끝난다.
    ///
    /// (전라는 두 장 다 감은 눈이라 아예 안 뜬다. 그건 뒤집어도 안 고쳐지는 그림 쪽 일이다.)
    /// </summary>
    private static readonly HashSet<string> reversedEyes = new HashSet<string> { "Chungcheong" };

    /// <summary>지금 손님이 거꾸로 구워졌는가. SetPersona 가 채운다.</summary>
    private bool eyesReversed;

    /// <summary>눈 뜬 장·감은 장. 장 수와 순서에 상관없이 이 둘만 쓴다.</summary>
    private int OpenFrame { get { return eyesReversed ? FrameCount - 1 : 0; } }

    private int ClosedFrame { get { return eyesReversed ? 0 : FrameCount - 1; } }

    /// <summary>그림 아래변에서 목까지(칸). 못 쟀으면 음수.</summary>
    private float neckFromBottom = -1f;

    /// <summary>말투마다 한 번만 재고 기억해 둔다. 한 번 재는 데 8만 칸을 훑는다.</summary>
    private static readonly Dictionary<string, float> neckCache = new Dictionary<string, float>();

    /// <summary>
    /// 눈을 감고 있는 시간(초).
    ///
    /// 1초는 사람이 깜빡이는 속도(0.1초 남짓)보다 훨씬 길다. 일부러 그렇게 잡았다 —
    /// "깜빡"이 아니라 **지그시 감았다 뜨는** 것으로 읽히고, 손님이 느긋해 보인다.
    /// 밤 포장마차의 박자에 맞춘 값이다.
    /// </summary>
    [SerializeField] private float closedSeconds = 1f;

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

        Face = FaceFor(personaId);

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
        silhouette = SilhouetteFor(personaId);
        neckFromBottom = NeckOf(personaId, frames[0]);

        float tuned;
        sipTopOffset = personaId != null && sipTopAboveNeck.TryGetValue(personaId, out tuned)
                       ? tuned : SipTopAboveNeckDefault;
        eyesReversed = personaId != null && reversedEyes.Contains(personaId);

        blinkOrder = BuildBlinkOrder();
        idleIndex = 0;
        idleElapsed = 0f;
        idleHold = NextHold(OpenFrame);
        ShowPortrait(frames[OpenFrame]);
    }

    /// <summary>
    /// 눈 깜빡임. 뜬 눈으로 한참 있다가 한 번 지그시 감았다 뜬다.
    ///
    /// 쓰는 장은 **완전히 뜬 것과 완전히 감은 것 둘뿐**이다(<see cref="BuildBlinkOrder"/>).
    /// 뜬 채로 2.5~5.5초 무작위, 감은 채로 <see cref="closedSeconds"/> 만큼 머문다.
    /// 뜬 시간을 매번 새로 뽑는 것은 손님이 여럿일 때 박자가 맞아떨어지지 않게 하려는 것이다.
    /// </summary>
    private void Update()
    {
        if (blinkOrder == null || blinkOrder.Length < 2 || portraitImage == null) return;
        if (!portraitImage.enabled) return;
        if (held) return;

        // 긴 프레임은 잘라서 센다. Play 를 누른 뒤 첫 프레임은 4초가 넘기도 하는데,
        // 그대로 더하면 손님이 뜨자마자 한 번 감았다 뜬다.
        idleElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
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

    public void OpenEyes() { HoldFrame(OpenFrame); }

    public void CloseEyes() { HoldFrame(ClosedFrame); }

    /// <summary>
    /// 걸어 들어오는 동안 인영 한 장으로 고정한다. 끄면 원래 그림으로 돌아가 다시 깜빡인다.
    ///
    /// 인영은 안쪽 빈 자리를 메워 구운 그림이다. 원본을 그대로 까맣게 칠하면 팔과 몸 사이
    /// 같은 데로 배경이 비쳐 몸에 구멍이 뚫린 것처럼 보인다. 인영이 없는 손님은
    /// 원래 그림을 그대로 쓴다 — 구멍이 없는 손님이 열넷 중 열이다.
    /// </summary>
    public void ShowSilhouette(bool on)
    {
        if (portraitImage == null) return;

        if (!on)
        {
            ReleaseFrame();
            return;
        }

        held = true;   // 인영으로 서 있는 동안에는 깜빡이지 않는다
        if (silhouette != null) ShowPortrait(silhouette);
    }

    /// <summary>
    /// 국물을 마실 때 그릇 윗변이 서야 할 높이(자리 아래변이 0).
    ///
    /// 손님마다 그림 높이도 목 자리도 달라서, 한 값을 더하면 누구에게는 그릇이 입에 못 미치고
    /// 누구에게는 눈까지 덮인다. 실제로 열넷을 재 보니 목이 그림 아래에서 104~132 로
    /// 서른 칸 가까이 벌어져 있었다. 그래서 목선에서 거꾸로 잰다.
    /// </summary>
    public float SipTopInSlot
    {
        get
        {
            RectTransform rect = portraitImage.rectTransform;
            return rect.anchoredPosition.y + neckFromBottom + sipTopOffset;
        }
    }

    /// <summary>목선을 잴 수 있는가. 못 재면 부르는 쪽이 예전 방식으로 돌아간다.</summary>
    public bool HasSipTop
    {
        get { return neckFromBottom >= 0f && portraitImage != null && portraitImage.enabled; }
    }

    /// <summary>말투 하나의 목 높이. 처음 한 번만 재고 그 뒤로는 기억해 둔 값을 쓴다.</summary>
    private static float NeckOf(string personaId, Sprite sprite)
    {
        float found;
        if (!string.IsNullOrEmpty(personaId) && neckCache.TryGetValue(personaId, out found)) return found;

        found = MeasureNeck(sprite);
        if (!string.IsNullOrEmpty(personaId)) neckCache[personaId] = found;

        return found;
    }

    /// <summary>
    /// 그림 아래변에서 목까지 몇 칸인지 잰다. 못 재면 음수.
    ///
    /// 살색을 찾지 않는다. 손님마다 피부 톤도 옷도 달라서, 살색으로 얼굴을 잡으려다
    /// 열넷 중 둘에서 엉뚱한 줄이 잡혔다(모자챙, 그리고 얼굴보다 넓은 손).
    ///
    /// 대신 불투명한 칸이 가로로 가장 좁아지는 줄을 쓴다. 머리와 어깨 사이가 잘록한 것은
    /// 열넷 모두에서 또렷하고, 그 자리가 곧 턱 밑이다.
    /// 위아래 끝은 건너뛴다 — 머리끝과 몸통은 애초에 목이 아니다.
    /// </summary>
    private static float MeasureNeck(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return -1f;

        Rect area = sprite.textureRect;
        int w = Mathf.RoundToInt(area.width);
        int h = Mathf.RoundToInt(area.height);
        if (w <= 0 || h <= 0) return -1f;

        Color[] px = sprite.texture.GetPixels(Mathf.RoundToInt(area.x), Mathf.RoundToInt(area.y), w, h);

        // 텍스처 좌표는 아래가 0 이라, 찾은 줄 번호가 그대로 "아래에서 몇 칸" 이다.
        int lo = Mathf.RoundToInt(h * 0.22f);
        int hi = Mathf.RoundToInt(h * 0.55f);

        int neck = lo;
        int thinnest = int.MaxValue;

        for (int y = lo; y < hi; y++)
        {
            int wide = 0;
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0.5f) wide++;

            if (wide >= thinnest) continue;

            thinnest = wide;
            neck = y;
        }

        return neck;
    }

    private Sprite SilhouetteFor(string personaId)
    {
        if (string.IsNullOrEmpty(personaId) || silhouettes == null) return null;

        string wanted = "Silhouette_" + personaId;
        foreach (Sprite sprite in silhouettes)
        {
            if (sprite != null && sprite.name == wanted) return sprite;
        }

        return null;
    }

    /// <summary>초상은 파일 이름이 곧 말투라 앞머리를 붙일 것이 없다.</summary>
    private Sprite FaceFor(string personaId)
    {
        if (string.IsNullOrEmpty(personaId) || faces == null) return null;

        foreach (Sprite sprite in faces)
        {
            if (sprite != null && sprite.name == personaId) return sprite;
        }

        return null;
    }

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

    /// <summary>이 장을 얼마나 물고 있을지. 뜬 눈(0번)은 무작위로 길고, 감은 눈은 정해진 값이다.</summary>
    private float NextHold(int frame)
    {
        // 0 번이 아니라 **눈 뜬 장**과 견준다. 충청은 0 번이 감은 장이라, 0 으로 견주면
        // 뜬 채로 1초만 있다가 감은 채로 몇 초씩 서 있게 된다.
        if (frame != OpenFrame) return Mathf.Max(0.01f, closedSeconds);

        return Random.Range(Mathf.Max(0.1f, openSecondsMin), Mathf.Max(openSecondsMin, openSecondsMax));
    }

    /// <summary>
    /// 깜빡이는 차례. **완전히 뜬 장과 완전히 감은 장 둘만 쓴다.**
    ///
    /// 그림은 손님마다 2·3·4장으로 제각각인데(중간은 반쯤 감은 장이다), 예전에는 있는 장을
    /// 다 거쳐 갔다 되짚었다(0→1→2→3→2→1→0). 그래서 4장짜리는 느릿하게 감기고 2장짜리는
    /// 툭 감겨 손님마다 박자가 달랐다. 중간 장을 버리면 장수와 무관하게 전부 같아진다.
    ///
    /// 중간 장은 그림에 그대로 남는다. 안 쓸 뿐이다.
    /// </summary>
    private int[] BuildBlinkOrder()
    {
        if (FrameCount < 2) return null;

        return new[] { OpenFrame, ClosedFrame };
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

    /// <summary>
    /// 그림 꼭대기에서 귀까지가 그림 높이의 몇 할인가.
    ///
    /// 0.2 였다. 그 값이면 꼬리가 귀가 아니라 정수리를 가리킨다.
    /// 2026-09-14 에 손님 그림 열넷을 실제로 재서 다시 잡았다 — 살색이 이어지는 구간으로
    /// 얼굴 위아래를 찾고 그 한가운데를 귀로 봤다. 잡힌 아홉 명이 0.415~0.479 였고
    /// 중앙값이 0.44 다(모자·안경에 가린 다섯은 자동으로 못 재서 뺐다).
    /// </summary>
    private const float EarFromTop = 0.44f;

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
        SetTint(1f - t, 1f - t * t);
    }

    /// <summary>
    /// 밝기와 진하기를 따로 준다. shade 0 은 새까만 인영, 1 은 제 색이다.
    ///
    /// <see cref="SetFade"/> 는 이 둘을 "어둠에 잠기며 지워진다" 한 줄기로 묶어 쓴 것이고,
    /// 걸어 들어오는 연출은 까맣게 보이는 채로 서 있어야 해서 둘을 갈라 쓴다.
    /// </summary>
    public void SetTint(float shade, float alpha)
    {
        var tint = new Color(shade, shade, shade, Mathf.Clamp01(alpha));

        if (bodyImage != null) bodyImage.color = tint;
        if (headImage != null) headImage.color = tint;
        if (portraitImage != null) portraitImage.color = tint;
    }

    /// <summary>
    /// 손님을 제자리에서 옆으로·위로 밀어 둔다. (0,0)이 카운터 앞 제자리다.
    ///
    /// 이 스크립트가 붙은 자리(CustomerSlot)를 통째로 옮긴다. 안쪽 그림만 옮기면
    /// 자리에 씌운 마스크가 옆으로 나간 부분을 잘라 내어, 걸어오는 도중에 몸이 잘린다.
    ///
    /// 제자리는 빌더가 정해 준 값을 처음 쓸 때 한 번 적어 둔다. Awake 에서 읽지 않는 것은
    /// 이 자리가 꺼진 채로 시작할 수 있기 때문이다.
    ///
    /// 칸은 정수로 끊는다. 픽셀아트라 반 칸에 놓이면 그림 전체가 한 겹 흐려진다.
    /// </summary>
    /// <summary>
    /// 카운터 밑으로 무너진다. 크레딧 마지막의 「콰당」에 쓴다.
    ///
    /// **돌리지 않는다.** 손님 그림은 흉상이라 아래가 평평하게 잘려 있어서, 기울이면
    /// 그 자른 선이 드러나 사람이 아니라 판때기가 넘어가는 것으로 보인다. 실제로
    /// 예전 연출이 −72도로 돌렸는데 그 문제가 그대로 났다.
    ///
    /// 대신 <see cref="SetOffset"/> 로 아래로 민다. 카운터(주문화면 카운터.png)가
    /// 손님 **앞**에 덮인 진짜 레이어라, 내려가는 만큼 알아서 가려 준다. 눈속임이 아니다.
    ///
    /// 내리는 깊이 185 는 네 값을 세워 놓고 고른 것이다.
    ///   112 구부정하다 · 150 카운터에 얼굴을 묻었다 · <b>185 쓰러졌다</b> · 220 사라졌다
    ///
    /// 가속하는 까닭 — 등속으로 내려가면 쓰러지는 것이 아니라 엘리베이터를 탄 것으로 보인다.
    ///
    /// 끝나고 제자리로 돌리지 않는다. 쓰러진 채로 있어야 그 위로 까마귀가 지나간다.
    /// 다음 손님을 받기 전에 부르는 쪽이 <see cref="Reseat"/> 로 세워 준다.
    /// </summary>
    /// <param name="drop">아래로 내리는 거리.</param>
    /// <param name="sideways">옆으로도 밀고 싶을 때. 양수면 오른쪽이다.
    /// 0 이 아니면 「의자에서 흘러내린다」가 된다 — 다만 까마귀가 앉는 자리(x −120)와 겹치지 않게 볼 것.</param>
    public IEnumerator Collapse(float drop = 185f, float sideways = 0f, float seconds = 0.8f)
    {
        // **제 크기로 되돌려 놓고 시작한다.**
        //
        // 시식 컷신이 손님을 확대해서 눈을 창 한가운데에 맞춘다(EatingCutscene.ApplyFraming).
        // 그때 자리(anchoredPosition)와 **배율(localScale)** 을 둘 다 직접 박는데,
        // SetOffset 은 자리만 건드리고 배율은 모른다. 확대가 남은 채로 쓰러지면
        // 손님이 엉뚱한 크기·자리에 있어서, 재어 둔 깊이(185)도 까마귀 앉을 자리도 다 어긋난다.
        //
        // 실제로 크레딧에서 그 일이 났다 — 머리가 카운터 밑으로 사라지고, 까마귀는 아무것도
        // 없는 허공에 앉고, 아이리스도 그 까마귀를 따라가 엉뚱한 데를 조였다.
        var seatRect = transform as RectTransform;
        if (seatRect != null) seatRect.localScale = Vector3.one;

        CloseEyes();

        // 버티다 무너지는 맛. 넘어가기 직전에 좌우로 두 칸 휘청인다.
        SetOffset(new Vector2(2f, 0f));
        yield return null;
        SetOffset(new Vector2(-2f, 0f));
        yield return null;

        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float k = Mathf.Pow(Mathf.Clamp01(t / seconds), 1.8f);
            SetOffset(new Vector2(sideways * k, -drop * k));
            yield return null;
        }

        SetOffset(new Vector2(sideways, -drop));
        Sfx.Play("sfx_cook_drop", 0.9f, 0.7f);
    }

    /// <summary>쓰러진 손님을 제자리에 세운다. 눈도 다시 뜨고 크기도 되돌린다.</summary>
    public void Reseat()
    {
        var seatRect = transform as RectTransform;
        if (seatRect != null) seatRect.localScale = Vector3.one;

        SetOffset(Vector2.zero);
        ReleaseFrame();
    }

    public void SetOffset(Vector2 offset)
    {
        if (!seatCaptured)
        {
            seat = transform as RectTransform;
            if (seat == null) return;

            seatHome = seat.anchoredPosition;
            seatCaptured = true;
        }

        seat.anchoredPosition = new Vector2(Mathf.Round(seatHome.x + offset.x),
                                            Mathf.Round(seatHome.y + offset.y));
    }

    private RectTransform seat;
    private Vector2 seatHome;
    private bool seatCaptured;

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
