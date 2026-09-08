using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameFlowDebugTester : MonoBehaviour
{
    [Header("시작 시 자동 실행 설정")]
    [Tooltip("Play 버튼을 누르면 자동으로 5일 전체 영업 시뮬레이션 로그를 출력합니다.")]
    [SerializeField] private bool autoRunSimulationOnPlay = true;

    [Header("테스트 대상 매니저 (비워두면 자동 생성/탐색)")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private RamenCalculator ramenCalculator;
    [SerializeField] private DialogueScenarioGenerator scenarioGenerator;

    private void Start()
    {
        if (autoRunSimulationOnPlay)
        {
            Debug.Log("<color=#00FF66><b>[GameFlowDebugTester] Play 모드 감지: 5일 전체 영업 시뮬레이션을 시작합니다.</b></color>");
            EnsureComponentsExist();
            SimulateFull5Days();
        }
    }

    /// <summary>
    /// 씬에 필요한 매니저들이 없으면 자동으로 GameObject를 생성하여 연결합니다.
    /// </summary>
    public void EnsureComponentsExist()
    {
        if (scenarioGenerator == null) scenarioGenerator = FindFirstObjectByType<DialogueScenarioGenerator>();
        if (scenarioGenerator == null)
        {
            GameObject obj = new GameObject("DialogueScenarioGenerator");
            scenarioGenerator = obj.AddComponent<DialogueScenarioGenerator>();
        }

        if (ramenCalculator == null) ramenCalculator = FindFirstObjectByType<RamenCalculator>();
        if (ramenCalculator == null)
        {
            GameObject obj = new GameObject("RamenCalculator");
            ramenCalculator = obj.AddComponent<RamenCalculator>();
        }

        if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();
        if (orderManager == null)
        {
            GameObject obj = new GameObject("OrderManager");
            orderManager = obj.AddComponent<OrderManager>();
        }

        if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
        if (dayManager == null)
        {
            GameObject obj = new GameObject("DayManager");
            dayManager = obj.AddComponent<DayManager>();
        }
    }

    [ContextMenu("1. [테스트] 손님 1명 주문 생성")]
    public void TestSingleOrder()
    {
        EnsureComponentsExist();
        orderManager.CreateOrder();
        DialogueScenario scenario = orderManager.CurrentScenario;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                           📋  [손님 1명 주문 생성 결과]  📋                                   ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine($"• 페르소나  : <b>{scenario.personaName}</b> ({scenario.personaId}) | 난이도: <b>{scenario.difficulty}</b>");
        sb.AppendLine($"• 선택 라멘 : <b>{GetKoreanRamenName(scenario.order.ramenType)}</b>");

        sb.AppendLine($"\n💬 <b>[손님 대사]</b>");
        sb.AppendLine($"<color=#00FFFF>\"{scenario.Dialogue}\"</color>");

        // 1. 기본 레시피 출력
        List<string> baseList = new List<string>();
        foreach (var kvp in scenario.baseRecipe)
        {
            if (kvp.Value > 0)
            {
                baseList.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
            }
        }
        sb.AppendLine($"\n🍲 <b>[기본 레시피]</b>");
        sb.AppendLine($"[ {string.Join(", ", baseList)} ]");

        // 2. 변경되는 재료 종류 수 및 세부 변경 내역
        sb.AppendLine($"\n📝 <b>[레시피 변경 사항] (총 {scenario.changes.Count}종류 재료 변경)</b>");
        if (scenario.changes.Count > 0)
        {
            foreach (var ch in scenario.changes)
            {
                int baseAmt = scenario.baseRecipe.TryGetValue(ch.ingredient, out int b) ? b : 0;
                int targetAmt = scenario.targetRecipe.TryGetValue(ch.ingredient, out int t) ? t : 0;
                string kindStr = ch.kind == IngredientChangeKind.Add ? "추가(Add)" :
                                 ch.kind == IngredientChangeKind.Remove ? "완전제거(Remove)" : "감소(Less)";
                sb.AppendLine($"   • <b>{GetKoreanIngredientName(ch.ingredient)}</b> : 기존 {baseAmt}개 ➔ <color=#FFCC00>{kindStr} ({ch.recipeDelta:+0;-0;0})</color> ➔ 최종 <b>{targetAmt}개</b>");
            }
        }
        else
        {
            sb.AppendLine("   • 변경 없음 (기본 레시피 그대로 주문)");
        }

        // 3. 최종 정답 레시피 출력
        List<string> targetList = new List<string>();
        foreach (var kvp in scenario.targetRecipe)
        {
            if (kvp.Value > 0)
            {
                targetList.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
            }
        }
        sb.AppendLine($"\n📋 <b>[최종 정답 레시피]</b>");
        sb.AppendLine($"<color=#FFFF66>[ {string.Join(", ", targetList)} ]</color>");
        sb.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════");
        Debug.Log(sb.ToString());
    }

    [ContextMenu("2. [테스트] 현재 손님에게 100% 정답 라멘 제출")]
    public void TestSubmitPerfect()
    {
        EnsureComponentsExist();
        if (orderManager.CurrentScenario == null)
        {
            TestSingleOrder();
        }

        DialogueScenario scenario = orderManager.CurrentScenario;
        RamenState submitted = new RamenState(scenario.targetRecipe, new Dictionary<IngredientType, int>());
        
        int profit = orderManager.SubmitAndEvaluate(submitted, notifyDayManager: true);
        Debug.Log($"<color=#66FF66><b>[라멘 제출 결과]</b> 완벽 일치! | 오차: {ramenCalculator.LastTotalErrorCount}개 | 정답률: {ramenCalculator.LastAccuracy:F0}% | 판매 금액: +{profit:N0}원</color>");
    }

    [ContextMenu("3. [테스트] 현재 손님에게 오차 라멘 제출 (정답률 깎임)")]
    public void TestSubmitWithErrors()
    {
        EnsureComponentsExist();
        if (orderManager.CurrentScenario == null)
        {
            TestSingleOrder();
        }

        DialogueScenario scenario = orderManager.CurrentScenario;
        Dictionary<IngredientType, int> imperfect = new Dictionary<IngredientType, int>(scenario.targetRecipe);
        // 면과 파 수량을 1개씩 틀리게 제출
        imperfect[IngredientType.Noodles] = imperfect.TryGetValue(IngredientType.Noodles, out int n) ? n + 1 : 1;
        imperfect[IngredientType.GreenOnion] = imperfect.TryGetValue(IngredientType.GreenOnion, out int g) ? Mathf.Max(0, g - 1) : 0;

        RamenState submitted = new RamenState(imperfect, new Dictionary<IngredientType, int>());
        int profit = orderManager.SubmitAndEvaluate(submitted, notifyDayManager: true);
        Debug.Log($"<color=#FF9933><b>[라멘 제출 결과]</b> 오차 발생! | 오차: {ramenCalculator.LastTotalErrorCount}개 | 정답률: {ramenCalculator.LastAccuracy:F0}% | 판매 금액: +{profit:N0}원</color>");
    }

    [ContextMenu("4. [테스트] 5일 전체 영업 사이클 일괄 시뮬레이션")]
    public void SimulateFull5Days()
    {
        EnsureComponentsExist();

        StringBuilder totalLog = new StringBuilder();
        totalLog.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════════════╗");
        totalLog.AppendLine("║                     🍜  [5일 전체 라멘 가게 영업 시뮬레이션 시작]  🍜                         ║");
        totalLog.AppendLine("║  • 규칙: 1~3일차 당일 5명 / 4~5일차 당일 7명 | 5일차 마감 후 최종 영업 정산 및 완료         ║");
        totalLog.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════════════╝\n");

        int cumulativeProfit = 0;

        for (int day = 1; day <= DayManager.MAX_DAYS; day++)
        {
            int targetCustomers = (day <= 3) ? 5 : 7;
            totalLog.AppendLine($"\n┌──────────────────────────────────────────────────────────────────────────────────────────────┐");
            totalLog.AppendLine($"│ ☀️  [DAY {day} 영업 시작] - 오늘 목표 손님 수: {targetCustomers}명 (1~3일차: 5명 / 4~5일차: 7명)");
            totalLog.AppendLine($"└──────────────────────────────────────────────────────────────────────────────────────────────┘");

            ramenCalculator.ResetDailyProfit();

            for (int c = 1; c <= targetCustomers; c++)
            {
                orderManager.CreateOrder();
                DialogueScenario scenario = orderManager.CurrentScenario;

                // 80% 확률로 완벽 정답, 20% 확률로 오차 1개 제출
                bool perfect = (c % 4 != 0);
                Dictionary<IngredientType, int> servedIngredients = new Dictionary<IngredientType, int>(scenario.targetRecipe);
                if (!perfect)
                {
                    servedIngredients[IngredientType.Noodles] = servedIngredients.TryGetValue(IngredientType.Noodles, out int n) ? n + 1 : 2;
                }

                RamenState submittedRamen = new RamenState(servedIngredients, new Dictionary<IngredientType, int>());
                int price = ramenCalculator.Calculate(scenario.order.ramenType, scenario.targetRecipe, submittedRamen);

                totalLog.AppendLine($"\n  ▶ <b>손님 #{c} [{scenario.personaName}]</b> (난이도: {scenario.difficulty} / 라멘: {GetKoreanRamenName(scenario.order.ramenType)})");
                totalLog.AppendLine($"    💬 <b>대사:</b> \"{scenario.Dialogue.Replace("\n", " ")}\"");
                
                // 1. 기본 레시피
                List<string> baseList = new List<string>();
                foreach (var kvp in scenario.baseRecipe)
                {
                    if (kvp.Value > 0) baseList.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
                }
                totalLog.AppendLine($"    🍲 <b>기본 레시피:</b> [ {string.Join(", ", baseList)} ]");

                // 2. 변경된 재료 종류 개수 및 변경 내역
                if (scenario.changes.Count > 0)
                {
                    List<string> changeDetails = new List<string>();
                    foreach (var ch in scenario.changes)
                    {
                        int bAmt = scenario.baseRecipe.TryGetValue(ch.ingredient, out int b) ? b : 0;
                        int tAmt = scenario.targetRecipe.TryGetValue(ch.ingredient, out int t) ? t : 0;
                        string kName = ch.kind == IngredientChangeKind.Add ? "추가" :
                                       ch.kind == IngredientChangeKind.Remove ? "제거" : "감소";
                        changeDetails.Add($"{GetKoreanIngredientName(ch.ingredient)}(기본 {bAmt}개➔{kName}{ch.recipeDelta:+0;-0;0}➔최종 {tAmt}개)");
                    }
                    totalLog.AppendLine($"    📝 <b>변경 재료 ({scenario.changes.Count}종류):</b> {string.Join(", ", changeDetails)}");
                }
                else
                {
                    totalLog.AppendLine($"    📝 <b>변경 재료 (0종류):</b> 기본 레시피 그대로");
                }

                // 3. 최종 정답 레시피
                List<string> recipeStrs = new List<string>();
                foreach (var kvp in scenario.targetRecipe)
                {
                    if (kvp.Value > 0) recipeStrs.Add($"{GetKoreanIngredientName(kvp.Key)}:{kvp.Value}개");
                }
                totalLog.AppendLine($"    📋 <b>[최종 정답 레시피]:</b> <color=#00FFFF>[ {string.Join(", ", recipeStrs)} ]</color>");

                // 4. 제출 결과
                string resultColor = (ramenCalculator.LastTotalErrorCount == 0) ? "#66FF66" : "#FFAA33";
                totalLog.AppendLine($"    🥣 <b>제출 결과:</b> <color={resultColor}>오차 {ramenCalculator.LastTotalErrorCount}개 | 정답률 {ramenCalculator.LastAccuracy:F0}% | 판매 금액: +{price:N0}원</color>");
            }

            int dayProfit = ramenCalculator.TodayTotalProfit;
            float dayAvgAccuracy = ramenCalculator.TodayAverageAccuracy;
            cumulativeProfit += dayProfit;

            // 당일 영업 마감 및 정산 로그
            totalLog.AppendLine($"\n┌──────────────────────────────────────────────────────────────────────────────────────────────┐");
            totalLog.AppendLine($"│ 📊  [DAY {day} 영업 마감 및 당일 정산 (DailyResultUI 팝업)]");
            totalLog.AppendLine($"├──────────────────────────────────────────────────────────────────────────────────────────────┤");
            totalLog.AppendLine($"│ • 당일 서빙 완료 손님 : {targetCustomers}명 / {targetCustomers}명 (100% 완료)");
            totalLog.AppendLine($"│ • 당일 총 판매 수익   : {dayProfit:N0}원");
            totalLog.AppendLine($"│ • 당일 평균 정답률     : {dayAvgAccuracy:F1}%");
            totalLog.AppendLine($"│ • 5일 누적 총 매출    : {cumulativeProfit:N0}원");
            totalLog.AppendLine($"└──────────────────────────────────────────────────────────────────────────────────────────────┘");

            if (day < DayManager.MAX_DAYS)
            {
                totalLog.AppendLine($"\n  ▼ [확인 버튼 클릭] ➔ Day {day} 정산 완료 후 통계 리셋 ➔ Day {day + 1}로 넘어갑니다.\n");
            }
        }

        totalLog.AppendLine("\n════════════════════════════════════════════════════════════════════════════════════════════════");
        totalLog.AppendLine($"🎉 <b><color=#00FF66>[5일차 영업 완료! 게임 클리어!]</color></b>  최종 5일 누적 총 매출: <b><color=#FFFF00>{cumulativeProfit:N0}원</color></b>");
        totalLog.AppendLine("════════════════════════════════════════════════════════════════════════════════════════════════\n");

        Debug.Log(totalLog.ToString());
    }

    private static string GetKoreanRamenName(RamenType type)
    {
        switch (type)
        {
            case RamenType.Shio: return "시오라멘(소금)";
            case RamenType.Shoyu: return "쇼유라멘(간장)";
            case RamenType.Tonkotsu: return "돈코츠라멘(돼지뼈)";
            default: return type.ToString();
        }
    }

    private static string GetKoreanIngredientName(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.ShioTare: return "소금타래";
            case IngredientType.ShoyuTare: return "간장타래";
            case IngredientType.TonkotsuBase: return "돈코츠베이스";
            case IngredientType.Broth: return "육수";
            case IngredientType.Noodles: return "면";
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

#if UNITY_EDITOR
    [MenuItem("라멘 테스트/1. 손님 1명 주문 생성 (로그 출력)")]
    private static void MenuTestSingleOrder()
    {
        GameFlowDebugTester tester = GetOrCreateTester();
        tester.TestSingleOrder();
    }

    [MenuItem("라멘 테스트/2. 5일 전체 영업 시뮬레이션 (로그 출력)")]
    private static void MenuSimulateFull5Days()
    {
        GameFlowDebugTester tester = GetOrCreateTester();
        tester.SimulateFull5Days();
    }

    private static GameFlowDebugTester GetOrCreateTester()
    {
        GameFlowDebugTester tester = FindFirstObjectByType<GameFlowDebugTester>();
        if (tester == null)
        {
            GameObject obj = new GameObject("GameFlowTester");
            tester = obj.AddComponent<GameFlowDebugTester>();
        }
        tester.EnsureComponentsExist();
        return tester;
    }
#endif
}

