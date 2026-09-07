using System;
using System.Collections.Generic;

[Serializable]
public class CustomerOrder
{
    // 기본 라멘
    public RamenType ramenType;

    // 추가 주문
    public List<IngredientRequest> requests = new List<IngredientRequest>();
}


[Serializable]
public class IngredientRequest
{
    // 어떤 재료를 추가할 것인지
    public IngredientType ingredient;

    // 몇 개 추가할 것인지
    public int amount;

    public IngredientRequest(IngredientType ingredient, int amount)
    {
        this.ingredient = ingredient;
        this.amount = amount;
    }
}