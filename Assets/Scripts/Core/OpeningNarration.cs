using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 스토리 모드를 고르면 검은 화면에서 먼저 도는 도입부 내레이션.
///
/// 줄은 위에서부터 쌓인다. 한 줄이 다 찍히면 그 아래에 ▼ 가 깜빡이고, 아무 키나 누르면
/// 다음 줄이 밑에 붙는다. 앞 줄을 지우지 않는 것은 읽던 흐름을 놓치지 않게 하려는 것이다.
///
/// 찍는 도중에 누르면 그 줄을 한 번에 보여 준다. 이미 읽은 사람을 기다리게 하지 않는다.
///
/// 검은 판을 자기가 들고 있다. 전환 판(ScreenFade)이 걷혀도 글자 뒤는 검어야 하고,
/// 마지막에 아이리스가 넘겨받을 때 한 프레임도 가게가 비쳐서는 안 된다.
///
/// 시간은 실시간으로 잰다(unscaled). 이 구간은 아직 하루가 시작되기 전이라 timeScale 이
/// 어떤 값이든 내레이션은 흘러야 한다.
/// </summary>
public class OpeningNarration : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.

    /// <summary>검은 판과 글자를 묶은 것. 이 스크립트는 이 바깥에 붙어 있어야 껐다 켤 수 있다.</summary>
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform promptRect;

    /// <summary>줄이 끝났음을 알리는 ▼. 켰다 껐다 하며 깜빡인다.</summary>
    [SerializeField] private Image prompt;

    /// <summary>글자가 찍히는 속도(초당 글자 수).</summary>
    [SerializeField] private float charsPerSecond = 20f;

    /// <summary>▼ 가 한 번 깜빡이는 데 걸리는 시간(초). 절반은 켜져 있고 절반은 꺼져 있다.</summary>
    [SerializeField] private float blinkSeconds = 0.5f;

    /// <summary>마지막 줄과 ▼ 사이 틈(칸).</summary>
    [SerializeField] private float promptGap = 10f;

    /// <summary>
    /// 읽을 줄들.
    ///
    /// 지금은 흐름을 보려고 넣어 둔 시험용 문장이다. 실제 도입부 글이 정해지면 여기만 갈아 끼우면 된다.
    /// </summary>
    [SerializeField]
    private string[] lines =
    {
        "냥냥.",
        "냥냥냥.",
        "냥냥 냥냥냥.",
        "냥냥냥 냥. 냥냥냥냥.",
        "냥냥."
    };

    /// <summary>한 번 돌고 끝날 때까지 기다린다. GameManager 가 부른다.</summary>
    public IEnumerator Play()
    {
        if (root == null || label == null || lines == null || lines.Length == 0) yield break;

        root.SetActive(true);
        label.text = string.Empty;
        if (prompt != null) prompt.enabled = false;

        // 지금까지 찍어 둔 줄들. 새 줄은 여기 뒤에 붙여 가며 그린다.
        var shown = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string before = shown.ToString();

            // 한 글자씩 늘려 그린다. 줄이 짧아 접히지 않으므로 앞 줄은 움직이지 않는다.
            float typed = 0f;
            int count = 0;

            while (count < line.Length)
            {
                // 찍는 도중에 누르면 그 줄을 한 번에 보여 준다.
                if (Pressed()) break;

                typed += charsPerSecond * Time.unscaledDeltaTime;
                int next = Mathf.Min(line.Length, Mathf.FloorToInt(typed));

                if (next != count)
                {
                    count = next;
                    label.text = before + line.Substring(0, count);
                }

                yield return null;
            }

            label.text = before + line;

            shown.Append(line);
            if (i < lines.Length - 1) shown.Append('\n');

            PlacePrompt();
            yield return WaitForPress();
        }

        if (prompt != null) prompt.enabled = false;
    }

    /// <summary>검은 판을 치운다. 아이리스가 화면을 넘겨받은 뒤에 부른다.</summary>
    public void Hide()
    {
        if (prompt != null) prompt.enabled = false;
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// ▼ 를 마지막 줄 바로 밑으로 옮긴다.
    ///
    /// 글상자는 피벗이 위쪽이라 anchoredPosition.y 가 곧 글의 윗변이다. 거기서 글 높이만큼
    /// 내려오면 마지막 줄의 아랫변이다. 줄이 쌓일수록 ▼ 도 같이 내려간다.
    ///
    /// 자리는 정수로 끊는다. 픽셀아트라 반 칸에 놓이면 삼각형 가장자리가 흐려진다.
    /// </summary>
    private void PlacePrompt()
    {
        if (promptRect == null || prompt == null || label == null) return;

        RectTransform rect = label.rectTransform;
        float height = label.GetPreferredValues(label.text, rect.rect.width, 0f).y;

        float x = rect.anchoredPosition.x - rect.rect.width * 0.5f + promptRect.rect.width * 0.5f;
        float y = rect.anchoredPosition.y - height - promptGap;

        promptRect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    /// <summary>
    /// 다음으로 넘기는 입력을 기다리며 ▼ 를 깜빡인다.
    ///
    /// 한 프레임 흘려보내고 시작한다. 줄을 한 번에 보이려고 누른 그 입력이 그대로 "다음"으로
    /// 읽히면 두 줄이 한꺼번에 지나간다.
    /// </summary>
    private IEnumerator WaitForPress()
    {
        yield return null;

        float t = 0f;

        while (!Pressed())
        {
            t += Time.unscaledDeltaTime;
            if (prompt != null) prompt.enabled = Mathf.Repeat(t, blinkSeconds) < blinkSeconds * 0.5f;
            yield return null;
        }

        if (prompt != null) prompt.enabled = false;
    }

    /// <summary>아무 키나, 또는 마우스 왼쪽.</summary>
    private static bool Pressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }
}
