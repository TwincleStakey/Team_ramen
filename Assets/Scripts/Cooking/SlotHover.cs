using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 재료통 위에 마우스가 올라왔는지 알려 주는 표시.
/// 커서가 젓가락·국자 그림이라 어디를 가리키는지 알기 어려워서, 통 자체가 반응하게 했다.
/// RamenLayoutBuilder가 모든 슬롯에 붙인다.
/// </summary>
public class SlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>올라왔을 때 커지는 비율. 크게 잡으면 옆 통과 겹친다.</summary>
    public float hoverScale = 1.08f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        // 커진 채로 꺼지면 다시 켤 때 그대로 남는다.
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = baseScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = baseScale;
    }
}
