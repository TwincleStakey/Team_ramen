using System;
using System.Collections.Generic;
using UnityEngine;

public class RamenCalculator : MonoBehaviour
{
    // 1. 라멘 기본 정가 설정 (모든 라멘 종류 통일: 1,000원)
    public const int BASE_PRICE = 1000;

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

    /// <summary>
    /// 라멘의 3대 핵심 필수 요소(베이스, 육수, 면)를 검증합니다.
    /// 베이스 누락/오답, 육수 누락, 면 누락/오답이거나 3요소 중 하나라도 2개 이상 투입된 경우 false를 반환합니다.
    /// </summary>
    public bool ValidateCoreIngredients(RamenType ramenType, Dictionary<IngredientType, int> targetRecipe, Dictionary<IngredientType, int> submittedRecipe, out string failReason)
    {
        failReason = string.Empty;
        if (submittedRecipe == null)
        {
            failReason = "제출된 라멘 데이터가 없습니다.";
            return false;
        }

        // 1. 베이스/타래 검증 (ShioTare, ShoyuTare, TonkotsuBase)
        IngredientType expectedBase;
        switch (ramenType)
        {
            case RamenType.Shio:
                expectedBase = IngredientType.ShioTare;
                break;
            case RamenType.Shoyu:
                expectedBase = IngredientType.ShoyuTare;
                break;
            case RamenType.Tonkotsu:
                expectedBase = IngredientType.TonkotsuBase;
                break;
            default:
                expectedBase = IngredientType.ShioTare;
                break;
        }

        int shioTare = submittedRecipe.TryGetValue(IngredientType.ShioTare, out int st) ? st : 0;
        int shoyuTare = submittedRecipe.TryGetValue(IngredientType.ShoyuTare, out int syt) ? syt : 0;
        int tonkotsuBase = submittedRecipe.TryGetValue(IngredientType.TonkotsuBase, out int tb) ? tb : 0;
        int totalBaseCount = shioTare + shoyuTare + tonkotsuBase;

        if (totalBaseCount == 0)
        {
            failReason = $"베이스(타래) 누락 (필요: {expectedBase})";
            return false;
        }
        if (totalBaseCount >= 2)
        {
            failReason = $"베이스(타래) 2개 이상 투입 (총 {totalBaseCount}개)";
            return false;
        }
        int expectedBaseAmount = submittedRecipe.TryGetValue(expectedBase, out int eb) ? eb : 0;
        if (expectedBaseAmount != 1)
        {
            failReason = $"잘못된 베이스(타래) 투입 (요구: {expectedBase})";
            return false;
        }

        // 2. 육수 검증 (Broth)
        int brothCount = submittedRecipe.TryGetValue(IngredientType.Broth, out int br) ? br : 0;
        if (brothCount == 0)
        {
            failReason = "육수 누락";
            return false;
        }
        if (brothCount >= 2)
        {
            failReason = $"육수 2개 이상 투입 (총 {brothCount}개)";
            return false;
        }

        // 3. 면 검증 (ThinNoodles, ThickNoodles)
        int targetThin = (targetRecipe != null && targetRecipe.TryGetValue(IngredientType.ThinNoodles, out int tt)) ? tt : 0;
        int targetThick = (targetRecipe != null && targetRecipe.TryGetValue(IngredientType.ThickNoodles, out int tk)) ? tk : 0;
        IngredientType expectedNoodle = (targetThin > 0) ? IngredientType.ThinNoodles : IngredientType.ThickNoodles;

        int submittedThin = submittedRecipe.TryGetValue(IngredientType.ThinNoodles, out int sth) ? sth : 0;
        int submittedThick = submittedRecipe.TryGetValue(IngredientType.ThickNoodles, out int stk) ? stk : 0;
        int totalNoodleCount = submittedThin + submittedThick;

        if (totalNoodleCount == 0)
        {
            failReason = $"면 누락 (필요: {expectedNoodle})";
            return false;
        }
        if (totalNoodleCount >= 2)
        {
            failReason = $"면 2개 이상 투입 (총 {totalNoodleCount}개)";
            return false;
        }
        int expectedNoodleAmount = submittedRecipe.TryGetValue(expectedNoodle, out int en) ? en : 0;
        if (expectedNoodleAmount != 1)
        {
            failReason = $"잘못된 면 종류 투입 (요구: {expectedNoodle})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 정답 레시피와 손님에게 제공한 레시피를 비교하여 정확도 및 판매 금액을 계산하고 당일 총 이익에 누적합니다.
    /// 필수 3대 요소(베이스, 육수, 면) 미달 시 즉시 정확도 0%, 판매금 0원으로 처리되며,
    /// 통과 시 나머지 토핑/조미료 합을 분모로 하여 오차율에 따른 정확도 및 판매금을 산출합니다.
    /// </summary>
    /// <returns>최종 판매 금액 (int)</returns>
    public int Calculate(RamenType ramenType, Dictionary<IngredientType, int> targetRecipe, Dictionary<IngredientType, int> submittedRecipe)
    {
        if (targetRecipe == null) targetRecipe = new Dictionary<IngredientType, int>();
        if (submittedRecipe == null) submittedRecipe = new Dictionary<IngredientType, int>();

        int basePrice = BASE_PRICE;

        // 1. 핵심 3요소(베이스, 육수, 면) 필수 조건 검증 (Fail-Fast: 탈락 시 0% 및 0원)
        if (!ValidateCoreIngredients(ramenType, targetRecipe, submittedRecipe, out string failReason))
        {
            lastAccuracy = 0f;
            lastSellingPrice = 0;
            lastTotalErrorCount = 0;
            lastTotalTargetCount = 0;

            todayTotalProfit += 0;
            todayTotalAccuracy += 0f;
            todayServedCount++;

            Debug.Log($"<color=#FF3333><b>[라멘 평가: 탈락]</b> 종류: {ramenType} | 사유: {failReason} ➔ 정답률: 0.0% | 판매 금액: 0원 (정가: {basePrice:N0}원)</color>");
            return 0;
        }

        // 2. 핵심 3요소를 제외한 나머지 재료(토핑/조미료)에 대한 정밀 채점
        int toppingTargetCount = 0;
        int toppingErrorCount = 0;

        IngredientType[] toppingTypes = new IngredientType[]
        {
            IngredientType.Chashu,
            IngredientType.Menma,
            IngredientType.GreenOnion,
            IngredientType.Egg,
            IngredientType.Nori,
            IngredientType.BeanSprout,
            IngredientType.WoodEar,
            IngredientType.FlavorOil,
            IngredientType.ChiliPowder
        };

        foreach (IngredientType topping in toppingTypes)
        {
            int targetAmount = targetRecipe.TryGetValue(topping, out int target) ? target : 0;
            int submittedAmount = submittedRecipe.TryGetValue(topping, out int submitted) ? submitted : 0;

            toppingTargetCount += targetAmount;
            toppingErrorCount += Mathf.Abs(targetAmount - submittedAmount);
        }

        // 3. 토핑 기준 정확도(%) 계산 (분모: 토핑 정답 총합, 분자: 토핑 오차 총합)
        float accuracy = 0f;
        if (toppingTargetCount > 0)
        {
            accuracy = Mathf.Max(0f, 100f - ((float)toppingErrorCount / toppingTargetCount) * 100f);
        }
        else
        {
            // 토핑이 전혀 없는 특수 주문인 경우: 오차가 없으면 100%, 있으면 0%
            accuracy = (toppingErrorCount == 0) ? 100f : 0f;
        }

        // 4. 판매 금액 책정 (정가 * 정확도%)
        int sellingPrice = Mathf.RoundToInt(basePrice * (accuracy / 100f));

        // 5. 최근 결과 저장 및 당일 통계에 누적
        lastAccuracy = accuracy;
        lastSellingPrice = sellingPrice;
        lastTotalErrorCount = toppingErrorCount;
        lastTotalTargetCount = toppingTargetCount;

        todayTotalProfit += sellingPrice;
        todayTotalAccuracy += accuracy;
        todayServedCount++;

        Debug.Log($"[라멘 평가: 정상 통과] 종류: {ramenType} | 정가: {basePrice:N0}원 | " +
                  $"토핑 오차: {toppingErrorCount}/{toppingTargetCount}개 | " +
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
