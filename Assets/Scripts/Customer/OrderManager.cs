using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [Header("시나리오 생성기 (DialogueMacroSystem)")]
    [SerializeField]
    private DialogueScenarioGenerator dialogueScenarioGenerator;

    [Header("매니저 및 계산기 연결")]
    [SerializeField]
    private DayManager dayManager;

    [SerializeField]
    private RamenCalculator ramenCalculator;

    // 현재 손님의 시나리오 (주문, 변경점, 기본/정답 레시피, 대사, 페르소나 포함)
    private DialogueScenario currentScenario;

    public DialogueScenario CurrentScenario => currentScenario;

    public CustomerOrder CurrentOrder
    {
        get
        {
            if (currentScenario == null)
            {
                return null;
            }

            return currentScenario.order;
        }
    }

    public Dictionary<IngredientType, int> CurrentTargetRecipe
    {
        get
        {
            if (currentScenario == null)
            {
                return null;
            }

            return currentScenario.targetRecipe;
        }
    }

    public string CurrentDialogue
    {
        get
        {
            if (currentScenario == null)
            {
                return "";
            }

            return currentScenario.Dialogue;
        }
    }

    public string CurrentPersonaName
    {
        get
        {
            if (currentScenario == null)
            {
                return "";
            }

            return currentScenario.personaName;
        }
    }

    public int CurrentDifficulty
    {
        get
        {
            if (currentScenario == null)
            {
                return 1;
            }

            return currentScenario.difficulty;
        }
    }

    private void Awake()
    {
        // 자동 컴포넌트 탐색 (인스펙터 연결 누락 방지)
        if (dialogueScenarioGenerator == null)
        {
            dialogueScenarioGenerator = GetComponent<DialogueScenarioGenerator>();
            if (dialogueScenarioGenerator == null)
            {
                dialogueScenarioGenerator = FindFirstObjectByType<DialogueScenarioGenerator>();
            }
        }

        if (dayManager == null)
        {
            dayManager = FindFirstObjectByType<DayManager>();
        }

        if (ramenCalculator == null)
        {
            ramenCalculator = FindFirstObjectByType<RamenCalculator>();
        }
    }

    /// <summary>
    /// 새로운 손님의 주문 시나리오를 생성하고 대사를 UI에 출력합니다.
    /// </summary>
    /// <returns>생성된 손님 주문 (CustomerOrder)</returns>
    public CustomerOrder CreateOrder()
    {
        if (dialogueScenarioGenerator == null)
        {
            dialogueScenarioGenerator = FindFirstObjectByType<DialogueScenarioGenerator>();
            if (dialogueScenarioGenerator == null)
            {
                Debug.LogError("[OrderManager] DialogueScenarioGenerator가 연결되지 않았습니다.");
                return null;
            }
        }

        // DayManager가 연결되어 있으면 DayManager의 날짜를 사용하고, 없으면 기본 1일차 사용
        int dayToUse = (dayManager != null) ? dayManager.CurrentDay : 1;

        // 시나리오 생성 (페르소나, 대사, 주문, 정답 레시피 일괄 생성)
        currentScenario = dialogueScenarioGenerator.GenerateScenario(dayToUse);

        if (dialogueText != null)
        {
            dialogueText.text = currentScenario.Dialogue;
        }

        Debug.Log($"[OrderManager] 손님 주문 생성 완료 (페르소나: {currentScenario.personaName}, 난이도: {currentScenario.difficulty})\n대사: {currentScenario.Dialogue}");

        return currentScenario.order;
    }

    /// <summary>
    /// 손님이 제출한 라멘을 채점하고 대사/피드백 UI에 오차, 정답률, 판매 금액을 띄운 뒤 판매 금액을 반환합니다.
    /// </summary>
    /// <param name="submitted">제출된 라멘 상태</param>
    /// <returns>정확도에 따른 판매 금액 (원)</returns>
    public int EvaluateRamen(RamenState submitted)
    {
        if (currentScenario == null)
        {
            Debug.LogWarning("[OrderManager] 현재 주문이 없어 채점할 수 없습니다.");
            return 0;
        }

        if (ramenCalculator == null)
        {
            ramenCalculator = FindFirstObjectByType<RamenCalculator>();
            if (ramenCalculator == null)
            {
                Debug.LogError("[OrderManager] RamenCalculator가 연결되지 않았습니다.");
                return 0;
            }
        }

        int sellingPrice = ramenCalculator.Calculate(currentScenario.order.ramenType, currentScenario.targetRecipe, submitted);

        // 손님 대사창 또는 피드백 UI에 오차 수, 정답률, 판매 금액 연출 표시
        if (dialogueText != null)
        {
            dialogueText.text = $"오차: {ramenCalculator.LastTotalErrorCount}개 | 정답률: {ramenCalculator.LastAccuracy:F0}% | +{sellingPrice:N0}원";
        }

        return sellingPrice;
    }

    /// <summary>
    /// 라멘을 제출하여 채점하고, 필요 시 DayManager에 손님 서빙 완료를 알립니다.
    /// </summary>
    public int SubmitAndEvaluate(RamenState submitted, bool notifyDayManager = true)
    {
        int sellingPrice = EvaluateRamen(submitted);

        if (notifyDayManager && dayManager != null)
        {
            dayManager.OnCustomerServed();
        }

        return sellingPrice;
    }

    /// <summary>
    /// 현재 주문이 끝났을 때(손님 퇴장 및 정산 완료 후) 호출하여 주문 상태와 대사창을 비웁니다.
    /// </summary>
    public void ClearCurrentOrder()
    {
        currentScenario = null;

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }

    [ContextMenu("테스트: 현재 주문 생성")]
    private void TestCreateOrder()
    {
        CreateOrder();
    }

    [ContextMenu("테스트: 100% 완벽 정답 라멘 제출")]
    private void TestSubmitPerfectRamen()
    {
        if (currentScenario == null)
        {
            Debug.LogWarning("현재 생성된 주문이 없습니다. 먼저 주문을 생성하세요.");
            return;
        }

        RamenState perfect = new RamenState(currentScenario.targetRecipe, new Dictionary<IngredientType, int>());
        SubmitAndEvaluate(perfect);
    }

    [ContextMenu("테스트: 오차가 있는 라멘 제출")]
    private void TestSubmitImperfectRamen()
    {
        if (currentScenario == null)
        {
            Debug.LogWarning("현재 생성된 주문이 없습니다. 먼저 주문을 생성하세요.");
            return;
        }

        // 정답 레시피에서 일부 재료 수량을 변경하여 오차 생성
        Dictionary<IngredientType, int> imperfect = new Dictionary<IngredientType, int>(currentScenario.targetRecipe);
        imperfect[IngredientType.Noodles] = imperfect.TryGetValue(IngredientType.Noodles, out int n) ? n + 1 : 1;
        
        RamenState ramen = new RamenState(imperfect, new Dictionary<IngredientType, int>());
        SubmitAndEvaluate(ramen);
    }
}