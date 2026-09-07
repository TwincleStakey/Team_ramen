using System.Collections.Generic;
using UnityEngine;

public class CustomerOrderGenerator : MonoBehaviour
{
    // 한 주문에서 변경할 수 있는 최대 재료 종류
    private const int MAX_MODIFICATION_TYPES = 4;

    // 재료 추가 시 최대 추가 수량
    private const int MAX_ADD_AMOUNT = 3;

    private readonly RecipeGenerator recipeGenerator = new RecipeGenerator();


    // 주문 생성
    public CustomerOrder GenerateOrder(int currentDay)
    {
        CustomerOrder order = new CustomerOrder();

        // 1. 기본 라멘 종류 결정
        order.ramenType = GetRandomRamen();

        // 2. 변경할 재료와 변경 수량 결정
        GenerateModificationRequests(order);

        return order;
    }

    // 라멘 종류 무작위 선택
    private RamenType GetRandomRamen()
    {
        List<RamenType> availableRamen = new List<RamenType> {RamenType.Shio, RamenType.Shoyu, RamenType.Tonkotsu};

        int randomIndex = Random.Range(0, availableRamen.Count);

        return availableRamen[randomIndex];
    }

    // 변경할 재료 종류와 변경량 생성
    private void GenerateModificationRequests(CustomerOrder order)
    {
        // requests가 비어 있으므로 기본 레시피가 반환된다.
        Dictionary<IngredientType, int> baseRecipe = recipeGenerator.GenerateTargetRecipe(order);

        // 해당 라멘에서 변경할 수 있는 재료 목록
        List<IngredientType> availableIngredients = GetAvailableModificationIngredients(order.ramenType);

        if (availableIngredients == null || availableIngredients.Count == 0)
        {
            return;
        }

        // 중복 선택을 막기 위해 목록을 먼저 섞는다.
        ShuffleList(availableIngredients);

        // 변경할 재료 종류를 1~4개 중 무작위로 결정
        int modificationTypeCount = Random.Range(1, MAX_MODIFICATION_TYPES + 1);

        modificationTypeCount = Mathf.Min(modificationTypeCount, availableIngredients.Count);

        for (int i = 0; i < modificationTypeCount; i++)
        {
            IngredientType ingredient = availableIngredients[i];

            // 기본 레시피에 들어 있는 수량
            int baseAmount = GetRecipeAmount(baseRecipe, ingredient);

            // 양수면 추가, 음수면 완전 제거
            int modificationAmount = GetRandomModificationAmount(baseAmount);

            order.requests.Add(new IngredientRequest(ingredient, modificationAmount));
        }
    }

    // 재료 변경량 결정
    private int GetRandomModificationAmount(int baseAmount)
    {
        // 기본 레시피에 없는 재료는 제거할 수 없다.
        // 따라서 1~3개 추가만 가능하다.
        if (baseAmount <= 0)
        {
            return GetRandomAddAmount();
        }

        // 기본 레시피에 들어 있는 재료는
        // 50% 확률로 추가 또는 완전 제거
        bool removeIngredient = Random.value < 0.5f;

        if (removeIngredient)
        {
            // 기본 수량 전체를 음수로 반환한다.
            // 예: 계란 1개 → -1 → 최종 0개, 멘마 2개 → -2 → 최종 0개
            return -baseAmount;
        }

        // 제거하지 않으면 1~3개 추가
        return GetRandomAddAmount();
    }

    // 추가 수량 결정
    private int GetRandomAddAmount()
    {
        return Random.Range(1, MAX_ADD_AMOUNT + 1);
    }

    // 기본 레시피 수량 조회
    private int GetRecipeAmount(Dictionary<IngredientType, int> recipe, IngredientType ingredient)
    {
        int amount;

        if (recipe.TryGetValue(ingredient, out amount))
        {
            return amount;
        }

        // 딕셔너리에 없으면 기본 수량은 0개
        return 0;
    }

    // 라멘별 변경 가능 재료
    private List<IngredientType>
        GetAvailableModificationIngredients(RamenType ramenType)
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

    // 리스트 섞기
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