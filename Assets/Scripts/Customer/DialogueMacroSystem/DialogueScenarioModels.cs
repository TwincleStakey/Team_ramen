using System;
using System.Collections.Generic;

public enum IngredientChangeKind
{
    Add,
    Remove,
    Less,
    Swap
}

[Serializable]
public class DialogueScenarioRequest
{
    public IngredientType ingredient;
    public IngredientChangeKind kind;
    public int recipeDelta;
    public int expressionAmount;
}

[Serializable]
public class DialogueScenario
{
    public string personaId;
    public string personaName;
    public int difficulty;
    public CustomerOrder order;
    public List<DialogueScenarioRequest> changes = new List<DialogueScenarioRequest>();
    public Dictionary<IngredientType, int> baseRecipe;
    public Dictionary<IngredientType, int> targetRecipe;
    public List<string> lines = new List<string>();

    public string Dialogue => string.Join("\n", lines);
}

[Serializable]
public class DialogueDb
{
    public PersonaRow[] personas;
    public RamenRow[] ramens;
    public TextRow[] openers;
    public TextRow[] closers;
    public HintRow[] hints;
    public TemplateRow[] templates;
    public KeywordRow[] keywords;
    public string[] fillers;
    public UnitRow[] units;
}

[Serializable]
public class PersonaRow
{
    public string personaId;
    public string name;
    public string memo;
    public string[] amt1;
    public string[] amt2;
    public string[] amt3;
    public SpeechTokenRow[] terminal;
    public SpeechTokenRow[] connecting;
}

[Serializable]
public class SpeechTokenRow
{
    public string key;
    public string[] values;
}

[Serializable]
public class RamenRow
{
    public string ramenType;
    public string[] addable;
    public string[] baseIngredients;
}

[Serializable]
public class TextRow
{
    public string personaId;
    public string text;
}

[Serializable]
public class HintRow
{
    public string ramenType;
    public int difficulty;
    public string template;
    public string[] excludePersona;
}

[Serializable]
public class TemplateRow
{
    public string ingredient;
    public int amount;       // 1~3 추가량, -1 제거, -2 감소, -3 면 교체, -4 기본면 유지 언급
    public int difficulty;   // 1~3. 0 이면 모든 난이도에 걸린다
    public string template;
    /// <summary>비어 있으면 공용. 말투 아이디가 적혀 있으면 그 말투에만 나온다.</summary>
    public string persona;
}

/// <summary>재료별 수량 단위. {unit} 슬롯에 들어간다 (한 점 / 두 점 / 세 점).</summary>
[Serializable]
public class UnitRow
{
    public string ingredient;
    public string unit1;
    public string unit2;
    public string unit3;
}

[Serializable]
public class KeywordRow
{
    public string type;
    public string enumName;
    public string value;
}
