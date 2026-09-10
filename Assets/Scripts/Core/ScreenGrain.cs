using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 덮는 그레인. 매 프레임 무늬가 갈려 필름처럼 자글거린다.
///
/// 알갱이를 게임 픽셀에 맞추는 일은 셰이더(Ramen/PixelGrain)가 한다. 여기서는
/// 매 프레임 노이즈를 한 칸씩 밀어 자글거리게 만들고, 세기를 정하는 것만 맡는다.
///
/// 세기는 늘 같다. 한때 손님이 화나면 두 배 반으로 거칠어지게 했는데, 결과창에서만
/// 화면 질감이 달라져 튀어 보였다. 지금 세기가 변하는 것은 폐기 순간의 한 번뿐이다.
///
/// RamenLayoutBuilder 가 Frame 의 마지막 자식으로 붙인다. 마지막이라야 커서와
/// 드래그 고스트 위에까지 덮인다.
///
/// 아래 세기 값들은 알파가 아니라 "화면 밝기가 몇 % 흔들리는가"다. 셰이더가 뒤 색에
/// 곱하는 방식이라 어두운 곳은 어둡게 남는다. 0.12 면 ±12% 다.
///
/// 그래서 그레인은 밝은 곳에서만 눈에 띈다. 흰 셔츠와 나무 반사에는 질감이 앉고,
/// 어두운 창밖은 그대로다. 밝기 200 짜리는 ±24 가 움직이지만 30 짜리는 ±3.6 뿐이다.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenGrain : MonoBehaviour
{
    /// <summary>
    /// 평소 진하기.
    ///
    /// 기획서의 0.06 은 회색을 알파로 덮던 시절 값이다. 곱하기로 바꾸면서 같은 숫자가
    /// 훨씬 약해졌다 — 재 보니 화면이 평균 1%, 최대 3% 밖에 안 움직여 사실상 안 보였다.
    /// 0.12 면 밝은 데에서 질감이 살고 어두운 데는 여전히 깨끗하다.
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float idleStrength = 0.12f;

    /// <summary>폐기 순간 한 프레임만 이만큼 튄다.</summary>
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.4f;

    /// <summary>튄 뒤 평소로 돌아오는 데 걸리는 시간.</summary>
    [SerializeField] private float flashSeconds = 0.25f;

    /// <summary>폐기 때 화면이 흔들리는 폭. 정수 칸으로만 흔든다.</summary>
    [SerializeField] private float shakePixels = 2f;

    /// <summary>흔들리는 시간.</summary>
    [SerializeField] private float shakeSeconds = 0.18f;

    /// <summary>흔들 대상. 판(Frame) 을 통째로 흔든다. 빌더가 꽂아 준다.</summary>
    [SerializeField] private RectTransform shakeTarget;

    /// <summary>노이즈 그림의 한 변. 셰이더가 칸 번호를 이 값으로 나눈다.</summary>
    [SerializeField] private float noiseSize = 64f;

    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private static readonly int NoiseSizeId = Shader.PropertyToID("_NoiseSize");
    private static readonly int GridId = Shader.PropertyToID("_Grid");

    /// <summary>
    /// 화면에 하나뿐이라 정적으로 잡아 둔다. 그릇과 결과창이 여기로 알려 온다.
    /// 참조를 꽂아 다니면 그레인을 끄고 켤 때마다 배선이 끊어진다.
    /// </summary>
    public static ScreenGrain Instance { get; private set; }

    private Material material;
    private Vector2 shakeHome;
    private float flashLeft;
    private float shakeLeft;

    private void Awake()
    {
        Instance = this;

        var image = GetComponent<Image>();
        image.raycastTarget = false;

        // 공유 머티리얼을 그대로 만지면 에셋 파일이 바뀌어 버린다. 인스턴스를 하나 떠서 쓴다.
        material = image.material != null ? new Material(image.material) : null;
        if (material != null) image.material = material;

        if (shakeTarget != null) shakeHome = shakeTarget.anchoredPosition;
    }

    private void Update()
    {
        if (material == null) return;

        material.SetFloat(NoiseSizeId, noiseSize);

        // 격자는 판 크기 그대로다. 판이 640x360 이므로 알갱이 하나가 게임 픽셀 하나가 된다.
        Vector2 grid = shakeTarget != null ? shakeTarget.rect.size : new Vector2(640f, 360f);
        material.SetVector(GridId, new Vector4(grid.x, grid.y, 0f, 0f));

        // 노이즈를 통째로 밀어 알갱이를 갈아 끼운다. 정수로 밀어야 칸이 어긋나지 않는다.
        material.SetVector(OffsetId, new Vector4(
            Mathf.Floor(Random.value * noiseSize), Mathf.Floor(Random.value * noiseSize), 0f, 0f));

        float strength = idleStrength;
        if (flashLeft > 0f)
        {
            flashLeft -= Time.unscaledDeltaTime;
            strength = Mathf.Lerp(idleStrength, flashStrength, Mathf.Clamp01(flashLeft / flashSeconds));
        }

        material.SetFloat(StrengthId, strength);
        UpdateShake();
    }

    /// <summary>
    /// 화면 흔들기. 픽셀아트라 정수 칸으로만 흔든다.
    /// 소수로 흔들면 화면 전체가 반칸에 걸려 모든 글자와 테두리가 한꺼번에 흐려진다.
    /// </summary>
    private void UpdateShake()
    {
        if (shakeTarget == null) return;

        if (shakeLeft <= 0f)
        {
            shakeTarget.anchoredPosition = shakeHome;
            return;
        }

        shakeLeft -= Time.unscaledDeltaTime;

        float amount = Mathf.Round(shakePixels * Mathf.Clamp01(shakeLeft / shakeSeconds));
        shakeTarget.anchoredPosition = shakeHome + new Vector2(
            Mathf.Round(Random.Range(-amount, amount)),
            Mathf.Round(Random.Range(-amount, amount)));
    }

    /// <summary>그릇을 버렸을 때. 한 번 튀고 화면이 흔들린다.</summary>
    public void Flash()
    {
        flashLeft = flashSeconds;
        shakeLeft = shakeSeconds;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
