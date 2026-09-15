using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 누르는 동안 버튼 판을 아래로 내려 실제로 눌린 것처럼 보이게 한다.
/// 색만 어두워지는 기본 반응은 픽셀아트 판 위에서 거의 읽히지 않는다.
/// RamenLayoutBuilder가 StyleButton에서 모든 버튼에 붙인다.
/// </summary>
public class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    /// <summary>눌렸을 때 내려가는 양. 픽셀 격자가 어긋나지 않게 아트 배율(4배)의 배수로 둔다.</summary>
    public float pressDepth = 4f;

    private RectTransform rect;
    private Vector2 basePosition;
    private bool pressed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
    }

    private void OnDisable()
    {
        // 눌린 채로 꺼지면 다시 켤 때 내려간 자리에 그대로 남는다.
        Release();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pressed) return;

        pressed = true;
        rect.anchoredPosition = basePosition + new Vector2(0f, -pressDepth);
        Sfx.Play("sfx_ui_press", 0.6f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 누른 채로 버튼 밖으로 나가면 클릭이 취소된다. 판도 같이 올라와야 한다.
        Release();
    }

    private void Release()
    {
        if (!pressed) return;

        pressed = false;
        if (rect != null) rect.anchoredPosition = basePosition;
    }
}
