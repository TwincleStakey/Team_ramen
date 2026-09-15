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

    /// <summary>지금까지 판 금액의 합. 재료비가 없어져서 매출이 곧 성적표다. (기획 확정)</summary>
    private int totalRevenue;

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

#if UNITY_EDITOR
    /// <summary>
    /// 개발용 건너뛰기. F2 를 누르면 그릇을 한 번에 채우고 제출한다.
    ///
    /// 손님 앞에 놓이는 그릇을 손볼 때마다 재료를 여덟 번 끌어 담아야 하는 것이 번거로워서 둔다.
    /// 타이틀의 F1 과 같이 에디터에서만 듣는다 — 빌드에는 이 키가 아예 없다.
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f2Key.wasPressedThisFrame) return;

        StartCoroutine(FillAndSubmit());
    }

    private IEnumerator FillAndSubmit()
    {
        Bowl bowl = FindFirstObjectByType<Bowl>();
        if (bowl == null)
        {
            Debug.LogWarning("[F2] 조리 화면에 그릇이 없습니다.");
            yield break;
        }

        // 재료 그림은 재료통이 들고 있다. 그림 없이 넣으면 그릇에 아무것도 안 올라간다.
        var icons = new Dictionary<IngredientType, Sprite>();
        foreach (IngredientSlot slot in FindObjectsByType<IngredientSlot>(FindObjectsSortMode.None))
        {
            icons[slot.type] = slot.bowlSprite;
        }

        TutorialManager tutorial = TutorialManager.Instance;
        bool inTutorial = tutorial != null && tutorial.IsRunning;

        if (inTutorial)
        {
            // 안내가 시키는 차례를 그대로 따라간다. 다른 것을 넣으면 안내가 그 자리에 멈춰 선다.
            // Tab(주문서)·B(레시피책) 차례는 재료가 아니라서 넘길 수 없다 — 사람이 눌러 준다.
            while (tutorial.IsRunning && !tutorial.ReadyToSubmit)
            {
                if (tutorial.CurrentStep is IngredientType step)
                {
                    icons.TryGetValue(step, out Sprite icon);
                    bowl.TryAdd(step, icon);
                    yield return new WaitForSeconds(0.05f);
                    continue;
                }

                Debug.LogWarning("[F2] 재료가 아닌 차례입니다(Tab 이나 B). 그것만 누르고 다시 F2.");
                yield break;
            }
        }
        else
        {
            foreach (IngredientType type in DebugRecipe)
            {
                icons.TryGetValue(type, out Sprite icon);
                bowl.TryAdd(type, icon);
                yield return new WaitForSeconds(0.05f);
            }
        }

        // 타래와 육수는 붓는 장면이 끝나야 그릇 그림이 자리를 잡는다. 그 전에 제출하면
        // 손님 앞에 붓다 만 그릇이 올라간다.
        yield return new WaitForSeconds(1.2f);
        bowl.Submit();
    }

    /// <summary>튜토리얼이 끝난 뒤 F2 가 마는 한 그릇. 정답일 필요는 없다.</summary>
    private static readonly IngredientType[] DebugRecipe =
    {
        IngredientType.ShioTare, IngredientType.Broth, IngredientType.ThickNoodles,
        IngredientType.Chashu, IngredientType.GreenOnion, IngredientType.Egg, IngredientType.Nori,
    };
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
        RefreshDayLabel(day);
        OpenOrderScreen(day);
    }

    private void RefreshDayLabel(int day)
    {
        if (dayText != null) dayText.text = day + "일차  " + currentHour + ":00";
    }

    /// <summary>
    /// 조리 화면의 ? 버튼이 부른다. 손님 주문 내역을 여닫는다.
    /// 기본 레시피는 B 키로 여는 레시피 책이 맡는다. 기획서 6.1대로 페널티는 없다.
    /// </summary>
    public void ShowOrderInfo()
    {
        if (orderNoteUI == null || !EnsureOrderManager()) return;
        orderNoteUI.Toggle(orderManager.CurrentDialogue);
    }

    /// <summary>손님을 맞는 화면을 연다. 조리 화면은 그 아래에서 계속 살아 있다.</summary>
    private void OpenOrderScreen(int day)
    {
        if (orderScreenUI == null || !EnsureOrderManager()) return;

        string dialogue = orderManager.CurrentDialogue;
        if (string.IsNullOrEmpty(dialogue)) return;

        orderScreenUI.Open(day, currentHour, dialogue, totalRevenue);

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
            orderScreenUI.Open(1, currentHour, orderManager.CurrentDialogue, totalRevenue);
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

        // 검은 화면에서 도입부를 읽힌다. 여기서는 아직 가게가 차려지기 전이다.
        if (narration != null) yield return narration.Play();

        // 덮여 있는 동안 주문 화면을 차려 둔다. 손님은 아직 안 보이게 지워 놓는다.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsRunning) OpenTutorialOrder();
        else if (EnsureDayManager()) dayManager.StartDay();

        // 미끄러지며 들어오는 연출은 끊는다. 걷히자마자 이미 가게에 와 있어야 한다.
        if (orderScreenUI != null) orderScreenUI.SnapOpen();

        HideCustomerForEntrance();

        if (orderScreenUI != null) orderScreenUI.ShowBubble(false);

        // 검은 화면을 한 박자 둔다. 곧바로 걷으면 시작 화면과 가게가 이어 붙은 것처럼 보인다.
        // 내레이션을 읽은 뒤라면 이미 충분히 머물렀으므로 마지막 줄을 넘긴 여운만 준다.
        yield return new WaitForSecondsRealtime(narration != null ? openingTailSeconds : blackHoldSeconds);

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
            orderScreenUI.OpenEating(1, currentHour, totalRevenue);
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
            orderResultUI.OpenTutorial(totalRevenue, TutorialEndLine, () => closed = true);
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

    /// <summary>5일차까지 다 팔면 온다. 하루 정산과 달리 전체 누계를 보여 준다.</summary>
    private void HandleGameCompleted()
    {
        float average = servedCount > 0 ? accuracySum / servedCount : 0f;

        Debug.Log("[영업 종료] 누적 매출 " + totalRevenue.ToString("N0") + "원 / 평균 정확도 "
                  + average.ToString("F1") + "% / 완벽 " + perfectCount + "건 / 총 " + servedCount + "건");

        if (finalResultUI != null) finalResultUI.Open(totalRevenue, average, perfectCount, servedCount);

        // 5일을 다 팔았으면 이어할 것이 없다. 남겨 두면 다음에 켰을 때 끝난 판이 되살아난다.
        SaveSystem.Delete();
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
            orderScreenUI.OpenEating(day, currentHour, totalRevenue);

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
            if (perfectSign != null) yield return perfectSign.Play();
        }

        // 결과창을 올린다. 손님은 그대로 세워 둔다.
        // 결과창이 손님을 가리므로 여기서 스러뜨리면 나가는 모습을 아무도 못 보고,
        // [확인]을 눌렀을 때는 이미 사라진 뒤라 손님이 순간이동한 것처럼 보인다.
        // 나가는 모습은 결과창이 걷힌 다음에 보여 준다(SwapCustomer).
        orderResultUI.Open(accuracy, price, totalRevenue);

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
        // 마지막 손님이 나간다. 어두워지다가 지워진다(SwapCustomer 의 첫 박자와 같다).
        yield return FadeCustomer(0f, 1f, exitSeconds);

        if (closedSign != null)
        {
            yield return new WaitForSecondsRealtime(closedSignDelay);   // 빈 카운터
            yield return closedSign.Play();               // 주 · 문 · 마 · 감

            // 화면이 검게 물드는 동안 글자는 그 위에 남아 있다가 뒤늦게 스러진다.
            // 둘을 나란히 돌리고 늦게 끝나는 쪽까지 기다린다.
            Coroutine black = ScreenFade.Instance != null
                ? StartCoroutine(ScreenFade.Instance.FadeOut())
                : null;

            yield return closedSign.FadeAway();
            if (black != null) yield return black;
        }
        else if (ScreenFade.Instance != null)
        {
            yield return ScreenFade.Instance.FadeOut();
        }

        // 먹는 화면을 띄운 채로 여기까지 왔으므로 닫아야 정산 팝업만 남는다.
        if (orderScreenUI != null) orderScreenUI.Close();

        dayManager.OnCustomerServed();                            // 그 안에서 하루가 마감된다
        SaveNow();                                                // 하루가 끝난 자리도 남긴다

        if (ScreenFade.Instance != null) yield return ScreenFade.Instance.FadeIn();
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

    private void RefreshRevenue()
    {
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";
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
