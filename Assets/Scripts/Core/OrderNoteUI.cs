using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 손님 주문 내역. Tab을 누르고 있는 동안 뜨고, 떼면 사라진다.
/// 조리 화면 상단의 ? 버튼으로도 열 수 있으며 그때는 토글이다.
///
/// 기본 레시피와 재료 속성은 여기 없다. 그쪽은 B 키로 여는 레시피 책이 맡는다.
/// </summary>
public class OrderNoteUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI dialogueText;

    /// <summary>미끄러져 들어오는 종이. 이것만 움직이고 root는 껐다 켜기만 한다.</summary>
    [SerializeField] private RectTransform panel;

    /// <summary>영수증 머리의 정보줄. 왼쪽은 며칠째, 오른쪽은 몇 번째 손님인지.</summary>
    [SerializeField] private TextMeshProUGUI dayLabel;
    [SerializeField] private TextMeshProUGUI customerLabel;

    /// <summary>일차와 손님 수를 읽어 온다. 읽기만 하고 건드리지 않는다.</summary>
    [SerializeField] private DayManager dayManager;

    /// <summary>
    /// 종이에서 글자를 뺀 나머지 높이. 머리글·구분선·정보줄·톱니를 다 더한 값이다.
    /// 빌더가 배치를 정하면서 같이 넣어 준다.
    /// </summary>
    [SerializeField] private float chromeHeight = 91f;

    /// <summary>글자가 쓸 수 있는 폭. 이 폭으로 재야 실제 줄 수가 나온다.</summary>
    [SerializeField] private float textWidth = 156f;

    /// <summary>종이 전체를 한꺼번에 흐리게 하는 데 쓴다. 빌더가 종이에 붙여 준다.</summary>
    [SerializeField] private CanvasGroup fade;

    /// <summary>마우스가 종이 위에 있을 때의 진하기. 뒤에 있는 재료가 비쳐 보이는 정도.</summary>
    [SerializeField, Range(0.1f, 1f)] private float hoverAlpha = 0.4f;

    /// <summary>흐려지고 돌아오는 데 걸리는 시간.</summary>
    [SerializeField] private float fadeSeconds = 0.1f;

    /// <summary>다 나왔을 때 종이 왼쪽에 남기는 여백.</summary>
    [SerializeField] private float shownMargin = 8f;

    /// <summary>다 나왔을 때 종이가 설 자리. 종이 폭에서 계산한다.</summary>
    private Vector2 shownPosition;

    /// <summary>숨었을 때 자리. 화면 왼쪽 바깥이라 종이가 안 보인다.</summary>
    private Vector2 hiddenPosition;

    /// <summary>미끄러지는 데 걸리는 시간.</summary>
    [SerializeField] private float slideSeconds = 0.18f;

    private Coroutine sliding;

    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        LayoutPositions();

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        if (panel != null) panel.anchoredPosition = Snap(hiddenPosition);
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 종이가 숨는 자리와 나오는 자리를 종이 폭에서 계산한다.
    ///
    /// 값을 적어 두면 종이 폭을 바꿀 때마다 같이 고쳐야 한다. 실제로 폭을 240에서 348로
    /// 넓혔더니 예전 값(-192)이 그대로 남아 종이 왼쪽이 46칸 화면 밖으로 나갔다.
    /// </summary>
    private void LayoutPositions()
    {
        if (panel == null) return;

        // 종이가 놓인 판. 화면과 같은 크기(640x360)고 가운데가 원점이다.
        var area = panel.parent as RectTransform;
        float half = area != null ? area.rect.width * 0.5f : 320f;
        float paperHalf = panel.sizeDelta.x * 0.5f;

        shownPosition = new Vector2(Mathf.Round(-half + shownMargin + paperHalf), 0f);

        // 숨을 때는 오른쪽 끝까지 화면 밖으로 나가야 한다. 조금 더 밀어 여유를 둔다.
        hiddenPosition = new Vector2(Mathf.Round(-half - paperHalf - 8f), 0f);
    }

    public void Show(string dialogue)
    {
        if (root != null) root.SetActive(true);
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrEmpty(dialogue) ? "(받은 주문이 없습니다)" : dialogue;
        }

        WriteHeader();
        FitPaper();

        // 흐려진 채로 숨었다가 다시 나오면 흐린 상태로 시작한다. 나올 때는 늘 진하게.
        if (fade != null) fade.alpha = 1f;

        Slide(shownPosition, false);
    }

    /// <summary>
    /// 마우스가 종이 위에 오면 흐려진다. 종이가 재료통과 그릇을 덮고 있어도
    /// 뒤가 비쳐 보여야 보면서 조리할 수 있다.
    /// </summary>
    private void Update()
    {
        if (fade == null || root == null || !root.activeSelf) return;

        float target = PointerOverPaper() ? hoverAlpha : 1f;

        // 팝업이 떠서 게임이 멈춰 있어도 주문서는 반응해야 한다.
        fade.alpha = fadeSeconds <= 0f
            ? target
            : Mathf.MoveTowards(fade.alpha, target, Time.unscaledDeltaTime / fadeSeconds);
    }

    /// <summary>
    /// 마우스가 종이 위에 있는가.
    ///
    /// 레이캐스트를 쓰지 않는다. 종이에 raycastTarget 을 켜는 순간 종이가 클릭을 가로채
    /// 뒤에 있는 재료통과 그릇을 못 만지게 된다. 그래서 상자 안에 들었는지만 좌표로 본다.
    /// 캔버스가 Screen Space - Overlay 라 카메라는 넘기지 않는다.
    /// </summary>
    private bool PointerOverPaper()
    {
        if (panel == null || Mouse.current == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            panel, Mouse.current.position.ReadValue(), null);
    }

    /// <summary>영수증 머리의 정보줄을 채운다.</summary>
    private void WriteHeader()
    {
        if (dayManager == null) return;

        if (dayLabel != null) dayLabel.text = dayManager.CurrentDay + "일차";

        // 손님 수는 "지금까지 낸 그릇 수"라, 지금 상대하는 손님은 그다음 번호다.
        if (customerLabel != null) customerLabel.text = (dayManager.CurrentCustomerCount + 1) + "번째 손님";
    }

    /// <summary>
    /// 대사 길이에 맞춰 종이 높이를 잡는다.
    ///
    /// 한 크기로 고정해 두면 짧은 주문에서 아래가 통째로 빈다. 진짜 영수증도 산 만큼
    /// 길어지니, 글이 끝나는 자리에서 톱니로 잘리는 편이 자연스럽다.
    ///
    /// 폭은 건드리지 않는다. 폭이 바뀌면 줄이 접히는 자리가 달라져 높이까지 같이 흔들린다.
    /// </summary>
    private void FitPaper()
    {
        if (dialogueText == null || panel == null) return;

        float content = Mathf.Ceil(dialogueText.GetPreferredValues(textWidth, 0f).y);
        dialogueText.rectTransform.sizeDelta = new Vector2(textWidth, content);

        // 종이는 가운데를 잡고 있어서 높이가 홀수면 위아래가 반칸씩 밀린다.
        // 픽셀아트라 반칸에 놓이면 테두리와 글자가 흐려진다. 짝수로 끊는다.
        float height = Mathf.Ceil((chromeHeight + content) * 0.5f) * 2f;

        panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
    }

    public void Hide()
    {
        // 여기서 바로 끄면 미끄러지는 게 안 보인다. 다 들어간 뒤에 끈다.
        Slide(hiddenPosition, true);
    }

    /// <summary>
    /// 종이를 목표 자리로 미끄러뜨린다.
    ///
    /// 자리는 정수 칸으로 끊는다. 픽셀아트라 반 칸에 놓이면 종이 테두리와 글자가 흐려진다.
    /// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰 있어도 주문 내역은 여닫혀야 한다.
    /// </summary>
    private void Slide(Vector2 target, bool disableWhenDone)
    {
        if (panel == null)
        {
            // 종이를 안 꽂아 줬으면 예전처럼 그냥 껐다 켠다.
            if (disableWhenDone && root != null) root.SetActive(false);
            return;
        }

        if (sliding != null) StopCoroutine(sliding);
        if (!gameObject.activeInHierarchy)
        {
            panel.anchoredPosition = Snap(target);
            if (disableWhenDone && root != null) root.SetActive(false);
            return;
        }

        sliding = StartCoroutine(SlideTo(target, disableWhenDone));
    }

    private IEnumerator SlideTo(Vector2 target, bool disableWhenDone)
    {
        Vector2 from = panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            float t = elapsed / slideSeconds;

            // 끝에서 부드럽게 멈춘다. 등속으로 움직이면 툭 하고 서는 느낌이 난다.
            t = 1f - (1f - t) * (1f - t);

            panel.anchoredPosition = Snap(Vector2.Lerp(from, target, t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        panel.anchoredPosition = Snap(target);
        sliding = null;

        if (disableWhenDone && root != null) root.SetActive(false);
    }

    private static Vector2 Snap(Vector2 v)
    {
        return new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));
    }

    public void Toggle(string dialogue)
    {
        if (IsOpen) Hide();
        else Show(dialogue);
    }
}
