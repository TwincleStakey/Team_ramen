using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 겉모습. 얼굴과 몸통을 따로 두고 손님이 바뀔 때마다 무작위로 짝지어 준다.
///
/// 얼굴 넷과 몸통 넷이면 조합이 열여섯 가지다. 그림 여덟 장으로 손님 열여섯 명을 만드는 셈이라,
/// 사람마다 통짜 그림을 그리는 것보다 훨씬 싸게 붙는다.
///
/// 새 손님이 올 때마다 주문 화면이 다시 켜지므로, 켜지는 순간에 다시 뽑는다.
/// 따로 신호를 받을 필요가 없다.
/// </summary>
public class CustomerAppearance : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image headImage;
    [SerializeField] private Sprite[] bodies;
    [SerializeField] private Sprite[] heads;

    /// <summary>
    /// 목이 옷깃 속으로 들어가는 깊이.
    ///
    /// 얼굴 그림 아래에 목을 10칸 그려 붙여 두었다. 그중 이만큼만 옷깃에 묻고 나머지는 보인다.
    /// 예전에는 19칸을 겹쳐서 목이 통째로 옷깃에 묻혀 얼굴이 어깨에 바로 얹힌 꼴이었다.
    /// </summary>
    [SerializeField] private float neckOverlap = 5f;

    /// <summary>
    /// 몸통을 자리 아래변보다 얼마나 더 내릴지.
    ///
    /// 손님은 카운터 뒤에 서 있다. 이만큼 내리면 아래쪽이 자리 밖으로 나가고,
    /// 자리에 씌운 마스크가 거기서 잘라 내어 카운터에 가린 것처럼 보인다.
    /// </summary>
    [SerializeField] private float sinkBelowCounter = 26f;

    private void OnEnable()
    {
        Randomize();
    }

    /// <summary>얼굴과 몸통을 하나씩 뽑아 세운다.</summary>
    public void Randomize()
    {
        if (bodies != null && bodies.Length > 0) SetBody(bodies[Random.Range(0, bodies.Length)]);
        if (heads != null && heads.Length > 0) SetHead(heads[Random.Range(0, heads.Length)]);
    }

    private void SetBody(Sprite sprite)
    {
        if (bodyImage == null || sprite == null) return;

        bodyImage.sprite = sprite;

        // 그림마다 크기가 조금씩 다르다. 상자를 그림에 맞춰야 늘어나거나 눌리지 않는다.
        RectTransform rect = bodyImage.rectTransform;
        rect.sizeDelta = sprite.rect.size;

        // 몸통은 자리의 아래변 기준으로 놓되 그보다 더 내린다. 키가 다른 몸통이 와도
        // 잘리는 높이가 같아서, 다들 같은 카운터 뒤에 서 있는 것으로 보인다.
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, -sinkBelowCounter);
    }

    private void SetHead(Sprite sprite)
    {
        if (headImage == null || sprite == null) return;

        headImage.sprite = sprite;

        RectTransform rect = headImage.rectTransform;
        rect.sizeDelta = sprite.rect.size;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);

        // 몸통 꼭대기에서 목이 묻히는 깊이만큼만 내려 얹는다. 몸통이 내려간 만큼 얼굴도 같이 내려간다.
        float bodyTop = bodyImage != null
            ? bodyImage.rectTransform.anchoredPosition.y + bodyImage.rectTransform.sizeDelta.y
            : 0f;
        rect.anchoredPosition = new Vector2(0f, Mathf.Round(bodyTop - neckOverlap));
    }
}
