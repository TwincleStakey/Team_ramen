using System;
using UnityEngine;

public class DayManager : MonoBehaviour
{
    // 1~2일차 목표 손님 수
    private const int CUSTOMER_COUNT_EARLY = 5;
    // 3~4일차 목표 손님 수
    private const int CUSTOMER_COUNT_MIDDLE = 6;
    // 5일차 목표 손님 수
    private const int CUSTOMER_COUNT_LATE = 8;
    // 최대 진행 일수
    public const int MAX_DAYS = 5;

    [Header("참조 연결")]
    [SerializeField]
    private OrderManager orderManager;

    [SerializeField]
    private RamenCalculator ramenCalculator;

    [SerializeField]
    private DailyResultUI dailyResultUI;

    [Header("진행 상태")]
    [SerializeField]
    private int currentDay = 1;

    [SerializeField]
    private int currentCustomerCount = 0;

    [Header("시작 설정")]
    [Tooltip("게임 시작 시 자동으로 1일차 첫 주문을 생성할지 여부")]
    [SerializeField]
    private bool autoStartFirstDay = true;

    // 이벤트 (외부 UI나 시스템 연동용)
    public event Action<int> OnDayStarted;
    public event Action<int, int> OnDayEnded;
    public event Action OnGameCompleted;

    public int CurrentDay => currentDay;
    public int CurrentCustomerCount => currentCustomerCount;
    public int TargetCustomerCount => GetTargetCustomerCount(currentDay);
    public bool IsGameCompleted => currentDay > MAX_DAYS;

    private void Start()
    {
        if (autoStartFirstDay)
        {
            StartDay();
        }
    }

    // 해당 일차의 목표 손님 수를 반환합니다. (1~2일차: 5명, 3~4일차: 6명, 5일차: 8명)
    public int GetTargetCustomerCount(int day)
    {
        if (day <= 2)
        {
            return CUSTOMER_COUNT_EARLY;
        }
        else if (day <= 4)
        {
            return CUSTOMER_COUNT_MIDDLE;
        }

        return CUSTOMER_COUNT_LATE;
    }

    // 현재 일차의 영업을 시작하고 첫 손님 주문을 받습니다.
    public void StartDay()
    {
        if (currentDay > MAX_DAYS)
        {
            Debug.Log("[DayManager] 5일차 영업이 이미 완료되었습니다.");
            return;
        }

        currentCustomerCount = 0;

        if (orderManager != null)
        {
            orderManager.CreateOrder();
        }
        else
        {
            Debug.LogWarning("[DayManager] OrderManager가 연결되지 않았습니다.");
        }

        OnDayStarted?.Invoke(currentDay);
        Debug.Log($"[DayManager] Day {currentDay} 영업 시작! (오늘 목표 손님 수: {TargetCustomerCount}명)");
    }

    /// <summary>
    /// 손님에게 라멘을 제출(서빙)하고 채점이 끝났을 때 호출합니다.
    /// 조리 완료 시점(GameManager 등)에서 호출해 주면 됩니다.
    /// </summary>
    public void OnCustomerServed()
    {
        currentCustomerCount++;
        Debug.Log($"[DayManager] 손님 서빙 완료 ({currentCustomerCount}/{TargetCustomerCount}명)");

        // 오늘 목표 손님 수를 모두 채운 경우 -> 하루 마감
        if (currentCustomerCount >= TargetCustomerCount)
        {
            EndDay();
        }
        else
        {
            // 다음 손님 주문 생성
            if (orderManager != null)
            {
                orderManager.CreateOrder();
            }
        }
    }

    // 하루 영업을 마감하고 당일 정산 UI 팝업을 띄웁니다.
    private void EndDay()
    {
        int todayProfit = (ramenCalculator != null) ? ramenCalculator.TodayTotalProfit : 0;
        float todayAvgAccuracy = (ramenCalculator != null) ? ramenCalculator.TodayAverageAccuracy : 0f;
        int todayPerfect = (ramenCalculator != null) ? ramenCalculator.TodayPerfectCount : 0;

        Debug.Log($"[DayManager] Day {currentDay} 영업 마감! (당일 총 수익: {todayProfit:N0}원, 평균 정확도: {todayAvgAccuracy:F1}%, 완벽한 한 그릇: {todayPerfect}건)");

        if (dailyResultUI != null)
        {
            dailyResultUI.OpenPopup(currentDay, todayProfit, todayAvgAccuracy, todayPerfect);
        }
        else
        {
            Debug.LogWarning("[DayManager] DailyResultUI가 연결되지 않았습니다.");
        }

        OnDayEnded?.Invoke(currentDay, todayProfit);
    }

    // 정산 팝업의 [확인] 버튼을 눌렀을 때 호출되어 다음 날짜로 넘어갑니다.
    public void NextDay()
    {
        if (dailyResultUI != null)
        {
            dailyResultUI.ClosePopup();
        }

        currentDay++;

        // 5일차를 넘어서면 게임 전체 완료
        if (currentDay > MAX_DAYS)
        {
            Debug.Log("[DayManager] ★ 축하합니다! 5일차까지 모든 일정을 완료했습니다! ★");
            OnGameCompleted?.Invoke();
            return;
        }

        // 당일 수익 리셋
        if (ramenCalculator != null)
        {
            ramenCalculator.ResetDailyProfit();
        }

        // 다음 날 시작
        StartDay();
    }
}
