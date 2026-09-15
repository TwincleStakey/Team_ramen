using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyResultUI : MonoBehaviour
{
    [Header("매니저 참조")]
    [SerializeField]
    private DayManager dayManager;

    [Header("UI 요소 연결")]
    [Tooltip("정산 팝업 패널 최상위 오브젝트 (열고 닫을 때 활성화/비활성화)")]
    [SerializeField]
    private GameObject popupRoot;

    [Tooltip("몇 일차인지 표시하는 텍스트 (예: Day 1 정산)")]
    [SerializeField]
    private TextMeshProUGUI titleText;

    [Tooltip("당일 목표 금액 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI targetProfitText;

    [Tooltip("당일 총 수익 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI profitText;

    [Tooltip("누적 총 매출 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI totalProfitText;

    [Tooltip("당일 평균 정확도 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI averageAccuracyText;

    [Tooltip("당일 100% 그릇 수 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI perfectCountText;

    [Tooltip("다음 날로 넘어가기 위한 [확인] 버튼 (목표 달성 시 활성화)")]
    [SerializeField]
    private Button confirmButton;

    [Tooltip("목표 미달 시 1일차부터 다시 시작하기 위한 [다시하기] 버튼 (목표 미달 시 활성화)")]
    [SerializeField]
    private Button retryButton;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }

        // 시작 시에는 팝업을 숨겨둠
        ClosePopup();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryButtonClicked);
        }
    }

    /// <summary>
    /// 정산 팝업을 열고 일차, 당일 총 수익, 당일 목표 금액, 누적 총 매출, 평균 정확도, 완벽한 한 그릇 수 및 목표 달성 여부를 표시합니다.
    /// </summary>
    /// <param name="day">현재 일차</param>
    /// <param name="todayProfit">당일 총 누적 수익</param>
    /// <param name="totalProfit">게임 전체 누적 총 매출</param>
    /// <param name="averageAccuracy">당일 평균 정확도 (%)</param>
    /// <param name="perfectCount">당일 100% 그릇 수</param>
    /// <param name="isSuccess">목표 금액 달성 여부 (true: [확인] 버튼 활성화, false: [다시하기] 버튼 활성화)</param>
    /// <param name="targetProfit">당일 목표 금액</param>
    public void OpenPopup(int day, int todayProfit, int totalProfit, float averageAccuracy, int perfectCount, bool isSuccess, int targetProfit = 0)
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        Sfx.Play("sfx_ui_dayend", 0.7f);

        if (titleText != null)
        {
            titleText.text = $"Day {day} 정산";
        }

        if (targetProfitText != null)
        {
            targetProfitText.text = $"목표 금액 : {targetProfit:N0}원";
        }

        if (profitText != null)
        {
            profitText.text = $"당일 총 수익 : {todayProfit:N0}원";
        }

        if (totalProfitText != null)
        {
            totalProfitText.text = $"누적 총 매출 : {totalProfit:N0}원";
        }

        if (averageAccuracyText != null)
        {
            averageAccuracyText.text = $"평균 정확도 : {averageAccuracy:F1}%";
        }

        if (perfectCountText != null)
        {
            perfectCountText.text = $"완벽한 한 그릇 : {perfectCount}건";
        }

        // 목표 달성 여부에 따라 버튼 표시 분기
        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(isSuccess);
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(!isSuccess);
        }
    }

    // 기본 호출 오버로드 (isSuccess 및 totalProfit 생략 시 기본 성공 처리)
    public void OpenPopup(int day, int todayProfit, float averageAccuracy, int perfectCount)
    {
        OpenPopup(day, todayProfit, todayProfit, averageAccuracy, perfectCount, true);
    }

    // 기본 호출 오버로드 (완벽 그릇 수 생략 시)
    public void OpenPopup(int day, int todayProfit, float averageAccuracy)
    {
        OpenPopup(day, todayProfit, todayProfit, averageAccuracy, 0, true);
    }

    // 기본 호출 오버로드 (평균 정확도 생략 시)
    public void OpenPopup(int day, int todayProfit)
    {
        OpenPopup(day, todayProfit, todayProfit, 0f, 0, true);
    }

    // 정산 팝업을 닫습니다.
    public void ClosePopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 확인 버튼 클릭 시 DayManager에 다음 날 진행을 알립니다.
    private void OnConfirmButtonClicked()
    {
        if (dayManager != null)
        {
            dayManager.NextDay();
        }
        else
        {
            ClosePopup();
        }
    }

    // 다시하기 버튼 클릭 시 DayManager에 1일차 재시작을 알립니다.
    private void OnRetryButtonClicked()
    {
        if (dayManager != null)
        {
            dayManager.RestartGame();
        }
        else
        {
            ClosePopup();
        }
    }
}