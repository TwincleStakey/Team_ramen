using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타래통·육수 냄비·조미료병. 끌지 않고 클릭하면 커서가 그 도구로 바뀐다.
/// 액체는 국자로 뜨고, 조미료는 병째로 든다.
/// 고체 재료 슬롯은 IngredientSlot이 따로 맡는다.
/// </summary>
public class LiquidSlot : MonoBehaviour, IPointerClickHandler
{
    // 이 통이 담당하는 재료. RamenLayoutBuilder가 생성 시 지정한다.
    public IngredientType type;

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
