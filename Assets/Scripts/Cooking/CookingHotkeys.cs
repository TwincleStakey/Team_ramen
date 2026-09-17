using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조리 화면 단축키. 한 번 누를 때마다 여닫는 토글이다.
/// 붙잡고 있어야 하면 한 손이 묶여서 보면서 조리를 못 한다.
///
/// 상단바의 Tab·B 아이콘을 눌러도 같은 자리로 들어온다(ToggleOrderNote / ToggleRecipeBook).
/// 키를 모르는 사람은 아이콘이 떠 있어도 누를 생각을 못 한다.
///
/// Tab 은 손님 주문 내역(오른쪽에서 나옴), B 는 기본 레시피(왼쪽에서 나옴)다. 좌우로 갈라 둬서
/// 둘을 같이 펴 놓아도 안 겹친다.
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

        // 해금 연출이 도는 동안에는 아무것도 열리지 않는다. 마우스는 연출이 깔아 둔 투명 판이
        // 막지만 키는 장치에서 바로 읽으므로 여기서 막아야 한다.
        if (UnlockSequence.IsPlaying) return;

        // 조리 중이 아니게 된 순간(제출했거나 다음 손님이 왔거나) 열려 있던 것을 닫는다.
        // 키를 누른 채로 제출하면 뗄 기회가 없어 그대로 남는다.
        if (!IsCooking)
        {
            if (orderNote != null && orderNote.IsOpen) orderNote.Hide();
            if (recipeBook != null && recipeBook.IsOpen) recipeBook.Hide();
            return;
        }

        // 누르고 있는 동안이 아니라 한 번 누를 때마다 여닫는다.
        // 붙잡고 있으면 한 손이 묶여서 보면서 조리를 못 한다.
        if (keyboard.tabKey.wasPressedThisFrame) ToggleOrderNote();
        if (keyboard.bKey.wasPressedThisFrame) ToggleRecipeBook();
    }

    /// <summary>
    /// 주문서를 여닫는다. Tab 키와 상단바 아이콘이 같이 쓴다.
    ///
    /// 아이콘 쪽에서도 이 자리를 거쳐야 「조리 중일 때만」 판정이 한 곳에만 남는다.
    /// 아이콘이 따로 열면 주문 화면 위로 주문서가 튀어나온다.
    /// </summary>
    public void ToggleOrderNote()
    {
        if (!IsCooking || orderNote == null) return;
        orderNote.Toggle(CurrentDialogue);
    }

    /// <summary>레시피북을 여닫는다. B 키와 상단바 아이콘이 같이 쓴다.</summary>
    public void ToggleRecipeBook()
    {
        if (!IsCooking || recipeBook == null) return;

        if (recipeBook.IsOpen) recipeBook.Hide();
        else recipeBook.Show();
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
