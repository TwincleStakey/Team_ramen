using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 네모 하나를 위아래 그라데이션으로 칠한다. 그림 파일도 텍스처도 쓰지 않는다 —
/// 네모를 가로 줄로 잘라 꼭짓점 색만 바꾼다.
///
/// 크레딧 아래 어두운 띠가 이걸 쓴다. 코드로 구운 텍스처를 스프라이트로 물렸더니 UI 에서
/// 아무것도 안 그려져서(메시는 멀쩡한데 화면에 안 나온다) 텍스처를 아예 없앴다.
/// 꼭짓점 색은 UI 기본 셰이더가 늘 곱해 주는 것이라 어디서든 똑같이 나온다.
///
/// 붙이는 <see cref="Graphic"/> 은 스프라이트 없는 <see cref="Image"/> 여야 한다.
/// 스프라이트가 있으면 그 그림 위에 색이 곱해진다.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UiVerticalGradient : BaseMeshEffect
{
    /// <summary>아래변의 진하기.</summary>
    [SerializeField] private float bottomAlpha = 0.88f;

    /// <summary>윗변의 진하기. 0 이면 위쪽이 완전히 사라진다.</summary>
    [SerializeField] private float topAlpha;

    /// <summary>
    /// 가로로 몇 줄로 자를지. 줄이 적으면 계단이 보이고, 많으면 곡선이 매끄럽다.
    /// 띠 높이가 132 칸이라 32 줄이면 한 줄이 4 칸이다.
    /// </summary>
    [SerializeField] private int steps = 32;

    /// <summary>
    /// 진하기가 오르는 곡선의 제곱수. 1 이면 곧은 직선이고, 키울수록 위쪽이 길게 흐려진다.
    /// 2 쯤이면 「아래는 확실히 어둡고 위로는 슬그머니 사라지는」 영화 자막 띠가 된다.
    /// </summary>
    [SerializeField] private float curve = 2f;

    /// <summary>
    /// 켜면 가운데가 제일 짙고 위아래 양 끝이 0 으로 빠진다. 아래에서 위로 한 방향으로
    /// 빠지는 대신 양쪽으로 사라져서, 화면 아래 붙은 자막 띠가 아니라 글 뒤에 깔린 판이 된다.
    /// </summary>
    [SerializeField] private bool symmetric;

    public void Set(float bottom, float top, int stepCount, float curvePower, bool bothEnds = false)
    {
        bottomAlpha = bottom;
        topAlpha = top;
        steps = Mathf.Max(1, stepCount);
        curve = curvePower;
        symmetric = bothEnds;

        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>t(0=아래, 1=위) 자리의 진하기.</summary>
    private float AlphaAt(float t)
    {
        if (!symmetric) return Mathf.Lerp(bottomAlpha, topAlpha, Mathf.Pow(t, curve));

        // 가운데에서 1, 양 끝에서 0.
        return bottomAlpha * (1f - Mathf.Pow(Mathf.Abs(t * 2f - 1f), curve));
    }

    private static readonly List<UIVertex> buffer = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper helper)
    {
        if (!IsActive() || helper.currentVertCount == 0) return;

        buffer.Clear();
        helper.GetUIVertexStream(buffer);
        if (buffer.Count < 6) return;

        // 원본 네모의 위아래 끝을 잰다. 자를 자리를 여기서 뽑는다.
        float low = float.MaxValue;
        float high = float.MinValue;
        float left = float.MaxValue;
        float right = float.MinValue;

        for (int i = 0; i < buffer.Count; i++)
        {
            Vector3 p = buffer[i].position;
            if (p.y < low) low = p.y;
            if (p.y > high) high = p.y;
            if (p.x < left) left = p.x;
            if (p.x > right) right = p.x;
        }

        if (high - low <= 0f) return;

        UIVertex sample = buffer[0];
        helper.Clear();

        // 아래에서 위로 한 줄씩 쌓는다. 줄 하나가 네모 하나다.
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float y = Mathf.Lerp(low, high, t);
            float a = AlphaAt(t);

            AddVertex(helper, sample, left, y, a);
            AddVertex(helper, sample, right, y, a);
        }

        // ⚠️ 감김 방향은 시계 방향이어야 한다. 유니티 UI 네모가 (좌하 → 좌상 → 우상) 으로
        // 도는데, 반대로 감으면 뒷면이 되어 화면에 아무것도 안 그려진다.
        // 메시도 꼭짓점도 멀쩡한데 안 보여서 한참 헤맸다.
        for (int i = 0; i < steps; i++)
        {
            int b = i * 2;
            helper.AddTriangle(b, b + 3, b + 1);        // 좌하 → 우상 → 우하
            helper.AddTriangle(b, b + 2, b + 3);        // 좌하 → 좌상 → 우상
        }
    }

    private static void AddVertex(VertexHelper helper, UIVertex sample, float x, float y, float alpha)
    {
        sample.position = new Vector3(x, y, 0f);
        sample.color = new Color32(sample.color.r, sample.color.g, sample.color.b,
                                   (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        helper.AddVert(sample);
    }
}
