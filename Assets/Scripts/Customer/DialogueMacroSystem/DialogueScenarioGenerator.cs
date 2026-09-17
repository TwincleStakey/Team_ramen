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

    /// <summary>템플릿 amount 칸의 "기본면 유지 언급" 부호. -3 은 교체.</summary>
    private const int KEEP_NOODLE_CODE = -4;

    [SerializeField] private float[] keepNoodleChance = { 0.4f, 0.3f, 0.25f };
    private readonly RecipeGenerator recipeGenerator = new RecipeGenerator();
    private DialogueWorkbookDatabase database;

    /// <summary>
    /// 일차별 난이도 배분. 칸 수가 그 날 손님 수(DayManager 5·5·6·6·8)와 같아야 한다.
    ///
    /// 손님마다 따로 뽑지 않고 하루치를 한 가방에 담아 섞는다. 매번 무작위로 뽑으면
    /// "다섯 중 셋은 난이도 1" 같은 배분이 지켜지지 않아, 첫날에 난이도 3만 다섯이 나올 수도 있다.
    /// </summary>
    private static readonly int[][] DayDifficulties =
    {
        new[] { 1, 1, 1, 1, 1 },          // 1일차 — 5명
        new[] { 1, 1, 1, 2, 2 },          // 2일차 — 5명
        new[] { 1, 1, 2, 2, 2, 3 },       // 3일차 — 6명
        new[] { 1, 2, 2, 2, 3, 3 },       // 4일차 — 6명
        new[] { 1, 2, 2, 3, 3, 3, 3, 3 }  // 5일차 — 8명
    };

    /// <summary>
    /// 난이도별 요청 개수(면 교체도 한 개로 센다).
    ///
    /// 난이도는 원래 "얼마나 에두르게 말하느냐"만 정했다. 그것만으로는 첫날 손님도 재료 넷을
    /// 시킬 수 있어서 일차가 올라가도 체감이 그대로였다.
    ///
    /// 위 끝 4 는 <see cref="RecipeGenerator.MAX_TOPPING_COUNT"/> 와 맞물려 있다.
    /// 한쪽만 올리면 100% 가 나올 수 없는 주문이 생긴다.
    /// </summary>
    private static readonly int[] ChangeMin = { 1, 2, 3 };
    private static readonly int[] ChangeMax = { 2, 3, 4 };

    /// <summary>오늘 남은 난이도. <see cref="BeginDay"/> 가 채우고 손님마다 한 개씩 꺼낸다.</summary>
    private readonly List<int> difficultyBag = new List<int>();

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

    /// <summary>
    /// 여기서 만들지 않은 손님의 말투를 최근 목록에 얹는다. 튜토리얼 손님이 그렇다.
    /// 안 얹으면 바로 다음 손님이 같은 얼굴로 나온다.
    /// </summary>
    public void NotePersonaUsed(string personaId)
    {
        if (database == null) Awake();
        if (database != null) database.NotePersonaUsed(personaId);
    }

    /// <summary>
    /// 오늘 몫의 난이도를 가방에 새로 담는다. <b>그 날 첫 손님 주문을 만들기 전에</b> 불러야 한다
    /// (DayManager.StartDay 가 손님 수를 0 으로 되돌리는 자리에서 부른다).
    ///
    /// 하루가 바뀌는 것을 여기서 스스로 알아채게 하지 않았다. 목표 미달로 1일차를 다시 시작하면
    /// 일차가 그대로 1 이라, 날이 바뀐 줄 모르고 앞판에서 쓰다 남은 가방을 이어 쓰게 된다.
    /// </summary>
    public void BeginDay(int day)
    {
        difficultyBag.Clear();

        if (day < 1 || day > DayDifficulties.Length) return;

        difficultyBag.AddRange(DayDifficulties[day - 1]);
        Shuffle(difficultyBag);
    }

    /// <summary>
    /// 이번 손님의 난이도. 가방에서 한 개 꺼낸다.
    ///
    /// 가방이 비어 있으면(표에 없는 날, 또는 표보다 손님이 많은 날) 예전처럼 무작위로 돈다.
    /// 배분이 어긋나는 것이 주문이 아예 안 만들어지는 것보다 낫다.
    /// </summary>
    private int NextDifficulty()
    {
        // 인스펙터에서 난이도를 고정해 둔 경우(디버그 테스터)에는 가방을 쓰지 않는다.
        if (!randomDifficulty) return fixedDifficulty;

        if (difficultyBag.Count == 0) return UnityEngine.Random.Range(1, 4);

        int last = difficultyBag.Count - 1;
        int value = difficultyBag[last];
        difficultyBag.RemoveAt(last);
        return value;
    }

    public DialogueScenario GenerateScenario(int currentDay)
    {
        if (database == null) Awake();

        DialogueScenario scenario = new DialogueScenario();
        PersonaRow persona = database.RandomPersona();
        scenario.personaId = persona.personaId;
        scenario.personaName = persona.name;
        scenario.difficulty = NextDifficulty();

        scenario.order = new CustomerOrder { ramenType = GetRandomRamen(currentDay) };
        scenario.baseRecipe = GetBaseRecipe(scenario.order.ramenType);

        HintRow hint = scenario.difficulty >= 2
            ? database.RandomHint(scenario.order.ramenType, scenario.difficulty, persona.personaId)
            : null;
        string rawHint = hint == null ? string.Empty : hint.template;
        HashSet<IngredientType> conflicts = database.GetConflicts(rawHint);

        GenerateChanges(scenario, conflicts, currentDay);
        scenario.targetRecipe = recipeGenerator.GenerateTargetRecipe(scenario.order);
        BuildDialogue(scenario, persona, rawHint);
        return scenario;
    }

    private void GenerateChanges(DialogueScenario scenario, HashSet<IngredientType> conflicts, int currentDay)
    {
        RamenRow ramenData = database.GetRamen(scenario.order.ramenType);
        List<IngredientType> candidates = ParseIngredients(ramenData.addable);
        candidates.RemoveAll(x => conflicts.Contains(x));

        // 아직 안 들어온 재료는 후보에서 뺀다. 조리 화면에서 통이 잠겨 있어서,
        // 넣으라고 시키면 만들 수 없는 주문이 된다. 표는 IngredientUnlock 한 곳에 있다.
        candidates.RemoveAll(x => IngredientUnlock.IsLocked(x, currentDay));

        Shuffle(candidates);

        // 기본 면 및 교체 대상 면 식별 (돈코츠: 기본 ThickNoodles ➔ ThinNoodles 교체, 시오/쇼유: 기본 ThinNoodles ➔ ThickNoodles 교체)
        IngredientType defaultNoodle = DefaultNoodle(scenario.order.ramenType);
        IngredientType swappedNoodle = SwappedNoodle(scenario.order.ramenType);

        // 40% 확률로 면 교체 요청 발생
        bool swapNoodle = UnityEngine.Random.value < 0.4f;

        // 변경할 총 개수. 난이도가 폭을 정하고(난1 1~2 · 난2 2~3 · 난3 3~4), 남은 후보가
        // 그보다 적으면 거기에 맞춘다. 첫날처럼 잠긴 재료가 많은 날은 후보가 먼저 바닥난다.
        int maxChanges = Mathf.Min(MAX_REQUEST_COUNT, candidates.Count + (swapNoodle ? 1 : 0));
        int tier = Mathf.Clamp(scenario.difficulty - 1, 0, ChangeMin.Length - 1);
        int totalChanges = Mathf.Clamp(UnityEngine.Random.Range(ChangeMin[tier], ChangeMax[tier] + 1),
                                       1, maxChanges);

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

        // 기본면 유지 언급 — 교체가 없을 때만, 난이도별 확률로 "면은 그대로"라고 말해 준다.
        // 정답에는 영향이 없으므로 changes 에 넣지 않는다. 요청이 꽉 찼으면(4개) 줄 수 때문에 생략 — 최대 8줄.
        bool hasSwap = scenario.changes.Exists(c => c.kind == IngredientChangeKind.Swap);
        bool keepNoodle = !hasSwap && scenario.changes.Count < MAX_REQUEST_COUNT
                          && UnityEngine.Random.value < KeepNoodleChance(scenario.difficulty);

        string ramenTemplate = database.RandomTemplate("Ramen", 0, scenario.difficulty, persona.personaId);
        AddIfNotEmpty(scenario.lines, Render(ramenTemplate, scenario, persona, null, scenario.changes.Count > 0 || keepNoodle));

        if (keepNoodle)
        {
            // Render 는 {ing}/{ing_desc} 를 채우는 데 재료만 쓴다. kind·delta 는 아무 데도 안 남는다.
            var keep = new DialogueScenarioRequest { ingredient = DefaultNoodle(scenario.order.ramenType), expressionAmount = 1 };
            string keepTemplate = database.RandomTemplate(keep.ingredient.ToString(), KEEP_NOODLE_CODE, scenario.difficulty, persona.personaId);
            bool keepConnecting = scenario.changes.Count > 0 && UnityEngine.Random.value < 0.55f;
            AddIfNotEmpty(scenario.lines, Render(keepTemplate, scenario, persona, keep, keepConnecting));
        }

        // 한 주문 안에서 같은 뼈대가 두 번 나오면("…충분하니 그렇게 해주시고요" 연타) 기계 티가 난다. 몇 번 다시 뽑는다.
        HashSet<string> usedTemplates = new HashSet<string>();
        for (int i = 0; i < scenario.changes.Count; i++)
        {
            DialogueScenarioRequest change = scenario.changes[i];
            int amountCode = change.kind == IngredientChangeKind.Remove ? -1 :
                             change.kind == IngredientChangeKind.Less ? -2 :
                             change.kind == IngredientChangeKind.Swap ? -3 : change.expressionAmount;
            string template = database.RandomTemplate(change.ingredient.ToString(), amountCode, scenario.difficulty, persona.personaId);
            for (int retry = 0; retry < 4 && usedTemplates.Contains(template); retry++)
                template = database.RandomTemplate(change.ingredient.ToString(), amountCode, scenario.difficulty, persona.personaId);
            usedTemplates.Add(template);
            bool connecting = i < scenario.changes.Count - 1 && UnityEngine.Random.value < 0.55f;
            AddIfNotEmpty(scenario.lines, Render(template, scenario, persona, change, connecting));
        }

        // 요청이 3개 이상이면 주문서가 넘치니(힌트+요청4+필러 = 9줄) 필러를 생략한다. 최대 8줄.
        // "면은 그대로" 뒤에 "다른 건 그대로"가 또 오면 겹치므로 유지 언급이 있으면 필러도 생략한다.
        if (!keepNoodle && scenario.changes.Count < 3 && UnityEngine.Random.value < fillerChance)
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
            text = text.Replace("{unit}", database.PickUnit(ingredientKey, change.expressionAmount));
        }

        string[] keys = { "give", "make", "want", "order", "good", "crave", "ask", "remove", "less" };

        // 두 문장짜리 뼈대("{ing} {amt} {give}. 많을수록 {good}.")는 마지막 문장만 연결형으로 만든다.
        // 앞 문장까지 연결형이 되면 "넣어주고. 많을수록 좋겠고,"처럼 문장 중간이 끊긴다.
        int split = connecting ? text.LastIndexOf(". ", StringComparison.Ordinal) : -1;
        string head = split < 0 ? string.Empty : text.Substring(0, split + 2);
        string tail = split < 0 ? text : text.Substring(split + 2);
        foreach (string key in keys)
        {
            head = head.Replace("{" + key + "}", database.PickSpeech(persona, key, false));
            tail = tail.Replace("{" + key + "}", database.PickSpeech(persona, key, connecting));
        }
        text = FixRieulParticle(head + tail);

        // 말투 어미가 !·?로 끝나면 뼈대의 마침표가 뒤에 겹친다("주세요!." "있죠?."). 마침표 쪽을 지운다.
        text = text.Replace("!.", "!").Replace("?.", "?");
        text = text.Replace("  ", " ").Trim();

        // 연결어미("~고" "~는데")로 끝난 줄은 다음 줄로 이어지는 말이라 마침표 대신 쉼표로 닫는다.
        // 말줄임(..)으로 끝난 건 그대로 둔다.
        if (connecting && text.EndsWith(".") && !text.EndsWith(".."))
            text = text.Substring(0, text.Length - 1) + ",";
        return text;
    }

    private Dictionary<IngredientType, int> GetBaseRecipe(RamenType ramenType)
    {
        return RecipeGenerator.GetBaseRecipe(ramenType);
    }

    private float KeepNoodleChance(int difficulty)
    {
        if (keepNoodleChance == null || keepNoodleChance.Length == 0) return 0f;
        return keepNoodleChance[Mathf.Clamp(difficulty - 1, 0, keepNoodleChance.Length - 1)];
    }

    /// <summary>기획서 5.3 — 시오·쇼유 기본면은 얇은 면, 돈코츠는 굵은 면.</summary>
    private static IngredientType DefaultNoodle(RamenType ramenType)
    {
        return ramenType == RamenType.Tonkotsu ? IngredientType.ThickNoodles : IngredientType.ThinNoodles;
    }

    private static IngredientType SwappedNoodle(RamenType ramenType)
    {
        return ramenType == RamenType.Tonkotsu ? IngredientType.ThinNoodles : IngredientType.ThickNoodles;
    }

    /// <summary>
    /// ㄹ 받침 뒤의 "으로"를 "로"로 고친다("면발으로" → "면발로"). 슬롯에 어떤 낱말이 올지 뼈대는 모르므로
    /// 뼈대에는 "으로"라고 적어 두고 여기서 맞춘다. ㄹ 받침 뒤에 "으로"가 오는 한국어는 없어 통째로 바꿔도 안전하다.
    /// </summary>
    private static string FixRieulParticle(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && text[i] == '으' && i + 1 < text.Length && text[i + 1] == '로')
            {
                int code = text[i - 1] - 0xAC00;
                if (code >= 0 && code < 11172 && code % 28 == 8) continue; // 받침 ㄹ → "으" 생략
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 그 날 손님이 시킬 수 있는 라멘. 해금 표는 <see cref="IngredientUnlock"/> 한 곳에만 둔다 —
    /// 조리 화면의 재료통도 같은 표를 보고 잠근다.
    /// </summary>
    private static RamenType GetRandomRamen(int currentDay)
    {
        int unlocked = IngredientUnlock.UnlockedRamenCount(currentDay);
        return IngredientUnlock.RamenOrder[UnityEngine.Random.Range(0, unlocked)];
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
