using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 하루가 바뀔 때 검은 화면에 뜨는 「N일차 / 목표 35,000원」.
///
/// 한 번에 나타나지 않는다. 「N일차」를 한 글자씩 찍고, 한 박자 쉬고, 그 밑에 목표액을 또
/// 한 글자씩 찍는다. 도입부 내레이션과 같은 결이라 두 화면이 한 연출로 이어진다.
///
/// 검은 판은 이쪽이 들지 않는다. 화면을 덮는 것은 ScreenFade 가 이미 하고 있고, 이 글자는
/// 그보다 앞 층(315)에 얹히기만 한다. 판을 또 들면 검은 것이 두 겹이 되어 걷히는 박자가 어긋난다.
///
/// 시간은 실시간으로 잰다(unscaled). 이 구간은 하루가 시작되기 전이라 timeScale 이
/// 어떤 값이든 흘러야 한다.
/// </summary>
public class DayTitleUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.

    /// <summary>글자 한 벌. 이 스크립트는 이 바깥에 붙어 있어야 껐다 켤 수 있다.</summary>
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup group;

    /// <summary>글자가 찍히는 속도(초당 글자 수). 내레이션(20)보다 느려야 한 자씩 읽힌다.</summary>
    [SerializeField] private float charsPerSecond = 12f;

    /// <summary>윗줄을 다 찍고 아랫줄을 시작하기까지 쉬는 시간(초).</summary>
    [SerializeField] private float lineGapSeconds = 0.7f;

    /// <summary>두 줄이 다 찍힌 채로 머무는 시간(초).</summary>
    [SerializeField] private float holdSeconds = 1.2f;

    /// <summary>글자가 스러지는 데 걸리는 시간(초). 툭 꺼지면 다음 장면과 이어지지 않는다.</summary>
    [SerializeField] private float fadeOutSeconds = 0.8f;

    /// <summary>글자마다 나는 톤. 없으면 소리 없이 찍힌다.</summary>
    [SerializeField] private DialogueBlip blip;

    /// <summary>자막 목소리. 내레이션과 같은 것을 쓴다.</summary>
    private const string Voice = "Formal";

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 시간의 상한(초).
    /// unscaledDeltaTime 은 유니티가 안 잘라 줘서, 무거운 프레임 하나에 연출이 다 끝나 버린다.
    /// </summary>
    private const float MaxStep = 0.05f;

    private void Awake()
    {
        Hide();
    }

    public void Hide()
    {
        if (group != null) group.alpha = 0f;
        if (label != null) label.text = string.Empty;
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 자막을 찍고 거둔다. 다 끝날 때까지 기다린다.
    ///
    /// <paramref name="text"/> 는 줄바꿈으로 나뉜 두 줄을 받는다. 줄이 하나뿐이면 그것만 찍는다.
    /// </summary>
    public IEnumerator Play(string text)
    {
        if (root == null || label == null || string.IsNullOrEmpty(text)) yield break;

        root.SetActive(true);
        if (group != null) group.alpha = 1f;
        if (blip != null) blip.SetPersona(Voice);

        // 줄 수가 달라져도 글 덩이가 위아래로 안 움직이게, 처음부터 전체를 넣고
        // 보이는 글자 수만 늘린다. 한 줄씩 넣으면 첫 줄이 가운데 있다가 위로 밀려 올라간다.
        label.text = text;
        label.maxVisibleCharacters = 0;
        label.ForceMeshUpdate();

        string[] lines = text.Split('\n');
        int shown = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            // 줄바꿈 자체는 눈에 안 보이므로 세는 데서 뺀다.
            yield return Type(shown, shown + lines[i].Length);
            shown += lines[i].Length;

            if (i < lines.Length - 1) yield return new WaitForSecondsRealtime(lineGapSeconds);
        }

        label.maxVisibleCharacters = int.MaxValue;
        yield return new WaitForSecondsRealtime(holdSeconds);
        yield return FadeOut();

        Hide();
    }

    /// <summary>from 번째 글자부터 to 번째까지 한 글자씩 드러낸다.</summary>
    private IEnumerator Type(int from, int to)
    {
        float typed = from;

        while (typed < to)
        {
            typed += Mathf.Max(1f, charsPerSecond) * Mathf.Min(Time.unscaledDeltaTime, MaxStep);

            int now = Mathf.Min(to, Mathf.FloorToInt(typed));
            if (now != label.maxVisibleCharacters)
            {
                label.maxVisibleCharacters = now;

                // 공백과 쉼표에서는 소리를 내지 않는다. 글자에만 붙어야 말이 찍히는 것으로 들린다.
                if (blip != null && now > 0 && now <= label.text.Length
                    && !DialogueBlip.IsSilent(label.text[now - 1])) blip.PlayTone();
            }

            yield return null;
        }

        label.maxVisibleCharacters = to;
    }

    private IEnumerator FadeOut()
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeOutSeconds)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            group.alpha = Mathf.Clamp01(1f - elapsed / fadeOutSeconds);
            yield return null;
        }

        group.alpha = 0f;
    }
}
