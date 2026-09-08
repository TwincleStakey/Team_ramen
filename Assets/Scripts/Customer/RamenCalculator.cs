using System;
using System.Collections.Generic;
using UnityEngine;

public class RamenCalculator : MonoBehaviour
{
    // 1. 라멘 기본 정가 설정
    private const int SHIO_PRICE = 8000;       // 시오라멘: 8,000원
    private const int SHOYU_PRICE = 10000;     // 소유라멘: 10,000원
    private const int TONKOTSU_PRICE = 13000;  // 돈꼬츠라멘: 13,000원

    // 2. 당일 총 이익 및 정확도 변수
    [Header("당일 통계")]
    [SerializeField] private int todayTotalProfit = 0;
    [SerializeField] private float todayTotalAccuracy = 0f;
    [SerializeField] private int todayServedCount = 0;

    // 최근 서빙한 라멘의 정확도 및 판매가, 오차 개수
    private float lastAccuracy = 0f;
    private int lastSellingPrice = 0;
    private int lastTotalErrorCount = 0;
    private int lastTotalTargetCount = 0;

    public int TodayTotalProfit => todayTotalProfit;
    public int TodayServedCount => todayServedCount;
    public float TodayAverageAccuracy => todayServedCount > 0 ? (todayTotalAccuracy / todayServedCount) : 0f;
    public float LastAccuracy => lastAccuracy;
    public int LastSellingPrice => lastSellingPrice;
    public int LastTotalErrorCount => lastTotalErrorCount;
    public int LastTotalTargetCount => lastTotalTargetCount;

    // 라멘 종류별 정가 반환
    public int GetRamenBasePrice(RamenType ramenType)
    {
        switch (ramenType)
        {
            case RamenType.Shio:
                return SHIO_PRICE;
            case RamenType.Shoyu:
                return SHOYU_PRICE;
            case RamenType.Tonkotsu:
                return TONKOTSU_PRICE;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 정답 레시피와 손님에게 제공한 레시피를 비교하여 정확도 및 판매 금액을 계산하고 당일 총 이익에 누적합니다.
    /// </summary>
    /// <returns>최종 판매 금액 (int)</returns>
    public int Calculate(RamenType ramenType, Dictionary<IngredientType, int> targetRecipe, Dictionary<IngredientType, int> submittedRecipe)
    {
        if (targetRecipe == null) targetRecipe = new Dictionary<IngredientType, int>();
        if (submittedRecipe == null) submittedRecipe = new Dictionary<IngredientType, int>();

        int totalTargetCount = 0;
        int totalErrorCount = 0;

        // 1. 모든 재료에 대해 정답 수량과 제공 수량 비교 (많아도 깎이고 적어도 깎임)
        foreach (IngredientType ingredient in Enum.GetValues(typeof(IngredientType)))
        {
            int targetAmount = targetRecipe.TryGetValue(ingredient, out int target) ? target : 0;
            int submittedAmount = submittedRecipe.TryGetValue(ingredient, out int submitted) ? submitted : 0;

            totalTargetCount += targetAmount;
            totalErrorCount += Mathf.Abs(targetAmount - submittedAmount);
        }

        // 2. 정확도(%) 계산
        float accuracy = 0f;
        if (totalTargetCount > 0)
        {
            accuracy = Mathf.Max(0f, 100f - ((float)totalErrorCount / totalTargetCount) * 100f);
        }

        // 3. 라멘 정가 확인
        int basePrice = GetRamenBasePrice(ramenType);

        // 4. 정가에 정확도를 곱한 값을 판매 금액으로 책정
        int sellingPrice = Mathf.RoundToInt(basePrice * (accuracy / 100f));

        // 5. 최근 결과 저장 및 당일 통계에 누적
        lastAccuracy = accuracy;
        lastSellingPrice = sellingPrice;
        lastTotalErrorCount = totalErrorCount;
        lastTotalTargetCount = totalTargetCount;

        todayTotalProfit += sellingPrice;
        todayTotalAccuracy += accuracy;
        todayServedCount++;

        Debug.Log($"[라멘 평가] 종류: {ramenType} | 정가: {basePrice:N0}원 | " +
                  $"오차: {totalErrorCount}/{totalTargetCount}개 | " +
                  $"정답률: {accuracy:F1}% | " +
                  $"판매 금액: {sellingPrice:N0}원 | 당일 누적 총 이익: {todayTotalProfit:N0}원 (당일 평균 정답률: {TodayAverageAccuracy:F1}%)");

        return sellingPrice;
    }

    // RamenState를 직접 전달받아 계산하는 편의 함수
    public int Calculate(RamenType ramenType, Dictionary<IngredientType, int> targetRecipe, RamenState submittedRamen)
    {
        Dictionary<IngredientType, int> submittedRecipe = submittedRamen != null ? submittedRamen.selectedIngredients : null;
        return Calculate(ramenType, targetRecipe, submittedRecipe);
    }

    // 새로운 날이 시작될 때 당일 통계(총 이익, 정확도, 서빙 수)를 초기화합니다.
    public void ResetDailyProfit()
    {
        todayTotalProfit = 0;
        todayTotalAccuracy = 0f;
        todayServedCount = 0;
        lastAccuracy = 0f;
        lastSellingPrice = 0;
        lastTotalErrorCount = 0;
        lastTotalTargetCount = 0;
        Debug.Log("[당일 총 이익 및 정확도 통계 초기화 완료]");
    }
}
