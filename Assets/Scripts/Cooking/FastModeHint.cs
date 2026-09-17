using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연출 배속이 켜졌는지를 키 안내 아이콘에 비춘다.
///
/// 꺼짐과 켜짐을 <b>색이 아니라 그림</b>으로 가른다. 흐리게만 하면 「지금 못 누르는 것」으로
/// 읽힌다. 속이 빈 겹화살표와 속이 찬 겹화살표 두 장을 갈아 끼운다.
/// </summary>
public class FastModeHint : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private Image icon;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;

    private void OnEnable()
    {
        CookTempo.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        CookTempo.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (icon == null) return;

        Sprite want = CookTempo.Fast ? onSprite : offSprite;
        if (want != null && icon.sprite != want) icon.sprite = want;
    }
}
