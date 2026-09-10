using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 면통의 바구니 하나. 왼쪽이 얇은면, 오른쪽이 굵은면이다.
/// 그림은 없고 자리만 잡는다 — 보이는 것은 그 아래 깔린 면통 한 장이다.
///
/// 손놀림은 국자·시치미와 같은 드래그다. 다만 대기 모션이 하나 더 있다.
///
///   통 위에 있는 동안   소쿠리를 위아래로 터는 40프레임이 돈다
///   통 밖으로 나가면    털기 0번에서 멈춘 채 마우스를 따라온다
///   그릇에 놓으면       면을 쏟는 40프레임이 돌고, 마지막 장에서 그릇에 들어간다
///
/// 마지막 장에서 들어가는 것은 시치미와 같은 규칙이다. 다 붓기 전에 놓으면 아무것도 안 들어간다.
/// </summary>
public class NoodleSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                          IPointerDownHandler, IPointerUpHandler,
                          IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>이 바구니가 담당하는 면. RamenLayoutBuilder가 생성 시 지정한다.</summary>
    public IngredientType type;

    /// <summary>이번에 든 면을 그릇이 받았는가. Bowl.OnDrop이 켜 준다.</summary>
    private bool delivered;

    /// <summary>지금 이 바구니에서 든 면을 쥐고 있는가. 눌림을 놓쳤는지 판단하는 데 쓴다.</summary>
    private bool picking;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CookingCursor.Instance == null) return;

        CookingCursor.Instance.PreviewTool(type);
        CookingCursor.Instance.SetOverNoodlePot(true);
    }

    /// <summary>통 밖으로 나가면 터는 것을 멈추고 0번에서 굳는다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (CookingCursor.Instance != null) CookingCursor.Instance.SetOverNoodlePot(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (CookingCursor.Instance == null)
        {
            Debug.LogWarning("[NoodleSlot] 씬에 커서가 없습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

        delivered = false;
        picking = true;
        CookingCursor.Instance.PickUp(type);
    }

    /// <summary>누를 때 이미 들었다. 눌림을 놓친 경우에만 여기서 든다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (picking || CookingCursor.Instance == null) return;

        delivered = false;
        picking = true;
        CookingCursor.Instance.PickUp(type);
    }

    /// <summary>커서가 스스로 마우스를 따라다닌다. 여기서 할 일은 없다.</summary>
    public void OnDrag(PointerEventData eventData)
    {
    }

    /// <summary>
    /// 끌지 않고 눌렀다 뗀 경우. 끄는 중이면 손대지 않는다 —
    /// uGUI는 뗄 때 OnPointerUp 을 먼저 부르고 그다음 그릇의 OnDrop 을 부른다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        picking = false;
        if (CookingCursor.Instance == null) return;

        CookingCursor.Instance.Drop();

        // 마우스가 아직 통 위에 있다. 놓자마자 젓가락으로 돌아가 버리면
        // "여기서 뜰 수 있다"는 표시가 사라진다. 통을 벗어나지 않았으니 도구를 다시 씌운다.
        CookingCursor.Instance.PreviewTool(type);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        picking = false;
        if (delivered) return;
        if (CookingCursor.Instance == null) return;

        CookingCursor.Instance.Drop();

        // 통 위에서 놓았으면 도구 모양을 그대로 둔다.
        if (eventData.hovered != null && eventData.hovered.Contains(gameObject))
            CookingCursor.Instance.PreviewTool(type);
    }

    /// <summary>그릇이 받아 갔다고 알려 준다. Bowl.OnDrop이 부른다.</summary>
    public void MarkDelivered()
    {
        delivered = true;
    }
}
