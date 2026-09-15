using UnityEngine;

/// <summary>
/// 마우스가 있는 화면 좌표(픽셀)를 캔버스 위 자리로 옮긴다.
///
/// 캔버스가 Screen Space - Overlay 이던 시절에는 둘이 같았다. 월드 한 칸이 화면 한 픽셀이라
/// <c>rect.position = 마우스좌표</c> 한 줄이면 끝났고, 코드 곳곳이 그렇게 적혀 있었다.
///
/// 2026-09-14 에 화면 필터를 넣으면서 캔버스를 Screen Space - Camera 로 옮겼다. UI 도 카메라
/// 후처리를 타게 하려는 것이었는데, 그 순간부터 월드 좌표가 카메라 기준 칸으로 바뀐다 —
/// 직교 카메라의 orthographicSize 가 5 라 화면 세로가 10칸뿐이다. 여기에 픽셀 좌표(예: 540)를
/// 그대로 넣으면 화면 밖 수백 칸 너머로 날아간다.
///
/// 조리 커서가 통째로 사라진 것이 이것이었다. 커서는 멀쩡히 켜져 있었고 자리만 화면 밖이었다.
/// 시스템 커서는 이미 꺼진 뒤라 화면에 아무것도 안 남았다.
/// </summary>
public static class CanvasPoint
{
    /// <summary>
    /// 화면 좌표를 <paramref name="area"/> 가 놓인 판 위의 월드 좌표로 옮긴다.
    ///
    /// 판의 크기는 상관없다. 판이 놓인 평면만 쓴다.
    /// 옮기지 못하면 넘어온 값을 그대로 돌려준다 — 자리가 틀리는 편이 사라지는 것보다 낫다.
    /// </summary>
    public static Vector3 ToWorld(RectTransform area, Vector2 screen)
    {
        if (area == null) return screen;

        Vector3 world;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(area, screen, CameraFor(area), out world)
            ? world
            : (Vector3)screen;
    }

    /// <summary>
    /// 이 판이 얹힌 캔버스를 그리는 카메라. Overlay 면 null 이고, 그때는 카메라를 넘기면 안 된다.
    ///
    /// 겹쳐 놓은 Canvas(DragLayer·팝업 등)는 자기 카메라를 갖고 있지 않다. 맨 위 캔버스에서 읽는다.
    /// </summary>
    public static Camera CameraFor(RectTransform area)
    {
        Canvas canvas = area != null ? area.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;

        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}
