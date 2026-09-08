using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DialogueScenarioDebugTester : MonoBehaviour
{
    [SerializeField] private DialogueScenarioGenerator generator;
    [SerializeField] private int currentDay = 1;

    [ContextMenu("주문 시나리오 생성 테스트")]
    public void TestGenerateScenario()
    {
        DialogueScenario result = generator.GenerateScenario(currentDay);
        StringBuilder log = new StringBuilder();
        log.AppendLine("===== 주문 시나리오 =====");
        log.AppendLine("페르소나: " + result.personaName + " (" + result.personaId + ")");
        log.AppendLine("난이도/대사 타입: " + result.difficulty + " / " + DifficultyName(result.difficulty));
        log.AppendLine("선택 라멘: " + result.order.ramenType);
        AppendRecipe(log, "원래 레시피", result.baseRecipe);

        log.AppendLine("변경 재료 종류 수: " + result.changes.Count);
        foreach (DialogueScenarioRequest change in result.changes)
            log.AppendLine("- " + change.ingredient + " / " + change.kind +
                           " / 실제 변화량 " + Signed(change.recipeDelta));

        AppendRecipe(log, "최종 정답 레시피", result.targetRecipe);
        log.AppendLine("----- 손님 대사 -----");
        log.AppendLine(result.Dialogue);
        Debug.Log(log.ToString());
    }

    [ContextMenu("주문 시나리오 100회 자동 검증")]
    public void ValidateOneHundredScenarios()
    {
        for (int i = 0; i < 100; i++)
        {
            DialogueScenario result = generator.GenerateScenario(currentDay);

            if (result.changes.Count < 1 || result.changes.Count > 3)
                Debug.LogError("변경 재료 종류 수 오류: " + result.changes.Count);

            foreach (DialogueScenarioRequest change in result.changes)
            {
                int finalAmount = result.targetRecipe.ContainsKey(change.ingredient)
                    ? result.targetRecipe[change.ingredient] : 0;

                if (finalAmount < 0)
                    Debug.LogError("음수 레시피 발생: " + change.ingredient + " = " + finalAmount);
                if (change.kind == IngredientChangeKind.Remove && finalAmount != 0)
                    Debug.LogError("완전 제거 실패: " + change.ingredient + " = " + finalAmount);
                if (change.kind == IngredientChangeKind.Add &&
                    (change.recipeDelta < 1 || change.recipeDelta > 3))
                    Debug.LogError("추가 수량 범위 오류: " + change.recipeDelta);
            }

            string dialogue = result.Dialogue;
            if (dialogue.Contains("{unit}") || dialogue.Contains("회분") ||
                dialogue.Contains("1번") || dialogue.Contains("2번") || dialogue.Contains("3번"))
                Debug.LogError("대사에 금지된 정확 수량 표현이 노출됨:\n" + dialogue);
        }

        Debug.Log("주문 시나리오 100회 자동 검증 완료");
    }

    private static void AppendRecipe(StringBuilder log, string title,
        Dictionary<IngredientType, int> recipe)
    {
        log.AppendLine("[" + title + "]");
        foreach (KeyValuePair<IngredientType, int> pair in recipe)
            log.AppendLine("- " + pair.Key + ": " + pair.Value);
    }

    private static string DifficultyName(int difficulty)
    {
        return difficulty == 1 ? "직설" : difficulty == 2 ? "설명형" : "간접형";
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
}
