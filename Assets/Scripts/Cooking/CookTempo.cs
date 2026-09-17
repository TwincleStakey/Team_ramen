using UnityEngine;

/// <summary>
/// 재료를 넣는 연출을 몇 배로 돌릴지. <b>조리대 안에서만 먹는다</b> —
/// 국자로 붓기·면 넣기·조미료 뿌리기·그릇 찰랑임이다.
///
/// 손님 입장·시식 컷신·결과창 같은 흐름 연출은 건드리지 않는다. 그쪽은 「보는 것」이라
/// 빨라지면 대사가 읽히기 전에 끝난다. 여기서 줄이려는 것은 <b>같은 동작을 서른 번 되풀이하는</b>
/// 조리 손놀림뿐이다.
///
/// <see cref="Time.timeScale"/> 을 쓰지 않는다. 이 게임의 연출은 거의 다 실시간
/// (WaitForSecondsRealtime·unscaledDeltaTime)으로 흐르게 짜여 있어서 — 설정창이 시간을 0 으로
/// 눌러도 연출은 돌아야 하므로 — timeScale 을 올려 봐야 아무것도 안 빨라진다.
/// 그래서 연출 코드가 직접 읽는 배율을 따로 둔다.
/// </summary>
public static class CookTempo
{
    /// <summary>빠르게 돌릴 때의 배율.</summary>
    public const float FastScale = 2f;

    private const string PrefKey = "cook.fast";

    private static bool loaded;
    private static bool fast;

    /// <summary>켜고 끌 때마다 알린다. 키 안내 아이콘이 이걸 듣고 그림을 바꾼다.</summary>
    public static event System.Action Changed;

    /// <summary>지금 빠르게 돌고 있는가. 한 번 켜면 끌 때까지 그대로다.</summary>
    public static bool Fast
    {
        get
        {
            Load();
            return fast;
        }
    }

    /// <summary>연출 시간을 나눌 값. 1 이면 평소 속도다.</summary>
    public static float Scale
    {
        get { return Fast ? FastScale : 1f; }
    }

    /// <summary>이 배율로 흐르는 한 프레임. <c>Time.unscaledDeltaTime</c> 대신 쓴다.</summary>
    public static float Delta
    {
        get { return Time.unscaledDeltaTime * Scale; }
    }

    /// <summary>이 배율로 재는 기다림. <c>new WaitForSecondsRealtime(x)</c> 대신 쓴다.</summary>
    public static WaitForSecondsRealtime Wait(float seconds)
    {
        return new WaitForSecondsRealtime(seconds / Scale);
    }

    /// <summary>초당 몇 칸 넘길지. 배율만큼 빨리 넘어간다.</summary>
    public static float Fps(float fps)
    {
        return fps * Scale;
    }

    public static void Toggle()
    {
        Load();
        fast = !fast;

        PlayerPrefs.SetInt(PrefKey, fast ? 1 : 0);
        PlayerPrefs.Save();

        if (Changed != null) Changed();
    }

    /// <summary>
    /// 저장해 둔 값을 한 번만 읽는다.
    ///
    /// 소리·화면 필터가 PlayerPrefs 에 넣는 것과 같은 방식이다. 다시 켰을 때 배속이 풀려
    /// 있으면 매번 다시 켜야 한다.
    /// </summary>
    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        fast = PlayerPrefs.GetInt(PrefKey, 0) == 1;
    }
}
