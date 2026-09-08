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
    public int amount;
    public int difficulty;
    public string template;
}

[Serializable]
public class KeywordRow
{
    public string type;
    public string enumName;
    public string value;
}
