using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임을 열면서 한 번 묻는 「튜토리얼을 보시겠습니까?」.
///
/// 도입부 내레이션이 끝나고 「N일차」 자막이 뜨기 전, **검은 화면에 이 판만** 뜬다.
/// 가게는 아직 안 열렸으니 뒤에 비칠 것이 없고, 그래서 뒷판도 깔지 않는다 —
/// 화면을 덮고 있는 것은 ScreenFade 와 내레이션 판이다.
///
/// 폐기·마무리 확인창(ConfirmDialogUI)과 모양은 같지만 코드를 나눠 둔다. 그쪽은 그릇에
/// 묶여 있고 timeScale 을 0 으로 눌러서, 하루가 시작되기도 전인 이 자리에는 안 맞는다.
///
/// 시간은 실시간으로 잰다(unscaled).
/// </summary>
public class TutorialAskUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.

    /// <summary>판 한 벌. 이 스크립트는 이 바깥에 붙어 있어야 껐다 켤 수 있다.</summary>
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    /// <summary>판이 떠오르고 스러지는 데 걸리는 시간(초).</summary>
    [SerializeField] private float fadeSeconds = 0.35f;

    private const float MaxStep = 0.05f;

    /// <summary>아직 안 고름 0 · 넵 1 · 아뇨 2.</summary>
    private int picked;

    private void Awake()
    {
        if (yesButton != null) yesButton.onClick.AddListener(PickYes);
        if (noButton != null) noButton.onClick.AddListener(PickNo);

        Hide();
    }

    private void OnDestroy()
    {
        if (yesButton != null) yesButton.onClick.RemoveListener(PickYes);
        if (noButton != null) noButton.onClick.RemoveListener(PickNo);
    }

    private void PickYes() { picked = 1; }
    private void PickNo() { picked = 2; }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 묻고 답을 받을 때까지 기다린다. 「넵」이면 true.
    ///
    /// 판이 없으면(빌더를 안 돌린 씬) 묻지 않고 true 로 지나간다 — 튜토리얼을 보여 주는 쪽이
    /// 안전한 기본값이다. 처음 여는 사람이 아무것도 못 배우는 것보다는 한 번 더 보는 편이 낫다.
    /// </summary>
    public IEnumerator Ask(System.Action<bool> answer)
    {
        if (root == null || yesButton == null || noButton == null)
        {
            if (answer != null) answer(true);
            yield break;
        }

        picked = 0;
        root.SetActive(true);
        yield return Fade(0f, 1f);

        while (picked == 0) yield return null;

        bool yes = picked == 1;

        // 누르는 소리는 여기서 내지 않는다. 버튼에 붙은 ButtonPress 가 이미 sfx_ui_press 를
        // 내고 있어서, 여기서 한 번 더 부르면 같은 소리가 두 번 겹쳐 울린다.

        yield return Fade(1f, 0f);
        Hide();

        if (answer != null) answer(yes);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds));
            yield return null;
        }

        group.alpha = to;
    }
}
