using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님에게 주문을 받는 화면. 씬은 하나지만 화면은 조리 화면과 별개다.
/// 배경이 불투명해 조리 화면을 완전히 가리고, 뒷판이 레이캐스트를 막아 조리 조작도 잠긴다.
/// [조리 시작]을 누르면 이 그룹만 꺼진다. 조리 화면은 그 아래에서 계속 살아 있으므로
/// 따로 시작 신호를 보낼 필요가 없다.
/// </summary>
public class OrderScreenUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private TextMeshProUGUI dayTimeText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button startButton;

    public bool IsOpen
    {
        get { return screenRoot != null && screenRoot.activeSelf; }
    }

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(Close);

        // 이 스크립트는 screenRoot 바깥에 붙어 있어야 한다.
        // 안에 있으면 여기서 자기 자신을 꺼 버려 다시 켤 수 없다.
        Close();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(Close);
    }

    /// <summary>새 손님이 왔을 때 연다.</summary>
    public void Open(int day, string dialogue, int totalRevenue)
    {
        if (screenRoot != null) screenRoot.SetActive(true);

        // 영업 시간은 아직 시스템이 없어 고정 표시다. 기획서 5.4에서 시간 제한은 이번 범위 밖이다.
        if (dayTimeText != null) dayTimeText.text = "영업 시간 " + day + "일차 / 19 : 00";
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";
        if (dialogueText != null) dialogueText.text = dialogue;
    }

    public void Close()
    {
        if (screenRoot != null) screenRoot.SetActive(false);
    }
}
