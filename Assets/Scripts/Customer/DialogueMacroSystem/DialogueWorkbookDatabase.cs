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

    public PersonaRow RandomPersona()
    {
        return Pick(db.personas);
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

    public string RandomTemplate(string ingredient, int amountCode, int difficulty)
    {
        List<string> exact = FindTemplates(ingredient, amountCode, difficulty);
        List<string> common = FindTemplates("Any", amountCode, difficulty);
        exact.AddRange(common);

        // 게임 요구사항: 정확한 횟수·단위가 등장하는 엑셀 템플릿은 사용하지 않는다.
        exact.RemoveAll(ContainsExactCountExpression);
        if (exact.Count > 0) return Pick(exact);

        // 같은 난이도에 안전한 문장이 없으면, 같은 요청량의 안전한 문장으로 폴백한다.
        List<string> fallback = new List<string>();
        foreach (TemplateRow row in db.templates)
            if ((row.ingredient == ingredient || row.ingredient == "Any") &&
                row.amount == amountCode && !ContainsExactCountExpression(row.template))
                fallback.Add(row.template);

        if (fallback.Count == 0)
            return amountCode == -1 ? "{ing} {remove}." :
                   amountCode == -2 ? "{ing} {less}." :
                   amountCode == -3 ? "면은 {ing}로 변경해{give}." : "{ing} {amt} {give}.";
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

    public string PickSpeech(PersonaRow persona, string key, bool connecting)
    {
        SpeechTokenRow[] rows = connecting ? persona.connecting : persona.terminal;
        foreach (SpeechTokenRow row in rows)
            if (row.key == key && row.values != null && row.values.Length > 0) return Pick(row.values);

        if (connecting) return PickSpeech(persona, key, false);
        return string.Empty;
    }

    private List<string> FindTemplates(string ingredient, int amount, int difficulty)
    {
        List<string> result = new List<string>();
        foreach (TemplateRow row in db.templates)
            if (row.ingredient == ingredient && row.amount == amount && row.difficulty == difficulty)
                result.Add(row.template);
        return result;
    }

    private static bool ContainsExactCountExpression(string text)
    {
        return text.Contains("{unit}") || text.Contains("하나 더") ||
               text.Contains("두 개") || text.Contains("한 장") || text.Contains("1번") ||
               text.Contains("2번") || text.Contains("3번") || text.Contains("회분");
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
