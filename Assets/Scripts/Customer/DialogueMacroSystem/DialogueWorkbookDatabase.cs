using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueWorkbookDatabase
{
    private readonly DialogueDb db;

    public DialogueWorkbookDatabase(TextAsset jsonAsset)
    {
        if (jsonAsset == null)
            throw new ArgumentNullException(nameof(jsonAsset), "DialogueDB.json TextAsset가 필요합니다.");

        db = JsonUtility.FromJson<DialogueDb>(jsonAsset.text);
        if (db == null || db.personas == null || db.personas.Length == 0)
            throw new InvalidOperationException("DialogueDB.json을 읽지 못했거나 persona 데이터가 없습니다.");
    }

    /// <summary>
    /// 그림이 없는 말투는 뽑지 않는다. 지금은 장난꾸러기(Joker) 하나뿐이다 —
    /// 손님 그림 14장이 나머지 말투와 1:1로 맞고 이 말투만 짝이 없다.
    /// 데이터는 그대로 두고 뽑기에서만 뺀다. 그림이 들어오면 이 목록에서 지우면 된다.
    /// </summary>
    private static readonly string[] DisabledPersonaIds = { "Joker" };

    /// <summary>
    /// 최근에 나온 말투는 다시 뽑지 않는다. 이만큼을 기억해 둔다.
    ///
    /// 하루 손님이 가장 많은 날이 8 명이라(DayManager.CUSTOMER_COUNT_LATE) 8 을 기억하면
    /// 같은 하루에 같은 말투가 두 번 나오는 일이 없다. 하루가 바뀌는 것을 따로 알려 받지
    /// 않아도 되도록 "오늘" 이 아니라 "최근 여덟" 으로 잡았다 — 날짜를 물어보려면
    /// 이 클래스가 DayManager 를 알아야 하는데, 대사 데이터가 진행 상황까지 알 이유가 없다.
    ///
    /// 쓸 수 있는 말투가 14 종이라 여덟을 빼도 여섯이 남는다.
    /// </summary>
    private const int RecentPersonaMemory = 8;

    private readonly List<string> recentPersonas = new List<string>();

    /// <summary>
    /// 이 클래스를 거치지 않고 나온 손님의 말투를 최근 목록에 얹는다.
    ///
    /// 튜토리얼 손님이 그렇다. TutorialManager.BuildScenario 가 시나리오를 직접 만들어서
    /// RandomPersona 를 안 타는데, 그러면 바로 다음 손님이 같은 말투로 나올 수 있다.
    /// 실제로 1일차 첫 손님이 튜토리얼 손님과 똑같이 생겨 나왔다.
    /// </summary>
    public void NotePersonaUsed(string personaId)
    {
        if (string.IsNullOrEmpty(personaId)) return;

        // 같은 것을 두 번 얹으면 기억 여덟 칸 중 둘을 혼자 먹는다.
        if (recentPersonas.Contains(personaId)) return;

        recentPersonas.Add(personaId);
        if (recentPersonas.Count > RecentPersonaMemory) recentPersonas.RemoveAt(0);
    }

    public PersonaRow RandomPersona()
    {
        var usable = new List<PersonaRow>();
        foreach (PersonaRow row in db.personas)
        {
            if (Array.IndexOf(DisabledPersonaIds, row.personaId) < 0) usable.Add(row);
        }

        // 최근에 안 나온 말투부터 고른다. 남는 것이 없으면 기억을 접고 그냥 뽑는다 —
        // 말투가 줄어든 날에도 손님은 나와야 한다.
        var fresh = new List<PersonaRow>();
        foreach (PersonaRow row in usable)
        {
            if (!recentPersonas.Contains(row.personaId)) fresh.Add(row);
        }

        if (fresh.Count > 0) usable = fresh;

        // 전부 빠져 버렸으면 막지 않는다. 손님이 아예 안 나오는 것보다 낫다.
        PersonaRow picked = usable.Count > 0 ? Pick(usable.ToArray()) : Pick(db.personas);
        if (picked == null) return null;

        recentPersonas.Add(picked.personaId);
        if (recentPersonas.Count > RecentPersonaMemory) recentPersonas.RemoveAt(0);

        return picked;
    }

    public RamenRow GetRamen(RamenType ramenType)
    {
        string key = ramenType.ToString();
        for (int i = 0; i < db.ramens.Length; i++)
            if (db.ramens[i].ramenType == key) return db.ramens[i];
        throw new KeyNotFoundException("라멘 데이터 없음: " + key);
    }

    public string RandomOpener(string personaId) => RandomPersonaText(db.openers, personaId);
    public string RandomCloser(string personaId) => RandomPersonaText(db.closers, personaId);
    public string RandomFiller() => Pick(db.fillers);

    public HintRow RandomHint(RamenType ramenType, int difficulty, string personaId)
    {
        List<HintRow> candidates = new List<HintRow>();
        string ramenKey = ramenType.ToString();

        foreach (HintRow row in db.hints)
        {
            if (row.ramenType != ramenKey || row.difficulty != difficulty) continue;
            if (Contains(row.excludePersona, personaId)) continue;
            candidates.Add(row);
        }
        return candidates.Count == 0 ? null : Pick(candidates);
    }

    /// <summary>
    /// 뼈대 하나를 고른다. 재료 전용 행 → 공용("Any") 행 순으로 모으고, 말투 전용 행(persona 칸)은
    /// 공용 행에 묻히지 않도록 두 번 넣는다. difficulty 0 인 행은 모든 난이도에 걸린다.
    /// </summary>
    public string RandomTemplate(string ingredient, int amountCode, int difficulty, string personaId)
    {
        List<string> exact = FindTemplates(ingredient, amountCode, difficulty, personaId);
        List<string> common = FindTemplates("Any", amountCode, difficulty, personaId);
        exact.AddRange(common);

        // 단위 표현("한 점 더")은 기획서 5.2·19.4의 지원 유형이라 거르지 않는다. {unit}은 Render에서 치환된다.
        if (exact.Count > 0) return Pick(exact);

        // 같은 난이도에 문장이 없으면 같은 요청량의 다른 난이도 문장으로 폴백한다.
        List<string> fallback = new List<string>();
        foreach (TemplateRow row in db.templates)
            if ((row.ingredient == ingredient || row.ingredient == "Any") && row.amount == amountCode
                && PersonaMatches(row, personaId))
                fallback.Add(row.template);

        if (fallback.Count == 0)
            return amountCode == -1 ? "{ing} {remove}." :
                   amountCode == -2 ? "{ing} {less}." :
                   amountCode == -3 ? "면은 {ing}으로 바꿔서 {give}." :
                   amountCode == -4 ? "면은 {ing} 그대로 {make}." : "{ing} {amt} {give}.";
        return Pick(fallback);
    }

    public string RandomKeyword(string type, string enumName)
    {
        List<string> values = new List<string>();
        foreach (KeywordRow row in db.keywords)
            if (row.type == type && row.enumName == enumName) values.Add(row.value);
        return values.Count == 0 ? enumName : Pick(values);
    }

    public HashSet<IngredientType> GetConflicts(string hint)
    {
        HashSet<IngredientType> result = new HashSet<IngredientType>();
        if (string.IsNullOrEmpty(hint)) return result;

        foreach (KeywordRow row in db.keywords)
        {
            IngredientType ingredient;
            if (row.type == "Conflict" && hint.Contains(row.value) &&
                Enum.TryParse(row.enumName, out ingredient))
                result.Add(ingredient);
        }
        return result;
    }

    public string PickAmountWord(PersonaRow persona, int amount)
    {
        if (amount <= 1) return Pick(persona.amt1);
        if (amount == 2) return Pick(persona.amt2);
        return Pick(persona.amt3);
    }

    /// <summary>재료별 수량 단위. 1은 unit1, 2는 unit2, 3 이상은 unit3. 단위가 없으면 빈 문자열.</summary>
    public string PickUnit(string ingredient, int amount)
    {
        if (db.units == null) return string.Empty;
        foreach (UnitRow row in db.units)
        {
            if (row.ingredient != ingredient) continue;
            return amount <= 1 ? row.unit1 : amount == 2 ? row.unit2 : row.unit3;
        }
        return string.Empty;
    }

    public string PickSpeech(PersonaRow persona, string key, bool connecting)
    {
        SpeechTokenRow[] rows = connecting ? persona.connecting : persona.terminal;
        foreach (SpeechTokenRow row in rows)
            if (row.key == key && row.values != null && row.values.Length > 0) return Pick(row.values);

        if (connecting) return PickSpeech(persona, key, false);
        return string.Empty;
    }

    private List<string> FindTemplates(string ingredient, int amount, int difficulty, string personaId)
    {
        List<string> result = new List<string>();
        foreach (TemplateRow row in db.templates)
        {
            if (row.ingredient != ingredient || row.amount != amount) continue;
            if (row.difficulty != difficulty && row.difficulty != 0) continue;
            if (!PersonaMatches(row, personaId)) continue;

            result.Add(row.template);
            if (!string.IsNullOrEmpty(row.persona)) result.Add(row.template);
        }
        return result;
    }

    /// <summary>persona 칸이 비어 있으면 공용, 적혀 있으면 그 말투에만.</summary>
    private static bool PersonaMatches(TemplateRow row, string personaId)
    {
        return string.IsNullOrEmpty(row.persona) || row.persona == personaId;
    }

    private static bool Contains(string[] values, string target)
    {
        if (values == null) return false;
        foreach (string value in values) if (value == target) return true;
        return false;
    }

    private static string RandomPersonaText(TextRow[] rows, string personaId)
    {
        List<string> values = new List<string>();
        foreach (TextRow row in rows) if (row.personaId == personaId) values.Add(row.text);
        return values.Count == 0 ? string.Empty : Pick(values);
    }

    private static T Pick<T>(IList<T> values) => values[UnityEngine.Random.Range(0, values.Count)];
}
