using UnityEngine;

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
    public string[] good;
    public string[] normal;
    public string[] bad;
    // 컷신 말풍선에만 띄우는 긴 소감. 비우면 위의 짧은 말이 나온다.
    public string[] perfect;     // PERFECT_ACCURACY 이상
    public string[] goodLong;    // 90 이상
    public string[] normalLong;  // 70 이상
    public string[] badLong;     // 그 미만
}
