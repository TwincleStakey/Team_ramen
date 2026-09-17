using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 그릇이 주문과 어긋난 방향. **재료 이름은 남기지 않는다** — 손님이 "멘마가 없네요"
/// 라고 짚어 주면 정답을 알려 주는 꼴이라, 주문서와 그릇을 견주어 보는 일이 사라진다.
///
/// <see cref="Swapped"/> 가 따로 있는 까닭: 주문이 김 2인데 파 2를 넣으면 빠진 쪽 2 ·
/// 많은 쪽 2 로 정확히 같아진다. 둘이 비슷하게 나오는 그릇은 "적다/많다"가 아니라
/// **다른 것을 넣은** 그릇이고, 실제로 제일 흔한 실수다.
/// </summary>
public enum ReactionMiss
{
    /// <summary>토핑·조미료가 주문과 똑같다.</summary>
    None,

    /// <summary>빠진 쪽이 세다.</summary>
    Missing,

    /// <summary>많이 들어간 쪽이 세다.</summary>
    Excess,

    /// <summary>빠진 것과 많은 것이 비슷하다.</summary>
    Swapped,

    /// <summary>3대 요소(타래·육수·면)가 어긋났다. 방향을 따질 그릇이 아니다.</summary>
    Wrong
}

/// <summary>
/// 손님이 라멘을 다 먹고 내놓는 한마디. `Resources/ReactionLines.json` 에서 읽는다.
///
/// 이모티콘과 같은 눈금을 쓴다 — 90 이상 웃음, 70 이상 무표정, 그 아래 화남.
/// 표정과 말이 어긋나면(웃는 얼굴로 "이건 좀 아닌데요") 손님이 고장 난 것처럼 보인다.
///
/// 말투 15종마다 칸이 따로 있다. 주문할 때는 경상도 말을 쓰다가 먹고 나서 표준어로
/// 돌아오면 다른 사람이 앉은 것 같다. 칸이 비어 있으면 아래 기본 대사가 나온다.
///
/// 한 번 뽑은 말은 손님이 갈 때까지 쥐고 있는다. 컷신 마지막 컷과 결과창이 각자 뽑으면
/// 같은 손님이 두 마디를 다르게 말한다. 결과창을 닫을 때 <see cref="Clear"/> 로 놓는다.
/// </summary>
public static class ReactionLines
{
    /// <summary>Resources 밑 경로. 확장자는 빼고 적는다.</summary>
    private const string ResourcePath = "ReactionLines";

    // 표에 그 말투가 없거나 칸이 비었을 때 쓰는 말.
    private const string DefaultGood = "잘 먹었습니다!";
    private const string DefaultNormal = "잘 먹었습니다.";
    private const string DefaultBad = "제가 주문한 라멘이 아닌데요?";

    /// <summary>그 말투에 받아 드는 말이 없을 때 쓰는 한 마디.</summary>
    private const string DefaultServed = "잘 먹겠습니다.";

    private static ReactionLineTable table;
    private static bool loaded;

    /// <summary>이번 손님에게 뽑아 둔 말. 손님이 갈 때까지 그대로 쓴다.</summary>
    private static string picked;

    /// <summary>
    /// 이번 손님의 반응. 정확도는 0~100 이다.
    /// 처음 물어본 쪽이 뽑고, 뒤에 물어보는 쪽은 같은 말을 받는다.
    /// </summary>
    public static string For(float accuracy)
    {
        if (string.IsNullOrEmpty(picked)) picked = Roll(accuracy);

        return picked;
    }

    /// <summary>손님이 갔다. 다음 손님은 새로 뽑는다.</summary>
    public static void Clear()
    {
        picked = null;
        miss = ReactionMiss.None;
    }

    /// <summary>이번 그릇이 어긋난 방향. 제출할 때 <see cref="NoteBowl"/> 가 적어 둔다.</summary>
    private static ReactionMiss miss;

    // 그 말투에 방향 칸이 없을 때 쓰는 말. 재료 이름은 넣지 않는다.
    private const string DefaultMissing = "뭔가 좀 빠진 것 같은데요…";
    private const string DefaultExcess = "뭐가 좀 많이 들어간 것 같은데요…";
    private const string DefaultSwapped = "제가 시킨 거랑 좀 다른 것 같은데요…";

    /// <summary>
    /// 제출한 그릇이 주문과 어떻게 어긋났는지 적어 둔다. 결과창이 <see cref="Feedback"/> 로 읽는다.
    ///
    /// 그릇은 제출 직후에 비워지므로(Bowl.Submit) 여기서 세어 두지 않으면 볼 수 없다.
    /// 3대 요소는 빼고 센다 — 그쪽이 틀리면 방향이 아니라 아예 다른 음식이라
    /// <paramref name="coreFailed"/> 로 따로 받는다.
    /// </summary>
    public static void NoteBowl(Dictionary<IngredientType, int> target,
                                Dictionary<IngredientType, int> submitted, bool coreFailed)
    {
        if (coreFailed)
        {
            miss = ReactionMiss.Wrong;
            return;
        }

        int missing = 0;
        int excess = 0;

        // enum 을 그대로 훑는다. 토핑 목록을 따로 적어 두면 재료가 늘 때 여기만 옛것으로 남는다.
        foreach (IngredientType type in System.Enum.GetValues(typeof(IngredientType)))
        {
            if (IsCore(type)) continue;

            int want = target != null && target.TryGetValue(type, out int w) ? w : 0;
            int got = submitted != null && submitted.TryGetValue(type, out int g) ? g : 0;

            if (got < want) missing += want - got;
            else excess += got - want;
        }

        miss = Judge(missing, excess);
    }

    /// <summary>
    /// 빠진 수와 넘친 수로 방향을 고른다.
    ///
    /// 한쪽이 다른 쪽의 두 배가 안 되면 "다른 것을 넣었다"로 읽는다. 기울어진 그릇
    /// (빠짐 3 · 넘침 1)까지 뭉뚱그리지 않으려고 두 배를 눈금으로 뒀다.
    /// </summary>
    private static ReactionMiss Judge(int missing, int excess)
    {
        if (missing == 0 && excess == 0) return ReactionMiss.None;
        if (excess == 0) return ReactionMiss.Missing;
        if (missing == 0) return ReactionMiss.Excess;

        if (missing >= excess * 2) return ReactionMiss.Missing;
        if (excess >= missing * 2) return ReactionMiss.Excess;

        return ReactionMiss.Swapped;
    }

    /// <summary>3대 요소(타래·육수·면)인가. 이쪽은 방향을 세지 않는다.</summary>
    private static bool IsCore(IngredientType type)
    {
        return type == IngredientType.ShioTare
            || type == IngredientType.ShoyuTare
            || type == IngredientType.TonkotsuBase
            || type == IngredientType.Broth
            || type == IngredientType.ThickNoodles
            || type == IngredientType.ThinNoodles;
    }

    /// <summary>
    /// 결과창에 뜨는 한마디. 정확도 구간이 아니라 **어긋난 방향**으로 고른다.
    /// 그릇과 주문서를 나란히 놓고 보는 자리라, 어느 쪽으로 틀렸는지가 소감보다 쓸모 있다.
    ///
    /// 딱 맞은 그릇과 3대 요소가 어긋난 그릇은 방향이 없다. 그때는 예전처럼
    /// 정확도 구간 대사(<see cref="For"/>)로 돌아간다 — 100% 는 칭찬, 탈락은 "이거 제 라멘 아닌데요".
    /// </summary>
    public static string Feedback(float accuracy)
    {
        if (miss == ReactionMiss.None || miss == ReactionMiss.Wrong) return For(accuracy);

        Load();
        ReactionLineRow row = Row(CurrentPersonaId());

        string[] pool = row == null ? null
                      : miss == ReactionMiss.Missing ? row.missing
                      : miss == ReactionMiss.Excess ? row.excess
                      : row.swapped;

        if (pool != null && pool.Length > 0) return pool[Random.Range(0, pool.Length)];

        return miss == ReactionMiss.Missing ? DefaultMissing
             : miss == ReactionMiss.Excess ? DefaultExcess
             : DefaultSwapped;
    }

    /// <summary>
    /// 그릇을 받고 먹기 전에 하는 한 마디.
    ///
    /// 먹은 뒤 반응과 달리 정확도를 보지 않는다 — 아직 맛을 안 봤다.
    /// <see cref="picked"/> 에도 담지 않는다. 그건 "이 손님의 소감" 한 줄을 컷신과
    /// 결과창이 나눠 쓰려고 쥐고 있는 것이라, 여기서 덮으면 소감 자리에 인사말이 나온다.
    ///
    /// 그 말투의 칸이 비어 있으면 기본 한 마디로 돌아간다.
    /// </summary>
    public static string Served()
    {
        Load();
        ReactionLineRow row = Row(CurrentPersonaId());

        string[] pool = row != null ? row.served : null;
        if (pool == null || pool.Length == 0) return DefaultServed;

        return pool[Random.Range(0, pool.Length)];
    }

    /// <summary>
    /// 컷신 말풍선에만 띄우는 긴 소감. 결과창은 이걸 쓰지 않고 <see cref="For"/> 의 짧은 말을 쓴다.
    /// 구간은 표정과 같되 완벽(<see cref="RamenCalculator.PERFECT_ACCURACY"/> 이상)만 따로 둔다 —
    /// "주문한 대로 하나도 안 빠졌다"는 말은 92% 에서 하면 거짓말이 된다.
    /// 그 말투의 칸이 비어 있으면 null — 부르는 쪽이 For 로 돌아간다.
    /// </summary>
    public static string Cutscene(float accuracy)
    {
        Load();
        ReactionLineRow row = Row(CurrentPersonaId());
        if (row == null) return null;

        string[] pool = accuracy >= RamenCalculator.PERFECT_ACCURACY ? row.perfect
                      : accuracy >= 90f ? row.goodLong
                      : accuracy >= 70f ? row.normalLong
                      : row.badLong;

        return pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null;
    }

    /// <summary>말투와 구간을 찾아 한 줄 고른다.</summary>
    private static string Roll(float accuracy)
    {
        Load();

        int tier = accuracy >= 90f ? 0 : accuracy >= 70f ? 1 : 2;
        string[] pool = Pool(CurrentPersonaId(), tier);

        if (pool != null && pool.Length > 0) return pool[Random.Range(0, pool.Length)];

        return tier == 0 ? DefaultGood : tier == 1 ? DefaultNormal : DefaultBad;
    }

    /// <summary>그 말투의 그 구간 칸. 못 찾으면 null 이다.</summary>
    private static string[] Pool(string personaId, int tier)
    {
        ReactionLineRow row = Row(personaId);
        if (row == null) return null;

        return tier == 0 ? row.good : tier == 1 ? row.normal : row.bad;
    }

    /// <summary>그 말투의 줄. 표가 없거나 말투를 못 찾으면 null 이다.</summary>
    private static ReactionLineRow Row(string personaId)
    {
        if (table == null || table.lines == null || string.IsNullOrEmpty(personaId)) return null;

        foreach (ReactionLineRow row in table.lines)
        {
            if (row != null && row.personaId == personaId) return row;
        }

        return null;
    }

    /// <summary>
    /// 지금 손님의 말투. 대사를 만드는 쪽(B의 OrderManager)에서 읽기만 한다.
    /// 손님마다 한 번씩만 부르는 자리라 찾는 값은 신경 쓰지 않는다.
    /// </summary>
    private static string CurrentPersonaId()
    {
        var manager = Object.FindFirstObjectByType<OrderManager>();
        if (manager == null || manager.CurrentScenario == null) return null;

        return manager.CurrentScenario.personaId;
    }

    /// <summary>표를 한 번만 읽어 둔다. 파일이 없어도 게임은 돌아야 하므로 조용히 넘어간다.</summary>
    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null) return;

        table = JsonUtility.FromJson<ReactionLineTable>(asset.text);
    }
}

/// <summary>ReactionLines.json 의 겉모양. JsonUtility 가 읽을 수 있는 것만 쓴다.</summary>
[System.Serializable]
public class ReactionLineTable
{
    public ReactionLineRow[] lines;
}

/// <summary>말투 하나. 칸 이름은 json 의 키와 글자까지 같아야 한다.</summary>
[System.Serializable]
public class ReactionLineRow
{
    public string personaId;
    public string name;

    /// <summary>그릇을 받고 먹기 전에 하는 한 마디. 먹은 뒤 반응과 달리 정확도와 무관하다.</summary>
    public string[] served;

    public string[] good;
    public string[] normal;
    public string[] bad;

    // 결과창 한마디. 정확도가 아니라 어긋난 방향으로 고른다(ReactionLines.Feedback).
    public string[] missing;   // 빠진 쪽이 세다
    public string[] excess;    // 많이 들어간 쪽이 세다
    public string[] swapped;   // 빠진 것과 많은 것이 비슷하다 — 다른 걸 넣었다
    // 컷신 말풍선에만 띄우는 긴 소감. 비우면 위의 짧은 말이 나온다.
    public string[] perfect;     // PERFECT_ACCURACY 이상
    public string[] goodLong;    // 90 이상
    public string[] normalLong;  // 70 이상
    public string[] badLong;     // 그 미만
}
