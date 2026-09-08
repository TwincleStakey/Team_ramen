using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타래통·육수 냄비·조미료병. 끌지 않고 클릭하면 커서가 그 도구로 바뀐다.
/// 액체는 국자로 뜨고, 조미료는 병째로 든다.
/// 고체 재료 슬롯은 IngredientSlot이 따로 맡는다.
/// </summary>
public class LiquidSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    // 이 통이 담당하는 재료. RamenLayoutBuilder가 생성 시 지정한다.
    public IngredientType type;

    /// <summary>
    /// 올려놓기만 해도 도구가 그 통에 맞게 바뀐다. 무엇을 쥐게 될지 미리 보이게 하려는 것이다.
    /// 실제로 뜨는 것은 클릭이다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CookingCursor.Instance != null) CookingCursor.Instance.PreviewTool(type);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CookingCursor.Instance == null)
        {
            Debug.LogWarning("[LiquidSlot] 씬에 커서가 없습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

        CookingCursor.Instance.PickUp(type);
    }
}
