using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리를 누르는 비네트. **그림 한 장**이고, 정렬 순서로 UI 밑에 깔린다.
///
/// 처음에는 URP Volume 의 Vignette(카메라 후처리)로 넣었다가 내렸다. 후처리는 다 그려진
/// 화면에 먹이는 것이라 **UI 도 같이 눌린다.** 이 게임은 배경·손님·그릇뿐 아니라 상단바·
/// 키 힌트·팝업까지 전부 UI 라서, 세기 0.5 에서 캔버스 자리별 밝기를 재면 이렇게 나왔다
/// (1 이면 원본):
///
///   화면 한가운데 1.00 · 상단바 가운데 0.82 · 상단바 왼쪽 0.19
///   <b>상단바 오른쪽 0.00 · Tab 힌트 0.00</b> ← 완전히 검다
///
/// 세기를 낮추면 비네트가 안 보이고(0.28 이면 구석이 0.74) 올리면 UI 가 죽는다.
/// 둘 다 만족하려면 **UI 가 비네트 위에 그려져야** 하고, 그건 후처리로는 못 한다.
///
/// 그래서 그림으로 내리고 빌더가 정렬 순서를 이렇게 잡는다.
///
///   … 조리 세계(0~101) · 주문 화면(180) → <b>비네트(182)</b> → 상단바·키 힌트·주문 화면 상단 패널(183)
///   → 팝업(185) · 완벽 팻말(190) · 암전(300) · 주문마감(330)
///
/// 색온도(WhiteBalance)는 후처리에 그대로 둔다. 그쪽은 UI 까지 같이 따뜻해지는 편이 낫다.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenVignette : MonoBehaviour
{
    /// <summary>화면에 하나뿐이다.</summary>
    public static ScreenVignette Instance { get; private set; }

    /// <summary>
    /// 주문 화면에서의 세기(0~1). 그림이 세기 0.5 로 구워져 있어 1 이 곧 0.5 다.
    /// 밤 포장마차라 가장자리를 눌러도 어울린다.
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float orderStrength = 1f;

    /// <summary>
    /// 그 밖(조리 화면 등)에서의 세기.
    ///
    /// 조리 화면은 **밝은 나무 카운터**라 같은 세기로 누르면 답답하고, 재료통이 화면
    /// 가장자리에 줄지어 있어서 구석이 어두워지면 집기 나빠진다.
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float otherStrength = 0.56f;

    /// <summary>세기가 갈아타는 데 걸리는 시간(초). 툭 바뀌면 화면이 깜빡인 것처럼 보인다.</summary>
    [SerializeField] private float lerpSeconds = 0.4f;

    private Image image;
    private float now = -1f;
    private float target;

    private void Awake()
    {
        Instance = this;
        image = GetComponent<Image>();
        image.raycastTarget = false;

        target = otherStrength;      // 시작은 조리 화면 쪽이다
        now = target;
        ApplyAlpha();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 주문 화면에 들어왔는가. 화면을 여닫는 쪽(<see cref="OrderScreenUI"/>)이 알려 준다.
    ///
    /// 정적 메서드인 까닭 — 부르는 쪽마다 참조를 꽂아 주려면 빌더를 고쳐야 한다.
    /// 아직 없으면 그냥 지나간다.
    /// </summary>
    public static void SetOrderScreen(bool on)
    {
        if (Instance == null) return;

        Instance.target = on ? Instance.orderStrength : Instance.otherStrength;
    }

    /// <summary>시간은 실시간으로 잰다 — 팝업이 떠 게임이 멈춰도 갈아타야 한다.</summary>
    private void Update()
    {
        if (Mathf.Approximately(now, target)) return;

        // 한 프레임에 흘려보낼 시간을 자른다. Play 직후 첫 프레임이 4초를 넘기도 하는데,
        // 그대로 쓰면 갈아타는 것이 한 프레임에 끝나 버린다.
        float step = lerpSeconds <= 0f
            ? 1f
            : Mathf.Min(Time.unscaledDeltaTime, 0.05f) / lerpSeconds;

        now = Mathf.MoveTowards(now, target, step);
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        if (image == null) return;

        Color c = image.color;
        image.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(now));
    }
}
