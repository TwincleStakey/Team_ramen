using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 저장·이어하기 (기획서 13.1). 일차·손님 순번·현재 주문·그릇 내용·누적 결과를 복원한다.
///
/// 핵심은 <b>주문을 다시 만들지 않는 것</b>이다. 다시 만들면 무작위가 새로 굴러
/// 손님이 시킨 것과 다른 라멘을 채점하게 된다.
///
/// 그런데 정답 레시피는 저장할 필요가 없다. B의 RecipeGenerator가 순수 함수라
/// "기본 레시피 + 주문 요청"이 언제 계산해도 같은 값을 낸다. 그래서 주문 원본만 저장하고
/// 정답은 불러올 때 다시 계산한다. 덕분에 Dictionary를 직렬화할 일이 없어진다
/// (Unity의 JsonUtility는 Dictionary를 못 다룬다).
///
/// B 파일(Assets/Scripts/Customer/)은 고치지 않는다. 진행 상태를 되돌려 놓아야 하는데
/// setter가 없으므로 리플렉션으로 private 필드에 직접 넣는다.
/// </summary>
public static class SaveSystem
{
    private const string FileName = "ramen_save.json";

    /// <summary>저장 형식이 바뀌면 올린다. 옛 파일은 버리고 새로 시작한다.</summary>
    private const int CurrentVersion = 1;

    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// 그릇을 되채우는 동안 켜진다. Bowl.TryAdd가 이걸 보고 저장을 건너뛴다.
    /// 안 그러면 복원하면서 매 재료마다 저장을 다시 쓰게 된다.
    /// </summary>
    public static bool Restoring { get; private set; }

    private static string Path
    {
        get { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
    }

    public static bool Exists
    {
        get { return File.Exists(Path); }
    }

    // ── 파일 ─────────────────────────────────────────────────────

    public static void Write(SaveData data)
    {
        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception e)
        {
            // 저장 실패로 게임이 멈추면 안 된다. 알리고 계속 진행한다.
            Debug.LogWarning("[저장] 실패: " + e.Message);
        }
    }

    /// <summary>읽을 게 없거나 형식이 다르면 null. 부르는 쪽은 새 게임으로 시작하면 된다.</summary>
    public static SaveData Read()
    {
        if (!Exists) return null;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
            if (data == null || data.version != CurrentVersion)
            {
                Debug.Log("[저장] 형식이 달라 옛 저장을 버립니다.");
                return null;
            }
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[저장] 읽기 실패: " + e.Message);
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (Exists) File.Delete(Path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[저장] 삭제 실패: " + e.Message);
        }
    }

    // ── 담기 ─────────────────────────────────────────────────────

    /// <summary>지금 상태를 통째로 긁어 온다. 씬에 없는 것은 조용히 건너뛴다.</summary>
    public static SaveData Capture(DayManager day, OrderManager order, RamenCalculator calc, Bowl bowl,
                                   int hour, int totalRevenue, int servedCount, float accuracySum, int perfectCount)
    {
        var data = new SaveData();
        data.version = CurrentVersion;

        data.hour = hour;
        data.totalRevenue = totalRevenue;
        data.servedCount = servedCount;
        data.accuracySum = accuracySum;
        data.perfectCount = perfectCount;

        if (day != null)
        {
            data.day = day.CurrentDay;
            data.customerIndex = day.CurrentCustomerCount;
        }

        // 당일 누계. 하루 마감 정산 팝업이 이 값으로 평균 정확도를 낸다.
        if (calc != null)
        {
            data.todayProfit = GetInt(calc, "todayTotalProfit");
            data.todayAccuracySum = GetFloat(calc, "todayTotalAccuracy");
            data.todayServed = GetInt(calc, "todayServedCount");
        }

        CaptureOrder(data, order);
        CaptureBowl(data, bowl);
        return data;
    }

    private static void CaptureOrder(SaveData data, OrderManager order)
    {
        DialogueScenario scenario = order != null ? order.CurrentScenario : null;
        if (scenario == null || scenario.order == null) return;

        data.hasOrder = true;
        data.ramenType = (int)scenario.order.ramenType;
        data.personaId = scenario.personaId;
        data.personaName = scenario.personaName;
        data.difficulty = scenario.difficulty;
        data.lines = scenario.lines != null ? scenario.lines.ToArray() : new string[0];

        // 요청 목록이 곧 주문의 알맹이다. 정답 레시피는 이걸로 다시 계산한다.
        List<IngredientRequest> requests = scenario.order.requests;
        int n = requests != null ? requests.Count : 0;
        data.requestIngredients = new int[n];
        data.requestAmounts = new int[n];
        for (int i = 0; i < n; i++)
        {
            data.requestIngredients[i] = (int)requests[i].ingredient;
            data.requestAmounts[i] = requests[i].amount;
        }

        // changes는 대사를 만들 때 쓴 기록이다. 대사 자체는 lines로 저장되지만,
        // 결과창이 나중에 이걸 읽을 수 있어 같이 남긴다.
        int m = scenario.changes != null ? scenario.changes.Count : 0;
        data.changeIngredients = new int[m];
        data.changeKinds = new int[m];
        data.changeDeltas = new int[m];
        data.changeAmounts = new int[m];
        for (int i = 0; i < m; i++)
        {
            DialogueScenarioRequest c = scenario.changes[i];
            data.changeIngredients[i] = (int)c.ingredient;
            data.changeKinds[i] = (int)c.kind;
            data.changeDeltas[i] = c.recipeDelta;
            data.changeAmounts[i] = c.expressionAmount;
        }
    }

    private static void CaptureBowl(SaveData data, Bowl bowl)
    {
        if (bowl == null) return;

        Flatten(GetDict(bowl, "bowl"), out data.bowlIngredients, out data.bowlCounts);
        Flatten(GetDict(bowl, "discarded"), out data.discardIngredients, out data.discardCounts);
    }

    // ── 되돌리기 ─────────────────────────────────────────────────

    /// <summary>
    /// 저장을 씬에 다시 얹는다. DayManager가 하루를 새로 시작하기 전에 불러야 한다.
    /// 부르는 쪽(GameManager.Awake)이 autoStartFirstDay를 먼저 꺼야 주문이 새로 안 만들어진다.
    /// </summary>
    public static void Apply(SaveData data, DayManager day, OrderManager order, RamenCalculator calc, Bowl bowl)
    {
        if (data == null) return;

        if (day != null)
        {
            SetField(day, "currentDay", data.day);
            SetField(day, "currentCustomerCount", data.customerIndex);
        }

        if (calc != null)
        {
            SetField(calc, "todayTotalProfit", data.todayProfit);
            SetField(calc, "todayTotalAccuracy", data.todayAccuracySum);
            SetField(calc, "todayServedCount", data.todayServed);
        }

        ApplyOrder(data, order);

        Restoring = true;
        try { ApplyBowl(data, bowl); }
        finally { Restoring = false; }   // 도중에 예외가 나도 반드시 꺼야 이후 저장이 계속 동작한다
    }

    private static void ApplyOrder(SaveData data, OrderManager order)
    {
        if (!data.hasOrder || order == null) return;

        var restored = new CustomerOrder();
        restored.ramenType = (RamenType)data.ramenType;
        restored.requests = new List<IngredientRequest>();

        int n = data.requestIngredients != null ? data.requestIngredients.Length : 0;
        for (int i = 0; i < n; i++)
        {
            restored.requests.Add(new IngredientRequest((IngredientType)data.requestIngredients[i],
                                                        data.requestAmounts[i]));
        }

        var scenario = new DialogueScenario();
        scenario.personaId = data.personaId;
        scenario.personaName = data.personaName;
        scenario.difficulty = data.difficulty;
        scenario.order = restored;
        scenario.lines = new List<string>(data.lines ?? new string[0]);

        int m = data.changeIngredients != null ? data.changeIngredients.Length : 0;
        scenario.changes = new List<DialogueScenarioRequest>(m);
        for (int i = 0; i < m; i++)
        {
            scenario.changes.Add(new DialogueScenarioRequest
            {
                ingredient = (IngredientType)data.changeIngredients[i],
                kind = (IngredientChangeKind)data.changeKinds[i],
                recipeDelta = data.changeDeltas[i],
                expressionAmount = data.changeAmounts[i]
            });
        }

        // 여기가 "주문 재생성 금지"의 알맹이다. 무작위를 다시 굴리지 않고,
        // 저장된 주문에서 정답을 그대로 다시 계산한다.
        scenario.baseRecipe = RecipeGenerator.GetBaseRecipe(restored.ramenType);
        scenario.targetRecipe = new RecipeGenerator().GenerateTargetRecipe(restored);

        SetField(order, "currentScenario", scenario);
    }

    /// <summary>
    /// 그릇은 딕셔너리를 직접 밀어 넣지 않고 <see cref="Bowl.TryAdd"/>로 다시 담는다.
    /// 그래야 그릇 그림과 재료 아이콘이 같이 살아난다. 딕셔너리만 채우면 숫자는 맞는데
    /// 화면은 빈 그릇으로 남는다.
    ///
    /// 담는 순서가 중요하다. Bowl은 타래 → 육수 → 면 순서만 받는다.
    /// </summary>
    private static void ApplyBowl(SaveData data, Bowl bowl)
    {
        if (bowl == null || data.bowlIngredients == null) return;

        Dictionary<IngredientType, int> contents = Unflatten(data.bowlIngredients, data.bowlCounts);
        Dictionary<IngredientType, Sprite> icons = CollectIcons();

        foreach (IngredientType type in OrderedForRefill(contents.Keys))
        {
            for (int i = 0; i < contents[type]; i++)
            {
                Sprite icon;
                icons.TryGetValue(type, out icon);
                if (!bowl.TryAdd(type, icon))
                {
                    Debug.LogWarning("[저장] 그릇 복원 중 거부됨: " + type + ". 남은 수량은 건너뜁니다.");
                    break;
                }
            }
        }

        // 폐기 기록은 화면에 안 나오고 제출할 때 함께 넘어가기만 한다. 딕셔너리에 바로 넣는다.
        Dictionary<IngredientType, int> discarded = GetDict(bowl, "discarded");
        if (discarded != null)
        {
            discarded.Clear();
            foreach (var pair in Unflatten(data.discardIngredients, data.discardCounts))
            {
                discarded[pair.Key] = pair.Value;
            }
        }
    }

    /// <summary>타래 → 육수 → 면 → 나머지. Bowl의 투입 순서 규칙과 같은 차례다.</summary>
    private static List<IngredientType> OrderedForRefill(IEnumerable<IngredientType> types)
    {
        var sorted = new List<IngredientType>(types);
        sorted.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        return sorted;
    }

    private static int Rank(IngredientType type)
    {
        if (type == IngredientType.ShioTare || type == IngredientType.ShoyuTare
            || type == IngredientType.TonkotsuBase) return 0;
        if (type == IngredientType.Broth) return 1;
        if (type == IngredientType.ThickNoodles || type == IngredientType.ThinNoodles) return 2;
        return 3;
    }

    /// <summary>재료통에서 그릇용 그림을 모은다. 이게 없으면 복원된 토핑이 안 보인다.</summary>
    private static Dictionary<IngredientType, Sprite> CollectIcons()
    {
        var icons = new Dictionary<IngredientType, Sprite>();

        IngredientSlot[] slots = Object.FindObjectsByType<IngredientSlot>(FindObjectsSortMode.None);
        foreach (IngredientSlot slot in slots)
        {
            if (slot != null && slot.bowlSprite != null) icons[slot.type] = slot.bowlSprite;
        }
        return icons;
    }

    // ── 잔손질 ───────────────────────────────────────────────────

    private static void Flatten(Dictionary<IngredientType, int> dict, out int[] keys, out int[] values)
    {
        int n = dict != null ? dict.Count : 0;
        keys = new int[n];
        values = new int[n];
        if (dict == null) return;

        int i = 0;
        foreach (var pair in dict)
        {
            keys[i] = (int)pair.Key;
            values[i] = pair.Value;
            i++;
        }
    }

    private static Dictionary<IngredientType, int> Unflatten(int[] keys, int[] values)
    {
        var dict = new Dictionary<IngredientType, int>();
        if (keys == null || values == null) return dict;

        int n = Mathf.Min(keys.Length, values.Length);
        for (int i = 0; i < n; i++) dict[(IngredientType)keys[i]] = values[i];
        return dict;
    }

    private static Dictionary<IngredientType, int> GetDict(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        return info != null ? info.GetValue(target) as Dictionary<IngredientType, int> : null;
    }

    private static int GetInt(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        return info != null ? (int)info.GetValue(target) : 0;
    }

    private static float GetFloat(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        return info != null ? (float)info.GetValue(target) : 0f;
    }

    /// <summary>B 컴포넌트에는 setter가 없다. 필드 이름이 바뀌면 조용히 실패하므로 경고를 남긴다.</summary>
    private static void SetField(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, Private);
        if (info == null)
        {
            Debug.LogWarning("[저장] " + target.GetType().Name + "." + field
                             + " 를 찾지 못했습니다. B 코드에서 이름이 바뀌었는지 확인이 필요합니다.");
            return;
        }
        info.SetValue(target, value);
    }
}

/// <summary>
/// 저장 파일에 그대로 담기는 모양. JsonUtility가 다룰 수 있는 것만 쓴다
/// (기본형·문자열·배열. Dictionary와 프로퍼티는 안 된다).
/// </summary>
[System.Serializable]
public class SaveData
{
    public int version;

    // 진행
    public int day;
    public int customerIndex;
    public int hour;

    // 누적 결과
    public int totalRevenue;
    public int servedCount;
    public float accuracySum;
    public int perfectCount;

    // 당일 누계 (하루 마감 정산용)
    public int todayProfit;
    public float todayAccuracySum;
    public int todayServed;

    // 현재 주문. 정답 레시피는 담지 않는다 — 불러올 때 다시 계산한다.
    public bool hasOrder;
    public int ramenType;
    public string personaId;
    public string personaName;
    public int difficulty;
    public string[] lines;
    public int[] requestIngredients;
    public int[] requestAmounts;
    public int[] changeIngredients;
    public int[] changeKinds;
    public int[] changeDeltas;
    public int[] changeAmounts;

    // 그릇
    public int[] bowlIngredients;
    public int[] bowlCounts;
    public int[] discardIngredients;
    public int[] discardCounts;
}
