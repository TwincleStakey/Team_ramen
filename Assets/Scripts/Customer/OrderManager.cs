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

    // 새로운 손님의 주문 시나리오를 생성하고 대사를 UI에 출력합니다.
    // <returns>생성된 손님 주문 (CustomerOrder)</returns>
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

        // 주문 상세 로그 (어떤 재료가 들어갔고, 어떤 재료의 수량이 어떻게 변경되었는지 전체 출력)
        LogOrderDetails(currentScenario);

        return currentScenario.order;
    }

    // 주문에 대한 상세 정보(기본 재료, 변경 재료 및 변화량, 최종 정답 레시피, 대사)를 콘솔에 출력합니다.
    private void LogOrderDetails(DialogueScenario scenario)
    {
        if (scenario == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                           📋  [손님 주문 생성 상세 정보]  📋                                   ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine($"• 페르소나  : <b>{scenario.personaName}</b> ({scenario.personaId}) | 난이도: <b>{scenario.difficulty}</b>");
        sb.AppendLine($"• 선택 라멘 : <b>{GetKoreanRamenName(scenario.order.ramenType)}</b>");

        sb.AppendLine($"\n💬 <b>[손님 대사]</b>");
        sb.AppendLine($"<color=#00FFFF>\"{scenario.Dialogue.Replace("\n", " ")}\"</color>");

        // 1. 기본 레시피 (원래 들어가는 재료 목록)
        List<string> baseList = new List<string>();
        if (scenario.baseRecipe != null)
        {
            foreach (var kvp in scenario.baseRecipe)
            {
                if (kvp.Value > 0)
                {
                    baseList.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
                }
            }
        }
        sb.AppendLine($"\n🍲 <b>[1. 기본 레시피 (원래 재료)]</b>");
        sb.AppendLine($"[ {string.Join(", ", baseList)} ]");

        // 2. 수량 변경 사항 (어떤 재료가 추가/제거/감소/교체되었는지)
        sb.AppendLine($"\n📝 <b>[2. 레시피 변경 사항] (총 {scenario.changes.Count}종류 재료 변경)</b>");
        if (scenario.changes != null && scenario.changes.Count > 0)
        {
            foreach (var ch in scenario.changes)
            {
                int baseAmt = scenario.baseRecipe != null && scenario.baseRecipe.TryGetValue(ch.ingredient, out int b) ? b : 0;
                int targetAmt = scenario.targetRecipe != null && scenario.targetRecipe.TryGetValue(ch.ingredient, out int t) ? t : 0;
                string kindStr = ch.kind == IngredientChangeKind.Add ? "추가(Add)" :
                                 ch.kind == IngredientChangeKind.Remove ? "완전제거(Remove)" :
                                 ch.kind == IngredientChangeKind.Less ? "감소(Less)" : "면교체(Swap)";
                sb.AppendLine($"   • <b>{GetKoreanIngredientName(ch.ingredient)}</b> : 기존 {baseAmt}개 ➔ <color=#FFCC00>{kindStr} ({ch.recipeDelta:+0;-0;0})</color> ➔ 최종 <b>{targetAmt}개</b>");
            }
        }
        else
        {
            sb.AppendLine("   • 변경 없음 (기본 레시피 그대로 주문)");
        }

        // 3. 최종 완성 목표 레시피 (정답 레시피)
        List<string> targetList = new List<string>();
        if (scenario.targetRecipe != null)
        {
            foreach (var kvp in scenario.targetRecipe)
            {
                if (kvp.Value > 0)
                {
                    targetList.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
                }
            }
        }
        sb.AppendLine($"\n📋 <b>[3. 최종 정답 레시피 (완성 목표)]</b>");
        sb.AppendLine($"<color=#FFFF66>[ {string.Join(", ", targetList)} ]</color>");
        sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════");

        Debug.Log(sb.ToString());
    }

    public static string GetKoreanRamenName(RamenType type)
    {
        switch (type)
        {
            case RamenType.Shio: return "시오라멘(소금)";
            case RamenType.Shoyu: return "쇼유라멘(간장)";
            case RamenType.Tonkotsu: return "돈코츠라멘(돼지뼈)";
            default: return type.ToString();
        }
    }

    public static string GetKoreanIngredientName(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.ShioTare: return "소금타래";
            case IngredientType.ShoyuTare: return "간장타래";
            case IngredientType.TonkotsuBase: return "돈코츠베이스";
            case IngredientType.Broth: return "육수";
            case IngredientType.ThinNoodles: return "얇은면";
            case IngredientType.ThickNoodles: return "굵은면";
            case IngredientType.Chashu: return "차슈";
            case IngredientType.Menma: return "멘마";
            case IngredientType.GreenOnion: return "파";
            case IngredientType.Egg: return "계란";
            case IngredientType.Nori: return "김";
            case IngredientType.BeanSprout: return "숙주";
            case IngredientType.WoodEar: return "목이버섯";
            case IngredientType.FlavorOil: return "향미유";
            case IngredientType.ChiliPowder: return "고춧가루";
            default: return type.ToString();
        }
    }

    // 손님이 제출한 라멘을 채점하고 대사/피드백 UI에 오차, 정답률, 판매 금액을 띄운 뒤 판매 금액을 반환합니다.
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

    // 라멘을 제출하여 채점하고, 필요 시 DayManager에 손님 서빙 완료를 알립니다.
    public int SubmitAndEvaluate(RamenState submitted, bool notifyDayManager = true)
    {
        int sellingPrice = EvaluateRamen(submitted);

        if (notifyDayManager && dayManager != null)
        {
            dayManager.OnCustomerServed();
        }

        return sellingPrice;
    }

    // 현재 주문이 끝났을 때(손님 퇴장 및 정산 완료 후) 호출하여 주문 상태와 대사창을 비웁니다.
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

        // 정답 레시피에서 일부 토핑 수량을 변경하여 오차 생성 (예: 차슈 1개 추가)
        Dictionary<IngredientType, int> imperfect = new Dictionary<IngredientType, int>(currentScenario.targetRecipe);
        imperfect[IngredientType.Chashu] = imperfect.TryGetValue(IngredientType.Chashu, out int c) ? c + 1 : 1;
        
        RamenState ramen = new RamenState(imperfect, new Dictionary<IngredientType, int>());
        SubmitAndEvaluate(ramen);
    }
}