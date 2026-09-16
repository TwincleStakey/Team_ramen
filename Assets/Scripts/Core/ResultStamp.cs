using System.Collections;
using UnityEngine;

/// <summary>
/// 정산표에 박히는 도장 둘과 임대 딱지 하나.
///
/// | 무엇 | 언제 |
/// |---|---|
/// | 대박 | 목표 달성 + 평균 정확도 <see cref="daebakAccuracy"/> 이상 |
/// | 명인의 솜씨 | 목표 달성 + 평균 정확도 <see cref="myunginAccuracy"/> 이상. **대박 다음에** 찍힌다 |
/// | 임대 딱지 | 목표 미달. 도장은 하나도 안 찍고 이것만 |
///
/// 「전원 완벽」을 평균 99.95 로 재는 이유 — 한 그릇이라도 100% 가 아니면 평균이 거기 못 미친다.
/// 그래서 손님 수를 따로 넘겨받지 않아도 판정된다(<c>RamenCalculator.PERFECT_ACCURACY</c> 와 같은 값).
///
/// 연출은 「주문마감」(<see cref="ClosedSign"/>)에서 가져왔다 — 크게 시작해 <c>k²</c> 로 가속해
/// 박히고, 눌렸다 펴진다. 도장은 「퉁」, 딱지는 소리를 낮춰 「쾅」으로 쓴다.
///
/// 시간은 실시간(unscaled)으로 잰다. 정산 팝업이 뜬 동안 게임이 멈춰 있어도 연출은 흘러야 한다.
/// **한 프레임에 흘려보낼 시간은 잘라 쓴다** — Play 직후 첫 프레임이 4초를 넘기도 하는데,
/// 그대로 쌓으면 도장 셋이 한 프레임에 다 박혀 버린다.
/// </summary>
public class ResultStamp : MonoBehaviour
{
    /// <summary>찍히는 것들. 프리팹에서 꽂아 준다. 비어 있으면 그 자리만 조용히 건너뛴다.</summary>
    [SerializeField] private RectTransform daebak;
    [SerializeField] private RectTransform myungin;
    [SerializeField] private RectTransform lease;

    [Header("조건")]
    /// <summary>
    /// 대박 도장의 평균 정확도 문턱.
    ///
    /// `정확도 = 100 − (토핑 오차 합 / 토핑 정답 합) × 100` 이고 `판매액 = 10000 × 정확도/100` 이라
    /// **수익률과 평균 정확도가 같은 수**다. 목표 금액도 세 구간 다 최대의 70% 이므로
    /// 목표 달성 = 평균 70% 다. 토핑 정답 수가 시오·쇼유 5 · 돈코츠 7 이라,
    /// 90 은 **두 그릇에 한 번만 틀리는** 수준이다.
    /// </summary>
    [SerializeField] private float daebakAccuracy = 90f;

    /// <summary>명인의 솜씨 문턱. 곧 「모든 주문이 100%」다.</summary>
    [SerializeField] private float myunginAccuracy = 99.95f;

    [Header("박히는 박자")]
    [SerializeField] private float leadIn = 0.36f;           // 숫자가 다 굴러간 뒤 한 박자 쉰다

    /// <summary>
    /// 한 장이 떨어져 박히는 데 걸리는 시간(초).
    ///
    /// 「주문마감」에서 그대로 가져온 0.14 는 정산표에서 너무 빨랐다 — 네 자가 연달아
    /// 박히는 쪽은 빠른 맛이 있지만, 여기는 도장이 많아야 둘이라 눈에 안 남는다. 2배로 늘렸다.
    ///
    /// **곡선이 `k²` 라 시간을 늘리면 「천천히 내려온다」가 아니라 「크게 뜬 채 머물다 훅 박힌다」가 된다.**
    /// 0.45·0.70 까지도 뽑아 견줬는데 그쯤 가면 도장이 공중에 떠 있는 시간이 눈에 띈다.
    /// 더 늘리고 싶으면 시간이 아니라 곡선을 손봐야 한다.
    /// </summary>
    [SerializeField] private float stampSeconds = 0.28f;

    [SerializeField] private float stampFrom = 2.4f;         // 크게 시작해야 다가와 박히는 것으로 읽힌다
    [SerializeField] private float stampSquash = 0.94f;      // 눌렸다 펴져야 맞은 티가 난다
    [SerializeField] private float settleSeconds = 0.12f;
    [SerializeField] private float stampGap = 0.36f;         // 대박 다음 명인까지 쉬는 시간

    /// <summary>한 프레임에 흘려보낼 수 있는 최대 시간(초).</summary>
    private const float MaxStep = 0.05f;

    private static float Step { get { return Mathf.Min(Time.unscaledDeltaTime, MaxStep); } }

    /// <summary>
    /// 지금 씬에 떠 있는 것. <see cref="ScreenGrain"/> 과 같은 방식이다.
    ///
    /// 정산 팝업은 프리팹이고 <c>DailyResultUI</c> 는 늘 살아 있는 OrderSystem 에 붙어 있어서,
    /// 둘을 직렬화 칸으로 이으려면 빌더가 꽂아 줘야 한다. 정적 칸으로 두면 **빌더를 안 돌려도**
    /// 되고 B 영역인 `DailyResultUI` 에 새 칸을 만들지 않아도 된다.
    ///
    /// <c>Awake</c> 가 아니라 <c>OnEnable</c> 에서 채운다 — 팝업은 꺼진 채로 만들어져서
    /// Awake 가 늦게 돈다.
    /// </summary>
    public static ResultStamp Instance { get; private set; }

    /// <summary>팝업이 켜질 때마다 지난 날 도장을 치운다. 안 치우면 이틀치가 겹쳐 남는다.</summary>
    private void OnEnable()
    {
        Instance = this;
        Clear();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public void Clear()
    {
        Hide(daebak);
        Hide(myungin);
        Hide(lease);
    }

    private static void Hide(RectTransform piece)
    {
        if (piece == null) return;

        piece.localScale = Vector3.one;
        piece.gameObject.SetActive(false);
    }

    /// <summary>
    /// 정산 숫자가 다 굴러간 뒤에 부른다.
    /// </summary>
    /// <param name="isSuccess">목표 금액 달성 여부. 못 채웠으면 도장 없이 임대 딱지만.</param>
    /// <param name="averageAccuracy">당일 평균 정확도(%).</param>
    public IEnumerator Play(bool isSuccess, float averageAccuracy)
    {
        Clear();
        yield return Wait(leadIn);

        if (!isSuccess)
        {
            // 딱지는 종이라 소리를 낮춰 둔다. 도장의 「퉁」과 갈려야 한다.
            yield return Stamp(lease, 0.9f, 0.72f);
            yield break;
        }

        if (averageAccuracy >= daebakAccuracy)
            yield return Stamp(daebak, 0.75f, 1f);

        // 명인은 **늘 대박 다음**이다. 제일 어려운 도장이라 마지막에 와야 한 방이 산다.
        // 평균 99.95 는 daebakAccuracy 를 반드시 넘으므로 둘이 같이 찍힌다.
        if (averageAccuracy >= myunginAccuracy)
        {
            yield return Wait(stampGap);
            yield return Stamp(myungin, 0.95f, 1.05f);
        }
    }

    /// <summary>한 장이 떨어져 박힌다. <see cref="ClosedSign.Stamp"/> 와 같은 곡선이다.</summary>
    private IEnumerator Stamp(RectTransform piece, float volume, float pitch)
    {
        if (piece == null) yield break;

        piece.gameObject.SetActive(true);
        piece.localScale = Vector3.one * stampFrom;

        // 끝에서 빨라져야 내리찍는 것으로 보인다. 등속이면 천천히 앉는 꼴이다.
        for (float t = 0f; t < stampSeconds; t += Step)
        {
            float k = t / stampSeconds;
            piece.localScale = Vector3.one * Mathf.Lerp(stampFrom, stampSquash, k * k);
            yield return null;
        }

        piece.localScale = Vector3.one * stampSquash;
        Sfx.Play("sfx_flow_stamp", volume, pitch, 0.04f);
        if (ScreenGrain.Instance != null) ScreenGrain.Instance.Flash();

        for (float t = 0f; t < settleSeconds; t += Step)
        {
            piece.localScale = Vector3.one * Mathf.Lerp(stampSquash, 1f, t / settleSeconds);
            yield return null;
        }

        piece.localScale = Vector3.one;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Step) yield return null;
    }
}
