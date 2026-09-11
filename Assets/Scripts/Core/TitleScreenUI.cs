using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임을 켜면 맨 처음 뜨는 화면. 로고와 버튼 넷이 전부다.
///
/// 씬을 따로 만들지 않고 조리 화면 위에 덮개로 올린다. 씬을 나누면 빌드 설정과 씬 전환,
/// 저장 시점이 한꺼번에 얽히는데, GameManager 에는 이미 "우리가 정한 때에 하루를 연다" 는
/// 장치가 있어서(HoldFirstDay) 거기에 한 칸만 더 얹으면 된다.
///
/// [게임시작] 을 누르기 전에는 튜토리얼도 1일차도 시작하지 않는다.
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;

    /// <summary>버튼 줄 둘. 같은 자리에 겹쳐 두고 한 번에 하나만 켠다.</summary>
    [SerializeField] private GameObject mainRow;
    [SerializeField] private GameObject modeRow;

    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [SerializeField] private Button storyButton;
    [SerializeField] private Button endlessButton;
    [SerializeField] private Button backButton;

    /// <summary>[설정] 창. 화면 필터 세기를 여기서 고른다.</summary>
    [SerializeField] private SettingsUI settings;

    /// <summary>크레딧·무한 모드에 띄울 쪽지. 아직 내용이 없어 안내만 한다.</summary>
    [SerializeField] private GameObject noticeRoot;
    [SerializeField] private TMPro.TextMeshProUGUI noticeText;
    [SerializeField] private Button noticeCloseButton;

    /// <summary>시작 화면이 아직 떠 있는가. GameManager 가 이걸 보고 게임을 붙잡아 둔다.</summary>
    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(ShowModes);
        if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
        if (creditsButton != null) creditsButton.onClick.AddListener(ShowCredits);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);

        if (storyButton != null) storyButton.onClick.AddListener(StartStory);
        if (endlessButton != null) endlessButton.onClick.AddListener(ShowEndless);
        if (backButton != null) backButton.onClick.AddListener(ShowMain);

        if (noticeCloseButton != null) noticeCloseButton.onClick.AddListener(CloseNotice);

        if (root != null) root.SetActive(true);
        if (noticeRoot != null) noticeRoot.SetActive(false);

        ShowMain();
    }

    /// <summary>
    /// 개발용 건너뛰기. F1 을 누르면 [게임시작] 을 누른 것과 같다.
    ///
    /// 에디터에서 화면 하나를 고칠 때마다 타이틀을 눌러 지나가야 하는 것이 번거로워서 둔다.
    /// 에디터에서만 듣는다 — 빌드에서는 이 키가 아예 없다.
    /// </summary>
    private void Update()
    {
#if UNITY_EDITOR
        if (!IsOpen) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) StartStory();
#endif
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(ShowModes);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
        if (creditsButton != null) creditsButton.onClick.RemoveListener(ShowCredits);
        if (quitButton != null) quitButton.onClick.RemoveListener(Quit);

        if (storyButton != null) storyButton.onClick.RemoveListener(StartStory);
        if (endlessButton != null) endlessButton.onClick.RemoveListener(ShowEndless);
        if (backButton != null) backButton.onClick.RemoveListener(ShowMain);

        if (noticeCloseButton != null) noticeCloseButton.onClick.RemoveListener(CloseNotice);
    }

    /// <summary>[게임시작] — 모드를 고르는 줄로 갈아 끼운다.</summary>
    private void ShowModes()
    {
        if (mainRow != null) mainRow.SetActive(false);
        if (modeRow != null) modeRow.SetActive(true);
    }

    /// <summary>[뒤로] — 처음 줄로 돌아간다.</summary>
    private void ShowMain()
    {
        if (mainRow != null) mainRow.SetActive(true);
        if (modeRow != null) modeRow.SetActive(false);
    }

    private void ShowEndless()
    {
        ShowNotice("무한 모드는 아직 준비 중입니다.");
    }

    /// <summary>
    /// [스토리 모드].
    ///
    /// 여기서 시작 화면을 끄지 않는다. 끄면 검게 덮이기 전에 조리 화면이 드러나 버린다.
    /// 화면이 다 검어진 뒤에 GameManager 가 Close 를 불러 치운다.
    /// </summary>
    private void StartStory()
    {
        if (storyButton != null) storyButton.interactable = false;

        if (GameManager.Instance != null) GameManager.Instance.BeginGame();
        else Debug.LogWarning("[시작 화면] 씬에 GameManager 가 없습니다.");
    }

    /// <summary>시작 화면을 치운다. 화면이 검게 덮인 동안 GameManager 가 부른다.</summary>
    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void ShowSettings()
    {
        if (settings != null) settings.Open();
        else ShowNotice("설정은 아직 준비 중입니다.");
    }

    private void ShowCredits()
    {
        ShowNotice("크레딧은 아직 준비 중입니다.");
    }

    private void ShowNotice(string message)
    {
        if (noticeText != null) noticeText.text = message;
        if (noticeRoot != null) noticeRoot.SetActive(true);
    }

    private void CloseNotice()
    {
        if (noticeRoot != null) noticeRoot.SetActive(false);
    }

    /// <summary>
    /// 게임을 끈다. 빌드에서는 창이 닫히고, 에디터에서는 Play 를 멈춘다.
    /// Application.Quit 은 에디터에서 아무 일도 하지 않아서 눌러도 반응이 없어 보인다.
    /// </summary>
    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
