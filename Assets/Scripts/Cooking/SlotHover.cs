using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 재료통 위에 마우스가 올라왔는지 알려 주는 표시.
/// 커서가 젓가락·국자 그림이라 어디를 가리키는지 알기 어려워서, 통 자체가 반응하게 했다.
/// RamenLayoutBuilder가 모든 슬롯에 붙인다.
/// </summary>
public class SlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>올라왔을 때 커지는 비율. 크게 잡으면 옆 통과 겹친다.</summary>
    public float hoverScale = 1.08f;

    /// <summary>
    /// 거짓이면 크기 대신 밝기로 표시한다.
    /// 면 튀김기는 두 통이 한 대로 이어져 있어서, 한쪽만 커지면 이음매가 벌어진다.
    /// </summary>
    public bool useScale = true;

    /// <summary>밝기로 표시할 때 곱하는 값.</summary>
    public float hoverBrightness = 1.25f;

    /// <summary>
    /// 올라왔을 때 갈아 끼울 그림. 꽂혀 있으면 크기·밝기 대신 이걸 쓴다.
    /// 타래통과 향미유통은 테두리를 두른 그림이 따로 있어서, 늘리지 않고 갈아 끼운다.
    /// </summary>
    public Sprite hoverSprite;

    /// <summary>
    /// hoverSprite 를 쓸 때의 상자 크기. 테두리가 사방 1픽셀이라 기본 그림보다 2씩 크다.
    /// 이 크기가 그림과 다르면 픽셀이 늘어나 뭉개진다.
    /// </summary>
    public Vector2 hoverSize;

    /// <summary>팻말에 띄울 재료 이름. 빌더가 넣어 준다.</summary>
    public string label;

    /// <summary>판 전체가 함께 쓰는 팻말 하나. 빌더가 꽂아 준다.</summary>
    public SlotNameplate nameplate;

    /// <summary>
    /// 마우스 판정에서 "그림이 있다"로 칠 알파. 0 이면 상자 전체가 판정이다.
    ///
    /// 상자는 네모라, 그림이 상자보다 좁은 통은 빈 자리를 눌러도 집혔다. 시치미가 제일
    /// 심해서 64칸 그림 안에서 병이 가로 24칸뿐인데 128칸 상자 전체가 판정이었다.
    /// 재료통도 모서리가 둥근데 상자는 네모라 네 귀퉁이가 남는다.
    ///
    /// 빌더에서 넣으면 안 된다. uGUI 의 alphaHitTestMinimumThreshold 는 직렬화되지 않는
    /// 필드라(Image.cs 주석 "Not serialized until we support read-enabled sprites better")
    /// 씬에 저장되지 않고 플레이할 때 0 으로 돌아온다. 실제로 빌드해 보니 씬 파일에 값이
    /// 하나도 안 남았다. 그래서 여기서 실행할 때 넣는다.
    ///
    /// 원본 그림의 isReadable 이 켜져 있어야 한다. 그건 빌더가 맞춰 둔다.
    /// </summary>
    public float hitAlpha = 0.5f;

    private Vector3 baseScale;
    private Image image;
    private Color baseColor;
    private Sprite baseSprite;
    private Vector2 baseSize;

    private void Awake()
    {
        baseScale = transform.localScale;
        image = GetComponent<Image>();
        if (image != null)
        {
            baseColor = image.color;
            baseSprite = image.sprite;

            // 판정을 그림 모양에 맞춘다. hitAlpha 설명 참고 — 여기서 넣어야 살아남는다.
            if (hitAlpha > 0f) image.alphaHitTestMinimumThreshold = hitAlpha;
        }
        baseSize = ((RectTransform)transform).sizeDelta;
    }

    private void OnDisable()
    {
        // 꺼지는 통에는 이탈 신호가 오지 않는다. 젓가락이 뜬 채로 남지 않게 여기서 알린다.
        if (CookingCursor.Instance != null) CookingCursor.Instance.ExitSlot(gameObject);

        // 강조된 채로 꺼지면 다시 켤 때 그대로 남는다.
        ResetLook();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 젓가락으로 재료를 집고 있는 동안에는 어떤 통도 반응하지 않는다.
        // 집은 채로는 다른 것을 집거나 부을 수 없다. 그런데 통이 커지거나 커서가 국자·병으로
        // 바뀌면 집은 것이 사라진 것처럼 보여서, 들고 있는 동안에는 통을 아예 죽여 둔다.
        if (CookingCursor.Instance != null && CookingCursor.Instance.IsGripping) return;

        // 스치기만 해도 나는 소리라 아주 작게.
        Sfx.Play("sfx_cook_hover", 0.12f, 1f, 0.05f);

        // 젓가락은 재료통 위에서만 뜬다. 나머지 자리에서는 시스템 화살표를 쓴다.
        if (CookingCursor.Instance != null) CookingCursor.Instance.EnterSlot(gameObject);

        // 이름표를 다 걷어낸 대신, 가리키는 통 하나에만 이름이 뜬다.
        if (nameplate != null) nameplate.Show(label, (RectTransform)transform);

        // 테두리 그림이 있으면 그걸로 갈아 끼운다. 늘리면 픽셀이 반칸에 걸려 뭉개진다.
        if (hoverSprite != null && image != null)
        {
            image.sprite = hoverSprite;
            ((RectTransform)transform).sizeDelta = hoverSize;
        }
        else if (useScale)
        {
            transform.localScale = baseScale * hoverScale;
        }
        else if (image != null)
        {
            image.color = baseColor * hoverBrightness;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CookingCursor.Instance != null) CookingCursor.Instance.ExitSlot(gameObject);
        ResetLook();
    }

    private void ResetLook()
    {
        transform.localScale = baseScale;
        if (image != null)
        {
            image.color = baseColor;
            if (hoverSprite != null)
            {
                image.sprite = baseSprite;
                ((RectTransform)transform).sizeDelta = baseSize;
            }
        }

        // 통에서 벗어나거나 통이 꺼질 때. 안 지우면 팻말이 남아 다른 통을 가린다.
        if (nameplate != null) nameplate.Hide();
    }
}
