using System.Collections.Generic;
using UnityEngine;

public class RecipeGenerator
{
    // 손님 주문을 기반으로 최종 정답(타깃) 레시피를 생성한다.
    public Dictionary<IngredientType, int> GenerateTargetRecipe(CustomerOrder order)
    {
        // 1. 기본 레시피 가져와서 타깃 주문 딕셔너리에 해당 재료 및 개수 넣기
        Dictionary<IngredientType, int> targetRecipe = GetBaseRecipe(order.ramenType);

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
            }
        }

        return targetRecipe;
    }

    // 기본 라멘 레시피
    private Dictionary<IngredientType, int> GetBaseRecipe(RamenType ramenType)
    {
        Dictionary<IngredientType, int> recipe = new Dictionary<IngredientType, int>();

        switch (ramenType)
        {
            case RamenType.Shio:

                recipe[IngredientType.ShioTare] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.Noodles] = 1;
                recipe[IngredientType.Chashu] = 1;
                recipe[IngredientType.Menma] = 2;
                recipe[IngredientType.GreenOnion] = 1;
                recipe[IngredientType.FlavorOil] = 1;

                break;

            case RamenType.Shoyu:

                recipe[IngredientType.ShoyuTare] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.Noodles] = 1;
                recipe[IngredientType.Chashu] = 2;
                recipe[IngredientType.Menma] = 1;
                recipe[IngredientType.GreenOnion] = 1;
                recipe[IngredientType.FlavorOil] = 1;

                break;

            case RamenType.Tonkotsu:

                recipe[IngredientType.TonkotsuBase] = 1;
                recipe[IngredientType.Broth] = 1;
                recipe[IngredientType.Noodles] = 1;
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
}