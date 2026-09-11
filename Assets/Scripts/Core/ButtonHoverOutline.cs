using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스를 얹으면 버튼 둘레에 흰 테두리가 뜬다.
///
/// 유니티 UI 의 Outline 효과를 썼다. 같은 그림을 네 방향으로 한 칸씩 밀어 뒤에 겹쳐 그리는
/// 방식이라, 어떤 모양의 버튼이든 그 실루엣을 그대로 따라간다. 버튼마다 테두리 그림을
/// 따로 구울 필요가 없다.
///
/// 평소에는 꺼 두고 얹었을 때만 켠다. 켜 둔 채로 색만 바꾸면 안 얹었을 때도 흐릿한 테가 남는다.
/// </summary>
[RequireComponent(typeof(Outline))]
public class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Outline outline;
    private Selectable target;

    private void Awake()
    {
        outline = GetComponent<Outline>();
        target = GetComponent<Selectable>();

        if (outline != null) outline.enabled = false;
    }

    /// <summary>
    /// 꺼질 때는 테두리도 같이 끈다. 버튼 위에 마우스를 둔 채로 화면이 닫히면
    /// 나갈 때(OnPointerExit)가 오지 않아 테두리가 켜진 채로 남는다.
    /// </summary>
    private void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target != null && !target.interactable) return;
        if (outline != null) outline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (outline != null) outline.enabled = false;
    }
}
