using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
    [SerializeField] private Text revenueText;
    [SerializeField] private Text dayText;
    [SerializeField] private RamenCalculator ramenCalculator;
    [SerializeField] private FinalResultUI finalResultUI;
    [SerializeField] private OrderScreenUI orderScreenUI;
    [SerializeField] private RecipeBookUI recipeBookUI;
    [SerializeField] private OrderResultUI orderResultUI;

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
    }

    private void Start()
    {
        RefreshRevenue();

        // 첫 주문은 DayManager가 StartDay()에서 만든다.
        // 여기서 또 만들면 손님 한 명에 주문이 두 개 생기고, 화면에 뜬 주문과 채점되는 주문이 어긋난다.
        if (dayManager == null)
        {
            Debug.LogWarning("[GameManager] 씬에 DayManager가 없어 하루 진행이 시작되지 않습니다. " +
                             "Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
        }
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
    /// 조리 화면의 ? 버튼이 부른다. 주문 원문·기본 레시피·재료 속성표를 함께 띄운다.
    /// 기획서 6.1대로 페널티는 없고 최종 정답은 보여 주지 않는다.
    /// </summary>
    public void ShowOrderInfo()
    {
        if (recipeBookUI == null || !EnsureOrderManager()) return;
        recipeBookUI.Open(orderManager.CurrentDialogue);
    }

    /// <summary>손님을 맞는 화면을 연다. 조리 화면은 그 아래에서 계속 살아 있다.</summary>
    private void OpenOrderScreen(int day)
    {
        if (orderScreenUI == null || !EnsureOrderManager()) return;

        string dialogue = orderManager.CurrentDialogue;
        if (string.IsNullOrEmpty(dialogue)) return;

        orderScreenUI.Open(day, currentHour, dialogue, totalRevenue);
    }

    /// <summary>5일차까지 다 팔면 온다. 하루 정산과 달리 전체 누계를 보여 준다.</summary>
    private void HandleGameCompleted()
    {
        float average = servedCount > 0 ? accuracySum / servedCount : 0f;

        Debug.Log("[영업 종료] 누적 매출 " + totalRevenue.ToString("N0") + "원 / 평균 정확도 "
                  + average.ToString("F1") + "% / 완벽 " + perfectCount + "건 / 총 " + servedCount + "건");

        if (finalResultUI != null) finalResultUI.Open(totalRevenue, average, perfectCount, servedCount);
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

        int price = orderManager.EvaluateRamen(ramenState);
        totalRevenue += price;

        // 채점 직후에만 읽을 수 있다. NextDay가 당일 집계를 지우기 전에 여기서 쌓아 둔다.
        if (EnsureRamenCalculator())
        {
            float accuracy = ramenCalculator.LastAccuracy;
            accuracySum += accuracy;
            servedCount++;
            if (accuracy >= 99.95f) perfectCount++;
        }

        RefreshRevenue();

        Debug.Log("[정산] 판매 금액 " + price.ToString("N0") + "원 / 누적 매출 " + totalRevenue.ToString("N0") + "원");

        // 결과창을 먼저 보여 주고, [확인]을 누르면 AdvanceCustomer가 진행을 이어받는다.
        // 결과창이 없으면(빌더를 안 돌린 경우) 예전처럼 바로 넘어간다.
        if (orderResultUI != null)
        {
            float shown = EnsureRamenCalculator() ? ramenCalculator.LastAccuracy : 0f;
            orderResultUI.Open(shown, price, totalRevenue);
        }
        else
        {
            AdvanceCustomer();
        }
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

        dayManager.OnCustomerServed();

        // 손님을 다 받았으면 다음 주문이 없다. 그때는 하루 마감 정산 팝업이 대신 뜬다.
        bool dayContinues = dayManager.CurrentCustomerCount < dayManager.TargetCustomerCount;
        if (dayContinues) OpenOrderScreen(dayManager.CurrentDay);
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
