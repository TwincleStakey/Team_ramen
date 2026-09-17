using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 개발용 단축키(F1~F8)에 걸어 둔 자물쇠. <b>평소에는 잠겨 있고 조합키로만 열린다.</b>
///
/// 예전에는 키 자체가 <c>#if UNITY_EDITOR</c> 안에 있어 빌드에는 아예 없었다. 그러면
/// 빌드를 받은 사람이 5일차나 크레딧을 확인할 길이 없다. 그렇다고 늘 열어 두면 플레이하다
/// F5 를 잘못 눌러 하루가 통째로 넘어간다.
///
///   <b>Ctrl + Shift + D</b> — 열고 닫는다. 열려 있는 동안만 F1~F8 이 듣는다.
///
/// 로그도 여기서 같이 다룬다. 빌드에서는 Log·Warning 을 막고 Error 위쪽만 남긴다 —
/// 주문 하나마다 색깔 붙은 긴 줄이 여러 개 찍히는데 그걸 볼 사람이 없다. 자물쇠를 열면
/// 다시 다 찍힌다.
/// </summary>
public static class DevKeys
{
    /// <summary>지금 개발 키가 듣는가.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>열리고 닫힐 때마다 알린다. 화면 구석의 표시가 이걸 듣는다.</summary>
    public static event System.Action Changed;

    /// <summary>
    /// 한 프레임에 한 번만 본다.
    ///
    /// 부르는 곳이 둘이다(타이틀 화면과 GameManager). 둘 다 같은 프레임에 보면 조합키를
    /// 한 번 눌렀는데 두 번 뒤집혀 아무 일도 안 일어난다.
    /// </summary>
    private static int seenFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Enabled = false;

        // 에디터에서는 늘 다 찍는다. 빌드에서만 조용히 둔다.
        Debug.unityLogger.filterLogType = Application.isEditor ? LogType.Log : LogType.Error;
    }

    /// <summary>조합키를 눌렀는지 본다. 매 프레임 도는 쪽에서 부른다.</summary>
    public static void Poll(Keyboard keyboard)
    {
        if (keyboard == null) return;
        if (seenFrame == Time.frameCount) return;
        seenFrame = Time.frameCount;

        if (!keyboard.dKey.wasPressedThisFrame) return;

        bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        if (!ctrl || !shift) return;

        Enabled = !Enabled;
        Debug.unityLogger.filterLogType = Enabled || Application.isEditor ? LogType.Log : LogType.Error;

        Debug.Log(Enabled
            ? "[개발 키] 열렸습니다. F1 시작 · F2 정답 그릇 · F3 손님 넘기기 · F4 튜토리얼 건너뛰기 · "
              + "F5 마감 · F6 크레딧 · F7 목표 미달 마감 · F8 5일차까지"
            : "[개발 키] 잠갔습니다.");

        if (Changed != null) Changed();
    }
}
