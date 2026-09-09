using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재료통에 마우스를 올렸을 때 이름을 알려 주는 팻말. 판 전체에 하나만 둔다.
///
/// 통마다 이름표를 붙여 두면 판이 스무 개 깔려 그림을 가린다. 그래서 이름표를 다 걷어내고,
/// 가리키는 통 하나에만 팻말이 뜨게 했다. 그림이 곧 이름이고, 팻말은 확인용이다.
///
/// 통과 같은 부모(640x360 판) 밑에 있어야 좌표 계산이 맞는다. 빌더가 그렇게 만든다.
/// </summary>
public class SlotNameplate : MonoBehaviour
{
    /// <summary>빌더가 꽂아 준다.</summary>
    public Text label;

    /// <summary>통과 팻말 사이 간격.</summary>
    private const float Gap = 4f;

    private RectTransform rect;
    private RectTransform frame;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        frame = rect.parent as RectTransform;
        Hide();
    }

    /// <summary>통 아래에 팻말을 띄운다. 판 밖으로 나갈 자리면 통 위로 넘긴다.</summary>
    public void Show(string text, RectTransform slot)
    {
        if (label == null || slot == null || frame == null) return;

        label.text = text;
        gameObject.SetActive(true);

        // 통은 앵커가 제각각(왼쪽 위, 오른쪽 가운데…)이라 anchoredPosition을 그대로 못 쓴다.
        // 화면상의 네 귀퉁이를 받아 판 기준 좌표로 바꾼다.
        Vector3[] corners = new Vector3[4];
        slot.GetWorldCorners(corners);
        Vector2 bottomLeft = frame.InverseTransformPoint(corners[0]);
        Vector2 topRight = frame.InverseTransformPoint(corners[2]);

        Vector2 half = rect.sizeDelta * 0.5f;
        Vector2 frameHalf = frame.rect.size * 0.5f;

        float x = (bottomLeft.x + topRight.x) * 0.5f;
        float y = bottomLeft.y - Gap - half.y;

        // 맨 아랫줄 통은 아래에 자리가 없다. 그때만 위로 올린다.
        if (y - half.y < -frameHalf.y) y = topRight.y + Gap + half.y;

        // 판 좌우 끝에 붙은 통은 팻말이 화면 밖으로 반쯤 나간다.
        x = Mathf.Clamp(x, -frameHalf.x + half.x, frameHalf.x - half.x);

        // 반칸에 놓이면 팻말 테두리와 글자가 픽셀 격자에서 벗어나 흐려진다.
        rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
