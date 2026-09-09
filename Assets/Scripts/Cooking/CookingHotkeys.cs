using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조리 화면 단축키. 둘 다 누르고 있는 동안만 뜨고 떼면 사라진다.
/// 손이 바쁜 중에 잠깐 확인하는 용도라, 펼쳐 두고 조리하게 두지 않는다.
///
/// Tab 은 손님 주문 내역(왼쪽에서 미끄러져 나옴), B 는 기본 레시피(아래에서 올라옴)다.
///
/// 조리 중이 아닐 때는 둘 다 안 먹는다. 주문을 듣는 중이거나 결과창이 떠 있을 때
/// 주문서가 튀어나오면 화면이 겹쳐 보이고, 애초에 그때는 볼 이유가 없다.
///
/// 키 입력은 장치에서 바로 읽는다. Tab 은 UI 포커스 이동에도 쓰이는 키라
/// 이벤트 시스템을 거치면 다른 곳에서 먹힐 수 있다.
/// </summary>
public class CookingHotkeys : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private OrderNoteUI orderNote;
    [SerializeField] private RecipeBookUI recipeBook;
    [SerializeField] private OrderManager orderManager;

    /// <summary>주문을 듣는 화면. 이게 떠 있으면 아직 조리 시작 전이다.</summary>
    [SerializeField] private OrderScreenUI orderScreen;

    /// <summary>제출 결과창. 이게 떠 있으면 이미 제출한 뒤다.</summary>
    [SerializeField] private OrderResultUI orderResult;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 조리 중이 아니게 된 순간(제출했거나 다음 손님이 왔거나) 열려 있던 것을 닫는다.
        // 키를 누른 채로 제출하면 뗄 기회가 없어 그대로 남는다.
        if (!IsCooking)
        {
            if (orderNote != null && orderNote.IsOpen) orderNote.Hide();
            if (recipeBook != null && recipeBook.IsOpen) recipeBook.Hide();
            return;
        }

        if (orderNote != null)
        {
            if (keyboard.tabKey.wasPressedThisFrame) orderNote.Show(CurrentDialogue);
            if (keyboard.tabKey.wasReleasedThisFrame) orderNote.Hide();
        }

        if (recipeBook != null)
        {
            if (keyboard.bKey.wasPressedThisFrame) recipeBook.Show();
            if (keyboard.bKey.wasReleasedThisFrame) recipeBook.Hide();
        }
    }

    /// <summary>
    /// 지금이 조리하는 중인가. 조리 시작(주문 화면이 닫힌 때)부터 제출(결과창이 뜬 때)까지다.
    /// </summary>
    private bool IsCooking
    {
        get
        {
            if (orderScreen != null && orderScreen.IsOpen) return false;
            if (orderResult != null && orderResult.IsOpen) return false;
            return true;
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
