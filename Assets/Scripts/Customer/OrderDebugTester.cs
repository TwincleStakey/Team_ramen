using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class OrderDebugTester : MonoBehaviour
{
    [Header("테스트 대상")]
    [SerializeField]
    private OrderManager orderManager;

    [Header("테스트 설정")]
    [SerializeField]
    private bool testOnStart = true;

    [SerializeField]
    [Min(1)]
    private int testCount = 1;

    private readonly RecipeGenerator recipeGenerator = new RecipeGenerator();

    private void Start()
    {
        if (testOnStart)
        {
            RunOrderTests();
        }
    }


    // 컴포넌트 우클릭 → Run Order Tests로도 실행할 수 있다.
    [ContextMenu("Run Order Tests")]
    public void RunOrderTests()
    {
        if (orderManager == null)
        {
            Debug.LogError("[주문 테스트 실패] OrderManager가 연결되지 않았습니다.");

            return;
        }

        for (int i = 0; i < testCount; i++)
        {
            RunSingleOrderTest(i + 1);
        }
    }


    private void RunSingleOrderTest(int testNumber)
    {
        // 실제 게임과 똑같은 진입점으로 주문을 생성한다.
        CustomerOrder generatedOrder = orderManager.CreateOrder();

        if (generatedOrder == null)
        {
            Debug.LogError("[주문 테스트 실패] 주문이 생성되지 않았습니다.");

            return;
        }

        Dictionary<IngredientType, int> baseRecipe = CreateBaseRecipe(generatedOrder.ramenType);

        Dictionary<IngredientType, int> targetRecipe = orderManager.CurrentTargetRecipe;

        StringBuilder log = new StringBuilder();

        log.AppendLine();
        log.AppendLine("========================================");
        log.AppendLine("[주문 생성 테스트 " + testNumber + "]");
        log.AppendLine("========================================");

        log.AppendLine("선택 지역: " + GetRegionName(orderManager.CurrentRegion));

        log.AppendLine("대사 타입: " + GetExpressionStyleName(orderManager.CurrentExpressionStyle));

        log.AppendLine("선택 라멘: " + GetRamenName(generatedOrder.ramenType));

        AppendRecipe(log, "원래 기본 레시피", baseRecipe, false);

        AppendRequests(log, generatedOrder.requests, baseRecipe);

        AppendRecipe(log, "최종 정답 레시피", targetRecipe, true);

        log.AppendLine();
        log.AppendLine("손님 대사:");
        log.AppendLine(orderManager.CurrentDialogue);
        log.AppendLine("========================================");

        Debug.Log(log.ToString());
    }


    // 변경 요청이 없는 CustomerOrder를 만들어 기본 레시피만 얻는다.
    private Dictionary<IngredientType, int> CreateBaseRecipe(RamenType ramenType)
    {
        CustomerOrder baseOrder = new CustomerOrder();
        baseOrder.ramenType = ramenType;
        baseOrder.requests.Clear();

        return recipeGenerator.GenerateTargetRecipe(baseOrder);
    }


    private void AppendRequests(StringBuilder log, List<IngredientRequest> requests, Dictionary<IngredientType, int> baseRecipe)
    {
        log.AppendLine();

        int requestCount = requests == null ? 0 : requests.Count;

        log.AppendLine("변경된 재료 종류 수: " + requestCount);

        if (requestCount == 0)
        {
            log.AppendLine("- 변경 없음");
            return;
        }

        foreach (IngredientRequest request in requests)
        {
            int baseAmount = GetAmount(baseRecipe, request.ingredient);

            int finalAmount = baseAmount + request.amount;

            string changeText;

            if (request.amount > 0)
            {
                changeText = "+" + request.amount;
            }
            else
            {
                changeText = request.amount.ToString();
            }

            log.AppendLine("- " + request.ingredient + ": 기본 " + baseAmount + "개 / 변경 " + changeText + "개 / 최종 " + finalAmount + "개");
        }
    }


    private void AppendRecipe(StringBuilder log, string title, Dictionary<IngredientType, int> recipe, bool showZeroAmount)
    {
        log.AppendLine();
        log.AppendLine(title + ":");

        if (recipe == null)
        {
            log.AppendLine("- 레시피 없음");
            return;
        }

        foreach (
            KeyValuePair<IngredientType, int> ingredient
            in recipe)
        {
            if (!showZeroAmount && ingredient.Value <= 0)
            {
                continue;
            }

            log.AppendLine("- " + ingredient.Key + ": " + ingredient.Value + "개");
        }
    }


    private int GetAmount(Dictionary<IngredientType, int> recipe, IngredientType ingredient)
    {
        int amount;

        if (recipe != null && recipe.TryGetValue(ingredient, out amount))
        {
            return amount;
        }

        return 0;
    }


    private string GetRegionName(CustomerRegion region)
    {
        switch (region)
        {
            case CustomerRegion.Seoul:
                return "서울";

            case CustomerRegion.Chungcheong:
                return "충청도";

            case CustomerRegion.Jeolla:
                return "전라도";

            case CustomerRegion.Gyeongsang:
                return "경상도";

            default:
                return region.ToString();
        }
    }


    private string GetExpressionStyleName(OrderExpressionStyle style)
    {
        return style == OrderExpressionStyle.Direct ? "직설적 표현" : "간접적 표현";
    }


    private string GetRamenName(RamenType ramenType)
    {
        switch (ramenType)
        {
            case RamenType.Shio:
                return "시오 라멘";

            case RamenType.Shoyu:
                return "쇼유 라멘";

            case RamenType.Tonkotsu:
                return "돈코츠 라멘";

            default:
                return ramenType.ToString();
        }
    }
}
