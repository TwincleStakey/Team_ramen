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

    // 현재 손님의 주문·정답 레시피·대사를 함께 보관
    private GeneratedCustomerOrder currentGeneratedOrder;

    [Header("매니저 연결")]
    [SerializeField]
    private DayManager dayManager;

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

    // 새로운 손님의 주문을 생성한다.
    public CustomerOrder CreateOrder()
    {
        if (dialogueGenerator == null)
        {
            Debug.LogError("CustomerDialogueGenerator가 연결되지 않았습니다.");

            return null;
        }

        // DayManager가 연결되어 있으면 DayManager의 날짜를 사용하고, 없으면 기본 1일차 사용
        int dayToUse = (dayManager != null) ? dayManager.CurrentDay : 1;

        // 이 호출 한 번으로 주문, 최종 레시피, 대사가 함께 생성된다.
        currentGeneratedOrder = dialogueGenerator.Generate(dayToUse);

        if (dialogueText != null)
        {
            dialogueText.text = currentGeneratedOrder.dialogue;
        }

        Debug.Log("[손님 주문] " + currentGeneratedOrder.dialogue);

        return currentGeneratedOrder.order;
    }

    // 조리 화면과 정산 계산 연결
    [Header("정산 계산기 (A 추가)")]
    [SerializeField]
    private RamenCalculator ramenCalculator;

    // 손님에게 낸 라멘을 채점하고 판매 금액을 돌려준다.
    // 조리 화면의 GameManager.SubmitRamen에서 부른다.
    public int EvaluateRamen(RamenState submitted)
    {
        if (currentGeneratedOrder == null)
        {
            Debug.LogWarning("[OrderManager] 현재 주문이 없어 채점할 수 없습니다.");

            return 0;
        }

        if (ramenCalculator == null)
        {
            Debug.LogError("[OrderManager] RamenCalculator가 연결되지 않았습니다.");

            return 0;
        }

        int sellingPrice = ramenCalculator.Calculate(currentGeneratedOrder.order.ramenType, currentGeneratedOrder.targetRecipe, submitted);

        // 손님 대사창에 피드백 말풍선 연출 표시 ("정확도 90%! +8,000원")
        if (dialogueText != null)
        {
            dialogueText.text = $"정확도 {ramenCalculator.LastAccuracy:F0}%! +{sellingPrice:N0}원";
        }

        return sellingPrice;
    }

    // 현재 주문이 끝났을 때(손님 퇴장 및 정산 완료 후) 호출하여 주문 상태와 대사창을 비운다.
    public void ClearCurrentOrder()
    {
        currentGeneratedOrder = null;

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }
}