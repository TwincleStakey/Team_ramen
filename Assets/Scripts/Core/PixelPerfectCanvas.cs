using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캔버스를 게임 픽셀 격자에 맞춰 정수 배율로만 띄운다.
///
/// CanvasScaler의 Scale With Screen Size는 창 크기에 맞춰 0.52 같은 어중간한 배율을 만든다.
/// 그러면 64픽셀 그림이 33.4픽셀로 그려지면서 어떤 줄은 2픽셀, 어떤 줄은 1픽셀이 되어
/// 픽셀아트가 지저분해진다. 그래서 배율을 1, 2, 3처럼 정수로만 끊는다.
///
/// 배율이 정수면 원본 1픽셀이 화면에서 항상 정확히 N×N 정사각형이 된다.
/// 기준 격자(640×360)가 화면에 몇 번 들어가는지를 내림한 값이 곧 배율이다.
/// </summary>
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
[ExecuteAlways]
public class PixelPerfectCanvas : MonoBehaviour
{
    /// <summary>기준 격자. 이 안에서 화면을 짠다.</summary>
    public Vector2Int referenceResolution = new Vector2Int(640, 360);

    private CanvasScaler scaler;
    private Vector2Int lastScreen;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void Update()
    {
        // 창 크기가 바뀔 때만 계산한다. 매 프레임 스케일러를 건드리면 캔버스가 통째로 다시 그려진다.
        var now = new Vector2Int(Screen.width, Screen.height);
        if (now == lastScreen) return;

        lastScreen = now;
        Apply();
    }

    private void Apply()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler == null) return;

        // 스케일러가 화면 크기를 따라가면 여기서 정한 배율을 덮어써 버린다.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        int scale = Mathf.Min(Screen.width / Mathf.Max(1, referenceResolution.x),
                              Screen.height / Mathf.Max(1, referenceResolution.y));

        // 기준 격자보다 작은 창에서도 그림은 나와야 하므로 1 밑으로는 안 내린다.
        scaler.scaleFactor = Mathf.Max(1, scale);
    }
}
