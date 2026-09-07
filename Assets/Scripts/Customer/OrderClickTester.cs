using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 화면 클릭 시 주문을 생성하고 대사 및 재료 상세 정보를 로그로 출력하는 테스트 스크립트
/// (유니티 콘솔 창 리스트에서도 한눈에 보이도록 한 줄 요약 및 상세 로그 출력)
/// </summary>
public class OrderClickTester : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾거나 생성합니다)")]
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private CustomerOrderGenerator orderGenerator;
    [SerializeField] private RecipeGenerator recipeGenerator;

    [Header("테스트 설정")]
    [Tooltip("테스트할 일차 (Day 1: 시오, Day 2: 쇼유, Day 4: 돈코츠 해금)")]
    [SerializeField] private int testDay = 1;

    [Tooltip("화면 아무 곳이나 클릭해도 작동할지 여부")]
    [SerializeField] private bool clickAnywhere = true;

    private void Awake()
    {
        // 씬에서 OrderManager를 찾고 없으면 컴포넌트 자동 준비
        if (orderManager == null)
        {
            orderManager = FindFirstObjectByType<OrderManager>();
        }

        if (orderGenerator == null)
        {
            orderGenerator = FindFirstObjectByType<CustomerOrderGenerator>();
            if (orderGenerator == null && orderManager != null)
            {
                orderGenerator = orderManager.GetComponent<CustomerOrderGenerator>();
            }
            if (orderGenerator == null)
            {
                orderGenerator = gameObject.AddComponent<CustomerOrderGenerator>();
            }
        }

        if (recipeGenerator == null)
        {
            recipeGenerator = new RecipeGenerator();
        }
    }

    private void Update()
    {
        if (!clickAnywhere) return;

        bool isClicked = false;

#if ENABLE_INPUT_SYSTEM
        // New Input System 처리 (마우스 좌클릭 또는 터치 또는 스페이스바)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            isClicked = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            isClicked = true;
        }
        else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isClicked = true;
        }
#else
        // Legacy Input 처리
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            isClicked = true;
        }
#endif

        if (isClicked)
        {
            TriggerOrderTest();
        }
    }

    /// <summary>
    /// 주문을 생성하고 대사 및 라멘 상세 정보(종류, 추가 재료, 최종 레시피)를 로그로 출력합니다.
    /// </summary>
    [ContextMenu("주문 생성 테스트 실행")]
    public void TriggerOrderTest()
    {
        CustomerOrder order;
        Dictionary<IngredientType, int> targetRecipe;

        // OrderManager가 있으면 OrderManager를 통해 생성, 없으면 독립 생성
        if (orderManager != null)
        {
            order = orderManager.CreateOrder();
            targetRecipe = orderManager.CurrentTargetRecipe;
        }
        else
        {
            order = orderGenerator.GenerateOrder(testDay);
            targetRecipe = recipeGenerator.GenerateTargetRecipe(order);
        }

        string dialogue = DialogueGenerator.GenerateDialogue(order);

        // 콘솔창 리스트에서도 바로 볼 수 있도록 한 줄 요약 로그 출력
        PrintOrderSummaryLog(order, dialogue, targetRecipe);
    }

    private void PrintOrderSummaryLog(CustomerOrder order, string dialogue, Dictionary<IngredientType, int> targetRecipe)
    {
        // 1. 추가 재료 요약 문자열 생성
        StringBuilder extraSb = new StringBuilder();
        if (order.requests != null && order.requests.Count > 0)
        {
            for (int i = 0; i < order.requests.Count; i++)
            {
                IngredientRequest req = order.requests[i];
                extraSb.Append($"{req.ingredient}(+{req.amount}개)");
                if (i < order.requests.Count - 1) extraSb.Append(", ");
            }
        }
        else
        {
            extraSb.Append("없음");
        }

        // 2. 최종 레시피 요약 문자열 생성
        StringBuilder recipeSb = new StringBuilder();
        if (targetRecipe != null)
        {
            int idx = 0;
            foreach (var pair in targetRecipe)
            {
                recipeSb.Append($"{pair.Key}:{pair.Value}개");
                idx++;
                if (idx < targetRecipe.Count) recipeSb.Append(", ");
            }
        }

        // =========================================================================
        // 콘솔 리스트에서 바로 확인 가능한 3줄 분리 로그 출력
        // =========================================================================
        Debug.Log($"[1. 손님 대사] {dialogue.Replace("\n", " / ")}");
        Debug.Log($"[2. 주문 정보] 라멘: {order.ramenType} | 추가 재료({order.requests?.Count ?? 0}종류): [{extraSb}]");
        Debug.Log($"[3. 최종 정답 레시피] [{recipeSb}]");
    }
}
