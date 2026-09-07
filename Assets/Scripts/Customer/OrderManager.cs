using System.Collections.Generic;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    private CustomerOrderGenerator orderGenerator;
    private RecipeGenerator recipeGenerator;
    private CustomerOrder currentOrder;

    private Dictionary<IngredientType, int> currentTargetRecipe;

    [SerializeField] private int currentDay = 1;

    public CustomerOrder CurrentOrder => currentOrder;
    public Dictionary<IngredientType, int> CurrentTargetRecipe => currentTargetRecipe;
    public int CurrentDay => currentDay;

    private void Awake()
    {
        orderGenerator = GetComponent<CustomerOrderGenerator>();
        recipeGenerator = new RecipeGenerator();
    }

    public CustomerOrder CreateOrder()
    {
        currentOrder = orderGenerator.GenerateOrder(currentDay);
        currentTargetRecipe = recipeGenerator.GenerateTargetRecipe(currentOrder);

        string dialogue = DialogueGenerator.GenerateDialogue(currentOrder);

        Debug.Log(dialogue);

        return currentOrder;
    }
}