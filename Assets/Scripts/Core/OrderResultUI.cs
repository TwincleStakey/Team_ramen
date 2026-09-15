using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 한 명에게 라멘을 낸 직후 뜨는 결과창.
/// 하루 마감 정산(B의 DailyResultUI)과는 다르다. 이건 매 주문마다 뜬다.
///
/// 이 창이 떠 있는 동안은 다음 손님으로 넘어가지 않는다.
/// [확인]을 눌러야 GameManager가 진행을 이어받는다.
/// </summary>
public class OrderResultUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI customerLine;
    [SerializeField] private Image emoji;
    [SerializeField] private Sprite emojiHappy;
    [SerializeField] private Sprite emojiNeutral;
    [SerializeField] private Sprite emojiAngry;
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameManager gameManager;

    /// <summary>결과창이 떠 있는가. 떠 있으면 이미 제출한 뒤라 조리 단축키가 막힌다.</summary>
    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        Close();
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
    }

    public void Open(float accuracy, int reward, int totalRevenue)
    {
        if (root != null) root.SetActive(true);
        Sfx.Play("sfx_ui_result", 0.6f);

        // 튜토리얼에서 감춰 두었을 수 있다. 평범한 손님은 늘 돈을 보여 준다.
        ShowMoney(true);

        if (accuracyText != null) accuracyText.text = "정확도 : " + accuracy.ToString("F0") + "%";
        if (rewardText != null) rewardText.text = "+ " + reward.ToString("N0") + "₩";
        if (revenueText != null) revenueText.text = "누적 수익 : " + totalRevenue.ToString("N0") + "₩";

        // 말투와 표정에 맞는 한마디. 컷신에서 이미 뽑았으면 같은 말이 온다.
        if (customerLine != null) customerLine.text = ReactionLines.For(accuracy);

        // 기획서 7.5 — 90% 이상 웃음 / 70~89% 무표정 / 69% 이하 화남
        if (emoji != null)
        {
            Sprite face = accuracy >= 90f ? emojiHappy
                        : accuracy >= 70f ? emojiNeutral
                        : emojiAngry;
            if (face != null) emoji.sprite = face;
        }
    }

    /// <summary>
    /// 튜토리얼 마무리용. 정확도 창을 띄우되 한마디 자리에 안내를 넣고,
    /// [확인] 뒤에 할 일을 따로 받는다.
    ///
    /// 안내대로만 넣게 해 둬서 그릇은 늘 정답이라 정확도는 100 이다.
    /// 값은 받지 않는다(기획 9.2 — 튜토리얼은 매출·정확도 집계에서 모두 제외).
    /// </summary>
    public void OpenTutorial(int totalRevenue, string message, System.Action confirmed)
    {
        Open(100f, 0, totalRevenue);

        // 보상과 누적 수익 칸은 아예 감춘다. 튜토리얼은 값을 받지도 쌓지도 않아서
        // 0원이 두 줄로 뜨면 뭔가 잘못된 것처럼 보인다. 정확도와 안내만 남긴다.
        ShowMoney(false);
        if (customerLine != null) customerLine.text = message;

        onConfirm = confirmed;
    }

    /// <summary>보상·누적 수익 칸을 여닫는다. 튜토리얼에서만 닫는다.</summary>
    private void ShowMoney(bool visible)
    {
        if (rewardText != null) rewardText.gameObject.SetActive(visible);
        if (revenueText != null) revenueText.gameObject.SetActive(visible);
    }

    /// <summary>[확인] 을 눌렀을 때 대신 할 일. 비어 있으면 평소대로 다음 손님으로 간다.</summary>
    private System.Action onConfirm;

    public void Close()
    {
        // 손님이 갔다. 다음 손님은 반응을 새로 뽑는다.
        ReactionLines.Clear();

        if (root != null) root.SetActive(false);
    }

    private void Confirm()
    {
        Close();

        // 한 번 쓰고 비운다. 남겨 두면 다음 손님의 [확인] 까지 여기로 빠진다.
        if (onConfirm != null)
        {
            System.Action done = onConfirm;
            onConfirm = null;
            done();
            return;
        }

        if (gameManager != null) gameManager.AdvanceCustomer();
    }
}
