using System.Collections;
using UnityEngine;

/// <summary>
/// 버튼 줄이 하나씩 토도도독 튀어나온다.
///
/// 처음에는 줄 전체가 없다. 로고 연출이 끝나면 왼쪽부터 차례로 뽕 하고 부풀어 오른다.
/// 넷이 한꺼번에 뜨면 "화면이 하나 더 켜졌다"로 보이고, 차례로 뜨면 "차려지는 중"으로 보인다.
///
/// 기다리는 대상은 시간이 아니라 <see cref="TitleLogoIntro.Finished"/> 다. 초를 세어 맞추면
/// 로고 박자를 손볼 때마다 여기까지 같이 고쳐야 한다.
///
/// **자리(anchoredPosition)는 건드리지 않는다. 크기만 만진다.**
/// 같은 버튼에 붙어 있는 <see cref="ButtonPress"/> 가 자기 Awake 에서 그때의 자리를
/// "제자리"로 기억해 두었다가 뗄 때 거기로 돌려놓는다. 줄이 켜질 때 이쪽이 먼저 돌아 버튼을
/// 내려놓으면, ButtonPress 는 **내려간 자리**를 제자리로 알고 그 뒤로는 누를 때마다
/// 버튼이 거기에 눌린 채로 앉는다. 실제로 그 버그를 냈다.
/// 크기는 ButtonPress 가 안 쓰는 값이라 서로 안 부딪힌다.
/// </summary>
public class TitleButtonCascade : MonoBehaviour
{
    /// <summary>이 연출이 끝나기를 기다린다. 비워 두면 곧바로 시작한다.</summary>
    [SerializeField] private TitleLogoIntro waitFor;

    /// <summary>
    /// 기다림이 끝나고 한 박자 쉰다.
    ///
    /// **기다린 경우에만** 쉰다. 로고가 켜지자마자 버튼이 따라 나오면 급해 보이는데,
    /// 버튼을 눌러 줄이 갈릴 때까지 이만큼 멈칫하면 그건 그냥 굼뜬 것이다.
    /// </summary>
    [SerializeField] private float startDelay = 1.2f;

    /// <summary>버튼 사이 간격. 이 값이 "토도도독" 의 속도다.</summary>
    [SerializeField] private float stagger = 0.44f;

    /// <summary>한 개가 튀어나오는 데 걸리는 시간.</summary>
    [SerializeField] private float popSeconds = 0.95f;

    /// <summary>시작 크기. 작을수록 뽕 하고 부푸는 맛이 세다.</summary>
    [SerializeField] private float startScale = 0.4f;

    /// <summary>
    /// 젤리처럼 출렁이는 정도. 커질 때 가로로 벌어지고 세로로 눌린다.
    ///
    /// 크기만 고르게 키우면 풍선이지 젤리가 아니다. 가로·세로를 반대로 흔들어야
    /// 말랑한 것이 튀어나온 것으로 보인다.
    /// </summary>
    [SerializeField] private float jelly = 0.55f;

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 시간의 상한.
    ///
    /// Play 를 누른 뒤 첫 프레임은 씬 로드 때문에 4초가 넘기도 한다. 그대로 두면 기다림도
    /// 튀어나오는 것도 그 한 프레임에 다 끝나서, 넷이 한꺼번에 뜬 것처럼 보인다.
    /// <see cref="TitleLogoIntro"/> 가 같은 이유로 같은 값을 쓴다.
    /// </summary>
    private const float MaxStep = 0.05f;

    private RectTransform[] items;
    private CanvasGroup[] groups;

    private void Awake()
    {
        int count = transform.childCount;
        items = new RectTransform[count];
        groups = new CanvasGroup[count];

        for (int i = 0; i < count; i++)
        {
            items[i] = (RectTransform)transform.GetChild(i);

            // 빌더가 안 붙여 줘도 되도록 여기서 챙긴다. 버튼 만드는 쪽을 건드리지 않으려는 것이다.
            groups[i] = items[i].GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>모드 줄에 갔다 돌아올 때도 다시 나온다.</summary>
    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (items == null) return;   // 한 번도 안 켜진 채로 꺼질 수 있다(모드 줄)
        for (int i = 0; i < items.Length; i++) Settle(i);
    }

    private IEnumerator Run()
    {
        for (int i = 0; i < items.Length; i++) Hide(i);

        bool waited = waitFor != null && !waitFor.Finished;
        while (waitFor != null && !waitFor.Finished) yield return null;

        // 버튼을 눌러 줄이 갈릴 때는 이미 끝나 있어서 안 기다린다 — 그때는 쉬지도 않는다.
        if (waited && startDelay > 0f) yield return Wait(startDelay);

        for (int i = 0; i < items.Length; i++)
            StartCoroutine(Pop(i, i * stagger));
    }

    private IEnumerator Pop(int index, float delay)
    {
        if (delay > 0f) yield return Wait(delay);

        // 버튼마다 조금씩 높아진다. 같은 높이로 넷이면 오류음처럼 들린다.
        Sfx.Play("sfx_flow_cascade", 0.5f, 1f + index * 0.06f);

        float elapsed = 0f;
        while (elapsed < popSeconds)
        {
            elapsed += Step();
            Apply(index, Mathf.Clamp01(elapsed / popSeconds));
            yield return null;
        }

        Settle(index);
    }

    /// <summary>기다린다. 긴 프레임 하나에 기다림이 통째로 먹히지 않도록 잘라서 센다.</summary>
    private static IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Step();
            yield return null;
        }
    }

    /// <summary>이번 프레임에 흘려보낼 시간. 긴 프레임은 잘라서 연출이 건너뛰지 않게 한다.</summary>
    private static float Step()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxStep);
    }

    private void Hide(int index)
    {
        groups[index].alpha = 0f;
        groups[index].blocksRaycasts = false;
        groups[index].interactable = false;
        items[index].localScale = new Vector3(startScale, startScale, 1f);
    }

    private void Settle(int index)
    {
        groups[index].alpha = 1f;
        groups[index].blocksRaycasts = true;
        groups[index].interactable = true;
        items[index].localScale = Vector3.one;
    }

    /// <summary>0 이면 없는 상태, 1 이면 제자리.</summary>
    private void Apply(int index, float t)
    {
        float spring = Spring(t);

        // 1을 넘긴 만큼이 "출렁이는 양" 이다. 커질 때 가로로 벌어지고 세로로 눌린다.
        float wobble = (spring - 1f) * jelly;
        float scale = Mathf.Lerp(startScale, 1f, spring);

        items[index].localScale = new Vector3(scale * (1f + wobble), scale * (1f - wobble), 1f);

        groups[index].alpha = Mathf.Clamp01(t * 4f);   // 부푸는 것이 보이도록 먼저 드러난다
        groups[index].blocksRaycasts = false;
        groups[index].interactable = false;
    }

    /// <summary>
    /// 튀어나왔다가 몇 번 출렁이고 멎는 곡선(easeOutElastic).
    ///
    /// 1을 한 번 크게 넘겼다가 점점 작게 넘나들며 1로 잦아든다. 한 번만 지나쳤다 오는 곡선보다
    /// 이쪽이 말랑하고 장난스럽다.
    /// </summary>
    private static float Spring(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        const float period = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * period) + 1f;
    }
}
