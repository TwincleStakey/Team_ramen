using TMPro;
using UnityEngine;

/// <summary>
/// 글자에 한 칸짜리 검은 외곽선을 두른다. 같은 글을 검게 칠해 여덟 방향으로 한 칸씩 민 것을
/// 뒤에 깔아 두는 방식이다.
///
/// TMP 가 자체로 가진 외곽선은 쓰지 않는다. 그쪽은 셰이더로 글자를 부풀리는 방식이라
/// 래스터 픽셀 폰트에서는 획이 반칸에 걸려 가장자리에 회색이 낀다. Galmuri 가 그 폰트다.
/// 통째로 복사해 정수 칸만큼 미는 방식은 원본 그대로라 흐려질 곳이 없다.
///
/// 유니티 UI 의 Outline 효과도 안 쓴다. 그쪽은 TMP 가 만드는 메시를 건드리지 못한다.
/// </summary>
public class PixelTextOutline : MonoBehaviour
{
    /// <summary>원본 글자. 이것의 내용을 따라간다.</summary>
    [SerializeField] private TextMeshProUGUI source;

    /// <summary>뒤에 깔린 검은 복사본들. 빌더가 만들어 꽂아 준다.</summary>
    [SerializeField] private TextMeshProUGUI[] copies;

    private string shown;

    /// <summary>
    /// 원본 글이 바뀌면 복사본에도 같은 글을 넣는다.
    ///
    /// LateUpdate 에서 한다. 글을 바꾸는 쪽(SeasoningBadges 등)이 Update 에서 바꾸므로,
    /// 같은 프레임에 따라가려면 그 뒤여야 한 프레임 늦게 따라오는 일이 없다.
    /// </summary>
    private void LateUpdate()
    {
        if (source == null || copies == null) return;
        if (source.text == shown) return;

        shown = source.text;
        for (int i = 0; i < copies.Length; i++)
        {
            if (copies[i] != null) copies[i].text = shown;
        }
    }
}
