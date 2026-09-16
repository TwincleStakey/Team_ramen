using System.Collections;
using UnityEngine;

/// <summary>
/// 가운데부터 바깥으로 밝아지는 전환. 검은 화면 한가운데에 동그란 구멍을 내고 그 구멍을 넓힌다.
///
/// 가장자리부터 걷히는 보통 페이드(ScreenFade)와 반대다. 눈을 뜨는 것처럼 읽혀서
/// "이제부터 이 가게를 본다" 는 도입에 맞는다.
///
/// 셰이더를 새로 만들지 않는다. 구멍이 뚫린 네모 판 한 장과, 그 판 바깥을 메우는 검은 띠 넷으로
/// 같은 그림을 만든다. 판이 커지면 띠는 물러난다. 둘을 합치면 구멍만 빼고 늘 화면 전체가 검다.
/// 띠가 없으면 판이 작을 때 네 귀퉁이가 그대로 뚫려 가게가 미리 비친다.
///
/// 시간은 실시간으로 잰다(unscaled). 전환은 게임이 멈춰 있어도 흘러야 한다.
/// </summary>
public class IrisFade : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.

    /// <summary>판과 띠를 묶은 것. 이 스크립트는 이 바깥에 붙어 있어야 껐다 켤 수 있다.</summary>
    [SerializeField] private GameObject root;

    /// <summary>가운데가 뚫린 검은 판.</summary>
    [SerializeField] private RectTransform hole;

    /// <summary>판 바깥을 메우는 검은 띠. 위·아래·왼·오 차례다.</summary>
    [SerializeField] private RectTransform[] bars;

    [SerializeField] private float seconds = 1.4f;

    /// <summary>멈춰 선 뒤 마저 닫는 데 적어도 쓰는 시간(초). <see cref="CloseTo"/> 참고.</summary>
    [SerializeField] private float minShutSeconds = 0.45f;

    /// <summary>
    /// 구멍 반지름에 대한 판 절반 크기의 비.
    ///
    /// 구운 그림에서 구멍 반지름이 판 절반의 절반이라 2다. 그림을 바꾸면 여기도 같이 바꿔야 한다.
    /// </summary>
    [SerializeField] private float holeToHalf = 2f;

    /// <summary>구멍이 화면을 다 덮는 반지름. 화면 대각선의 절반이라 네 귀퉁이까지 닿는다.</summary>
    private float MaxRadius
    {
        get
        {
            var area = transform as RectTransform;
            if (area == null) return 0f;

            float w = area.rect.width;
            float h = area.rect.height;
            return Mathf.Sqrt(w * w + h * h) * 0.5f;
        }
    }

    /// <summary>
    /// 판을 치운다. 오므린 뒤 다른 검은 판이 화면을 넘겨받았을 때 부른다.
    ///
    /// 안 치우면 이 판(층 320)이 화면 맨 앞에 검게 남아, 그 뒤에서 무엇을 드러내도
    /// 아무것도 안 보인다. 자리도 가운데로 되돌린다 — 다음에 열 때 엉뚱한 데서 열린다.
    /// </summary>
    public void Hide()
    {
        if (root == null) return;

        var self = root.transform as RectTransform;
        if (self != null) self.anchoredPosition = Vector2.zero;

        root.SetActive(false);
    }

    /// <summary>구멍을 완전히 닫은 채로 켠다. 화면이 통째로 검어진다.</summary>
    public void Close()
    {
        if (root == null) return;

        root.SetActive(true);
        SetRadius(0f);
    }

    /// <summary>
    /// 구멍을 <paramref name="center"/> 를 한가운데로 두고 오므린다. <see cref="Open"/> 의 반대다.
    ///
    /// 크레딧 끝에서 쓴다 — 쓰러진 손님을 지켜보던 까마귀를 한가운데 두고 화면이 오므라든다.
    /// 다 오므리면 화면이 통째로 검으므로, 그 뒤는 검은 판이 넘겨받으면 된다.
    ///
    /// 반지름은 <b>그 자리에서 제일 먼 귀퉁이까지</b>로 잡는다. 가운데가 한쪽으로 치우쳐
    /// 있으면 화면 대각선 절반으로는 반대편 귀퉁이가 안 덮여 검은 삼각형이 남는다.
    /// </summary>
    /// <param name="holdRadius">
    /// 오므리다 <b>한 번 멈춰 서는</b> 반지름. 0이면 안 멈추고 내리 닫는다.
    /// 크레딧에서는 까마귀에 딱 맞는 크기를 준다 — 구멍이 새 모양만 하게 줄어든 데서
    /// 잠깐 서면, 그냥 닫히는 것이 아니라 <b>그 새를 한 번 보고</b> 닫는 것으로 읽힌다.
    /// </param>
    /// <param name="holdSeconds">그 자리에서 서 있는 시간(초).</param>
    public IEnumerator CloseTo(Vector2 center, float closeSeconds, float holdRadius, float holdSeconds)
    {
        if (root == null) yield break;

        var area = transform as RectTransform;
        if (area != null)
        {
            var self = root.transform as RectTransform;
            if (self != null) self.anchoredPosition = center;
        }

        root.SetActive(true);
        Sfx.Play("sfx_flow_iris", 0.4f);

        float max = FarthestCorner(center);
        float length = closeSeconds > 0f ? closeSeconds : seconds;

        float hold = Mathf.Clamp(holdRadius, 0f, max);
        bool pauses = holdSeconds > 0f && hold > 0f;

        // 시간을 **지나는 거리에 비례해** 나눈다. 반씩 주면 남은 조금을 같은 시간에 닫느라
        // 뒤 토막이 느려져, 멈췄다 다시 갈 때 속도가 툭 바뀐 것으로 보인다.
        float first = pauses ? length * (max - hold) / max : length;

        // 다만 뒤 토막에는 바닥을 둔다. 멈춰 서는 자리가 까마귀만 하게 작아서 비례대로면
        // 0.12초가 나온다 — 일곱 프레임이라 닫히는 것이 안 보이고 툭 꺼진 것이 된다.
        float rest = pauses ? Mathf.Max(length - first, minShutSeconds) : 0f;

        yield return Sweep(max, pauses ? hold : 0f, first);

        if (pauses)
        {
            SetRadius(hold);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Sweep(hold, 0f, rest);
        }

        SetRadius(0f);
    }

    /// <summary>반지름을 한 값에서 다른 값으로 민다. 시작과 끝이 느리다.</summary>
    private IEnumerator Sweep(float from, float to, float length)
    {
        if (length <= 0f)
        {
            SetRadius(to);
            yield break;
        }

        for (float t = 0f; t < length; t += Time.unscaledDeltaTime)
        {
            float k = t / length;
            k = k * k * (3f - 2f * k);

            SetRadius(Mathf.Lerp(from, to, k));
            yield return null;
        }

        SetRadius(to);
    }

    /// <summary>그 자리에서 화면 네 귀퉁이 중 제일 먼 곳까지의 거리.</summary>
    private float FarthestCorner(Vector2 center)
    {
        var area = transform as RectTransform;
        if (area == null) return 0f;

        float w = area.rect.width * 0.5f;
        float h = area.rect.height * 0.5f;

        float best = 0f;
        for (int i = 0; i < 4; i++)
        {
            var corner = new Vector2(i < 2 ? -w : w, (i % 2) == 0 ? -h : h);
            best = Mathf.Max(best, Vector2.Distance(corner, center));
        }
        return best;
    }

    /// <summary>구멍을 넓혀 화면을 연다. 다 열리면 판을 치운다.</summary>
    public IEnumerator Open()
    {
        if (root == null) yield break;

        root.SetActive(true);
        // 0.7 이면 게임에서 제일 큰 소리였다. 화면이 바뀔 때마다 나는 것이라 가운데로 내린다.
        Sfx.Play("sfx_flow_iris", 0.4f);

        float max = MaxRadius;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;

            // 시작과 끝이 느리다. 등속으로 열면 다 열리는 순간에 툭 끊긴 것처럼 보인다.
            k = k * k * (3f - 2f * k);

            SetRadius(max * k);
            yield return null;
        }

        SetRadius(max);
        root.SetActive(false);
    }

    /// <summary>
    /// 구멍 반지름을 정한다. 판은 그만큼 커지고 띠 넷은 판 바깥으로 물러난다.
    ///
    /// 띠는 피벗이 안쪽 변이라 자리만 옮기면 바깥쪽은 알아서 덮인다.
    /// </summary>
    private void SetRadius(float radius)
    {
        float half = radius * holeToHalf;

        if (hole != null) hole.sizeDelta = new Vector2(half * 2f, half * 2f);

        if (bars == null) return;

        if (bars.Length > 0 && bars[0] != null) bars[0].anchoredPosition = new Vector2(0f, half);
        if (bars.Length > 1 && bars[1] != null) bars[1].anchoredPosition = new Vector2(0f, -half);
        if (bars.Length > 2 && bars[2] != null) bars[2].anchoredPosition = new Vector2(-half, 0f);
        if (bars.Length > 3 && bars[3] != null) bars[3].anchoredPosition = new Vector2(half, 0f);
    }
}
