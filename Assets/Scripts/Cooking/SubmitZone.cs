using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 상단 제출 영역. 그릇이 위로 들어오면 강조하고, 여기서 놓으면 제출된다.
/// </summary>
public class SubmitZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    private static readonly Color HighlightColor = new Color(0.48f, 0.72f, 0.38f);

    private Image image;
    private Color baseColor;

    private void Awake()
    {
        image = GetComponent<Image>();
        baseColor = image.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스가 그냥 지나가는 것과 그릇을 끌고 들어온 것을 구분한다.
        if (GetDraggedBowl(eventData) != null) image.color = HighlightColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        image.color = baseColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        image.color = baseColor;

        Bowl bowl = GetDraggedBowl(eventData);
        if (bowl != null) bowl.Submit();
    }

    private static Bowl GetDraggedBowl(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return null;
        return eventData.pointerDrag.GetComponent<Bowl>();
    }
}
