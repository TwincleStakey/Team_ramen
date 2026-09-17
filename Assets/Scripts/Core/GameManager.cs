using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 씬에 하나만 존재하는 진입점.
/// 손님을 한 명씩 받아 주문을 띄우고, 그릇이 제출되면 B의 OrderManager로 넘겨 채점한다.
/// 아직 주문·정산 화면이 없어서 조리 화면 위에 글자로만 보여 준다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 아래 넷은 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private DayManager dayManager;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private DayClockIcon dayClock;
    [SerializeField] private RamenCalculator ramenCalculator;
    [SerializeField] private FinalResultUI finalResultUI;
    [SerializeField] private OrderScreenUI orderScreenUI;

    /// <summary>시작 화면. 이게 떠 있는 동안에는 하루도 튜토리얼도 시작하지 않는다.</summary>
    [SerializeField] private TitleScreenUI titleScreen;
    [SerializeField] private OrderNoteUI orderNoteUI;
    [SerializeField] private OrderResultUI orderResultUI;

    /// <summary>「주문마감」 붓글씨. 오늘 마지막 손님이 나간 뒤 한 자씩 박힌다.</summary>
    [SerializeField] private ClosedSign closedSign;

    /// <summary>
    /// 마지막 손님이 스러져 나간 뒤 글자가 박히기까지 비워 두는 시간(초).
    ///
    /// 손님이 사라지자마자 글자가 떨어지면 손님을 지우려고 띄운 것처럼 보인다.
    /// 빈 카운터를 한 박자 보여 줘야 "오늘은 여기까지" 가 된다.
    /// </summary>
    [SerializeField] private float closedSignDelay = 1.5f;

    /// <summary>
    /// 손님이 먹는 장면을 보여 주는 시간(초).
    /// 지금은 얼굴 그림이 없어 손님이 가만히 서 있기만 한다. 표정이 붙으면 거기 맞춰 늘린다.
    /// </summary>
    [SerializeField] private float eatSeconds = 2.5f;

    /// <summary>손님이 스러지는 데 걸리는 시간(초). 결과창이 뜨는 순간부터 센다.</summary>
    [SerializeField] private float exitSeconds = 0.6f;

    /// <summary>앞 손님이 나가고 다음 손님이 올 때까지 카운터가 비어 있는 시간(초).</summary>
    [SerializeField] private float emptySeconds = 3f;

    /// <summary>손님이 자리에 선 뒤 인영에서 제 색으로 밝아지는 데 걸리는 시간(초).</summary>
    [SerializeField] private float enterSeconds = 0.6f;

    /// <summary>
    /// 걸어 들어오기 시작하는 자리. 제자리에서 오른쪽으로 이만큼 떨어진 곳이다.
    /// 음수로 두면 왼쪽에서 걸어온다.
    ///
    /// 420 이면 손님이 화면 오른쪽 끄트머리에 걸친 채로 시작한다. 더 멀리 두면 한 걸음이
    /// 그만큼 넓어져 성큼성큼 걷는 것으로 보인다 — 걸음 수는 발소리 박자가 정하기 때문이다.
    /// </summary>
    [SerializeField] private float walkInDistance = 420f;

    /// <summary>한 걸음마다 몸이 들리는 높이(칸). 크게 주면 걷는 게 아니라 뛰는 것으로 보인다.</summary>
    [SerializeField] private float walkBob = 3f;

    /// <summary>
    /// 걸어오는 동안 손님을 아래로 내리는 깊이(칸).
    ///
    /// 손님 자리는 아래변이 카운터 윗선에 딱 맞춰져 있고 거기서 잘린다. 그래서 그대로 걸으면
    /// 몸이 카운터에 닿지 않고 선 위에서 툭 끊겨, 카운터 뒤가 아니라 공중에 뜬 것처럼 보인다.
    /// 이만큼 내리면 아랫도리가 카운터에 가려져 그 뒤를 걸어오는 것으로 읽힌다.
    /// 자리에 서면서 도로 올라온다.
    /// </summary>
    [SerializeField] private float walkSink = 24f;

    /// <summary>튜토리얼 대사 한 마디를 읽힐 시간(초). 그 전에 아무 키나 누르면 바로 넘어간다.</summary>
    [SerializeField] private float tutorialLineSeconds = 3f;

    /// <summary>그릇을 받은 손님이 한 마디 하고 읽힐 시간(초). 그 전에 누르면 바로 넘어간다.</summary>
    [SerializeField] private float servedLineSeconds = 2.5f;

    /// <summary>그 말을 마치고 그릇을 들기까지 두는 짬(초). 말하자마자 들면 허겁지겁 먹는 꼴이다.</summary>
    [SerializeField] private float servedPauseSeconds = 0.6f;


    /// <summary>그릇을 내자마자 튜토리얼 손님이 하는 말.</summary>
    private const string TutorialServedLine = "오, 벌써 나왔나요?\n잘 먹겠습니다.";

    /// <summary>
    /// 다 먹고 나서 하는 말. 마디마다 버튼을 눌러 넘긴다.
    /// 마지막 마디의 버튼이 [안녕히 가세요.] 이고, 그걸 누르면 정확도 창이 열린다.
    /// </summary>
    private static readonly string[] TutorialClosingLines =
    {
        "맛있네요. 잘 먹었습니다.",
        "앞으로 올 손님들은 취향이 각각 다르실 거에요.",
        "손님들의 말을 귀 기울여 듣고, 완벽하게 만들어서 그 손님의 인생라멘집이 되어보세요.",
        "그럼 이만.",
    };

    /// <summary>정확도 창에 싣는 튜토리얼 마무리 안내.</summary>
    private const string TutorialEndLine = "튜토리얼은 여기까지입니다.\n손님이 원하는 라멘을 만들어주세요!";

    /// <summary>가게 문을 열 때 검은 화면에 머무는 시간(초). 장면이 바뀌었다는 사이를 둔다.</summary>
    [SerializeField] private float blackHoldSeconds = 2f;

    /// <summary>
    /// 검은 화면에서 도는 도입부 내레이션. 없으면(빌더를 안 돌린 경우) 그냥 건너뛴다.
    /// </summary>
    [SerializeField] private OpeningNarration narration;

    /// <summary>가운데부터 바깥으로 밝아지는 전환. 없으면 보통 페이드로 대신한다.</summary>
    [SerializeField] private IrisFade iris;

    /// <summary>내레이션 마지막 줄을 넘기고 가게가 밝아지기까지 두는 사이(초).</summary>
    [SerializeField] private float openingTailSeconds = 0.4f;

    /// <summary>손님이 걸어오는 소리. 빌더가 CustomerSlot 에 붙여 준다.</summary>
    [SerializeField] private Footsteps footsteps;

    /// <summary>먹는 네 컷 연출. 없으면 eatSeconds 만큼 그냥 기다린다.</summary>
    [SerializeField] private EatingCutscene cutscene;

    /// <summary>100% 일 때 뜨는 "완벽한 한 그릇!" 팻말. 처음 쓸 때 한 번 찾아 둔다.</summary>
    private PerfectSign perfectSign;

    /// <summary>팻말이 다 스러진 뒤 결과창이 뜨기까지 두는 틈(초).</summary>
    private const float PerfectSignTailSeconds = 0.5f;

    /// <summary>지금까지 판 금액의 합. 재료비가 없어져서 매출이 곧 성적표다. (기획 확정)</summary>
    private int totalRevenue;

    /// <summary>
    /// 오늘 판 금액. 인게임 화면(상단바·주문 화면·정확도 창)에 뜨는 것은 전부 이쪽이다.
    ///
    /// 누적(totalRevenue)은 **정산표의 「누적 총 매출」과 최종 성적표에만** 쓴다. 조리하는 동안
    /// 보여야 하는 것은 오늘 목표까지 얼마나 왔는지지, 지금까지 번 총액이 아니다.
    /// NextDay 가 RamenCalculator 의 당일 집계를 지우므로 하루가 바뀌면 저절로 0 으로 돌아간다.
    /// </summary>
    private int TodayRevenue
    {
        get { return EnsureRamenCalculator() ? ramenCalculator.TodayTotalProfit : 0; }
    }

    // 최종 성적표용 누계.
    // RamenCalculator도 정확도를 모으지만 NextDay가 하루마다 지우므로 5일치를 여기서 따로 쌓는다.
    // 영업 시각. 17시에 열고 손님이 한 명 갈 때마다 한 시간씩 흐른다.
    // 시간 제한은 없다(기획서 5.4). 진행이 눈에 보이게 하는 표시일 뿐이다.
    private const int OpenHour = 17;
    private int currentHour = OpenHour;

    private int servedCount;
    private float accuracySum;
    private int perfectCount;

    /// <summary>Awake에서 읽어 두고 Start에서 얹는다. 얹고 나면 비운다.</summary>
    private SaveData pendingSave;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 구독은 반드시 Awake에서 한다. 실행 순서가 정해져 있지 않아 DayManager.Start()가
        // 이쪽 Start()보다 먼저 돌 수 있고, 그러면 1일차 첫 주문 신호를 놓친다.
        // 유니티는 모든 Awake를 끝낸 뒤에야 Start를 시작하므로 여기서 걸면 순서와 무관하게 안전하다.
        if (EnsureDayManager())
        {
            dayManager.OnDayStarted += HandleDayStarted;
            dayManager.OnGameCompleted += HandleGameCompleted;
            dayManager.OnGameRestart += HandleGameRestart;
        }

        PrepareResume();
    }

    /// <summary>
    /// 저장·이어하기를 켤지. 지금은 꺼 둔다.
    ///
    /// 시스템은 다 만들어져 있고 동작도 확인했지만, 개발 중에는 켜 두면 방해가 된다.
    /// 플레이할 때마다 지난번 그릇 내용물과 주문이 되살아나서, 조리 화면을 열면
    /// 재료가 이미 담겨 있는 상태로 시작한다. 화면을 손보는 동안에는 늘 빈 그릇이어야 한다.
    ///
    /// 다시 켤 때는 이 값만 true 로 바꾸면 된다. 저장 코드는 하나도 지우지 않았다.
    /// 켜기 전에 남아 있는 옛 저장 파일을 지우는 것이 좋다.
    /// (Windows: AppData/LocalLow/DefaultCompany/Team_ramen/ramen_save.json)
    /// </summary>
    [Header("저장")]
    [SerializeField]
    [Tooltip("끄면 저장도 이어하기도 하지 않는다. 개발 중에는 꺼 두는 편이 편하다.")]
    private bool saveEnabled = false;

    /// <summary>
    /// 이어할 저장이 있으면 DayManager가 하루를 새로 시작하지 못하게 막아 둔다.
    /// DayManager.Start()는 autoStartFirstDay가 켜져 있으면 1일차 주문을 새로 만들어 버리는데,
    /// 그러면 저장해 둔 주문 대신 무작위로 뽑은 새 주문이 뜬다(기획서 13.1 — 주문 재생성 금지).
    ///
    /// 유니티는 모든 Awake를 끝낸 뒤에 Start를 돌리므로, Awake에서 끄면 순서와 무관하게 안전하다.
    /// 실제로 값을 되돌려 놓는 것은 Start에서 한다.
    /// </summary>
    private void PrepareResume()
    {
        // DayManager가 스스로 1일차를 열지 못하게 늘 막아 둔다.
        // 시작 화면·튜토리얼·이어하기 셋 다 "우리가 정한 때에 연다"가 필요해서,
        // 조건을 따지지 않고 막은 뒤 BeginGame에서 한 번만 연다.
        HoldFirstDay();

        // 꺼져 있으면 저장 파일을 읽지 않는다. pendingSave 가 null 이면 아래 이어하기 경로가
        // 통째로 건너뛰어지고, autoStartFirstDay 도 그대로라 1일차가 정상으로 시작한다.
        if (!saveEnabled) return;

        pendingSave = SaveSystem.Read();
    }

    /// <summary>
    /// DayManager 가 스스로 1일차를 열지 못하게 막는다.
    ///
    /// DayManager.Start() 는 autoStartFirstDay 가 켜져 있으면 1일차 주문을 새로 만든다.
    /// 이어하기에서는 저장해 둔 주문 대신 새 주문이 뜨고(기획 13.1 — 주문 재생성 금지),
    /// 튜토리얼에서는 튜토리얼 주문을 덮어써 버린다.
    ///
    /// 유니티는 모든 Awake 를 끝낸 뒤에 Start 를 돌리므로, Awake 에서 끄면 순서와 무관하게 안전하다.
    /// </summary>
    private void HoldFirstDay()
    {
        if (dayManager == null) return;

        var field = typeof(DayManager).GetField("autoStartFirstDay",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null) field.SetValue(dayManager, false);
        else Debug.LogWarning("[GameManager] DayManager.autoStartFirstDay를 찾지 못해 새 주문이 생길 수 있습니다.");
    }

    private void Start()
    {
        RefreshRevenue();

        if (dayManager == null)
        {
            Debug.LogWarning("[GameManager] 씬에 DayManager가 없어 하루 진행이 시작되지 않습니다. " +
                             "Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
        }

        // 시작 화면이 떠 있으면 아무것도 시작하지 않는다. [게임시작]을 누르면 BeginGame이 온다.
        if (titleScreen != null && titleScreen.IsOpen) return;

        BeginGame();
    }

    /// <summary>
    /// 일차를 몰아서 넘기는 중인가. 그동안은 하루 시작 연출(검은 화면·자막·아이리스)을 안 건다.
    ///
    /// 안 막으면 넘긴 일차마다 그 코루틴이 하나씩 쌓여, 네 일차를 건너뛰는 데 20초가 넘게 걸리고
    /// 자막 넷이 줄지어 뜬다.
    ///
    /// **개발용 건너뛰기가 세우는 값인데 선언은 `#if UNITY_EDITOR` 바깥에 둔다.** 이걸 보는
    /// <see cref="HandleDayStarted"/> 는 본편 흐름이라 안 가려져 있어서, 선언만 안쪽에 두면
    /// 에디터에서는 멀쩡하고 **빌드에서만** CS0103 으로 깨진다. 실제로 2026-09-16 에 그랬다 —
    /// 콘솔은 조용한데 Build 를 누르면 그때서야 실패했다.
    /// 값을 세우는 쪽(F8)은 그대로 에디터 전용이라, 빌드에서는 늘 거짓이다.
    /// </summary>
    private bool fastForwarding;

#if UNITY_EDITOR
    /// <summary>
    /// 개발용 건너뛰기 두 가지. 타이틀의 F1 과 같이 에디터에서만 듣는다 — 빌드에는 이 키가 아예 없다.
    ///
    ///   F2  **조리 화면에서** 그릇을 정답으로 채우고 [마무리] 확인창까지 띄운다.
    ///       확인은 사람이 누른다 — 실제 흐름과 같은 자리로 나와야 연출도 같이 확인된다.
    ///       튜토리얼 중이면 안내가 시키는 차례를 그대로 따라간다.
    ///   F3  **주문 화면부터** 손님 하나를 통째로 넘긴다. 확인창도 안 거치고 바로 낸다 —
    ///       손님 서른 명을 몰아 볼 때 쓰는 것이라 클릭이 하나도 없어야 한다.
    ///   F4  튜토리얼을 통째로 건너뛰고 본편 1일차 첫 손님을 세운다.
    ///   F5  오늘 남은 손님을 정답으로 처리하고 곧장 「주문마감」→ 정산표로 간다(목표 달성).
    ///   F7  같은 자리로 가되 **돈 없이** 흘려보낸다. 임대 딱지 → [다시하기] → 배드엔딩 길을 볼 때.
    ///   F8  1~4일차를 몰아 넘기고 5일차 주문마감까지. [확인]을 누르면 최종 성적표가 뜬다.
    ///
    /// F2 와 F3 는 둘 다 정답 레시피를 담아 정확도 100% 가 나온다.
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f2Key.wasPressedThisFrame) StartCoroutine(FillAndSubmit());
        else if (keyboard.f3Key.wasPressedThisFrame) SkipCustomer();
        else if (keyboard.f4Key.wasPressedThisFrame) SkipTutorial();
        else if (keyboard.f5Key.wasPressedThisFrame) SkipToClosing();
        else if (keyboard.f6Key.wasPressedThisFrame) StartCredits();
        else if (keyboard.f7Key.wasPressedThisFrame) SkipToClosingUnmet();
        else if (keyboard.f8Key.wasPressedThisFrame) SkipToFinalDay();
    }

    /// <summary>
    /// 지금 이 자리에서 크레딧을 연다. 5일을 다 팔거나 타이틀을 거치지 않고 바로 본다.
    ///
    /// 돌고 있던 연출을 먼저 끊는다. 안 끊으면 손님 교대나 시식 연출이 크레딧 무대와
    /// 같이 돌아 두 벌이 겹친다.
    /// </summary>
    private void StartCredits()
    {
        if (CreditsSequence.Running)
        {
            Debug.LogWarning("[F6] 크레딧이 이미 돌고 있습니다.");
            return;
        }

        ClearForDevSkip();
        CreditsSequence.Begin(CreditsSequence.Exit.Title);
    }

    /// <summary>
    /// 돌고 있던 연출을 끊고 화면에 떠 있던 것을 걷는다. F4·F5 가 같이 쓴다.
    ///
    /// **반드시 코루틴 밖에서 부를 것.** StopAllCoroutines 는 자기를 부른 코루틴까지 멈춘다.
    /// 코루틴 안에서 부르면 그다음 yield 에서 영영 안 돌아온다.
    /// </summary>
    private void ClearForDevSkip()
    {
        StopAllCoroutines();

        // 확인창이 열린 채면 timeScale 이 0 으로 눌려 있다. 그냥 두면 게임이 멈춘 채로 남는다.
        foreach (ConfirmDialogUI dialog in FindObjectsByType<ConfirmDialogUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            dialog.ForceClose();
        }

        // 연출 도중이었으면 검은 바·확대·우주가 그대로 남는다.
        if (EnsureCutscene()) cutscene.ResetStage();

        if (orderResultUI != null) orderResultUI.Close();
        if (orderNoteUI != null) orderNoteUI.Hide();

        // 담다 만 재료를 비운다. 폐기분은 채점에 안 들어가므로 점수에 영향이 없다.
        Bowl bowl = FindFirstObjectByType<Bowl>();
        if (bowl != null && !bowl.IsEmpty) bowl.Discard();
    }

    /// <summary>
    /// 오늘 남은 손님을 정답 한 그릇씩 낸 것으로 치고 곧장 주문마감으로 간다.
    ///
    /// 그냥 하루만 끝내면 매출이 0 이라 정산표가 늘 「미달 → 다시하기」로 떠서, 정작 보려던
    /// [확인] → 다음날 연출을 못 본다. 그래서 남은 손님 몫을 실제 채점 경로로 얹는다.
    /// </summary>
    private void SkipToClosing() { SkipToClosing(true, "[F5]"); }

    /// <summary>
    /// F7 — 목표를 못 채운 채로 마감한다. 남은 손님을 **돈 없이** 흘려보낸다.
    ///
    /// 임대 딱지 → [다시하기] → 배드엔딩 길을 보려면 미달로 끝나는 하루가 있어야 한다.
    /// 이미 목표를 넘긴 뒤라면 미달이 될 수 없다 — 그때는 하루가 열리자마자 눌러야 한다.
    /// </summary>
    private void SkipToClosingUnmet() { SkipToClosing(false, "[F7]"); }

    private void SkipToClosing(bool credit, string tag)
    {
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsRunning)
        {
            Debug.LogWarning(tag + " 튜토리얼 중에는 쓸 수 없습니다. F4 로 건너뛰세요.");
            return;
        }

        if (!EnsureDayManager() || dayManager.IsGameCompleted)
        {
            Debug.LogWarning(tag + " 지금은 영업 중이 아닙니다.");
            return;
        }

        if (!EnsureOrderManager() || orderManager.CurrentTargetRecipe == null)
        {
            Debug.LogWarning(tag + " 지금 받아 둔 주문이 없습니다.");
            return;
        }

        ClearForDevSkip();
        StartCoroutine(SkipToClosingRoutine(credit, tag));
    }

    private IEnumerator SkipToClosingRoutine(bool credit, string tag)
    {
        // 마지막 한 명은 남겨 둔다. 그 몫은 CloseShop 이 OnCustomerServed 로 마무리하면서 끝난다.
        // 여기서 다 채우면 하루가 먼저 끝나 「주문마감」을 끼울 자리가 없어진다.
        int guard = 0;
        while (dayManager.CurrentCustomerCount < dayManager.TargetCustomerCount - 1 && guard++ < 32)
        {
            if (credit && !CreditCurrentCustomer()) break;

            currentHour++;
            dayManager.OnCustomerServed();      // 그 안에서 다음 손님 주문이 만들어진다
        }

        // 오늘의 마지막 손님 몫.
        if (credit) CreditCurrentCustomer();
        currentHour++;

        RefreshDayLabel(dayManager.CurrentDay);
        RefreshRevenue();

        Debug.Log(tag + (credit ? " 남은 손님을 정답으로 처리하고" : " 남은 손님을 돈 없이 흘려보내고")
                  + " 주문마감으로 갑니다. 오늘 수익 " + TodayRevenue.ToString("N0")
                  + "원 / 목표 " + dayManager.TargetProfit.ToString("N0") + "원");

        // 실제 흐름에서는 마지막 그릇을 낸 직후라 주문 화면이 떠 있다. 조리 화면에서 눌렀을 때도
        // 같은 그림이 되도록 먼저 올린다 — 안 그러면 「주문마감」과 정산표가 조리대 위에 뜬다.
        if (orderScreenUI != null && !orderScreenUI.IsOpen)
        {
            orderScreenUI.OpenEating(dayManager.CurrentDay, currentHour, TodayRevenue);
            yield return orderScreenUI.WaitForSlide();
        }
        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);

        yield return CloseShop();
    }

    /// <summary>
    /// F8 — 1~4일차를 정답으로 몰아 넘기고 5일차 주문마감까지 간다.
    ///
    /// 5일차 정산표에서 [확인]을 누르면 최종 성적표가 뜬다. 거기까지 가려면 손님 서른 명을
    /// 지나야 해서 손으로는 확인이 사실상 불가능하다.
    /// </summary>
    private void SkipToFinalDay()
    {
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsRunning)
        {
            Debug.LogWarning("[F8] 튜토리얼 중에는 쓸 수 없습니다. F4 로 건너뛰세요.");
            return;
        }

        if (!EnsureDayManager() || dayManager.IsGameCompleted)
        {
            Debug.LogWarning("[F8] 지금은 영업 중이 아닙니다.");
            return;
        }

        if (dayManager.CurrentDay >= DayManager.MAX_DAYS)
        {
            Debug.LogWarning("[F8] 이미 " + DayManager.MAX_DAYS + "일차입니다. F5 로 마감하세요.");
            return;
        }

        ClearForDevSkip();
        StartCoroutine(SkipToFinalDayRoutine());
    }

    private IEnumerator SkipToFinalDayRoutine()
    {
        fastForwarding = true;
        try
        {
            int guard = 0;
            while (dayManager.CurrentDay < DayManager.MAX_DAYS && guard++ < DayManager.MAX_DAYS + 2)
            {
                // 오늘 손님을 전부 정답으로 낸다. 마지막 한 명에서 DayManager 가 하루를 마감하고
                // 정산표를 연다 — 연출은 안 거치고 숫자만 쌓인다.
                int seat = 0;
                while (dayManager.CurrentCustomerCount < dayManager.TargetCustomerCount && seat++ < 32)
                {
                    if (!CreditCurrentCustomer()) break;
                    dayManager.OnCustomerServed();
                }

                // 정산표를 닫고 다음 날로. NextDay 가 StartDay 까지 부른다.
                dayManager.NextDay();
                yield return null;
            }
        }
        finally { fastForwarding = false; }

        Debug.Log("[F8] " + dayManager.CurrentDay + "일차까지 몰아 넘겼습니다. 이제 주문마감으로 갑니다.");

        currentHour = OpenHour;
        RefreshDayLabel(dayManager.CurrentDay);
        RefreshRevenue();

        // 5일차는 F5 와 같은 길로 마감한다.
        yield return SkipToClosingRoutine(true, "[F8]");
    }

    /// <summary>
    /// 지금 손님에게 정답 한 그릇을 낸 것으로 치고 매출·정확도에 얹는다. 화면은 건드리지 않는다.
    ///
    /// SubmitRamen 의 채점 부분과 같은 자리를 쓴다 — 따로 계산하면 정산표와 어긋난다.
    /// </summary>
    private bool CreditCurrentCustomer()
    {
        if (!EnsureOrderManager()) return false;

        Dictionary<IngredientType, int> recipe = orderManager.CurrentTargetRecipe;
        if (recipe == null || recipe.Count == 0) return false;

        var state = new RamenState(recipe, new Dictionary<IngredientType, int>());

        int price = orderManager.EvaluateRamen(state);
        totalRevenue += price;

        if (EnsureRamenCalculator())
        {
            float accuracy = ramenCalculator.LastAccuracy;
            accuracySum += accuracy;
            servedCount++;
            if (accuracy >= RamenCalculator.PERFECT_ACCURACY) perfectCount++;
        }

        return true;
    }

    /// <summary>
    /// 튜토리얼을 건너뛰고 본편 1일차를 연다.
    ///
    /// 안내가 도는 동안(조리 단계)에만 듣는다. 그릇을 낸 뒤로는 시식 컷신이 도는데,
    /// 그건 GameManager 가 yield return 으로 돌리는 것이라 여기서 멈추면 화면이 중간 상태로 남는다.
    /// </summary>
    private void SkipTutorial()
    {
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial == null || !tutorial.IsRunning)
        {
            Debug.LogWarning("[F4] 지금은 튜토리얼 안내가 돌고 있지 않습니다. 그릇을 낸 뒤라면 그대로 두고 보세요.");
            return;
        }

        // 연출을 끊고 창을 걷는다. 코루틴 **밖**이라 바로 아래에서 시작하는 것은 안 죽는다.
        ClearForDevSkip();

        // 안내·테두리·어두운 판을 걷는다. 저장도 이때부터 시작된다(CanSave).
        TutorialManager.Instance.Finish();

        if (orderScreenUI != null)
        {
            orderScreenUI.ShowBubble(false);
            orderScreenUI.Close();
        }

        StartCoroutine(SkipTutorialRoutine());
    }

    private IEnumerator SkipTutorialRoutine()
    {
        Debug.Log("[F4] 튜토리얼을 건너뛰고 1일차를 엽니다.");

        // 아래는 ServeDineAndDash 의 꼬리와 같다 — 손님이 나가고 본편이 열린다.
        yield return FadeCustomer(0f, 1f, exitSeconds);

        if (EnsureDayManager()) dayManager.StartDay();

        HideCustomerForEntrance();
        yield return EnterCustomer();
    }

    private IEnumerator FillAndSubmit()
    {
        Bowl bowl = FindFirstObjectByType<Bowl>();
        if (bowl == null)
        {
            Debug.LogWarning("[F2] 조리 화면에 그릇이 없습니다.");
            yield break;
        }

        TutorialManager tutorial = TutorialManager.Instance;

        if (tutorial != null && tutorial.IsRunning)
        {
            // 안내가 시키는 차례를 그대로 따라간다. 다른 것을 넣으면 안내가 그 자리에 멈춰 선다.
            // Tab(주문서)·B(레시피책) 차례는 재료가 아니라서 넘길 수 없다 — 사람이 눌러 준다.
            var icons = CollectBowlSprites();
            while (tutorial.IsRunning && !tutorial.ReadyToSubmit)
            {
                if (tutorial.CurrentStep is IngredientType step)
                {
                    icons.TryGetValue(step, out Sprite icon);
                    bowl.TryAdd(step, icon);
                    yield return new WaitForSeconds(AddInterval);
                    continue;
                }

                // 토글이라 여는 것만으로는 안 넘어간다. 열고 다시 눌러 닫아야 다음 차례가 된다.
                Debug.LogWarning("[F2] 재료가 아닌 차례입니다(Tab 이나 B). 눌러서 열었다가 다시 눌러 닫고 F2.");
                yield break;
            }
        }
        else if (!FillWithAnswer(bowl, "[F2]", out IEnumerator fill))
        {
            yield break;
        }
        else
        {
            yield return fill;
        }

        // 타래와 육수는 붓는 장면이 끝나야 그릇 그림이 자리를 잡는다. 그 전에 내면
        // 손님 앞에 붓다 만 그릇이 올라간다.
        yield return new WaitForSeconds(PourSeconds);

        // 여기서 bowl.Submit() 을 바로 부르지 않는다. 지금 게임은 그릇을 끌어다 내는 것이 아니라
        // 상단바 [마무리] → 확인창 → 그때 Submit 이다. 바로 부르면 확인창을 건너뛰어서,
        // 그릇 내용만 소리 없이 사라지고 실제 흐름과 달라진다.
        ConfirmDialogUI confirm = FindSubmitConfirm();
        if (confirm != null) confirm.Open();
        else bowl.Submit();
    }

    /// <summary>
    /// 마무리 확인창. 이름으로 찾는다 — 폐기 확인창과 같은 컴포넌트라 타입만으로는 안 갈린다.
    /// 닫혀 있을 때도 찾아야 하므로 꺼진 것까지 뒤진다.
    /// </summary>
    private ConfirmDialogUI FindSubmitConfirm()
    {
        foreach (ConfirmDialogUI dialog in FindObjectsByType<ConfirmDialogUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (dialog.name == "SubmitConfirm") return dialog;
        }
        return null;
    }

// 아래 넷은 개발용 건너뛰기(F2·F3)만 쓰던 것인데, 크레딧이 무대에서 그릇을 담는 데도 쓴다.
// 크레딧은 빌드에도 들어가므로 에디터 전용 구역 밖으로 내놓는다. 옮기지 않고 구역만 끊는다 —
// 코드를 움직이면 다른 세션이 같은 자리를 고치고 있을 때 그 편집이 묻힌다.
#endif

    /// <summary>재료를 하나 담고 다음 것을 담기까지의 틈.</summary>
    private const float AddInterval = 0.05f;

    /// <summary>타래·육수가 부어지는 장면이 끝나기를 기다리는 시간.</summary>
    private const float PourSeconds = 1.2f;

    /// <summary>재료 그림은 재료통이 들고 있다. 그림 없이 넣으면 그릇에 아무것도 안 올라간다.</summary>
    private Dictionary<IngredientType, Sprite> CollectBowlSprites()
    {
        var icons = new Dictionary<IngredientType, Sprite>();
        foreach (IngredientSlot slot in FindObjectsByType<IngredientSlot>(FindObjectsSortMode.None))
        {
            icons[slot.type] = slot.bowlSprite;
        }
        return icons;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 지금 손님의 정답 레시피를 그릇에 담는 코루틴을 만든다. F2 와 F3 가 같이 쓴다.
    ///
    /// 예전에는 F2 가 고정된 일곱 가지(시오 타래 + 굵은면 …)를 담았다. 그런데 채점이
    /// 타래가 틀리면 3대 요소 미달로 **0% · 0원**을 주므로, 주문이 쇼유나 돈코츠면
    /// 그 그릇은 늘 0원이었다. 목표 수익도 정산도 그걸로는 못 본다.
    /// </summary>
    private bool FillWithAnswer(Bowl bowl, string tag, out IEnumerator routine)
    {
        routine = null;

        OrderManager orders = FindFirstObjectByType<OrderManager>();
        Dictionary<IngredientType, int> recipe = orders != null ? orders.CurrentTargetRecipe : null;
        if (recipe == null || recipe.Count == 0)
        {
            Debug.LogWarning(tag + " 지금 받아 둔 주문이 없습니다.");
            return false;
        }

        // 이미 담긴 것이 있으면 정답에 얹혀 100% 가 안 나온다. 폐기분은 채점에 안 들어가므로
        // (RamenState 가 받기만 한다) 비우고 시작해도 점수에 영향이 없다.
        if (!bowl.IsEmpty) bowl.Discard();

        routine = PourRecipe(bowl, recipe);
        return true;
    }
#endif

    private IEnumerator PourRecipe(Bowl bowl, Dictionary<IngredientType, int> recipe)
    {
        var icons = CollectBowlSprites();

        // enum 순서가 곧 붓는 순서다(타래 → 육수 → 면 → 토핑 → 조미료).
        // 딕셔너리 순서를 그대로 믿으면 면 위에 타래를 붓는 장면이 나온다.
        var order = new List<IngredientType>(recipe.Keys);
        order.Sort((a, b) => ((int)a).CompareTo((int)b));

        foreach (IngredientType type in order)
        {
            icons.TryGetValue(type, out Sprite icon);

            // 「빼 주세요」로 0 이 된 재료가 섞여 있다. 그건 담지 않는 것이 정답이다.
            for (int n = 0; n < recipe[type]; n++)
            {
                bowl.TryAdd(type, icon);
                yield return new WaitForSeconds(AddInterval);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>F3 가 도는 중인가. 연타로 코루틴이 겹치면 그릇이 두 번 채워진다.</summary>
    private bool skipping;

    /// <summary>대사 넘기기와 그릇 기다리기에 쓰는 최대 프레임. 무한 대기를 막는 안전장치다.</summary>
    private const int SkipGuardFrames = 180;

    private void SkipCustomer()
    {
        if (skipping) return;
        StartCoroutine(SkipCustomerRoutine());
    }

    private IEnumerator SkipCustomerRoutine()
    {
        skipping = true;
        try { yield return SkipCustomerBody(); }
        finally { skipping = false; }
    }

    private IEnumerator SkipCustomerBody()
    {
        // 튜토리얼은 안내가 시키는 차례대로만 재료가 들어가므로 정답을 한 번에 못 붓는다.
        // Tab·B 차례는 재료가 아니라 넘길 수도 없다. 그쪽은 F2 가 맡는다.
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsRunning)
        {
            Debug.LogWarning("[F3] 튜토리얼 중에는 쓸 수 없습니다. F2 로 진행하세요.");
            yield break;
        }

        // 1. 주문 화면의 대사를 끝까지 민다. 한 프레임에 한 번씩만 누른다 —
        //    한 프레임에 몰아 누르면 타자기 코루틴이 아직 시작 전이라 줄을 건너뛴다.
        OrderScreenUI screen = FindFirstObjectByType<OrderScreenUI>();
        for (int i = 0; i < SkipGuardFrames && screen != null && screen.IsOpen; i++)
        {
            screen.PressStart();
            yield return null;
        }

        // 2. 조리 화면의 그릇이 설 때까지 기다린다.
        Bowl bowl = null;
        for (int i = 0; i < SkipGuardFrames && bowl == null; i++)
        {
            bowl = FindFirstObjectByType<Bowl>();
            if (bowl == null) yield return null;
        }

        if (bowl == null)
        {
            Debug.LogWarning("[F3] 조리 화면에 그릇이 없습니다.");
            yield break;
        }

        // 3. 이 손님의 정답을 담는다. F2 와 같은 코드다.
        if (!FillWithAnswer(bowl, "[F3]", out IEnumerator fill)) yield break;
        yield return fill;

        // 4. 붓는 장면이 끝나야 그릇 그림이 자리를 잡는다.
        yield return new WaitForSeconds(PourSeconds);
        bowl.Submit();
    }
#endif

    /// <summary>
    /// 실제로 게임을 연다. 시작 화면의 [게임시작]이 부른다.
    ///
    /// Awake에서 1일차를 미리 막아 두었으므로(HoldFirstDay) 여기서 직접 열어야 한다.
    /// 이어하기가 있으면 저장을 얹는 것이 먼저다 — 그다음 화면을 그 시점으로 되돌린다.
    /// </summary>
    public void BeginGame()
    {
        // 타이틀 BGM 은 화면이 검어지는 동안 같이 잦아든다.
        Sfx.Stop("bgm_title", 1.5f);

        bool resumed = pendingSave != null;
        if (resumed) Resume();

        RefreshRevenue();

        // 이어하기는 Resume이 그때 상태를 그대로 되살린다. 여기서 또 열면 주문이 새로 생긴다
        // (기획서 13.1 — 주문 재생성 금지).
        if (resumed) { StartShopSounds(); return; }

        StartCoroutine(OpenShop());
    }

    /// <summary>저장을 씬에 얹고 화면을 그때 상태로 되돌린다.</summary>
    private void Resume()
    {
        SaveData data = pendingSave;
        pendingSave = null;

        currentHour = data.hour;
        totalRevenue = data.totalRevenue;
        servedCount = data.servedCount;
        accuracySum = data.accuracySum;
        perfectCount = data.perfectCount;

        EnsureOrderManager();
        EnsureRamenCalculator();
        SaveSystem.Apply(data, dayManager, orderManager, ramenCalculator, FindFirstObjectByType<Bowl>());

        int day = dayManager != null ? dayManager.CurrentDay : data.day;
        RefreshDayLabel(day);

        // 주문 화면은 다시 띄운다. 대사를 처음부터 다시 듣게 되지만, 주문 자체는 저장된 그대로다.
        OpenOrderScreen(day);

        Debug.Log("[이어하기] " + day + "일차 " + (data.customerIndex + 1) + "번째 손님 / 누적 매출 "
                  + totalRevenue.ToString("N0") + "원");
    }

    /// <summary>
    /// 지금 상태를 파일에 남긴다. 상태가 바뀔 때마다 부른다
    /// (주문을 받을 때, 재료를 넣거나 버릴 때, 제출할 때, 손님이 바뀔 때).
    /// 파일이 작아서 자주 써도 부담이 없고, 언제 꺼도 그 자리에서 이어진다.
    /// </summary>
    public void SaveNow()
    {
        if (!saveEnabled) return;

        // 크레딧 중에는 남기지 않는다. 그 자리는 판이 이미 끝난 뒤이고, 무대가 소리를 내려고
        // 그릇에 실제로 재료를 담는다(CreditsPour) — 담을 때마다 Bowl 이 여기를 부른다.
        // 남겨 두면 5일을 다 판 뒤 지워 둔 저장이 크레딧 도중에 되살아나, 다음에 켰을 때
        // 끝난 판이 이어진다. 지금은 saveEnabled 가 꺼져 있어 안 드러나지만, 그 값 하나만
        // 켜면 바로 터지는 자리다.
        if (CreditsSequence.Running) return;

        // 튜토리얼을 끝내야 저장이 시작된다. 도중에 껐다 켜면 튜토리얼부터 다시 한다.
        if (!TutorialManager.CanSave()) return;

        if (!EnsureDayManager()) return;

        EnsureOrderManager();
        EnsureRamenCalculator();

        SaveSystem.Write(SaveSystem.Capture(dayManager, orderManager, ramenCalculator,
                                            FindFirstObjectByType<Bowl>(),
                                            currentHour, totalRevenue, servedCount, accuracySum, perfectCount));
    }

    private void OnDestroy()
    {
        if (dayManager != null)
        {
            dayManager.OnDayStarted -= HandleDayStarted;
            dayManager.OnGameCompleted -= HandleGameCompleted;
            dayManager.OnGameRestart -= HandleGameRestart;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// DayManager가 하루를 열 때 온다. StartDay()는 주문을 먼저 만들고 이 신호를 쏘므로
    /// 여기서는 이미 만들어진 주문을 화면에 옮기기만 하면 된다.
    /// </summary>
    private void HandleDayStarted(int day)
    {
        currentHour = OpenHour;

        // 어제 넘긴 것이 오늘까지 따라오면 안 된다. 목표도 수익도 하루마다 새로 센다.
        goalReached = false;

        RefreshDayLabel(day);
        RefreshRevenue();

        // 주문 화면 상단바도 같은 목표를 쓴다. 그쪽은 DayManager 를 모르므로 여기서 넣어 준다.
        // 시계 조각을 나누는 손님 수와 개점 시각도 같이 준다.
        if (orderScreenUI != null && EnsureDayManager())
            orderScreenUI.SetGoal(dayManager.TargetProfit, OpenHour, dayManager.TargetCustomerCount);

        // 개발용으로 일차를 몰아 넘기는 중이면 연출을 아예 안 건다. 배드엔딩보다 먼저 본다 —
        // 여기서 코루틴이 걸리면 넘긴 일차마다 하나씩 쌓인다.
        if (fastForwarding)
        {
            OpenOrderScreen(day);
            return;
        }

        // 목표 미달로 되돌아온 길이면 배드엔딩을 먼저 보여 준다. RestartGame 도 1일차를 열기 때문에
        // 아래 day <= 1 보다 먼저 봐야 한다.
        if (badEndingPending)
        {
            badEndingPending = false;
            StartCoroutine(PlayBadEnding(day));
            return;
        }

        // 1일차는 도입부와 튜토리얼을 막 지나온 참이라 또 어둡게 만들 이유가 없다.
        if (day <= 1)
        {
            OpenOrderScreen(day);
            return;
        }

        StartCoroutine(OpenNextDay(day));
    }

    /// <summary>[다시하기]로 돌아오는 길인가. HandleGameRestart 가 세우고 다음 StartDay 가 쓴다.</summary>
    private bool badEndingPending;

    /// <summary>
    /// 배드엔딩. 목표를 못 채워 임대 딱지가 붙고 [다시하기]를 눌렀을 때 온다.
    ///
    /// 도입부와 같은 연출을 글만 바꿔 쓴다 — 검은 화면에 줄이 쌓이고 클릭으로 넘긴다.
    /// 끝나면 첫날 밤으로 돌아가므로 아이리스도 도입부와 같이 연다.
    /// </summary>
    private static readonly string[] BadEndingLines =
    {
        "손님들이 하나둘 발길을 끊었다.",

        // 한 줄로 두면 560 폭을 넘겨(614) 아무 데서나 접힌다. 쉼표에서 끊어 두 줄로 보이되
        // 한 덩이라 클릭은 한 번이다.
        "말을 알아듣지 못하는 가게에,\n오래 머무는 손님은 없다.",

        "불 꺼진 가게 앞에 딱지 한 장이 붙었다.",
        "…그리고 다시, 첫날 밤.",
    };

    /// <summary>
    /// 크레딧에 뜨는 이름. 한 줄이 [역할, 이름] 한 쌍이고, 손님 하나가 지날 때 한 쌍씩 넘어간다.
    ///
    /// 한 장이 뜨는 시간은 <see cref="CreditsSequence"/> 가 정한다. 줄을 늘리면 크레딧이
    /// 그만큼 길어진다 — 노래 길이에 맞춰 나누지 않는다.
    ///
    /// <b>역할은 한 줄로 끝나야 한다.</b> 「Programming — Cooking &amp; Presentation」처럼
    /// 길게 적었더니 왼쪽 칸(440)을 넘겨 세 줄로 접혔고, 그러면 역할 덩어리가 이름을
    /// 아홉 배쯤 눌러 버려 6:4 가 9:1 로 보인다. 업무 부제는 적지 않는다.
    /// </summary>
    public static readonly string[,] CreditLines =
    {
        // 이름 칸이 빈 줄은 제목 카드다. 금선도 이름도 안 뜨고 역할 글자만 선다.
        { "네오위즈 K 게임\n아카데미 8기", "" },

        { "Director",    "권혁진" },
        { "Programming", "김기백 · 김은서" },
        { "Art",         "오규원" },
        { "QA",          "김중현 · 박은석 · 최상우" },

        // 로고 석 장의 머리말. 이 뒤로 네오위즈 · RAPA · MBC아카데미가 한 장씩 올라온다.
        { "Special Thanks", "" },
    };

    /// <summary>
    /// 로고가 다 지나간 뒤 왼쪽 칸에 <b>계속 떠 있는</b> 인사.
    ///
    /// 이름은 40초면 다 지나가는데 무대(손님이 먹고 쓰러지고 까마귀가 앉는 것)는 70초까지
    /// 간다. 그 사이 왼쪽 칸이 30초쯤 비어서, 화면 절반이 그냥 검은 채로 남았다.
    /// 이 한 줄이 그 자리를 메운다 — 까마귀 아이리스가 오므라들기 직전에 스러진다.
    /// </summary>
    public const string CreditThanksLine = "Thanks for Playing";

    /// <summary>크레딧 마지막 한 줄. 이것이 스러지면서 화면이 검어진다.</summary>
    public const string CreditClosingLine = "오늘 밤도, 불을 밝힙니다.";

    private IEnumerator PlayBadEnding(int day)
    {
        ScreenFade fade = ScreenFade.Instance;
        if (fade != null) yield return fade.FadeOut();

        if (narration != null) yield return narration.Play(BadEndingLines);

        // 「…그리고 다시, 첫날 밤」 뒤에 곧바로 가게가 열리면 하루가 시작된 티가 안 난다.
        // 게임을 처음 여는 길(OpenShop)과 **같은 순서**로 자막까지 거친다.
        if (narration != null) yield return narration.FadeOutLines(NarrationFadeSeconds);
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        if (dayTitle != null) yield return dayTitle.Play(DayCaption(day));
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        // 어둠 뒤에서 첫날을 차려 둔다. 손님은 아직 세우지 않는다 — 걸어 들어와야 한다.
        OpenOrderScreen(day);
        HideCustomerForEntrance();

        yield return RevealAndEnter(fade);
    }

    /// <summary>
    /// 검은 화면을 아이리스로 걷고 손님이 걸어 들어오게 한다. 다음날·배드엔딩이 같이 쓴다.
    ///
    /// 내레이션 판은 아이리스가 화면을 넘겨받은 **뒤에** 걷는다. 먼저 걷으면 한 프레임이지만
    /// 가게가 통째로 비친다. 도입부(RevealShop)에서 얻은 순서다.
    /// </summary>
    private IEnumerator RevealAndEnter(ScreenFade fade)
    {
        // 화면을 걷기 **전에** 말풍선을 내린다.
        //
        // 말풍선을 내리는 자리가 EnterCustomer 안이었는데, 그건 아이리스가 다 열린 뒤에 돈다.
        // 그래서 화면이 열리는 내내 아직 오지도 않은 손님의 대사가 떠 있었다.
        // 도입부(OpenShop)는 열기 전에 내려서 이 일이 없었다 — 같은 순서로 맞춘다.
        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);

        if (iris == null)
        {
            if (narration != null) narration.Hide();
            if (fade != null) yield return fade.FadeIn();
        }
        else
        {
            iris.Close();
            if (narration != null) narration.Hide();
            if (fade != null) fade.Clear();
            yield return iris.Open();
        }

        yield return EnterCustomer();
    }

    /// <summary>하루가 바뀔 때 뜨는 글자. 빌더가 꽂아 준다.</summary>
    [SerializeField] private DayTitleUI dayTitle;

    /// <summary>「튜토리얼을 보시겠습니까?」 물음판. 빌더가 꽂아 준다.</summary>
    [SerializeField] private TutorialAskUI tutorialAsk;

    /// <summary>
    /// 검은 화면에 아무것도 없이 머무는 시간(초).
    ///
    /// 자막이 뜨기 전과 스러진 뒤에 각각 한 번씩 둔다. 이 틈이 없으면 정산표 → 자막 →
    /// 가게가 한 동작으로 이어 붙어, 하루가 바뀐 것이 아니라 화면만 깜빡인 것으로 보인다.
    /// </summary>
    private const float BlackHoldSeconds = 1f;

    /// <summary>도입부 내레이션 글자가 스러지는 데 걸리는 시간(초).</summary>
    private const float NarrationFadeSeconds = 0.8f;

    /// <summary>
    /// 다음 날이 열린다. 문을 열 때(RevealShop)와 같은 박자다 —
    /// 화면이 통째로 검어지고, 「N일차」 가 떴다 스러지고, 구멍이 넓어지며 가게가 드러난다.
    ///
    /// 화면을 덮은 **뒤에** 주문 화면을 차린다. 먼저 차리면 어두워지는 도중에 다음 손님이 비친다.
    /// </summary>
    private IEnumerator OpenNextDay(int day)
    {
        ScreenFade fade = ScreenFade.Instance;

        if (fade != null) yield return fade.FadeOut();

        // 다 어두워진 뒤 한 박자. 곧바로 자막이 뜨면 정산표에서 이어 붙은 것처럼 보인다.
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        // 아침 소리. 음원이 아직 없으면 Sfx 가 조용히 지나간다.
        Sfx.Play("sfx_flow_morning", 0.7f);

        if (dayTitle != null) yield return dayTitle.Play(DayCaption(day));

        // 자막이 스러진 뒤에도 한 박자 두고 연다.
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        // 어둠 뒤에서 다음 날을 차려 둔다. 손님은 아직 세우지 않는다 —
        // 화면이 열리자마자 앉아 있으면 "이미 와 있던 손님" 이 되어 하루가 시작된 티가 안 난다.
        OpenOrderScreen(day);
        HideCustomerForEntrance();

        yield return RevealAndEnter(fade);
    }

    private void RefreshDayLabel(int day)
    {
        if (dayText != null) dayText.text = day + "일차  " + currentHour + ":00";

        // 시계 조각. 손님 한 명에 한 시간이라 지나간 손님 수 = 지금 시각 - 개점 시각이다.
        if (dayClock != null && EnsureDayManager())
            dayClock.Set(currentHour - OpenHour, dayManager.GetTargetCustomerCount(day));
    }

    /// <summary>손님을 맞는 화면을 연다. 조리 화면은 그 아래에서 계속 살아 있다.</summary>
    private void OpenOrderScreen(int day)
    {
        if (orderScreenUI == null || !EnsureOrderManager()) return;

        string dialogue = orderManager.CurrentDialogue;
        if (string.IsNullOrEmpty(dialogue)) return;

        orderScreenUI.Open(day, currentHour, dialogue, TodayRevenue);

        // 새 주문이 떴다. 여기서 남겨야 이 손님부터 다시 시작할 수 있다.
        SaveNow();
    }

    /// <summary>
    /// 튜토리얼 주문을 띄운다. 1일차보다 먼저 오는 손님 한 명이다.
    /// 저장은 하지 않는다 — 튜토리얼을 끝내야 저장이 시작된다.
    /// </summary>
    private void OpenTutorialOrder()
    {
        if (!EnsureOrderManager()) return;

        orderManager.SetScenario(TutorialManager.BuildScenario());

        currentHour = OpenHour;
        RefreshDayLabel(1);

        if (orderScreenUI != null)
        {
            orderScreenUI.Open(1, currentHour, orderManager.CurrentDialogue, TodayRevenue);
        }
    }

    /// <summary>
    /// 가게 문을 여는 장면. [게임시작] 을 누르면 이 흐름으로 들어간다.
    ///
    ///   검게 덮기 → 검은 화면에서 도입부 내레이션 → 주문 화면을 뒤에서 차려 놓기
    ///   → 가운데부터 밝아지기 → 빈 카운터 → 발소리 → 첫 손님이 밝아지며 등장
    ///
    /// 시작 화면에서 곧바로 손님이 서 있으면 장면이 툭 바뀌어 싸구려로 보인다.
    /// 손님이 걸어 들어오는 것을 보여 주면 가게가 열린 것으로 읽힌다.
    ///
    /// 손님이 드러나는 순서는 SwapCustomer(손님 교대)와 같다. 두 곳이 다르게 굴면
    /// 첫 손님만 유독 다르게 들어오는 것처럼 보인다.
    /// </summary>
    private IEnumerator OpenShop()
    {
        ScreenFade fade = ScreenFade.Instance;

        // 시작 화면이 켜진 채로 천천히 어두워진다. 먼저 끄면 검어지기 전에 조리 화면이 드러난다.
        if (fade != null) yield return fade.FadeOut();

        // 다 어두워진 뒤에 시작 화면을 치운다.
        if (titleScreen != null) titleScreen.Close();

        // 완전히 검어진 채로 한 박자 둔다.
        //
        // 곧바로 첫 줄이 찍히면 시작 화면을 누른 손과 글이 겹쳐, 화면이 넘어간 것이 아니라
        // 버튼에 글이 딸려 나온 것처럼 읽힌다. 여기서 한 번 끊어야 이야기가 시작된다.
        yield return new WaitForSecondsRealtime(blackHoldSeconds);

        // 검은 화면에서 도입부를 읽힌다. 여기서는 아직 가게가 차려지기 전이다.
        if (narration != null) yield return narration.Play();

        // 검은 화면을 한 박자 둔다. 곧바로 걷으면 시작 화면과 가게가 이어 붙은 것처럼 보인다.
        // 내레이션을 읽은 뒤라면 이미 충분히 머물렀으므로 마지막 줄을 넘긴 여운만 준다.
        yield return new WaitForSecondsRealtime(narration != null ? openingTailSeconds : blackHoldSeconds);

        // 내레이션 글자를 먼저 거둔다. 판은 그대로 둬서 검은 화면이 유지된다 —
        // 물음판이 마지막 줄 위에 겹쳐 뜨면 두 덩이가 한 화면에 나란히 보인다.
        if (narration != null) yield return narration.FadeOutLines(NarrationFadeSeconds);

        // 빈 검은 화면을 한 박자 둔다. 여기가 장면이 넘어가는 자리다.
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        // 「튜토리얼을 보시겠습니까?」 — 검은 화면에 이 판만 뜬다.
        //
        // **손님을 차리는 것은 답을 받은 뒤다.** 먼저 차려 두면 「아뇨」를 골랐을 때
        // 이미 만들어진 튜토리얼 주문을 버리고 1일차를 다시 열어야 한다.
        bool wantTutorial = true;
        if (tutorialAsk != null) yield return tutorialAsk.Ask(yes => wantTutorial = yes);

        // 「아뇨」면 튜토리얼을 아예 끝난 것으로 표시한다. 그래야 아래에서 1일차로 간다.
        if (!wantTutorial && TutorialManager.Instance != null) TutorialManager.Instance.Finish();

        // 덮여 있는 동안 주문 화면을 차려 둔다. 손님은 아직 안 보이게 지워 놓는다.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsRunning) OpenTutorialOrder();
        else if (EnsureDayManager()) dayManager.StartDay();

        // 미끄러지며 들어오는 연출은 끊는다. 걷히자마자 이미 가게에 와 있어야 한다.
        if (orderScreenUI != null) orderScreenUI.SnapOpen();

        HideCustomerForEntrance();

        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);

        // 오늘이 며칠이고 목표가 얼마인지. 자막은 내레이션(310)보다 앞 층(315)이라
        // 검은 화면 위에 그대로 얹힌다. 다음날(OpenNextDay)과 같은 자리·같은 글이다.
        if (dayTitle != null) yield return dayTitle.Play(DayCaption(EnsureDayManager() ? dayManager.CurrentDay : 1));

        // 자막이 스러진 뒤에도 한 박자. 그다음에 아이리스가 열린다.
        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        yield return RevealShop(fade);

        // 빈 카운터로 첫 손님이 걸어 들어온다. 말을 거는 것까지 EnterCustomer 가 맡는다.
        yield return EnterCustomer();
    }

    /// <summary>
    /// 검은 화면을 걷고 가게를 드러낸다.
    ///
    /// 아이리스가 있으면 가운데부터 바깥으로 밝아진다. 가장자리부터 걷는 보통 페이드와 달리
    /// 눈을 뜨는 것처럼 읽혀서, 검은 화면에서 도입부를 읽은 흐름과 이어진다.
    ///
    /// 넘겨주는 순서가 중요하다. 아이리스를 닫힌 채로 먼저 켜서 화면을 덮은 다음에
    /// 앞의 검은 판들을 치운다. 순서를 뒤집으면 한 프레임 동안 가게가 통째로 비친다.
    /// </summary>
    private IEnumerator RevealShop(ScreenFade fade)
    {
        // 화면이 걷히는 것과 같이 가게 소리가 올라온다.
        StartShopSounds();

        if (iris == null)
        {
            if (narration != null) narration.Hide();
            if (fade != null) yield return fade.FadeIn();
            yield break;
        }

        iris.Close();

        if (narration != null) narration.Hide();
        if (fade != null) fade.Clear();

        yield return iris.Open();
    }

    /// <summary>
    /// 가게 BGM 과 앰비언스를 켠다. 문을 열 때와 이어하기 둘 다 여기로 온다.
    ///
    /// 냄비 소리는 주문 화면이 조리대를 덮은 채로 시작하므로 낮게 켠다.
    /// 화면이 내려가면 OrderScreenUI 가 올린다.
    /// </summary>
    private void StartShopSounds()
    {
        Sfx.Loop("bgm_shop", Sfx.ShopBgm, 1.5f);
        Sfx.Loop("amb_street_night", Sfx.StreetAmbience, 1.5f);
        Sfx.Loop("amb_broth_boil", Sfx.KitchenAmbienceCovered, 1.5f);
        Sfx.Loop("amb_noodle_pot", Sfx.KitchenAmbienceCovered, 1.5f);
    }

    /// <summary>
    /// 튜토리얼 손님이 먹고 그냥 가는 장면.
    ///
    /// 결과창을 안 띄운다. 낼 돈이 없으니 보여 줄 정산도 없다.
    /// 손님이 스러진 뒤에 본편 1일차를 연다.
    /// </summary>
    private IEnumerator ServeDineAndDash()
    {
        if (orderScreenUI != null)
        {
            orderScreenUI.OpenEating(1, currentHour, TodayRevenue);
            yield return orderScreenUI.WaitForSlide();

            // 받자마자 한 마디. 먹기 전이라 컷신보다 앞이다.
            orderScreenUI.ShowBubble(true);
            orderScreenUI.StartTypingBubble(TutorialServedLine);
            yield return ReadLine(tutorialLineSeconds);

            // 천천히 들어서 후루룩, 눈 감고 한 박자.
            //
            // 우주도 따봉도 쓰지 않는다(EatingCutscene.PlayTutorial). 튜토리얼은 안내대로만
            // 넣게 해 둬서 늘 100점인데, 거기서 제일 센 연출을 다 보여 주면 본편에서 진짜로
            // 100점을 냈을 때 아무렇지 않다.
            if (EnsureCutscene()) yield return cutscene.PlayTutorial();
            else yield return new WaitForSecondsRealtime(eatSeconds);

            // 먹고 나서 하는 말. 맛 이야기로 시작해 게임을 어떻게 하는지까지 일러 준다.
            // 마지막 마디의 버튼이 [넵] 이 아니라 [안녕히 가세요.] 이고, 그걸 눌러야 넘어간다.
            bool spoken = false;
            orderScreenUI.PlayLines(TutorialClosingLines, "안녕히 가세요.", () => spoken = true);
            while (!spoken) yield return null;

            orderScreenUI.ShowBubble(false);
        }

        // 정확도 창. 튜토리얼이 끝났다는 안내가 여기에 실린다.
        //
        // 기획 9.2 대로 채점도 정산도 하지 않으므로 EvaluateRamen 을 부르지 않는다.
        // 누적 매출·손님 수·평균 정확도에는 아무것도 남지 않는다.
        if (orderResultUI != null)
        {
            bool closed = false;
            orderResultUI.OpenTutorial(TodayRevenue, TutorialEndLine, () => closed = true);
            while (!closed) yield return null;
        }

        // 손님이 나간다.
        yield return FadeCustomer(0f, 1f, exitSeconds);

        // 여기서부터 본편이다. 1일차를 열면 DayManager 가 첫 주문을 만든다.
        if (EnsureDayManager()) dayManager.StartDay();

        // 본편 첫 손님도 걸어 들어온다. StartDay 가 손님을 세워 두었으므로 바로 물린다.
        HideCustomerForEntrance();
        yield return EnterCustomer();
    }

    /// <summary>
    /// 목표 미달로 [다시하기]를 눌렀을 때 온다. 버린 판의 누계를 지운다.
    ///
    /// RamenCalculator 는 DayManager 가 비워 주지만 여기 넷은 아무도 안 건드린다.
    /// 안 지우면 1일차로 돌아가도 상단바의 「누적 수익」과 최종 성적표가 버린 판을 그대로 안고 간다.
    /// currentHour 는 HandleDayStarted 가 하루를 열 때마다 되돌리므로 여기서 따로 안 만진다.
    /// </summary>
    private void HandleGameRestart()
    {
        totalRevenue = 0;
        servedCount = 0;
        accuracySum = 0f;
        perfectCount = 0;
        RefreshRevenue();

        // 끝난 판의 저장이 남아 있으면 다음에 켰을 때 그 판이 되살아난다.
        SaveSystem.Delete();

        // 곧 StartDay 가 1일차를 연다. 그때 배드엔딩을 끼워야 한다.
        badEndingPending = true;
    }

    /// <summary>5일차까지 다 팔면 온다. 하루 정산과 달리 전체 누계를 보여 준다.</summary>
    /// <summary>
    /// 닷새를 다 판 뒤의 엔딩 글. 도입부와 같은 연출로 검은 화면에 한 줄씩 쌓인다.
    ///
    /// 도입부가 「그리고 오늘 밤…」으로 열어 둔 것을 여기서 닫는다. 「그 말에 꼭 맞는 한 그릇」은
    /// 도입부 넷째 줄을 그대로 되받은 것이다.
    /// </summary>
    private static readonly string[] EndingLines =
    {
        "닷새 밤이 지나갔다.",

        // 한 줄로 두면 560 폭을 넘겨(566) 아무 데서나 접힌다. 쉼표에서 끊어 두 줄로 보이되
        // 한 덩이라 클릭은 한 번이다. 배드엔딩 둘째 줄과 같은 처리다.
        "누군가는 허기를, 누군가는 할 말을 안고 왔다.",

        "그 말에 꼭 맞는 한 그릇이었는지는",
        "먹고 간 사람만이 안다.",
        "오늘도 포렴 너머로 불빛이 새어 나간다.",
        "누군가 그 앞에 멈춰 설 때까지.",
    };

    private void HandleGameCompleted()
    {
        float average = servedCount > 0 ? accuracySum / servedCount : 0f;

        Debug.Log("[영업 종료] 누적 매출 " + totalRevenue.ToString("N0") + "원 / 평균 정확도 "
                  + average.ToString("F1") + "% / 완벽 " + perfectCount + "건 / 총 " + servedCount + "건");

        // 5일을 다 팔았으면 이어할 것이 없다. 남겨 두면 다음에 켰을 때 끝난 판이 되살아난다.
        SaveSystem.Delete();

        // **성적표가 먼저, 이야기가 그다음이다.** 숫자를 확인하고 [확인]을 누르면
        // 엔딩 글 → 암전 → 크레딧으로 이어진다.
        //
        // 예전에는 엔딩 글부터 띄우고 그 뒤에 성적표를 올렸는데, 그때 화면은 전환 판(300)이
        // 검게 덮고 있고 성적표는 팝업 층(185)이라 **그 밑에 깔려 보이지도 눌리지도 않았다.**
        // 순서를 뒤집으니 성적표는 가게 화면 위에 제 배경(70% 검정)으로 뜨고, 검게 덮는 일은
        // 엔딩 글의 몫이 된다 — 층 문제가 아예 사라진다.
        if (finalResultUI != null)
        {
            finalResultUI.Open(totalRevenue, average, perfectCount, servedCount,
                               () => StartCoroutine(PlayEnding()));
        }
        else
        {
            Debug.LogWarning("[영업 종료] 씬에 FinalResultUI 가 없어 성적표를 건너뜁니다.");
            StartCoroutine(PlayEnding());
        }
    }

    /// <summary>
    /// 엔딩. 성적표를 걷고, 검은 화면에 글이 한 줄씩 쌓이고, 다 읽으면 암전을 지나 크레딧으로 간다.
    ///
    /// 크레딧을 부르고 끝난다. 여기서 검은 판을 걷지 않는다 — 크레딧이 그 어둠을 그대로
    /// 넘겨받아 소리를 마저 잦아들게 한 뒤에 무대를 차린다(CreditsSequence.Prelude).
    /// </summary>
    private IEnumerator PlayEnding()
    {
        // 성적표를 먼저 걷는다. 덮이고 나서 걷으면 검은 화면 뒤에서 판이 사라지는 셈이라
        // 걷는 것이 안 보이고, 덮이는 동안 남겨 두면 판이 같이 어두워지다 툭 없어진다.
        if (finalResultUI != null) finalResultUI.Close();

        ScreenFade fade = ScreenFade.Instance;
        if (fade != null) yield return fade.FadeOut();

        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        if (narration != null)
        {
            yield return narration.Play(EndingLines);
            yield return narration.FadeOutLines(NarrationFadeSeconds);
        }

        yield return new WaitForSecondsRealtime(BlackHoldSeconds);

        // 글자만 걷고 검은 판은 그대로 둔다. 가게로 돌아갈 일이 없으니 걷을 이유가 없고,
        // 크레딧이 이 어둠을 그대로 넘겨받는다.
        if (narration != null) narration.Hide();

        CreditsSequence.Begin(CreditsSequence.Exit.Reload);
    }

    /// <summary>
    /// 그릇을 제출 영역에 놓으면 호출된다.
    /// 채점하고 매출에 더한 뒤 바로 다음 손님을 받는다.
    /// </summary>
    public void SubmitRamen(RamenState ramenState)
    {
        if (ramenState == null)
        {
            Debug.LogWarning("[GameManager] SubmitRamen에 null이 들어왔습니다.");
            return;
        }

        // B의 RamenState는 그릇 딕셔너리(selectedIngredients)만 보관한다.
        // 폐기 딕셔너리는 생성자에서 인자로 받기만 하고 저장하지 않으므로 여기서 읽을 수 없다.
        // 그래서 폐기분 로그는 Bowl이 제출 직전에 직접 남긴다.
        Debug.Log("[제출] 그릇: " + Describe(ramenState.selectedIngredients));

        // 결과창에 얹을 그릇 사진. **여기서 찍어야 한다** — Bowl.Submit 은 이 함수가 돌아가자마자
        // ClearBowl 로 안을 비운다. 한 프레임만 늦어도 빈 그릇이 찍힌다.
        if (orderResultUI != null) orderResultUI.CaptureBowl();

        if (!EnsureOrderManager())
        {
            Debug.LogWarning("[GameManager] 씬에 OrderManager가 없어 채점을 건너뜁니다.");
            return;
        }

        // 튜토리얼 손님은 먹고 그냥 간다. 채점도 정산도 하지 않는다.
        //
        // 기획 9.2 는 튜토리얼을 손님 수·운영시간·누적 매출·평균 정확도에서 모두 빼라고 한다.
        // 여기서 EvaluateRamen 을 부르면 RamenCalculator 의 당일 집계(todayServedCount 등)가
        // 올라가 버리므로 아예 부르지 않는다. 안내대로만 넣게 해 둬서 어차피 정답 한 그릇이다.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsRunning)
        {
            TutorialManager.Instance.Finish();
            Debug.Log("[튜토리얼] 손님이 먹고 그냥 갔습니다. 매출·정확도에 남지 않습니다.");
            StartCoroutine(ServeDineAndDash());
            return;
        }

        // 손님 한마디는 정확도 구간이 아니라 **어긋난 방향**으로 고른다(ReactionLines.Feedback).
        // 채점보다 먼저 세어 둔다 — 여기 넘기는 딕셔너리는 그릇이 쥐고 있던 것이라, 제출이
        // 끝나면 비워진다.
        NoteBowlDirection(ramenState);

        int price = orderManager.EvaluateRamen(ramenState);
        totalRevenue += price;

        // 채점 직후에만 읽을 수 있다. NextDay가 당일 집계를 지우기 전에 여기서 쌓아 둔다.
        if (EnsureRamenCalculator())
        {
            float accuracy = ramenCalculator.LastAccuracy;
            accuracySum += accuracy;
            servedCount++;
            if (accuracy >= RamenCalculator.PERFECT_ACCURACY) perfectCount++;
        }

        RefreshRevenue();

        Debug.Log("[정산] 판매 금액 " + price.ToString("N0") + "원 / 누적 매출 " + totalRevenue.ToString("N0") + "원");

        // 손님이 먹는 장면을 먼저 보여 주고, 그다음에 결과창을 올린다.
        float shown = EnsureRamenCalculator() ? ramenCalculator.LastAccuracy : 0f;
        StartCoroutine(ServeCustomer(shown, price));
    }

    /// <summary>
    /// 제출한 그릇이 주문과 어느 쪽으로 어긋났는지 ReactionLines 에 적어 둔다.
    ///
    /// 3대 요소(타래·육수·면)가 어긋났는지는 채점하는 쪽(B의 RamenCalculator)에 물어본다.
    /// 같은 판정을 여기서 한 벌 더 쓰면 채점과 대사가 따로 놀 수 있다.
    /// </summary>
    private void NoteBowlDirection(RamenState ramenState)
    {
        Dictionary<IngredientType, int> target = orderManager.CurrentTargetRecipe;
        CustomerOrder order = orderManager.CurrentOrder;

        bool coreFailed = true;
        if (order != null && EnsureRamenCalculator())
        {
            coreFailed = !ramenCalculator.ValidateCoreIngredients(
                order.ramenType, target, ramenState.selectedIngredients, out _);
        }

        ReactionLines.NoteBowl(target, ramenState.selectedIngredients, coreFailed);
    }

    /// <summary>
    /// 라멘을 낸 뒤부터 결과창이 뜨기까지.
    ///
    /// 손님 화면을 다시 띄우되 손님은 그대로 둔다. 방금 주문한 사람이 먹어야지 다른 사람이
    /// 먹으면 안 되므로 Open 이 아니라 OpenEating 을 부른다. Open 은 얼굴을 새로 뽑는다.
    ///
    /// 먹는 장면을 끄지 않고 그 위에 결과창을 올린다. 빌더가 결과창을 손님 화면보다 뒤에
    /// 만들어서 위에 얹히기 때문이다. 끄면 조리 화면이 한 번 비쳤다 사라진다.
    ///
    /// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰도 연출은 흘러야 한다.
    /// </summary>
    private IEnumerator ServeCustomer(float accuracy, int price)
    {
        if (orderScreenUI != null)
        {
            int day = EnsureDayManager() ? dayManager.CurrentDay : 1;
            orderScreenUI.OpenEating(day, currentHour, TodayRevenue);

            // 화면이 다 올라온 다음에 먹는 연출을 시작한다.
            yield return orderScreenUI.WaitForSlide();

            // 받자마자 들이켜지 않는다. 고맙다고 한 마디 하고 한 박자 쉰 뒤에 그릇을 든다.
            // 말풍선은 컷신이 시작하면서 스스로 치운다.
            orderScreenUI.ShowBubble(true);
            orderScreenUI.StartTypingBubble(ReactionLines.Served());
            yield return ReadLine(servedLineSeconds);
            yield return new WaitForSecondsRealtime(servedPauseSeconds);

            // 컷신이 없으면(빌더를 안 돌린 경우) 예전처럼 잠깐 기다리기만 한다.
            if (EnsureCutscene()) yield return cutscene.Play(accuracy);
            else yield return new WaitForSecondsRealtime(eatSeconds);
        }

        // 결과창이 없으면(빌더를 안 돌린 경우) 예전처럼 바로 넘어간다.
        if (orderResultUI == null)
        {
            AdvanceCustomer();
            yield break;
        }

        // 100% 면 팻말이 팍 떴다가 스윽 사라진다(기획서 v1.2 7.3).
        // 결과창보다 먼저 띄운다. 결과창 위에 얹으면 숫자를 가리고,
        // 먹는 컷신 중에 띄우면 두 연출이 겹쳐 어느 쪽도 안 보인다.
        if (accuracy >= RamenCalculator.PERFECT_ACCURACY)
        {
            if (perfectSign == null) perfectSign = FindFirstObjectByType<PerfectSign>();
            if (perfectSign != null)
            {
                yield return perfectSign.Play();

                // 팻말이 다 스러진 뒤 반 박자 두고 결과창을 올린다.
                // 붙여 놓으면 팻말이 사라지는 것과 창이 뜨는 것이 한 동작처럼 보여, 둘 다 안 읽힌다.
                yield return new WaitForSecondsRealtime(PerfectSignTailSeconds);
            }
        }

        // 결과창을 올린다. 손님은 그대로 세워 둔다.
        // 결과창이 손님을 가리므로 여기서 스러뜨리면 나가는 모습을 아무도 못 보고,
        // [확인]을 눌렀을 때는 이미 사라진 뒤라 손님이 순간이동한 것처럼 보인다.
        // 나가는 모습은 결과창이 걷힌 다음에 보여 준다(SwapCustomer).
        orderResultUI.Open(accuracy, price, TodayRevenue);

        // 말풍선만 먼저 치운다. 지금은 결과창에 가려 있어 사라지는 티가 안 난다.
        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);
    }

    /// <summary>
    /// 앞 손님이 나가고 다음 손님이 들어오기까지.
    ///
    /// 화면은 계속 켜 둔다. 껐다 켜면 카운터가 한 번 깜빡이고, 나가는 모습도 들어오는 모습도
    /// 볼 수 없다.
    ///
    /// [확인]을 눌러 결과창이 걷힌 다음에 온다. 그래야 손님이 스러지는 것이 보인다.
    /// </summary>
    private IEnumerator SwapCustomer(int day)
    {
        // 나간다. 어두워지다가 지워진다.
        yield return FadeCustomer(0f, 1f, exitSeconds);

        // 새 손님을 세우고 같은 프레임에 옆으로 물러난 인영으로 만든다.
        OpenOrderScreen(day);
        HideCustomerForEntrance();

        // 빈 카운터로 걸어 들어온다. 발소리가 이 사이를 채운다.
        yield return EnterCustomer();
    }

    /// <summary>
    /// 걸어 들어오기 직전 상태로 만들어 둔다. 옆으로 물러난 채 아직 안 보인다.
    ///
    /// OrderScreenUI.Open 이 스러짐을 0 으로 되돌리므로, 손님을 세운 그 프레임 안에서
    /// 곧바로 불러야 한다. 코루틴은 화면이 그려지기 전에 도니 사이에 손님이 번쩍이지 않는다.
    /// </summary>
    private void HideCustomerForEntrance()
    {
        CustomerAppearance look = orderScreenUI != null ? orderScreenUI.Appearance : null;
        if (look == null) return;

        look.SetOffset(new Vector2(walkInDistance, -walkSink));
        look.SetTint(0f, 0f);

        // 걷는 동안에는 구멍을 메운 인영 한 장으로 선다. 원래 그림을 까맣게 칠하면
        // 팔과 몸 사이 같은 빈 자리로 배경이 비쳐 몸에 구멍이 뚫린 것처럼 보인다.
        look.ShowSilhouette(true);
    }

    /// <summary>
    /// 빈 카운터로 새 손님이 걸어 들어온다.
    ///
    /// 검은 인영이 옆에서 한 걸음씩 다가와 자리에 서고, 거기서 제 색으로 밝아진다.
    /// 걸음 수와 박자는 발소리(Footsteps)에서 그대로 가져온다. 소리와 그림이 어긋나면
    /// 발소리가 손님 것이 아니라 어디 딴 데서 나는 것처럼 들린다.
    ///
    /// 부르기 전에 <see cref="HideCustomerForEntrance"/> 로 옆에 물러나 있어야 한다.
    /// </summary>
    private IEnumerator EnterCustomer()
    {
        CustomerAppearance look = orderScreenUI != null ? orderScreenUI.Appearance : null;

        float stride = EnsureFootsteps() ? footsteps.Stride : 0.42f;
        int steps = Mathf.Max(1, Mathf.RoundToInt(emptySeconds / stride));

        // 첫 발은 늦추지 않는다. 그림의 첫 걸음과 같이 떨어져야 한 사람의 발소리로 들린다.
        if (footsteps != null) footsteps.Walk(steps * stride, 0f);

        if (look == null)
        {
            yield return new WaitForSecondsRealtime(steps * stride);
            yield break;
        }

        // 걸어오는 동안에는 말이 없다. 아직 자리에 서지도 않았는데 말풍선이 뜨면
        // 대사가 허공에서 나오는 것처럼 보인다.
        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);

        for (int i = 0; i < steps; i++)
        {
            // 첫 걸음에 인영이 배어 나온다. 대뜸 새까만 사람이 서 있으면 튄다.
            yield return StepIn(look, i, steps, i == 0 ? 0f : 1f, stride);
        }

        // 자리에 서면 인영을 내려놓고 제 그림이 어둠에서 밝아진다.
        // 밝아지기 전에 바꿔야 한다 — 인영은 흰 그림이라 밝히면 하얀 덩어리가 된다.
        // 내려 두었던 몸도 여기서 제자리로 올라온다.
        look.ShowSilhouette(false);
        yield return LightCustomer(look, enterSeconds);

        // 다 들어온 뒤에 말을 건다.
        //
        // 대사를 처음부터 다시 친다. 말풍선을 감춘 채로 화면을 차려 두는 동안에도 타자기는
        // 돌아서, 여기까지 오면 이미 다 찍혀 있다. 다시 쳐야 찍히는 것이 보인다.
        if (orderScreenUI != null)
        {
            orderScreenUI.ShowBubble(true);
            orderScreenUI.ReplayCurrentLine();
        }
    }

    /// <summary>
    /// 한 걸음. 자리 쪽으로 한 칸 다가오면서 몸이 한 번 떴다 내린다.
    ///
    /// 사인 반 주기라 걸음의 처음과 끝에서 가장 낮다. 발소리가 나는 순간이 거기라,
    /// 소리가 날 때 발이 바닥에 닿아 있는 것으로 보인다.
    /// </summary>
    private IEnumerator StepIn(CustomerAppearance look, int index, int steps, float fromAlpha, float seconds)
    {
        float from = 1f - (float)index / steps;
        float to = 1f - (float)(index + 1) / steps;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float t = elapsed / seconds;

            look.SetOffset(new Vector2(Mathf.Lerp(from, to, t) * walkInDistance,
                                       Mathf.Sin(t * Mathf.PI) * walkBob - walkSink));
            look.SetTint(0f, Mathf.Lerp(fromAlpha, 1f, t));

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        look.SetOffset(new Vector2(to * walkInDistance, -walkSink));
        look.SetTint(0f, 1f);
    }

    /// <summary>
    /// 한 마디를 읽힌다. 정해진 시간을 기다리되, 다 읽은 사람이 누르면 바로 넘어간다.
    ///
    /// 한 프레임 흘리고 시작한다. 앞 연출을 넘기려고 누른 그 입력이 같은 프레임에
    /// "다 읽었다" 로 한 번 더 읽히면 한 마디가 통째로 지나간다.
    /// </summary>
    private IEnumerator ReadLine(float seconds)
    {
        yield return null;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Pressed()) yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>아무 키나, 또는 마우스 왼쪽.</summary>
    private static bool Pressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    /// <summary>
    /// 인영에서 제 색으로. 진하기는 그대로 두고 밝기만 올린다.
    /// 걷는 동안 내려 두었던 몸도 같이 제자리로 올라온다 — 자리에 들어서는 한 동작이다.
    /// </summary>
    private IEnumerator LightCustomer(CustomerAppearance look, float seconds)
    {
        if (seconds <= 0f)
        {
            look.SetTint(1f, 1f);
            look.SetOffset(Vector2.zero);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / seconds);
            look.SetTint(t, 1f);
            look.SetOffset(new Vector2(0f, -walkSink * (1f - t)));

            yield return null;
        }

        look.SetTint(1f, 1f);
        look.SetOffset(Vector2.zero);
    }

    /// <summary>손님을 from 에서 to 까지 스러뜨리거나 밝힌다. 시간은 실시간으로 잰다.</summary>
    private IEnumerator FadeCustomer(float from, float to, float seconds)
    {
        CustomerAppearance look = orderScreenUI != null ? orderScreenUI.Appearance : null;
        if (look == null) yield break;

        if (seconds <= 0f)
        {
            look.SetFade(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            look.SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }

        look.SetFade(to);
    }

    // ── 크레딧이 쓰는 문 ──────────────────────────────────────────
    //
    // 크레딧은 무대를 직접 몬다. 하루 진행·정산·다음 손님(DayManager·AdvanceCustomer)은
    // 타지 않는다 — 5일이 이미 끝난 자리에서 도는 것이라 진행할 하루가 없다.
    // 아래 셋은 **여기 있는 것을 그대로 쓰기 위한 문**이고, 본편 흐름은 하나도 안 바뀐다.

    /// <summary>손님 하나가 옆에서 걸어 들어와 자리에 서고 말을 건다.</summary>
    public IEnumerator CreditsWalkIn()
    {
        HideCustomerForEntrance();
        yield return EnterCustomer();
    }

    /// <summary>손님이 스러져 나간다.</summary>
    public IEnumerator CreditsWalkOut()
    {
        yield return FadeCustomer(0f, 1f, exitSeconds);
    }

    /// <summary>
    /// 그릇에 레시피대로 담는다. 담는 소리가 나라고 실제로 담는다 —
    /// 조리 화면은 주문 화면에 덮여 있어 보이지는 않는다.
    /// </summary>
    public IEnumerator CreditsPour(Dictionary<IngredientType, int> recipe)
    {
        Bowl bowl = CreditsBowl();
        if (bowl == null || recipe == null) yield break;

        // 버리는 것이 아니라 그냥 비운다. 폐기 소리와 화면 거칠어짐은 크레딧에 얹을 것이 아니다.
        if (!bowl.IsEmpty) bowl.ClearQuietly();
        yield return PourRecipe(bowl, recipe);
        yield return new WaitForSecondsRealtime(PourSeconds);
    }

    /// <summary>
    /// 크레딧이 담을 그릇을 집어 온다.
    ///
    /// ⚠️ <b>꺼져 있는 것까지 찾고, 찾은 뒤에는 켜 둔다.</b> 주문 화면이 열릴 때마다
    /// <c>ShowCookingProps(false)</c> 가 그릇을 통째로 끄는데, 기본값(활성만 찾기)으로 두면
    /// 그 뒤로 null 이 돌아와 <see cref="CreditsPour"/> 가 통째로 건너뛰어진다. 그러면 크레딧
    /// 내내 <b>재료 담는 소리가 한 번도 안 난다</b> — 2026-09-16 에 실측으로 확인했다
    /// (주문 화면이 열린 t≈10초부터 끝까지 그릇이 빈 채였다).
    ///
    /// 켜 두어도 화면에는 안 나온다. 그릇은 캔버스 층 0 이고 주문 화면은 180 이라, 그 위에
    /// 깔린 불투명한 밤 배경에 가려진다.
    /// </summary>
    private Bowl CreditsBowl()
    {
        Bowl bowl = FindFirstObjectByType<Bowl>(FindObjectsInactive.Include);
        if (bowl == null) return null;

        // 타래·육수는 붓는 장면을 코루틴으로 돌린다. 꺼진 오브젝트에서는 코루틴이 시작조차
        // 안 되고 콘솔에 경고만 쌓이므로, 담기 전에 켜 둔다.
        if (!bowl.gameObject.activeSelf) bowl.gameObject.SetActive(true);
        return bowl;
    }

    /// <summary>담아 둔 것을 비운다. 다음 손님이 앞 손님 그릇에 얹지 않게.</summary>
    public void CreditsClearBowl()
    {
        Bowl bowl = CreditsBowl();
        if (bowl != null && !bowl.IsEmpty) bowl.ClearQuietly();
    }

    /// <summary>손님 화면. 크레딧이 무대를 세우는 데 쓴다.</summary>
    public OrderScreenUI CreditsScreen { get { return orderScreenUI; } }

    /// <summary>시식 연출. 없으면 null.</summary>
    public EatingCutscene CreditsCutscene { get { return EnsureCutscene() ? cutscene : null; } }

    /// <summary>인스펙터가 비어 있으면 씬에서 한 번 찾아 둔다.</summary>
    private bool EnsureFootsteps()
    {
        if (footsteps == null) footsteps = FindFirstObjectByType<Footsteps>(FindObjectsInactive.Include);
        return footsteps != null;
    }

    /// <summary>인스펙터가 비어 있으면 씬에서 한 번 찾아 둔다.</summary>
    private bool EnsureCutscene()
    {
        if (cutscene == null) cutscene = FindFirstObjectByType<EatingCutscene>(FindObjectsInactive.Include);
        return cutscene != null;
    }

    /// <summary>
    /// 결과창의 [확인]이 부른다. 여기서부터 다음 손님이다.
    /// 진행은 DayManager가 쥔다. 이 안에서 다음 주문을 만들거나 오늘 영업을 마감한다.
    /// 동기 호출이라 돌아온 직후엔 CurrentDialogue가 이미 갱신돼 있다.
    /// </summary>
    public void AdvanceCustomer()
    {
        if (!EnsureDayManager()) return;

        // 손님 한 명이 갔으니 한 시간 흐른다. 다음 주문 화면에 새 시각이 뜨도록 먼저 올린다.
        currentHour++;
        RefreshDayLabel(dayManager.CurrentDay);

        // 오늘 마지막 손님인지 먼저 센다. OnCustomerServed() 안에서 하루가 마감되고 정산 팝업이
        // 곧바로 열리기 때문에, 부르고 난 뒤에는 그 앞에 「주문마감!」 을 끼울 자리가 없다.
        bool dayEnds = dayManager.CurrentCustomerCount + 1 >= dayManager.TargetCustomerCount;
        if (dayEnds)
        {
            StartCoroutine(CloseShop());
            return;
        }

        dayManager.OnCustomerServed();                            // 그 안에서 다음 주문이 만들어진다
        StartCoroutine(SwapCustomer(dayManager.CurrentDay));      // 그 안에서 저장된다
    }

    /// <summary>
    /// 오늘 장사가 끝났다.
    ///
    /// 마지막 손님이 스러져 나가고 → 빈 카운터를 한 박자 두고 → 「주문마감」 이 한 자씩
    /// 박히고 → 화면이 검게 물드는 동안 글자만 남았다가 스러지고 → 정산 팝업.
    ///
    /// 손님을 여기서 내보내는 까닭은, 마지막 손님에게는 <see cref="SwapCustomer"/> 가
    /// 오지 않기 때문이다. 그대로 두면 앉아 있는 손님 얼굴 위로 글자가 떨어진다.
    ///
    /// 팝업은 <see cref="DayManager.OnCustomerServed"/> 안에서 열린다. 그래서 그 호출을
    /// 화면이 다 어두워질 때까지 미룬다 — 먼저 부르면 팝업이 페이드 도중에 비친다.
    /// </summary>
    private IEnumerator CloseShop()
    {
        // 마지막 손님과 그 앞의 그릇이 같이 스러진다. 그릇만 남으면 손님이 들고 간 것처럼 보이고,
        // 그릇만 툭 꺼지면 빈 카운터가 아니라 "그릇이 사라진 카운터" 가 된다.
        Coroutine bowlAway = orderScreenUI != null
            ? StartCoroutine(orderScreenUI.FadeServedBowl(exitSeconds))
            : null;

        yield return FadeCustomer(0f, 1f, exitSeconds);
        if (bowlAway != null) yield return bowlAway;

        if (closedSign != null)
        {
            yield return new WaitForSecondsRealtime(closedSignDelay);   // 빈 카운터
            yield return closedSign.Play();               // 주 · 문 · 마 · 감
            yield return closedSign.FadeAway();
        }

        // 화면을 검게 덮지 않는다. 주문 화면(빈 포장마차)을 그대로 두고 그 위에 정산표를 얹는다.
        // 예전에는 여기서 FadeOut → 주문 화면 Close → 팝업 → FadeIn 이었는데, 정산표가
        // 검은 바탕에 떠서 가게와 끊겼다. 하루를 닫는 자리는 가게에 남아 있는 편이 맞다.
        dayManager.OnCustomerServed();                            // 그 안에서 하루가 마감된다
        SaveNow();                                                // 하루가 끝난 자리도 남긴다
    }

    /// <summary>인스펙터가 비어 있으면 씬에서 한 번 찾아 둔다.</summary>
    private bool EnsureOrderManager()
    {
        if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();
        return orderManager != null;
    }

    private bool EnsureDayManager()
    {
        if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
        return dayManager != null;
    }

    private bool EnsureRamenCalculator()
    {
        if (ramenCalculator == null) ramenCalculator = FindFirstObjectByType<RamenCalculator>();
        return ramenCalculator != null;
    }

    /// <summary>오늘 목표를 이미 넘겼는가. 넘긴 순간 한 번만 알리려고 들고 있는다.</summary>
    private bool goalReached;

    /// <summary>목표를 넘겼을 때 수익 글자색. 넘기 전 색은 빌더가 준 것을 그대로 쓴다.</summary>
    private static readonly Color GoalColor = new Color32(0x2E, 0x7D, 0x32, 0xFF);

    private Color? normalRevenueColor;

    /// <summary>
    /// 상단바 수익. 오늘 번 돈과 오늘 목표를 같이 싣는다.
    ///
    /// 목표를 넘기는 순간에만 색이 바뀌고 띡 소리가 한 번 난다. 그 이상은 만들지 않는다 —
    /// 조리하는 내내 눈에 들어오는 자리라 뭐가 더 붙으면 손이 그쪽으로 끌린다.
    /// </summary>
    private void RefreshRevenue()
    {
        if (revenueText == null) return;

        if (normalRevenueColor == null) normalRevenueColor = revenueText.color;

        int today = TodayRevenue;
        int goal = EnsureDayManager() ? dayManager.TargetProfit : 0;

        revenueText.text = "금일 수익 : " + today.ToString("N0") + " / " + goal.ToString("N0") + "₩";

        bool reached = goal > 0 && today >= goal;
        if (reached && !goalReached)
        {
            goalReached = true;
            Sfx.Play("sfx_ui_count", 0.5f);
        }

        revenueText.color = reached ? GoalColor : normalRevenueColor.Value;
    }

    /// <summary>
    /// 하루가 열릴 때 뜨는 자막에 싣는 글. 「N일차」 밑에 「목표 35,000원」.
    ///
    /// 목표는 DayManager 가 일차로 정한다(35,000 / 42,000 / 56,000). 여기서 표를 베끼지 않는다 —
    /// 베껴 두면 그쪽이 바뀌었을 때 자막만 옛 숫자를 말한다.
    /// </summary>
    private string DayCaption(int day)
    {
        int goal = EnsureDayManager() ? dayManager.TargetProfit : 0;
        return day + "일차\n목표 " + goal.ToString("N0") + "원";
    }

    private static string Describe(Dictionary<IngredientType, int> dict)
    {
        if (dict == null || dict.Count == 0) return "(비어 있음)";

        var sb = new StringBuilder();
        foreach (var pair in dict)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(pair.Key).Append('=').Append(pair.Value);
        }
        return sb.ToString();
    }
}
