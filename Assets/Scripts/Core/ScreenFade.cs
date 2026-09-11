using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 검게 덮었다 걷는 판. 장면이 툭 바뀌는 것을 가린다.
///
/// 시간은 실시간으로 잰다(unscaled). 확인창이 떠서 게임이 멈춰 있어도 전환은 흘러야 한다.
///
/// 판은 클릭을 막는다. 덮인 동안 눌린 버튼이 걷힌 뒤에 뒤늦게 동작하면
/// 플레이어가 누른 기억이 없는 일이 일어난다.
/// </summary>
public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }

    /// <summary>검은 판. 빌더가 꽂아 준다.</summary>
    [SerializeField] private Image cover;

    /// <summary>
    /// 덮거나 걷는 데 걸리는 시간(초).
    ///
    /// 0.4 로는 눈에 안 들어왔다. 화면이 깜빡인 것처럼 보이고 지나가서 전환이 있었는지도 모른다.
    /// </summary>
    [SerializeField] private float seconds = 1.1f;

    private void Awake()
    {
        Instance = this;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>검게 덮는다.</summary>
    public IEnumerator FadeOut()
    {
        yield return Fade(0f, 1f);
    }

    /// <summary>걷어 낸다.</summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f);
    }

    /// <summary>덮은 채로 둔다. 뒤에서 화면을 바꿔 놓을 때 쓴다.</summary>
    public void HoldBlack()
    {
        SetAlpha(1f);
    }

    /// <summary>
    /// 걷는 연출 없이 판을 바로 치운다.
    ///
    /// 다른 검은 판(아이리스)이 화면을 넘겨받은 뒤에 쓴다. 화면은 이미 그쪽이 덮고 있어서
    /// 여기서 치워도 가게가 비치지 않는다.
    /// </summary>
    public void Clear()
    {
        SetAlpha(0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (cover == null) yield break;

        SetAlpha(from);

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (cover == null) return;

        Color c = cover.color;
        cover.color = new Color(c.r, c.g, c.b, a);

        // 다 걷힌 뒤에도 켜 두면 화면 전체가 클릭을 먹는다.
        cover.enabled = a > 0.001f;
        cover.raycastTarget = cover.enabled;
    }
}
