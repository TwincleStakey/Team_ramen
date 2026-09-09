using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 상단 제출 영역. 그릇이 여기 닿으면 불이 들어오고, 그 상태에서 놓으면 제출된다.
///
/// 예전에는 마우스 포인터가 이 안에 들어와야 반응했다. 그런데 그릇을 끌 때 포인터는
/// 그릇 한가운데에 있어서, 그릇 윗부분이 이미 푹 겹쳐 있어도 아무 반응이 없었다.
/// 포인터를 여기까지 밀어 올리면 그릇은 화면 위로 절반쯤 나가 있다.
///
/// 그래서 판정을 Bowl에 넘겼다. Bowl이 끌리는 동안 자기 그림과 이 영역이 겹치는지 재서
/// SetHighlight를 부른다. 보이는 것과 판정이 같아진다.
/// </summary>
public class SubmitZone : MonoBehaviour
{
    private static readonly Color HighlightColor = new Color(0.40f, 0.85f, 0.35f);

    /// <summary>닿았을 때 커지는 비율. 색만으로는 그릇에 가려 잘 안 보인다.</summary>
    private const float HighlightScale = 1.12f;

    private Image image;
    private RectTransform rect;
    private Color baseColor;
    private Vector3 baseScale;
    private bool lit;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        baseColor = image.color;
        baseScale = transform.localScale;
    }

    /// <summary>겹침을 재는 쪽(Bowl)이 쓰는 화면 사각형.</summary>
    public Rect ScreenRect()
    {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
    }

    /// <summary>그릇이 닿았는지에 따라 불을 켜고 끈다. 바뀔 때만 손대 매 프레임 낭비를 피한다.</summary>
    public void SetHighlight(bool on)
    {
        if (on == lit) return;
        lit = on;

        image.color = on ? HighlightColor : baseColor;
        transform.localScale = on ? baseScale * HighlightScale : baseScale;
    }
}
