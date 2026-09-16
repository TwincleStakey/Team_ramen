using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 「꾹 눌러서 넘기기」 게이지. 오른쪽 아래 구석에 도넛으로 뜬다.
///
/// 예전에는 연출 중에 왼쪽 버튼을 **한 번** 누르면 그 자리에서 건너뛰었다. 손이 미끄러져
/// 한 번 눌린 것으로 제일 센 연출(100점 우주)이 통째로 날아가는 것이 아까워서 바꿨다.
///
/// 한 번 누르면 게이지가 잠깐 떠서 "누르고 있으면 넘어간다"를 알려 주고, 누르고 있는 동안
/// 차오른다. 손을 떼면 도로 빠진다 — 반쯤 차 있다가 나중에 한 번 더 눌렀을 때 갑자기
/// 넘어가면 누른 사람도 놀란다.
///
/// 시간은 실시간으로 잰다(unscaled). 연출 중에는 게임이 멈춰 있을 수 있다.
/// </summary>
public class HoldToSkip : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.

    /// <summary>게이지 한 벌. 이 스크립트는 이 바깥에 붙어 있어야 껐다 켤 수 있다.</summary>
    [SerializeField] private GameObject root;

    /// <summary>차오르는 도넛. Filled · Radial360 로 만들어 fillAmount 만 만진다.</summary>
    [SerializeField] private Image ring;

    /// <summary>다 차는 데 걸리는 시간(초). 누르고 있는 시간이다.</summary>
    [SerializeField] private float holdSeconds = 1.2f;

    /// <summary>한 번 누르고 손을 뗐을 때 게이지가 남아 있는 시간(초).</summary>
    [SerializeField] private float visibleSeconds = 2.5f;

    /// <summary>손을 뗐을 때 게이지가 빠지는 속도(초당). 차는 것보다 빨라야 미련이 안 남는다.</summary>
    [SerializeField] private float drainPerSecond = 1.5f;

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 시간의 상한(초).
    ///
    /// unscaledDeltaTime 은 유니티가 안 잘라 준다. 무거운 프레임이 한 번 끼면 그 한 프레임에
    /// 게이지가 다 차서, 누르지도 않았는데 연출이 넘어간다.
    /// </summary>
    private const float MaxStep = 0.05f;

    private float fill;
    private float visibleLeft;

    private void Awake()
    {
        Hide();
    }

    /// <summary>게이지를 접고 처음 상태로 되돌린다.</summary>
    public void Hide()
    {
        fill = 0f;
        visibleLeft = 0f;

        if (ring != null) ring.fillAmount = 0f;
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 연출이 도는 동안 매 프레임 부른다. 다 찼으면 true 를 한 번 돌려주고 스스로 접는다.
    /// </summary>
    public bool Poll()
    {
        float step = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        // 누른 순간에도, 누르고 있는 동안에도 게이지는 떠 있어야 한다.
        // 누른 순간만 보면 꾹 누르고 있는 사이에 2.5초가 지나 게이지가 사라진다.
        bool held = Held();
        if (held || Pressed()) visibleLeft = visibleSeconds;

        fill = Mathf.Clamp01(held
            ? fill + step / Mathf.Max(0.01f, holdSeconds)
            : fill - step * drainPerSecond);

        visibleLeft -= step;

        // 다 빠진 뒤에도 남은 시간 동안은 빈 도넛이 떠 있다. 그게 "누르면 된다" 는 안내다.
        bool show = visibleLeft > 0f || fill > 0f;
        if (root != null && root.activeSelf != show) root.SetActive(show);
        if (ring != null) ring.fillAmount = fill;

        if (fill < 1f) return false;

        Hide();
        return true;
    }

    private static bool Pressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private static bool Held()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    }
}
