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

    /// <summary>
    /// 글자가 찍힐 때 나는 톤. 손님 대사와 같은 것을 쓴다.
    ///
    /// 도입부만 소리 없이 찍히고 있었다. 가게가 열리고 손님이 말을 걸 때 비로소 소리가 나서,
    /// 앞부분이 고장 난 것처럼 들렸다.
    /// </summary>
    [SerializeField] private DialogueBlip blip;

    /// <summary>내레이션 목소리. 손님이 아니라 이야기하는 사람이라 낮고 차분한 쪽을 쓴다.</summary>
    private const string NarratorVoice = "Formal";

    /// <summary>글자가 찍히는 속도(초당 글자 수).</summary>
    [SerializeField] private float charsPerSecond = 20f;

    /// <summary>▼ 가 한 번 깜빡이는 데 걸리는 시간(초). 절반은 켜져 있고 절반은 꺼져 있다.</summary>
    [SerializeField] private float blinkSeconds = 0.5f;

    /// <summary>마지막 줄 끝과 ▼ 사이 틈(칸).</summary>
    [SerializeField] private float promptGap = 10f;

    /// <summary>
    /// 읽을 줄들.
    ///
    /// 일부러 직렬화하지 않는다. [SerializeField] 를 달아 두면 씬에 한 벌이 따로 저장되어,
    /// 여기를 고쳐도 화면에는 옛 글이 그대로 나온다. 실제로 시험용 「냥냥」이 그렇게 남아 있었다.
    /// 글을 바꾸려면 여기만 고치면 되고 빌더를 돌릴 필요도 없다.
    /// </summary>
    private readonly string[] lines =
    {
        "모두가 하루를 마치고 집으로 돌아갈 무렵,",
        "비로소 불을 밝히는 작은 라멘 가게가 있다.",
        "먹고 싶은 맛을 이야기하면,",
        "주인장이 그 말에 꼭 맞는 한 그릇을 내어 준다고.",
        "그리고 오늘 밤..."
    };

    /// <summary>한 번 돌고 끝날 때까지 기다린다. GameManager 가 부른다.</summary>
    public IEnumerator Play()
    {
        return Play(lines);
    }

    /// <summary>
    /// 다른 글로 같은 연출을 돌린다. 배드엔딩(목표 미달 → 다시하기)이 이걸 쓴다.
    ///
    /// 검은 판·타자기·▼·클릭 대기가 전부 같은 것이라 따로 만들 이유가 없다.
    /// 끝나도 판을 안 걷는다 — 아이리스가 화면을 넘겨받은 뒤에 <see cref="Hide"/> 를 불러야
    /// 한 프레임도 가게가 비치지 않는다. 도입부와 같은 규칙이다.
    /// </summary>
    public IEnumerator Play(string[] lines)
    {
        if (root == null || label == null || lines == null || lines.Length == 0) yield break;

        root.SetActive(true);
        label.text = string.Empty;
        if (prompt != null) prompt.enabled = false;
        if (blip != null) blip.SetPersona(NarratorVoice);

        // 모드를 고른 그 클릭이 첫 줄 건너뛰기로 읽히지 않게 한 프레임 흘린다.
        // 아래 WaitForPress 가 나갈 때 하는 것과 같은 처리다.
        yield return null;

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
                    // 이번에 드러난 마지막 글자로 소리를 낸다. 한 프레임에 두 글자가 나와도
                    // 톤은 한 번만 낸다 — 겹쳐 내면 또로록이 아니라 잡음이 된다.
                    char letter = line[next - 1];

                    count = next;
                    label.text = before + line.Substring(0, count);

                    if (blip != null && !DialogueBlip.IsSilent(letter)) blip.PlayTone();
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
    /// 글자만 스르륵 지운다. 검은 판은 그대로 둔다.
    ///
    /// 마지막 줄을 넘기자마자 자막(「1일차」)이 뜨면 두 글 덩이가 한 화면에 겹친다.
    /// 글자를 먼저 거두고 한 박자 쉰 뒤에 자막이 들어와야 장면이 넘어간 것으로 읽힌다.
    /// 판을 같이 걷지 않는 것은, 걷는 순간 아직 안 열린 가게가 드러나기 때문이다.
    /// </summary>
    public IEnumerator FadeOutLines(float seconds)
    {
        if (label == null) yield break;

        if (prompt != null) prompt.enabled = false;

        Color from = label.color;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float a = Mathf.Clamp01(1f - elapsed / seconds);
            label.color = new Color(from.r, from.g, from.b, a);
            yield return null;
        }

        label.text = string.Empty;
        label.color = from;          // 다음에 쓸 때는 다시 진하게
    }

    /// <summary>
    /// ▼ 를 마지막 줄의 오른쪽 끝, 그 줄과 같은 높이에 놓는다.
    ///
    /// 예전에는 글 덩이 아래 왼쪽에 두었다. 줄이 쌓일수록 방금 읽은 줄에서 멀어지고,
    /// 다음 줄이 붙을 자리를 ▼ 가 먼저 차지해 한 줄이 밀린 것처럼 보였다.
    ///
    /// 자리는 TMP 가 실제로 짠 줄에서 읽는다. 글자 폭을 따로 재면 띄어쓰기와 자간 때문에
    /// 한두 칸씩 어긋난다. 방금 text 를 바꿨으므로 ForceMeshUpdate 로 먼저 짜게 한다.
    ///
    /// 글상자 안 좌표의 원점은 피벗(윗변 가운데)이고, 글상자의 anchoredPosition 도 같은 점을
    /// 가리킨다. ▼ 와 글상자가 같은 부모에 같은 앵커로 달려 있어 그대로 더하면 된다.
    ///
    /// 자리는 정수로 끊는다. 픽셀아트라 반 칸에 놓이면 삼각형 가장자리가 흐려진다.
    /// </summary>
    private void PlacePrompt()
    {
        if (promptRect == null || prompt == null || label == null) return;

        label.ForceMeshUpdate();

        TMP_TextInfo info = label.textInfo;
        if (info == null || info.lineCount == 0) return;

        TMP_LineInfo line = info.lineInfo[info.lineCount - 1];
        Vector2 origin = label.rectTransform.anchoredPosition;

        float x = origin.x + line.lineExtents.max.x + promptGap + promptRect.rect.width * 0.5f;

        // ▼ 피벗이 윗변이라, 줄 한가운데에 맞추려면 제 높이의 절반만큼 올려 잡는다.
        float y = origin.y + (line.ascender + line.descender) * 0.5f + promptRect.rect.height * 0.5f;

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
        Sfx.Play("sfx_flow_next", 0.5f);

        // 나갈 때도 한 프레임 흘린다. 들어올 때와 같은 이유다.
        //
        // wasPressedThisFrame 은 그 프레임 내내 참이라, 넘기려고 누른 그 입력이 바로 다음
        // 줄의 "건너뛰기" 로 한 번 더 읽힌다. 이게 없어서 둘째 줄부터는 타자기가 한 글자도
        // 안 돌고 통째로 찍혔다.
        yield return null;
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
