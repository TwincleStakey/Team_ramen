using System.Collections;
using UnityEngine;

/// <summary>
/// 오늘 장사가 끝났다는 「주문마감」. 네 글자가 한 자씩 퉁 하고 박힌다.
///
/// 글자는 붓글씨 그림 넉 장이다(<c>Tools/make_closed_sign.py</c> 가 원본 한 장을 갈라 굽는다).
/// 한 장으로 두면 한 자씩 박을 수 없어 넷으로 나눴다. 늘어서는 자리는 빌더가 원본 배치
/// 그대로 잡아 주므로 여기서는 크기와 투명도만 만진다.
///
/// 시간은 실시간으로 잰다(unscaled). 뒤이어 정산 팝업이 뜨며 게임이 멈춰도 연출은 흘러야 한다.
/// </summary>
public class ClosedSign : MonoBehaviour
{
    /// <summary>주·문·마·감 넉 장. 빌더가 왼쪽부터 꽂아 준다.</summary>
    [SerializeField] private RectTransform[] glyphs;

    /// <summary>넉 장을 한꺼번에 흐리게 하려고 CanvasGroup 을 쓴다.</summary>
    [SerializeField] private CanvasGroup group;

    [Header("한 자가 박히는 구간")]

    /// <summary>한 자가 떨어져 박히는 데 걸리는 시간(초).</summary>
    [SerializeField] private float stampSeconds = 0.14f;

    /// <summary>어느 크기에서 떨어지는가. 크게 시작해야 다가와 박히는 것으로 읽힌다.</summary>
    [SerializeField] private float stampFrom = 2.4f;

    /// <summary>박히는 순간 제자리(1.0)보다 얼마나 눌리는가. 눌렸다 펴져야 맞은 티가 난다.</summary>
    [SerializeField] private float stampSquash = 0.94f;

    /// <summary>눌린 데서 제자리로 펴지는 시간(초).</summary>
    [SerializeField] private float settleSeconds = 0.07f;

    /// <summary>다음 자까지 쉬는 시간(초). 퉁, 퉁, 퉁, 퉁.</summary>
    [SerializeField] private float stampGap = 0.2f;

    [Header("머무는 구간")]
    [SerializeField] private float holdSeconds = 0.8f;

    [Header("스스스 사라지는 구간")]

    /// <summary>
    /// 다 박힌 뒤 스러지기 시작할 때까지 기다리는 시간(초).
    ///
    /// 화면이 먼저 검게 물들고 글자는 그 위에 남았다가 뒤늦게 사라진다. 같이 사라지면
    /// 글자가 어둠에 묻혀 버려 마지막에 무엇이 적혀 있었는지가 안 남는다.
    /// </summary>
    [SerializeField] private float fadeDelay = 1.2f;

    [SerializeField] private float fadeSeconds = 0.9f;

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 시간의 상한(초).
    ///
    /// <see cref="Time.unscaledDeltaTime"/> 은 유니티가 안 잘라 준다. 씬이 막 뜬 뒤의 첫
    /// 프레임은 몇 초씩 되는데 그대로 쌓으면 연출이 프레임 하나에 다 끝나 버린다.
    /// 화면에는 "애니메이션이 아예 재생되지 않는다" 로 나타난다.
    /// </summary>
    private const float MaxStep = 0.05f;

    private static float Step => Mathf.Min(Time.unscaledDeltaTime, MaxStep);

    private void Awake()
    {
        Hide();
    }

    /// <summary>글자를 모두 감춘다. 다음에 다시 쓸 수 있는 상태로 되돌린다.</summary>
    public void Hide()
    {
        if (group != null) group.alpha = 1f;
        if (glyphs == null) return;

        foreach (RectTransform glyph in glyphs)
        {
            if (glyph == null) continue;
            glyph.localScale = Vector3.one;
            glyph.gameObject.SetActive(false);
        }
    }

    /// <summary>네 글자를 한 자씩 박고, 다 박힌 채로 잠시 둔다. GameManager 가 부른다.</summary>
    public IEnumerator Play()
    {
        Hide();
        if (glyphs == null) yield break;

        for (int i = 0; i < glyphs.Length; i++)
        {
            yield return Stamp(glyphs[i], i == glyphs.Length - 1);

            // 마지막 자 뒤에는 쉬지 않는다. 그 몫은 머무는 구간이 받는다.
            if (i < glyphs.Length - 1) yield return Wait(stampGap);
        }

        yield return Wait(holdSeconds);
    }

    /// <summary>한 자가 위에서 커다랗게 떨어져 박힌다.</summary>
    private IEnumerator Stamp(RectTransform glyph, bool last)
    {
        if (glyph == null) yield break;

        glyph.gameObject.SetActive(true);
        glyph.localScale = Vector3.one * stampFrom;

        // 떨어지는 동안. 끝에서 빨라져야 내리찍는 것으로 보인다.
        // 등속이면 글자가 천천히 다가와 앉는 꼴이라 박히는 맛이 없다.
        for (float t = 0f; t < stampSeconds; t += Step)
        {
            float k = t / stampSeconds;
            glyph.localScale = Vector3.one * Mathf.Lerp(stampFrom, stampSquash, k * k);
            yield return null;
        }

        // 닿는 순간. 소리와 화면 흔들림을 여기서 같이 낸다.
        glyph.localScale = Vector3.one * stampSquash;
        Sfx.Play("sfx_flow_stamp", last ? 0.85f : 0.7f, 1f, 0.04f);
        if (ScreenGrain.Instance != null) ScreenGrain.Instance.Flash();

        // 눌렸다 펴진다.
        for (float t = 0f; t < settleSeconds; t += Step)
        {
            glyph.localScale = Vector3.one * Mathf.Lerp(stampSquash, 1f, t / settleSeconds);
            yield return null;
        }

        glyph.localScale = Vector3.one;
    }

    /// <summary>화면이 검게 물든 뒤, 글자만 남았다가 스러진다.</summary>
    public IEnumerator FadeAway()
    {
        yield return Wait(fadeDelay);

        if (group != null)
        {
            for (float t = 0f; t < fadeSeconds; t += Step)
            {
                group.alpha = 1f - t / fadeSeconds;
                yield return null;
            }
        }

        Hide();
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Step) yield return null;
    }
}
