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

    [Tooltip("당일 총 수익 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI profitText;

    [Tooltip("당일 평균 정확도 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI averageAccuracyText;

    [Tooltip("당일 100% 그릇 수 표시 텍스트 (기획서 v1.2 10장 '일일 결과 - 완벽 주문 기록')")]
    [SerializeField]
    private TextMeshProUGUI perfectCountText;

    [Tooltip("다음 날로 넘어가기 위한 [확인] 버튼")]
    [SerializeField]
    private Button confirmButton;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
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
    }

    /// <summary>
    /// 정산 팝업을 열고 일차, 당일 총 수익, 평균 정확도를 표시합니다.
    /// </summary>
    /// <param name="day">현재 일차</param>
    /// <param name="todayProfit">당일 총 누적 수익</param>
    /// <param name="averageAccuracy">당일 평균 정확도 (%)</param>
    /// <param name="perfectCount">당일 100% 그릇 수. 누계가 아니라 오늘치다</param>
    public void OpenPopup(int day, int todayProfit, float averageAccuracy, int perfectCount)
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = $"Day {day} 정산";
        }

        if (profitText != null)
        {
            profitText.text = $"당일 총 수익 : {todayProfit:N0}원";
        }

        if (averageAccuracyText != null)
        {
            averageAccuracyText.text = $"평균 정확도 : {averageAccuracy:F1}%";
        }

        if (perfectCountText != null)
        {
            perfectCountText.text = $"완벽한 한 그릇 : {perfectCount}건";
        }
    }

    /// <summary>
    /// 기본 호출 오버로드 (완벽 그릇 수 생략 시)
    /// </summary>
    public void OpenPopup(int day, int todayProfit, float averageAccuracy)
    {
        OpenPopup(day, todayProfit, averageAccuracy, 0);
    }

    /// <summary>
    /// 기본 호출 오버로드 (평균 정확도 생략 시)
    /// </summary>
    public void OpenPopup(int day, int todayProfit)
    {
        OpenPopup(day, todayProfit, 0f, 0);
    }

    /// <summary>
    /// 정산 팝업을 닫습니다.
    /// </summary>
    public void ClosePopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 확인 버튼 클릭 시 DayManager에 다음 날 진행을 알립니다.
    /// </summary>
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
}
