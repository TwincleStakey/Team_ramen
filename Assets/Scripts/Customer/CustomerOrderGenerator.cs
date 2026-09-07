using System.Collections.Generic;
using UnityEngine;

public class CustomerOrderGenerator : MonoBehaviour
{
    // 추가 주문 가능한 최대 재료 종류 개수
    private const int MAX_ADDITIONAL_TYPES = 4;

    // 현재 날짜를 기준으로 손님 주문을 생성한다.
    public CustomerOrder GenerateOrder(int currentDay)
    {
        CustomerOrder order = new CustomerOrder();

        // 1. 기본 라멘 결정
        order.ramenType = GetRandomRamen(currentDay);

        // 2. 추가 주문 생성 (최대 4종류 무작위 선택 & 대사 기준 최대 수량 적용)
        GenerateAdditionalRequests(order);

        return order;
    }

    // 라멘 종류 랜덤 선택
    private RamenType GetRandomRamen(int currentDay)
    {
        List<RamenType> availableRamen = new List<RamenType>();

        // Day 1
        // 시오
        availableRamen.Add(RamenType.Shio);

        // Day 2
        // 쇼유 추가
        if (currentDay >= 2)
        {
            availableRamen.Add(RamenType.Shoyu);
        }

        // Day 4
        // 돈코츠 추가
        if (currentDay >= 4)
        {
            availableRamen.Add(RamenType.Tonkotsu);
        }

        int randomIndex = Random.Range(0, availableRamen.Count);
        return availableRamen[randomIndex];
    }

    // 추가 주문 생성 (라멘별 추가 가능 목록에서 최대 4종류 무작위 선택)
    private void GenerateAdditionalRequests(CustomerOrder order)
    {
        // 1. 해당 라멘 타입에서 추가 가능한 전체 재료 목록 가져오기
        List<IngredientType> availableIngredients = GetAvailableAdditionalIngredients(order.ramenType);

        if (availableIngredients == null || availableIngredients.Count == 0)
        {
            return;
        }

        // 2. 재료 목록 셔플 (랜덤 섞기)
        ShuffleList(availableIngredients);

        // 3. 0 ~ 4종류 중 랜덤으로 추가할 종류 개수 결정 (최대 4종류)
        int requestTypeCount = Random.Range(1, MAX_ADDITIONAL_TYPES + 1);
        requestTypeCount = Mathf.Min(requestTypeCount, availableIngredients.Count);

        // 4. 뽑힌 재료 종류별로 DialogueGenerator 대사 기준에 맞춰 수량 결정
        for (int i = 0; i < requestTypeCount; i++)
        {
            IngredientType ingredient = availableIngredients[i];
            int maxAmount = GetMaxRequestAmount(ingredient);
            int amount = Random.Range(1, maxAmount + 1);

            order.requests.Add(new IngredientRequest(ingredient, amount));
        }
    }

    // DialogueGenerator.cs에 정의된 대사 케이스별 최대 추가 수량
    private int GetMaxRequestAmount(IngredientType ingredient)
    {
        switch (ingredient)
        {
            case IngredientType.GreenOnion:
                return 2; // 파: case 1, 2 (최대 2)

            default:
                return 3; // 차슈, 향미유, 고추가루, 멘마, 계란, 숙주, 목이버섯, 김: case 1, 2, 3 (최대 3)
        }
    }

    // 라멘 종류별 추가 가능 재료 목록
    private List<IngredientType> GetAvailableAdditionalIngredients(RamenType ramenType)
    {
        List<IngredientType> list = new List<IngredientType>();

        switch (ramenType)
        {
            case RamenType.Shio:
                list.Add(IngredientType.Chashu);
                list.Add(IngredientType.Menma);
                list.Add(IngredientType.GreenOnion);
                list.Add(IngredientType.FlavorOil);
                list.Add(IngredientType.Nori);
                list.Add(IngredientType.ChiliPowder);
                break;

            case RamenType.Shoyu:
                list.Add(IngredientType.Chashu);
                list.Add(IngredientType.Menma);
                list.Add(IngredientType.GreenOnion);
                list.Add(IngredientType.FlavorOil);
                list.Add(IngredientType.Nori);
                list.Add(IngredientType.ChiliPowder);
                break;

            case RamenType.Tonkotsu:
                list.Add(IngredientType.Chashu);
                list.Add(IngredientType.Egg);
                list.Add(IngredientType.BeanSprout);
                list.Add(IngredientType.WoodEar);
                list.Add(IngredientType.GreenOnion);
                list.Add(IngredientType.FlavorOil);
                list.Add(IngredientType.ChiliPowder);
                break;
        }

        return list;
    }

    // 리스트 무작위 셔플 헬퍼 함수
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}