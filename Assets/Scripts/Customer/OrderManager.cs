using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [Header("주문 대사 생성기")]
    [SerializeField]
    private CustomerDialogueGenerator dialogueGenerator;

    [Header("현재 날짜")]
    [SerializeField]
    private int currentDay = 1;

    // 현재 손님의 주문·정답 레시피·대사를 함께 보관한다.
    private GeneratedCustomerOrder currentGeneratedOrder;

    public CustomerOrder CurrentOrder
    {
        get
        {
            if (currentGeneratedOrder == null)
            {
                return null;
            }

            return currentGeneratedOrder.order;
        }
    }

    public Dictionary<IngredientType, int> CurrentTargetRecipe
    {
        get
        {
            if (currentGeneratedOrder == null)
            {
                return null;
            }

            return currentGeneratedOrder.targetRecipe;
        }
    }

    public string CurrentDialogue
    {
        get
        {
            if (currentGeneratedOrder == null)
            {
                return "";
            }

            return currentGeneratedOrder.dialogue;
        }
    }

    public CustomerRegion CurrentRegion
    {
        get
        {
            if (currentGeneratedOrder == null)
            {
                return CustomerRegion.Seoul;
            }

            return currentGeneratedOrder.region;
        }
    }

    public OrderExpressionStyle CurrentExpressionStyle
    {
        get
        {
            if (currentGeneratedOrder == null)
            {
                return OrderExpressionStyle.Direct;
            }

            return currentGeneratedOrder.expressionStyle;
        }
    }

    public int CurrentDay => currentDay;

    // 새로운 손님의 주문을 생성한다.
    public CustomerOrder CreateOrder()
    {
        if (dialogueGenerator == null)
        {
            Debug.LogError("CustomerDialogueGenerator가 연결되지 않았습니다.");

            return null;
        }

        // 이 호출 한 번으로 주문, 최종 레시피, 대사가 함께 생성된다.
        currentGeneratedOrder = dialogueGenerator.Generate(currentDay);

        if (dialogueText != null)
        {
            dialogueText.text = currentGeneratedOrder.dialogue;
        }

        Debug.Log("[손님 주문] " + currentGeneratedOrder.dialogue);

        return currentGeneratedOrder.order;
    }

    // 날짜 진행이 필요할 때 호출한다.
    public void SetCurrentDay(int day)
    {
        currentDay = Mathf.Max(1, day);
    }

    // 현재 주문이 끝났을 때 호출한다.
    public void ClearCurrentOrder()
    {
        currentGeneratedOrder = null;

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }
}