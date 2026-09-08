using System.Collections.Generic;
using UnityEngine;

public class RecipeGenerator
{
    // 손님 주문을 기반으로 최종 정답(타깃) 레시피를 생성한다.
    public Dictionary<IngredientType, int> GenerateTargetRecipe(CustomerOrder order)
    {
        // 1. 기본 레시피 가져와서 타깃 주문 딕셔너리에 해당 재료 및 개수 넣기
        Dictionary<IngredientType, int> targetRecipe =  GetBaseRecipe(order.ramenType);

        // 2. 추가 주문 적용
        if (order.requests != null)
        {
            foreach (IngredientRequest request in order.requests)
            {
                if (targetRecipe.ContainsKey(request.ingredient))
                {
                    targetRecipe[request.ingredient] += request.amount;
                }
                else
                {
                    targetRecipe.Add(request.ingredient, request.amount);
                }

                if (targetRecipe[request.ingredient] < 0)
                {
                    targetRecipe[request.ingredient] = 0;
                }
            }
        }

        return targetRecipe;
    }

    /// <summary>
    /// 라멘 종류별 기본 레시피 딕셔너리를 반환합니다. (어디서나 RecipeGenerator.GetBaseRecipe로 호출 가능)
    /// </summary>
    public static Dictionary<IngredientType, int> GetBaseRecipe(RamenType ramenType)
    {
        Dictionary<IngredientType, int> recipe = new Dictionary<IngredientType, int>();

        switch (ramenType)
        {
            case RamenType.Shio:
                recipe[IngredientType.ShioTare] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.ThinNoodles] = 1;
                recipe[IngredientType.Chashu] = 1;
                recipe[IngredientType.Menma] = 2;
                recipe[IngredientType.GreenOnion] = 1;
                recipe[IngredientType.FlavorOil] = 1;
                break;

            case RamenType.Shoyu:
                recipe[IngredientType.ShoyuTare] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.ThinNoodles] = 1;
                recipe[IngredientType.Chashu] = 2;
                recipe[IngredientType.Menma] = 1;
                recipe[IngredientType.GreenOnion] = 1;
                recipe[IngredientType.FlavorOil] = 1;
                break;

            case RamenType.Tonkotsu:
                recipe[IngredientType.TonkotsuBase] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.ThickNoodles] = 1;
                recipe[IngredientType.Chashu] = 1;
                recipe[IngredientType.Egg] = 1;
                recipe[IngredientType.BeanSprout] = 1;
                recipe[IngredientType.WoodEar] = 1;
                recipe[IngredientType.GreenOnion] = 1;
                recipe[IngredientType.FlavorOil] = 1;
                break;
        }

        return recipe;
    }

    /// <summary>
    /// UI 텍스트(TextMeshPro 등)에 여러 줄로 출력하기 좋은 한글 기본 레시피 문자열을 반환합니다.
    /// 예: "소금타래: 1개\n육수: 1개\n얇은면: 1개\n차슈: 1개..."
    /// </summary>
    public static string GetBaseRecipeDescription(RamenType ramenType)
    {
        Dictionary<IngredientType, int> recipe = GetBaseRecipe(ramenType);
        List<string> lines = new List<string>();

        foreach (KeyValuePair<IngredientType, int> kvp in recipe)
        {
            if (kvp.Value > 0)
            {
                lines.Add($"{OrderManager.GetKoreanIngredientName(kvp.Key)}: {kvp.Value}개");
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// UI 툴팁이나 한 줄 텍스트에 출력하기 좋은 콤마 구분 한글 기본 레시피 문자열을 반환합니다.
    /// 예: "소금타래 1개, 육수 1개, 얇은면 1개, 차슈 1개, 멘마 2개, 파 1개, 향미유 1개"
    /// </summary>
    public static string GetBaseRecipeSummary(RamenType ramenType)
    {
        Dictionary<IngredientType, int> recipe = GetBaseRecipe(ramenType);
        List<string> items = new List<string>();

        foreach (KeyValuePair<IngredientType, int> kvp in recipe)
        {
            if (kvp.Value > 0)
            {
                items.Add($"{OrderManager.GetKoreanIngredientName(kvp.Key)} {kvp.Value}개");
            }
        }

        return string.Join(", ", items);
    }
}