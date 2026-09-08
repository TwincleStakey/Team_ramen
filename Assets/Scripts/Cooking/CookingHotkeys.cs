using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조리 화면 단축키.
/// Tab은 누르고 있는 동안만 주문 내역이 뜬다. 손이 바쁜 중에 잠깐 확인하는 용도다.
/// B는 레시피 책을 여닫는다. 펼쳐 두고 조리해도 되므로 토글이다.
///
/// 키 입력은 장치에서 바로 읽는다. Tab은 UI 포커스 이동에도 쓰이는 키라
/// 이벤트 시스템을 거치면 다른 곳에서 먹힐 수 있다.
/// </summary>
public class CookingHotkeys : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private OrderNoteUI orderNote;
    [SerializeField] private RecipeBookUI recipeBook;
    [SerializeField] private OrderManager orderManager;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (orderNote != null)
        {
            if (keyboard.tabKey.wasPressedThisFrame) orderNote.Show(CurrentDialogue);
            if (keyboard.tabKey.wasReleasedThisFrame) orderNote.Hide();
        }

        if (recipeBook != null && keyboard.bKey.wasPressedThisFrame)
        {
            recipeBook.Toggle();
        }
    }

    private string CurrentDialogue
    {
        get
        {
            if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();
            return orderManager != null ? orderManager.CurrentDialogue : "";
        }
    }
}
