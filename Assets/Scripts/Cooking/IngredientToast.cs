using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 투입이 거부됐을 때 그릇 위에 잠깐 뜨는 안내. 위로 떠오르면서 사라진다.
///
/// 붉은 깜빡임과 역할이 다르다. 깜빡임은 "어느 그릇이 거부했다"는 자리를 짚어 주고,
/// 이쪽은 "왜 거부했다"를 말한다. 그래서 둘을 같이 낸다.
/// </summary>
public class IngredientToast : MonoBehaviour
{
    /// <summary>떠오르는 높이. 그릇에서 너무 멀어지면 무엇이 거부됐는지 연결이 끊긴다.</summary>
    private const float RiseDistance = 30f;

    private const float Seconds = 0.8f;

    /// <summary>이 구간까지는 또렷하게 둔다. 처음부터 흐려지면 읽을 새가 없다.</summary>
    private const float HoldRatio = 0.3f;

    // RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    public TextMeshProUGUI label;

    private RectTransform rect;
    private Vector2 homePosition;
    private Coroutine playing;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        if (label == null) label = GetComponent<TextMeshProUGUI>();

        homePosition = rect.anchoredPosition;
        Hide();
    }

    /// <summary>같은 말이 연달아 뜨면 앞엣것을 끊고 처음부터 다시 띄운다.</summary>
    public void Show(string message)
    {
        if (label == null) return;

        label.text = message;

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Rise());
    }

    private IEnumerator Rise()
    {
        float elapsed = 0f;

        while (elapsed < Seconds)
        {
            float t = elapsed / Seconds;

            rect.anchoredPosition = homePosition + new Vector2(0f, RiseDistance * t);
            label.alpha = t < HoldRatio ? 1f : 1f - (t - HoldRatio) / (1f - HoldRatio);

            // 팝업이 떠서 시간이 멈추더라도 안내는 사라져야 한다. 그릇의 깜빡임도 같은 이유로 실시간을 쓴다.
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Hide();
        playing = null;
    }

    private void Hide()
    {
        rect.anchoredPosition = homePosition;
        if (label != null) label.alpha = 0f;
    }
}
