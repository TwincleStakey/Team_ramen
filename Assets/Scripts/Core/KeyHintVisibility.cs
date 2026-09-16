using UnityEngine;

/// <summary>
/// Tab·B 안내 아이콘을 조리 화면에서만 보여 준다.
///
/// 두 키는 CookingHotkeys 가 "조리 중일 때만" 받는다. 주문 화면에서도 아이콘이 떠 있으면
/// 눌러도 아무 일이 없어서, 있는 기능이 고장 난 것처럼 보인다.
///
/// 오브젝트를 껐다 켜지 않고 CanvasGroup 의 투명도만 건드린다. 자기가 붙어 있는 오브젝트를
/// SetActive(false) 로 끄면 Update 가 같이 멈춰서, 다시 켤 기회가 영영 오지 않는다.
/// (실제로 그렇게 만들었다가 아이콘이 한 번 사라진 뒤로 안 돌아왔다.)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class KeyHintVisibility : MonoBehaviour
{
    private CanvasGroup group;
    private OrderScreenUI orderScreen;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (orderScreen == null) orderScreen = FindFirstObjectByType<OrderScreenUI>();
        if (group == null) return;

        bool cooking = orderScreen == null || !orderScreen.IsOpen;
        group.alpha = cooking ? 1f : 0f;

        // 아이콘을 눌러도 열리게 되면서 레이캐스트도 같이 끊어야 한다.
        // 투명도만 0 으로 두면 안 보이는 아이콘이 그 자리에서 클릭을 계속 받아,
        // 주문 화면에서 손님 얼굴 옆을 눌렀는데 주문서가 튀어나온다.
        group.blocksRaycasts = cooking;
    }
}
