using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 전체에 먹이는 색보정. 중간톤을 띄워 빛바랜 필름처럼 만든다.
///
/// 2026-09-11 에 녹화 경로에서 sRGB 변환이 한 번 더 걸려 나온 색을 그대로 쓰기로 해서 넣었다.
///
/// URP 의 Lift Gamma Gain 을 쓴다. 처음에는 카메라에 OnRenderImage 를 붙였는데 한 번도 안 불렸다 —
/// 이 프로젝트는 GraphicsSettings 기본값은 빌트인인데 품질 설정 쪽에 URP 에셋이 물려 있어서
/// 실제로는 URP 로 돈다. URP 에서 OnRenderImage 는 아예 호출되지 않는다.
///
/// 캔버스가 Screen Space - Camera 여야 한다. Overlay 는 카메라가 다 그린 뒤에 따로 얹혀서
/// 후처리를 통과하지 않는다 — 빌더가 캔버스를 카메라에 물려 준다.
///
/// 그레인(ScreenGrain)과 하는 일이 다르다. 그쪽은 알갱이를 곱해 질감을 내고,
/// 이쪽은 밝기 곡선을 바꾼다. 그레인은 캔버스 위 판이라 이 보정보다 먼저 섞인다.
/// </summary>
public class ScreenGrade : MonoBehaviour
{
    /// <summary>화면에 하나뿐이다. 설정 창이 여기로 값을 알려 온다.</summary>
    public static ScreenGrade Instance { get; private set; }

    /// <summary>설정에 저장하는 이름.</summary>
    private const string PrefKey = "ramen.filter.step";

    /// <summary>눈금 수. 0 이면 보정 없음, 10 이면 가장 강하다.</summary>
    public const int MaxStep = 10;

    /// <summary>
    /// 눈금 한 칸이 감마 얼마인가.
    ///
    /// 고른 값이 아니라 재서 맞춘 값이다. 목표로 삼은 색(원본에 감마 1/2.2)과 화면을 실제로
    /// 대 보며 훑었더니 gamma.w 1.2~1.3 에서 가장 가까웠다(평균 오차 8.9/255). 기본 6 칸이
    /// 거기에 떨어지도록 0.21 로 잡았다.
    /// </summary>
    private const float GammaPerStep = 0.21f;

    /// <summary>빌더가 꽂아 준다. 전역 Volume 하나다.</summary>
    [SerializeField] private Volume volume;

    /// <summary>
    /// 기본은 꺼짐이다. 2026-09-14 에 되돌렸다 — 6 칸(감마 1.26)은 중간톤을 띄워
    /// 화면이 전체적으로 밝고 색이 빠져 보였다. 필터를 넣기 전 화면이 기본이고,
    /// 원하는 사람은 설정에서 올린다.
    /// </summary>
    [SerializeField, Range(0, MaxStep)] private int step = 0;

    private LiftGammaGain grade;

    /// <summary>후처리를 걸 카메라. 처음 쓸 때 한 번만 찾는다.</summary>
    private UniversalAdditionalCameraData cameraData;

    public int Step
    {
        get { return step; }
    }

    /// <summary>눈금을 바꾸고 바로 저장한다. 설정 창이 부른다.</summary>
    public void SetStep(int value)
    {
        step = Mathf.Clamp(value, 0, MaxStep);

        PlayerPrefs.SetInt(PrefKey, step);
        PlayerPrefs.Save();

        Apply();
    }

    private void Awake()
    {
        Instance = this;

        if (PlayerPrefs.HasKey(PrefKey))
        {
            step = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, MaxStep);
        }

        Apply();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 후처리를 걸 카메라를 찾는다. 빌더가 쓰는 것과 같은 순서다.
    /// </summary>
    private UniversalAdditionalCameraData CameraData()
    {
        if (cameraData != null) return cameraData;

        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return null;

        cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
        return cameraData;
    }

    private void Apply()
    {
        // 0 칸에서는 후처리 자체를 끈다. 효과만 끄는 것으로는 모자라다 — URP 는 후처리가
        // 켜져 있으면 활성 효과가 하나도 없어도 색보정 LUT 를 한 번 거치게 해서,
        // 옅게나마 색이 달라진다. 필터를 넣기 전 화면으로 정확히 돌아가려면 통째로 꺼야 한다.
        UniversalAdditionalCameraData camera = CameraData();
        if (camera != null) camera.renderPostProcessing = step > 0;

        if (volume == null) return;

        // profile 은 실행 중에 이 Volume 만의 사본을 만들어 준다. sharedProfile 을 만지면
        // 에셋 파일이 바뀌어, 플레이 중에 고친 값이 프로젝트에 그대로 남는다.
        VolumeProfile profile = volume.profile;
        if (profile == null) return;

        if (grade == null && !profile.TryGet(out grade)) return;

        // 0 칸은 효과를 아예 끈다. 감마 0 이 곧 원본이라는 보장이 없어서, 끄는 쪽이 확실하다.
        grade.active = step > 0;
        grade.gamma.overrideState = true;
        grade.gamma.value = new Vector4(1f, 1f, 1f, GammaPerStep * step);
    }
}
