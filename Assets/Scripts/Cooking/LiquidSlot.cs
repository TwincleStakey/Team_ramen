using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타래통·육수 냄비·조미료병. 여기서 그릇으로 끌어다 놓아 붓는다.
/// 액체는 국자로 뜨고, 조미료는 병째로 든다.
/// 고체 재료 슬롯은 IngredientSlot이 따로 맡는다.
///
/// 고체와 같은 드래그로 맞춰 두었다. 예전에는 액체만 "통을 클릭해서 들고 → 그릇을 클릭해서 붓기"라
/// 두 손놀림이 섞여 있었다.
///
/// 뜨는 것은 누르는 순간(OnPointerDown)에 시작한다. OnBeginDrag는 마우스가 몇 픽셀 움직여야
/// 오기 때문에, 거기서 시작하면 이미 통 밖으로 끌고 나간 뒤에야 푸는 동작이 나온다.
/// 젓가락(IngredientSlot)도 같은 이유로 누를 때 집는다.
///
/// 국자가 실제로 담기는 것은 푸는 동작이 다 끝난 뒤다(CookingCursor.ScoopRoutine이 바닥에서
/// IsHolding을 켠다). 그래서 뜨는 도중에 그릇에 놓으면 아무것도 안 부어진다.
///
/// 고체와 달리 따라다니는 고스트를 만들지 않는다. 국자·병은 커서 자신이 그 도구로 바뀌므로
/// 그림이 이미 마우스를 따라다닌다. 그래서 OnDrag는 할 일이 없지만, 이 인터페이스가 없으면
/// uGUI가 드래그를 시작하지 않아 그릇의 OnDrop이 영영 안 온다.
/// </summary>
public class LiquidSlot : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler,
                          IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 이 통이 담당하는 재료. RamenLayoutBuilder가 생성 시 지정한다.
    public IngredientType type;

    /// <summary>이번에 뜬 것을 그릇이 받았는가. Bowl.OnDrop이 켜 준다.</summary>
    private bool delivered;

    /// <summary>지금 이 통에서 뜬 것을 들고 있는가. 눌림을 놓쳤는지 판단하는 데 쓴다.</summary>
    private bool picking;

    /// <summary>
    /// 올려놓기만 해도 도구가 그 통에 맞게 바뀐다. 무엇을 쥐게 될지 미리 보이게 하려는 것이다.
    /// 실제로 뜨는 것은 누를 때다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CookingCursor.Instance != null) CookingCursor.Instance.PreviewTool(type);
    }

    /// <summary>누르는 순간 푸는 동작이 시작된다. 다 퍼야 담긴 상태가 된다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 튜토리얼 중에는 안내한 재료 말고는 아예 집히지 않는다(기획서 v1.2 9장).
        if (!TutorialManager.CanPick(type)) return;

        if (CookingCursor.Instance == null)
        {
            Debug.LogWarning("[LiquidSlot] 씬에 커서가 없습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

        delivered = false;
        picking = true;
        CookingCursor.Instance.PickUp(type);
    }

    /// <summary>누를 때 이미 펐다. 눌림을 놓친 경우에만 여기서 뜬다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        // OnPointerDown 에서 막아도 여기로 다시 들어온다. 눌림이 거부되면 picking 이 false 로
        // 남아 있어서 아래 검사를 그냥 지나친다. 두 곳 다 막아야 한다.
        if (!TutorialManager.CanPick(type)) return;

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
    /// 끌지 않고 눌렀다 뗀 경우. 그대로 두면 커서가 국자를 든 채로 남는다.
    ///
    /// 끄는 중이었다면 여기서 손대면 안 된다. uGUI는 뗄 때 OnPointerUp을 먼저 부르고
    /// 그다음에 그릇의 OnDrop을 부르는데, 여기서 내려놓으면 부을 것이 이미 사라진 뒤가 된다.
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

    /// <summary>
    /// 손을 뗐을 때. 그릇 밖이면 들고 있던 것을 내려놓는다.
    ///
    /// 그릇 위였다면 Bowl.OnDrop이 먼저 와서 Deliver까지 끝냈다. 그것까지 여기서 또 내려놓으면
    /// 병이 뿌리는 동작 도중에 끊긴다. 그래서 IsHolding이 아니라 그릇이 켜 준 표시를 본다 —
    /// 뜨는 도중에 놓았을 때도 IsHolding이 꺼져 있어서 둘을 구분하지 못하기 때문이다.
    /// </summary>
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
