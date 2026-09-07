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

    // ── 여기부터 김기백(A) 추가 ────────────────────────────────────
    // B 동의를 받고, 조리 화면과 정산 계산을 잇기 위해 넣은 진입점이다.
    // 라멘 종류와 정답 레시피를 OrderManager만 들고 있어서, 조리 쪽에서
    // RamenCalculator를 직접 부르려면 그 둘을 다 알아야 한다. 그래서 여기서 감쌌다.
    // 이 블록 밖은 건드리지 않았다.

    [Header("정산 계산기 (A 추가)")]
    [SerializeField]
    private RamenCalculator ramenCalculator;

    /// <summary>
    /// 손님에게 낸 라멘을 채점하고 판매 금액을 돌려준다.
    /// 조리 화면의 GameManager.SubmitRamen에서 부른다.
    /// </summary>
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

        return ramenCalculator.Calculate(currentGeneratedOrder.order.ramenType,
                                         currentGeneratedOrder.targetRecipe,
                                         submitted);
    }

    // ── 김기백(A) 추가 끝 ──────────────────────────────────────────

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