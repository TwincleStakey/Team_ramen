using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작 화면 로고가 간판처럼 켜지는 연출. 깜빡 · 깜빡깜빡 · ... · 스르르.
///
/// 앞의 깜빡임 셋은 방전이다 — **즉시 붙고 지수로 식는다.** 직선으로 오르내리게 하면
/// 전등이 아니라 조광기 손잡이를 돌리는 움직임이 되어 단번에 가짜로 보인다.
/// 둘째와 셋째가 0.2초 간격으로 붙어 있어서 "어어, 켜지나?" 하는 자리를 만든다.
///
/// 마지막만 성격이 다르다. 탁 붙지 않고 관이 달아오르듯 **서서히** 차오른다.
/// 그 곡선은 뒤로 몰려 있다 — 한참 어둑하다가 끝에서 확 밝아진다. 침묵이 끝나고도
/// 한 박자 더 기다리게 만드는 것이 이 연출의 맛이다.
///
/// **판 크기는 한 번도 안 건드린다.** 튀거나 부풀리면 악센트가 둘이 되어 산만해진다.
///
/// 뒤에 깔리는 불빛은 간판을 **늦게 따라간다.** 밝아질 때는 바짝 붙고 식을 때는 천천히 —
/// 깜빡임이 꺼진 뒤에도 빛이 잠깐 남는다. 이 잔광이 "그림 밝기가 바뀐다" 와
/// "불이 켜진다" 를 가른다.
///
/// **밝게는 못 만든다.** 유니티 UI 색은 원본에 곱하는 값이라 1을 넘겨도 더 밝아지지 않는다.
/// 뒤에 까는 불빛이 그 한계를 비껴간다 — 더할 수는 없어도 옆에 빛을 놓을 수는 있다.
/// </summary>
[RequireComponent(typeof(Image))]
public class TitleLogoIntro : MonoBehaviour
{
    /// <summary>헛불 한 번. 붙는 시각 · 그때 밝기 · 식는 데 걸리는 시간이다.</summary>
    private struct Blink
    {
        public readonly float At;
        public readonly float Peak;
        public readonly float Tau;

        public Blink(float at, float peak, float tau)
        {
            At = at;
            Peak = peak;
            Tau = tau;
        }
    }

    /// <summary>
    /// 켜지기 전 밝기. 0 으로 두면 로고가 아예 사라져 화면이 비어 보인다.
    /// 0.12 면 꺼진 간판의 검은 실루엣으로 남는다 — 없는 것이 아니라 **꺼져 있는** 것으로 보인다.
    /// </summary>
    [SerializeField] private float darkLevel = 0.12f;

    /// <summary>헛불 셋. 뒤의 둘이 붙어 있어야 "켜지려다 만" 것으로 읽힌다.</summary>
    private static readonly Blink[] Blinks =
    {
        new Blink(0.70f, 1.00f, 0.14f),
        new Blink(1.75f, 0.90f, 0.10f),
        new Blink(1.95f, 0.78f, 0.08f),
    };

    /// <summary>마지막으로 불이 붙는 시각과, 다 차오르는 데 걸리는 시간.</summary>
    [SerializeField] private float igniteAt = 2.85f;
    [SerializeField] private float riseSeconds = 1.35f;

    /// <summary>한 번 오르내리는 데 걸리는 시간과 가장 어두울 때의 밝기.</summary>
    [SerializeField] private float breathSeconds = 2.6f;
    [SerializeField] private float breathLow = 0.88f;

    /// <summary>로고 뒤에 깔리는 따뜻한 불빛. 없어도 돌아간다.</summary>
    [SerializeField] private Image glow;

    /// <summary>불빛이 제일 밝을 때의 진하기. 그림 자체가 이미 옅어서 보통 1 로 둔다.</summary>
    [SerializeField] private float glowStrength = 1f;

    /// <summary>
    /// 불빛이 간판을 따라가는 더딤. 밝아질 때와 식을 때가 다르다.
    ///
    /// 같은 값으로 두면 잔광이 안 생겨 간판과 똑같이 깜빡인다. 식는 쪽을 훨씬 더디게
    /// 두어야 헛불이 꺼진 뒤에도 빛이 남아 관이 식는 것처럼 보인다.
    /// </summary>
    [SerializeField] private float glowRiseTau = 0.03f;
    [SerializeField] private float glowFallTau = 0.26f;

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 시간의 상한.
    ///
    /// **이게 없으면 연출이 통째로 사라진다.** Play 를 누른 뒤 첫 프레임은 씬 로드와
    /// 도메인 리로드 때문에 4초가 넘기도 한다. <c>Time.deltaTime</c> 은 유니티가
    /// <c>maximumDeltaTime</c>(0.333) 으로 잘라 주지만 <c>unscaledDeltaTime</c> 은 안 잘라 준다.
    /// 그대로 쌓으면 4.2초짜리 점등이 프레임 하나에 다 끝나고, 화면에는 이미 켜진 간판만 남는다.
    /// 실제로 그렇게 만들어 놓고 "재생이 안 된다" 를 한참 찾았다.
    /// </summary>
    private const float MaxStep = 0.05f;

    /// <summary>켜지는 데까지 다 끝났는가. 버튼 줄이 이걸 보고 따라 나온다.</summary>
    public bool Finished { get; private set; }

    private Image image;
    private float level;
    private float glowLevel;

    private void Awake()
    {
        image = GetComponent<Image>();

        // 첫 프레임부터 꺼져 있어야 한다. OnEnable 을 기다리면 한 컷 밝게 스친다.
        SetLevel(darkLevel);
        glowLevel = 0f;
        ApplyGlow();
    }

    /// <summary>시작 화면은 껐다 다시 켜질 수 있다(뒤로 가기). 그때마다 처음부터 다시 한다.</summary>
    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SetLevel(1f);   // 꺼진 자리에 어두운 로고가 남지 않게 되돌린다
    }

    /// <summary>
    /// 불빛은 코루틴이 아니라 여기서 따라간다.
    ///
    /// 간판 밝기는 깜빡일 때 뚝뚝 바뀌는데 불빛은 그것과 **다른 박자**로 움직여야 한다.
    /// 같은 자리에서 같이 정하면 둘이 늘 붙어 다녀 잔광이 안 생긴다.
    /// </summary>
    private void Update()
    {
        float target = Lit(level);
        float tau = target > glowLevel ? glowRiseTau : glowFallTau;

        glowLevel += (target - glowLevel) * (1f - Mathf.Exp(-Step() / tau));
        ApplyGlow();
    }

    private IEnumerator Run()
    {
        Finished = false;

        yield return LightUp();

        Finished = true;
        yield return Breathe();
    }

    private IEnumerator LightUp()
    {
        float end = igniteAt + riseSeconds;
        float elapsed = 0f;
        bool ignited = false;

        while (elapsed < end)
        {
            elapsed += Step();

            // 헛불이 끝나고 간판이 정말로 켜지는 순간에 한 번.
            if (!ignited && elapsed >= igniteAt) { ignited = true; Sfx.Play("sfx_flow_logo", 0.6f); }

            SetLevel(LevelAt(elapsed));
            yield return null;
        }

        SetLevel(1f);
    }

    /// <summary>그 순간의 간판 밝기. 헛불들의 잔광과 마지막 차오름 중 가장 밝은 것을 쓴다.</summary>
    private float LevelAt(float t)
    {
        float value = darkLevel;

        for (int i = 0; i < Blinks.Length; i++)
        {
            Blink blink = Blinks[i];
            if (t < blink.At) continue;

            value = Mathf.Max(value,
                              darkLevel + (blink.Peak - darkLevel) * Mathf.Exp(-(t - blink.At) / blink.Tau));
        }

        if (t >= igniteAt)
        {
            float k = Mathf.Clamp01((t - igniteAt) / riseSeconds);
            value = Mathf.Max(value, darkLevel + (1f - darkLevel) * Rise(k));
        }

        return value;
    }

    /// <summary>
    /// 차오르는 곡선. 뒤로 몰려 있다 — 한참 어둑하다가 끝에서 확 밝아진다.
    ///
    /// 세제곱만 쓰면 끝이 너무 급해 탁 켜진 것처럼 보이고, S자만 쓰면 뜸이 안 든다.
    /// 둘을 섞어 앞은 세제곱이, 끝은 S자가 끌고 가게 했다.
    /// </summary>
    private static float Rise(float k)
    {
        float cubic = k * k * k;
        float smooth = k * k * (3f - 2f * k);
        return cubic * 0.55f + smooth * 0.45f;
    }

    private IEnumerator Breathe()
    {
        float elapsed = 0f;

        while (true)
        {
            elapsed += Step();

            // 코사인은 1에서 시작해 내려갔다 돌아온다 — 켜진 밝기에서 출발하는 모양이다.
            float k = (Mathf.Cos(elapsed / breathSeconds * 2f * Mathf.PI) + 1f) * 0.5f;
            SetLevel(Mathf.Lerp(breathLow, 1f, k));
            yield return null;
        }
    }

    /// <summary>이번 프레임에 흘려보낼 시간. 긴 프레임은 잘라서 연출이 건너뛰지 않게 한다.</summary>
    private static float Step()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxStep);
    }

    private void SetLevel(float value)
    {
        level = value;

        Color tint = image.color;
        image.color = new Color(value, value, value, tint.a);
    }

    /// <summary>꺼진 밝기면 0, 제 밝기면 1. 간판이 켜진 만큼만 불빛이 번진다.</summary>
    private float Lit(float value)
    {
        return Mathf.Clamp01((value - darkLevel) / Mathf.Max(0.0001f, 1f - darkLevel));
    }

    private void ApplyGlow()
    {
        if (glow == null) return;

        Color light = glow.color;
        glow.color = new Color(light.r, light.g, light.b, glowLevel * glowStrength);
    }
}
