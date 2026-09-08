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

    private Vector3 baseScale;
    private Image image;
    private Color baseColor;

    private void Awake()
    {
        baseScale = transform.localScale;
        image = GetComponent<Image>();
        if (image != null) baseColor = image.color;
    }

    private void OnDisable()
    {
        // 강조된 채로 꺼지면 다시 켤 때 그대로 남는다.
        ResetLook();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (useScale)
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
        ResetLook();
    }

    private void ResetLook()
    {
        transform.localScale = baseScale;
        if (image != null) image.color = baseColor;
    }
}
