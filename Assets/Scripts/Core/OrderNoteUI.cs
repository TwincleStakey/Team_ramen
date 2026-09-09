using System.Collections;
using TMPro;
using UnityEngine;

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

    /// <summary>
    /// 다 나왔을 때 종이가 설 자리. 화면 왼쪽에 여백 8칸만 두고 붙는다.
    /// 종이 폭이 240 이라 왼쪽 끝이 8, 오른쪽 끝이 248 이다.
    /// </summary>
    [SerializeField] private Vector2 shownPosition = new Vector2(-192f, 0f);

    /// <summary>숨었을 때 자리. 화면 왼쪽 바깥이라 종이가 안 보인다.</summary>
    [SerializeField] private Vector2 hiddenPosition = new Vector2(-440f, 0f);

    /// <summary>미끄러지는 데 걸리는 시간.</summary>
    [SerializeField] private float slideSeconds = 0.18f;

    private Coroutine sliding;

    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        if (panel != null) panel.anchoredPosition = Snap(hiddenPosition);
        if (root != null) root.SetActive(false);
    }

    public void Show(string dialogue)
    {
        if (root != null) root.SetActive(true);
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrEmpty(dialogue) ? "(받은 주문이 없습니다)" : dialogue;
        }

        Slide(shownPosition, false);
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
