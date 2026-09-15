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

    /// <summary>
    /// 올려놓기만 해도 커서가 소쿠리로 바뀐다. 무엇을 쥐게 될지 미리 보이게 하려는 것이다.
    ///
    /// EnterSlot 을 같이 불러 줘야 한다. 커서는 "통 위에 있다"는 신호가 있어야 도구 그림을
    /// 띄우는데, 그 신호는 재료통에 붙은 SlotHover 가 보낸다. 면 바구니는 투명한 자리라
    /// SlotHover 가 없어서, 이걸 안 부르면 커서가 시스템 화살표 그대로 남는다.
    ///
    /// 터는 모션은 켜지 않는다. 대기 중에 저 혼자 흔들리면 이미 쥔 것처럼 보인다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CookingCursor.Instance == null) return;

        CookingCursor.Instance.EnterSlot(gameObject);
        CookingCursor.Instance.PreviewTool(type);
    }

    /// <summary>통 밖으로 나가면 터는 것을 멈추고 0번에서 굳는다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (CookingCursor.Instance == null) return;

        CookingCursor.Instance.ExitSlot(gameObject);
        CookingCursor.Instance.SetOverNoodlePot(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 튜토리얼 중에는 안내한 재료 말고는 아예 집히지 않는다(기획서 v1.2 9장).
        if (!TutorialManager.CanPick(type)) return;

        if (CookingCursor.Instance == null)
        {
            Debug.LogWarning("[NoodleSlot] 씬에 커서가 없습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

        delivered = false;
        picking = true;
        CookingCursor.Instance.PickUp(type);

        // 터는 것은 쥔 뒤부터다. 올려놓기만 했을 때는 가만히 있는다.
        CookingCursor.Instance.SetOverNoodlePot(true);
    }

    /// <summary>누를 때 이미 들었다. 눌림을 놓친 경우에만 여기서 든다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        // OnPointerDown 에서 막아도 여기로 다시 들어온다. 눌림이 거부되면 picking 이 false 로
        // 남아 있어서 아래 검사를 그냥 지나친다. 두 곳 다 막아야 한다.
        if (!TutorialManager.CanPick(type)) return;

        if (picking || CookingCursor.Instance == null) return;

        delivered = false;
        picking = true;
        CookingCursor.Instance.PickUp(type);
        CookingCursor.Instance.SetOverNoodlePot(true);
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
        CookingCursor.Instance.SetOverNoodlePot(false);

        // 마우스가 아직 통 위에 있다. 놓자마자 젓가락으로 돌아가 버리면
        // "여기서 뜰 수 있다"는 표시가 사라진다. 통을 벗어나지 않았으니 도구를 다시 씌운다.
        CookingCursor.Instance.PreviewTool(type);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        picking = false;
        if (CookingCursor.Instance != null) CookingCursor.Instance.SetOverNoodlePot(false);

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
