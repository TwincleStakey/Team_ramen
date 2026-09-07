using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CustomerDialogueGenerator : MonoBehaviour
{
    [Header("기존 주문 생성기")]
    [SerializeField] private CustomerOrderGenerator orderGenerator;

    private readonly RecipeGenerator recipeGenerator = new RecipeGenerator();

    // 지역과 표현 방식까지 전부 무작위로 생성한다.
    public GeneratedCustomerOrder Generate(int currentDay)
    {
        return Generate(currentDay, GetRandomEnum<CustomerRegion>(), GetRandomEnum<OrderExpressionStyle>());
    }

    // 지역과 표현 방식을 지정해 생성할 때 사용한다.
    public GeneratedCustomerOrder Generate(int currentDay, CustomerRegion region, OrderExpressionStyle style)
    {
        // 1. 라멘 종류와 추가 재료 주문 생성
        CustomerOrder order = orderGenerator.GenerateOrder(currentDay);

        // 2. 기본 레시피 + 추가 주문 합계
        // 최종 정답 레시피 딕셔너리 생성
        Dictionary<IngredientType, int> targetRecipe = recipeGenerator.GenerateTargetRecipe(order);

        // 3. 동일한 주문 데이터로 대사 생성
        string dialogue = BuildDialogue(order, region, style);

        // 4. 주문·정답 레시피·대사 함께 반환
        return new GeneratedCustomerOrder(order, targetRecipe, region, style, dialogue);
    }

    // 이미 생성된 주문을 다른 지역 말투로 다시 표현할 수도 있다.
    public string BuildDialogue( CustomerOrder order, CustomerRegion region, OrderExpressionStyle style)
    {
        RegionDialogueData regionData = RegionDialogueDatabase.Get(region);

        List<string> sentences = new List<string>
        {
            BuildRamenSentence(order.ramenType, style, regionData)
        };

        if (order.requests != null)
        {
            foreach (IngredientRequest request in order.requests)
            {
                sentences.Add(BuildIngredientSentence(request, style, regionData));
            }
        }

        return string.Join(" ", sentences.ToArray());
    }

    private string BuildRamenSentence(RamenType ramenType, OrderExpressionStyle style, RegionDialogueData regionData)
    {
        string ramen = OrderExpressionDatabase.GetRamenExpression(ramenType, style);

        string template = regionData.GetTemplate(style, DialogueFunction.RamenOrder);

        return template.Replace("{ramen}", ramen);
    }

    // amount > 0은 추가, amount < 0은 제외 대사로 처리한다.
    private string BuildIngredientSentence(IngredientRequest request, OrderExpressionStyle style, RegionDialogueData regionData)
    {
        bool isRemove = request.amount < 0;
        int amount = Mathf.Abs(request.amount);

        DialogueFunction function = isRemove ? DialogueFunction.IngredientRemove : DialogueFunction.IngredientAdd;

        string ingredient = OrderExpressionDatabase.GetIngredientExpression(request.ingredient,style);

        string template = regionData.GetTemplate(style, function);

        return template.Replace("{ingredient}", ingredient)
            .Replace("{amount}", amount.ToString()).Replace("{unit}", OrderExpressionDatabase.GetIngredientUnit(request.ingredient))
            .Replace("{add}", regionData.GetAddWord())
            .Replace("{expression}", regionData.GetAmountExpression(amount));
    }

    private T GetRandomEnum<T>() where T : struct
    {
        Array values = Enum.GetValues(typeof(T));
        return (T)values.GetValue(Random.Range(0, values.Length));
    }
}