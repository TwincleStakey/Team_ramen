using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueScenarioGenerator : MonoBehaviour
{
    [Header("Resources/DialogueDB.json 또는 직접 연결")]
    [SerializeField] private TextAsset dialogueDatabaseJson;
    [SerializeField, Range(1, 3)] private int fixedDifficulty;
    [SerializeField] private bool randomDifficulty = true;
    [SerializeField] private bool includeRemoveAndLess = true;
    [SerializeField, Range(0f, 1f)] private float fillerChance = 0.5f;

    private const int MAX_REQUEST_COUNT = 4; // 최대 4개 변경 가능
    private readonly RecipeGenerator recipeGenerator = new RecipeGenerator();
    private DialogueWorkbookDatabase database;

    private void Awake()
    {
        if (dialogueDatabaseJson == null)
        {
            dialogueDatabaseJson = Resources.Load<TextAsset>("DialogueDB");
            if (dialogueDatabaseJson == null)
            {
                dialogueDatabaseJson = Resources.Load<TextAsset>("DialogueMacroSystem/DialogueDB");
            }
        }

        if (dialogueDatabaseJson != null)
        {
            database = new DialogueWorkbookDatabase(dialogueDatabaseJson);
        }
        else
        {
            Debug.LogError("[DialogueScenarioGenerator] DialogueDB.json을 찾을 수 없습니다. Resources/DialogueDB.json 또는 인스펙터 연결을 확인하세요.");
        }
    }

    public DialogueScenario GenerateScenario(int currentDay)
    {
        if (database == null) Awake();

        DialogueScenario scenario = new DialogueScenario();
        PersonaRow persona = database.RandomPersona();
        scenario.personaId = persona.personaId;
        scenario.personaName = persona.name;
        scenario.difficulty = randomDifficulty ? UnityEngine.Random.Range(1, 4) : fixedDifficulty;

        scenario.order = new CustomerOrder { ramenType = GetRandomRamen(currentDay) };
        scenario.baseRecipe = GetBaseRecipe(scenario.order.ramenType);

        HintRow hint = scenario.difficulty >= 2
            ? database.RandomHint(scenario.order.ramenType, scenario.difficulty, persona.personaId)
            : null;
        string rawHint = hint == null ? string.Empty : hint.template;
        HashSet<IngredientType> conflicts = database.GetConflicts(rawHint);

        GenerateChanges(scenario, conflicts);
        scenario.targetRecipe = recipeGenerator.GenerateTargetRecipe(scenario.order);
        BuildDialogue(scenario, persona, rawHint);
        return scenario;
    }

    private void GenerateChanges(DialogueScenario scenario, HashSet<IngredientType> conflicts)
    {
        RamenRow ramenData = database.GetRamen(scenario.order.ramenType);
        List<IngredientType> candidates = ParseIngredients(ramenData.addable);
        candidates.RemoveAll(x => conflicts.Contains(x));
        Shuffle(candidates);

        // 기본 면 및 교체 대상 면 식별 (돈코츠: 기본 ThickNoodles ➔ ThinNoodles 교체, 시오/쇼유: 기본 ThinNoodles ➔ ThickNoodles 교체)
        IngredientType defaultNoodle = (scenario.order.ramenType == RamenType.Tonkotsu)
            ? IngredientType.ThickNoodles
            : IngredientType.ThinNoodles;
        IngredientType swappedNoodle = (scenario.order.ramenType == RamenType.Tonkotsu)
            ? IngredientType.ThinNoodles
            : IngredientType.ThickNoodles;

        // 50% 확률로 면 교체 요청 발생 (자주 등장하여 체감 및 테스트 가능)
        bool swapNoodle = UnityEngine.Random.value < 0.5f;

        // 변경할 총 개수 (최대 4개)
        int maxChanges = Mathf.Min(MAX_REQUEST_COUNT, candidates.Count + (swapNoodle ? 1 : 0));
        int totalChanges = UnityEngine.Random.Range(1, maxChanges + 1);

        int toppingSlots = swapNoodle ? (totalChanges - 1) : totalChanges;

        if (swapNoodle)
        {
            // 면 교체는 1개의 독립적인 변경 요소로 카운팅
            DialogueScenarioRequest noodleChange = new DialogueScenarioRequest
            {
                ingredient = swappedNoodle,
                kind = IngredientChangeKind.Swap,
                recipeDelta = 1,
                expressionAmount = 1
            };
            scenario.changes.Add(noodleChange);

            // 기본 면 1개 제거(-1), 교체 면 1개 추가(+1)하여 targetRecipe에서 합이 정확히 1 유지되도록 함
            scenario.order.requests.Add(new IngredientRequest(defaultNoodle, -1));
            scenario.order.requests.Add(new IngredientRequest(swappedNoodle, 1));
        }

        for (int i = 0; i < toppingSlots && i < candidates.Count; i++)
        {
            IngredientType ingredient = candidates[i];
            int baseAmount = GetAmount(scenario.baseRecipe, ingredient);
            DialogueScenarioRequest change = DecideChange(ingredient, baseAmount);
            scenario.changes.Add(change);
            scenario.order.requests.Add(new IngredientRequest(ingredient, change.recipeDelta));
        }
    }

    private DialogueScenarioRequest DecideChange(IngredientType ingredient, int baseAmount)
    {
        DialogueScenarioRequest result = new DialogueScenarioRequest { ingredient = ingredient };
        float roll = UnityEngine.Random.value;

        if (includeRemoveAndLess && baseAmount > 0 && roll < 0.2f)
        {
            result.kind = IngredientChangeKind.Remove;
            result.recipeDelta = -baseAmount; // 최종 수량을 반드시 0으로 만든다.
            result.expressionAmount = 0;
        }
        else if (includeRemoveAndLess && baseAmount >= 2 && roll < 0.35f)
        {
            result.kind = IngredientChangeKind.Less;
            result.recipeDelta = -1;
            result.expressionAmount = 1;
        }
        else
        {
            result.kind = IngredientChangeKind.Add;
            result.recipeDelta = UnityEngine.Random.Range(1, 4); // 실제 정답 레시피에는 +1~+3
            result.expressionAmount = result.recipeDelta;
        }
        return result;
    }

    private void BuildDialogue(DialogueScenario scenario, PersonaRow persona, string rawHint)
    {
        AddIfNotEmpty(scenario.lines, database.RandomOpener(persona.personaId));

        if (!string.IsNullOrEmpty(rawHint))
            AddIfNotEmpty(scenario.lines, Render(rawHint, scenario, persona, null, true));

        string ramenTemplate = database.RandomTemplate("Ramen", 0, scenario.difficulty);
        AddIfNotEmpty(scenario.lines, Render(ramenTemplate, scenario, persona, null, scenario.changes.Count > 0));

        for (int i = 0; i < scenario.changes.Count; i++)
        {
            DialogueScenarioRequest change = scenario.changes[i];
            int amountCode = change.kind == IngredientChangeKind.Remove ? -1 :
                             change.kind == IngredientChangeKind.Less ? -2 :
                             change.kind == IngredientChangeKind.Swap ? -3 : change.expressionAmount;
            string template = database.RandomTemplate(change.ingredient.ToString(), amountCode, scenario.difficulty);
            bool connecting = i < scenario.changes.Count - 1 && UnityEngine.Random.value < 0.55f;
            AddIfNotEmpty(scenario.lines, Render(template, scenario, persona, change, connecting));
        }

        if (UnityEngine.Random.value < fillerChance)
        {
            string filler = database.RandomFiller();
            bool hasNoodleChange = scenario.changes.Exists(c => c.kind == IngredientChangeKind.Swap);
            // 면이 변경되었는데 filler에서 "면은 기본으로 해주세요"가 나오는 모순 방지
            if (!hasNoodleChange || !filler.Contains("면"))
            {
                AddIfNotEmpty(scenario.lines, Render(filler, scenario, persona, null, false));
            }
        }

        AddIfNotEmpty(scenario.lines, database.RandomCloser(persona.personaId));
    }

    private string Render(string text, DialogueScenario scenario, PersonaRow persona,
        DialogueScenarioRequest change, bool connecting)
    {
        string ramenKey = scenario.order.ramenType.ToString();
        text = text.Replace("{ramen}", database.RandomKeyword("Ramen", ramenKey));
        text = text.Replace("{ramen_desc}", database.RandomKeyword("RamenDesc", ramenKey));

        if (change != null)
        {
            string ingredientKey = change.ingredient.ToString();
            text = text.Replace("{ing}", database.RandomKeyword("Ingredient", ingredientKey));
            text = text.Replace("{ing_desc}", database.RandomKeyword("IngDesc", ingredientKey));
            text = text.Replace("{amt}", database.PickAmountWord(persona, change.expressionAmount));
        }

        string[] keys = { "give", "make", "want", "order", "good", "crave", "ask", "remove", "less" };
        foreach (string key in keys)
            text = text.Replace("{" + key + "}", database.PickSpeech(persona, key, connecting));

        return text.Replace("  ", " ").Trim();
    }

    private Dictionary<IngredientType, int> GetBaseRecipe(RamenType ramenType)
    {
        return RecipeGenerator.GetBaseRecipe(ramenType);
    }

    private static RamenType GetRandomRamen(int currentDay)
    {
        // currentDay에 따른 해금 조건이 생기면 이 배열만 필터링하면 된다.
        RamenType[] available = { RamenType.Shio, RamenType.Shoyu, RamenType.Tonkotsu };
        return available[UnityEngine.Random.Range(0, available.Length)];
    }

    private static List<IngredientType> ParseIngredients(string[] names)
    {
        List<IngredientType> result = new List<IngredientType>();
        foreach (string name in names)
        {
            IngredientType value;
            if (Enum.TryParse(name, out value)) result.Add(value);
        }
        return result;
    }

    private static int GetAmount(Dictionary<IngredientType, int> recipe, IngredientType ingredient)
    {
        int value;
        return recipe.TryGetValue(ingredient, out value) ? value : 0;
    }

    private static void AddIfNotEmpty(List<string> lines, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add(value);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            T temp = list[i]; list[i] = list[j]; list[j] = temp;
        }
    }
}
