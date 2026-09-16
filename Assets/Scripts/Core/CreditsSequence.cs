using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 크레딧. 새 화면을 만들지 않고 게임을 데모처럼 돌리면서 그 위에 이름을 얹는다.
///
/// 시계는 벽시계가 아니라 <b>엔딩곡 자신</b>이다(<see cref="Now"/>). 프레임이 튀어도
/// 글자가 음악에서 밀리지 않는다. 노래가 끝나는 순간에 화면이 검어지도록 모든 시각을
/// 클립 길이에서 거꾸로 잡는다.
///
/// 화면은 스스로 만든다. 빌더(RamenLayoutBuilder)를 거치지 않는 까닭은 두 가지다 —
/// 띠 하나와 글자 두 줄이 전부라 코드로 만들어도 무리가 없고, 씬을 다시 만들지 않아도
/// 되므로 고칠 때마다 Play 만 눌러 확인할 수 있다.
/// </summary>
public class CreditsSequence : MonoBehaviour
{
    /// <summary>돌고 있는 크레딧. 없으면 null. 시식 연출이 이걸 보고 자동으로 넘어간다.</summary>
    public static CreditsSequence Active { get; private set; }

    /// <summary>크레딧이 도는 중인가. 입력을 막아야 하는 쪽이 본다.</summary>
    public static bool Running { get { return Active != null; } }

    /// <summary>끝나고 어디로 돌아가는가.</summary>
    public enum Exit
    {
        /// <summary>5일 완주에서 들어온 길. 성적표 → [확인] → 엔딩 글 → 암전 → 여기.</summary>
        Reload,

        /// <summary>타이틀의 [크레딧] 으로 들어온 길.</summary>
        Title,
    }

    // ── 배선 ──────────────────────────────────────────────────────

    private Exit exit;
    private AudioSource song;

    /// <summary>왼쪽 칸에 쌓이는 줄들. 한 칸이 역할·이름 한 쌍이고, 한 번 뜨면 안 지워진다.</summary>
    private CanvasGroup[] lineGroups;

    /// <summary>왼쪽 칸 전체. 마지막에 이것만 걷으면 여섯 줄이 같이 스러진다.</summary>
    private CanvasGroup columnGroup;

    private CanvasGroup closingGroup;   // 마지막 한 줄.
    private CanvasGroup stageGroup;     // 오른쪽 창 둘레(뒷판·테두리).
    private GameObject backdropRoot;

    // ── 화면 ──────────────────────────────────────────────────────

    /// <summary>캔버스 층. 화면 전환 판(300)보다 아래여야 마지막 페이드가 글자까지 덮는다.</summary>
    private const int CreditsOrder = 290;

    /// <summary>캔버스 층. 창보다 뒤에 깔리는 판. 주문 화면(180)보다 낮아야 창이 위에 뜬다.</summary>
    private const int BackdropOrder = 5;

    // ── 오른쪽 창 ─────────────────────────────────────────────────

    /// <summary>
    /// 무대를 줄이는 배율.
    ///
    /// <b>0.5 여야 한다.</b> 캔버스 배율이 2(1920x1080)라 여기서 절반으로 줄이면
    /// 원본 그림 1픽셀이 화면 1픽셀에 정확히 떨어진다. 0.6 이나 0.75 로 두면 한 픽셀이
    /// 1.2·1.5 칸이 되어 어떤 줄은 두 칸, 어떤 줄은 한 칸으로 그려져 그림이 지저분해진다.
    /// </summary>
    private const float StageScale = 0.5f;

    /// <summary>
    /// 창 한가운데 자리. 화면을 세로로 반 가른 <b>오른쪽 칸의 한가운데</b>다.
    /// 캔버스가 -480~480 이니 오른쪽 칸은 0~480, 그 가운데가 240 이다.
    /// </summary>
    private static readonly Vector2 StageCenter = new Vector2(240f, 0f);

    /// <summary>
    /// 창을 오른쪽 칸 안에 넉넉히 앉히려고 잘라 내는 양(왼·아래·오른·위, 칸).
    ///
    /// 배율은 0.5 에서 못 내린다 — 픽셀이 한 칸에 정확히 떨어지는 유일한 값이다.
    /// 그래서 <b>줄이는 대신 가장자리를 잘라</b> 크기를 맞춘다. 무대 960x540 에서 양옆 60,
    /// 위아래 18 을 덜면 840x504 가 남고, 절반이니 화면에서는 420x252 로 보인다.
    /// 오른쪽 칸(480) 안에 양쪽 30칸씩 여백이 생긴다.
    ///
    /// 잘려 나가는 것은 포장마차 그림의 바깥 테두리뿐이라 장면은 그대로다.
    /// </summary>
    private static readonly Vector4 StageCrop = new Vector4(60f, 18f, 60f, 18f);

    /// <summary>
    /// 창 바깥을 덮는 판. <b>완전한 검정이다.</b>
    ///
    /// 처음엔 살짝 푸른 남색으로 두었는데, 화면 둘레가 그만큼 들려서 창이 「종이에 붙인 사진」
    /// 처럼 보였다. 새까맣게 두면 창 말고는 아무것도 없는 것이 되어 창 안이 제대로 선다.
    /// </summary>
    private static readonly Color BackdropInk = Color.black;

    // ── 왼쪽 글자 칸 ──────────────────────────────────────────────

    /// <summary>글자 칸의 폭과 한가운데 자리(칸). 창 왼쪽 변(-40)에 닿지 않게 둔다.</summary>
    private const float ColumnWidth = 440f;
    private const float ColumnCenterX = -240f;

    /// <summary>
    /// 줄이 서는 높이(칸).
    ///
    /// 여섯 줄이 <b>모두 같은 자리</b>에 선다. 위에서 아래로 쌓아 봤더니 명단이 채워지는 것이지
    /// 「한 사람을 소개하는 것」이 아니어서, 한 번에 한 사람만 스윽 떴다 스윽 사라지게 바꿨다.
    /// 왼쪽 칸의 위아래 한가운데다.
    /// </summary>
    private const float CardRoleY = 26f;
    private const float CardRuleY = -26f;
    private const float CardNameY = -54f;

    /// <summary>
    /// 역할 글자가 차지하는 높이(칸). 두 줄짜리 역할이 있어서 두 줄 자리를 미리 잡아 둔다.
    ///
    /// 아래변에 맞춰 찍는다(<see cref="TextAlignmentOptions.Bottom"/>). 그래야 한 줄이든
    /// 두 줄이든 금선 바로 위에서 시작해, 카드마다 금선과 이름이 같은 높이에 선다.
    /// 위로만 자라므로 줄 수가 달라도 아래쪽이 안 흔들린다.
    /// </summary>
    private const float RoleBoxHeight = 88f;

    /// <summary>역할과 이름 사이를 가르는 짧은 금선.</summary>
    private static readonly Vector2 RuleSize = new Vector2(130f, 1f);

    /// <summary>
    /// 글자 크기. <b>역할이 크고 이름이 작다 — 6 대 4다.</b>
    ///
    /// 처음엔 반대로 두었다(역할 12, 이름 36). 그러면 「사람 이름을 소개하는 화면」이 되는데,
    /// 크레딧에서 먼저 읽혀야 하는 것은 「이 자리에 누가 있었나」다. 역할을 앞세우면
    /// 같은 이름이 여러 역할에 나와도 각 줄이 제 뜻을 갖는다.
    ///
    /// 12·24 는 RamenLayoutBuilder 의 TextBody·TextTitle 과 같은 값이고, 36 은 그 표에
    /// 없는 새 크기다. 11픽셀 폰트를 12의 정수배(12·24·36)로 구우면 획이 칸에 정확히
    /// 들어가 흐려지지 않는다 — 22·33 이 어긋나는 것과 같은 이유다.
    /// </summary>
    private const int RoleFontSize = 36;   // 6
    private const int NameFontSize = 24;   // 4

    /// <summary>
    /// 칸을 넘치는 긴 이름에 쓰는 크기. 「김중현 · 박은석 · 최상우」가 24 로는 아슬아슬하다.
    /// 12 도 정수배라 획이 안 뭉갠다.
    /// </summary>
    private const int NameFontSizeNarrow = 12;

    /// <summary>이름에 주는 자간. 작게 쓰는 만큼 벌려 두어야 덩어리로 안 보인다.</summary>
    private const float NameTracking = 100f * 2f / NameFontSize;

    /// <summary>마지막 한 줄의 크기. 카드보다 크다 — 이게 마지막이라 무게를 싣는다.</summary>
    private const int ClosingFontSize = 36;

    /// <summary>
    /// 마지막 한 줄이 앉는 높이(칸).
    ///
    /// 화면 한가운데(0)에 두었더니 라멘 그릇과 겹쳤다. 그릇은 캔버스 +30~+125 에 앉고
    /// 글 뒤 판이 150 칸이라, -170 에 두면 판 윗변이 -95 로 그릇 아래에 여유 있게 선다.
    /// <b>그릇 자리를 옮기면 이 값도 같이 재야 한다.</b>
    /// </summary>
    private const float ClosingLineY = -170f;

    /// <summary>마지막 장면에서 조리대를 죽이는 정도. 글자가 나무 바닥에 묻히지 않게.</summary>
    private const float ClosingDimAlpha = 0.3f;

    /// <summary>
    /// 글자가 흐르는 높이(칸). 아래로 이만큼에서 떠올라, 제자리를 지나, 위로 이만큼 빠진다.
    ///
    /// 5 였을 때는 0.8초에 다섯 칸이라 움직임이 뚝뚝 끊겨 보였다 — 반칸을 못 쓰니
    /// 다섯 계단이 전부였다. 12 로 늘리면 같은 시간에 계단이 열두 개라 훨씬 부드럽다.
    /// 래스터 폰트라 반칸에 걸리면 획에 회색이 끼므로 <b>정수 칸으로만</b> 움직인다.
    /// </summary>
    private const float RiseHeight = 12f;

    /// <summary>
    /// 역할 글자에 주는 자간(TMP 단위, em 의 1/100).
    ///
    /// ⚠️ 래스터 픽셀 폰트라 자간이 정수 칸으로 떨어지지 않으면 획이 반칸에 걸린다.
    /// 값을 바꿀 때는 100*정수/글자크기 로 계산해서 넣고, 반드시 찍어서 확인할 것.
    /// </summary>
    private const float RoleTracking = 100f * 3f / RoleFontSize;

    /// <summary>
    /// 역할 글자 색. 등불빛에 익은 크림색이다.
    ///
    /// 순백은 이 게임 화면에서 혼자 튄다 — 배경이 통째로 주황·남색이라 흰 글자만 차갑다.
    /// 로고와 제등이 쓰는 노란빛 쪽으로 당기면 화면에 얹힌 것이 아니라 화면 안에 있는 것이 된다.
    /// </summary>
    private static readonly Color RoleInk = new Color(0.96f, 0.89f, 0.74f, 1f);

    /// <summary>이름 색. 역할보다 한 단계 죽여야 역할이 먼저 읽힌다.</summary>
    private static readonly Color NameInk = new Color(0.72f, 0.66f, 0.56f, 1f);

    /// <summary>금선 색. 이름보다 훨씬 죽여 둔다 — 눈에 띄면 그게 먼저 읽힌다.</summary>
    private static readonly Color RuleInk = new Color(0.85f, 0.72f, 0.45f, 0.35f);

    // ── 박자 ──────────────────────────────────────────────────────

    /// <summary>
    /// 카드 한 장이 뜨고 스러지기까지(초). 페이드 넣고 잰 전체다.
    ///
    /// <b>길이는 글이 정한다. 노래가 정하지 않는다.</b> 예전에는 엔딩곡(198.5초)을 줄 수로
    /// 나눠서 한 장에 31초를 줬는데, 읽고 나서 19초를 빈 화면으로 버티게 되어 지루했다.
    /// 10초면 스윽 떠서 읽히고 스윽 사라진다. 대신 노래는 다 못 쓰고 중간에 잦아든다.
    /// </summary>
    private const float CardSeconds = 4.2f;

    /// <summary>첫 카드가 뜨기 전에 두는 시간(초). 그동안 첫 손님이 걸어 들어온다.</summary>
    private const float IntroSeconds = 2.5f;

    /// <summary>마지막 카드가 스러지고 화면이 검어지기까지 두는 사이(초).</summary>
    private const float ClosingGap = 1.6f;

    // ── 마지막 장면 ───────────────────────────────────────────────
    //
    // 크레딧이 검어진 뒤, 그 어둠 뒤에서 무대를 통째로 갈아 끼운다 —
    // 창을 치우고 조리 화면을 온 화면으로 세우고, 그릇에 라멘 한 그릇을 제대로 말아 둔다.
    // 그리고 어둠을 걷으면 「내가 만든 라멘」이 드러나고 그 위로 한 줄이 배어 나온다.

    /// <summary>어둠이 걷히며 조리 화면이 드러나는 시간(초).</summary>
    private const float RevealSeconds = 2.2f;

    /// <summary>드러나기 시작하고 한 줄이 뜨기까지 두는 사이(초).</summary>
    private const float LineDelay = 1f;

    /// <summary>한 줄이 배어 나오는 시간(초).</summary>
    private const float LineInSeconds = 2.6f;

    /// <summary>한 줄이 떠 있는 시간(초).</summary>
    private const float LineHoldSeconds = 4.5f;

    /// <summary>끝으로 검어지는 시간(초).</summary>
    private const float LineOutSeconds = 2.2f;

    /// <summary>노래가 잦아들며 화면이 완전히 검어지기까지(초). 「여운」이다.</summary>
    private const float TailSeconds = 3f;

    /// <summary>
    /// 카드가 배어 나오거나 스러지는 데 걸리는 시간(초).
    ///
    /// 0.8 은 너무 빨라서 글이 튀어나왔다 사라지는 것처럼 보였다. 1.3 이면 스윽 흐른다.
    /// 카드 4.2초에서 페이드 둘(2.6초)을 빼면 다 뜬 채로 1.6초가 남는다 — 짧은 이름 한 줄은
    /// 그 안에 읽힌다. 더 늘리면 읽을 틈이 사라지므로 카드 길이도 같이 늘려야 한다.
    /// </summary>
    private const float FadeSeconds = 1.3f;

    /// <summary>
    /// 인사(Thanks for Playing)가 스러지는 시간(초).
    ///
    /// 이름 카드(1.3초)보다 느리다. 한참 서 있던 글이라 같은 속도로 걷으면 툭 꺼진 것으로
    /// 보인다 — 천천히 빠져야 「끝났다」가 된다.
    /// </summary>
    private const float ThanksOutSeconds = 2.4f;

    /// <summary>엔딩곡 크기.</summary>
    private const float SongVolume = 0.55f;

    // ── 들어가는 자리 ─────────────────────────────────────────────
    //
    // 예전에는 Begin 을 부른 그 프레임에 무대가 절반으로 줄고 검은 판이 깔렸다. 가게가
    // 툭 사라지고 크레딧이 툭 시작해서, 「장면이 넘어갔다」가 아니라 「화면이 바뀌었다」로
    // 보였다. 이제는 소리와 화면을 같이 잦아들게 한 뒤, **다 검어진 뒤에** 무대를 차린다.

    /// <summary>
    /// 크레딧에 들어가기 전, 가게 소리가 잦아드는 시간(초).
    ///
    /// <see cref="ScreenFade"/> 가 덮는 시간(outSeconds 2.2)과 같은 길이다. 소리가 먼저
    /// 끊기면 검어지는 동안 화면만 남고, 늦게 끊기면 검은 화면에서 가게 소리가 계속 난다.
    /// </summary>
    private const float PreludeFadeSeconds = 2.2f;

    /// <summary>다 검어지고 크레딧이 시작하기까지 검은 채로 두는 사이(초). 이 사이가 「암전」이다.</summary>
    private const float PreludeHoldSeconds = 1.4f;

    /// <summary>
    /// 검은 화면에서 크레딧이 배어 나오는 시간(초).
    ///
    /// 이 밑에 깔린 것도 대부분 검다(뒷판은 검은색이고 창은 아직 안 열렸다). 그래서 눈에
    /// 보이는 일은 거의 없고, 판을 넘겨받는 순간 한 프레임이라도 무대가 비치는 것을 막는다.
    /// </summary>
    private const float OpeningRevealSeconds = 1.2f;

    /// <summary>
    /// 크레딧이 끝나고 씬을 다시 열기까지 <b>완전히 검고 조용한 채로</b> 두는 시간(초).
    ///
    /// 예전에는 다 검어진 다음 프레임에 씬을 다시 열었다. 가게 소리를 끄는 페이드(0.8초)가
    /// 시작만 하고 씬 전환에 잘려서, 검어지는 순간 소리가 툭 끊기고 곧바로 타이틀 곡이 났다.
    /// </summary>
    private const float ClosingSilenceSeconds = 2.4f;

    // ── 건너뛰기 ──────────────────────────────────────────────────

    /// <summary>
    /// 「꾹 눌러서 넘기기」 게이지. 시식 연출이 쓰는 <b>그것을 그대로 빌려 쓴다.</b>
    ///
    /// 새로 만들지 않는 이유는 모양이 아니라 <b>박자</b> 때문이다. 누르는 시간(1.2초)·손을 뗄 때
    /// 빠지는 속도·한 프레임 상한이 저쪽에 맞춰져 있는데, 두 벌로 나누면 「우주에서 넘길 때와
    /// 크레딧에서 넘길 때 손맛이 다르다」가 된다. 왼쪽 마우스도 스페이스도 저쪽이 이미 받는다.
    /// </summary>
    private HoldToSkip skipGauge;

    /// <summary>건너뛰기가 들어왔는가. 두 번 들어오지 않게 막는다.</summary>
    private bool skipping;

    /// <summary>
    /// 건너뛸 때 화면과 소리가 잦아드는 시간(초).
    ///
    /// 제 순서로 끝날 때(2.2초)보다 짧다. 넘기겠다고 누른 사람을 2초 더 붙잡아 두면
    /// 그 페이드가 곧 「안 넘어가는 것」으로 읽힌다. 그렇다고 툭 끊으면 건너뛴 것이 아니라
    /// 게임이 튕긴 것으로 보여서, 잦아들기는 하되 짧게 잦아든다.
    /// </summary>
    private const float SkipOutSeconds = 1.2f;

    /// <summary>무대 코루틴. 건너뛸 때 <b>이것만</b> 끊는다.</summary>
    private Coroutine stageRoutine;

    // ── 가게 소리 ─────────────────────────────────────────────────
    //
    // 크레딧 내내 곡 밑에 아주 낮게 깔린다. 소리가 아예 없으면 화면은 가게인데
    // 귀에는 빈 방이라, 창 안에서 벌어지는 일이 남의 일처럼 들린다.

    private const float StreetUnderSong = 0.10f;
    private const float KitchenUnderSong = 0.05f;

    /// <summary>마지막 장면에서 되살리는 국물 끓는 소리. 화면에 그릇 하나뿐이라 조금 올린다.</summary>
    private const float KitchenLastScene = 0.14f;

    /// <summary>
    /// 엔딩곡 길이(초). 이제 박자를 정하지는 않고, 빨리 감기가 곡 끝을 넘지 않게 하는 데만 쓴다.
    /// </summary>
    private float songSeconds = 198.5f;

    /// <summary>카드 한 장이 받는 시간. 무대(손님)도 이 박자에 선다.</summary>
    private float PairSeconds { get { return CardSeconds; } }

    /// <summary>카드가 다 지나간 시각(초).</summary>
    private float CardsEnd
    {
        get { return IntroSeconds + (GameManager.CreditLines.GetLength(0) + Logos.Length) * CardSeconds; }
    }

    /// <summary>
    /// 노래가 잦아들며 화면이 검어지기 시작하는 시각(초).
    ///
    /// 마지막 한 줄(「오늘 밤도, 불을 밝힙니다.」)은 여기 없다. 검어진 뒤 씬을 다시 열고
    /// <b>시작 화면 위에</b> 띄운다(<see cref="CreditsClosingLine"/>).
    /// </summary>
    private float MusicOut { get { return CardsEnd + ClosingGap; } }

    // ── 시계 ──────────────────────────────────────────────────────

    /// <summary>
    /// 지금 몇 초인가. 엔딩곡의 재생 위치가 그대로 시계다.
    ///
    /// 빠른 미리보기(F8)일 때만 따로 센 시간을 쓰고, 쌍이 바뀔 때마다 노래를 그 자리로 옮긴다.
    /// 음원이 아예 없으면 실시간으로 센다.
    /// </summary>
    private float Now
    {
        get
        {
            if (fastForward || song == null || !song.isPlaying) return clock;
            return song.time;
        }
    }

    private float clock;
    private bool fastForward;

    /// <summary>빠른 미리보기 배속. 31초짜리 한 쌍이 6초쯤에 지나간다.</summary>
    private const float FastFactor = 5f;

    // ── 시작 ──────────────────────────────────────────────────────

    /// <summary>
    /// 크레딧을 연다. 이미 돌고 있으면 아무 일도 없다.
    ///
    /// 게임오브젝트를 여기서 만든다. 씬에 미리 심어 두지 않으므로 빌더를 안 돌려도 된다.
    /// </summary>
    public static void Begin(Exit exit)
    {
        if (Active != null) return;

        var go = new GameObject("CreditsSequence");
        var credits = go.AddComponent<CreditsSequence>();
        credits.exit = exit;
        Active = credits;
    }

    private void Awake()
    {
        Active = this;

        // ⚠️ 여기서 화면을 만들지 않는다. BuildScreen 은 무대를 절반으로 줄이고 검은 뒷판을
        // 까는 일이라, Awake 에서 하면 Begin 을 부른 그 프레임에 가게가 통째로 사라진다.
        // 화면이 다 검어진 뒤에 Run → Prelude 가 부른다.
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    // ── 진행 ──────────────────────────────────────────────────────

    /// <summary>
    /// 어느 시각에 글자가 어떤 모습인가. <b>순수 함수다</b> — 같은 t 면 늘 같은 답이 나온다.
    ///
    /// 진행(<see cref="Run"/>)도 확인용 영상(CreditsPreview)도 이 하나만 본다. 둘로 나눠
    /// 적어 두면 미리보기에서 맞춰 놓은 박자가 실제 게임에서는 다르게 나온다.
    /// </summary>
    /// <summary>
    /// 글자 카드가 다 지나가고 화면이 검어지기까지(초).
    ///
    /// <b>크레딧 전체 길이가 아니다.</b> 그 뒤로 무대가 아이리스·마지막 장면·마지막 한 줄을
    /// 제 속도로 더 돈다(<see cref="PlayEnding"/>). 확인용 렌더가 어디까지 찍을지 정할 때 쓴다.
    /// </summary>
    public float TotalSeconds { get { return MusicOut + TailSeconds; } }

    /// <summary>글자가 다 지나갔는가.</summary>
    public bool IsDone(float t) { return t >= TotalSeconds; }

    /// <summary>
    /// 화면을 t 초 시점의 모습으로 세운다. <b>순수하다</b> — 같은 t 면 늘 같은 모습이다.
    ///
    /// 진행(<see cref="Run"/>)도 확인용 영상(CreditsPreview)도 이 하나만 부른다.
    /// 둘로 나눠 적어 두면 미리보기에서 맞춰 놓은 박자가 실제 게임에서는 다르게 나온다.
    /// </summary>
    /// <summary>
    /// 창 자리와 UI 를 <b>매 프레임 다시 박는다.</b>
    ///
    /// Awake 에서 한 번만 세워 두었더니 Play 에서 창이 도로 화면 한가운데로 돌아가고
    /// 상단바가 되살아났다. 무엇이 되돌리는지 끝내 못 잡아서, 잡는 대신 매 프레임 이기기로 했다.
    /// 값이 이미 맞으면 아무것도 안 하므로 부담도 없다.
    ///
    /// 되돌려 놓은 범인이 잡히면 이 함수는 없애고 원인을 고치는 것이 맞다 —
    /// 그래서 처음 한 번은 무엇이 어긋나 있었는지 콘솔에 남긴다.
    /// </summary>
    private void PinStage()
    {
        if (stageFrame == null || lastScene) return;

        if (stageFrame.localScale.x != StageScale || stageFrame.anchoredPosition != StageCenter)
        {
            if (!warnedPin)
            {
                warnedPin = true;
                Debug.Log("[크레딧] 창 자리가 " + stageFrame.anchoredPosition + " / 배율 "
                          + stageFrame.localScale.x + " 로 되돌아가 있어 다시 박습니다.");
            }

            stageFrame.localScale = new Vector3(StageScale, StageScale, 1f);
            stageFrame.anchoredPosition = StageCenter;
        }

        if (stageMask != null && stageMask.padding != StageCrop) stageMask.padding = StageCrop;
        for (int i = 0; i < stageInnerMasks.Count; i++)
        {
            if (stageInnerMasks[i] != null && stageInnerMasks[i].padding != StageCrop)
            {
                stageInnerMasks[i].padding = StageCrop;
            }
        }

        // 상단바·수익 패널 같은 UI 도 같이 눌러 둔다. 주문 화면을 다시 열 때 되살아난다.
        Transform screen = stageFrame.Find("OrderScreen");
        if (screen == null) return;

        for (int i = 0; i < StageHidden.Length; i++)
        {
            Transform t = screen.Find(StageHidden[i]);
            if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }
    }

    private bool warnedPin;

    /// <summary>마지막 장면으로 갈아 끼운 뒤인가. 그때부터는 창을 도로 온 화면으로 편다.</summary>
    private bool lastScene;

    public void SeekTo(float t)
    {
        PinStage();

        if (lineGroups != null)
        {
            for (int i = 0; i < lineGroups.Length; i++)
            {
                if (lineGroups[i] == null) continue;

                // 한 번에 한 사람만. 제 차례에 스윽 떴다가 스윽 사라진다.
                float start = IntroSeconds + i * CardSeconds;
                float came = Ramp(t, start, FadeSeconds);

                // 인사 한 장만 저 혼자 안 넘어간다. 뜬 자리에 그대로 서서, 무대가 걷을 때
                // (까마귀 아이리스 직전) 비로소 스러진다.
                float gone = i == thanksIndex
                    ? thanksAway
                    : Ramp(t, start + CardSeconds - FadeSeconds, FadeSeconds);

                lineGroups[i].alpha = came * (1f - gone);

                // 뜰 때도 질 때도 **위로** 흐른다. 아래에서 떠올라 제자리를 지나 위로 빠진다 —
                // 한 방향으로만 흐르니 글이 「지나간다」로 읽힌다. 제자리에서 그냥 꺼지면
                // 떠오른 보람이 없다.
                //
                // 반칸에 걸리면 획에 회색이 끼므로 정수 칸으로만 움직인다.
                float rise = -RiseHeight * (1f - came) + RiseHeight * gone;
                ((RectTransform)lineGroups[i].transform).anchoredPosition =
                    new Vector2(0f, Mathf.Round(rise));
            }
        }

        // 글자 칸은 여기서 안 걷는다.
        //
        // 예전에는 카드가 다 지나간 뒤(MusicOut 직전) 칸째로 걷었는데, 이제 맨 끝 인사가
        // 그 뒤로도 계속 서 있어야 한다. 칸을 걷으면 그 위에 있는 인사까지 같이 꺼진다.
        // 걷는 일은 카드마다 저 혼자 하고(위), 인사는 무대가 걷는다.

        // ⚠️ **창은 건드리지 않는다.** 예전에는 글자와 같이 걷었는데, 이름이 다 지나가는
        // 시각(카드 시계)과 손님이 다 먹는 시각(무대)이 달라서 — 손님이 아직 먹고 있는데
        // 창이 먼저 스러지고 화면이 검어졌다. 창을 치우는 것은 끝맺음(PlayEnding)이 맡는다.
        //
        // 검은 뒷판(stageGroup)도 안 건드린다. 걷으면 그 뒤의 조리 화면 나무가 드러난다.

        // 여기서부터 끝까지는 **무대가 몬다**(PlayEnding). 카드 시계가 끝맺음까지 쥐고 있으면,
        // 손님이 아직 먹고 있는데 화면이 먼저 검어져 아이리스도 마지막 장면도 잘린다.
        // 실제로 그렇게 잘렸다.
        // 검은 판은 여기서 아예 안 만진다.
        //
        // 예전에는 카드가 다 지나가면(약 40초) 화면을 덮었는데, 그때 손님은 아직 먹는 중이라
        // **쓰러지기도 전에 화면이 검어졌다.** 검어지는 것은 아이리스가 오므린 뒤에만 일어나야
        // 한다 — 그 자리는 PlayEnding 이 쥔다.
    }

    /// <summary>인사 카드가 <see cref="lineGroups"/> 에서 몇 번째인가. 맨 끝이다.</summary>
    private int thanksIndex = -1;

    /// <summary>
    /// 인사가 얼마나 스러졌는가(0 = 그대로 · 1 = 다 걷혔다).
    ///
    /// 다른 카드는 노래 시계가 걷는데 이것만 무대가 민다. 언제 걷을지를 정하는 것이
    /// 노래가 아니라 <b>손님이 언제 쓰러지고 까마귀가 언제 앉느냐</b>라서, 시계로는 못 맞춘다.
    /// </summary>
    private float thanksAway;

    /// <summary>인사를 걷는다. 다른 카드와 같은 길로 나간다 — 흐려지면서 위로 빠진다.</summary>
    private IEnumerator FadeThanks(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            thanksAway = Mathf.SmoothStep(0f, 1f, t / seconds);
            yield return null;
        }

        thanksAway = 1f;
    }

    /// <summary>끝맺음을 무대가 넘겨받았는가. 그때부터 <see cref="SeekTo"/> 는 손을 뗀다.</summary>
    private bool endingOwned;

    /// <summary>끝맺음까지 다 돌았는가. <see cref="Run"/> 이 이걸 보고 씬을 다시 연다.</summary>
    private bool endingDone;

    /// <summary>0 → 1 로 부드럽게 오르는 구간. from 이전은 0, from+length 이후는 1.</summary>
    private static float Ramp(float t, float from, float length)
    {
        if (length <= 0f) return t >= from ? 1f : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - from) / length));
    }


    /// <summary>
    /// 시식 연출에서 「누르면 다음」 자리를 크레딧이 대신 기다리는 시간(초).
    /// 손님 소감을 읽을 만큼이다. <see cref="EatingCutscene.WaitForNextPress"/> 가 본다.
    /// </summary>
    public const float AutoReadSeconds = 2.6f;

    /// <summary>
    /// 들어가는 자리. <b>가게를 닫고 나서 크레딧을 연다.</b>
    ///
    /// 순서가 전부다 — 가게 소리와 화면이 같이 잦아들고, 다 검어진 채로 한 박자 쉬고,
    /// 그 어둠 뒤에서 무대를 차린 다음에야 엔딩곡이 걸린다. 하나라도 앞뒤가 바뀌면
    /// 「가게가 사라지고 크레딧이 시작했다」로 보인다.
    ///
    /// 검은 판이 둘인 것은 층이 달라서다. 덮는 동안은 전환 판(<see cref="ScreenFade"/>, 300)이
    /// 쥐고, 크레딧이 서고 나면 제 판(<see cref="blackout"/>, 290)이 넘겨받는다.
    /// 넘겨받은 <b>뒤에</b> 전환 판을 치운다 — 순서를 뒤집으면 한 프레임 가게가 비친다.
    /// </summary>
    private IEnumerator Prelude()
    {
        // 설정창이 열려 있으면 먼저 닫는다. 그 창은 Time.timeScale 을 0 으로 눌러 두는데,
        // 크레딧은 실시간으로 도니까 닫을 사람 없이 눌린 채로 남는다. 그 상태로 씬이 다시
        // 열리면 시작 화면이 멈춘 채 뜬다. 창은 시작 화면 밑에 있어 같이 감춰지기만 한다.
        SettingsUI settings = FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
        if (settings != null) settings.Close();

        // 소리부터 뺀다. 타이틀에서 들어오면 타이틀 곡, 5일 완주로 들어오면 가게 곡이다.
        // 어느 쪽인지 따지지 않고 둘 다 끈다 — 안 울고 있는 이름은 Sfx.Stop 이 그냥 넘긴다.
        float soundOutAt = Time.unscaledTime + PreludeFadeSeconds;
        Sfx.Stop("bgm_title", PreludeFadeSeconds);
        Sfx.Stop("bgm_shop", PreludeFadeSeconds);
        Sfx.Stop("amb_street_night", PreludeFadeSeconds);
        Sfx.Stop("amb_broth_boil", PreludeFadeSeconds);
        Sfx.Stop("amb_noodle_pot", PreludeFadeSeconds);

        ScreenFade fade = ScreenFade.Instance;

        // 5일 완주 길은 엔딩 글을 검은 화면에 띄우고 오므로 이미 덮여 있다. 그대로 또 덮으면
        // ScreenFade 가 0 부터 다시 시작해서 화면이 한 번 환해졌다 도로 검어진다.
        if (fade != null && !fade.IsBlack) yield return fade.FadeOut();
        else if (fade != null) fade.HoldBlack();

        // 소리가 다 빠질 때까지 기다린다. 이미 검게 들어온 길에서는 위 페이드가 없어서
        // 이 기다림이 곧 「브금이 잦아드는 시간」이 된다.
        while (Time.unscaledTime < soundOutAt) yield return null;

        // 암전. 검은 화면에 아무것도 없는 이 사이가 있어야 앞과 뒤가 다른 장면이 된다.
        yield return new WaitForSecondsRealtime(PreludeHoldSeconds);

        // 어둠 뒤에서 무대를 차린다.
        BuildScreen();
        SetBlack(1f);

        // 한 프레임 — 내 판이 실제로 그려진 뒤에 전환 판을 치운다.
        yield return null;
        if (fade != null) fade.Clear();
    }

    private IEnumerator Run()
    {
        yield return Prelude();

        HideStageUi();
        AttachSkipGauge();
        StartSong();

        // 노래가 걸린 자리에서 검은 판을 걷는다. 밑에 깔린 것도 아직 검어서(뒷판은 검은색,
        // 창은 FadeStageIn 이 열기 전이라 투명) 눈에는 거의 안 띄고, 넘겨받는 한 프레임을 막는다.
        StartCoroutine(FadeBlack(1f, 0f, OpeningRevealSeconds));

        // 무대와 글자는 따로 돈다. 글자는 시계(노래)만 보고, 무대는 손님 하나를 끝까지 몬다.
        // 한 코루틴에 묶으면 손님이 늦어질 때 글자까지 같이 밀려 음악에서 떨어진다.
        stageRoutine = StartCoroutine(RunStage());

        int announced = -1;
        int lines = GameManager.CreditLines.GetLength(0);

        // **무대가 끝맺음을 마칠 때까지 돈다.** 카드 시계(IsDone)로 끊으면 손님이 아직
        // 먹고 있는데 씬이 다시 열려 아이리스도 마지막 장면도 안 나온다.
        while (!endingDone)
        {
            // 게이지는 **매 프레임** 물어봐야 한다. 차오르는 것도 빠지는 것도 도넛을 띄우고
            // 접는 것도 전부 Poll 안에서 일어난다 — 걸러 부르면 게이지가 얼어붙는다.
            if (WantsSkip())
            {
                yield return SkipOut();
                break;
            }

            float t = Now;
            SeekTo(t);

            // 줄이 새로 뜨는 자리마다 한 줄 남긴다. 음악과 얼마나 어긋났는지 여기서 본다.
            int now = Mathf.Clamp(Mathf.FloorToInt(t / PairSeconds), -1, lines - 1);
            if (now >= 0 && now != announced)
            {
                announced = now;
                float due = now * PairSeconds;
                Debug.Log(string.Format("[크레딧] {0}/{1}  {2}  음악 {3:F1}s (예정 {4:F1}s, 드리프트 {5:+0.0;-0.0}s)",
                                        now + 1, lines, GameManager.CreditLines[now, 1], t, due, t - due));
            }

            clock += Time.unscaledDeltaTime * (fastForward ? FastFactor : 1f);
            yield return null;
        }

        yield return Finish();
    }

    // ── 건너뛰기 ──────────────────────────────────────────────────

    /// <summary>
    /// 「꾹 눌러서 넘기기」 게이지를 크레딧 화면으로 옮겨 온다.
    ///
    /// 게이지는 원래 <c>Frame</c> 밑에 산다. 그런데 크레딧은 Frame 을 절반으로 줄여 오른쪽 창에
    /// 앉히고 자르개까지 물린다 — 그대로 두면 도넛도 반으로 줄어 창 안에 갇히고, 「꾹 눌러서
    /// 넘기기」 글자는 자르개에 잘려 나간다. 캔버스 직속으로 옮기면 앵커(오른쪽 아래)와 크기가
    /// 그대로 살아나 제자리에 선다.
    ///
    /// <see cref="HideStageUi"/> 가 방금 무대에서 걷어 낸 것을 여기서 되살린다. 순서가 중요하다 —
    /// 먼저 걷고 나서 옮겨야, 창에 딸려 들어간 한 프레임이 안 보인다. 게이지 자신은 다 접힌 채로
    /// 시작하므로(<see cref="HoldToSkip.Hide"/>) 켜 둔다고 바로 보이지는 않는다.
    ///
    /// 되돌리지 않는다 — 크레딧이 끝나면 씬을 다시 연다.
    /// </summary>
    private void AttachSkipGauge()
    {
        skipGauge = FindFirstObjectByType<HoldToSkip>(FindObjectsInactive.Include);
        if (skipGauge == null)
        {
            Debug.LogWarning("[크레딧] 씬에 HoldToSkip 이 없어 건너뛰기를 못 씁니다. 빌더를 한 번 돌려 주세요.");
            return;
        }

        // ⚠️ **자르개를 먼저 떼어 낸다.**
        //
        // ShrinkStage 는 Canvas 를 가진 자식마다 자르개를 한 벌씩 붙인다(RectMask2D 가 중첩
        // Canvas 를 못 넘어서다). 게이지도 제 Canvas 를 얹고 있어서 그 한 벌을 받는다.
        // 그대로 옮기면 자르개가 따라와 **제 자식을 창 크기(60,18)만큼 잘라 낸다** —
        // 도넛 오른쪽이 날아가고 「꾹 눌러서 넘기기」는 뒷글자와 아랫단이 잘린다.
        // 실제로 그렇게 잘려 보였다. 목록에서도 빼야 PinStage 가 매 프레임 다시 먹이지 않는다.
        RectMask2D mask = skipGauge.GetComponent<RectMask2D>();
        if (mask != null)
        {
            stageInnerMasks.Remove(mask);
            Destroy(mask);
        }

        Transform frame = FindFrame();
        if (frame != null && frame.parent != null) skipGauge.transform.SetParent(frame.parent, false);

        skipGauge.gameObject.SetActive(true);
        skipGauge.Hide();
    }

    /// <summary>
    /// 건너뛰고 싶은가. 게이지를 다 채워야 참이 된다.
    ///
    /// 시식 연출과 <b>같은 손맛</b>이다 — 왼쪽 마우스나 스페이스를 1.2초 누르고 있으면 넘어가고,
    /// 손을 떼면 도로 빠진다. 한 번 누르면 그 자리에서 넘어가게 두지 않는 이유도 저쪽과 같다.
    /// 손이 미끄러져 한 번 눌린 것으로 크레딧이 통째로 날아가면 다시 볼 길이 타이틀뿐이다.
    /// </summary>
    private bool WantsSkip()
    {
        if (skipping || skipGauge == null) return false;
        if (!skipGauge.Poll()) return false;

        skipping = true;
        Debug.Log("[크레딧] 건너뜁니다.");
        return true;
    }

    /// <summary>
    /// 건너뛰고 나가는 자리. <b>제 순서로 끝날 때와 같은 그림으로 닫는다</b> —
    /// 화면과 소리가 같이 잦아들고, 그 뒤는 <see cref="Finish"/> 가 이어받는다.
    ///
    /// 툭 끊고 씬을 열면 건너뛴 것이 아니라 게임이 튕긴 것으로 보인다.
    /// </summary>
    private IEnumerator SkipOut()
    {
        // ⚠️ 무대만 끊는다. StopAllCoroutines 는 못 쓴다 — 이 코루틴을 부른 Run 까지 멈춰서
        // 그다음 Finish 가 영영 안 돌고, 검은 화면에 그대로 갇힌다.
        if (stageRoutine != null)
        {
            StopCoroutine(stageRoutine);
            stageRoutine = null;
        }

        if (skipGauge != null) skipGauge.Hide();
        if (song != null) StartCoroutine(FadeSong(SkipOutSeconds));

        // 지금 얼마나 검은지에서 이어 간다. 0 에서 다시 시작하면, 마지막 장면처럼 이미 검어져
        // 있던 자리에서 눌렀을 때 화면이 한 번 환해졌다 도로 검어진다.
        float from = blackout != null ? blackout.color.a : 0f;
        yield return FadeBlack(from, 1f, SkipOutSeconds);

        // 다 검어진 **뒤에** 아이리스를 치운다. 손님이 쓰러지는 대목에서 눌렀으면 아이리스가
        // 반쯤 오므린 채로 남아 있는데, 먼저 치우면 그 구멍으로 무대가 비친다.
        IrisFade iris = FindFirstObjectByType<IrisFade>(FindObjectsInactive.Include);
        if (iris != null) iris.Hide();
    }

    // ── 마지막 장면 ───────────────────────────────────────────────

    /// <summary>
    /// 마지막 한 줄이 뜰 배경 — 조리 화면에 라멘 한 그릇.
    ///
    /// 화면이 새까맣게 덮인 동안에 갈아 끼운다. 창을 치우고 무대를 온 화면으로 되돌린 뒤,
    /// 주문 화면을 걷고 조리대를 드러내고, 그릇에 제대로 한 그릇을 만다.
    /// 어둠이 걷히면 「이 가게가 내는 라멘」이 화면에 남는다.
    /// </summary>
    private IEnumerator SetUpLastScene()
    {
        Transform frame = FindFrame();
        if (frame == null) yield break;

        // 여기서부터는 PinStage 가 손을 뗀다. 안 그러면 온 화면으로 편 것을 도로 창으로 만든다.
        lastScene = true;

        // 창 바깥을 덮던 검은 판을 걷는다. 안 걷으면 조리대가 그 밑에 깔려 새까만 화면만 남는다.
        if (backdropRoot != null) backdropRoot.SetActive(false);

        // 창을 접고 온 화면으로 되돌린다. 자르개는 전부 뗀다.
        if (stageMask != null) Destroy(stageMask);
        for (int i = 0; i < stageInnerMasks.Count; i++)
        {
            if (stageInnerMasks[i] != null) Destroy(stageInnerMasks[i]);
        }
        stageInnerMasks.Clear();
        frame.localScale = Vector3.one;
        ((RectTransform)frame).anchoredPosition = Vector2.zero;
        if (stageAway != null) stageAway.alpha = 1f;

        // 주문 화면(포장마차)을 걷어 조리대를 드러낸다.
        Transform screen = frame.Find("OrderScreen");
        if (screen != null) screen.gameObject.SetActive(false);

        // 그릇과 재료통을 **다시 켠다.**
        //
        // 켜는 것을 빼먹으면 안 된다. 주문 화면을 열 때 OrderScreenUI.ShowCookingProps(false) 가
        // 조리대 물건(그릇 포함)을 통째로 내려 두는데, 우리는 주문 화면을 「끄는」 것이지
        // 「닫는」 것이 아니라서 그쪽이 도로 켜 주지 않는다.
        Transform bowl = frame.Find("Bowl");
        if (bowl != null)
        {
            bowl.gameObject.SetActive(true);
            bowl.localScale = new Vector3(LastBowlScale, LastBowlScale, 1f);
        }

        Transform slots = frame.Find("Slots");
        if (slots != null) slots.gameObject.SetActive(true);

        // ⚠️ **재료통이 켜져 있는 동안에 담아야 한다.**
        //
        // 재료 그림은 재료통(IngredientSlot)이 들고 있고, 그것을 찾는 쪽은 켜진 것만 뒤진다.
        // 재료통을 먼저 끄고 담으면 그림 없이 담겨서 — 토핑이 하나도 안 올라간
        // **빈 그릇**이 된다. 실제로 그랬다.
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            game.CreditsClearBowl();
            yield return game.CreditsPour(LastBowl);
        }

        // 다 담은 뒤에 조리대를 치운다. 그릇 하나만 남는다.
        foreach (string name in LastSceneHidden)
        {
            Transform t = frame.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        // 조리대 그림을 판(960x540)에 딱 맞춘다. 늘어난 채로 두면 16:9 가 아닌 창에서
        // 화면 바깥까지 나무가 깔려, 게임 화면과 바깥의 경계가 사라진다.
        var background = frame.parent.Find("Background") as RectTransform;
        if (background != null)
        {
            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(960f, 540f);
        }

        // 그 바깥은 카메라가 지우는 색이 그대로 보인다. 검게 둔다.
        if (Camera.main != null) Camera.main.backgroundColor = Color.black;

        // 비네트를 도로 켠다.
        //
        // 무대를 오른쪽 창으로 줄여 놓는 동안에는 비네트도 같이 줄어 창 밖에 갈색 네모로
        // 남으므로 꺼 두었다(FrameHidden). 여기서는 판이 원래 크기로 돌아와 화면을 꽉 채우니
        // 켜도 새는 데가 없고, 가장자리가 눌려야 그릇 하나에 눈이 모인다.
        foreach (string name in new[] { "ScreenVignette", "ScreenGrain" })
        {
            Transform t = frame.Find(name);
            if (t != null) t.gameObject.SetActive(true);
        }

        // 국물 끓는 소리만 도로 올린다. 화면에 그릇 하나뿐이라, 이 소리가 있어야
        // 「아직 불이 켜져 있다」가 된다 — 마지막 한 줄이 하려는 말과 같다.
        Sfx.Loop("amb_broth_boil", KitchenLastScene, RevealSeconds);
    }

    /// <summary>마지막 장면에서 치우는 것. 조리대와 그릇만 남긴다.</summary>
    public static readonly string[] LastSceneHidden =
    {
        "TopBar", "SlotNameplate", "SeasoningBadges", "IngredientToast",
        "DiscardConfirm", "SubmitConfirm", "TutorialPrompt", "TutorialDim",

        // 조리대 물건을 전부 치운다. 나무 바닥에 그릇 하나만 남겨
        // 「이 가게가 내는 라멘」이 화면의 주인공이 되게 한다.
        "NoodlePot", "Slots",
    };

    /// <summary>
    /// 마지막 장면에서 그릇을 키우는 배율.
    ///
    /// <b>정수여야 한다.</b> 1.5 로 두면 1920x1080(캔버스 배율 2)에서는 3 화면픽셀로 떨어져
    /// 멀쩡하지만, 창이 작아 캔버스 배율이 1 이 되는 순간 1.5 픽셀이 되어 획이 흐려진다.
    /// 2 는 어느 배율에서든 정수다.
    /// </summary>
    private const float LastBowlScale = 2f;

    /// <summary>
    /// 마지막 장면에 말아 두는 한 그릇.
    ///
    /// 주문에 맞춘 것이 아니라 <b>보기 좋으라고</b> 고른 것이다 — 시오 타래에 굵은 면,
    /// 차슈 둘에 계란·김·파·멘마까지 올려 그릇이 가득 차 보이게 한다.
    /// </summary>
    public static readonly Dictionary<IngredientType, int> LastBowl =
        new Dictionary<IngredientType, int>
        {
            { IngredientType.ShioTare, 1 },
            { IngredientType.Broth, 1 },
            { IngredientType.ThickNoodles, 1 },

            // 토핑 일곱 가지가 하나도 빠짐없이 올라간다. 차슈만 둘이다 — 제일 큰 조각이라
            // 하나면 그릇 한쪽이 휑하고, 둘이면 가운데를 가로질러 구도가 잡힌다.
            { IngredientType.Chashu, 2 },
            { IngredientType.Egg, 1 },

            // 김은 석 장. 자리가 넷까지 있고(Bowl.ToppingLayout), 세 장이 부챗살처럼 겹쳐
            // 서면 그릇 왼쪽 위가 제대로 채워진다. 한 장이면 허전하다.
            { IngredientType.Nori, 3 },
            { IngredientType.Menma, 1 },
            { IngredientType.BeanSprout, 1 },
            { IngredientType.WoodEar, 1 },
            { IngredientType.GreenOnion, 1 },

            // 조미료 둘. 그릇 그림에는 안 얹히지만(배지로만 보인다) 「다 넣은 한 그릇」이다.
            { IngredientType.FlavorOil, 1 },
            { IngredientType.ChiliPowder, 1 },
        };

    // ── 무대 ──────────────────────────────────────────────────────

    /// <summary>
    /// 크레딧 손님 한 명. 글자 한 쌍에 이 한 줄이 붙는다.
    ///
    /// <see cref="Persona"/> 는 DialogueDB 의 말투 아이디다(Assets/Art/손님리뉴얼 의 폴더 이름과 같다).
    /// 그 말투의 얼굴·대사가 통째로 따라온다.
    /// </summary>
    private struct Shot
    {
        public string Persona;

        /// <summary>노리는 반응. 정확도를 여기에 맞춰 그릇을 담는다.</summary>
        public Take Take;

        /// <summary>한 손님이 그릇을 몇 번 비우는가. QA 자리만 셋이다.</summary>
        public int Bowls;
    }

    /// <summary>노리는 반응. <see cref="EatingCutscene"/> 의 네 갈래와 같다.</summary>
    private enum Take { Perfect, Good, Okay, Bad }

    /// <summary>
    /// 샷 리스트. 줄 순서가 <see cref="GameManager.CreditLines"/> 와 그대로 맞물린다.
    ///
    /// 정확도를 손님마다 달리 먹여서 여섯 컷이 전부 다른 그림이 되게 한다 —
    /// 우주·번개 / 흐뭇 / 갸웃 / 까마귀. 연출은 넷 다 이미 만들어져 있다.
    /// </summary>
    private static readonly Shot[] Shots =
    {
        new Shot { Persona = "Gourmet", Take = Take.Perfect, Bowls = 1 },  // 눈 클로즈업 → 번개 → 우주 → 따봉
        new Shot { Persona = "Otaku",   Take = Take.Bad,     Bowls = 1 },  // 까마귀 + 머리 위 「…」
        new Shot { Persona = "Child",   Take = Take.Perfect, Bowls = 3 },  // 그릇을 세 번 비운다
    };

    /// <summary>손님이 자리에 서서 주문을 말하고 나서 조리에 들어가기까지(초).</summary>
    private const float OrderLineSeconds = 4.5f;

    /// <summary>그릇을 받고 한 마디 하는 시간(초).</summary>
    private const float ServedLineSeconds = 2.2f;

    /// <summary>한 손님이 끝나고 다음 손님이 들어오기까지 빈 카운터로 두는 최소 시간(초).</summary>
    private const float EmptyCounterSeconds = 1.2f;

    /// <summary>
    /// 크레딧 무대에서 치우는 것(캔버스 Frame 바로 밑). 조리 화면과 상단바·키 안내다.
    /// 확인용 렌더(CreditsPreview)도 이 목록을 본다 — 두 벌로 적어 두면 미리보기와
    /// 실제 화면이 다르게 나오고, 그러면 미리보기가 거짓말이 된다.
    /// </summary>
    /// <remarks>
    /// 조리 화면(TopBar·Slots·Bowl·NoodlePot…)은 여기 없다. 주문 화면의 Night 판이 불투명이라
    /// 이미 통째로 가려지고, <b>Bowl 은 살아 있어야 한다</b> — 무대가 거기에 실제로 담는다.
    /// 끄면 FindFirstObjectByType&lt;Bowl&gt; 이 못 찾아 담기가 통째로 건너뛰어진다.
    /// </remarks>
    /// <remarks>
    /// <b>ScreenVignette 가 그 「갈색 화면」이었다.</b> 비네트는 후처리가 아니라 그림 한 장이고,
    /// <c>Frame</c> 밑에 960x540 으로 깔려 있다. 크레딧에서 Frame 을 0.5배로 줄여 오른쪽에
    /// 앉히면 그 그림도 같이 줄어 480x270 짜리 판이 되는데, 가장자리가 짙을수록 진한 갈색이라
    /// 창 바깥 검은 바탕 위에 그대로 얹혀 네모난 갈색 띠로 보인다(실측 RGB 22,11,8).
    /// 게임 화면을 어둡게 누르는 용도라 크레딧에는 올릴 이유가 없다. 알갱이(ScreenGrain)도 같다.
    /// </remarks>
    /// <remarks>
    /// <b>HoldToSkip 은 여기서 걷었다가 곧바로 되살아난다.</b> 크레딧도 「꾹 눌러서 넘기기」를
    /// 쓰는데, 게이지가 Frame 밑에 있어 창으로 줄면 같이 줄고 잘린다. 그래서 무대에서 한 번
    /// 걷어 낸 다음 캔버스 직속으로 옮겨 다시 켠다(<see cref="AttachSkipGauge"/>).
    /// 순서를 뒤집어 옮기고 나서 걷으면 게이지가 꺼진 채로 남아 영영 안 뜬다.
    /// </remarks>
    public static readonly string[] FrameHidden =
    {
        "TitleScreen", "KeyHint_Tab", "KeyHint_Book", "KeyHint_Esc", "HoldToSkip", "DragLayer",
        "TutorialPrompt", "TutorialDim",
        "ScreenVignette", "ScreenGrain",
    };

    /// <summary>주문 화면 안에서 치우는 것. 영업시간·수익 패널은 UI 라 크레딧에 안 올린다.</summary>
    public static readonly string[] StageHidden =
    {
        "DayTimePanel", "RevenuePanel",
    };

    /// <summary>
    /// 무대에서 UI 를 걷는다. 되돌리지 않는다 — 크레딧은 끝나면 씬을 다시 연다.
    ///
    /// 조리 화면 쪽은 시식 연출이 <c>ShowGameUI(false)</c> 로 껐다 켜기를 되풀이하므로,
    /// 거기 맡기지 않고 여기서 통째로 내려 둔다.
    /// </summary>
    public static void HideStageUi()
    {
        Transform frame = FindFrame();
        if (frame == null) return;

        // 튜토리얼을 끝난 것으로 친다.
        //
        // ⚠️ 이게 없으면 **그릇에 아무것도 안 담긴다.** TutorialManager.CanPick 이 튜토리얼이
        // 도는 동안 「안내한 그 재료」 말고 전부 거부하는데, F6 으로 크레딧을 켜면 튜토리얼이
        // 아직 안 끝난 상태라 담는 족족 튕겨 나간다. 손님 앞 그릇은 그림 한 장이라 티가 안 나고,
        // 마지막 조리 화면 그릇에서야 빈 그릇으로 드러났다.
        //
        // 크레딧은 게임이 끝난 자리이므로 튜토리얼이 무언가를 막을 이유가 없다.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsRunning)
        {
            TutorialManager.Instance.Finish();
        }

        // 재료 안내 토스트를 떼어 둔다.
        //
        // 그릇에 재료를 담을 때마다 Bowl 이 토스트를 띄우는데, 그 판은 꺼져 있어서
        // 「Coroutine couldn't be started because the game object 'IngredientToast' is
        // inactive!」 가 담는 재료 수만큼 쏟아진다. 크레딧에는 안내 UI 를 올리지 않으므로
        // 켜는 대신 아예 떼어 낸다. 크레딧이 끝나면 씬을 다시 여니 되돌릴 것이 없다.
        var pourBowl = FindFirstObjectByType<Bowl>(FindObjectsInactive.Include);
        if (pourBowl != null) pourBowl.toast = null;

        // 밤 배경은 판(960x540)보다 크게 잡혀 있다 — 16:9 가 아닌 창을 메우려고.
        // 창으로 줄이면 그 큰 판이 창 밖으로 삐져나와 검은 화면 옆에 짙은 갈색 띠가 생긴다.
        // 판 크기로 맞춰 둔다. 크레딧이 끝나면 씬을 다시 여니 되돌릴 것이 없다.
        var night = frame.Find("OrderScreen/Night") as RectTransform;
        if (night != null) night.sizeDelta = new Vector2(960f, 540f);

        foreach (string name in FrameHidden)
        {
            Transform t = frame.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        Transform screen = frame.Find("OrderScreen");
        if (screen == null) return;

        foreach (string name in StageHidden)
        {
            Transform t = screen.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    private IEnumerator RunStage()
    {
        GameManager game = GameManager.Instance;
        if (game == null)
        {
            Debug.LogWarning("[크레딧] 씬에 GameManager 가 없어 무대 없이 글자만 돕니다.");
            yield break;
        }

        // 무대는 글자 박자를 따라가지 않는다. 카드는 3.6초에 한 장씩 저 혼자 넘어가고,
        // 여기서는 손님 하나의 이야기를 처음부터 끝까지 제 속도로 돈다.
        yield return PlayBigEater(game);

        Debug.Log("[크레딧] 무대 끝.");
    }

    // ── 대식가 한 사람 ────────────────────────────────────────────

    /// <summary>먹방 유튜버 손님. 열다섯 말투 중 제일 잘 먹게 생긴 쪽이다.</summary>
    private const string EaterPersona = "Youtuber";

    /// <summary>자리에 서서 하는 첫 마디.</summary>
    private const string EaterOpening = "여기 너무 맛있다!\n곱빼기로 주세요.";

    /// <summary>한 그릇을 비우고 하는 말. 그릇 수만큼 돈다.</summary>
    private static readonly string[] EaterMore =
    {
        "한 그릇 더!",
        "한 그릇만 더요!",
        "…한 그릇 더.",
    };

    /// <summary>다 먹고 나서.</summary>
    private const string EaterDone = "후…\n더 이상은 못 먹겠어…";

    /// <summary>비우는 그릇 수. <see cref="EaterMore"/> 보다 하나 많다 — 첫 그릇이 있으니.</summary>
    private const int EaterBowls = 4;

    private IEnumerator PlayBigEater(GameManager game)
    {
        OrderScreenUI screen = game.CreditsScreen;
        OrderManager orders = FindFirstObjectByType<OrderManager>();
        EatingCutscene cutscene = game.CreditsCutscene;

        if (screen == null || orders == null)
        {
            Debug.LogWarning("[크레딧] 주문 화면이나 OrderManager 가 없어 무대를 건너뜁니다.");
            yield break;
        }

        // 1. 손님을 세운다. 주문 대사는 DB 것 대신 크레딧 전용 대사를 쓴다.
        DialogueScenario scenario = MakeScenario(EaterPersona);
        if (scenario == null) yield break;

        orders.SetScenario(scenario);
        screen.Open(CreditsDay, CreditsHour, EaterOpening, 0);
        screen.SnapOpen();
        screen.HideServedBowl();

        // ⚠️ 손님을 **창이 열리기 전에** 치워 둔다.
        //
        // screen.Open 은 손님을 제자리에 세워 놓는다. 그대로 창을 열면 손님이 이미 앉아 있다가
        // 다음 순간 사라졌다 옆에서 걸어 들어온다 — 「갑자기 튀어나왔다」로 보인다.
        // 걸어 들어오기 직전 모습(옆으로 물러나 안 보이는 상태)으로 먼저 만들어 둔다.
        CustomerAppearance entering = screen.Appearance;
        if (entering != null)
        {
            entering.SetTint(0f, 0f);
            entering.ShowSilhouette(true);
        }

        // ⚠️ 말풍선도 같이 치운다. 손님만 물리고 말풍선을 두면, 창이 열리는 1.4초 동안
        // **빈 카운터 위에 「여기 너무 맛있다!」만 둥둥 떠 있다.** screen.Open 이 대사를 받아
        // 말풍선을 켜 두기 때문이고, 그걸 내리는 자리(EnterCustomer)는 창이 다 열린 뒤다.
        // 손님이 자리에 서고 나서 아래에서 다시 켠다.
        screen.ShowBubble(false);

        // 포장마차가 다 선 뒤에 창을 연다. 이 한 프레임을 기다려야 창이 열리는 순간에
        // 이미 가게가 차려져 있다 — 안 기다리면 나무 조리대가 한 번 비친다.
        yield return null;
        yield return FadeStageIn();

        // 2. 뚜벅뚜벅 걸어 들어온다. 자리에 서면 제 말투로 첫 마디를 친다.
        yield return game.CreditsWalkIn();
        screen.ShowBubble(true);
        screen.StartTypingBubble(EaterOpening);
        yield return new WaitForSecondsRealtime(OrderLineSeconds);

        // 3. 후룩후룩 — 받자마자 비우고, 「한 그릇 더!」 하고 또 받는다.
        Dictionary<IngredientType, int> recipe = orders.CurrentTargetRecipe;

        for (int bowl = 0; bowl < EaterBowls && !IsDone(Now); bowl++)
        {
            if (recipe != null && recipe.Count > 0) yield return game.CreditsPour(recipe);

            // 그릇을 먼저 감춰 둔다. OpenEating 이 그릇을 켜는데, 진하기를 미리 0 으로
            // 내려 두지 않으면 한 프레임 번쩍인 뒤에 페이드가 시작된다.
            screen.HideServedBowl();
            screen.OpenEating(CreditsDay, CreditsHour, 0);
            yield return screen.WaitForSlide();

            // 새 그릇이 스르르 놓인다. 툭 나타나면 몇 그릇째인지가 안 읽힌다.
            yield return screen.FadeServedBowlIn(BowlFadeInSeconds);

            // 먹는 것만 쓴다. 우주·번개·따봉은 여기서 안 쓴다 —
            // 이 사람의 이야기는 「맛있어서 계속 먹는다」이지 「감동했다」가 아니다.
            if (cutscene != null) yield return cutscene.PlayTutorial();
            else yield return new WaitForSecondsRealtime(3f);

            // 다 비운 그릇이 스르르 물러난다. 이게 있어야 한 그릇이 「끝났다」가 된다.
            yield return screen.FadeServedBowl(BowlFadeOutSeconds);
            yield return new WaitForSecondsRealtime(BetweenBowlsSeconds);

            game.CreditsClearBowl();

            if (bowl >= EaterBowls - 1) break;

            screen.ShowBubble(true);
            screen.StartTypingBubble(EaterMore[Mathf.Min(bowl, EaterMore.Length - 1)]);
            yield return new WaitForSecondsRealtime(MoreLineSeconds);
        }

        // 4. 후… 더 이상은 못 먹겠어.
        screen.ShowBubble(true);
        screen.StartTypingBubble(EaterDone);
        yield return new WaitForSecondsRealtime(DoneLineSeconds);
        screen.ShowBubble(false);

        // 5. 버티다 무너진다. 머리 위에 점 셋이 찍히고 나서 쓰러져야
        //    「갑자기 쓰러짐」이 아니라 「참다가 무너짐」이 된다.
        if (cutscene != null) yield return cutscene.SilentBeat();

        CustomerAppearance look = screen.Appearance;
        if (look != null) yield return look.Collapse();
        else yield return new WaitForSecondsRealtime(0.8f);

        // 6. 그걸 지켜보던 까마귀. 쓰러진 손님 모자 위에 내려앉는다.
        yield return CrowCloses(cutscene, look);

        // 7. 검은 화면 → 조리 화면 → 마지막 한 줄 → 끝.
        yield return PlayEnding();
    }

    /// <summary>
    /// 끝맺음. 아이리스가 오므린 그 검은 화면을 그대로 넘겨받아,
    /// 어둠 뒤에서 조리 화면을 차려 놓고 은은하게 드러낸다.
    ///
    /// 카드 시계와 떼어 놓았다 — 손님이 얼마나 오래 먹든 이 순서는 그대로 돈다.
    /// </summary>
    private IEnumerator PlayEnding()
    {
        endingOwned = true;

        // 아이리스가 덮고 있는 화면을 내 검은 판이 넘겨받는다. 넘겨받은 **뒤에** 아이리스를
        // 치운다 — 순서를 뒤집으면 한 프레임 동안 무대가 통째로 비친다.
        SetBlack(1f);
        yield return null;

        IrisFade iris = FindFirstObjectByType<IrisFade>(FindObjectsInactive.Include);
        if (iris != null) iris.Hide();

        // 어둠 뒤에서 조리 화면과 라멘 한 그릇을 차린다.
        yield return SetUpLastScene();

        // 검은 채로 한 박자. 바로 걷으면 장면이 바뀐 것이 아니라 깜빡인 것으로 보인다.
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        yield return FadeBlack(1f, 0f, RevealSeconds);
        yield return new WaitForSecondsRealtime(LineDelay);

        yield return FadeClosing(0f, 1f, LineInSeconds);
        yield return new WaitForSecondsRealtime(LineHoldSeconds);

        // 끝. 화면과 소리가 같이 잦아든다.
        if (song != null) StartCoroutine(FadeSong(LineOutSeconds));
        yield return FadeBlack(0f, 1f, LineOutSeconds);

        endingDone = true;
    }

    /// <summary>아이리스를 넘겨받고 조리 화면을 드러내기까지 검은 채로 두는 시간(초).</summary>
    private const float BlackHoldSeconds = 1.2f;

    /// <summary>창이 열리는 시간(초). 검은 화면에서 가게가 배어 나온다.</summary>
    private const float StageInSeconds = 1.4f;

    private IEnumerator FadeStageIn()
    {
        if (stageAway == null) yield break;

        for (float t = 0f; t < StageInSeconds; t += Time.unscaledDeltaTime)
        {
            stageAway.alpha = Mathf.SmoothStep(0f, 1f, t / StageInSeconds);
            yield return null;
        }
        stageAway.alpha = 1f;
    }

    private void SetBlack(float alpha)
    {
        if (blackout == null) return;

        Color c = blackout.color;
        blackout.color = new Color(c.r, c.g, c.b, alpha);
        blackout.enabled = alpha > 0.001f;
    }

    private IEnumerator FadeBlack(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetBlack(Mathf.SmoothStep(from, to, t / seconds));
            yield return null;
        }
        SetBlack(to);
    }

    private IEnumerator FadeClosing(float from, float to, float seconds)
    {
        if (closingGroup == null) yield break;

        var rect = (RectTransform)closingGroup.transform;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(from, to, t / seconds);
            closingGroup.alpha = k;
            rect.anchoredPosition = new Vector2(0f, -Mathf.Round(RiseHeight * (1f - k)));
            yield return null;
        }

        closingGroup.alpha = to;
        rect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator FadeSong(float seconds)
    {
        float from = song.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            song.volume = Mathf.Lerp(from, 0f, t / seconds);
            yield return null;
        }
        song.volume = 0f;
    }

    /// <summary>한 마디를 읽히는 시간(초).</summary>
    private const float MoreLineSeconds = 1.6f;
    private const float DoneLineSeconds = 2.6f;

    // ── 그릇이 오가는 박자 ────────────────────────────────────────
    //
    // 그릇이 툭 나타났다 툭 사라지면 한 손님이 네 그릇을 비워도 「같은 그릇을 계속 먹는다」로
    // 보인다. 스르르 놓이고 스르르 물러나야 한 그릇이 끝나고 다음 그릇이 온 것이 읽힌다.

    private const float BowlFadeInSeconds = 0.7f;
    private const float BowlFadeOutSeconds = 0.6f;

    /// <summary>빈 그릇이 물러나고 다음 그릇이 오기까지 두는 짬(초).</summary>
    private const float BetweenBowlsSeconds = 0.5f;

    /// <summary>
    /// 콰당 — 손님이 옆으로 넘어간다.
    ///
    /// 넘어지는 그림이 따로 없어서 자리를 통째로 기울이고 떨군다. 픽셀아트를 돌리면
    /// 획이 지저분해지지만, 한 번 휙 지나가는 개그라 그 거칠음이 오히려 맞는다.
    /// 기울면서 가속한다 — 등속으로 넘어가면 쓰러지는 게 아니라 눕는 것으로 보인다.
    /// </summary>
    /// <summary>
    /// 까마귀가 쓰러진 손님 모자 위에 내려앉고, 그 까마귀를 한가운데 두고 화면이 오므라든다.
    ///
    /// 앉는 자리는 다른 세션이 실측한 값이다 — 185칸 가라앉힌 뒤 모자 꼭대기가 캔버스 y = −6
    /// 이라, 발이 모자에 닿으려면 y = 4 여야 한다. <b>Collapse 의 깊이를 바꾸면 이 값도
    /// 다시 재야 한다.</b>
    /// </summary>
    private IEnumerator CrowCloses(EatingCutscene cutscene, CustomerAppearance look)
    {
        IrisFade iris = FindFirstObjectByType<IrisFade>(FindObjectsInactive.Include);

        if (cutscene != null)
        {
            yield return cutscene.CrowLandOn(CrowSeatX, CrowSeatY, CrowFlySeconds);

            // 앉고 나서 한 번 더 운다. 내려오면서 한 번(CrowLandOn), 자리 잡고 한 번.
            // 한 번만 울면 지나가다 앉은 새가 아니라 소리만 난 것으로 들린다.
            //
            // 세기 0.45 는 첫 울음과 같은 값이다(CutsceneSfx.cawVolume). 다르게 주면
            // 두 번째가 다른 새처럼 들린다 — 같은 새가 두 번 우는 것이어야 한다.
            yield return new WaitForSecondsRealtime(CrowSettleSeconds);
            Sfx.Play("sfx_cut_crow", 0.45f, Random.Range(0.94f, 1.06f));

            // 인사를 여기서 걷는다. 왼쪽 칸이 비고 나서야 까마귀만 남는다 —
            // 글자가 붙어 있는 채로 아이리스가 오므리면 조여드는 것이 둘로 갈린다.
            yield return FadeThanks(ThanksOutSeconds);

            yield return new WaitForSecondsRealtime(CrowWatchSeconds);
        }

        if (iris != null)
        {
            // ⚠️ 까마귀 자리를 **그대로** 넘긴다. 변환하지 않는다.
            //
            // 아이리스 판도 까마귀도 둘 다 Frame 안에 있다. Frame 이 창으로 줄고 옮겨진 것은
            // 둘에게 똑같이 걸리므로, 같은 좌표계에서 이미 맞아 있다. 여기서 화면 좌표로
            // 한 번 더 옮기면(× 0.5 + 240) 변환이 두 번 걸려 오른쪽으로 120칸 밀린다 —
            // 실제로 그렇게 엉뚱한 데가 조여졌다.
            Vector2 spot = cutscene != null ? cutscene.CrowSpot : Vector2.zero;
            yield return iris.CloseTo(spot, IrisCloseSeconds, IrisHoldRadius, IrisHoldSeconds);
        }

        // 어둠 뒤에서 무대를 치운다. 점과 까마귀를 안 지우면 마지막 장면에 얹혀 나온다.
        if (cutscene != null)
        {
            cutscene.ClearDots();
            cutscene.HideCrow();
        }
        if (look != null) look.Reseat();

        // 가게 소리도 아이리스와 같이 닫는다. 화면은 오므라들었는데 길거리 소리가 계속 나면
        // 「아직 거기 있다」가 되어 장면이 안 끊긴다.
        Sfx.Stop("amb_street_night", 1.2f);
        Sfx.Stop("amb_broth_boil", 1.2f);
    }

    /// <summary>
    /// 까마귀가 앉는 자리. 쓰러진 손님 <b>모자 꼭대기</b>다.
    ///
    /// 세 높이를 세워 놓고 픽셀로 비교해 골랐다 —
    ///   4 발이 모자에 파묻힌다 · <b>12 발이 꼭대기에 닿는다</b> · 20 떠 있다
    ///
    /// x 는 0 이다. −120 으로 두었더니 손님(모자 중심 x ≈ −5)에서 한참 왼쪽,
    /// 아무것도 없는 허공에 앉았다.
    /// <b>Collapse 의 깊이(185)를 바꾸면 y 를 다시 재야 한다.</b>
    /// </summary>
    private const float CrowSeatX = 0f;
    private const float CrowSeatY = 12f;

    /// <summary>날아와 앉기까지(초). 내려앉는 데 뒤쪽 45% 를 쓰므로 늘리면 착륙이 느려진다.</summary>
    private const float CrowFlySeconds = 3.4f;

    /// <summary>앉고 나서 두 번째 「까악」이 나기까지(초).</summary>
    private const float CrowSettleSeconds = 0.6f;
    private const float CrowWatchSeconds = 1.2f;

    /// <summary>
    /// 아이리스가 오므리는 데 걸리는 시간(초). 멈춰 서 있는 <see cref="IrisHoldSeconds"/> 는
    /// 여기 안 든다 — 멈춤을 늘려도 닫히는 속도는 그대로다.
    /// </summary>
    private const float IrisCloseSeconds = 1.8f;

    /// <summary>
    /// 오므리다 멈춰 서는 반지름. <b>까마귀에 딱 맞는 크기다.</b>
    ///
    /// 녹화본에서 새를 재서 잡았다 — 몸통이 기기 픽셀로 53 x 38, 대각선이 65 라
    /// 반지름 33 이면 날개 끝이 원에 닿는다. 조금 띄워 38(지름 76)로 둔다.
    /// 아이리스 판은 창 안(Frame, 0.5배)에 있고 캔버스는 다시 2배라서, <b>여기 1칸이
    /// 곧 기기 1픽셀</b>이다. 창 배율을 바꾸면 이 값도 다시 재야 한다.
    /// </summary>
    private const float IrisHoldRadius = 38f;

    /// <summary>그 크기에서 서 있는 시간(초).</summary>
    private const float IrisHoldSeconds = 1.4f;

    private IEnumerator PlayShot(GameManager game, Shot shot, int index)
    {
        OrderScreenUI screen = game.CreditsScreen;
        OrderManager orders = FindFirstObjectByType<OrderManager>();
        RamenCalculator calculator = FindFirstObjectByType<RamenCalculator>();

        if (screen == null || orders == null)
        {
            Debug.LogWarning("[크레딧] 주문 화면이나 OrderManager 가 없어 이 손님을 건너뜁니다.");
            yield break;
        }

        // 1. 이 손님으로 주문을 세운다. 말투가 정해지면 얼굴도 대사도 그걸 따라온다.
        DialogueScenario scenario = MakeScenario(shot.Persona);
        if (scenario == null) yield break;

        orders.SetScenario(scenario);
        screen.Open(CreditsDay, CreditsHour, orders.CurrentDialogue, 0);
        screen.SnapOpen();

        // 2. 걸어 들어와 자리에 서고, 제 말투로 주문을 말한다.
        yield return game.CreditsWalkIn();
        DumpCustomer(shot.Persona + " 입장 직후");
        yield return new WaitForSecondsRealtime(OrderLineSeconds);

        // 3. 그릇을 몇 번 비우는지. QA 자리만 셋이다.
        for (int n = 0; n < Mathf.Max(1, shot.Bowls); n++)
        {
            yield return ServeOnce(game, screen, orders, calculator, shot, n == 0);
        }

        // 4. 나간다.
        game.CreditsClearBowl();
        yield return game.CreditsWalkOut();
        yield return new WaitForSecondsRealtime(EmptyCounterSeconds);
    }

    /// <summary>한 그릇. 담고 → 내고 → 채점하고 → 시식 연출.</summary>
    private IEnumerator ServeOnce(GameManager game, OrderScreenUI screen, OrderManager orders,
                                  RamenCalculator calculator, Shot shot, bool first)
    {
        Dictionary<IngredientType, int> target = orders.CurrentTargetRecipe;
        if (target == null || target.Count == 0) yield break;

        Dictionary<IngredientType, int> serving = BuildServing(target, shot.Take);

        // 담는다. 화면은 주문 화면이 덮고 있어 보이지 않고, 소리만 난다.
        yield return game.CreditsPour(serving);

        // 손님 앞에 그릇이 놓인다.
        screen.OpenEating(CreditsDay, CreditsHour, 0);
        yield return screen.WaitForSlide();

        screen.ShowBubble(true);
        screen.StartTypingBubble(ReactionLines.Served());
        yield return new WaitForSecondsRealtime(ServedLineSeconds);

        // 채점은 실제로 돈다. 정확도가 곧 시식 연출의 갈래다.
        orders.EvaluateRamen(new RamenState(serving, new Dictionary<IngredientType, int>()));
        float accuracy = calculator != null ? calculator.LastAccuracy : 100f;

        if (first)
        {
            Debug.Log(string.Format("[크레딧] {0}  노린 것 {1}  실제 {2:F1}%  → {3}",
                                    shot.Persona, shot.Take, accuracy, Branch(accuracy)));
        }

        EatingCutscene cutscene = game.CreditsCutscene;
        if (cutscene != null) yield return cutscene.Play(accuracy);
        else yield return new WaitForSecondsRealtime(4f);

        game.CreditsClearBowl();
    }

    /// <summary>
    /// 손님 자리가 지금 어떤 꼴인지 콘솔에 적는다. 「사람이 안 보인다」를 눈이 아니라
    /// 숫자로 잡으려고 둔 것이다. 원인이 잡히면 지운다.
    /// </summary>
    private static void DumpCustomer(string when)
    {
        Transform frame = FindFrame();
        Transform slot = frame != null ? frame.Find("OrderScreen/CustomerSlot") : null;

        if (slot == null)
        {
            Debug.LogWarning("[크레딧|계측] " + when + " — CustomerSlot 을 못 찾았습니다.");
            return;
        }

        // 한 줄씩 따로 찍는다. 여러 줄을 한 번에 찍으면 유니티 콘솔 목록에 첫 줄만 보여서
        // 정작 알고 싶은 내용이 안 읽힌다. 실제로 그래서 한 번 헛다리를 짚었다.
        Debug.Log("[크레딧|계측] " + when + "  slot켜짐=" + slot.gameObject.activeInHierarchy
                  + " 자식=" + slot.childCount + " localScale=" + slot.localScale);

        foreach (Image image in slot.GetComponentsInChildren<Image>(true))
        {
            var rt = image.rectTransform;
            Debug.Log("[크레딧|계측]    " + image.name
                      + "  켜짐=" + image.gameObject.activeInHierarchy
                      + " enabled=" + image.enabled
                      + " 그림=" + (image.sprite != null ? image.sprite.name : "없음")
                      + " 색=" + image.color
                      + " 자리=" + rt.anchoredPosition
                      + " 크기=" + rt.sizeDelta);
        }
    }

    /// <summary>크레딧에 뜨는 날짜·시각. 화면에 안 보이지만 인자로는 있어야 한다.</summary>
    private const int CreditsDay = 5;
    private const int CreditsHour = 23;

    /// <summary>
    /// 노린 반응이 나오도록 그릇을 담는다.
    ///
    /// 정확도는 토핑 오차로만 난다(RamenCalculator) — 100 - 오차/정답 * 100.
    /// 다만 타래·육수·면 셋 중 하나라도 틀리면 그 자리에서 0% 다. Bad 는 그걸 쓴다.
    /// </summary>
    private static Dictionary<IngredientType, int> BuildServing(
        Dictionary<IngredientType, int> target, Take take)
    {
        var serving = new Dictionary<IngredientType, int>(target);

        if (take == Take.Perfect) return serving;

        if (take == Take.Bad)
        {
            // 타래를 엉뚱한 것으로 바꾼다. 3대 요소 미달이라 0% 가 되고 까마귀가 난다.
            foreach (IngredientType type in new List<IngredientType>(serving.Keys))
            {
                if (!IsBase(type)) continue;

                serving.Remove(type);
                serving[OtherBase(type)] = 1;
                return serving;
            }
            return serving;
        }

        // Good·Okay — 토핑을 몇 개 빼서 노리는 구간에 떨어뜨린다.
        float low = take == Take.Good ? 86f : 71f;
        float high = take == Take.Good ? 98f : 84f;

        var toppings = new List<IngredientType>();
        int total = 0;
        foreach (var pair in serving)
        {
            if (IsBase(pair.Key) || IsBroth(pair.Key) || IsNoodle(pair.Key)) continue;
            for (int n = 0; n < pair.Value; n++) toppings.Add(pair.Key);
            total += pair.Value;
        }

        if (total == 0) return serving;

        // 하나씩 빼 보면서 구간에 들어가는 첫 자리에서 멈춘다.
        for (int drop = 1; drop <= toppings.Count; drop++)
        {
            float accuracy = 100f - (float)drop / total * 100f;
            if (accuracy > high) continue;

            if (accuracy < low)
            {
                Debug.LogWarning(string.Format(
                    "[크레딧] 토핑이 {0}개뿐이라 {1} 구간({2}~{3}%)을 못 맞춥니다. {4:F1}% 로 갑니다.",
                    total, take, low, high, accuracy));
            }

            for (int n = 0; n < drop; n++)
            {
                IngredientType type = toppings[n];
                serving[type] = serving[type] - 1;
                if (serving[type] <= 0) serving.Remove(type);
            }
            return serving;
        }

        return serving;
    }

    private static string Branch(float accuracy)
    {
        if (accuracy >= 99.5f) return "우주·번개";
        if (accuracy >= 85f) return "흐뭇";
        if (accuracy >= 70f) return "갸웃";
        return "까마귀";
    }

    /// <summary>
    /// 그 말투의 손님이 나올 때까지 주문을 다시 뽑는다.
    ///
    /// 생성기(B 영역)는 말투를 무작위로 고르고 지정하는 문이 없다. 그쪽을 고치는 대신
    /// 여기서 다시 뽑는다 — 말투가 열넷이라 몇 번이면 걸리고, B 코드는 한 줄도 안 건드린다.
    /// </summary>
    private static DialogueScenario MakeScenario(string persona)
    {
        const int Tries = 60;

        // OrderManager.CreateOrder 가 아니라 생성기를 바로 부른다. 그쪽은 뽑을 때마다
        // 주문 상세를 콘솔에 박스로 찍어서, 예순 번 다시 뽑으면 콘솔이 통째로 묻힌다.
        // 여기서 뽑고 SetScenario 로 한 번만 꽂으면 로그도 한 줄이다.
        var maker = FindFirstObjectByType<DialogueScenarioGenerator>();
        if (maker == null)
        {
            Debug.LogWarning("[크레딧] 씬에 DialogueScenarioGenerator 가 없습니다.");
            return null;
        }

        DialogueScenario fallback = null;

        for (int i = 0; i < Tries; i++)
        {
            DialogueScenario made = maker.GenerateScenario(CreditsDay);
            if (made == null) continue;

            fallback = made;
            if (made.personaId == persona) return made;
        }

        Debug.LogWarning("[크레딧] 말투 '" + persona + "' 를 " + Tries + "번 안에 못 뽑았습니다. 나온 것으로 갑니다.");
        return fallback;
    }

    private static bool IsBase(IngredientType t)
    {
        return t == IngredientType.ShioTare || t == IngredientType.ShoyuTare
               || t == IngredientType.TonkotsuBase;
    }

    private static IngredientType OtherBase(IngredientType t)
    {
        return t == IngredientType.ShioTare ? IngredientType.TonkotsuBase : IngredientType.ShioTare;
    }

    private static bool IsBroth(IngredientType t)
    {
        return t == IngredientType.Broth;
    }

    private static bool IsNoodle(IngredientType t)
    {
        return t == IngredientType.ThickNoodles || t == IngredientType.ThinNoodles;
    }

    /// <summary>크레딧을 닫고 돌아간다. 화면은 이미 <see cref="blackout"/> 이 덮고 있다.</summary>
    private IEnumerator Finish()
    {
        // 검은 판을 그대로 넘겨받는다. 여기서 또 덮으면 다 검어진 화면을 한 번 더 덮는 셈이라
        // 아무 일도 안 일어난 채 2초가 흐른다.
        ScreenFade fade = ScreenFade.Instance;
        if (fade != null) fade.HoldBlack();

        // 남은 소리를 전부 닫는다. 씬을 다시 열면 타이틀 BGM 이 처음부터 켜지므로,
        // 여기서 안 끄면 두 곡이 잠깐 겹쳐 난다.
        Sfx.Stop("amb_broth_boil", ClosingSilenceSeconds);
        Sfx.Stop("amb_street_night", ClosingSilenceSeconds);
        Sfx.Stop("amb_noodle_pot", ClosingSilenceSeconds);

        // 엔딩곡은 끝맺음이 이미 0 까지 내려놓았다(PlayEnding → FadeSong). 그래도 여기서 한 번
        // 더 끊는다 — 노래가 짧아 먼저 끝났거나, 개발용 건너뛰기로 끝맺음을 지나온 경우가 있다.
        if (song != null) song.Stop();

        // **완전히 검고 조용해진 자리에 머문다.** 바로 씬을 열면 위 페이드가 통째로 잘려
        // 소리가 툭 끊기고 곧장 타이틀 곡이 난다. 이 사이가 크레딧의 마지막 박자다.
        yield return new WaitForSecondsRealtime(ClosingSilenceSeconds);

        Debug.Log("[크레딧] 끝. " + (exit == Exit.Title ? "타이틀로 돌아갑니다." : "씬을 다시 엽니다."));

        // 어느 쪽이든 씬을 다시 연다. 시작 화면은 씬을 열면 저절로 떠 있다(TitleScreenUI.Awake).
        // 5일 완주에서 들어온 길도 같은 자리로 돌아가는 것이 맞다 — 이어서 할 판이 없다.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── 소리 ──────────────────────────────────────────────────────

    private void StartSong()
    {
        // 곡(타이틀·가게)은 이미 Prelude 가 화면과 같이 잦아들게 해서 꺼 두었다.
        // 여기서 또 끄지 않는다 — 끄는 시각을 두 군데에 적어 두면 한쪽만 고치게 된다.
        // 발소리·조리 효과음은 그대로 둔다. 라멘집이 돌아가는 소리가 곡 위에 얹히는 것이
        // 이 게임다움이다.

        // 가게 소리는 낮게 깔아 둔다. 엔딩곡만 남기면 화면은 가게인데 소리는 빈 방이 된다.
        // 크기는 주문 화면이 덮였을 때 쓰는 값(Sfx.KitchenAmbienceCovered)보다도 낮게 —
        // 곡이 주인공이고 이쪽은 「저 안에서 가게가 돌아간다」는 기척만 내면 된다.
        Sfx.Loop("amb_street_night", StreetUnderSong, 2f);
        Sfx.Loop("amb_broth_boil", KitchenUnderSong, 2f);

        AudioClip clip = Sfx.Clip("bgm_credits");
        if (clip == null)
        {
            Debug.LogWarning("[크레딧] 엔딩곡이 없습니다: Resources/Audio/bgm_credits. 실시간으로 진행합니다.");
            return;
        }

        songSeconds = clip.length;

        song = gameObject.AddComponent<AudioSource>();
        song.clip = clip;
        song.loop = false;              // 넘치면 다시 시작하는 것이 아니라 그냥 끝나야 한다
        song.playOnAwake = false;
        song.volume = SongVolume;
        song.Play();

        Debug.Log(string.Format(
            "[크레딧] 글자 {0:F1}초까지 (엔딩곡은 {1:F1}초짜리라 다 안 씁니다)."
            + " 카드 {2}장 x {3:F1}초. 그 뒤 끝맺음은 무대가 제 속도로 몹니다.",
            TotalSeconds, songSeconds,
            GameManager.CreditLines.GetLength(0) + Logos.Length, CardSeconds));
    }

    // ── 개발용 ────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// 확인용 두 가지. 에디터에서만 듣는다.
    ///
    ///   F7  다음 쌍이 뜨는 자리로 시계를 밀어 놓는다. 31초를 안 기다리고 넘어가는 것만 본다.
    ///   F8  빠른 미리보기를 켰다 끈다. 노래도 시계를 따라 같이 옮겨진다.
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f7Key.wasPressedThisFrame)
        {
            clock = (Mathf.FloorToInt(Now / PairSeconds) + 1) * PairSeconds;
            if (song != null) song.time = Mathf.Min(clock, songSeconds - 0.1f);
            Debug.Log(string.Format("[크레딧] {0:F1}s 로 건너뜁니다.", clock));
        }

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            fastForward = !fastForward;
            clock = song != null ? song.time : clock;
            if (song != null) song.volume = fastForward ? SongVolume * 0.4f : SongVolume;

            // ⚠️ 빨라지는 것은 **글자와 노래뿐**이다. 무대(손님·시식 연출)는 코루틴이라
            // 그대로 흐르므로, 켜 두면 글자가 손님을 앞질러 간다. 글자 박자만 볼 때 쓸 것.
            Debug.Log("[크레딧] 빠른 미리보기 " + (fastForward ? "켬 (x" + FastFactor + ") — 글자만 빨라집니다" : "끔"));
        }

        // 빠른 미리보기 중에는 노래를 시계에 맞춰 끌고 간다.
        if (fastForward && song != null && Mathf.Abs(song.time - clock) > 1.5f)
        {
            song.time = Mathf.Min(clock, songSeconds - 0.1f);
        }
    }
#endif

#if UNITY_EDITOR
    /// <summary>
    /// Play 를 안 켜고 한 장면만 화면에 세워 본다. 확인용 사진을 찍는 쪽(CreditsPreview)이 쓴다.
    ///
    /// 진행(코루틴)은 돌리지 않는다. 글자만 자리에 놓고 그대로 선다.
    /// 만든 것에는 전부 <see cref="HideFlags.DontSaveInEditor"/> 가 붙어 씬에 저장되지 않는다 —
    /// 에디터 하나를 여러 세션이 같이 쓰므로, 남이 씬을 저장해도 이것이 딸려 들어가면 안 된다.
    /// </summary>
    public static CreditsSequence BuildPreview()
    {
        // 앞서 남은 판을 먼저 치운다. 에디터가 찍는 도중에 꺼지면 DestroyPreview 가 못 돌아서
        // 판이 씬에 그대로 남는다. 그게 네 벌 쌓여서 화면을 덮은 적이 있다.
        ClearLeftovers();

        var go = new GameObject("CreditsPreview") { hideFlags = HideFlags.HideAndDontSave };
        var credits = go.AddComponent<CreditsSequence>();

        AudioClip clip = Sfx.Clip("bgm_credits");
        if (clip != null) credits.songSeconds = clip.length;

        credits.BuildScreen();
        if (credits.overlayRoot != null) MarkDontSave(credits.overlayRoot.transform);
        if (credits.backdropRoot != null) MarkDontSave(credits.backdropRoot.transform);

        return credits;
    }

    /// <summary>
    /// 마지막 한 줄만 올린 상태로 세운다. 조리대 위에서 글자 자리를 맞춰 보는 데 쓴다.
    /// 창은 접어 둔다 — 마지막 장면에서는 무대가 온 화면이라 창이 없다.
    /// </summary>
    public void ShowClosingPreview()
    {
        if (stageFrame != null)
        {
            stageFrame.localScale = Vector3.one;
            stageFrame.anchoredPosition = Vector2.zero;
        }

        if (columnGroup != null) columnGroup.alpha = 0f;
        if (stageAway != null) stageAway.alpha = 1f;
        if (blackout != null) blackout.enabled = false;
        if (backdropRoot != null) backdropRoot.SetActive(false);
        if (closingGroup != null)
        {
            closingGroup.alpha = 1f;
            ((RectTransform)closingGroup.transform).anchoredPosition = Vector2.zero;
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>미리보기를 t 초 시점의 모습으로 세운다. 실제 진행과 같은 <see cref="SeekTo"/> 를 쓴다.</summary>
    public void SeekPreview(float t)
    {
        // 창은 실제로는 손님이 선 뒤에 스르르 열린다(FadeStageIn). 확인용 렌더는 코루틴이
        // 안 돌아 계속 감춰진 채로 찍히므로, 여기서는 열어 둔 것으로 친다.
        if (stageAway != null) stageAway.alpha = 1f;

        SeekTo(t);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 미리보기로 만든 것을 전부 지우고 무대를 제자리로 돌린다.
    ///
    /// 무대를 안 되돌리면 씬의 Frame 이 절반으로 줄어든 채 남아서, 에디터를 같이 쓰는
    /// 다른 세션이 그 상태로 작업하게 된다.
    /// </summary>
    public void DestroyPreview()
    {
        if (stageFrame != null)
        {
            stageFrame.localScale = stageScaleWas;
            stageFrame.anchoredPosition = stagePosWas;
            stageFrame.gameObject.SetActive(true);
        }

        if (stageAway != null)
        {
            stageAway.alpha = 1f;
            if (addedStageGroup) DestroyImmediate(stageAway);
        }

        if (stageMask != null) DestroyImmediate(stageMask);
        for (int i = 0; i < stageInnerMasks.Count; i++)
        {
            if (stageInnerMasks[i] != null) DestroyImmediate(stageInnerMasks[i]);
        }
        stageInnerMasks.Clear();
        if (overlayRoot != null) DestroyImmediate(overlayRoot);
        if (backdropRoot != null) DestroyImmediate(backdropRoot);

        DestroyImmediate(gameObject);
    }

    /// <summary>
    /// 씬에 남아 있는 크레딧 판을 전부 지우고 무대를 제자리로 돌린다.
    ///
    /// 되돌릴 값을 들고 있던 객체가 이미 사라진 뒤라, 무대는 기본값(1배·가운데)으로 돌린다.
    /// 빌더가 세우는 값이 그것이라 맞다.
    /// </summary>
    public static void ClearLeftovers()
    {
        Transform frame = FindFrame();
        if (frame == null) return;

        Transform canvasRoot = frame.parent;
        for (int i = canvasRoot.childCount - 1; i >= 0; i--)
        {
            Transform t = canvasRoot.GetChild(i);
            if (t.name == "CreditsOverlay" || t.name == "CreditsBackdrop") DestroyImmediate(t.gameObject);
        }

        foreach (CreditsSequence stale in FindObjectsByType<CreditsSequence>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (stale != null) DestroyImmediate(stale.gameObject);
        }

        // 자르개는 Frame 과 그 안의 중첩 Canvas 마다 붙여 두므로 전부 훑어 뗀다.
        foreach (RectMask2D mask in frame.GetComponentsInChildren<RectMask2D>(true))
        {
            DestroyImmediate(mask);
        }

        var group = frame.GetComponent<CanvasGroup>();
        if (group != null) DestroyImmediate(group);

        frame.localScale = Vector3.one;
        ((RectTransform)frame).anchoredPosition = Vector2.zero;
        frame.gameObject.SetActive(true);
    }

    private static void MarkDontSave(Transform t)
    {
        t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        for (int i = 0; i < t.childCount; i++) MarkDontSave(t.GetChild(i));
    }
#endif

    // ── 화면 만들기 ────────────────────────────────────────────────

    private void BuildScreen()
    {
        Transform frame = FindFrame();
        if (frame == null)
        {
            Debug.LogWarning("[크레딧] 캔버스의 Frame 을 못 찾았습니다. 빌더를 한 번 돌려 주세요.");
            return;
        }

        TMP_FontAsset font = FindFont();
        Transform canvasRoot = frame.parent;

        ShrinkStage(frame);
        BuildBackdrop(canvasRoot);

        // 글자판은 **Frame 바깥**(캔버스 직속)에 만든다. 안에 두면 무대와 같이 절반으로 줄어든다.
        var root = (RectTransform)NewRect("CreditsOverlay", canvasRoot);
        Center(root);

        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = CreditsOrder;
        root.gameObject.AddComponent<GraphicRaycaster>();

        // 입력 막이. 투명하지만 클릭을 먹는다. 크레딧 중에는 아무것도 눌리면 안 된다.
        var blocker = NewImage("Blocker", root);
        Stretch(blocker.rectTransform);
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        BuildColumn(root, font);

        // 마지막 한 줄. 화면 한가운데다 — 칸도 창도 걷힌 자리에 이것만 남는다.
        var closing = (RectTransform)NewRect("Closing", root);
        Center(closing);
        closingGroup = closing.gameObject.AddComponent<CanvasGroup>();
        closingGroup.blocksRaycasts = false;
        closingGroup.alpha = 0f;

        // 조리대를 한 단계 죽인다. 나무와 양은 그릇이 밝아서 글자만으로는 눈이 안 간다.
        Image kitchenDim = NewImage("ClosingDim", closing);
        Stretch(kitchenDim.rectTransform);
        kitchenDim.color = new Color(0.01f, 0.01f, 0.02f, ClosingDimAlpha);
        kitchenDim.raycastTarget = false;

        // 글 뒤에 어두운 판을 깐다. 이 한 줄만은 조리 화면(밝은 나무와 그릇) 위에 뜨므로,
        // 판이 없으면 글자가 배경에 묻힌다. 위아래 양 끝으로 사라져 띠처럼 안 보인다.
        var linePos = new Vector2(0f, ClosingLineY);

        Image plate = NewImage("ClosingPlate", closing);
        plate.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        plate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        plate.rectTransform.anchoredPosition = linePos;
        plate.rectTransform.sizeDelta = new Vector2(960f, 150f);
        plate.color = new Color(0.01f, 0.01f, 0.02f, 1f);
        plate.raycastTarget = false;
        plate.gameObject.AddComponent<UiVerticalGradient>().Set(0.72f, 0f, 40, 1.6f, true);

        var closingText = NewText("ClosingLine", closing, linePos, new Vector2(900f, 60f),
                                  ClosingFontSize, RoleInk, font);
        closingText.text = GameManager.CreditClosingLine;
        AttachShadow(closingText, closing, linePos, font);
        foreach (TextMeshProUGUI copy in closing.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            copy.text = GameManager.CreditClosingLine;
        }

        // 여운의 검은 판. 맨 마지막 형제라 글자까지 덮는다.
        blackout = NewImage("Blackout", root);
        Stretch(blackout.rectTransform);
        blackout.color = new Color(0f, 0f, 0f, 0f);
        blackout.raycastTarget = false;
        blackout.enabled = false;

        overlayRoot = root.gameObject;
    }

    /// <summary>
    /// 무대를 오른쪽 창으로 줄인다.
    ///
    /// 자르개(RectMask2D)를 같이 붙인다. 주문 화면의 밤 배경은 판(960x540)보다 크게 잡혀
    /// 있어서(16:9 아닌 창을 메우려고) 절반으로 줄여도 창 밖으로 삐져나온다.
    /// </summary>
    private void ShrinkStage(Transform frame)
    {
        stageFrame = (RectTransform)frame;
        stageScaleWas = stageFrame.localScale;
        stagePosWas = stageFrame.anchoredPosition;

        stageFrame.localScale = new Vector3(StageScale, StageScale, 1f);
        stageFrame.anchoredPosition = StageCenter;

        // padding 은 자르는 칸만 줄인다 — 자식들의 자리는 하나도 안 건드린다.
        // 무대 크기를 줄이면 그 안의 배치가 통째로 틀어지므로 이쪽이 맞다.
        stageMask = Clip(stageFrame);

        // ⚠️ 자르개 하나로는 모자란다. RectMask2D 는 **중첩 Canvas 를 못 넘는다** —
        // OrderScreen 이 제 Canvas(overrideSorting)를 얹고 있어서, Frame 에 붙인 자르개가
        // 그 안쪽에는 아예 안 먹힌다. 시식 컷신의 확대·우주·검은 바가 창 밖으로 통째로
        // 쏟아져 나온 것이 그 때문이다. 그래서 Canvas 를 가진 자식마다 따로 붙인다.
        stageInnerMasks.Clear();
        foreach (Canvas nested in stageFrame.GetComponentsInChildren<Canvas>(true))
        {
            if (nested.transform == stageFrame) continue;

            RectMask2D mask = Clip((RectTransform)nested.transform);
            if (mask != null) stageInnerMasks.Add(mask);
        }

        // 창을 통째로 흐리게 하는 손잡이. 마지막에 이것만 내린다 — 뒷판(검은 판)은
        // 끝까지 불투명하게 둬야 그 뒤의 조리 화면이 안 드러난다.
        stageAway = stageFrame.GetComponent<CanvasGroup>();
        if (stageAway == null)
        {
            stageAway = stageFrame.gameObject.AddComponent<CanvasGroup>();
            addedStageGroup = true;
        }

        // 창을 감춘 채로 시작한다. 크레딧이 켜지는 순간에는 아직 포장마차가 서기 전이라
        // 조리대 나무나 밤 배경(#14100E, 짙은 갈색)이 창에 비친다 — 검은 화면 옆에 갈색
        // 네모가 뜬 꼴이다. 손님을 세운 뒤에 스르르 연다(PlayBigEater).
        stageAway.alpha = 0f;
    }

    /// <summary>같은 자리로 자르는 자르개를 붙인다. 이미 있으면 그대로 두고 null 을 준다.</summary>
    private static RectMask2D Clip(RectTransform target)
    {
        if (target.GetComponent<RectMask2D>() != null) return null;

        RectMask2D mask = target.gameObject.AddComponent<RectMask2D>();
        mask.padding = StageCrop;
        return mask;
    }

    private readonly List<RectMask2D> stageInnerMasks = new List<RectMask2D>();
    private CanvasGroup stageAway;
    private bool addedStageGroup;

    /// <summary>창 바깥을 덮는 판과 창 테두리. 창(주문 화면 180)보다 뒤에 깔린다.</summary>
    private void BuildBackdrop(Transform canvasRoot)
    {
        var root = (RectTransform)NewRect("CreditsBackdrop", canvasRoot);
        Center(root);

        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = BackdropOrder;

        stageGroup = root.gameObject.AddComponent<CanvasGroup>();
        stageGroup.blocksRaycasts = false;

        // 판 하나뿐이다. 창 테두리는 긋지 않는다 — 한 칸짜리 희미한 선을 둘러 봤더니
        // 창이 「검은 종이에 붙인 사진」처럼 보였다. 테두리가 없어야 창 안만 남는다.
        var fill = NewImage("Fill", root);
        Stretch(fill.rectTransform);
        fill.color = BackdropInk;
        fill.raycastTarget = false;

        backdropRoot = root.gameObject;
    }

    /// <summary>
    /// 왼쪽 글자 칸. 줄 하나가 역할·이름 한 쌍이다.
    ///
    /// 여섯 줄을 <b>모두 같은 자리에 겹쳐</b> 만들어 두고, 제 차례에만 한 줄씩 켠다.
    /// 글을 바꿔 끼우지 않고 통째로 미리 만들어 두는 까닭은, 글자를 갈아 끼우면 TMP 가
    /// 그 프레임에 메시를 다시 뜨면서 한 번 깜빡이기 때문이다. 여섯 벌은 얼마 되지 않는다.
    /// </summary>
    private void BuildColumn(Transform parent, TMP_FontAsset font)
    {
        var column = (RectTransform)NewRect("Column", parent);
        Center(column);
        columnGroup = column.gameObject.AddComponent<CanvasGroup>();
        columnGroup.blocksRaycasts = false;

        int count = GameManager.CreditLines.GetLength(0);

        // 글자 카드 뒤에 로고가 한 장씩 붙는다. 셋을 한 카드에 몰아 넣었더니
        // 「같이 박혀 있는 판」으로 보여서, 이름 카드처럼 하나씩 넘어가게 갈랐다.
        // 맨 끝 한 장이 인사(Thanks for Playing)다 — 이것만 저 혼자 안 넘어가고 계속 서 있다.
        lineGroups = new CanvasGroup[count + Logos.Length + 1];

        var rolePos = new Vector2(ColumnCenterX, CardRoleY);
        var namePos = new Vector2(ColumnCenterX, CardNameY);

        for (int i = 0; i < count; i++)
        {
            var block = (RectTransform)NewRect("Line" + i, column);
            Center(block);

            lineGroups[i] = block.gameObject.AddComponent<CanvasGroup>();
            lineGroups[i].blocksRaycasts = false;
            lineGroups[i].alpha = 0f;

            var role = NewText("Role", block, rolePos, new Vector2(ColumnWidth, RoleBoxHeight),
                               RoleFontSize, RoleInk, font);
            role.alignment = TextAlignmentOptions.Bottom;
            role.textWrappingMode = TextWrappingModes.Normal;
            role.characterSpacing = RoleTracking;
            role.text = GameManager.CreditLines[i, 0];

            // 외곽선을 두르지 않는다. 바탕이 새까매서 두를 이유가 없고, 검은 복사본 여덟 장을
            // 한 칸씩 밀어 깔면 획만 굵어져 글자가 뭉툭해 보인다. 창 위에 뜨는 마지막 한 줄만
            // 외곽선을 쓴다 — 그건 가게 그림 위에 얹히기 때문이다.

            // 이름이 빈 줄은 제목 카드다(팀 이름 등). 금선도 이름도 만들지 않는다 —
            // 빈 이름 아래 금선만 뜨면 무언가 빠진 화면으로 보인다.
            if (string.IsNullOrEmpty(GameManager.CreditLines[i, 1])) continue;

            Image rule = NewImage("Rule", block);
            rule.rectTransform.anchoredPosition = new Vector2(ColumnCenterX, CardRuleY);
            rule.rectTransform.sizeDelta = RuleSize;
            rule.color = RuleInk;
            rule.raycastTarget = false;

            var person = NewText("Name", block, namePos, new Vector2(ColumnWidth, 28f),
                                 NameFontSize, NameInk, font);
            person.characterSpacing = NameTracking;
            person.text = GameManager.CreditLines[i, 1];
        }

        for (int i = 0; i < Logos.Length; i++) BuildLogoCard(column, count + i, Logos[i]);

        BuildThanksCard(column, font);
    }

    /// <summary>
    /// 로고가 다 지나간 자리에 서는 인사 한 장. 역할 글자와 같은 크기·같은 색이라
    /// 앞에 지나간 이름들과 한 식구로 읽힌다. 금선도 이름도 없다 — 제목 카드와 같은 꼴이다.
    ///
    /// <b>이 장만 시계로 안 걷는다.</b> 나머지는 제 차례가 끝나면 스스로 스러지는데,
    /// 이것은 무대가 걷어 준다(<see cref="FadeThanks"/>) — 까마귀가 앉고 아이리스가
    /// 오므리기 직전이다. 언제인지는 노래가 아니라 손님이 정하므로 시계로는 못 맞춘다.
    /// </summary>
    private void BuildThanksCard(Transform column, TMP_FontAsset font)
    {
        var block = (RectTransform)NewRect("Line_Thanks", column);
        Center(block);

        thanksIndex = lineGroups.Length - 1;
        lineGroups[thanksIndex] = block.gameObject.AddComponent<CanvasGroup>();
        lineGroups[thanksIndex].blocksRaycasts = false;
        lineGroups[thanksIndex].alpha = 0f;

        var line = NewText("Thanks", block, new Vector2(ColumnCenterX, 0f),
                           new Vector2(ColumnWidth, RoleBoxHeight), RoleFontSize, RoleInk, font);
        line.alignment = TextAlignmentOptions.Center;
        line.characterSpacing = RoleTracking;
        line.text = GameManager.CreditThanksLine;
    }

    /// <summary>
    /// 크레딧에 올리는 로고. 한 장에 하나씩, 이름 카드와 같은 박자로 넘어간다.
    ///
    /// 원본이 흰 바탕에 짙은 글자라 검은 화면에 그대로 얹으면 안 보인다. 흰 단색으로 구워
    /// <c>Assets/Resources/Credits/</c> 에 넣어 두었다(네오위즈만 붉은 마크를 살렸다).
    /// 폭은 로고마다 글자 크기가 눈으로 비슷해 보이게 따로 잡은 값이다.
    /// </summary>
    private struct LogoCard
    {
        public string File;
        public float Width;
    }

    private static readonly LogoCard[] Logos =
    {
        new LogoCard { File = "logo_neowiz", Width = 300f },
        new LogoCard { File = "logo_rapa",   Width = 300f },
        new LogoCard { File = "logo_mbc",    Width = 260f },
    };

    private void BuildLogoCard(Transform column, int index, LogoCard card)
    {
        var block = (RectTransform)NewRect("Line_" + card.File, column);
        Center(block);

        lineGroups[index] = block.gameObject.AddComponent<CanvasGroup>();
        lineGroups[index].blocksRaycasts = false;
        lineGroups[index].alpha = 0f;

        Logo(block, card.File, new Vector2(ColumnCenterX, 0f), card.Width);
    }

    /// <summary>로고 한 장. 가로 폭만 주면 원본 비율대로 세로를 잡는다.</summary>
    private static void Logo(Transform parent, string file, Vector2 pos, float width)
    {
        Sprite sprite = Resources.Load<Sprite>("Credits/" + file);
        if (sprite == null)
        {
            Debug.LogWarning("[크레딧] 로고가 없습니다: Resources/Credits/" + file);
            return;
        }

        // ⚠️ 시트로 잘려 들어오지 않았는지 본다.
        //
        // 유니티가 이 그림들을 Sprite Mode = Multiple 로 들여와 제멋대로 조각냈던 적이 있다
        // (네오위즈 7조각·MBC 23조각·RAPA 18조각). 그러면 Resources.Load 가 **첫 조각**을
        // 집어 와서, 로고 대신 손톱만 한 부스러기가 화면에 뜬다. 에러도 경고도 없이 그림만
        // 틀리므로 여기서 잡는다. 임포터에서 Sprite Mode 를 Single 로 두면 된다.
        if (sprite.rect.width < sprite.texture.width * 0.9f)
        {
            Debug.LogWarning("[크레딧] 로고가 잘려 들어왔습니다: " + file
                             + "  스프라이트 " + sprite.rect.width + "x" + sprite.rect.height
                             + " / 원본 " + sprite.texture.width + "x" + sprite.texture.height
                             + "  → 임포터에서 Sprite Mode 를 Single 로 바꿔 주세요.");
        }

        Image image = NewImage(file, parent);
        image.sprite = sprite;
        image.raycastTarget = false;

        // 앵커를 손으로 박는다. 코드로 만든 RectTransform 은 기본이 늘어나기(stretch)라,
        // sizeDelta 가 크기가 아니라 「부모보다 이만큼 더」가 되어 화면 전체로 퍼진다.
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;

        float height = Mathf.Round(width * sprite.rect.height / sprite.rect.width);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>외곽선 복사본에 원본과 같은 글·크기·정렬·자간을 넣는다.</summary>
    private static void CopyToShadow(TextMeshProUGUI[] copies, TextMeshProUGUI source)
    {
        for (int i = 0; i < copies.Length; i++)
        {
            copies[i].alignment = source.alignment;
            copies[i].characterSpacing = source.characterSpacing;
            copies[i].textWrappingMode = source.textWrappingMode;
            copies[i].fontSize = source.fontSize;
            copies[i].text = source.text;
        }
    }

    /// <summary>줄여 놓은 무대. 미리보기가 끝나고 되돌릴 때 쓴다.</summary>
    private RectTransform stageFrame;
    private Vector3 stageScaleWas = Vector3.one;
    private Vector2 stagePosWas;
    private RectMask2D stageMask;

    /// <summary>만들어 둔 판. 미리보기가 끝나고 지울 때 쓴다.</summary>
    private GameObject overlayRoot;

    /// <summary>여운에 화면을 덮는 검은 판.</summary>
    private Image blackout;

    /// <summary>글자가 떠 있는 동안 무대를 죽이는 판.</summary>
    private Image dim;

    /// <summary>
    /// 검은 복사본 여덟 장을 한 칸씩 밀어 뒤에 깐다. PixelTextOutline 과 같은 방식이다.
    ///
    /// 그쪽 컴포넌트를 쓰지 않는 까닭은 복사본 배열을 인스펙터로 꽂아야 하기 때문이다.
    /// 여기서는 글을 바꾸는 것도 이쪽이라 <see cref="SetText"/> 로 같이 넣으면 된다.
    ///
    /// ⚠️ 앵커를 반드시 원본과 같은 것(Center)으로 준다. 빌더의 AttachPixelOutline 이
    /// Center 로 박혀 있어서, BottomRight 로 앉힌 글자에 썼다가 복사본만 화면 한가운데로
    /// 날아간 적이 있다(CLAUDE.md).
    /// </summary>
    private static TextMeshProUGUI[] AttachShadow(TextMeshProUGUI source, Transform parent,
                                                  Vector2 pos, TMP_FontAsset font)
    {
        Vector2[] offsets =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
        };

        var copies = new TextMeshProUGUI[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            copies[i] = NewText("Outline" + i, parent, pos + offsets[i],
                                source.rectTransform.sizeDelta, (int)source.fontSize, Color.black, font);
            copies[i].text = source.text;
        }

        // 원본이 복사본들 위에 오도록 맨 뒤 형제로 보낸다.
        source.transform.SetAsLastSibling();
        return copies;
    }

    // ── 자잘한 손도구 ──────────────────────────────────────────────

    private static Transform FindFrame()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;

            Transform frame = canvas.transform.Find("Frame");
            if (frame != null) return frame;
        }
        return null;
    }

    /// <summary>씬에 이미 떠 있는 글자에서 폰트를 빌려 온다. 폰트는 Resources 밖에 있다.</summary>
    private static TMP_FontAsset FindFont()
    {
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null) return text.font;
        }
        return null;
    }

    private static Transform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 pos, Vector2 size,
                                           int fontSize, Color ink, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.color = ink;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    /// <summary>판 한가운데에 고정 크기로 앉힌다. 여기 붙은 것은 anchoredPosition 으로 움직일 수 있다.</summary>
    private static void Center(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(960f, 540f);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
