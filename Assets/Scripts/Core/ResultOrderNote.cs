using TMPro;
using UnityEngine;

/// <summary>
/// 결과창 가운데에 붙박이로 서는 주문서. 그릇과 견주어 보라고 정확도 판 옆에 같이 띄운다.
///
/// Tab 으로 여는 <see cref="OrderNoteUI"/> 와 종이는 같지만 다루는 법이 다르다. 그쪽은
/// 위에서 미끄러져 내려오고, 마우스를 올리면 흐려지고, 조리 중이 아니면 CookingHotkeys 가
/// 바로 닫는다 — 결과창이 뜬 순간이 곧 "조리 중이 아닌" 때라 그 한 벌을 여기로 끌어다
/// 쓸 수 없다. 그래서 종이만 같고 움직임이 없는 것을 따로 세운다.
///
/// 종이 높이는 대사 길이에 맞춰 매번 다시 잡는다. 피벗이 가운데라 위아래로 똑같이 자라,
/// 주문이 길든 짧든 그릇·정확도 판과 세로 가운데가 맞는다.
/// </summary>
public class ResultOrderNote : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private RectTransform paper;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI dayLabel;
    [SerializeField] private TextMeshProUGUI customerLabel;

    /// <summary>종이에서 글자를 뺀 나머지 높이. 머리글·구분선·정보줄을 다 더한 값이다.</summary>
    [SerializeField] private float chromeHeight = 73f;

    /// <summary>글자가 쓸 수 있는 폭. 이 폭으로 재야 실제 줄 수가 나온다.</summary>
    [SerializeField] private float textWidth = 156f;

    [SerializeField] private DayManager dayManager;
    [SerializeField] private OrderManager orderManager;

    /// <summary>지금 손님의 주문을 종이에 옮기고 길이를 맞춘다. 결과창이 열릴 때 부른다.</summary>
    public void Show()
    {
        if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();

        string dialogue = orderManager != null ? orderManager.CurrentDialogue : "";
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrEmpty(dialogue) ? "(받은 주문이 없습니다)" : dialogue;
        }

        WriteHeader();
        FitPaper();
    }

    /// <summary>
    /// 영수증 머리의 정보줄. 조리 중에 보던 주문서와 같은 번호가 떠야 한다.
    ///
    /// 손님 수가 오르는 것은 [확인]을 누른 뒤다(GameManager.AdvanceCustomer). 결과창이
    /// 떠 있는 동안은 아직 안 낸 것으로 세므로 Tab 주문서와 똑같이 +1 한다.
    /// </summary>
    private void WriteHeader()
    {
        if (dayManager == null) return;

        if (dayLabel != null) dayLabel.text = dayManager.CurrentDay + "일차";
        if (customerLabel != null) customerLabel.text = (dayManager.CurrentCustomerCount + 1) + "번째 손님";
    }

    /// <summary>대사 길이에 맞춰 종이 높이를 잡는다. 가운데를 쥐고 위아래로 자란다.</summary>
    private void FitPaper()
    {
        if (dialogueText == null || paper == null) return;

        float content = Mathf.Ceil(dialogueText.GetPreferredValues(textWidth, 0f).y);
        dialogueText.rectTransform.sizeDelta = new Vector2(textWidth, content);

        // 픽셀아트라 반 칸에 놓이면 테두리와 글자가 흐려진다. 짝수로 끊는다.
        float height = Mathf.Ceil((chromeHeight + content) * 0.5f) * 2f;

        paper.sizeDelta = new Vector2(paper.sizeDelta.x, height);
    }
}
