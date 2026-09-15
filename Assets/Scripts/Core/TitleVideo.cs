using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 시작 화면 배경 영상. 첫 프레임이 나온 뒤에야 화면에 올린다.
///
/// <see cref="VideoPlayer"/> 는 <see cref="RenderTexture"/> 에 그리는데, 첫 프레임이 나오기 전까지
/// 그 판은 **검다.** 그대로 두면 게임을 켤 때마다 시작 화면이 한 번 검게 깜빡인다.
/// 플레이어가 제일 먼저 보는 화면이라 그 한 컷이 싸구려로 보인다.
///
/// 그래서 영상 판은 꺼 둔 채로 시작하고, 뒤에 첫 프레임을 구워 둔 그림 한 장을 깔아 둔다.
/// 영상이 나오면 판을 켜고 그림을 끈다. 같은 그림이라 바뀌는 순간이 안 보인다.
///
/// 영상이 아예 안 열리면(코덱이 없거나 파일이 빠졌거나) 그림이 그대로 남는다.
/// 배경이 검게 비는 것보다 멈춰 있는 편이 낫다.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class TitleVideo : MonoBehaviour
{
    /// <summary>영상이 그려지는 판. 첫 프레임이 나오기 전에는 꺼 둔다.</summary>
    [SerializeField] private RawImage screen;

    /// <summary>영상 첫 프레임을 구워 둔 그림. 영상이 나오면 끈다.</summary>
    [SerializeField] private Graphic still;

    private VideoPlayer player;

    private void Awake()
    {
        player = GetComponent<VideoPlayer>();

        if (screen != null) screen.enabled = false;
        if (still != null) still.enabled = true;
    }

    /// <summary>
    /// 켜질 때마다 처음부터 다시 튼다.
    ///
    /// <c>playOnAwake</c> 는 이름 그대로 Awake 때 한 번만 듣는다. 시작 화면을 닫았다
    /// 다시 열면(<see cref="TitleScreenUI"/> 가 root 를 껐다 켠다) 영상은 멈춘 채로 남고,
    /// 첫 프레임을 구워 둔 그림만 계속 보인다 — 배경이 정지 화면이 된다.
    /// </summary>
    private void OnEnable()
    {
        if (screen != null) screen.enabled = false;
        if (still != null) still.enabled = true;

        if (player != null && !player.isPlaying) player.Play();
    }

    /// <summary>
    /// 첫 프레임이 나왔는지 지켜본다.
    ///
    /// <c>prepareCompleted</c> 를 안 쓴다. 델리게이트를 걸기 전에 준비가 끝나 버리면 그 신호가
    /// 영영 안 와서, 영상이 캐시에 올라와 있을 때 오히려 배경이 안 뜬다.
    /// <c>frame</c> 이 0을 넘었는지 보는 편이 어느 경우에나 맞는다.
    /// </summary>
    private void Update()
    {
        if (screen == null || screen.enabled) return;
        if (player == null || !player.isPrepared || player.frame <= 0) return;

        screen.enabled = true;
        if (still != null) still.enabled = false;

        // 여기서 컴포넌트를 꺼 버리면 안 된다. 꺼진 컴포넌트에는 OnEnable 이 다시 안 와서,
        // 시작 화면을 닫았다 여는 순간 영상이 멈춘 채로 남는다. 위의 두 줄이 곧 빗장이라
        // 켜 둔 채로도 하는 일이 없다.
    }
}
