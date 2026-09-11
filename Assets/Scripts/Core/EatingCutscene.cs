using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 라멘을 낸 뒤 손님이 먹는 네 컷 연출.
///
///   1컷  일반 화면. 손님이 그릇을 받는다.
///   2컷  위아래 검은 바가 들어오고 얼굴이 화면을 채운다. "…"
///   3컷  번쩍이고 흔들린다. "!!"
///   4컷  바가 걷히고 반짝임이 돈다. 정확도에 따른 반응.
///
/// 컷은 툭 바뀐다. 1배에서 3배로 부드럽게 확대하면 중간에 1.4배·2.2배를 지나면서
/// 픽셀이 반칸에 걸려 뭉개진다. 확대는 정수배로만 하고 전환은 한 프레임에 끝낸다.
/// 원본이 만화 네 컷이라 이쪽이 그림에도 맞다.
///
/// 표정 그림이 아직 없다. 지금은 같은 얼굴이 커졌다 작아지고, 말풍선과 효과로만 박자를 낸다.
/// 표정이 들어오면 각 컷에서 얼굴만 갈아 끼우면 된다.
/// 번개와 우주 배경은 임시로 코드로 찍은 그림이다(Tools 의 makeart 스크립트).
/// 나비·꽃은 아직 없어서 흰 점 반짝임으로 대신하고 있다.
/// </summary>
public class EatingCutscene : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;
    [SerializeField] private Image flash;
    [SerializeField] private RectTransform sparkleRoot;
    [SerializeField] private Image cosmos;

    /// <summary>확대 직전에 가게를 덮는 검은 판. 손님보다 뒤에 있어 얼굴은 밝게 남는다.</summary>
    [SerializeField] private Image dim;
    [SerializeField] private Image bolt;
    [SerializeField] private Image aura;
    [SerializeField] private Sprite[] auraFrames;
    [SerializeField] private Image thumb;
    [SerializeField] private RectTransform customerSlot;
    [SerializeField] private RectTransform customerHead;

    /// <summary>손님 앞에 놓인 그릇. 국물을 마실 때 이걸 들어 올린다.</summary>
    [SerializeField] private RectTransform customerBowl;

    /// <summary>관자놀이에 맺히는 땀방울. 0 맺힘 / 1 흘러내림 두 장이다.</summary>
    [SerializeField] private Image sweat;
    [SerializeField] private Sprite[] sweatFrames;

    /// <summary>어색한 침묵에 지나가는 까마귀. 날갯짓 세 장이다.</summary>
    [SerializeField] private Image crow;
    [SerializeField] private Sprite[] crowFrames;

    /// <summary>손님 머리 위 침묵의 점 셋. 까마귀가 지나며 하나씩 찍고 간다.</summary>
    [SerializeField] private Image[] silenceDots;

    /// <summary>
    /// 시네마틱 동안 치울 것들. 상단바처럼 연출과 상관없는 판이 여기 들어간다.
    /// 바가 가려 주는 것도 있지만, 바를 푸는 동안 가장자리가 비친다.
    /// </summary>
    [SerializeField] private GameObject[] hiddenDuringCut;

    /// <summary>
    /// 확대에 들어갈 때 치울 것들. 손님 앞 그릇처럼 "가게 화면의 물건"이지만
    /// 연출 첫 컷에는 남아 있어야 하는 것이 여기 들어간다.
    /// </summary>
    [SerializeField] private GameObject[] hiddenAtZoom;
    [SerializeField] private OrderScreenUI orderScreen;
    [SerializeField] private CutsceneSfx sfx;

    /// <summary>손님 그림. 먹는 동안 눈을 감기고 감동하는 순간 뜨게 하려고 쥔다.</summary>
    [SerializeField] private CustomerAppearance customerAppearance;

    [Header("정확도 구간 경계(0~100)")]
    /// <summary>
    /// 이 값 이상이면 그 구간이다. Perfect 는 100 점인데, 나눗셈 오차로 99.999 가 들어오는
    /// 일이 있어 조금 낮춰 잡는다.
    /// </summary>
    [SerializeField] private float perfectFrom = 99.5f;
    [SerializeField] private float goodFrom = 90f;
    [SerializeField] private float okayFrom = 70f;

    [Header("박자 길이(초)")]
    [SerializeField] private float slurpSeconds = 1.6f;      // 1 후룩후룩
    [SerializeField] private float zoomStepSeconds = 0.22f;  // 2 클로즈업 한 단
    [SerializeField] private float boltSeconds = 1.6f;       // 3 번개
    [SerializeField] private float cosmosSeconds = 3.5f;     // 4 우주 - 여기서 한 박자 쉰다
    [SerializeField] private float auraSeconds = 1.1f;       // 5 오우라

    /// <summary>오우라가 다 그려진 뒤 천천히 스러지는 시간. 툭 꺼지면 여운이 없다.</summary>
    [SerializeField] private float auraFadeSeconds = 1.2f;

    /// <summary>오우라가 다 스러진 뒤 우주를 조금 더 두는 시간. 곧바로 걷히면 숨 돌릴 틈이 없다.</summary>
    [SerializeField] private float cosmosHoldSeconds = 0.8f;

    /// <summary>
    /// 가게가 우주로 덮이는 데 걸리는 시간(초). 툭 갈아 끼우면 장면이 끊겨 보인다.
    /// 우주 배경이 손님보다 뒤에 깔려 있어서, 알파만 녹이면 그대로 크로스 디졸브가 된다.
    /// </summary>
    [SerializeField] private float cosmosFadeSeconds = 1.1f;

    /// <summary>우주에서 가게로 돌아오는 데 걸리는 시간(초). 들어갈 때보다 조금 길게 둔다.</summary>
    [SerializeField] private float cosmosOutSeconds = 1.3f;

    /// <summary>
    /// 우주가 도는 속도(초당 도). 아주 느려야 한다 — 빠르면 배경이 돌아가는 게 아니라
    /// 화면이 기울어지는 것으로 보인다. 그림이 네모라 돌려도 구석이 안 비친다.
    /// </summary>
    [SerializeField] private float cosmosSpinPerSecond = 2.2f;

    /// <summary>확대 직전에 가게를 어둡게 덮는 데 걸리는 시간(초)과 그때의 진하기.</summary>
    [SerializeField] private float dimSeconds = 0.45f;
    [SerializeField] private float dimAlpha = 0.82f;
    [SerializeField] private float thumbSeconds = 3f;        // 7 따봉

    [Header("국물 마시기")]
    /// <summary>
    /// 그릇이 입까지 올라가는 높이(칸).
    ///
    /// 그릇 한가운데가 −75 에 있고 그림 안에서 위로 12칸 더 올라간다. 130 을 올리면 윗변이 67 인데,
    /// 손님 얼굴은 입이 42, 코가 60, 눈이 80 근처다. 입과 코는 가리고 눈은 남는 높이다.
    /// 더 올리면 눈까지 덮여 그릇을 뒤집어쓴 꼴이 되고, 덜 올리면 입이 드러나 마시는 걸로 안 보인다.
    /// </summary>
    [SerializeField] private float sipRise = 130f;
    [SerializeField] private float sipRiseSeconds = 0.85f;
    [SerializeField] private float sipDownSeconds = 0.6f;
    [SerializeField] private float sipHoldSeconds = 0.18f;

    /// <summary>까딱 횟수와 한 번에 걸리는 시간.</summary>
    [SerializeField] private int sipCount = 3;
    [SerializeField] private float sipTipSeconds = 0.4f;

    /// <summary>까딱할 때 그릇이 더 올라가는 칸과 고개가 숙여지는 칸. 둘 다 아주 작아야 한다.</summary>
    [SerializeField] private float sipTipPixels = 7f;
    [SerializeField] private float sipNodPixels = 2f;

    [Header("구간별")]
    /// <summary>Perfect — 눈을 뜨고 번개가 칠 때까지의 뜸. 이 틈이 있어야 "뜬 것"이 보인다.</summary>
    [SerializeField] private float eyeOpenSeconds = 0.45f;

    /// <summary>Good — 바를 얕게만 문다. Perfect(110)보다 확실히 낮아야 딴 장면으로 읽힌다.</summary>
    [SerializeField] private float goodBarHeight = 48f;
    [SerializeField] private float goodSparkleSeconds = 1.8f;

    /// <summary>Okay — 땀이 맺히기 전후로 두는 뜸.</summary>
    [SerializeField] private float puzzledPauseSeconds = 0.5f;

    /// <summary>Bad — 화면을 살짝 내려앉히는 정도. Perfect 의 암전(0.82)처럼 진하면 안 된다.</summary>
    [SerializeField] private float badDimAlpha = 0.3f;

    [Header("땀방울")]
    /// <summary>맺힌 채로 머무는 시간. 여기서 "어...?" 하는 한 박자가 난다.</summary>
    [SerializeField] private float sweatBeadSeconds = 0.55f;

    /// <summary>흘러내리는 데 걸리는 시간과 내려가는 칸. 빨라야 물방울로 보인다.</summary>
    [SerializeField] private float sweatRunSeconds = 0.3f;
    [SerializeField] private float sweatFall = 44f;

    [Header("어색한 침묵")]
    /// <summary>까마귀가 나타나기 전에 아무 일도 안 일어나는 시간. 이 정적이 연출의 본체다.</summary>
    [SerializeField] private float silenceSeconds = 0.9f;

    /// <summary>
    /// 까마귀가 화면을 건너는 데 걸리는 시간과 날갯짓 한 장의 길이.
    ///
    /// 건너는 시간이 곧 점이 하나씩 찍히는 간격이다. 2.2초로는 세 점이 0.2초 만에 다 떠서
    /// 하나씩 찍히는 맛이 없었다. 느리게 지나가야 어색한 침묵도 같이 길어진다.
    /// </summary>
    [SerializeField] private float crowCrossSeconds = 6f;
    [SerializeField] private float crowFlapSeconds = 0.13f;

    /// <summary>
    /// 까악 사이의 최소 간격(초). 부리가 벌어지는 칸마다 울면 0.5초에 한 번씩 열한 번을 운다.
    /// 이 간격을 두면 지나가는 동안 서너 번, "까악 까악 까악" 이 된다.
    /// </summary>
    [SerializeField] private float cawGapSeconds = 1.4f;

    /// <summary>
    /// 까마귀가 지나가는 높이. 점과 같은 줄이라야 "점 위를 톡톡 짚고 간다"가 된다.
    /// 빌더의 DotY 와 맞춘다.
    /// </summary>
    [SerializeField] private float crowHeight = 188f;

    /// <summary>까마귀가 지나간 뒤 한 박자 더 두는 시간.</summary>
    [SerializeField] private float silenceTailSeconds = 0.5f;


    [Header("모양")]
    /// <summary>클로즈업 배율. 정수만 쓴다 — 소수 배율은 픽셀을 반칸에 걸치게 한다.</summary>
    /// 손님 그림이 300칸으로 커지면서 3배로는 얼굴이 화면을 한참 넘어섰다. 2배가 알맞다.
    [SerializeField] private int closeUpScale = 2;

    /// <summary>
    /// 다 들어왔을 때 검은 바 하나의 높이(칸). 위아래가 같은 높이로 들어온다.
    ///
    /// 몸통 잘린 끝을 따로 재지 않는다. 1배일 때는 카운터가 그 끝을 가려 주고,
    /// 확대하고 나면 끝이 화면 아래로 한참 내려가 어차피 안 보인다.
    /// 그래서 이 값은 "얼마나 조일까"라는 구도 문제만 정하면 된다.
    /// </summary>
    [SerializeField] private float barHeight = 110f;

    /// <summary>
    /// 우주가 덮인 뒤 바를 여기까지 되돌린다. 다 조인 채로 두면 우주가 띠만큼만 보인다.
    /// 조였을 때보다 크면 조인 높이를 그대로 쓴다(더 벌리지는 않는다).
    /// </summary>
    [SerializeField] private float barRelaxHeight = 40f;

    /// <summary>
    /// 손님 그림 안에서 눈이 있는 높이(0이면 그림 아래끝, 1이면 위끝).
    /// 확대할 때 여기를 바 사이 창의 한가운데에 둔다.
    ///
    /// 그림이 얼굴만이 아니라 가슴까지 든 흉상이라 0.5 로 두면 목이 한가운데에 온다.
    /// 값을 키우면 얼굴이 아래로 내려가고, 줄이면 위로 올라간다.
    /// </summary>
    [SerializeField] private float eyeHeightRatio = 0.58f;

    /// <summary>바가 들어오고 걷히는 데 걸리는 시간(초).</summary>
    [SerializeField] private float barSeconds = 0.3f;

    /// <summary>번개가 칠 때 흔들리는 폭(칸). 정수만 쓴다.</summary>
    [SerializeField] private int shakePixels = 2;

    /// <summary>번개가 몇 번 치는가. "콰가강 가강" 정도면 된다.</summary>
    [SerializeField] private int shakeHits = 2;

    /// <summary>흔든 자세 하나를 쥐고 있는 시간(초). 매 프레임 떨면 지직거려 픽셀이 뭉개져 보인다.</summary>
    [SerializeField] private float shakeHoldSeconds = 0.11f;

    /// <summary>번개가 칠 때 화면이 하얘지는 세기. 1이면 아무것도 안 보인다.</summary>
    [SerializeField] private float flashAlpha = 0.45f;

    /// <summary>따봉이 튀어나오는 한 단의 길이(초).</summary>
    [SerializeField] private float thumbPopStepSeconds = 0.06f;

    /// <summary>따봉과 함께 도는 반짝임 개수.</summary>
    [SerializeField] private int sparkleCount = 14;

    /// <summary>연출 전 손님 자리. 끝나면 여기로 돌려놓는다.</summary>
    private Vector2 homePosition;
    private Vector3 homeScale;

    /// <summary>손님 앞 그릇의 제자리. 국물을 마실 때만 여기서 벗어난다.</summary>
    private Vector2 bowlHomePosition;

    /// <summary>땀방울이 맺히는 자리. 흘러내린 뒤 여기로 되돌린다.</summary>
    private Vector2 sweatHomePosition;

    /// <summary>지금 확대 배율. 바가 움직일 때 이 배율로 자리를 다시 잡는다.</summary>
    private int currentScale = 1;

    /// <summary>우주가 지금까지 돌아간 각. 손님이 바뀔 때 0으로 돌린다.</summary>
    private float cosmosAngle;

    /// <summary>마지막 컷에서 "다음"을 기다리는 중인가. 그동안은 눌러도 건너뛰지 않는다.</summary>
    private bool waitingForNext;

    private Image[] sparkles;

    private void Awake()
    {
        if (customerSlot != null)
        {
            homePosition = customerSlot.anchoredPosition;
            homeScale = customerSlot.localScale;
        }

        // 그릇 제자리. 들이켜다 건너뛰면 들린 채로 남으므로 여기서 기억해 둔다.
        if (customerBowl != null) bowlHomePosition = customerBowl.anchoredPosition;
        if (sweat != null) sweatHomePosition = sweat.rectTransform.anchoredPosition;

        ResetStage();
    }

    /// <summary>
    /// 우주가 떠 있는 동안 아주 천천히 돌린다.
    ///
    /// 그림을 돌려도 안 뭉개진다 — 별을 2x2로 찍고 판을 네모로 뽑아 두었다(Tools/makeart.py).
    /// 여기만 예외로 런타임 회전을 쓴다. 돌아가는 각을 그림으로 구우려면 한 바퀴에 수십 장이 필요하다.
    /// </summary>
    private void Update()
    {
        if (cosmos == null || !cosmos.enabled) return;

        cosmosAngle += cosmosSpinPerSecond * Time.unscaledDeltaTime;
        cosmos.rectTransform.localRotation = Quaternion.Euler(0f, 0f, cosmosAngle);
    }

    /// <summary>
    /// 차례로 보여 준다. 부르는 쪽(GameManager)이 코루틴을 쥔다 —
    /// 화면이 꺼져도 연출이 중간에 끊기지 않게 하기 위해서다.
    ///
    /// 아무 데나 누르면 건너뛴다. 손님마다 15초씩 같은 연출을 다시 보는 것은 지루하다.
    /// 건너뛰면 여기서 바로 끝나고, 부르는 쪽이 이어서 결과창을 띄운다.
    ///
    /// 연출 본체를 따로 돌리고 여기서는 누름만 지켜본다. 단계마다 "건너뛰었나"를 묻는 것보다
    /// 코루틴을 통째로 멈추는 편이 확실하다. 중간에 무엇을 켜 두었든 ResetStage 가 정리한다.
    /// </summary>
    public IEnumerator Play(float accuracy)
    {
        // 이 오브젝트가 꺼져 있으면 여기서 코루틴을 시작할 수 없다. 그때는 예전처럼
        // 부르는 쪽 코루틴 위에서 그대로 돌린다. 대신 건너뛰기는 안 된다.
        if (!gameObject.activeInHierarchy)
        {
            yield return Sequence(accuracy);
            ResetStage();
            yield break;
        }

        playing = true;
        Coroutine body = StartCoroutine(Sequence(accuracy));
        bool skipped = false;

        while (playing)
        {
            // 마지막 컷에서는 누르는 것이 "건너뛰기"가 아니라 "다음"이다. 본체가 직접 받는다.
            if (!skipped && !waitingForNext && WantsSkip())
            {
                // 연출만 건너뛴다. 여기서 통째로 끝내면 손님이 소감 한 마디 없이 사라진다.
                StopCoroutine(body);
                if (sparkling != null) StopCoroutine(sparkling);
                sparkling = null;
                skipped = true;
                body = StartCoroutine(SkipToFinish(accuracy));
            }

            yield return null;
        }

        playing = false;
        sparkling = null;
        ResetStage();
    }

    /// <summary>누르는 순간 건너뛴다. 뗄 때가 아니라 누를 때라야 반응이 빠르다.</summary>
    private static bool WantsSkip()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;

        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    /// <summary>정확도 구간. 기획서 14장의 네 갈래다.</summary>
    private enum Reaction { Perfect, Good, Okay, Bad }

    /// <summary>
    /// 정확도(0~100)를 구간으로 옮긴다.
    ///
    /// Perfect 는 100 점인데 소수 오차로 99.999 가 들어오는 일이 있어 조금 낮춰 잡는다.
    /// </summary>
    private Reaction ReactionOf(float accuracy)
    {
        if (accuracy >= perfectFrom) return Reaction.Perfect;
        if (accuracy >= goodFrom) return Reaction.Good;
        if (accuracy >= okayFrom) return Reaction.Okay;

        return Reaction.Bad;
    }

    /// <summary>
    /// 연출 본체. 다 돌면 playing 을 내려 Play 가 빠져나가게 한다.
    ///
    /// 앞은 넷이 같다 — 판을 치우고 국물을 마신다. 거기서 정확도에 따라 갈린다.
    /// 뒤도 넷이 같다 — 판을 되돌리고 소감을 친다.
    ///
    /// 갈래는 "센 것과 약한 것"이 아니라 **서로 다른 연출**이다. Good 에 확대를 넣으면
    /// Perfect 의 축소판으로 보여서 둘이 안 갈린다. 화면이 난리가 나느냐 아니냐로 가른다.
    /// </summary>
    private IEnumerator Sequence(float accuracy)
    {
        ResetStage();

        // 연출이 시작되는 순간부터 조리 화면 판을 치운다. 바가 들어올 때까지 기다리면
        // 그동안 그릇이 손님 얼굴을 덮는다 — 그릇은 튜토리얼 때문에 Canvas 가 따로 얹혀 있어
        // 계층 순서와 무관하게 주문 화면 위로 올라온다(RamenLayoutBuilder 의 LiftCanvas).
        ShowGameUI(false);

        // 말풍선은 연출 내내 꺼 둔다. 마지막 소감 때 FinishBeat 가 다시 켠다.
        if (orderScreen != null) orderScreen.ShowBubble(false);

        // 공통 — 국물을 마신다. 먹는 동안에는 눈을 감겨 둔다.
        if (customerAppearance != null) customerAppearance.CloseEyes();
        if (sfx != null) sfx.Slurp(slurpSeconds);
        yield return Sip();

        switch (ReactionOf(accuracy))
        {
            case Reaction.Perfect: yield return Moved();   break;
            case Reaction.Good:    yield return Pleased(); break;
            case Reaction.Okay:    yield return Puzzled(); break;
            default:               yield return Silent();  break;
        }

        // 가게로 다 돌아왔으니 감춰 둔 판을 바로 되돌린다. 대사를 치는 동안에도 화면은
        // 평소 모습이어야 한다 — 마지막까지 숨겨 두면 "아직 연출 중"으로 보인다.
        if (customerAppearance != null) customerAppearance.ReleaseFrame();
        ShowGameUI(true);

        yield return FinishBeat(accuracy);
        playing = false;
    }

    /// <summary>
    /// Perfect — 감동. 화면이 통째로 난리가 난다.
    /// 어둡게 덮고 눈을 클로즈업한 뒤, 감은 눈이 번쩍 뜨이는 순간 번개가 친다.
    /// </summary>
    private IEnumerator Moved()
    {
        // 2 — 시네마틱 바가 먼저 들어온다. 확대보다 먼저여야 "장면이 바뀐다"는 신호가 되고,
        // 확대하는 동안 몸통 잘린 끝이 드러나는 것도 미리 막는다.
        //
        // 위아래가 같은 높이로 들어온다. 한쪽만 올라오면 시네마틱이 아니라 화면이 잘린 것으로 보인다.
        Vector2 closed = new Vector2(barHeight, barHeight);
        yield return MoveBars(Vector2.zero, closed, barSeconds);

        // 3 — 가게를 어둡게 덮는다. 확대하기 전에 주변을 지워야 얼굴만 남는다.
        // 검은 판이 손님보다 뒤에 있어 얼굴은 밝은 채로 도드라진다.
        yield return FadeDim(0f, dimAlpha, dimSeconds);

        // 확대에 들어가기 직전에 손님 앞 그릇을 치운다. 얼굴만 남아야 한다.
        Show(hiddenAtZoom, false);

        // 4 — 천천히 클로즈업. 정수배로 단을 밟아 올라가고, 눈높이가 화면 한가운데에 온다.
        for (int step = 2; step <= Mathf.Max(2, closeUpScale); step++)
        {
            Zoom(step);
            yield return new WaitForSecondsRealtime(zoomStepSeconds);
        }

        // 5 — 감은 눈이 번쩍 뜨인다. 이 연출에서 표정이 바뀌는 유일한 한 컷이다.
        //
        // 손님 그림에 쓸 수 있는 표정이 뜬 눈·감은 눈 둘뿐이라, 오래 감겨 두었다가 한 번에
        // 뜨는 것이 낼 수 있는 가장 센 한 방이다. 뜨자마자 번개가 쳐야 둘이 한 사건이 된다.
        if (customerAppearance != null) customerAppearance.OpenEyes();
        yield return new WaitForSecondsRealtime(eyeOpenSeconds);

        // 6 — 번개가 한 방.
        if (sfx != null) sfx.Thunder();
        yield return Strike(boltSeconds);

        // 5 — 가게가 우주로 녹아든다. 우주가 손님보다 뒤에 깔려 있어 알파만 올리면
        // 뒷배경만 갈린다. 손님은 그대로 앞에 남는다.
        yield return FadeCosmos(0f, 1f, cosmosFadeSeconds);

        // 6 — 바를 푼다. 다 조인 채로 두면 우주가 띠만큼만 보인다.
        // 이때는 확대 상태라 몸통 아래끝이 화면 밖으로 한참 내려가 있어서, 아래 바를 내려도
        // 잘린 자리가 드러나지 않는다.
        Vector2 relaxed = new Vector2(Mathf.Min(closed.x, barRelaxHeight),
                                      Mathf.Min(closed.y, barRelaxHeight));
        yield return MoveBars(closed, relaxed, barSeconds);

        // 녹아드는 데 쓴 시간만큼 빼서 전체 박자를 예전과 비슷하게 둔다.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, cosmosSeconds - cosmosFadeSeconds));

        // 7 — 감동 오우라. 다 그려지면 천천히 스러진다.
        yield return PlayAura(auraSeconds);

        // 오우라가 사라진 자리를 잠깐 그대로 둔다. 여기가 이 연출의 여운이다.
        yield return new WaitForSecondsRealtime(cosmosHoldSeconds);

        // 8 — 가게로 돌아온다. 확대를 먼저 풀고 그 다음에 우주를 녹여 낸다.
        // 확대한 채로 배경만 갈리면 "가게로 돌아왔다"가 아니라 "배경만 바뀌었다"로 읽힌다.
        Zoom(1);
        yield return FadeCosmos(1f, 0f, cosmosOutSeconds);
        yield return MoveBars(relaxed, Vector2.zero, barSeconds);
    }

    /// <summary>
    /// Good — 만족. 눈은 감은 채로 둔다. 그대로 두면 미소로 읽힌다.
    ///
    /// 확대도 우주도 없다. 바를 얕게 한 번 물었다 풀고 얼굴 둘레에 반짝임만 돈다.
    /// Perfect 의 약한 판이 아니라 "잔잔하게 좋다"는 딴 장면이어야 둘이 갈린다.
    /// </summary>
    private IEnumerator Pleased()
    {
        Vector2 closed = new Vector2(goodBarHeight, goodBarHeight);
        yield return MoveBars(Vector2.zero, closed, barSeconds);

        yield return Sparkle(goodSparkleSeconds);
        yield return MoveBars(closed, Vector2.zero, barSeconds);
    }

    /// <summary>
    /// Okay — 갸웃. 먹다 말고 눈을 뜨고, 관자놀이에 땀이 맺힌다.
    ///
    /// 뜬 눈은 Perfect 의 "번쩍" 과 같은 장이다. 옆에 땀이 붙느냐 번개가 치느냐로 갈린다 —
    /// 표정이 아니라 주변이 감정을 만든다.
    /// </summary>
    private IEnumerator Puzzled()
    {
        if (customerAppearance != null) customerAppearance.OpenEyes();
        yield return new WaitForSecondsRealtime(puzzledPauseSeconds);

        yield return PlaySweat();
        yield return new WaitForSecondsRealtime(puzzledPauseSeconds);
    }

    /// <summary>
    /// Bad — 침묵. 아무 일도 안 일어나는 것이 연출이다.
    ///
    /// 바도 확대도 없다. 앞의 셋이 전부 "화면이 뭔가 해 주는" 장면이라, 여기서만 그걸 안 하면
    /// 그 자체로 무겁다. 살짝 어둡게 깔고 머리 위로 까마귀가 점을 떨구고 지나간다.
    ///
    /// 채도를 빼면 더 좋겠지만 유니티 UI 색은 곱셈이라 어둡게밖에 안 된다.
    /// 흑백으로 하려면 손님 그림을 흑백으로 구워야 해서 지금은 어둡게로 간다.
    /// </summary>
    private IEnumerator Silent()
    {
        if (customerAppearance != null) customerAppearance.OpenEyes();

        yield return FadeDim(0f, badDimAlpha, dimSeconds);
        yield return PlayAwkward();
        yield return FadeDim(badDimAlpha, 0f, dimSeconds);
    }

    /// <summary>
    /// 국물을 마신다. 그릇을 입 앞까지 들어 올려 몇 번 까딱하고 도로 내려놓는다.
    ///
    /// 손님 그림은 눈 감은 장과 뜬 장 둘뿐이라 입을 벌리게 할 수 없다. 그래서 **그릇이
    /// 입을 가린다.** 가려 놓으면 표정이 없어도 마시는 것으로 읽히고, 그림을 새로 그릴 일도 없다.
    ///
    /// 움직이는 것은 그릇 높이와 손님 자리 높이 둘뿐이고 둘 다 정수 칸이라,
    /// 배율이 정수로 고정된 이 화면에서 획이 반칸에 걸리지 않는다.
    /// </summary>
    private IEnumerator Sip()
    {
        if (customerBowl == null)
        {
            // 그릇이 안 꽂혔으면 예전처럼 소리만 내고 지나간다.
            yield return new WaitForSecondsRealtime(slurpSeconds);
            yield break;
        }

        // 제자리는 Awake 에서 기억해 둔 값을 쓴다. 지금 자리를 읽으면, 앞선 연출이 중간에
        // 끊겨 들린 채로 남았을 때 그 자리를 제자리로 알고 거기서 또 들어 올린다.
        Vector2 bowlHome = bowlHomePosition;
        Vector2 slotHome = homePosition;

        float up = bowlHome.y + sipRise;

        yield return MoveY(customerBowl, bowlHome.y, up, sipRiseSeconds);

        // 고개는 마시는 내내 숙인 채로 둔다. 까딱마다 들었다 놓으면 고개를 끄덕이는 것처럼 보인다.
        // 실제로 그릇째 들이켤 때 움직이는 건 그릇이지 고개가 아니다.
        if (customerSlot != null) customerSlot.anchoredPosition = slotHome - new Vector2(0f, sipNodPixels);
        yield return new WaitForSecondsRealtime(sipHoldSeconds);

        for (int i = 0; i < sipCount; i++)
        {
            yield return MoveY(customerBowl, up, up + sipTipPixels, sipTipSeconds * 0.35f);
            yield return new WaitForSecondsRealtime(sipTipSeconds * 0.15f);
            yield return MoveY(customerBowl, up + sipTipPixels, up, sipTipSeconds * 0.35f);

            // 한 모금 사이의 숨. 없으면 세 번이 한 번의 떨림으로 뭉친다.
            yield return new WaitForSecondsRealtime(sipTipSeconds * 0.15f);
        }

        if (customerSlot != null) customerSlot.anchoredPosition = slotHome;
        yield return new WaitForSecondsRealtime(sipHoldSeconds);
        yield return MoveY(customerBowl, up, bowlHome.y, sipDownSeconds);

        customerBowl.anchoredPosition = bowlHome;
        if (customerSlot != null) customerSlot.anchoredPosition = slotHome;
    }

    /// <summary>
    /// 관자놀이에 땀이 맺혔다 흘러내린다. 갸웃(Okay) 구간에서 쓴다.
    ///
    /// 손님 그림에 표정이 눈 뜸·감음 둘뿐이라 "애매하다"를 얼굴로 못 낸다. 같은 눈 뜬 얼굴에
    /// 이걸 붙여 놓으면 갸웃으로 읽힌다 — 감정을 얼굴이 아니라 옆에 붙는 것으로 낸다.
    ///
    /// 맺힌 채로 한 박자 머무는 것이 중요하다. 곧바로 흘러내리면 그냥 물이 지나간 것이 된다.
    ///
    /// 아직 정확도 구간이 갈리지 않아 연출 본편에는 안 걸어 두었다. 구간이 생기면 Okay 에 붙인다.
    /// </summary>
    public IEnumerator PlaySweat()
    {
        if (sweat == null || sweatFrames == null || sweatFrames.Length < 2) yield break;

        sweat.rectTransform.anchoredPosition = sweatHomePosition;
        sweat.sprite = sweatFrames[0];
        sweat.enabled = true;

        yield return new WaitForSecondsRealtime(sweatBeadSeconds);

        sweat.sprite = sweatFrames[1];
        yield return MoveY(sweat.rectTransform, sweatHomePosition.y, sweatHomePosition.y - sweatFall,
                           sweatRunSeconds);

        sweat.enabled = false;
        sweat.rectTransform.anchoredPosition = sweatHomePosition;
    }

    /// <summary>
    /// 어색한 침묵 한 박자. 정적이 흐르고 `. . .` 이 뜨는 위로 까마귀가 까악 하며 지나간다.
    ///
    /// 셋이 한 덩어리다. 따로 떼면 아무것도 안 된다 — 정적 없이 까마귀만 지나가면 그냥
    /// 새가 날아간 것이고, 까마귀 없이 점만 찍히면 그냥 말이 없는 것이다.
    ///
    /// 까마귀는 손님보다 뒤에 그린다. 얼굴을 가리지 않고 몸 뒤로 사라졌다 나오는 편이
    /// 화면 앞으로 지나가는 것보다 자연스럽다.
    ///
    /// Bad(69점 이하) 구간에서 쓴다. 아직 정확도 구간이 갈리지 않아 본편에는 안 걸어 두었다.
    /// </summary>
    public IEnumerator PlayAwkward()
    {
        // 말풍선은 쓰지 않는다. 글자로 ". . ." 를 치면 손님이 그렇게 **말한** 것으로 읽힌다.
        // 침묵은 말이 아니므로 머리 위에 점으로 찍힌다.
        if (orderScreen != null) orderScreen.ShowBubble(false);
        HideDots();

        yield return new WaitForSecondsRealtime(silenceSeconds);

        if (crow != null && crowFrames != null && crowFrames.Length > 0)
        {
            // 화면 밖에서 들어와 반대편 밖으로 나간다. 판 절반에 넉넉히 한 칸 더 준다.
            float half = 0.5f * (crowSpan > 0f ? crowSpan : 960f);
            float from = -half - crow.rectTransform.sizeDelta.x;
            float to = half + crow.rectTransform.sizeDelta.x;

            crow.sprite = crowFrames[0];
            crow.enabled = true;

            float elapsed = 0f;
            float flap = 0f;
            int frame = 0;
            float lastCaw = -999f;

            while (elapsed < crowCrossSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                flap += Time.unscaledDeltaTime;

                // 날갯짓은 가는 거리와 무관하게 제 박자로 돈다.
                if (flap >= crowFlapSeconds)
                {
                    flap -= crowFlapSeconds;
                    frame = (frame + 1) % crowFrames.Length;
                    crow.sprite = crowFrames[frame];

                    // 부리가 활짝 벌어지는 칸에 맞춰 운다. 그림과 소리가 어긋나면 둘 다 죽는다.
                    if (frame == cawFrame && elapsed - lastCaw >= cawGapSeconds)
                    {
                        lastCaw = elapsed;
                        if (sfx != null) sfx.Caw();
                    }
                }

                float x = Mathf.Round(Mathf.Lerp(from, to, elapsed / crowCrossSeconds));
                crow.rectTransform.anchoredPosition = new Vector2(x, crowHeight);

                // 점을 지나칠 때마다 그 점이 톡 찍힌다. 까마귀가 점을 떨구고 가는 모양새다.
                TapDots(x);

                yield return null;
            }

            crow.enabled = false;
        }

        // 점 셋이 다 찍힌 채로 한 박자 남는다. 여기가 이 연출의 마지막 정적이다.

        yield return new WaitForSecondsRealtime(silenceTailSeconds);
    }

    /// <summary>부리가 활짝 벌어지는 칸. 그 칸에서 까악 소리를 낸다(시트 0 다묾 1 반쯤 2 까악 3 반쯤).</summary>
    [SerializeField] private int cawFrame = 2;

    /// <summary>
    /// 까마귀가 x 를 지났으면 그 자리의 점을 켠다.
    ///
    /// 점 자리를 코드에 적지 않고 빌더가 놓아 준 자리를 그대로 읽는다. 점 간격을 바꿔도
    /// 여기를 고칠 일이 없다. 까마귀 부리(오른쪽 끝)가 점을 지나는 순간에 찍히도록
    /// 폭의 절반을 더해서 잰다.
    /// </summary>
    private void TapDots(float crowX)
    {
        if (silenceDots == null) return;

        float beak = crowX + (crow != null ? crow.rectTransform.sizeDelta.x * 0.5f : 0f);

        foreach (var dot in silenceDots)
        {
            if (dot == null || dot.enabled) continue;
            if (beak < dot.rectTransform.anchoredPosition.x) continue;

            dot.enabled = true;
            if (sfx != null) sfx.Tick();
        }
    }

    private void HideDots()
    {
        if (silenceDots == null) return;

        foreach (var dot in silenceDots)
            if (dot != null) dot.enabled = false;
    }

    /// <summary>까마귀가 건너야 하는 폭. 판 크기에서 읽는다.</summary>
    private float crowSpan
    {
        get
        {
            var canvas = crow != null ? crow.canvas : null;
            return canvas == null ? 0f : ((RectTransform)canvas.transform).rect.width;
        }
    }

    /// <summary>높이만 부드럽게 옮긴다. 칸을 반올림해 반칸에 걸리지 않게 한다.</summary>
    private IEnumerator MoveY(RectTransform rect, float from, float to, float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(elapsed / seconds);
            k = k * k * (3f - 2f * k);       // 시작과 끝이 느리다

            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                                                Mathf.Round(Mathf.Lerp(from, to, k)));
            yield return null;
        }

        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, to);
    }

    /// <summary>
    /// 마지막 컷 — 소감을 치고, 누르면 따봉을 보여 주고 끝난다.
    ///
    /// 연출 본체와 건너뛰기가 같이 쓴다. 건너뛰어도 손님 소감은 들어야 하므로,
    /// 건너뛰기는 "끝내기"가 아니라 "여기로 건너뛰기"다.
    /// </summary>
    private IEnumerator FinishBeat(float accuracy)
    {
        // 여기서부터 끝까지 누르는 것은 "건너뛰기"가 아니라 "다음"이다.
        waitingForNext = true;

        if (orderScreen != null)
        {
            // 말풍선용 긴 소감. 결과창은 따로 짧은 말을 쓰므로 여기서 뽑아 둔 말은 건드리지 않는다.
            string line = ReactionLines.Cutscene(accuracy);
            if (line == null) line = ReactionLine(accuracy);

            orderScreen.ShowBubble(true);
            orderScreen.StartTypingBubble(line);

            // 찍는 도중에 누르면 마저 기다리지 않고 한 번에 보여 준다.
            while (orderScreen.IsBubbleTyping)
            {
                if (WantsSkip()) orderScreen.FinishBubbleLine();
                yield return null;
            }
        }

        // 다 찍었으면 누를 때까지 기다린다.
        yield return WaitForNextPress();
        waitingForNext = false;

        // 따봉이 튀어나왔다가 사라진다. 그 뒤에 정확도 창이 뜬다(부르는 쪽이 연다).
        if (gameObject.activeInHierarchy) sparkling = StartCoroutine(Sparkle(thumbSeconds));
        yield return PopThumb();
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, thumbSeconds - thumbPopStepSeconds * 3f));

        if (thumb != null) thumb.enabled = false;
        if (sparkling != null) StopCoroutine(sparkling);
        sparkling = null;
    }

    /// <summary>건너뛰었을 때. 화면을 제자리로 되돌리고 마지막 컷만 보여 준다.</summary>
    private IEnumerator SkipToFinish(float accuracy)
    {
        ResetStage();
        ShowGameUI(true);

        yield return FinishBeat(accuracy);
        playing = false;
    }

    /// <summary>
    /// 다음으로 넘기는 입력을 기다린다.
    ///
    /// 한 프레임 흘려보내고 시작한다. 대사를 마저 찍으려고 누른 그 입력이 그대로
    /// "다음"으로 읽히면, 따봉이 뜨자마자 지나가 버린다.
    /// </summary>
    private IEnumerator WaitForNextPress()
    {
        yield return null;

        while (!WantsSkip()) yield return null;
    }

    /// <summary>연출용 판을 전부 끄고 손님을 제자리로 돌린다.</summary>
    private void ResetStage()
    {
        waitingForNext = false;
        ShowGameUI(true);
        SetBars(0f, 0f);
        SetFlash(0f);
        SetDim(0f);
        SetCosmos(false);

        // 들이켜다 건너뛰면 그릇이 입 앞에 뜬 채로 남는다.
        if (customerBowl != null) customerBowl.anchoredPosition = bowlHomePosition;

        if (sweat != null)
        {
            sweat.enabled = false;
            sweat.rectTransform.anchoredPosition = sweatHomePosition;
        }

        if (crow != null) crow.enabled = false;
        HideDots();

        if (bolt != null) bolt.enabled = false;
        if (aura != null) aura.enabled = false;
        HideSparkles();
        if (thumb != null)
        {
            thumb.enabled = false;

            // 튀어나오다 건너뛰면 부푼 채로 남는다. 뒤집은 부호는 지키고 크기만 되돌린다.
            Vector3 scale = thumb.rectTransform.localScale;
            thumb.rectTransform.localScale = new Vector3(Mathf.Sign(scale.x), 1f, 1f);
        }

        Zoom(1);
    }

    /// <summary>
    /// 얼굴이 화면 한가운데에 오도록 손님 자리를 정수배로 키운다.
    ///
    /// 자리를 그냥 키우면 자리 한가운데를 기준으로 커져서 얼굴이 화면 위로 빠져나간다.
    /// 얼굴이 자리 안 어디에 있는지 재서, 커진 만큼 자리를 반대로 밀어 준다.
    /// </summary>
    private void Zoom(int scale)
    {
        // 말풍선도 같은 배율로 키운다. 손님만 커지면 말풍선이 혼자 작게 남아 따로 논다.
        // 피벗이 왼쪽 위라 오른쪽·아래로만 자라고, 글에 맞춰 줄어 있어서 3배로도 화면을 안 넘는다.
        ZoomBubble(scale);

        currentScale = scale;
        ApplyFraming();
    }

    /// <summary>
    /// 지금 배율과 지금 바 높이에 맞춰 손님을 앉힌다.
    ///
    /// 눈을 화면 한가운데가 아니라 <b>바 사이 보이는 창의 한가운데</b>에 둔다.
    /// 아래 바가 배꼽까지 올라오므로 화면 한가운데에 맞추면 얼굴 아랫부분이 바에 깔린다.
    ///
    /// 바가 움직일 때마다 다시 부르기 때문에, 바를 푸는 동안 얼굴이 따라 내려온다.
    /// </summary>
    private void ApplyFraming()
    {
        if (customerSlot == null) return;

        if (currentScale <= 1)
        {
            customerSlot.localScale = homeScale;
            customerSlot.anchoredPosition = homePosition;
            return;
        }

        Vector2 eye = HeadCenterInSlot();
        customerSlot.localScale = new Vector3(currentScale, currentScale, 1f);
        customerSlot.anchoredPosition = new Vector2(
            Mathf.Round(-eye.x * currentScale),
            Mathf.Round(-eye.y * currentScale + VisibleBandCenter()));
    }

    /// <summary>
    /// 연출과 상관없는 판을 껐다 켠다. ResetStage 가 늘 다시 켜 주므로,
    /// 건너뛰든 중간에 멈추든 상단바가 사라진 채로 남지 않는다.
    /// </summary>
    private void ShowGameUI(bool show)
    {
        Show(hiddenDuringCut, show);
        if (show) Show(hiddenAtZoom, true);
    }

    private static void Show(GameObject[] targets, bool show)
    {
        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) targets[i].SetActive(show);
        }
    }

    /// <summary>위아래 바를 빼고 남은 창의 한가운데가 화면 어디인가(칸).</summary>
    private float VisibleBandCenter()
    {
        var root = transform as RectTransform;
        if (root == null) return 0f;

        float half = root.rect.height * 0.5f;
        float top = half - (topBar != null ? topBar.sizeDelta.y : 0f);
        float bottom = -half + (bottomBar != null ? bottomBar.sizeDelta.y : 0f);
        return (top + bottom) * 0.5f;
    }

    private void ZoomBubble(int scale)
    {
        if (orderScreen == null) return;

        RectTransform bubble = orderScreen.Bubble;
        if (bubble == null) return;

        float k = Mathf.Max(1, scale);
        bubble.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>자리 한가운데를 (0,0)으로 봤을 때 눈높이가 어디인가.</summary>
    private Vector2 HeadCenterInSlot()
    {
        if (customerHead == null || customerSlot == null) return Vector2.zero;

        // 얼굴은 자리 아래변을 기준으로 얹혀 있다(CustomerAppearance.SetHead, 피벗 0.5/0).
        float bottom = -customerSlot.rect.height * 0.5f;
        return new Vector2(customerHead.anchoredPosition.x,
                           bottom + customerHead.anchoredPosition.y
                                  + customerHead.rect.height * Mathf.Clamp01(eyeHeightRatio));
    }

    /// <summary>x 가 위 바, y 가 아래 바 높이다. 둘이 다르게 움직인다.</summary>
    private IEnumerator MoveBars(Vector2 from, Vector2 to, float seconds)
    {
        if (seconds <= 0f)
        {
            SetBars(to.x, to.y);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / seconds);
            SetBars(Mathf.Lerp(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k));
            yield return null;
        }

        SetBars(to.x, to.y);
    }

    /// <summary>
    /// 검은 바 높이를 정수 칸으로 맞춘다. 반칸이면 경계에 회색 줄이 낀다.
    ///
    /// 위아래를 따로 받는다. 아래는 몸통 잘린 자리를 덮어야 해서 훨씬 많이 올라오고,
    /// 위는 얼굴이 눌리지 않게 평범한 시네마틱 높이로 둔다.
    /// </summary>
    private void SetBars(float top, float bottom)
    {
        if (topBar != null)
            topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, Mathf.Max(0f, Mathf.Round(top)));

        if (bottomBar != null)
            bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, Mathf.Max(0f, Mathf.Round(bottom)));

        // 창이 달라졌으니 얼굴 자리도 다시 잡는다. 안 하면 바를 푸는 동안 얼굴이 위에 남는다.
        ApplyFraming();
    }

    /// <summary>
    /// 번개 한 방. 심장이 한 번 뛰듯 세게 번쩍이고 잦아든다.
    ///
    /// 여러 줄기를 흩뿌리지 않는다. 굵은 것 하나가 화면을 가로지르는 편이 훨씬 세게 보인다.
    /// 방향(11시 → 5시)은 그림에 이미 구워져 있어 돌리지 않는다.
    /// </summary>
    private IEnumerator Strike(float seconds)
    {
        Vector2 home = customerSlot != null ? customerSlot.anchoredPosition : Vector2.zero;
        int hits = Mathf.Max(1, shakeHits);
        float beat = seconds / hits;

        for (int i = 0; i < hits; i++)
        {
            // 뒤로 갈수록 약해진다. 콰가강 — 가강.
            float power = 1f - i / (float)hits;
            int amplitude = Mathf.Max(1, Mathf.RoundToInt(shakePixels * power));

            // 한 자세를 쥐었다가 반대로 한 번, 그리고 제자리. 매 프레임 난수로 떨면
            // 지직거리기만 하고 "한 방 맞았다"가 안 읽힌다. 칸은 정수로만 움직인다.
            //
            // 번쩍임은 첫 자세에서만 세게 주고 곧바로 뺀다. 자세를 쥐는 동안 계속 켜 두면
            // 0.3초 내내 하얀 판이 깔려 화면이 통째로 날아간다.
            if (bolt != null) bolt.enabled = true;
            SetFlash(flashAlpha * power);
            yield return Hold(home, new Vector2(amplitude, -amplitude));

            SetFlash(flashAlpha * power * 0.35f);
            yield return Hold(home, new Vector2(-amplitude, amplitude));

            if (bolt != null) bolt.enabled = false;
            SetFlash(0f);
            yield return Hold(home, Vector2.zero);

            // 다음 한 방까지 쉰다. 이 사이가 있어야 두 방으로 들린다.
            float rest = beat - shakeHoldSeconds * 3f;
            if (rest > 0f) yield return new WaitForSecondsRealtime(rest);
        }

        if (customerSlot != null) customerSlot.anchoredPosition = home;
        if (bolt != null) bolt.enabled = false;
        SetFlash(0f);
    }

    /// <summary>
    /// 따봉이 튀어나온다. 작게 나왔다가 크게 부풀고 제자리로 앉는다.
    ///
    /// 배율은 정수만 밟는다 — 1.4배 같은 중간 값을 거치면 픽셀이 반칸에 걸려 뭉개진다.
    /// 그림이 오른손이라 x 를 음수로 뒤집어 둔 상태이므로, 키울 때도 그 부호를 지켜야 한다.
    /// </summary>
    private IEnumerator PopThumb()
    {
        if (thumb == null) yield break;

        RectTransform rect = thumb.rectTransform;
        float flip = Mathf.Sign(rect.localScale.x);
        thumb.enabled = true;

        int[] steps = { 1, 2, 1 };
        foreach (int step in steps)
        {
            rect.localScale = new Vector3(flip * step, step, 1f);
            yield return new WaitForSecondsRealtime(thumbPopStepSeconds);
        }
    }

    /// <summary>흔든 자세 하나를 shakeHoldSeconds 동안 쥔다.</summary>
    private IEnumerator Hold(Vector2 home, Vector2 offset)
    {
        if (customerSlot != null) customerSlot.anchoredPosition = home + offset;
        yield return new WaitForSecondsRealtime(shakeHoldSeconds);
    }

    /// <summary>
    /// 구워 둔 프레임을 차례로 넘긴다.
    /// 고리 하나를 코드로 키우면 배율이 소수가 되어 픽셀이 깨지므로 그림으로 퍼뜨린다.
    /// </summary>
    private IEnumerator PlayAura(float seconds)
    {
        if (aura == null || auraFrames == null || auraFrames.Length == 0)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        aura.enabled = true;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed / seconds * auraFrames.Length),
                                    0, auraFrames.Length - 1);
            aura.sprite = auraFrames[frame];

            yield return null;
        }

        // 마지막 그림을 쥔 채 천천히 스러진다. 그냥 끄면 원이 툭 없어져 여운이 안 남는다.
        Color tint = aura.color;
        float fade = 0f;

        while (fade < auraFadeSeconds)
        {
            fade += Time.unscaledDeltaTime;
            tint.a = 1f - Mathf.Clamp01(fade / auraFadeSeconds);
            aura.color = tint;
            yield return null;
        }

        aura.enabled = false;

        // 다음 손님을 위해 진하기를 되돌린다. 안 하면 두 번째부터 투명한 채로 뜬다.
        tint.a = 1f;
        aura.color = tint;
    }

    /// <summary>연출이 도는 중인가. 본체가 다 돌거나 건너뛰면 내려간다.</summary>
    private bool playing;

    /// <summary>따봉 반짝임. 본체와 따로 도는 것이라 건너뛸 때 같이 멈춰야 한다.</summary>
    private Coroutine sparkling;

    private void SetCosmos(bool on)
    {
        if (cosmos == null) return;

        cosmos.enabled = on;
        SetCosmosAlpha(on ? 1f : 0f);

        if (!on)
        {
            cosmosAngle = 0f;
            cosmos.rectTransform.localRotation = Quaternion.identity;
        }
    }

    private void SetDim(float alpha)
    {
        if (dim == null) return;

        float a = Mathf.Clamp01(alpha);
        Color color = dim.color;
        color.a = a;
        dim.color = color;
        dim.enabled = a > 0f;
    }

    /// <summary>가게를 검게 덮는다. 확대 직전에 한 번 쓰고, 우주가 들어올 때 같이 걷힌다.</summary>
    private IEnumerator FadeDim(float from, float to, float seconds)
    {
        if (dim == null) yield break;

        SetDim(from);
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetDim(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }

        SetDim(to);
    }

    private void SetCosmosAlpha(float alpha)
    {
        if (cosmos == null) return;

        Color color = cosmos.color;
        color.a = Mathf.Clamp01(alpha);
        cosmos.color = color;
    }

    /// <summary>
    /// 가게와 우주를 겹쳐 녹인다. 알파만 움직이므로 픽셀은 안 깨진다 —
    /// 크기나 자리를 건드리는 전환이면 정수배를 벗어나 획이 뭉개진다.
    /// </summary>
    private IEnumerator FadeCosmos(float from, float to, float seconds)
    {
        if (cosmos == null) yield break;

        cosmos.enabled = true;
        SetCosmosAlpha(from);

        // 들어올 때는 덮어 둔 검은 판을 같이 걷는다. 남겨 두면 우주까지 어두워진다.
        float dimFrom = dim != null ? dim.color.a : 0f;

        if (seconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / seconds);
                SetCosmosAlpha(Mathf.Lerp(from, to, k));
                if (to > from) SetDim(Mathf.Lerp(dimFrom, 0f, k));
                yield return null;
            }
        }

        SetCosmosAlpha(to);
        if (to > from) SetDim(0f);

        // 다 스러졌으면 꺼 둔다. 투명한 판이 남아 있어도 손해는 없지만, 켜진 채로 두면
        // 다음 손님 차례에 ResetStage 가 지나가기 전까지 상태가 헷갈린다.
        if (to <= 0f) cosmos.enabled = false;
    }

    private void SetFlash(float alpha)
    {
        if (flash == null) return;

        float a = Mathf.Clamp01(alpha);
        Color color = flash.color;
        color.a = a;
        flash.color = color;
        flash.enabled = a > 0f;
    }

    /// <summary>
    /// 4컷 반짝임. 나비·꽃 그림이 없어 흰 점으로 대신한다.
    /// Image 에 그림을 안 넣으면 흰 사각형이 그려지므로 그림 없이 만들 수 있다.
    /// </summary>
    private IEnumerator Sparkle(float seconds)
    {
        if (sparkleRoot == null) yield break;

        EnsureSparkles();

        var origin = new Vector2[sparkles.Length];
        var delay = new float[sparkles.Length];

        for (int i = 0; i < sparkles.Length; i++)
        {
            origin[i] = new Vector2(Random.Range(-46, 47), Random.Range(-40, 21));
            delay[i] = Random.Range(0f, seconds * 0.5f);
            sparkles[i].enabled = false;
        }

        float life = Mathf.Max(0.01f, seconds * 0.5f);
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < sparkles.Length; i++)
            {
                float t = (elapsed - delay[i]) / life;
                if (t < 0f || t > 1f)
                {
                    sparkles[i].enabled = false;
                    continue;
                }

                sparkles[i].enabled = true;

                // 위로 떠오르며 스러진다. 자리는 정수 칸으로 맞춘다.
                sparkles[i].rectTransform.anchoredPosition =
                    new Vector2(origin[i].x, Mathf.Round(origin[i].y + t * 18f));

                sparkles[i].color = new Color(1f, 1f, 1f, 1f - t);
            }

            yield return null;
        }

        for (int i = 0; i < sparkles.Length; i++) sparkles[i].enabled = false;
    }

    /// <summary>
    /// 반짝임 조각을 전부 끈다.
    ///
    /// Sparkle 은 자기 루프를 끝까지 돌아야 스스로 끈다. 건너뛰거나 중간에 멈추면
    /// 그때 켜져 있던 조각이 화면에 그대로 박힌 채 남는다. 그래서 정리는 여기서 한 번 더 한다.
    /// </summary>
    private void HideSparkles()
    {
        if (sparkles == null) return;

        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkles[i] != null) sparkles[i].enabled = false;
        }
    }

    /// <summary>반짝임 조각을 처음 쓸 때 한 번 만들어 두고 돌려 쓴다.</summary>
    private void EnsureSparkles()
    {
        if (sparkles != null) return;

        sparkles = new Image[Mathf.Max(0, sparkleCount)];
        for (int i = 0; i < sparkles.Length; i++)
        {
            var go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));

            var rect = (RectTransform)go.transform;
            rect.SetParent(sparkleRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 2f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            sparkles[i] = image;
        }
    }

    /// <summary>
    /// 손님의 한마디. 말투와 표정에 맞는 것을 ReactionLines 가 골라 준다.
    /// 여기서 뽑은 말을 결과창도 그대로 쓴다 — 같은 손님이 두 번 다르게 말하지 않도록.
    /// </summary>
    private static string ReactionLine(float accuracy)
    {
        return ReactionLines.For(accuracy);
    }
}
