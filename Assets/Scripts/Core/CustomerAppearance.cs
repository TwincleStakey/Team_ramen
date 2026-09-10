using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 겉모습. 얼굴과 몸통을 따로 두고 손님이 바뀔 때마다 무작위로 짝지어 준다.
///
/// 얼굴 넷과 몸통 넷이면 조합이 열여섯 가지다. 그림 여덟 장으로 손님 열여섯 명을 만드는 셈이라,
/// 사람마다 통짜 그림을 그리는 것보다 훨씬 싸게 붙는다.
///
/// 언제 다시 뽑을지는 OrderScreenUI 가 정한다. 예전에는 화면이 켜질 때마다 스스로 뽑았는데,
/// 라멘을 낸 뒤 같은 손님이 먹는 장면을 보여 주려고 화면을 다시 켜면서 그 전제가 깨졌다.
/// 화면이 켜졌다고 새 손님인 것은 아니다.
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
    /// 얼굴 그림 아래에 목을 10칸 그려 붙여 두었다. 그중 이만큼이 옷깃에 묻히고
    /// 나머지가 밖으로 보인다. 지금은 9칸이 묻히고 1칸이 보인다.
    ///
    /// 처음에는 19칸을 겹쳐 목이 통째로 묻혔고(얼굴이 어깨에 바로 얹힌 꼴), 그다음 5칸으로
    /// 줄였더니 이번엔 목이 길어 보였다. 값을 키우면 목이 짧아지고 줄이면 길어진다.
    /// </summary>
    [SerializeField] private float neckOverlap = 9f;

    /// <summary>
    /// 몸통을 자리 아래변보다 얼마나 더 내릴지.
    ///
    /// 손님은 카운터 뒤에 서 있다. 이만큼 내리면 아래쪽이 자리 밖으로 나가고,
    /// 자리에 씌운 마스크가 거기서 잘라 내어 카운터에 가린 것처럼 보인다.
    ///
    /// 26 이었다가 14 로 줄였다. 자리 아래변(카운터 선)이 잘못 잡혀 있어 12칸 위에 있었는데,
    /// 그걸 배경 그림에서 다시 재어 12칸 내렸다. 같이 줄이지 않으면 손님이 통째로 12칸
    /// 내려앉는다. 지금 값은 몸통을 제자리에 두고 잘리는 높이만 카운터에 맞춘 것이다.
    /// </summary>
    [SerializeField] private float sinkBelowCounter = 14f;

    /// <summary>방금 서 있던 손님. 다음 손님이 같은 짝으로 나오지 않게 기억해 둔다.</summary>
    private int lastBody = -1;
    private int lastHead = -1;

    /// <summary>
    /// 얼굴과 몸통을 하나씩 뽑아 세운다.
    ///
    /// 앞 손님과 같은 짝이 다시 나오면 손님이 안 바뀐 것처럼 보인다. 조합이 열여섯뿐이라
    /// 그냥 뽑으면 열여섯 번에 한 번은 그렇게 된다. 겹치면 다시 뽑는다.
    /// </summary>
    public void Randomize()
    {
        if (bodies == null || bodies.Length == 0) return;
        if (heads == null || heads.Length == 0) return;

        int body = Random.Range(0, bodies.Length);
        int head = Random.Range(0, heads.Length);

        // 조합이 하나뿐이면 다시 뽑아도 같으므로 그대로 쓴다. 안 그러면 여기서 영영 돈다.
        if (bodies.Length * heads.Length > 1)
        {
            while (body == lastBody && head == lastHead)
            {
                body = Random.Range(0, bodies.Length);
                head = Random.Range(0, heads.Length);
            }
        }

        lastBody = body;
        lastHead = head;

        SetBody(bodies[body]);
        SetHead(heads[head]);
    }

    /// <summary>
    /// 스러진 정도. 0이면 평소 모습이고 1이면 완전히 사라진 상태다.
    ///
    /// 알파만 내리면 뒤 배경이 그대로 비쳐서 "흐려진다"로 보인다. 색을 검정 쪽으로 함께
    /// 당기면 어둠에 잠기듯 사라진다. 알파는 제곱으로 늦춰 두었다 —
    /// 먼저 어두워지고 그다음에 지워져야 인영이 스러지는 것으로 읽힌다.
    /// </summary>
    public void SetFade(float t)
    {
        t = Mathf.Clamp01(t);

        float shade = 1f - t;
        float alpha = 1f - t * t;
        var tint = new Color(shade, shade, shade, alpha);

        if (bodyImage != null) bodyImage.color = tint;
        if (headImage != null) headImage.color = tint;
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
