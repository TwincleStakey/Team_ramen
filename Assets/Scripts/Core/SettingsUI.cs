using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [설정] 창. 배경음·효과음·화면 필터 세 줄이다.
///
/// **시작 화면과 인게임이 같은 창을 쓴다.** 그래서 시작 화면 밑이 아니라 캔버스 바로 밑에
/// 따로 서 있고, 정렬 순서로 모든 화면 위에 그려진다(빌더의 <c>SettingsOrder</c>).
/// 시작 화면에서는 [설정] 버튼이, 인게임에서는 ESC 가 연다.
///
/// 값은 눈금 0~10 으로 끊는다. 셋 다 같은 눈금이라 한 위젯(<see cref="SettingsRow"/>)으로
/// 그린다 — <see cref="Sfx.MaxVolumeStep"/> 과 <see cref="ScreenGrade.MaxStep"/> 이 둘 다 10 이다.
///
/// 저장은 값을 쥔 쪽이 한다(Sfx·ScreenGrade 가 각자 PlayerPrefs 에 넣는다).
/// 닫을 때 저장하는 식이면 창을 그냥 닫았을 때 방금 고른 값이 사라진다.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private SettingsRow bgmRow;
    [SerializeField] private SettingsRow sfxRow;
    [SerializeField] private SettingsRow filterRow;
    [SerializeField] private Button closeButton;

    /// <summary>
    /// 메인 화면으로 나가는 문. <b>인게임에서만 뜬다</b> — 시작 화면에서 열었을 때는 감춘다.
    /// 이미 시작 화면인데 「시작 화면으로 나가기」가 있으면 아무 데도 안 가는 버튼이 된다.
    /// </summary>
    [SerializeField] private Button exitButton;

    /// <summary>「나가시겠습니까?」 를 묻는 작은 창. 설정창 안에 들어 있다.</summary>
    [SerializeField] private GameObject exitConfirm;
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;

    /// <summary>
    /// 시작 화면. 문을 띄울지 말지 판단하는 데만 읽는다.
    ///
    /// 시작 화면이 떠 있으면 그쪽에서 연 설정이고, 아니면 인게임에서 ESC 로 연 것이다.
    /// 창 하나를 둘이 나눠 쓰기 때문에 여는 쪽을 따로 기억해 두는 것보다 이쪽이 틀릴 일이 적다.
    /// </summary>
    [SerializeField] private TitleScreenUI titleScreen;

    /// <summary>창이 떠 있는가. 다른 쪽이 「지금 설정 중」인지 물어볼 수 있게 열어 둔다.</summary>
    public bool IsOpen { get { return root != null && root.activeSelf; } }

    /// <summary>
    /// 열기 전의 시간 배속. 닫을 때 이 값으로 되돌린다.
    ///
    /// 1 로 못 박으면 안 된다 — 확인창처럼 이미 0 으로 내려 둔 것 위에서 설정을 열었다가
    /// 닫으면, 확인창이 아직 떠 있는데 게임이 다시 흐른다.
    /// </summary>
    private float resumeScale = 1f;

    private void Awake()
    {
        Bind(bgmRow, ChangeBgm);
        Bind(sfxRow, ChangeSfx);
        Bind(filterRow, ChangeFilter);

        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (exitButton != null) exitButton.onClick.AddListener(AskExit);
        if (exitYesButton != null) exitYesButton.onClick.AddListener(ExitToTitle);
        if (exitNoButton != null) exitNoButton.onClick.AddListener(CancelExit);

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버려
        // Awake 가 안 돌고 버튼에 손이 안 붙는다(실제로 한 번 그렇게 만들었다).
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        Unbind(bgmRow, ChangeBgm);
        Unbind(sfxRow, ChangeSfx);
        Unbind(filterRow, ChangeFilter);

        if (closeButton != null) closeButton.onClick.RemoveListener(Close);

        if (exitButton != null) exitButton.onClick.RemoveListener(AskExit);
        if (exitYesButton != null) exitYesButton.onClick.RemoveListener(ExitToTitle);
        if (exitNoButton != null) exitNoButton.onClick.RemoveListener(CancelExit);
    }

    private static void Bind(SettingsRow row, System.Action<int> move)
    {
        if (row == null) return;

        if (row.Minus != null) row.Minus.onClick.AddListener(delegate { move(-1); });
        if (row.Plus != null) row.Plus.onClick.AddListener(delegate { move(1); });
    }

    private static void Unbind(SettingsRow row, System.Action<int> move)
    {
        if (row == null) return;

        if (row.Minus != null) row.Minus.onClick.RemoveAllListeners();
        if (row.Plus != null) row.Plus.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// ESC 로 여닫는다. 시작 화면에서도 인게임에서도 같다.
    ///
    /// 멈춰 있는 동안에도 키를 받아야 하므로 Update 에서 본다 — timeScale 이 0 이어도
    /// Update 는 계속 돈다(FixedUpdate 만 멈춘다).
    /// </summary>
    private void Update()
    {
        // ⚠️ 옛 Input 클래스(Input.GetKeyDown)를 쓰면 안 된다. 이 프로젝트는 Player Settings 에서
        // Input System 으로 넘어가 있어서, 저쪽을 읽는 순간 매 프레임 InvalidOperationException 이
        // 터진다. 예외가 Update 를 끊어 그 뒤 줄이 통째로 안 돌고, 콘솔도 그 예외로 뒤덮인다.
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        // ⚠️ 크레딧이 도는 동안에는 안 듣는다.
        //
        // 크레딧은 시계를 **실시간(unscaled)**으로 본다. 그래서 설정창이 Time.timeScale 을
        // 0 으로 눌러도 크레딧은 뒤에서 그대로 굴러가고, 설정창만 그 위에 얹힌 채 남는다.
        // 멈추지도 않으면서 화면만 가리는 셈이라 어느 쪽으로도 맞는 그림이 아니다.
        //
        // 크레딧은 「끝까지 보는 것」이고 중간에 손댈 것이 없다. 닫을 길이 없어 갇히는 것도
        // 아니다 — 끝나면 저절로 씬을 다시 열어 시작 화면으로 돌아간다.
        if (CreditsSequence.Running)
        {
            // 크레딧이 시작되기 전에 열어 둔 창이 있으면 그것만 닫아 준다.
            // 열린 채로 두면 timeScale 이 0 에 눌린 채 씬이 다시 열린다.
            if (IsOpen) Close();
            return;
        }

        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (root == null || root.activeSelf) return;

        // 조리 중에 설정을 보다 손님을 놓치면 억울하다. 창이 떠 있는 동안 시간을 멈춘다.
        // 창 자체는 실시간으로 도는 것들만 쓰므로(버튼·TMP) 멈춰도 멀쩡하다.
        resumeScale = Time.timeScale;
        Time.timeScale = 0f;

        root.SetActive(true);

        // 나가는 문은 인게임에서만. 시작 화면에서 연 설정에는 갈 곳이 없다.
        if (exitButton != null)
        {
            bool inGame = titleScreen == null || !titleScreen.IsOpen;
            exitButton.gameObject.SetActive(inGame);
        }

        // 지난번에 묻다 만 창이 남아 있으면 지운다.
        if (exitConfirm != null) exitConfirm.SetActive(false);

        Refresh();
    }

    public void Close()
    {
        if (root == null || !root.activeSelf) return;

        if (exitConfirm != null) exitConfirm.SetActive(false);

        root.SetActive(false);
        Time.timeScale = resumeScale;
    }

    /// <summary>
    /// 문을 눌렀다. 바로 안 나가고 한 번 묻는다.
    ///
    /// 저장이 꺼져 있어(GameManager.saveEnabled) 나가면 그 판은 되돌릴 길이 없다.
    /// 게다가 이 문은 [닫기] 바로 옆이라 잘못 누르기 쉽다.
    /// </summary>
    private void AskExit()
    {
        if (exitConfirm != null) exitConfirm.SetActive(true);
    }

    private void CancelExit()
    {
        if (exitConfirm != null) exitConfirm.SetActive(false);
    }

    /// <summary>
    /// 시작 화면으로 돌아간다. 씬을 다시 여는 것이 곧 시작 화면이다
    /// (TitleScreenUI 가 씬이 열릴 때 스스로 뜬다). 크레딧이 끝나고 돌아가는 길과 같다.
    /// </summary>
    private void ExitToTitle()
    {
        // **배속을 먼저 되돌린다.** 설정창이 0 으로 눌러 둔 채로 씬을 열면 새 판이 멈춘 채 뜬다.
        Time.timeScale = 1f;

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // 고른 크기를 귀로 바로 확인할 수 있다.
    //   배경음 — Sfx 가 돌고 있는 루프에 새 배율을 바로 먹인다
    //   효과음 — 화살표를 누르는 소리(ButtonPress 의 sfx_ui_press)가 이미 새 크기로 난다
    // 그래서 여기서 확인용 소리를 따로 내지 않는다. 내면 누를 때마다 두 번 겹친다.

    private void ChangeBgm(int delta)
    {
        Sfx.BgmStep = Sfx.BgmStep + delta;
        Refresh();
    }

    private void ChangeSfx(int delta)
    {
        Sfx.SfxStep = Sfx.SfxStep + delta;
        Refresh();
    }

    private void ChangeFilter(int delta)
    {
        ScreenGrade grade = ScreenGrade.Instance;
        if (grade == null) return;

        grade.SetStep(grade.Step + delta);
        Refresh();
    }

    /// <summary>세 줄을 지금 값에 맞춘다.</summary>
    private void Refresh()
    {
        if (bgmRow != null) bgmRow.Show(Sfx.BgmStep);
        if (sfxRow != null) sfxRow.Show(Sfx.SfxStep);

        ScreenGrade grade = ScreenGrade.Instance;
        if (filterRow != null) filterRow.Show(grade != null ? grade.Step : 0);
    }
}
