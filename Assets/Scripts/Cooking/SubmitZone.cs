using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 상단 제출 영역. 그릇이 위로 들어오면 강조하고, 여기서 놓으면 제출된다.
/// </summary>
public class SubmitZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    private static readonly Color HighlightColor = new Color(0.40f, 0.85f, 0.35f);

    /// <summary>그릇이 올라왔을 때 커지는 비율. 색만으로는 그릇에 가려 잘 안 보인다.</summary>
    private const float HighlightScale = 1.12f;

    private Image image;
    private Color baseColor;
    private Vector3 baseScale;

    private void Awake()
    {
        image = GetComponent<Image>();
        baseColor = image.color;
        baseScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스가 그냥 지나가는 것과 그릇을 끌고 들어온 것을 구분한다.
        if (GetDraggedBowl(eventData) == null) return;

        image.color = HighlightColor;
        transform.localScale = baseScale * HighlightScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetLook();
    }

    public void OnDrop(PointerEventData eventData)
    {
        ResetLook();

        Bowl bowl = GetDraggedBowl(eventData);
        if (bowl != null) bowl.Submit();
    }

    private void ResetLook()
    {
        image.color = baseColor;
        transform.localScale = baseScale;
    }

    private static Bowl GetDraggedBowl(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return null;
        return eventData.pointerDrag.GetComponent<Bowl>();
    }
}
