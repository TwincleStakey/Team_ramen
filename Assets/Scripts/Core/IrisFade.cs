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

    /// <summary>구멍을 완전히 닫은 채로 켠다. 화면이 통째로 검어진다.</summary>
    public void Close()
    {
        if (root == null) return;

        root.SetActive(true);
        SetRadius(0f);
    }

    /// <summary>구멍을 넓혀 화면을 연다. 다 열리면 판을 치운다.</summary>
    public IEnumerator Open()
    {
        if (root == null) yield break;

        root.SetActive(true);

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
