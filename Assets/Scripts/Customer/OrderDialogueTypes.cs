using System.Collections.Generic;

public enum CustomerRegion
{
    Seoul,
    Chungcheong,
    Jeolla,
    Gyeongsang
}

public enum OrderExpressionStyle
{
    Direct,
    Indirect
}

public enum DialogueFunction
{
    RamenOrder,
    IngredientAdd,
    IngredientRemove
}

public class GeneratedCustomerOrder
{
    public CustomerOrder order;
    public Dictionary<IngredientType, int> targetRecipe;
    public CustomerRegion region;
    public OrderExpressionStyle expressionStyle;
    public string dialogue;

    public GeneratedCustomerOrder(
        CustomerOrder order,
        Dictionary<IngredientType, int> targetRecipe,
        CustomerRegion region,
        OrderExpressionStyle expressionStyle,
        string dialogue)
    {
        this.order = order;
        this.targetRecipe = targetRecipe;
        this.region = region;
        this.expressionStyle = expressionStyle;
        this.dialogue = dialogue;
    }
}

public class HintData
{
    public string[] direct;
    public string[] indirect;

    public HintData(string[] direct, string[] indirect)
    {
        this.direct = direct;
        this.indirect = indirect;
    }
}