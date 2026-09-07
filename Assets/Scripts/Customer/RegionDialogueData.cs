using System.Collections.Generic;
using UnityEngine;

public abstract class RegionDialogueData
{
    public abstract CustomerRegion Region { get; }

    protected string[] muchWords;
    protected string[] littleWords;
    protected string[] addWords;

    protected readonly Dictionary<OrderExpressionStyle, Dictionary<DialogueFunction, string[]>> templates =
        new Dictionary<OrderExpressionStyle, Dictionary<DialogueFunction, string[]>>();

    public string GetTemplate(OrderExpressionStyle style, DialogueFunction function)
    {
        string[] values = templates[style][function];

        return values[Random.Range(0, values.Length)];
    }

    public string GetAmountExpression(int amount)
    {
        // +1: littleWords에서 하나
        if (amount == 1)
        {
            return GetRandom(littleWords);
        }

        // +2: littleWords와 addWords에서 각각 하나
        if (amount == 2)
        {
            string littleWord = GetRandom(littleWords);

            string addWord = GetRandom(addWords);

            return littleWord + " " + addWord;
        }

        // +3: muchWords에서 하나
        return GetRandom(muchWords);
    }

    public string GetAddWord()
    {
        return GetRandom(addWords);
    }

    protected void SetTemplates(
        string[] directRamen,
        string[] directAdd,
        string[] directRemove,
        string[] indirectRamen,
        string[] indirectAdd,
        string[] indirectRemove)
    {
        templates[OrderExpressionStyle.Direct] = CreateFunctionDictionary(directRamen, directAdd, directRemove);

        templates[OrderExpressionStyle.Indirect] = CreateFunctionDictionary(indirectRamen, indirectAdd, indirectRemove);
    }

    private Dictionary<DialogueFunction, string[]> CreateFunctionDictionary(string[] ramen, string[] add, string[] remove)
    {
        return new Dictionary<DialogueFunction, string[]>
        {
            { DialogueFunction.RamenOrder, ramen },
            { DialogueFunction.IngredientAdd, add },
            { DialogueFunction.IngredientRemove, remove }
        };
    }

    private string GetRandom(string[] values)
    {
        return values[Random.Range(0, values.Length)];
    }
}
