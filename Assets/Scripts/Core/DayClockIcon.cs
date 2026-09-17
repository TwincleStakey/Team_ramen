using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단바 「N일차 HH:00」 판 왼쪽의 시계 아이콘.
///
/// 손님 한 명이 갈 때마다 한 시간이 흐르므로, 지나간 시간(검정)이 남은 시간(노랑)을
/// 시계 방향으로 잠식해 간다. 조각 하나의 크기는 360도 ÷ 오늘 손님 수라서 날마다 다르다
/// (5명인 날 72도, 8명인 날 45도).
///
/// 그림은 원본 20x18 픽셀이고 Image 가 40x36 으로 2배 늘린다. 유니티 Image 의 Radial 채우기를
/// 쓰지 않는 이유 — 조각 경계가 픽셀 격자를 벗어나 반픽셀 회색이 낀다. 여기서는 텍스처를
/// 픽셀 단위로 직접 칠하므로 경계가 늘 격자에 맞는다.
///
/// 같은 칠하기(<see cref="Paint"/>)를 빌더도 써서 씬에 박아 두는 기본 그림(0명 지남)을 만든다.
/// </summary>
[RequireComponent(typeof(Image))]
public class DayClockIcon : MonoBehaviour
{
    public const int Width = 20;
    public const int Height = 18;

    private static readonly Color32 Remaining = new Color32(255, 213, 65, 255);   // 돈 아이콘과 같은 노랑
    private static readonly Color32 Elapsed = new Color32(0, 0, 0, 255);
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    private Texture2D texture;
    private Sprite sprite;
    private int shownServed = -1;
    private int shownTotal = -1;

    /// <summary>오늘 손님 total 명 중 served 명이 갔다. 값이 같으면 다시 칠하지 않는다.</summary>
    public void Set(int served, int total)
    {
        if (served == shownServed && total == shownTotal) return;
        shownServed = served;
        shownTotal = total;

        if (texture == null)
        {
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 1f);
            GetComponent<Image>().sprite = sprite;
        }

        float fraction = total > 0 ? Mathf.Clamp01((float)served / total) : 0f;
        var px = new Color32[Width * Height];
        Paint(px, Width, Height, fraction);
        texture.SetPixels32(px);
        texture.Apply();
    }

    /// <summary>
    /// 시계 한 장을 칠한다. 픽셀 배열은 유니티 순서(왼쪽 아래부터 한 줄씩 위로)다.
    ///
    /// 테두리 1칸 검정, 안쪽은 12시에서 시계 방향으로 fraction 만큼 검정, 나머지 노랑.
    /// 눈금도 가운데 점도 없다 — 넣어 봤더니 검정 조각 위에서 지저분했다.
    /// </summary>
    public static void Paint(Color32[] px, int width, int height, float fraction)
    {
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;
        float rx = (width - 1) * 0.5f;
        float ry = (height - 1) * 0.5f;
        float sweep = fraction * 360f - 1e-4f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;

                if (!Inside(x, y, cx, cy, rx, ry)) { px[i] = Clear; continue; }

                bool edge = !Inside(x + 1, y, cx, cy, rx, ry) || !Inside(x - 1, y, cx, cy, rx, ry)
                         || !Inside(x, y + 1, cx, cy, rx, ry) || !Inside(x, y - 1, cx, cy, rx, ry);
                if (edge) { px[i] = Elapsed; continue; }

                // 유니티는 y 가 위로 커지므로 (dx, dy) 그대로가 「12시 = 위」다.
                // 20x18 타원이라 반지름으로 나눈 좌표로 각도를 잰다. 그래야 경계선이
                // 세로로 눌린 원 위에서 매끈하게 떨어진다(승인받은 시안과 같은 계산).
                float angle = Mathf.Atan2((x - cx) / rx, (y - cy) / ry) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;

                px[i] = angle < sweep ? Elapsed : Remaining;
            }
        }
    }

    private static bool Inside(int x, int y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
