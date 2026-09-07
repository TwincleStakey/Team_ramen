using System.Collections.Generic;

public class RamenState
{
    // 최종 제출된 그릇
    public Dictionary<IngredientType, int>
        selectedIngredients;

    public RamenState(Dictionary<IngredientType, int> selected, Dictionary<IngredientType, int> discarded)
    {
        // CookingManager에서 Clear해도 전달 데이터가
        // 사라지지 않도록 반드시 복사한다.
        selectedIngredients = new Dictionary<IngredientType, int> (selected);
    }
}